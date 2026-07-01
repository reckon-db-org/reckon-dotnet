namespace Reckon.Admin;

/// <summary>Aggregate counters for a store.</summary>
public sealed record StoreStats(
    ulong TotalStreams,
    ulong TotalEvents,
    ulong TotalSubscriptions,
    ulong TotalSnapshots,
    IReadOnlyDictionary<string, string> Details);

/// <summary>Metadata about a single stream.</summary>
public sealed record StreamInfo(
    string StreamId,
    ulong Version,
    ulong EventCount,
    long CreatedAt,
    long LastEventAt,
    IReadOnlyList<string> EventTypes);

/// <summary>Count of events of one type across a store.</summary>
public sealed record EventTypeCount(string EventType, ulong Count);

/// <summary>Result of a scavenge operation.</summary>
public sealed record ScavengeResult(
    ulong EventsRemoved,
    ulong EventsRemaining,
    ulong SpaceReclaimedBytes,
    IReadOnlyDictionary<string, string> Details);

/// <summary>Static definition of a stream link (projection).</summary>
public sealed record LinkInfo(
    string Name,
    string Source,
    string Target,
    IReadOnlyDictionary<string, string> Options);

/// <summary>Runtime state of a stream link.</summary>
public sealed record LinkRuntimeInfo(
    string Name,
    string Status,
    ulong EventsProcessed,
    IReadOnlyDictionary<string, string> Details);

/// <summary>Result of reloading the gateway's federation catalogue.</summary>
public sealed record CatalogueReload(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Restarted,
    string Error);

/// <summary>Status of one cluster connector inside the catalogue.</summary>
public sealed record CatalogueClusterStatus(
    string ClusterId,
    IReadOnlyList<string> Members,
    int StoreCount,
    string Status,
    string LastRefresh,
    string LastError);

/// <summary>Gateway federation catalogue status.</summary>
public sealed record CatalogueStatus(
    int CatalogueSize,
    long GatewayUptimeMs,
    IReadOnlyList<CatalogueClusterStatus> Clusters);
