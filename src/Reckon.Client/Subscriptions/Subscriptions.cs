using Reckon.Streams;

namespace Reckon.Subscriptions;

/// <summary>What a subscription selects on. The <c>selector</c> passed to
/// the sub-client is interpreted against this type.</summary>
public enum SubscriptionType
{
    /// <summary>Unset. Do not use.</summary>
    Unspecified = 0,

    /// <summary>Selector is a stream id.</summary>
    Stream = 1,

    /// <summary>Selector is an event type name.</summary>
    EventType = 2,

    /// <summary>Selector is an event-type glob/pattern.</summary>
    EventPattern = 3,

    /// <summary>Selector matches on event payload.</summary>
    EventPayload = 4,

    /// <summary>Selector is a tag.</summary>
    Tags = 5,
}

/// <summary>An event delivered over a subscription, with its checkpoint position.</summary>
public sealed record SubscriptionEnvelope(RecordedEvent Event, ulong Checkpoint);

/// <summary>Metadata about a persistent subscription.</summary>
public sealed record SubscriptionDescriptor(
    string Id,
    SubscriptionType Type,
    string Selector,
    string Name,
    long CreatedAt,
    uint PoolSize,
    ulong Checkpoint);

/// <summary>How far a subscription trails the stream head.</summary>
public sealed record SubscriptionLag(ulong Lag, ulong CurrentCheckpoint, ulong LatestVersion);
