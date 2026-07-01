using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;
using Reckon.Streams;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Subscriptions;

/// <summary>
/// Store-bound sub-client for live and persistent subscriptions. Construct via
/// <see cref="ReckonClient.Subscriptions(string)"/>.
/// </summary>
public sealed class SubscriptionsClient
{
    private readonly Gw.SubscriptionService.SubscriptionServiceClient _grpc;
    private readonly string _store;

    internal SubscriptionsClient(GrpcChannel channel, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);
        _grpc = new(channel);
        _store = store;
    }

    /// <summary>
    /// Open a push subscription and enumerate events as they arrive. Enumerate
    /// with <c>await foreach</c>; cancel the token to stop. Pass a
    /// <paramref name="subscriptionName"/> to resume a persistent subscription
    /// created with <see cref="CreateAsync"/>, then <see cref="AckAsync"/> each
    /// processed event to advance its checkpoint.
    /// </summary>
    public async IAsyncEnumerable<SubscriptionEnvelope> SubscribeAsync(
        SubscriptionType type,
        string selector,
        string? subscriptionName = null,
        ulong startFrom = 0,
        uint poolSize = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        var request = new Gw.SubscribeRequest
        {
            StoreId = _store,
            Type = (Gw.SubscriptionType)(int)type,
            Selector = selector,
            SubscriptionName = subscriptionName ?? string.Empty,
            StartFrom = startFrom,
            PoolSize = poolSize,
        };

        using var call = _grpc.Subscribe(request, cancellationToken: cancellationToken);
        await foreach (var e in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new SubscriptionEnvelope(WireMapping.ToRecordedEvent(e.Event), e.Checkpoint);
        }
    }

    /// <summary>Subscribe to a single stream.</summary>
    public IAsyncEnumerable<SubscriptionEnvelope> SubscribeToStreamAsync(
        string streamId, string? subscriptionName = null, ulong startFrom = 0,
        CancellationToken cancellationToken = default) =>
        SubscribeAsync(SubscriptionType.Stream, streamId, subscriptionName, startFrom, 0, cancellationToken);

    /// <summary>Subscribe to all events of a given type across the store.</summary>
    public IAsyncEnumerable<SubscriptionEnvelope> SubscribeToEventTypeAsync(
        string eventType, string? subscriptionName = null, ulong startFrom = 0, uint poolSize = 0,
        CancellationToken cancellationToken = default) =>
        SubscribeAsync(SubscriptionType.EventType, eventType, subscriptionName, startFrom, poolSize, cancellationToken);

    /// <summary>Acknowledge a processed event, advancing the persistent subscription's checkpoint.</summary>
    public async Task AckAsync(
        string streamId,
        string subscriptionName,
        ulong eventNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        await _grpc.AckEventAsync(
            new Gw.AckEventRequest
            {
                StoreId = _store,
                StreamId = streamId ?? string.Empty,
                SubscriptionName = subscriptionName,
                EventNumber = eventNumber,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Create a persistent subscription with a durable checkpoint. Returns its id.</summary>
    public async Task<string> CreateAsync(
        SubscriptionType type,
        string selector,
        string subscriptionName,
        ulong startFrom = 0,
        uint poolSize = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        var response = await _grpc.CreateSubscriptionAsync(
            new Gw.CreateSubscriptionRequest
            {
                StoreId = _store,
                Type = (Gw.SubscriptionType)(int)type,
                Selector = selector,
                SubscriptionName = subscriptionName,
                StartFrom = startFrom,
                PoolSize = poolSize,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.SubscriptionId;
    }

    /// <summary>Remove a persistent subscription.</summary>
    public async Task RemoveAsync(
        SubscriptionType type,
        string selector,
        string subscriptionName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        await _grpc.RemoveSubscriptionAsync(
            new Gw.RemoveSubscriptionRequest
            {
                StoreId = _store,
                Type = (Gw.SubscriptionType)(int)type,
                Selector = selector,
                SubscriptionName = subscriptionName,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>List every persistent subscription on the store.</summary>
    public async Task<IReadOnlyList<SubscriptionDescriptor>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await _grpc.ListSubscriptionsAsync(
            new Gw.ListSubscriptionsRequest { StoreId = _store },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<SubscriptionDescriptor>(response.Subscriptions.Count);
        foreach (var s in response.Subscriptions)
        {
            list.Add(ToDescriptor(s));
        }
        return list;
    }

    /// <summary>Fetch one persistent subscription by name.</summary>
    public async Task<SubscriptionDescriptor> GetAsync(string subscriptionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        var info = await _grpc.GetSubscriptionAsync(
            new Gw.GetSubscriptionRequest { StoreId = _store, SubscriptionName = subscriptionName },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToDescriptor(info);
    }

    /// <summary>How far a persistent subscription trails the stream head.</summary>
    public async Task<SubscriptionLag> GetLagAsync(string subscriptionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        var response = await _grpc.GetSubscriptionLagAsync(
            new Gw.GetSubscriptionLagRequest { StoreId = _store, SubscriptionName = subscriptionName },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new SubscriptionLag(response.Lag, response.CurrentCheckpoint, response.LatestVersion);
    }

    private static SubscriptionDescriptor ToDescriptor(Gw.SubscriptionInfo s) => new(
        s.Id,
        (SubscriptionType)(int)s.Type,
        s.Selector,
        s.SubscriptionName,
        s.CreatedAt,
        s.PoolSize,
        s.Checkpoint);
}
