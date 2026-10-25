using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Settings;

namespace OpenPortal.Web.Authentication;

/// <summary>
/// Signing in with Google and with Microsoft (Entra ID work and school accounts, personal Microsoft accounts) through
/// OpenID Connect. One scheme per provider, named after it, always registered; its options come from the settings an
/// administrator saved in the portal (<see cref="IExternalLoginProviders"/>) and are rebuilt when those change, so no
/// restart is needed. The handler leaves the person in Identity's external cookie with the portal's own claims
/// (<see cref="ExternalLoginClaimTypes"/>), which the Identity module reads.
/// </summary>
public static class ExternalLogins
{
    /// <summary>Microsoft's tenant of personal accounts: every account there owns its address.</summary>
    public const string MicrosoftConsumersTenantId = "9188040d-6c67-4c5b-b112-36a304b66dad";

    /// <summary>The challenge's item that says where to come back to: a sign-in or connecting an account.</summary>
    public const string PurposeItem = "openportal.purpose";

    public const string LinkPurpose = "link";

    public static IServiceCollection AddOpenPortalExternalLogins(this IServiceCollection services, bool requireSecureCookies)
    {
        var authentication = services.AddAuthentication();
        foreach (var provider in ExternalLoginProviders.All)
        {
            authentication.AddOpenIdConnect(provider, ExternalLoginProviders.DisplayName(provider), _ => { });
        }

        services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>>(provider =>
            new ExternalLoginOptionsSetup(provider.GetRequiredService<IExternalLoginProviders>(), requireSecureCookies));
        services.AddHostedService<ExternalLoginOptionsInvalidator>();

        return services;
    }

    /// <summary>
    /// Reads the settings again before the provider's answer is handled, so an instance that did not save a change
    /// still uses the current client secret.
    /// </summary>
    public static IApplicationBuilder UseOpenPortalExternalLogins(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/signin-google", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/signin-microsoft", StringComparison.OrdinalIgnoreCase))
            {
                await context.RequestServices.GetRequiredService<IExternalLoginProviders>()
                    .RefreshAsync(context.RequestAborted)
                    .ConfigureAwait(false);
            }

            await next(context).ConfigureAwait(false);
        });

    /// <summary>
    /// A local path to come back to (<c>/connect/authorize?…</c>, <c>/account</c>), or null. Never another site, so the
    /// sign-in cannot be turned into an open redirect.
    /// </summary>
    public static string? LocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
        && !returnUrl.Contains('\r', StringComparison.Ordinal)
        && !returnUrl.Contains('\n', StringComparison.Ordinal)
            ? returnUrl
            : null;
}

/// <summary>Builds each provider's handler options from the saved settings.</summary>
internal sealed class ExternalLoginOptionsSetup : IConfigureNamedOptions<OpenIdConnectOptions>
{
    private const string Unconfigured = "unconfigured";

    private readonly IExternalLoginProviders _providers;
    private readonly bool _requireSecureCookies;

    public ExternalLoginOptionsSetup(IExternalLoginProviders providers, bool requireSecureCookies)
    {
        _providers = providers;
        _requireSecureCookies = requireSecureCookies;
    }

    public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (!ExternalLoginProviders.IsKnown(name))
        {
            return;
        }

        var provider = name!;
        var settings = _providers.Find(provider);

        // A provider that is off still needs valid options (the scheme exists); the portal never challenges it.
        options.ClientId = settings?.ClientId ?? Unconfigured;
        options.ClientSecret = settings?.ClientSecret ?? Unconfigured;
        options.Authority = provider == ExternalLoginProviders.Google
            ? "https://accounts.google.com"
            : $"https://login.microsoftonline.com/{settings?.Tenant ?? ExternalLoginProvider.MicrosoftOrganizations}/v2.0";

        options.CallbackPath = ExternalLoginProviders.CallbackPath(provider);
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ResponseType = OpenIdConnectResponseType.Code;

        // The answer comes back as a top-level navigation (not a cross-site form post), so the correlation cookies can
        // stay SameSite=Lax and work over plain http in development.
        options.ResponseMode = OpenIdConnectResponseMode.Query;
        options.UsePkce = true;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.MapInboundClaims = false;
        options.DisableTelemetry = true;
        options.Scope.Clear();
        options.Scope.Add(OpenIdConnectScope.OpenId);
        options.Scope.Add(OpenIdConnectScope.Email);
        options.Scope.Add(OpenIdConnectScope.Profile);

        var secure = _requireSecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = secure;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SecurePolicy = secure;

        if (provider == ExternalLoginProviders.Microsoft && settings is not { SingleTenant: true })
        {
            // The shared authorities name every directory's own issuer; a token is good when its issuer is the
            // directory it says it comes from.
            options.TokenValidationParameters.IssuerValidator = ValidateMicrosoftIssuer;
        }

