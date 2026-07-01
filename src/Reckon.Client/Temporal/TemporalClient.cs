using Grpc.Net.Client;
using Reckon.Streams;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Temporal;

/// <summary>
/// Store-bound sub-client for time-based reads. Construct via
/// <see cref="ReckonClient.Temporal(string)"/>. Timestamps are in the same
/// units the store stamps on events (<see cref="RecordedEvent.Timestamp"/>).
/// </summary>
public sealed class TemporalClient
{
    private readonly Gw.TemporalService.TemporalServiceClient _grpc;
    private readonly string _store;

    internal TemporalClient(GrpcChannel channel, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);
        _grpc = new(channel);
        _store = store;
    }

    /// <summary>Read events on a stream up to and including <paramref name="timestamp"/>.</summary>
    public async Task<IReadOnlyList<RecordedEvent>> ReadUntilAsync(
        string streamId,
        long timestamp,
        ulong batchSize = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        var response = await _grpc.ReadUntilAsync(
            new Gw.ReadUntilRequest { StoreId = _store, StreamId = streamId, Timestamp = timestamp, BatchSize = batchSize },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Map(response.Events);
    }

    /// <summary>Read events on a stream between two timestamps (inclusive).</summary>
    public async Task<IReadOnlyList<RecordedEvent>> ReadRangeAsync(
        string streamId,
        long fromTimestamp,
        long toTimestamp,
        ulong batchSize = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        var response = await _grpc.ReadRangeAsync(
            new Gw.ReadRangeRequest
            {
                StoreId = _store,
                StreamId = streamId,
                FromTimestamp = fromTimestamp,
                ToTimestamp = toTimestamp,
                BatchSize = batchSize,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Map(response.Events);
    }

    /// <summary>The stream's version as of <paramref name="timestamp"/> (-1 if before its first event).</summary>
    public async Task<long> VersionAtAsync(string streamId, long timestamp, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        var response = await _grpc.VersionAtAsync(
            new Gw.VersionAtRequest { StoreId = _store, StreamId = streamId, Timestamp = timestamp },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Version;
    }

    private static IReadOnlyList<RecordedEvent> Map(IEnumerable<Gw.RecordedEvent> events)
    {
        var list = new List<RecordedEvent>();
        foreach (var e in events)
        {
            list.Add(WireMapping.ToRecordedEvent(e));
        }
        return list;
    }
}
