using System.Security.Cryptography.X509Certificates;
using OpenPortal.Access.Application.Contracts;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenPortal.Web.Oidc;

/// <summary>Settings for the OpenID Connect server, bound from the <c>Oidc</c> configuration section.</summary>
public sealed class OidcServerOptions
{
    public const string SectionName = "Oidc";

    /// <summary>
    /// Signs and encrypts tokens with keys that live only in memory. For tests: every restart invalidates
    /// every token that was issued.
    /// </summary>
    public bool UseEphemeralKeys { get; set; }

    /// <summary>PKCS#12 file holding the token signing key. Required outside Development.</summary>
    public string? SigningCertificatePath { get; set; }

    public string? SigningCertificatePassword { get; set; }

    /// <summary>PKCS#12 file holding the token encryption key. Required outside Development.</summary>
    public string? EncryptionCertificatePath { get; set; }

    public string? EncryptionCertificatePassword { get; set; }

    /// <summary>
    /// A folder where the portal keeps its own self-signed signing and encryption certificates, creating them on
    /// first start. For a container: point it at a persistent volume (next to the Data Protection keys) and the
    /// keys survive restarts and are shared by every instance mounting it. Ignored when both certificate paths
    /// are set. Protect the folder like the Data Protection keys: whoever reads it can mint tokens.
    /// </summary>
    public string? CertificatesPath { get; set; }

    /// <summary>Optional password for the certificates kept in <see cref="CertificatesPath"/>.</summary>
    public string? CertificatesPassword { get; set; }

    /// <summary>
    /// Kept short on purpose: an application finds out that access was revoked when it next refreshes,
    /// because the token endpoint re-checks access every time.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 10;

    public int RefreshTokenLifetimeDays { get; set; } = 14;
}

/// <summary>
/// Turns the portal into an OpenID Connect provider for other applications.
/// <para>
/// The endpoints run in pass-through mode: OpenIddict validates the protocol, and
/// <c>ConnectController</c> decides who gets a token, which is where the access check lives. Only the
/// authorization-code flow with PKCE and refresh tokens is enabled.
/// </para>
/// </summary>
public static class OidcServerSetup
{
    public static IServiceCollection AddOpenPortalOidcServer(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        bool requireSecureTransport)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var options = configuration.GetSection(OidcServerOptions.SectionName).Get<OidcServerOptions>() ?? new OidcServerOptions();

        services.AddOpenIddict()
            .AddServer(server =>
            {
                server.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetUserInfoEndpointUris("connect/userinfo")
                    .SetEndSessionEndpointUris("connect/endsession");

                server.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .RequireProofKeyForCodeExchange();

                server.RegisterScopes(
                    Scopes.OpenId,
                    Scopes.Email,
                    Scopes.Profile,
                    Scopes.OfflineAccess,
                    PortalScopes.Roles,
                    PortalScopes.Groups);

                // Advertised in the discovery document, so a client library knows what to expect.
                server.RegisterClaims(
                    Claims.Subject,
                    Claims.Name,
                    Claims.PreferredUsername,
                    Claims.Email,
                    Claims.Role,
                    ConnectController.GroupsClaim);

                server.SetAccessTokenLifetime(TimeSpan.FromMinutes(options.AccessTokenLifetimeMinutes))
                    .SetRefreshTokenLifetime(TimeSpan.FromDays(options.RefreshTokenLifetimeDays));

                ConfigureKeys(server, options, environment);

                var aspNetCore = server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();

                // Mirrors Identity:Cookie:RequireSecure: the dev server and the tests run over plain http.
                if (!requireSecureTransport)
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(validation =>
            {
                // Lets the userinfo endpoint authenticate the access tokens this server issued.
                validation.UseLocalServer();
                validation.UseAspNetCore();
            });

        return services;
    }

    private static void ConfigureKeys(
        OpenIddictServerBuilder server,
        OidcServerOptions options,
        IWebHostEnvironment environment)
    {
        if (options.UseEphemeralKeys)
        {
            server.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.SigningCertificatePath)
            && !string.IsNullOrWhiteSpace(options.EncryptionCertificatePath))
        {
            server.AddSigningCertificate(LoadCertificate(options.SigningCertificatePath, options.SigningCertificatePassword))
                .AddEncryptionCertificate(LoadCertificate(options.EncryptionCertificatePath, options.EncryptionCertificatePassword));
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.CertificatesPath))
        {
            server.AddSigningCertificate(StoredCertificates.LoadOrCreate(options.CertificatesPath, "signing", X509KeyUsageFlags.DigitalSignature, options.CertificatesPassword))
                .AddEncryptionCertificate(StoredCertificates.LoadOrCreate(options.CertificatesPath, "encryption", X509KeyUsageFlags.KeyEncipherment, options.CertificatesPassword));
            return;
        }

        if (environment.IsDevelopment())
        {
            // Self-signed certificates kept in the current user's certificate store, created on first run.
            server.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
            return;
        }

        throw new InvalidOperationException(
            "The OpenID Connect server has no keys. Set Oidc:SigningCertificatePath and "
            + "Oidc:EncryptionCertificatePath (PKCS#12 files, passwords in Oidc:*CertificatePassword), "
            + "or Oidc:CertificatesPath (a persistent folder where the portal creates its own), "
            + "or Oidc:UseEphemeralKeys=true for a throwaway instance.");
    }

    private static X509Certificate2 LoadCertificate(string path, string? password) =>
        X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
}
