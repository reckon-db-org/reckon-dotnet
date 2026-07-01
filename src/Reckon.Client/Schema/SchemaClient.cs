using Google.Protobuf;
using Grpc.Net.Client;
using Reckon.Streams;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Schema;

/// <summary>A registered schema for an event type.</summary>
public sealed record SchemaEntry(string EventType, ReadOnlyMemory<byte> Schema, uint Version);

/// <summary>
/// Store-bound sub-client for event-type schema registration and upcasting.
/// Construct via <see cref="ReckonClient.Schema(string)"/>.
/// </summary>
public sealed class SchemaClient
{
    private readonly Gw.SchemaService.SchemaServiceClient _grpc;
    private readonly string _store;

    internal SchemaClient(GrpcChannel channel, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);
        _grpc = new(channel);
        _store = store;
    }

    /// <summary>Register (or bump) the schema for an event type.</summary>
    public async Task RegisterAsync(
        string eventType,
        ReadOnlyMemory<byte> schema,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        await _grpc.RegisterSchemaAsync(
            new Gw.RegisterSchemaRequest { StoreId = _store, EventType = eventType, Schema = ByteString.CopyFrom(schema.Span) },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Remove the schema for an event type.</summary>
    public async Task UnregisterAsync(string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        await _grpc.UnregisterSchemaAsync(
            new Gw.UnregisterSchemaRequest { StoreId = _store, EventType = eventType },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetch the schema registered for an event type.</summary>
    public async Task<SchemaEntry> GetAsync(string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        var response = await _grpc.GetSchemaAsync(
            new Gw.GetSchemaRequest { StoreId = _store, EventType = eventType },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToEntry(response);
    }

    /// <summary>List every registered schema on the store.</summary>
    public async Task<IReadOnlyList<SchemaEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await _grpc.ListSchemasAsync(
            new Gw.ListSchemasRequest { StoreId = _store },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<SchemaEntry>(response.Schemas.Count);
        foreach (var s in response.Schemas)
        {
            list.Add(ToEntry(s));
        }
        return list;
    }

    /// <summary>Current schema version for an event type.</summary>
    public async Task<uint> GetVersionAsync(string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        var response = await _grpc.GetSchemaVersionAsync(
            new Gw.GetSchemaVersionRequest { StoreId = _store, EventType = eventType },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Version;
    }

    /// <summary>Upcast events through their registered schema upcasters.</summary>
    public async Task<IReadOnlyList<RecordedEvent>> UpcastAsync(
        IEnumerable<RecordedEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        var request = new Gw.UpcastEventsRequest { StoreId = _store };
        foreach (var e in events)
        {
            request.Events.Add(WireMapping.ToWireRecorded(e));
        }

        var response = await _grpc
            .UpcastEventsAsync(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var list = new List<RecordedEvent>(response.Events.Count);
        foreach (var e in response.Events)
        {
            list.Add(WireMapping.ToRecordedEvent(e));
        }
        return list;
    }

    private static SchemaEntry ToEntry(Gw.GetSchemaResponse s) =>
        new(s.EventType, s.Schema.Memory, s.Version);
}
