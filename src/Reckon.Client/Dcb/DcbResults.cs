using Reckon.Streams;

namespace Reckon.Dcb;

/// <summary>
/// The consistency context for a decision: events matching a filter plus the
/// highest seq observed. Feed <see cref="MaxSeq"/> back as the
/// <c>seqCutoff</c> of a follow-up <see cref="DcbClient.AppendAsync"/>.
/// <see cref="MaxSeq"/> is -1 when no matching events exist.
/// </summary>
public sealed record DcbContext(IReadOnlyList<RecordedEvent> Events, long MaxSeq);

/// <summary>The conditional append succeeded.</summary>
public sealed record DcbCommitted(ulong LastSeq);

/// <summary>
/// The conditional append was rejected: an event matching the filter had a seq
/// above the cutoff. Refresh the context to <see cref="MaxSeq"/> and retry.
/// </summary>
public sealed record DcbConflict(ulong MaxSeq);

/// <summary>
/// Outcome of <see cref="DcbClient.AppendAsync"/>. Exactly one of
/// <see cref="Committed"/> / <see cref="Conflict"/> is non-null. A conflict is
/// normal control flow, not an exception.
/// </summary>
public sealed record DcbAppendResult(DcbCommitted? Committed, DcbConflict? Conflict)
{
    /// <summary>True when the append committed.</summary>
    public bool IsCommitted => Committed is not null;
}
