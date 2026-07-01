using Google.Protobuf;
using Grpc.Net.Client;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Snapshots;

/// <summary>
/// Store-bound sub-client for aggregate snapshots. Construct via
/// <see cref="ReckonClient.Snapshots(string)"/>.
/// </summary>
/// <remarks>
/// A snapshot is keyed by a <c>sourceUuid</c> (the projector/reader identity
/// that took it) and a <c>streamUuid</c> (the aggregate stream) at a given
/// <c>version</c>.
/// </remarks>
public sealed class SnapshotsClient
{
    private readonly Gw.SnapshotService.SnapshotServiceClient _grpc;
    private readonly string _store;

    internal SnapshotsClient(GrpcChannel channel, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);
        _grpc = new(channel);
        _store = store;
    }

    /// <summary>Record a snapshot of a stream at a version.</summary>
    public async Task RecordAsync(
        string sourceUuid,
        string streamUuid,
        ulong version,
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> metadata = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUuid);
        await _grpc.RecordSnapshotAsync(
            new Gw.RecordSnapshotRequest
            {
                StoreId = _store,
                SourceUuid = sourceUuid,
                StreamUuid = streamUuid,
                Version = version,
                Data = ByteString.CopyFrom(data.Span),
                Metadata = ByteString.CopyFrom(metadata.Span),
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Read a snapshot. Pass <paramref name="version"/> 0 for the latest.</summary>
    public async Task<Snapshot> ReadAsync(
        string sourceUuid,
        string streamUuid,
        ulong version = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUuid);
        var record = await _grpc.ReadSnapshotAsync(
            new Gw.ReadSnapshotRequest
            {
                StoreId = _store,
                SourceUuid = sourceUuid,
                StreamUuid = streamUuid,
                Version = version,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToSnapshot(record);
    }

    /// <summary>Delete a snapshot at a specific version.</summary>
    public async Task DeleteAsync(
        string sourceUuid,
        string streamUuid,
        ulong version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUuid);
        await _grpc.DeleteSnapshotAsync(
            new Gw.DeleteSnapshotRequest
            {
                StoreId = _store,
                SourceUuid = sourceUuid,
                StreamUuid = streamUuid,
                Version = version,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>List snapshots for a single source/stream pair.</summary>
    public async Task<IReadOnlyList<Snapshot>> ListAsync(
        string sourceUuid,
        string streamUuid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUuid);
        var response = await _grpc.ListSnapshotsAsync(
            new Gw.ListSnapshotsRequest { StoreId = _store, SourceUuid = sourceUuid, StreamUuid = streamUuid },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Map(response);
    }

    /// <summary>List every snapshot on the store.</summary>
    public async Task<IReadOnlyList<Snapshot>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await _grpc.ListAllSnapshotsAsync(
            new Gw.ListAllSnapshotsRequest { StoreId = _store },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Map(response);
    }

    private static IReadOnlyList<Snapshot> Map(Gw.ListSnapshotsResponse response)
    {
        var list = new List<Snapshot>(response.Snapshots.Count);
        foreach (var s in response.Snapshots)
        {
            list.Add(ToSnapshot(s));
        }
        return list;
    }

    private static Snapshot ToSnapshot(Gw.SnapshotRecord s) => new(
        s.StreamId,
        s.Version,
        s.Data.Memory,
        s.Metadata.Memory,
        s.Timestamp,
        s.AnchorHash.Memory);
}
