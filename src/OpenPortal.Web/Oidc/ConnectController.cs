using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using static OpenIddict.Abstractions.OpenIddictConstants;
using PortalAuthentication = OpenPortal.Identity.Application.Abstractions.IAuthenticationService;

namespace OpenPortal.Web.Oidc;

/// <summary>
/// The OpenID Connect endpoints that other applications send their users to.
/// <para>
/// OpenIddict has already validated the protocol (client, redirect URI, PKCE) when an action runs; what is
/// decided here is whether this user may have a token for this application. That decision is made on the
/// authorization request <em>and again on every token request</em>, so revoking access takes effect at the
/// application's next refresh even if it still holds a refresh token.
/// </para>
/// <para>
/// Antiforgery does not apply: the browser arrives here by redirect from another site, and the token
/// endpoint is called server-to-server. Each request is authenticated by the protocol itself (PKCE, client
/// secret, state).
/// </para>
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
public sealed class ConnectController : ControllerBase
{
    private readonly IAccessEvaluator _access;
    private readonly IUserLookupService _users;
    private readonly PortalAuthentication _authentication;
    private readonly IOpenIddictApplicationManager _applications;
    private readonly IOpenIddictAuthorizationManager _authorizations;

    public ConnectController(
        IAccessEvaluator access,
        IUserLookupService users,
        PortalAuthentication authentication,
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations)
    {
        _access = access;
        _users = users;
        _authentication = authentication;
        _applications = applications;
        _authorizations = authorizations;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> AuthorizeAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var session = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);

        if (!session.Succeeded || request.HasPromptValue(PromptValues.Login))
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Refuse(Errors.LoginRequired, "The user is not signed in.");
            }

            // Back to this same request once the user has signed in. Only the query string is replayed: a
            // POSTed authorization request is rare, and its parameters would not survive the redirect anyway.
            var parameters = Request.HasFormContentType
                ? Request.Form.Where(pair => pair.Key != Parameters.Prompt).ToList()
                : Request.Query.Where(pair => pair.Key != Parameters.Prompt).ToList();

            return Challenge(
                new AuthenticationProperties { RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parameters) },
                IdentityConstants.ApplicationScheme);
        }

        var user = await FindUserAsync(session.Principal, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            // The cookie outlived the account (deleted or locked out). Start over at the sign-in page.
            return Challenge(
                new AuthenticationProperties { RedirectUri = Request.PathBase + Request.Path + Request.QueryString },
                IdentityConstants.ApplicationScheme);
        }

        var decision = await _access.EvaluateAsync(user.Id, request.ClientId!, cancellationToken).ConfigureAwait(false);
        if (!decision.Allowed)
        {
            // A page of the portal rather than an error sent back to the application: the user is better off
            // being told plainly, and the application has nothing useful to do with "access_denied" anyway.
            var app = Uri.EscapeDataString(decision.ApplicationName ?? request.ClientId ?? string.Empty);
            return Redirect($"{Request.PathBase}/access-denied?app={app}");
        }

        var client = await _applications.FindByClientIdAsync(request.ClientId!, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The client was validated by OpenIddict but cannot be found.");

        var identity = BuildIdentity(user, request.GetScopes());

        // A permanent authorization ties the refresh tokens to (user, client), which is what revocation
        // targets when access is withdrawn.
        var clientKey = await _applications.GetIdAsync(client, cancellationToken).ConfigureAwait(false);
        var authorization = await FindAuthorizationAsync(user.Id, clientKey!, identity.GetScopes(), cancellationToken).ConfigureAwait(false)
            ?? await _authorizations.CreateAsync(
                identity,
                user.Id.ToString(),
                clientKey!,
                AuthorizationTypes.Permanent,
                identity.GetScopes(),
                cancellationToken).ConfigureAwait(false);

        identity.SetAuthorizationId(await _authorizations.GetIdAsync(authorization, cancellationToken).ConfigureAwait(false));

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> ExchangeAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            // OpenIddict rejects every other grant type before this action runs.
            throw new InvalidOperationException("The specified grant type is not supported.");
        }

        var stored = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);

        if (!Guid.TryParse(stored.Principal?.GetClaim(Claims.Subject), out var userId))
        {
            return Refuse(Errors.InvalidGrant, "The token is no longer valid.");
        }

        var user = await _users.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return Refuse(Errors.InvalidGrant, "The account can no longer sign in.");
        }

        var decision = await _access.EvaluateAsync(userId, request.ClientId!, cancellationToken).ConfigureAwait(false);
        if (!decision.Allowed)
        {
            return Refuse(Errors.InvalidGrant, "Access to this application has been withdrawn.");
        }

        // Rebuilt from the current account so a renamed user or changed email reaches the application.
        var identity = BuildIdentity(user, stored.Principal!.GetScopes());
        identity.SetAuthorizationId(stored.Principal!.GetAuthorizationId());

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [Produces("application/json")]
    public async Task<IActionResult> UserInfoAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.GetClaim(Claims.Subject), out var userId)
            || await _users.FindAsync(userId, cancellationToken).ConfigureAwait(false) is not { IsActive: true } user)
        {
            return Challenge(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictValidationAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictValidationAspNetCoreConstants.Properties.ErrorDescription] = "The account can no longer sign in.",
                }),
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.Id.ToString(),
        };

        if (User.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = user.Email;
        }

        if (User.HasScope(Scopes.Profile))
        {
            claims[Claims.Name] = user.DisplayName;
            claims[Claims.PreferredUsername] = user.Email;
        }

        return Ok(claims);
    }

    /// <summary>
    /// Ends the portal session and returns to the application's registered post-logout URI (OpenIddict
    /// checks it against the client) or, without one, to the portal's sign-in page.
    /// </summary>
    [HttpGet("~/connect/endsession")]
    [HttpPost("~/connect/endsession")]
    public async Task<IActionResult> EndSessionAsync(CancellationToken cancellationToken)
    {
        await _authentication.SignOutAsync(cancellationToken).ConfigureAwait(false);

        return SignOut(
            new AuthenticationProperties { RedirectUri = $"{Request.PathBase}/sign-in" },
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<UserReferenceDto?> FindUserAsync(ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        return Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? await _users.FindAsync(userId, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private async Task<object?> FindAuthorizationAsync(
        Guid userId,
        string clientKey,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        object? found = null;

        await foreach (var authorization in _authorizations.FindAsync(
                           userId.ToString(),
                           clientKey,
                           Statuses.Valid,
                           AuthorizationTypes.Permanent,
                           [.. scopes],
                           cancellationToken).ConfigureAwait(false))
        {
            found = authorization;
        }

        return found;
    }

    private static ClaimsIdentity BuildIdentity(UserReferenceDto user, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(Claims.PreferredUsername, user.Email);

        identity.SetScopes(scopes);
        identity.SetDestinations(claim => DestinationsFor(claim, identity));

        return identity;
    }

    /// <summary>
    /// Every claim goes into the access token, which only the portal's userinfo endpoint reads. Profile and
    /// email claims also go into the identity token when the application asked for that scope.
    /// </summary>
    private static IEnumerable<string> DestinationsFor(Claim claim, ClaimsIdentity identity)
    {
        yield return Destinations.AccessToken;

        var scope = claim.Type switch
        {
            Claims.Name or Claims.PreferredUsername => Scopes.Profile,
            Claims.Email => Scopes.Email,
            _ => null,
        };

        if (claim.Type == Claims.Subject || (scope is not null && identity.HasScope(scope)))
        {
            yield return Destinations.IdentityToken;
        }
    }

    private ForbidResult Refuse(string error, string description) => Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
