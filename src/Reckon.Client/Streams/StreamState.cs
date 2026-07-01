namespace Reckon.Streams;

/// <summary>
/// Optimistic-concurrency expectation for <see cref="StreamsClient.AppendAsync"/>.
///
/// Mirrors the reckon-go sentinels (<c>AnyVersion</c>, <c>NoStream</c>,
/// <c>StreamExists</c>) and the EventStoreDB .NET client's <c>StreamState</c>,
/// so .NET event-sourcing developers meet a familiar shape. The wire encoding
/// matches reckon-proto's <c>expected_version</c> constants: NO_STREAM (-1),
/// ANY_VERSION (-2), STREAM_EXISTS (-4).
/// </summary>
public readonly record struct StreamState
{
    private const long NoStreamValue = -1;
    private const long AnyValue = -2;
    private const long StreamExistsValue = -4;

    private StreamState(long value) => Value = value;

    /// <summary>The wire value for <c>expected_version</c>.</summary>
    public long Value { get; }

    /// <summary>No version check: append regardless of the stream's current state.</summary>
    public static StreamState Any => new(AnyValue);

    /// <summary>Assert the stream does not yet exist (append creates it).</summary>
    public static StreamState NoStream => new(NoStreamValue);

    /// <summary>Assert the stream already exists, at any version.</summary>
    public static StreamState StreamExists => new(StreamExistsValue);

    /// <summary>
    /// Assert the stream is at exactly <paramref name="version"/> (zero-based).
    /// The append fails with a concurrency conflict if the stored version differs.
    /// </summary>
    public static StreamState AtVersion(long version) =>
        version < 0
            ? throw new ArgumentOutOfRangeException(nameof(version), version, "Expected version must be non-negative.")
            : new(version);
}
