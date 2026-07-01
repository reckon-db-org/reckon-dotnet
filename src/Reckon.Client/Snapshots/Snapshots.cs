namespace Reckon.Snapshots;

/// <summary>
/// A stored aggregate snapshot. <see cref="AnchorHash"/> is the SHA-256 chain
/// hash of the event at <see cref="Version"/> captured at save time (empty for
/// legacy snapshots), letting a consumer detect post-snapshot mutation.
/// </summary>
public sealed record Snapshot(
    string StreamId,
    ulong Version,
    ReadOnlyMemory<byte> Data,
    ReadOnlyMemory<byte> Metadata,
    long Timestamp,
    ReadOnlyMemory<byte> AnchorHash);
