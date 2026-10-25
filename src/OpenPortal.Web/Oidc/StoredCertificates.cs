using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenPortal.Web.Oidc;

/// <summary>
/// Self-signed token certificates kept as PKCS#12 files in a folder, created the first time they are needed and
/// rolled over before they expire.
/// <para>
/// For deployments without a certificate of their own (a container with a volume). Several instances starting
/// at once may each create one: the first file written wins and the others load it, so every instance ends up
/// signing with the same key.
/// </para>
/// <para>
/// Rollover: when the newest certificate has less than <see cref="RenewBefore"/> left, a successor is created whose
/// validity starts <see cref="PublishLead"/> later. Every valid certificate is loaded, so the successor's key is in
/// the discovery document (<c>jwks_uri</c>) for that long before anything is signed with it, and applications that
/// cache the keys pick it up in time. OpenIddict signs with the valid certificate that expires last, so the switch
/// happens at the first start after the successor became valid; tokens signed with the old key stay verifiable
/// until it expires.
/// </para>
/// </summary>
internal static class StoredCertificates
{
    /// <summary>How long a certificate is valid. Rolled over automatically, so a year is enough.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    /// <summary>When the newest certificate has less than this left, its successor is created.</summary>
    public static readonly TimeSpan RenewBefore = TimeSpan.FromDays(60);

    /// <summary>How long a successor's key is published before it signs anything.</summary>
    public static readonly TimeSpan PublishLead = TimeSpan.FromDays(7);

    /// <summary>The newest valid certificate (the one in use since the original file name), created when missing.</summary>
    public static X509Certificate2 LoadOrCreate(string folder, string name, X509KeyUsageFlags usage, string? password) =>
        LoadKeyRing(folder, name, usage, password, DateTimeOffset.UtcNow)
            .Where(certificate => certificate.NotBefore.ToUniversalTime() <= DateTime.UtcNow)
            .OrderByDescending(certificate => certificate.NotAfter)
            .First();

    /// <summary>
    /// Every certificate of <paramref name="name"/> that has not expired, creating the first one and, when the newest is
    /// close to its end, its successor. Expired ones are left on disk but not loaded.
    /// </summary>
    public static IReadOnlyList<X509Certificate2> LoadKeyRing(
        string folder,
        string name,
        X509KeyUsageFlags usage,
        string? password,
        DateTimeOffset now)
    {
        Directory.CreateDirectory(folder);

        var valid = LoadValid(folder, name, password, now);

        if (valid.Count == 0)
        {
            Write(folder, $"{name}.pfx", Create(name, usage, now.AddMinutes(-5)), password);
            valid = LoadValid(folder, name, password, now);
        }

        var newest = valid.MaxBy(certificate => certificate.NotAfter)!;
        if (newest.NotAfter.ToUniversalTime() - now.UtcDateTime < RenewBefore)
        {
            // Named by its start, so a second instance doing the same at the same moment writes the same file name.
            var starts = now.Add(PublishLead);
            Write(folder, $"{name}-{starts.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pfx", Create(name, usage, starts), password);
            valid = LoadValid(folder, name, password, now);
        }

        return valid;
    }

    private static List<X509Certificate2> LoadValid(string folder, string name, string? password, DateTimeOffset now)
    {
        var certificates = new List<X509Certificate2>();

        foreach (var path in Directory.GetFiles(folder, $"{name}*.pfx"))
        {
            var file = Path.GetFileNameWithoutExtension(path);
            if (file != name && !file.StartsWith(name + "-", StringComparison.Ordinal))
            {
                continue;
            }

            var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
            // X.509 dates come back in local time.
            if (certificate.NotAfter.ToUniversalTime() > now.UtcDateTime)
            {
                certificates.Add(certificate);
            }
            else
            {
                certificate.Dispose();
            }
        }

        return certificates;
    }

    private static void Write(string folder, string fileName, X509Certificate2 certificate, string? password)
    {
        var path = Path.Combine(folder, fileName);
        if (File.Exists(path))
        {
            certificate.Dispose();
            return;
        }

        var temporary = Path.Combine(folder, $"{fileName}.{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(temporary, certificate.Export(X509ContentType.Pkcs12, password));
        certificate.Dispose();

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

    private static X509Certificate2 Create(string name, X509KeyUsageFlags usage, DateTimeOffset notBefore)
    {
        using var key = RSA.Create(3072);
        var request = new CertificateRequest($"CN=OpenPortal {name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));

        return request.CreateSelfSigned(notBefore, notBefore.Add(Lifetime));
    }
}
