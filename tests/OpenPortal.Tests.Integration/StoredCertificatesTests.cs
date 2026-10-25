using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using OpenPortal.Web.Oidc;

namespace OpenPortal.Tests.Integration;

/// <summary>The token certificates a container keeps in its volume (<c>Oidc:CertificatesPath</c>), and their rollover.</summary>
public sealed class StoredCertificatesTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>, IDisposable
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
        again.NotAfter.ShouldBeGreaterThan(DateTime.UtcNow.AddDays(300));
        Directory.GetFiles(_folder).Select(Path.GetFileName).ShouldBe(["signing.pfx"]);
    }

    [Fact]
    public void Signing_and_encryption_get_different_keys()
    {
        using var signing = StoredCertificates.LoadOrCreate(_folder, "signing", X509KeyUsageFlags.DigitalSignature, null);
        using var encryption = StoredCertificates.LoadOrCreate(_folder, "encryption", X509KeyUsageFlags.KeyEncipherment, null);

        encryption.Thumbprint.ShouldNotBe(signing.Thumbprint);
    }

    [Fact]
    public void A_certificate_close_to_its_end_gets_a_successor_that_starts_a_week_later()
    {
        var current = StoredCertificates.LoadKeyRing(_folder, "signing", X509KeyUsageFlags.DigitalSignature, null, DateTimeOffset.UtcNow).Single();
        var later = new DateTimeOffset(current.NotAfter).AddDays(-30);

        var ring = StoredCertificates.LoadKeyRing(_folder, "signing", X509KeyUsageFlags.DigitalSignature, null, later);

        ring.Count.ShouldBe(2);
        var successor = ring.Single(certificate => certificate.Thumbprint != current.Thumbprint);
        successor.NotBefore.ToUniversalTime().ShouldBe(later.AddDays(7).UtcDateTime, TimeSpan.FromMinutes(1));
        successor.HasPrivateKey.ShouldBeTrue();

        // Asking again at the same moment does not create a third.
        StoredCertificates.LoadKeyRing(_folder, "signing", X509KeyUsageFlags.DigitalSignature, null, later).Count.ShouldBe(2);

        // Once the first has expired, only the successor is loaded.
        var afterExpiry = new DateTimeOffset(current.NotAfter).AddDays(1);
        StoredCertificates.LoadKeyRing(_folder, "signing", X509KeyUsageFlags.DigitalSignature, null, afterExpiry)
            .Select(certificate => certificate.Thumbprint)
            .ShouldBe([successor.Thumbprint]);
    }

    [Fact]
    public async Task The_successor_key_is_published_before_it_signs_anything()
    {
        // A key ring whose successor starts in the future, as it is during the week before the switch.
        foreach (var name in new[] { "signing", "encryption" })
        {
            var usage = name == "signing" ? X509KeyUsageFlags.DigitalSignature : X509KeyUsageFlags.KeyEncipherment;
            var current = StoredCertificates.LoadKeyRing(_folder, name, usage, null, DateTimeOffset.UtcNow).Single();
            StoredCertificates.LoadKeyRing(_folder, name, usage, null, DateTimeOffset.UtcNow.AddDays(-7).Add(new DateTimeOffset(current.NotAfter) - DateTimeOffset.UtcNow).AddDays(-30));
        }

        await using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Oidc:UseEphemeralKeys", "false");
            builder.UseSetting("Oidc:CertificatesPath", _folder);
        });

        using var client = host.CreateClient();
        using var jwks = await client.GetAsync("/.well-known/jwks");
        var keys = (await Scenario.JsonAsync(jwks, HttpStatusCode.OK)).GetProperty("keys").EnumerateArray().ToList();

        keys.Count(key => key.GetProperty("use").GetString() == "sig").ShouldBe(2);
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
