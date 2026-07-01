using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reckon;
using Reckon.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration for the ReckonDB client and subscription workers.</summary>
public static class ReckonServiceCollectionExtensions
{
    /// <summary>
    /// Register a singleton <see cref="ReckonClient"/> built from the configured
    /// options. The client owns one gRPC channel and is disposed with the
    /// container.
    /// </summary>
    public static IServiceCollection AddReckonClient(
        this IServiceCollection services,
        Action<ReckonClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.TryAddSingleton(_ =>
        {
            var options = new ReckonClientOptions();
            configure(options);
            if (string.IsNullOrWhiteSpace(options.Address))
            {
                throw new InvalidOperationException("ReckonClientOptions.Address is required (host:port).");
            }
            return ReckonClient.Connect(options.Address!, options);
        });
        return services;
    }

    /// <summary>
    /// Register a background worker that drives <typeparamref name="THandler"/>
    /// from a ReckonDB subscription. Requires <see cref="AddReckonClient"/>.
    /// The handler is resolved from a fresh scope per event. Call once per
    /// subscription; multiple subscriptions with distinct handlers coexist.
    /// </summary>
    public static IServiceCollection AddReckonSubscription<THandler>(
        this IServiceCollection services,
        Action<ReckonSubscriptionOptions> configure)
        where THandler : class, IReckonEventHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ReckonSubscriptionOptions();
        configure(options);
        options.Validate();

        services.TryAddScoped<THandler>();
        services.AddSingleton<IHostedService>(sp => new ReckonSubscriptionService<THandler>(
            sp.GetRequiredService<ReckonClient>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            options,
            sp.GetRequiredService<ILogger<ReckonSubscriptionService<THandler>>>()));
        return services;
    }
}
