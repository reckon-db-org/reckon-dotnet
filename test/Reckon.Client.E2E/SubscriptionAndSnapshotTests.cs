using System.Text;
using Reckon;
using Reckon.Snapshots;
using Reckon.Subscriptions;
using Xunit;

namespace Reckon.Client.E2E;

/// <summary>
/// M2 smoke tests: persistent-subscription lifecycle and snapshot round-trip.
/// Gated on <c>RECKON_GATEWAY</c>; honours <c>RECKON_STORE</c> and
/// <c>RECKON_INSECURE</c>.
/// </summary>
public sealed class SubscriptionAndSnapshotTests
{
    private static string? Gateway => Environment.GetEnvironmentVariable("RECKON_GATEWAY");
    private static string Store => Environment.GetEnvironmentVariable("RECKON_STORE") ?? "default_store";

    private static ReckonClientOptions Options => new()
    {
        Insecure = Environment.GetEnvironmentVariable("RECKON_INSECURE") == "1",
    };

    [Fact]
    public async Task Persistent_subscription_lifecycle()
    {
        if (string.IsNullOrWhiteSpace(Gateway))
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client = await ReckonClient.ConnectAsync(Gateway, Options, cts.Token);
        var subs = client.Subscriptions(Store);

        var name = $"reckon-dotnet-e2e-{Guid.NewGuid():N}";
        var id = await subs.CreateAsync(SubscriptionType.EventType, "user_registered_v1", name, cancellationToken: cts.Token);
        Assert.False(string.IsNullOrEmpty(id));

        try
        {
            var all = await subs.ListAsync(cts.Token);
            Assert.Contains(all, s => s.Name == name);

            var got = await subs.GetAsync(name, cts.Token);
            Assert.Equal(name, got.Name);
            Assert.Equal(SubscriptionType.EventType, got.Type);
        }
        finally
        {
            await subs.RemoveAsync(SubscriptionType.EventType, "user_registered_v1", name, cts.Token);
        }
    }

    [Fact]
    public async Task Snapshot_record_read_delete()
    {
        if (string.IsNullOrWhiteSpace(Gateway))
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client = await ReckonClient.ConnectAsync(Gateway, Options, cts.Token);
        var snapshots = client.Snapshots(Store);

        var source = $"reckon-dotnet-e2e-src-{Guid.NewGuid():N}";
        var stream = $"reckon-dotnet-e2e-{Guid.NewGuid():N}";
        var payload = Encoding.UTF8.GetBytes("""{"balance":100}""");

        await snapshots.RecordAsync(source, stream, version: 3, payload, cancellationToken: cts.Token);
        try
        {
            Snapshot snap = await snapshots.ReadAsync(source, stream, version: 3, cts.Token);
            Assert.Equal(3ul, snap.Version);
            Assert.Equal(payload, snap.Data.ToArray());
        }
        finally
        {
            await snapshots.DeleteAsync(source, stream, version: 3, cts.Token);
        }
    }
}
