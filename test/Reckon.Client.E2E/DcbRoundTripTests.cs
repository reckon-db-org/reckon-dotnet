using System.Text;
using Reckon;
using Reckon.Dcb;
using Reckon.Streams;
using Xunit;

namespace Reckon.Client.E2E;

/// <summary>
/// M3 smoke test: the DCB read-decide-append loop demonstrating a cross-stream
/// uniqueness invariant. Gated on <c>RECKON_GATEWAY</c>; requires a DCB-capable
/// backing (reckon-db 3.1.1+). Honours <c>RECKON_STORE</c> / <c>RECKON_INSECURE</c>.
/// </summary>
public sealed class DcbRoundTripTests
{
    private static string? Gateway => Environment.GetEnvironmentVariable("RECKON_GATEWAY");
    private static string Store => Environment.GetEnvironmentVariable("RECKON_STORE") ?? "default_store";

    private static ReckonClientOptions Options => new()
    {
        Insecure = Environment.GetEnvironmentVariable("RECKON_INSECURE") == "1",
    };

    [Fact]
    public async Task First_append_commits_then_stale_append_conflicts()
    {
        if (string.IsNullOrWhiteSpace(Gateway))
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client = await ReckonClient.ConnectAsync(Gateway, Options, cts.Token);
        var dcb = client.Dcb(Store);

        var tag = $"reckon-dotnet-e2e-slot:{Guid.NewGuid():N}";
        var filter = DcbFilter.MatchAny(tag);

        // Fresh boundary: nothing observed yet.
        var context = await dcb.ReadAsync(filter, cancellationToken: cts.Token);
        Assert.Empty(context.Events);
        Assert.Equal(DcbClient.NothingObserved, context.MaxSeq);

        // First reservation commits.
        var first = await dcb.AppendAsync(filter, context.MaxSeq, new[]
        {
            new ProposedEvent("slot_reserved_v1", Encoding.UTF8.GetBytes("{}"), Tags: new[] { tag }),
        }, cts.Token);
        Assert.True(first.IsCommitted);
        Assert.NotNull(first.Committed);

        // A second reservation against the same tag with a stale cutoff conflicts.
        var second = await dcb.AppendAsync(filter, DcbClient.NothingObserved, new[]
        {
            new ProposedEvent("slot_reserved_v1", Encoding.UTF8.GetBytes("{}"), Tags: new[] { tag }),
        }, cts.Token);
        Assert.False(second.IsCommitted);
        Assert.NotNull(second.Conflict);
        Assert.Equal(first.Committed!.LastSeq, second.Conflict!.MaxSeq);
    }
}
