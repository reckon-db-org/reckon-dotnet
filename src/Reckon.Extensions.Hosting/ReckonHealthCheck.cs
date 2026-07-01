using Microsoft.Extensions.Diagnostics.HealthChecks;
using Reckon.Health;

namespace Reckon.Extensions.Hosting;

/// <summary>
/// ASP.NET Core health check that reports the gateway's health via
/// <see cref="ReckonClient.Health"/>. Register with <c>AddReckonHealthCheck</c>.
/// </summary>
public sealed class ReckonHealthCheck : IHealthCheck
{
    private readonly ReckonClient _client;

    /// <summary>Create the health check over an existing client.</summary>
    public ReckonHealthCheck(ReckonClient client) => _client = client;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var overview = await _client.Health.OverviewAsync(cancellationToken).ConfigureAwait(false);
            var data = new Dictionary<string, object> { ["node"] = overview.Node, ["totalWorkers"] = overview.TotalWorkers };
            return overview.Status switch
            {
                ReckonHealthStatus.Healthy => HealthCheckResult.Healthy("Gateway healthy.", data),
                ReckonHealthStatus.Degraded => HealthCheckResult.Degraded("Gateway degraded.", data: data),
                _ => HealthCheckResult.Unhealthy("Gateway unhealthy.", data: data),
            };
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Gateway unreachable.", ex);
        }
    }
}
