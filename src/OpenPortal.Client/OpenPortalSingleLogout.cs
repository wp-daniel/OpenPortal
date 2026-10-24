using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace OpenPortal.Client;

/// <summary>
/// Remembers when the portal said a user signed out (back-channel logout), so this application's cookie sessions
/// started before that moment end at their next request. Kept in <see cref="IDistributedCache"/>: in memory for one
/// instance, or a shared cache (Redis) when the application runs on several.
/// </summary>
public sealed class OpenPortalLogoutRegistry
{
    private static readonly TimeSpan Remembered = TimeSpan.FromDays(30);

    private readonly IDistributedCache _cache;

    public OpenPortalLogoutRegistry(IDistributedCache cache)
    {
        _cache = cache;
    }

    public Task MarkSignedOutAsync(string subject, DateTimeOffset at, CancellationToken cancellationToken) =>
        _cache.SetStringAsync(
            Key(subject),
            at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Remembered },
            cancellationToken);

    public async Task<DateTimeOffset?> SignedOutAtAsync(string subject, CancellationToken cancellationToken) =>
        await _cache.GetStringAsync(Key(subject), cancellationToken).ConfigureAwait(false) is { } value
        && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    private static string Key(string subject) => "openportal:signed-out:" + subject;
}

/// <summary>Single logout and token hygiene for applications that sign in through OpenPortal.</summary>
public static partial class OpenPortalSingleLogout
{
    private const string LogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    /// <summary>
    /// Ends a cookie session the portal said is over: the user signed out of the portal after this session began.
    /// </summary>
    internal static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var subject = context.Principal?.FindFirst("sub")?.Value;
        if (subject is null || context.Properties.IssuedUtc is not { } issued)
        {
            return;
        }

        var registry = context.HttpContext.RequestServices.GetRequiredService<OpenPortalLogoutRegistry>();
        if (await registry.SignedOutAtAsync(subject, context.HttpContext.RequestAborted).ConfigureAwait(false) is { } signedOut
            && signedOut >= issued.AddSeconds(-1))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        }
    }

    /// <summary>Revokes the refresh token at the portal before signing out there, so it cannot be redeemed later.</summary>
    internal static async Task RevokeRefreshTokenAsync(RedirectContext context)
    {
        // The tokens live in the cookie session that is ending (SaveTokens).
        var session = await context.HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        var refreshToken = session.Properties?.GetTokenValue("refresh_token");
        var configuration = context.Options.ConfigurationManager is { } manager
            ? await manager.GetConfigurationAsync(context.HttpContext.RequestAborted).ConfigureAwait(false)
            : null;

        if (refreshToken is null
            || configuration?.AdditionalData.TryGetValue("revocation_endpoint", out var endpoint) is not true
            || endpoint?.ToString() is not { Length: > 0 } revocationEndpoint)
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, revocationEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = refreshToken,
                    ["token_type_hint"] = "refresh_token",
                    ["client_id"] = context.Options.ClientId!,
                    ["client_secret"] = context.Options.ClientSecret ?? string.Empty,
                }),
            };

            using var response = await context.Options.Backchannel.SendAsync(request, context.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            // Best effort: the portal revokes the tokens itself when access ends, and they expire anyway.
            LogRevocationFailed(context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OpenPortalSingleLogout)), exception);
        }
    }

    /// <summary>
    /// Maps the endpoint the portal posts logout tokens to (OpenID Connect Back-Channel Logout 1.0). A valid token
    /// ends every session of its user in this application at the next request. Map it at
    /// <see cref="OpenPortalClientOptions.BackChannelLogoutPath"/> (the default), which is what the application
    /// announces to the portal.
    /// </summary>
    public static IEndpointConventionBuilder MapOpenPortalBackChannelLogout(this IEndpointRouteBuilder endpoints, string? pattern = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var path = pattern
            ?? endpoints.ServiceProvider.GetRequiredService<IOptions<OpenPortalClientOptions>>().Value.BackChannelLogoutPath
            ?? "/signout-backchannel";

        return endpoints.MapPost(path, (Delegate)HandleLogoutAsync).AllowAnonymous().DisableAntiforgery();
    }

    private static async Task<IResult> HandleLogoutAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-cache, no-store";

        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest();
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        var token = form["logout_token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            return Results.BadRequest();
        }

        var oidc = context.RequestServices.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        var configuration = await oidc.ConfigurationManager!.GetConfigurationAsync(context.RequestAborted).ConfigureAwait(false);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudience = oidc.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidTypes = ["logout+jwt", "JWT"],
            ClockSkew = TimeSpan.FromMinutes(2),
        }).ConfigureAwait(false);

        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OpenPortalSingleLogout));

        if (!validation.IsValid
            || validation.ClaimsIdentity.FindFirst("sub")?.Value is not { Length: > 0 } subject
            || validation.ClaimsIdentity.FindFirst("nonce") is not null
            || !HasLogoutEvent(validation.ClaimsIdentity.FindFirst("events")?.Value))
        {
            LogInvalidLogoutToken(logger, validation.Exception);
            return Results.BadRequest();
        }

        await context.RequestServices.GetRequiredService<OpenPortalLogoutRegistry>()
            .MarkSignedOutAsync(subject, DateTimeOffset.UtcNow, context.RequestAborted)
            .ConfigureAwait(false);

        LogSignedOut(logger, subject);

        return Results.Ok();
    }

    private static bool HasLogoutEvent(string? events)
    {
        if (string.IsNullOrEmpty(events))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(events);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(LogoutEvent, out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenPortal: a back-channel logout token was refused.")]
    private static partial void LogInvalidLogoutToken(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenPortal: user {Subject} signed out of the portal; their sessions here end.")]
    private static partial void LogSignedOut(ILogger logger, string subject);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OpenPortal: the refresh token could not be revoked at sign-out.")]
    private static partial void LogRevocationFailed(ILogger logger, Exception exception);
}

/// <summary>Lets an application's own API accept the access tokens the portal issues.</summary>
public static class OpenPortalApiAuthentication
{
    /// <summary>
    /// Validates the portal's access tokens (signed JWTs) with the keys its discovery document publishes: issuer,
    /// signature and lifetime, and that the token was issued to one of <paramref name="allowedClients"/> (the
    /// <c>client_id</c> claim; by default this application's own client id).
    /// </summary>
    public static AuthenticationBuilder AddOpenPortalApiAuthentication(
        this AuthenticationBuilder builder,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string scheme = "OpenPortal.Bearer",
        params string[] allowedClients)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(OpenPortalClientOptions.SectionName).Get<OpenPortalClientOptions>() ?? new OpenPortalClientOptions();
        var clients = allowedClients.Length > 0 ? allowedClients : [options.ClientId];

        return builder.AddJwtBearer(scheme, bearer =>
        {
            bearer.Authority = options.Authority;
            bearer.RequireHttpsMetadata = options.RequireHttpsMetadata;
            bearer.MapInboundClaims = false;

            // The portal's tokens name no audience unless asked to; the client check below is what scopes them.
            bearer.TokenValidationParameters.ValidateAudience = false;
            bearer.TokenValidationParameters.NameClaimType = "name";
            bearer.TokenValidationParameters.RoleClaimType = "role";
            bearer.TokenValidationParameters.ValidTypes = ["at+jwt", "JWT"];

            bearer.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var client = context.Principal?.FindFirst("client_id")?.Value ?? context.Principal?.FindFirst("azp")?.Value;
                    if (client is null || !clients.Contains(client, StringComparer.Ordinal))
                    {
                        context.Fail("The token was issued to another application.");
                    }

                    return Task.CompletedTask;
                },
            };
        });
    }
}
