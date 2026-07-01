using Gw = Reckon.Gateway.V1;

namespace Reckon.Streams;

/// <summary>Shared protobuf → domain mappers reused across sub-clients.</summary>
internal static class WireMapping
{
    internal static RecordedEvent ToRecordedEvent(Gw.RecordedEvent e) => new(
        e.EventId,
        e.EventType,
        e.StreamId,
        e.Version,
        e.Data.Memory,
        e.Metadata.Memory,
        e.Tags,
        e.Timestamp,
        e.EpochUs,
        e.DataContentType,
        e.MetadataContentType);
}
