using System.Runtime.CompilerServices;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Streams;

/// <summary>
/// Store-bound sub-client for appending to and reading from event streams.
/// Construct via <see cref="ReckonClient.Streams(string)"/>; cheap to create,
/// reuses the parent channel.
/// </summary>
public sealed class StreamsClient
{
    private readonly Gw.StreamService.StreamServiceClient _grpc;
    private readonly string _store;

    internal StreamsClient(GrpcChannel channel, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);
        _grpc = new(channel);
        _store = store;
    }

    /// <summary>
    /// Append events to a stream under the given optimistic-concurrency
    /// expectation. Use <see cref="StreamState.Any"/> for no check,
    /// <see cref="StreamState.NoStream"/> to assert the stream is new, or
    /// <see cref="StreamState.AtVersion(long)"/> for an exact check.
    /// </summary>
    public async Task<AppendResult> AppendAsync(
        string streamId,
        StreamState expected,
        IEnumerable<ProposedEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(events);

        var request = new Gw.AppendEventsRequest
        {
            StoreId = _store,
            StreamId = streamId,
            ExpectedVersion = expected.Value,
        };
        foreach (var e in events)
        {
            request.Events.Add(ToWire(e));
        }

        var response = await _grpc
            .AppendEventsAsync(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new AppendResult(response.Version, response.Position, response.Count);
    }

    /// <summary>Read events forward from <paramref name="fromVersion"/>, up to <paramref name="maxCount"/>.</summary>
    public async Task<IReadOnlyList<RecordedEvent>> ReadAsync(
        string streamId,
        ulong fromVersion = 0,
        ulong maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var response = await _grpc
            .ReadStreamForwardAsync(ReadRequest(streamId, fromVersion, maxCount), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return MapEvents(response);
    }

    /// <summary>Read events backward from <paramref name="fromVersion"/>, up to <paramref name="maxCount"/>.</summary>
    public async Task<IReadOnlyList<RecordedEvent>> ReadBackwardAsync(
        string streamId,
        ulong fromVersion,
        ulong maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var response = await _grpc
            .ReadStreamBackwardAsync(ReadRequest(streamId, fromVersion, maxCount), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return MapEvents(response);
    }

    /// <summary>
    /// Live tail: stream events forward as they are appended. Enumerate with
    /// <c>await foreach</c>; cancel the token to stop.
    /// </summary>
    public async IAsyncEnumerable<RecordedEvent> WatchAsync(
        string streamId,
        ulong startVersion = 0,
        ulong count = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var call = _grpc.StreamEventsForward(
            ReadRequest(streamId, startVersion, count),
            cancellationToken: cancellationToken);
        await foreach (var e in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return WireMapping.ToRecordedEvent(e);
        }
    }

    /// <summary>Current version of a stream, or -1 if it does not exist.</summary>
    public async Task<long> GetVersionAsync(string streamId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        var response = await _grpc
            .GetStreamVersionAsync(
                new Gw.GetStreamVersionRequest { StoreId = _store, StreamId = streamId },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Version;
    }

    private Gw.ReadStreamRequest ReadRequest(string streamId, ulong startVersion, ulong count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        return new Gw.ReadStreamRequest
        {
            StoreId = _store,
            StreamId = streamId,
            StartVersion = startVersion,
            Count = count,
        };
    }

    private static IReadOnlyList<RecordedEvent> MapEvents(Gw.ReadStreamResponse response)
    {
        var list = new List<RecordedEvent>(response.Events.Count);
        foreach (var e in response.Events)
        {
            list.Add(WireMapping.ToRecordedEvent(e));
        }
        return list;
    }

    private static Gw.ProposedEvent ToWire(ProposedEvent e)
    {
        var wire = new Gw.ProposedEvent
        {
            EventType = e.EventType,
            Data = ByteString.CopyFrom(e.Data.Span),
            Metadata = ByteString.CopyFrom(e.Metadata.Span),
            EventId = e.EventId ?? string.Empty,
            DataContentType = e.DataContentType ?? string.Empty,
            MetadataContentType = e.MetadataContentType ?? string.Empty,
        };
        if (e.Tags is not null)
        {
            wire.Tags.AddRange(e.Tags);
        }
        return wire;
    }
}
