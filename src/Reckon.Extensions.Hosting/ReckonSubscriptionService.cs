using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Reckon.Extensions.Hosting;

/// <summary>
/// A background worker that drives an <see cref="IReckonEventHandler"/> from a
/// ReckonDB subscription. This is the "ReckonDB event log → your read model"
/// bridge: ReckonDB owns the log and delivery; your handler owns the projection.
/// </summary>
/// <typeparam name="THandler">The handler resolved (per event, from a scope) to process events.</typeparam>
public sealed class ReckonSubscriptionService<THandler> : BackgroundService
    where THandler : class, IReckonEventHandler
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly ReckonClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReckonSubscriptionOptions _options;
    private readonly ILogger<ReckonSubscriptionService<THandler>> _logger;

    /// <summary>Create the worker. Normally registered via <c>AddReckonSubscription</c>.</summary>
    public ReckonSubscriptionService(
        ReckonClient client,
        IServiceScopeFactory scopeFactory,
        ReckonSubscriptionOptions options,
        ILogger<ReckonSubscriptionService<THandler>> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Reckon subscription on {Store}/{Selector} failed; reconnecting in {Backoff}.",
                    _options.Store, _options.Selector, backoff);
                await Task.Delay(backoff, stoppingToken).ConfigureAwait(false);
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var subscriptions = _client.Subscriptions(_options.Store);
        var stream = subscriptions.SubscribeAsync(
            _options.Type, _options.Selector, _options.SubscriptionName,
            _options.StartFrom, _options.PoolSize, cancellationToken);

        await foreach (var envelope in stream.ConfigureAwait(false))
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
            await handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(_options.SubscriptionName))
            {
                await subscriptions
                    .AckAsync(envelope.Event.StreamId, _options.SubscriptionName!, envelope.Event.Version, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
