using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Stores;

/// <summary>How a store is hosted on a node.</summary>
public enum StoreMode
{
    /// <summary>Unset.</summary>
    Unspecified = 0,

    /// <summary>Single node, no cluster.</summary>
    Single = 1,

    /// <summary>Clustered via Raft.</summary>
    Cluster = 2,
}

/// <summary>Lifecycle transition of a store registration.</summary>
public enum StoreEventType
{
    /// <summary>Unset.</summary>
    Unspecified = 0,

    /// <summary>Store newly registered on a node.</summary>
    Announced = 1,

    /// <summary>Store unregistered (node down or explicit shutdown).</summary>
    Retired = 2,
}

/// <summary>A single (store, node) registration observed by the gateway.</summary>
public sealed record StoreInstance(
    string StoreId,
    string Node,
    StoreMode Mode,
    string DataDir,
    uint TimeoutMs,
    long RegisteredAtUs);

/// <summary>A store registration change delivered by <see cref="StoresClient.WatchAsync"/>.</summary>
public sealed record StoreChange(StoreEventType Type, StoreInstance Instance, long EventAtUs);

/// <summary>
/// Gateway-wide sub-client for store topology discovery. Not store-bound;
/// construct via <see cref="ReckonClient.Stores"/>.
/// </summary>
public sealed class StoresClient
{
    private readonly Gw.StoresService.StoresServiceClient _grpc;

    internal StoresClient(GrpcChannel channel) => _grpc = new(channel);

    /// <summary>Every (store, node) instance the gateway currently sees.</summary>
    public async Task<IReadOnlyList<StoreInstance>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await _grpc
            .ListStoresAsync(new Gw.ListStoresRequest(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Map(response.Instances);
    }

    /// <summary>Instances currently hosting a given store (empty if none).</summary>
    public async Task<IReadOnlyList<StoreInstance>> GetAsync(string storeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);
        var response = await _grpc
            .GetStoreAsync(new Gw.GetStoreRequest { StoreId = storeId }, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Map(response.Instances);
    }

    /// <summary>
    /// Watch store announce/retire events. When <paramref name="includeSnapshot"/>
    /// is true, the current instances are emitted as ANNOUNCED before going live.
    /// </summary>
    public async IAsyncEnumerable<StoreChange> WatchAsync(
        bool includeSnapshot = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var call = _grpc.WatchStores(
            new Gw.WatchStoresRequest { IncludeSnapshot = includeSnapshot },
            cancellationToken: cancellationToken);
        await foreach (var e in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new StoreChange((StoreEventType)(int)e.Type, ToInstance(e.Instance), e.EventAtUs);
        }
    }

    private static IReadOnlyList<StoreInstance> Map(IEnumerable<Gw.StoreInstance> instances)
    {
        var list = new List<StoreInstance>();
        foreach (var i in instances)
        {
            list.Add(ToInstance(i));
        }
        return list;
    }

    private static StoreInstance ToInstance(Gw.StoreInstance i) => new(
        i.StoreId,
        i.Node,
        (StoreMode)(int)i.Mode,
        i.DataDir,
        i.TimeoutMs,
        i.RegisteredAtUs);
}
