using System.Text;
using Reckon;
using Reckon.Dcb;
using Reckon.Streams;

// QuickStart: connect, append + read a stream, then run a DCB uniqueness loop.
//
//   RECKON_GATEWAY=beam01.lab:50051 RECKON_INSECURE=1 dotnet run
//
// Env:
//   RECKON_GATEWAY   host:port of a reckon-gateway (required)
//   RECKON_STORE     store id (default: default_store)
//   RECKON_INSECURE  1 for plaintext (lab); omit for TLS

var gateway = Environment.GetEnvironmentVariable("RECKON_GATEWAY");
if (string.IsNullOrWhiteSpace(gateway))
{
    Console.Error.WriteLine("Set RECKON_GATEWAY=host:port (add RECKON_INSECURE=1 for a plaintext lab gateway).");
    return 1;
}

var store = Environment.GetEnvironmentVariable("RECKON_STORE") ?? "default_store";
var options = new ReckonClientOptions { Insecure = Environment.GetEnvironmentVariable("RECKON_INSECURE") == "1" };

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
await using var client = await ReckonClient.ConnectAsync(gateway, options, cts.Token);

var overview = await client.Health.OverviewAsync(cts.Token);
Console.WriteLine($"connected: node={overview.Node} status={overview.Status}");

// --- Streams: append then read ---
var streams = client.Streams(store);
var streamId = $"quickstart-{Guid.NewGuid():N}";
var append = await streams.AppendAsync(streamId, StreamState.NoStream, new[]
{
    new ProposedEvent("user_registered_v1", Encoding.UTF8.GetBytes("""{"name":"Ada"}""")),
    new ProposedEvent("user_promoted_v1", Encoding.UTF8.GetBytes("""{"role":"admin"}"""), Tags: new[] { "role:admin" }),
}, cts.Token);
Console.WriteLine($"appended {append.Count} events; head at v{append.Version}");

foreach (var e in await streams.ReadAsync(streamId, 0, 100, cts.Token))
{
    Console.WriteLine($"  v{e.Version,-3} {e.EventType,-24} {Encoding.UTF8.GetString(e.Data.Span)}");
}

// --- DCB: cross-stream uniqueness ---
var dcb = client.Dcb(store);
var tag = $"quickstart-slot:{Guid.NewGuid():N}";
var filter = DcbFilter.MatchAny(tag);

var context = await dcb.ReadAsync(filter, cancellationToken: cts.Token);
var first = await dcb.AppendAsync(filter, context.MaxSeq, new[]
{
    new ProposedEvent("slot_reserved_v1", Encoding.UTF8.GetBytes("{}"), Tags: new[] { tag }),
}, cts.Token);
Console.WriteLine($"dcb first append committed={first.IsCommitted}");

var second = await dcb.AppendAsync(filter, DcbClient.NothingObserved, new[]
{
    new ProposedEvent("slot_reserved_v1", Encoding.UTF8.GetBytes("{}"), Tags: new[] { tag }),
}, cts.Token);
Console.WriteLine($"dcb stale re-append committed={second.IsCommitted} (conflict expected)");

return 0;
