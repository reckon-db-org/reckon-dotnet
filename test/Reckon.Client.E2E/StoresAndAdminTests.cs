using Reckon;
using Xunit;

namespace Reckon.Client.E2E;

/// <summary>
/// M4 smoke tests: gateway-wide store discovery and store stats. Gated on
/// <c>RECKON_GATEWAY</c>; honours <c>RECKON_STORE</c> / <c>RECKON_INSECURE</c>.
/// </summary>
public sealed class StoresAndAdminTests
{
    private static string? Gateway => Environment.GetEnvironmentVariable("RECKON_GATEWAY");
    private static string Store => Environment.GetEnvironmentVariable("RECKON_STORE") ?? "default_store";

    private static ReckonClientOptions Options => new()
    {
        Insecure = Environment.GetEnvironmentVariable("RECKON_INSECURE") == "1",
    };

    [Fact]
    public async Task List_stores_and_read_store_stats()
    {
        if (string.IsNullOrWhiteSpace(Gateway))
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client = await ReckonClient.ConnectAsync(Gateway, Options, cts.Token);

        var instances = await client.Stores.ListAsync(cts.Token);
        Assert.NotNull(instances);

        var stats = await client.Admin(Store).GetStoreStatsAsync(cts.Token);
        Assert.NotNull(stats.Details);

        var summary = await client.Admin(Store).GetEventTypeSummaryAsync(cts.Token);
        Assert.NotNull(summary);
    }
}
