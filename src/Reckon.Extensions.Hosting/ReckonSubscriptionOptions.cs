using Reckon.Subscriptions;

namespace Reckon.Extensions.Hosting;

/// <summary>Configures one subscription-driven background worker.</summary>
public sealed class ReckonSubscriptionOptions
{
    /// <summary>Store to subscribe against. Required.</summary>
    public string Store { get; set; } = string.Empty;

    /// <summary>What the <see cref="Selector"/> is interpreted as.</summary>
    public SubscriptionType Type { get; set; } = SubscriptionType.EventType;

    /// <summary>Selector value (event type, stream id, tag, ...). Required.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>
    /// Persistent-subscription name. When set, each processed event is
    /// acknowledged to advance the durable checkpoint; when null, the
    /// subscription is ephemeral (no acking).
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>Checkpoint/version to start from on first run.</summary>
    public ulong StartFrom { get; set; }

    /// <summary>Worker pool size hint (0 lets the server choose).</summary>
    public uint PoolSize { get; set; }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Store, nameof(Store));
        ArgumentException.ThrowIfNullOrWhiteSpace(Selector, nameof(Selector));
    }
}
