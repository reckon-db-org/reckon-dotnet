using Grpc.Net.Client;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Admin;

/// <summary>
/// Store-bound sub-client for administrative operations: stats, scavenging and
/// stream links. Construct via <see cref="ReckonClient.Admin(string)"/>.
/// </summary>
/// <remarks>
/// The catalogue operations (<see cref="ReloadCatalogueAsync"/>,
/// <see cref="GetCatalogueStatusAsync"/>) are gateway-wide: they ignore the
/// bound store and act on the gateway's federation configuration.
/// </remarks>
public sealed class AdminClient
{
    private readonly Gw.AdminService.AdminServiceClient _grpc;
    private readonly string _store;

    internal AdminClient(GrpcChannel channel, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);
        _grpc = new(channel);
        _store = store;
    }

    /// <summary>Aggregate counters for the store.</summary>
    public async Task<StoreStats> GetStoreStatsAsync(CancellationToken cancellationToken = default)
    {
        var r = await _grpc.GetStoreStatsAsync(
            new Gw.StoreStatsRequest { StoreId = _store }, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new StoreStats(r.TotalStreams, r.TotalEvents, r.TotalSubscriptions, r.TotalSnapshots, r.Details);
    }

    /// <summary>Metadata about a single stream.</summary>
    public async Task<StreamInfo> GetStreamInfoAsync(string streamId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        var r = await _grpc.GetStreamInfoAsync(
            new Gw.StreamInfoRequest { StoreId = _store, StreamId = streamId },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new StreamInfo(r.StreamId, r.Version, r.EventCount, r.CreatedAt, r.LastEventAt, r.EventTypes);
    }

    /// <summary>Per-event-type counts across the store.</summary>
    public async Task<IReadOnlyList<EventTypeCount>> GetEventTypeSummaryAsync(CancellationToken cancellationToken = default)
    {
        var r = await _grpc.GetEventTypeSummaryAsync(
            new Gw.EventTypeSummaryRequest { StoreId = _store }, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<EventTypeCount>(r.Entries.Count);
        foreach (var e in r.Entries)
        {
            list.Add(new EventTypeCount(e.EventType, e.Count));
        }
        return list;
    }

    /// <summary>Scavenge a stream (compact deleted/superseded events).</summary>
    public Task<ScavengeResult> ScavengeAsync(
        string streamId, IReadOnlyDictionary<string, string>? options = null, CancellationToken cancellationToken = default) =>
        ScavengeCore(streamId, options, dryRun: false, cancellationToken);

    /// <summary>Report what a scavenge would remove, without mutating the store.</summary>
    public Task<ScavengeResult> ScavengeDryRunAsync(
        string streamId, IReadOnlyDictionary<string, string>? options = null, CancellationToken cancellationToken = default) =>
        ScavengeCore(streamId, options, dryRun: true, cancellationToken);

    /// <summary>Scavenge every stream whose id matches <paramref name="pattern"/>.</summary>
    public async Task<IReadOnlyList<ScavengeResult>> ScavengeMatchingAsync(
        string pattern,
        IReadOnlyDictionary<string, string>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var request = new Gw.ScavengeMatchingRequest { StoreId = _store, Pattern = pattern };
        AddOptions(request.Options, options);
        var r = await _grpc.ScavengeMatchingAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<ScavengeResult>(r.Results.Count);
        foreach (var res in r.Results)
        {
            list.Add(ToScavengeResult(res));
        }
        return list;
    }

    /// <summary>Create a stream link (projection from source to target).</summary>
    public async Task CreateLinkAsync(
        string name, string source, string target,
        IReadOnlyDictionary<string, string>? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var request = new Gw.CreateLinkRequest { StoreId = _store, Name = name, Source = source ?? string.Empty, Target = target ?? string.Empty };
        AddOptions(request.Options, options);
        await _grpc.CreateLinkAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Delete a stream link.</summary>
    public async Task DeleteLinkAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _grpc.DeleteLinkAsync(
            new Gw.DeleteLinkRequest { StoreId = _store, Name = name }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetch a link's static definition.</summary>
    public async Task<LinkInfo> GetLinkAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var r = await _grpc.GetLinkAsync(
            new Gw.GetLinkRequest { StoreId = _store, Name = name }, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new LinkInfo(r.Name, r.Source, r.Target, r.Options);
    }

    /// <summary>List every link on the store.</summary>
    public async Task<IReadOnlyList<LinkInfo>> ListLinksAsync(CancellationToken cancellationToken = default)
    {
        var r = await _grpc.ListLinksAsync(
            new Gw.ListLinksRequest { StoreId = _store }, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<LinkInfo>(r.Links.Count);
        foreach (var l in r.Links)
        {
            list.Add(new LinkInfo(l.Name, l.Source, l.Target, l.Options));
        }
        return list;
    }

    /// <summary>Start a stopped link.</summary>
    public async Task StartLinkAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _grpc.StartLinkAsync(
            new Gw.StartLinkRequest { StoreId = _store, Name = name }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stop a running link.</summary>
    public async Task StopLinkAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _grpc.StopLinkAsync(
            new Gw.StopLinkRequest { StoreId = _store, Name = name }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runtime state (status, throughput) of a link.</summary>
    public async Task<LinkRuntimeInfo> GetLinkInfoAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var r = await _grpc.GetLinkInfoAsync(
            new Gw.GetLinkRequest { StoreId = _store, Name = name }, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new LinkRuntimeInfo(r.Name, r.Status, r.EventsProcessed, r.Details);
    }

    /// <summary>Gateway-wide: reload the federation catalogue configuration.</summary>
    public async Task<CatalogueReload> ReloadCatalogueAsync(CancellationToken cancellationToken = default)
    {
        var r = await _grpc.ReloadCatalogueAsync(
            new Gw.ReloadCatalogueRequest(), cancellationToken: cancellationToken).ConfigureAwait(false);
        return new CatalogueReload(r.Added, r.Removed, r.Restarted, r.Error);
    }

    /// <summary>Gateway-wide: current federation catalogue status.</summary>
    public async Task<CatalogueStatus> GetCatalogueStatusAsync(CancellationToken cancellationToken = default)
    {
        var r = await _grpc.GetCatalogueStatusAsync(
            new Gw.GetCatalogueStatusRequest(), cancellationToken: cancellationToken).ConfigureAwait(false);
        var clusters = new List<CatalogueClusterStatus>(r.Clusters.Count);
        foreach (var c in r.Clusters)
        {
            clusters.Add(new CatalogueClusterStatus(c.ClusterId, c.Members, c.StoreCount, c.Status, c.LastRefresh, c.LastError));
        }
        return new CatalogueStatus(r.CatalogueSize, r.GatewayUptimeMs, clusters);
    }

    private async Task<ScavengeResult> ScavengeCore(
        string streamId, IReadOnlyDictionary<string, string>? options, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        var request = new Gw.ScavengeRequest { StoreId = _store, StreamId = streamId };
        AddOptions(request.Options, options);
        var r = dryRun
            ? await _grpc.ScavengeDryRunAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false)
            : await _grpc.ScavengeAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToScavengeResult(r);
    }

    private static ScavengeResult ToScavengeResult(Gw.ScavengeResponse r) =>
        new(r.EventsRemoved, r.EventsRemaining, r.SpaceReclaimedBytes, r.Details);

    private static void AddOptions(
        Google.Protobuf.Collections.MapField<string, string> target,
        IReadOnlyDictionary<string, string>? options)
    {
        if (options is null)
        {
            return;
        }
        foreach (var kv in options)
        {
            target[kv.Key] = kv.Value;
        }
    }
}
