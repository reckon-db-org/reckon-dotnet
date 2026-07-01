using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
using Reckon.Dcb;
using Reckon.Health;
using Reckon.Snapshots;
using Reckon.Streams;
using Reckon.Subscriptions;

namespace Reckon;

/// <summary>
/// The entry point to a ReckonDB gateway. One <see cref="ReckonClient"/> wraps a
/// single gRPC channel to a single gateway endpoint. Per-service sub-clients
/// (health, streams, subscriptions, dcb, ...) share that channel; store-bound
/// sub-clients are cheap to construct.
/// </summary>
/// <remarks>
/// This mirrors the reckon-go client: <c>ReckonClient.Connect(addr)</c> is the
/// .NET analogue of <c>reckon.Connect(ctx, addr)</c>.
/// </remarks>
public sealed class ReckonClient : IAsyncDisposable, IDisposable
{
    private readonly GrpcChannel _channel;

    private ReckonClient(GrpcChannel channel) => _channel = channel;

    /// <summary>The underlying gRPC channel. Escape hatch for advanced callers.</summary>
    public GrpcChannel Channel => _channel;

    /// <summary>Gateway-wide health sub-client (not store-bound).</summary>
    public HealthClient Health => new(_channel);

    /// <summary>Stream append/read/watch sub-client, bound to <paramref name="store"/>.</summary>
    public StreamsClient Streams(string store) => new(_channel, store);

    /// <summary>Live + persistent subscription sub-client, bound to <paramref name="store"/>.</summary>
    public SubscriptionsClient Subscriptions(string store) => new(_channel, store);

    /// <summary>Snapshot sub-client, bound to <paramref name="store"/>.</summary>
    public SnapshotsClient Snapshots(string store) => new(_channel, store);

    /// <summary>DCB + CCC consistency sub-client, bound to <paramref name="store"/>.</summary>
    public DcbClient Dcb(string store) => new(_channel, store);

    /// <summary>
    /// Connect to a gateway endpoint (<c>host:port</c>). Establishes the channel
    /// lazily; the first RPC performs the actual connection.
    /// </summary>
    public static ReckonClient Connect(string address, ReckonClientOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var channel = GrpcChannel.ForAddress(BuildUri(address, options), BuildChannelOptions(options));
        return new ReckonClient(channel);
    }

    /// <summary>
    /// Connect and eagerly verify the channel is reachable. Fails fast if the
    /// gateway cannot be dialed within <paramref name="cancellationToken"/>.
    /// </summary>
    public static async Task<ReckonClient> ConnectAsync(
        string address,
        ReckonClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var client = Connect(address, options);
        await client._channel.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return client;
    }

    private static Uri BuildUri(string address, ReckonClientOptions? options)
    {
        // Accept host:port or a full URI; normalise to http:// (insecure) or https://.
        var hostPort = address.Contains("://", StringComparison.Ordinal)
            ? new Uri(address).Authority
            : address;
        var scheme = options?.Insecure == true ? "http" : "https";
        return new Uri($"{scheme}://{hostPort}");
    }

    private static GrpcChannelOptions BuildChannelOptions(ReckonClientOptions? options)
    {
        var channelOptions = new GrpcChannelOptions();
        if (options is null || options.Insecure)
        {
            return channelOptions;
        }

        var ca = options.CaCertificate ?? LoadCa(options.CaCertificatePath);
        if (ca is null && options.ServerNameOverride is null)
        {
            // Default TLS with system roots; no custom handler needed.
            return channelOptions;
        }

        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = options.ServerNameOverride,
                RemoteCertificateValidationCallback = ca is null
                    ? null
                    : (_, cert, _, _) => ValidateAgainstCa(cert, ca),
            },
        };
        channelOptions.HttpHandler = handler;
        return channelOptions;
    }

    private static X509Certificate2? LoadCa(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificateFromFile(path);
#else
        return new X509Certificate2(path);
#endif
    }

    private static bool ValidateAgainstCa(X509Certificate? presented, X509Certificate2 ca)
    {
        if (presented is null)
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(new X509Certificate2(presented));
    }

    /// <summary>Close the channel and release its connections.</summary>
    public void Dispose() => _channel.Dispose();

    /// <inheritdoc cref="Dispose"/>
    public async ValueTask DisposeAsync()
    {
        await _channel.ShutdownAsync().ConfigureAwait(false);
        _channel.Dispose();
    }
}
