using System.Security.Cryptography.X509Certificates;

namespace Reckon;

/// <summary>
/// Connection options for a <see cref="ReckonClient"/>.
///
/// The default is TLS with the system certificate roots. Set
/// <see cref="Insecure"/> for a plaintext gateway (lab only), or supply
/// <see cref="CaCertificatePath"/> to trust a private CA.
/// </summary>
public sealed class ReckonClientOptions
{
    /// <summary>
    /// Gateway endpoint as <c>host:port</c> (for example <c>beam01.lab:50051</c>).
    /// Required when the options object is used through dependency injection;
    /// when calling <see cref="ReckonClient.ConnectAsync(string, ReckonClientOptions?, System.Threading.CancellationToken)"/>
    /// the address argument takes precedence.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Use plaintext HTTP/2 instead of TLS. Lab and local development only.
    /// </summary>
    public bool Insecure { get; set; }

    /// <summary>
    /// Path to a PEM CA certificate to trust in place of the system roots.
    /// Ignored when <see cref="Insecure"/> is set.
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Override the server name used for TLS validation (SNI / certificate
    /// subject). Useful when dialing an IP or an internal hostname that does
    /// not match the certificate. Ignored when <see cref="Insecure"/> is set.
    /// </summary>
    public string? ServerNameOverride { get; set; }

    /// <summary>
    /// Explicit CA certificate to trust, as an alternative to
    /// <see cref="CaCertificatePath"/>. Ignored when <see cref="Insecure"/> is set.
    /// </summary>
    public X509Certificate2? CaCertificate { get; set; }
}
