using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace OpenPortal.Client;

/// <summary>Connects an ASP.NET Core application to OpenPortal.</summary>
public static class OpenPortalClientExtensions
{
    /// <summary>
    /// Signs users in through the portal (OpenID Connect, authorization code with PKCE) into a local cookie,
    /// and announces the application to the portal so an administrator can approve it.
    /// <para>
    /// The portal decides who may sign in: a user without access to this application never gets back here
    /// with a session. A user whose access is withdrawn cannot sign in again and the saved tokens can no longer
    /// be refreshed, but the local cookie session is not re-checked: it lasts until it expires or the user signs out.
    /// </para>
    /// </summary>
    public static IServiceCollection AddOpenPortalAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(OpenPortalClientOptions.SectionName);
        var options = section.Get<OpenPortalClientOptions>() ?? new OpenPortalClientOptions();

        services.AddOptions<OpenPortalClientOptions>().Bind(section);

        services.AddAuthentication(authentication =>
            {
                authentication.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                authentication.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = $".OpenPortal.{options.ClientId}";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.SlidingExpiration = true;
            })
            .AddOpenIdConnect(oidc =>
            {
                oidc.Authority = options.Authority;
                oidc.ClientId = options.ClientId;
                oidc.ClientSecret = options.ClientSecret;
                oidc.RequireHttpsMetadata = options.RequireHttpsMetadata;

                oidc.ResponseType = OpenIdConnectResponseType.Code;
                oidc.UsePkce = true;

                // The code comes back on a top-level GET, so the correlation and nonce cookies can stay Lax and
                // work over plain http on localhost; form_post would need SameSite=None (and https).
                oidc.ResponseMode = OpenIdConnectResponseMode.Query;
                oidc.CorrelationCookie.SameSite = SameSiteMode.Lax;
                oidc.NonceCookie.SameSite = SameSiteMode.Lax;

                oidc.CallbackPath = options.CallbackPath;
                oidc.SignedOutCallbackPath = options.SignedOutCallbackPath;

                oidc.Scope.Clear();
                oidc.Scope.Add("openid");
                oidc.Scope.Add("profile");
                oidc.Scope.Add("email");
                oidc.Scope.Add("offline_access");

                // The identity token already carries the profile; the claim names stay as the portal sends them
                // ("sub", "name", "email") instead of being rewritten to long WS-Federation URIs.
                oidc.GetClaimsFromUserInfoEndpoint = false;
                oidc.MapInboundClaims = false;
                oidc.SaveTokens = true;
                oidc.TokenValidationParameters.NameClaimType = "name";
                oidc.TokenValidationParameters.RoleClaimType = "role";
            });

        services.AddHttpClient(OpenPortalAnnouncementService.HttpClientName);
        services.AddHostedService<OpenPortalAnnouncementService>();

        return services;
    }

    /// <summary>
    /// Maps a sign-out endpoint that ends both the local session and the portal session, then returns to
    /// <paramref name="returnUrl"/> through the portal's end-session endpoint.
    /// </summary>
    public static IEndpointConventionBuilder MapOpenPortalSignOut(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/account/logout",
        string returnUrl = "/")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapPost(pattern, () => Results.SignOut(
            new AuthenticationProperties { RedirectUri = returnUrl },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
    }

    /// <summary>
    /// The entry assembly's version without build metadata: the SDK appends "+&lt;commit hash&gt;" to the
    /// informational version, which is noise on the portal's screens.
    /// </summary>
    internal static string DefaultVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "unknown";

        var metadata = version.IndexOf('+', StringComparison.Ordinal);

        return metadata > 0 ? version[..metadata] : version;
    }
}
