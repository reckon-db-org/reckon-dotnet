using Grpc.Net.Client;
using Gw = Reckon.Gateway.V1;

namespace Reckon.Health;

/// <summary>Overall health of a store or of the gateway.</summary>
public enum ReckonHealthStatus
{
    /// <summary>All checks passing.</summary>
    Healthy = 0,

    /// <summary>Serving, but one or more checks are degraded.</summary>
    Degraded = 1,

    /// <summary>Not serving.</summary>
    Unhealthy = 2,
}

/// <summary>Per-store health snapshot returned by <see cref="HealthClient.CheckAsync"/>.</summary>
public sealed record HealthReport(
    ReckonHealthStatus Status,
    IReadOnlyDictionary<string, string> Details);

/// <summary>Gateway-wide health snapshot returned by <see cref="HealthClient.OverviewAsync"/>.</summary>
public sealed record HealthOverview(
    ReckonHealthStatus Status,
    IReadOnlyDictionary<string, uint> Stores,
    uint TotalWorkers,
    string Node,
    long Timestamp);

/// <summary>
/// Gateway-wide health sub-client. Not store-bound; construct via
/// <see cref="ReckonClient.Health"/>.
/// </summary>
public sealed class HealthClient
{
    private readonly Gw.HealthService.HealthServiceClient _grpc;

    internal HealthClient(GrpcChannel channel) => _grpc = new(channel);

    /// <summary>Check the health of a single store.</summary>
    public async Task<HealthReport> CheckAsync(string storeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);
        var response = await _grpc
            .CheckAsync(new Gw.HealthCheckRequest { StoreId = storeId }, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new HealthReport(
            (ReckonHealthStatus)(int)response.Status,
            response.Details);
    }

    /// <summary>Gateway-wide health across every store the gateway fronts.</summary>
    public async Task<HealthOverview> OverviewAsync(CancellationToken cancellationToken = default)
    {
        var response = await _grpc
            .HealthAsync(new Gw.HealthRequest(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new HealthOverview(
            (ReckonHealthStatus)(int)response.Status,
            response.Stores,
            response.TotalWorkers,
            response.Node,
            response.Timestamp);
    }
}
