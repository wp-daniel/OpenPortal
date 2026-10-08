using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenPortal.Web.Oidc;

/// <summary>
/// Self-signed token certificates kept as PKCS#12 files in a folder, created the first time they are needed.
/// <para>
/// For deployments without a certificate of their own (a container with a volume). Several instances starting
/// at once may each create one: the first file written wins and the others load it, so every instance ends up
/// signing with the same key.
/// </para>
/// </summary>
internal static class StoredCertificates
{
    /// <summary>Long enough to outlive most deployments; replacing it signs every application's users out once.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(5 * 365);

    public static X509Certificate2 LoadOrCreate(string folder, string name, X509KeyUsageFlags usage, string? password)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{name}.pfx");

        if (!File.Exists(path))
        {
            var temporary = Path.Combine(folder, $"{name}.{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(temporary, Create(name, usage).Export(X509ContentType.Pkcs12, password));

            if (!OperatingSystem.IsWindows())
            {
                // Readable by the portal's own account only.
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            try
            {
                File.Move(temporary, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Another instance got there first; use its certificate.
                File.Delete(temporary);
            }
        }

        return X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
    }

    private static X509Certificate2 Create(string name, X509KeyUsageFlags usage)
    {
        using var key = RSA.Create(3072);
        var request = new CertificateRequest($"CN=OpenPortal {name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));

        var now = DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(now.AddMinutes(-5), now.Add(Lifetime));
    }
}
