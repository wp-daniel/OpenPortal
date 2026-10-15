using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Tokens;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Authentication;

/// <summary>
/// Personal access tokens on the portal's own API: <c>Authorization: Bearer opp_…</c> on an <c>/api</c> path
/// (not the application API, which has its own tokens) authenticates as the token's owner; anything else falls back
/// to the session cookie. When a token is presented the cookie is ignored altogether, so a request either is a
/// script or a browser, never both.
/// </summary>
public static class PersonalAccessTokenAuthentication
{
    public const string Scheme = "PersonalAccessToken";

    /// <summary>The default authenticate scheme: picks the token or the cookie per request.</summary>
    public const string SelectorScheme = "PortalSelector";

    public static IServiceCollection AddPersonalAccessTokenAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, PersonalAccessTokenHandler>(Scheme, displayName: null, _ => { })
            .AddPolicyScheme(SelectorScheme, displayName: null, options =>
                options.ForwardDefaultSelector = context => PresentsToken(context.Request) ? Scheme : IdentityConstants.ApplicationScheme);

        // Only authentication is forwarded; challenges stay the cookie's (401 on /api, a redirect for /connect).
        services.Configure<AuthenticationOptions>(options => options.DefaultAuthenticateScheme = SelectorScheme);

        return services;
    }

    /// <summary>Whether the request carries a personal access token for the portal's own API.</summary>
    public static bool PresentsToken(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !request.Path.StartsWithSegments("/api/apps", StringComparison.OrdinalIgnoreCase)
            && request.Headers.Authorization.ToString() is { } header
            && header.StartsWith("Bearer " + PersonalAccessToken.Prefix, StringComparison.Ordinal);
    }

    /// <summary>Whether the caller authenticated with a personal access token.</summary>
    public static bool IsToken(ClaimsPrincipal principal) =>
        principal.HasClaim(claim => claim.Type == PortalClaimTypes.PersonalAccessTokenId);
}

/// <summary>Turns a valid token into the owner's identity, with the same claims a session carries.</summary>
internal sealed class PersonalAccessTokenHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IPersonalAccessTokenValidator _validator;

    public PersonalAccessTokenHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IPersonalAccessTokenValidator validator)
        : base(options, logger, encoder)
    {
        _validator = validator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var identity = await _validator.ValidateAsync(header["Bearer ".Length..].Trim(), Context.RequestAborted).ConfigureAwait(false);
        if (identity is null)
        {
            return AuthenticateResult.Fail("The personal access token is not valid.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.UserId.ToString()),
            new(ClaimTypes.Name, identity.Email),
            new(ClaimTypes.Email, identity.Email),
            new(PortalClaimTypes.PersonalAccessTokenId, identity.TokenId.ToString()),
        };
        claims.AddRange(identity.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(identity.Scopes.Select(scope => new Claim(PortalClaimTypes.PersonalAccessTokenScope, scope)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, PersonalAccessTokenAuthentication.Scheme));

        return AuthenticateResult.Success(new AuthenticationTicket(principal, PersonalAccessTokenAuthentication.Scheme));
    }
}

/// <summary>
/// A token with only the <c>read</c> scope may send safe requests (GET, HEAD) only: anything that changes state is
/// refused with 403 <c>auth.token_scope_insufficient</c>. Applied globally, before every other filter.
/// </summary>
public sealed class PersonalAccessTokenScopeFilter : IAuthorizationFilter, IOrderedFilter
{
    public static readonly Error ScopeInsufficient = Error.Forbidden(
        "auth.token_scope_insufficient",
        "This personal access token may only read; create one with the write scope to change anything.");

    public int Order => int.MinValue + 50;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.HttpContext.User;
        if (!PersonalAccessTokenAuthentication.IsToken(user)
            || HttpMethods.IsGet(context.HttpContext.Request.Method)
            || HttpMethods.IsHead(context.HttpContext.Request.Method))
        {
            return;
        }

        if (!user.HasClaim(PortalClaimTypes.PersonalAccessTokenScope, PersonalAccessTokenScopes.Write))
        {
            context.Result = ProblemResults.FromResult(context.HttpContext, Result.Failure(ScopeInsufficient));
        }
    }
}
