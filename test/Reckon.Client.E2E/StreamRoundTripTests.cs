using System.Text;
using Reckon;
using Reckon.Streams;
using Xunit;

namespace Reckon.Client.E2E;

/// <summary>
/// M1 smoke test: append events with optimistic concurrency, then read them
/// back forward. Gated on <c>RECKON_GATEWAY</c> (host:port); set
/// <c>RECKON_STORE</c> to target a store other than <c>default_store</c> and
/// <c>RECKON_INSECURE=1</c> for a plaintext lab endpoint.
/// </summary>
public sealed class StreamRoundTripTests
{
    private static string? Gateway => Environment.GetEnvironmentVariable("RECKON_GATEWAY");
    private static string Store => Environment.GetEnvironmentVariable("RECKON_STORE") ?? "default_store";

    private static ReckonClientOptions Options => new()
    {
        Insecure = Environment.GetEnvironmentVariable("RECKON_INSECURE") == "1",
    };

    [Fact]
    public async Task Append_then_read_forward_round_trips()
    {
        if (string.IsNullOrWhiteSpace(Gateway))
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client = await ReckonClient.ConnectAsync(Gateway, Options, cts.Token);
        var streams = client.Streams(Store);

        // The store validates stream ids as {type}-{id}; use that shape.
        var streamId = $"user-{Guid.NewGuid():N}";
        var append = await streams.AppendAsync(streamId, StreamState.NoStream, new[]
        {
            new ProposedEvent("user_registered_v1", Encoding.UTF8.GetBytes("""{"name":"Ada"}""")),
            new ProposedEvent("user_promoted_v1", Encoding.UTF8.GetBytes("""{"role":"admin"}"""),
                Tags: new[] { "role:admin" }),
        }, cts.Token);

        Assert.Equal(2ul, append.Count);
        Assert.Equal(1ul, append.Version); // zero-based: two events => head at version 1

        var events = await streams.ReadAsync(streamId, fromVersion: 0, maxCount: 100, cts.Token);

        Assert.Equal(2, events.Count);
        Assert.Equal("user_registered_v1", events[0].EventType);
        Assert.Equal("user_promoted_v1", events[1].EventType);
        Assert.Contains("role:admin", events[1].Tags);

        Assert.Equal(1L, await streams.GetVersionAsync(streamId, cts.Token));
    }
}