        options.Events = new OpenIdConnectEvents
        {
            // Let the person pick among the accounts their browser is signed in to, rather than reusing the last one.
            OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.Prompt = "select_account";
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                context.Principal = Normalise(provider, settings, context.Principal);
                return Task.CompletedTask;
            },
            OnAccessDenied = context =>
            {
                context.Response.Redirect(FailureTarget(context.Properties, "identity.external_sign_in_cancelled"));
                context.HandleResponse();
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                context.Response.Redirect(FailureTarget(context.Properties, "identity.external_sign_in_failed"));
                context.HandleResponse();
                return Task.CompletedTask;
            },
        };
    }

    /// <summary>
    /// The provider's token claims as the portal's own: a stable subject, the address and whether the provider
    /// vouches for it, and the names.
    /// </summary>
    internal static ClaimsPrincipal Normalise(string provider, ExternalProviderConfiguration? settings, ClaimsPrincipal? source)
    {
        ArgumentNullException.ThrowIfNull(source);

        string? Claim(string type) => source.FindFirst(type)?.Value;

        string? subject;
        string? email;
        bool verified;

        if (provider == ExternalLoginProviders.Google)
        {
            subject = Claim("sub");
            email = Claim("email");
            verified = string.Equals(Claim("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // The object id within its directory is stable across application registrations, unlike the pairwise sub.
            var tenant = Claim("tid");
            var objectId = Claim("oid");
            subject = tenant is not null && objectId is not null ? $"{tenant}:{objectId}" : Claim("sub");

            // A directory's administrators can put any address on their accounts, so the address proves ownership only
            // for personal accounts, when Entra says the domain owner verified it, or within the one directory the
            // portal was set up to trust.
            var singleTenant = settings is { SingleTenant: true };
            email = Claim("email") ?? (singleTenant ? Claim("preferred_username") : null);
            verified = string.Equals(tenant, ExternalLogins.MicrosoftConsumersTenantId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Claim("xms_edov"), "true", StringComparison.OrdinalIgnoreCase)
                || Claim("xms_edov") == "1"
                || singleTenant;
        }

        var identity = new ClaimsIdentity(provider);
        if (!string.IsNullOrEmpty(subject))
        {
            identity.AddClaim(new Claim(ExternalLoginClaimTypes.Subject, subject));
        }

        void Add(string type, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                identity.AddClaim(new Claim(type, value.Trim()));
            }
        }

        Add(ExternalLoginClaimTypes.Email, email);
        Add(ExternalLoginClaimTypes.EmailVerified, email is not null && verified ? "true" : "false");
        Add(ExternalLoginClaimTypes.GivenName, Claim("given_name"));
        Add(ExternalLoginClaimTypes.FamilyName, Claim("family_name"));
        Add(ExternalLoginClaimTypes.Name, Claim("name"));

        return new ClaimsPrincipal(identity);
    }

    private static string ValidateMicrosoftIssuer(string issuer, SecurityToken token, TokenValidationParameters parameters)
    {
        var tenant = token is JsonWebToken jwt && jwt.TryGetPayloadValue<string>("tid", out var tid) ? tid : null;

        if (tenant is not null
            && Guid.TryParse(tenant, out _)
            && string.Equals(issuer, $"https://login.microsoftonline.com/{tenant}/v2.0", StringComparison.OrdinalIgnoreCase))
        {
            return issuer;
        }

        throw new SecurityTokenInvalidIssuerException("The token's issuer is not the directory it names.");
    }

    /// <summary>Where a failed or cancelled round trip lands: the sign-in page, or the account's security tab.</summary>
    private static string FailureTarget(AuthenticationProperties? properties, string code)
    {
        if (properties?.Items.TryGetValue(ExternalLogins.PurposeItem, out var purpose) == true && purpose == ExternalLogins.LinkPurpose)
        {
            return "/account?tab=security&externalError=" + Uri.EscapeDataString(code);
        }

        return "/sign-in?externalError=" + Uri.EscapeDataString(code);
    }
}

/// <summary>Drops the handlers' options (and the metadata they fetched) when the saved settings change.</summary>
internal sealed class ExternalLoginOptionsInvalidator : IHostedService
{
    private readonly IExternalLoginProviders _providers;
    private readonly IOptionsMonitorCache<OpenIdConnectOptions> _cache;

    public ExternalLoginOptionsInvalidator(IExternalLoginProviders providers, IOptionsMonitorCache<OpenIdConnectOptions> cache)
    {
        _providers = providers;
        _cache = cache;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _providers.Changed += OnChanged;
        await _providers.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _providers.Changed -= OnChanged;
        return Task.CompletedTask;
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        foreach (var provider in ExternalLoginProviders.All)
        {
            _cache.TryRemove(provider);
        }
    }
}
