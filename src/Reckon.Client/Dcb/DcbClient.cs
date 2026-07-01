using Grpc.Net.Client;
using Reckon.Streams;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Dcb;

/// <summary>
/// Store-bound sub-client for the Dynamic Consistency Boundary (DCB) primitive
/// and its payload-keyed CCC read variant. Construct via
/// <see cref="ReckonClient.Dcb(string)"/>.
/// </summary>
/// <remarks>
/// DCB enforces an invariant against a context query ("no event matching this
/// filter has been written since I last looked") instead of a per-stream
/// version. The loop is: <see cref="ReadAsync"/> the context, decide, then
/// <see cref="AppendAsync"/> with the context's <see cref="DcbContext.MaxSeq"/>
/// as the cutoff; a <see cref="DcbConflict"/> means retry.
///
/// CCC (Command Context Consistency) is the same primitive keyed on payload
/// fields rather than tags: <see cref="CccReadByPayloadAsync"/> and
/// <see cref="CccReadByPayloadHashAsync"/> are alternate context reads that feed
/// the same <see cref="AppendAsync"/>. There is no separate CCC service.
/// </remarks>
public sealed class DcbClient
{
    /// <summary>Seq-cutoff sentinel meaning "I have observed no matching events yet".</summary>
    public const long NothingObserved = -1;

    private readonly Gw.DcbService.DcbServiceClient _grpc;
    private readonly string _store;

    internal DcbClient(GrpcChannel channel, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);
        _grpc = new(channel);
        _store = store;
    }

    /// <summary>Read the consistency context matching <paramref name="filter"/>.</summary>
    public async Task<DcbContext> ReadAsync(
        DcbFilter filter,
        ulong batchSize = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var response = await _grpc.ReadDcbContextAsync(
            new Gw.ReadDcbContextRequest
            {
                StoreId = _store,
                TagFilter = filter.ToWire(),
                BatchSize = batchSize,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var events = new List<RecordedEvent>(response.Events.Count);
        foreach (var e in response.Events)
        {
            events.Add(WireMapping.ToRecordedEvent(e));
        }
        return new DcbContext(events, response.MaxSeq);
    }

    /// <summary>
    /// Conditionally append: commits iff no event matching <paramref name="filter"/>
    /// has a seq strictly above <paramref name="seqCutoff"/>. Pass
    /// <see cref="NothingObserved"/> (-1) when the caller has seen nothing, or a
    /// <see cref="DcbContext.MaxSeq"/> from a prior <see cref="ReadAsync"/>.
    /// </summary>
    public async Task<DcbAppendResult> AppendAsync(
        DcbFilter filter,
        long seqCutoff,
        IEnumerable<ProposedEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(events);

        var request = new Gw.AppendIfNoTagMatchesRequest
        {
            StoreId = _store,
            TagFilter = filter.ToWire(),
            SeqCutoff = seqCutoff,
        };
        foreach (var e in events)
        {
            request.Events.Add(WireMapping.ToWire(e));
        }

        var response = await _grpc
            .AppendIfNoTagMatchesAsync(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.ResultCase switch
        {
            Gw.AppendIfNoTagMatchesResponse.ResultOneofCase.Committed =>
                new DcbAppendResult(new DcbCommitted(response.Committed.LastSeq), null),
            Gw.AppendIfNoTagMatchesResponse.ResultOneofCase.Conflict =>
                new DcbAppendResult(null, new DcbConflict(response.Conflict.MaxSeq)),
            _ => throw new InvalidOperationException("DCB append returned no result."),
        };
    }

    /// <summary>
    /// CCC read: events whose JSON payload field <paramref name="key"/> equals
    /// <paramref name="value"/>. Requires a <c>{ccc, key}</c> index on the store.
    /// </summary>
    public async Task<IReadOnlyList<RecordedEvent>> CccReadByPayloadAsync(
        string key,
        string value,
        ulong batchSize = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var response = await _grpc.CccReadByPayloadAsync(
            new Gw.CccReadByPayloadRequest
            {
                StoreId = _store,
                Key = key,
                Value = value ?? string.Empty,
                BatchSize = batchSize,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Map(response.Events);
    }

    /// <summary>
    /// CCC read over a combo hash of several payload fields.
    /// <paramref name="keys"/> and <paramref name="values"/> must be non-empty
    /// and equal length; key order must match the <c>{ccc_hash, keys}</c> index.
    /// </summary>
    public async Task<IReadOnlyList<RecordedEvent>> CccReadByPayloadHashAsync(
        IReadOnlyList<string> keys,
        IReadOnlyList<string> values,
        ulong batchSize = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(values);
        if (keys.Count == 0 || keys.Count != values.Count)
        {
            throw new ArgumentException("keys and values must be non-empty and equal length.", nameof(values));
        }

        var request = new Gw.CccReadByPayloadHashRequest { StoreId = _store, BatchSize = batchSize };
        request.Keys.AddRange(keys);
        request.Values.AddRange(values);

        var response = await _grpc
            .CccReadByPayloadHashAsync(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Map(response.Events);
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
