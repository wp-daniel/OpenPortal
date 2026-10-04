using System.Security.Cryptography.X509Certificates;
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

                server.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess);

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

        if (environment.IsDevelopment())
        {
            // Self-signed certificates kept in the current user's certificate store, created on first run.
            server.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
            return;
        }

        throw new InvalidOperationException(
            "The OpenID Connect server has no keys. Set Oidc:SigningCertificatePath and "
            + "Oidc:EncryptionCertificatePath (PKCS#12 files, passwords in Oidc:*CertificatePassword), "
            + "or Oidc:UseEphemeralKeys=true for a throwaway instance.");
    }

    private static X509Certificate2 LoadCertificate(string path, string? password) =>
        X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
}
