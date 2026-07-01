namespace Reckon.Streams;

/// <summary>
/// An event to append to a stream. Only <see cref="EventType"/> and
/// <see cref="Data"/> are required; the server assigns an id when
/// <see cref="EventId"/> is null and defaults content types to
/// <c>application/json</c>.
/// </summary>
public sealed record ProposedEvent(
    string EventType,
    ReadOnlyMemory<byte> Data,
    ReadOnlyMemory<byte> Metadata = default,
    IReadOnlyList<string>? Tags = null,
    string? EventId = null,
    string? DataContentType = null,
    string? MetadataContentType = null);

/// <summary>A persisted event read back from a stream.</summary>
public sealed record RecordedEvent(
    string EventId,
    string EventType,
    string StreamId,
    ulong Version,
    ReadOnlyMemory<byte> Data,
    ReadOnlyMemory<byte> Metadata,
    IReadOnlyList<string> Tags,
    long Timestamp,
    long EpochUs,
    string DataContentType,
    string MetadataContentType);

/// <summary>Outcome of a successful append.</summary>
public sealed record AppendResult(ulong Version, ulong Position, ulong Count);
