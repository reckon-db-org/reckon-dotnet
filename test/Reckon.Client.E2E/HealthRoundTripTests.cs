using Reckon;
using Reckon.Health;
using Xunit;

namespace Reckon.Client.E2E;

/// <summary>
/// M0 smoke test: connect to a live gateway and read health. Gated on the
/// <c>RECKON_GATEWAY</c> environment variable (host:port) so the suite is inert
/// in environments without a lab gateway. Set <c>RECKON_INSECURE=1</c> for a
/// plaintext lab endpoint.
///
/// <code>
/// RECKON_GATEWAY=beam01.lab:50051 RECKON_INSECURE=1 dotnet test
/// </code>
/// </summary>
public sealed class HealthRoundTripTests
{
    private static string? Gateway => Environment.GetEnvironmentVariable("RECKON_GATEWAY");

    private static ReckonClientOptions Options => new()
    {
        Insecure = Environment.GetEnvironmentVariable("RECKON_INSECURE") == "1",
    };

    [Fact]
    public async Task Connect_and_read_gateway_health_overview()
    {
        if (string.IsNullOrWhiteSpace(Gateway))
        {
            // No lab gateway configured; nothing to exercise.
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client = await ReckonClient.ConnectAsync(Gateway, Options, cts.Token);

        HealthOverview overview = await client.Health.OverviewAsync(cts.Token);

        Assert.False(string.IsNullOrEmpty(overview.Node));
        Assert.InRange(
            (int)overview.Status,
            (int)ReckonHealthStatus.Healthy,
            (int)ReckonHealthStatus.Unhealthy);
    }
}
