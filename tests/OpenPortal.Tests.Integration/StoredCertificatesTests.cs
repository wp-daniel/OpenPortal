using System.Security.Cryptography.X509Certificates;
using OpenPortal.Web.Oidc;

namespace OpenPortal.Tests.Integration;

/// <summary>The token certificates a container keeps in its volume (<c>Oidc:CertificatesPath</c>).</summary>
public sealed class StoredCertificatesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"openportal-certs-{Guid.NewGuid():N}");

    [Fact]
    public void A_certificate_is_created_once_and_then_loaded_with_its_private_key()
    {
        using var first = StoredCertificates.LoadOrCreate(_folder, "signing", X509KeyUsageFlags.DigitalSignature, "secret");
        using var again = StoredCertificates.LoadOrCreate(_folder, "signing", X509KeyUsageFlags.DigitalSignature, "secret");

        again.Thumbprint.ShouldBe(first.Thumbprint);
        again.HasPrivateKey.ShouldBeTrue();
        again.Extensions.OfType<X509KeyUsageExtension>().Single().KeyUsages.ShouldBe(X509KeyUsageFlags.DigitalSignature);
        again.NotAfter.ShouldBeGreaterThan(DateTime.UtcNow.AddYears(4));
        Directory.GetFiles(_folder).Select(Path.GetFileName).ShouldBe(["signing.pfx"]);
    }

    [Fact]
    public void Signing_and_encryption_get_different_keys()
    {
        using var signing = StoredCertificates.LoadOrCreate(_folder, "signing", X509KeyUsageFlags.DigitalSignature, null);
        using var encryption = StoredCertificates.LoadOrCreate(_folder, "encryption", X509KeyUsageFlags.KeyEncipherment, null);

        encryption.Thumbprint.ShouldNotBe(signing.Thumbprint);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}
