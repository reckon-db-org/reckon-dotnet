using Google.Protobuf;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Streams;

/// <summary>Shared protobuf mappers reused across sub-clients.</summary>
internal static class WireMapping
{
    internal static Gw.ProposedEvent ToWire(ProposedEvent e)
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
