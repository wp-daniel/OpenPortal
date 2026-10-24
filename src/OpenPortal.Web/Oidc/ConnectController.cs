using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Web.Authentication;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Observability;
using OpenPortal.Web.Security;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using OpenPortal.Access.Application.Abstractions;
using AccessDecision = OpenPortal.Access.Application.Contracts.AccessDecision;
using OrganizationMembershipDto = OpenPortal.Access.Application.Contracts.OrganizationMembershipDto;
using OrganizationOutcome = OpenPortal.Access.Application.Contracts.OrganizationOutcome;
using PortalScopes = OpenPortal.Access.Application.Contracts.PortalScopes;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Auditing;
using static OpenIddict.Abstractions.OpenIddictConstants;
using PortalAuthentication = OpenPortal.Identity.Application.Abstractions.IAuthenticationService;

namespace OpenPortal.Web.Oidc;

/// <summary>
/// The OpenID Connect endpoints that other applications send their users to, and where an application's
/// backend gets a token of its own for the application API (client credentials).
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
[AllowDuringTwoFactorSetup]
public sealed class ConnectController : ControllerBase
{
    /// <summary>The claim carrying group names (not in OpenIddict's constants; the name most applications expect).</summary>
    public const string GroupsClaim = "groups";

    /// <summary>The organization the user signed in for: its id (stable), short name, name and the user's role in it.</summary>
    public const string OrganizationIdClaim = "org_id";
    public const string OrganizationSlugClaim = "org_slug";
    public const string OrganizationNameClaim = "org_name";
    public const string OrganizationRoleClaim = "org_role";

    /// <summary>The authorization request parameter naming the organization (slug or id) to sign in for.</summary>
    public const string OrganizationParameter = "organization";

    /// <summary>The <c>acr_values</c> an application sends to require a sign-in with two factors.</summary>
    public const string MultiFactorAcr = "mfa";

    /// <summary>OpenID Connect error for an authentication that cannot meet the requested <c>acr_values</c>.</summary>
    public const string UnmetAuthenticationRequirements = "unmet_authentication_requirements";

    private readonly IAccessEvaluator _access;
    private readonly IUserLookupService _users;
    private readonly PortalAuthentication _authentication;
    private readonly IOpenIddictApplicationManager _applications;
    private readonly IOpenIddictAuthorizationManager _authorizations;
    private readonly IAuditTrail _audit;
    private readonly ILegalDocumentService _legal;
    private readonly ITwoFactorPolicy _twoFactor;
    private readonly BackChannelLogoutNotifier _logout;
    private readonly Microsoft.AspNetCore.Antiforgery.IAntiforgery _antiforgery;

    public ConnectController(
        IAccessEvaluator access,
        IUserLookupService users,
        PortalAuthentication authentication,
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations,
        IAuditTrail audit,
        ILegalDocumentService legal,
        ITwoFactorPolicy twoFactor,
        BackChannelLogoutNotifier logout,
        Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        _twoFactor = twoFactor;
        _logout = logout;
        _antiforgery = antiforgery;
        _audit = audit;
        _legal = legal;
        _access = access;
        _users = users;
        _authentication = authentication;
        _applications = applications;
        _authorizations = authorizations;
    }

    /// <summary>This authorization request again, without a prompt (so coming back does not ask to sign in anew).</summary>
    private string ThisRequest()
    {
        var parameters = (Request.HasFormContentType ? Request.Form.AsEnumerable() : Request.Query.AsEnumerable())
            .Where(pair => pair.Key != Parameters.Prompt)
            .ToList();

        return Request.PathBase + Request.Path + QueryString.Create(parameters);
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

        // A support session acts for someone else: it never signs in to the other applications as them.
        if (session.Principal?.FindFirst(PortalClaimTypes.ImpersonatorId) is not null)
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Refuse(Errors.LoginRequired, "Applications cannot be opened while impersonating an account.");
            }

            return Redirect($"{Request.PathBase}/access-denied?reason=impersonation");
        }

        // An account waiting to be deleted signs in to the portal only (to cancel the deletion), never to an application.
        if (user.DeletionScheduled)
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Refuse(Errors.LoginRequired, "The account is scheduled for deletion.");
            }

            return Redirect($"{Request.PathBase}/access-denied?reason=deletion");
        }

        // The privacy notice and terms in force come first: an application cannot be used to skip them.
        if ((await _legal.PendingAsync(user.Id, cancellationToken).ConfigureAwait(false)).Count > 0)
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Refuse(Errors.ConsentRequired, "The user must accept the portal's current terms first.");
            }

            return Redirect($"{Request.PathBase}/legal/accept?returnUrl={Uri.EscapeDataString(ThisRequest())}");
        }

        // An account the portal requires to have a second factor sets one up before it opens anything.
        if (await _twoFactor.IsSetupRequiredAsync(user.Id, cancellationToken).ConfigureAwait(false))
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Refuse(Errors.InteractionRequired, "The user must set up a second factor first.");
            }

            return Redirect($"{Request.PathBase}/account/security-setup?returnUrl={Uri.EscapeDataString(ThisRequest())}");
        }

        // The application asked for a two-factor sign-in (acr_values=mfa) and this session had only one.
        var methods = SessionAuthentication.Methods(session.Principal);
        if (request.GetAcrValues().Contains(MultiFactorAcr, StringComparer.Ordinal)
            && !methods.Contains(AuthenticationMethods.MultiFactor, StringComparer.Ordinal))
        {
            await _audit.RecordAsync(
                    AuditEvent.Failed(
                        OidcAuditActions.SignIn,
                        ApplicationSubject(request.ClientId!, null),
                        new Dictionary<string, string?> { ["reason"] = "mfa_required" }),
                    cancellationToken)
                .ConfigureAwait(false);

            if (request.HasPromptValue(PromptValues.None))
            {
                return Refuse(UnmetAuthenticationRequirements, "The application requires a sign-in with two factors.");
            }

            var clientName = Uri.EscapeDataString(request.ClientId ?? string.Empty);
            return Redirect($"{Request.PathBase}/access-denied?reason=mfa&client={clientName}");
        }

        var decision = await _access.EvaluateAsync(user.Id, request.ClientId!, cancellationToken).ConfigureAwait(false);
        if (!decision.Allowed)
        {
            await _audit.RecordAsync(
                    AuditEvent.Failed(
                        OidcAuditActions.SignIn,
                        ApplicationSubject(request.ClientId!, decision.ApplicationName),
                        new Dictionary<string, string?> { ["reason"] = "access_denied" }),
                    cancellationToken)
                .ConfigureAwait(false);

            PortalTelemetry.ApplicationSignIns.Add(1, new KeyValuePair<string, object?>("outcome", "access_denied"));

            // A page of the portal rather than an error sent back to the application: the user is better off
            // being told plainly, and the application has nothing useful to do with "access_denied" anyway.
            // The client id lets the page offer an access request when the application accepts them.
            var app = Uri.EscapeDataString(decision.ApplicationName ?? request.ClientId ?? string.Empty);
            var clientId = Uri.EscapeDataString(request.ClientId ?? string.Empty);
            return Redirect($"{Request.PathBase}/access-denied?app={app}&client={clientId}");
        }

        var organization = await _access.EvaluateOrganizationAsync(
                user.Id,
                request.ClientId!,
                (string?)request[OrganizationParameter],
                interactive: true,
                cancellationToken)
            .ConfigureAwait(false);

        switch (organization.Outcome)
        {
            case OrganizationOutcome.ChoiceNeeded:
                if (request.HasPromptValue(PromptValues.None))
                {
                    return Refuse(Errors.AccountSelectionRequired, "The user belongs to several organizations and must choose one.");
                }

                // The portal asks which organization, then comes back here with organization=<slug>.
                var back = (Request.HasFormContentType ? Request.Form.AsEnumerable() : Request.Query.AsEnumerable())
                    .Where(pair => pair.Key != Parameters.Prompt && pair.Key != OrganizationParameter)
                    .ToList();
                var returnUrl = Uri.EscapeDataString(Request.PathBase + Request.Path + QueryString.Create(back));
                var name = Uri.EscapeDataString(decision.ApplicationName ?? request.ClientId ?? string.Empty);
                return Redirect($"{Request.PathBase}/select-organization?app={name}&returnUrl={returnUrl}");

            case OrganizationOutcome.Missing or OrganizationOutcome.NotMember:
                await _audit.RecordAsync(
                        AuditEvent.Failed(
                            OidcAuditActions.SignIn,
                            ApplicationSubject(request.ClientId!, decision.ApplicationName),
                            new Dictionary<string, string?>
                            {
                                ["reason"] = organization.Outcome == OrganizationOutcome.Missing ? "no_organization" : "not_member",
                                ["organization"] = (string?)request[OrganizationParameter],
                            }),
                        cancellationToken)
                    .ConfigureAwait(false);

                var denied = Uri.EscapeDataString(decision.ApplicationName ?? request.ClientId ?? string.Empty);
                return Redirect($"{Request.PathBase}/access-denied?app={denied}&reason=organization");
        }

        var client = await _applications.FindByClientIdAsync(request.ClientId!, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The client was validated by OpenIddict but cannot be found.");

        // The application API scopes belong to the application's own token: a user's token never carries them,
        // even when the client is allowed to request them.
        var scopes = request.GetScopes().Where(scope => !ApplicationApiScopes.IsKnown(scope));
        var identity = BuildIdentity(user, scopes, decision, organization.Organization, methods);

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

        var signInDetails = new Dictionary<string, string?>();
        if (decision.Roles.Count > 0)
        {
            signInDetails["roles"] = string.Join(", ", decision.Roles);
        }

        if (organization.Organization is { } chosen)
        {
            signInDetails["organization"] = chosen.Slug;
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    OidcAuditActions.SignIn,
                    ApplicationSubject(request.ClientId!, decision.ApplicationName),
                    signInDetails.Count > 0 ? signInDetails : null),
                cancellationToken)
            .ConfigureAwait(false);

        PortalTelemetry.ApplicationSignIns.Add(1, new KeyValuePair<string, object?>("outcome", "success"));

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [EnableRateLimiting(RateLimitPolicies.Machine)]
    [Produces("application/json")]
    public async Task<IActionResult> ExchangeAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
        {
            return await IssueApplicationTokenAsync(request, cancellationToken).ConfigureAwait(false);
        }

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
        if (user is null || !user.IsActive || user.DeletionScheduled)
        {
            await RecordRefusalAsync(userId, user?.Email, request.ClientId!, applicationName: null, "account_inactive", cancellationToken).ConfigureAwait(false);

            return Refuse(Errors.InvalidGrant, "The account can no longer sign in.");
        }

        var decision = await _access.EvaluateAsync(userId, request.ClientId!, cancellationToken).ConfigureAwait(false);
        if (!decision.Allowed)
        {
            await RecordRefusalAsync(userId, user.Email, request.ClientId!, decision.ApplicationName, "access_withdrawn", cancellationToken).ConfigureAwait(false);

            return Refuse(Errors.InvalidGrant, "Access to this application has been withdrawn.");
        }

        // The organization the sign-in chose stays the one the tokens name; leaving it ends the session like
        // losing the application would.
        var organization = await _access.EvaluateOrganizationAsync(
                userId,
                request.ClientId!,
                stored.Principal!.GetClaim(OrganizationIdClaim),
                interactive: false,
                cancellationToken)
            .ConfigureAwait(false);

        if (organization.Outcome is OrganizationOutcome.Missing or OrganizationOutcome.NotMember or OrganizationOutcome.ChoiceNeeded)
        {
            await RecordRefusalAsync(userId, user.Email, request.ClientId!, decision.ApplicationName, "organization_withdrawn", cancellationToken).ConfigureAwait(false);

            return Refuse(Errors.InvalidGrant, "The user no longer belongs to the organization this session was for.");
        }

        // Rebuilt from the current account and grants, so a renamed user, a changed email or a role given or
        // taken away reaches the application at its next refresh.
        var identity = BuildIdentity(
            user,
            stored.Principal!.GetScopes(),
            decision,
            organization.Organization,
            stored.Principal!.GetClaims(AuthenticationMethods.ClaimType));
        identity.SetAuthorizationId(stored.Principal!.GetAuthorizationId());

        PortalTelemetry.TokensIssued.Add(1, new KeyValuePair<string, object?>("grant", request.GrantType));

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [Produces("application/json")]
    public async Task<IActionResult> UserInfoAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.GetClaim(Claims.Subject), out var userId)
            || await _users.FindAsync(userId, cancellationToken).ConfigureAwait(false) is not { IsActive: true, DeletionScheduled: false } user)
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

        // Roles and groups as they were when the access token was issued (minutes ago at most): the token
        // endpoint recomputes them on every refresh.
        if (User.HasScope(PortalScopes.Roles))
        {
            claims[Claims.Role] = User.GetClaims(Claims.Role).ToArray();
        }

        if (User.HasScope(PortalScopes.Groups))
        {
            claims[GroupsClaim] = User.GetClaims(GroupsClaim).ToArray();
        }

        if (User.HasScope(PortalScopes.Organization) && User.GetClaim(OrganizationIdClaim) is { } organizationId)
        {
            claims[OrganizationIdClaim] = organizationId;
            claims[OrganizationSlugClaim] = User.GetClaim(OrganizationSlugClaim) ?? string.Empty;
            claims[OrganizationNameClaim] = User.GetClaim(OrganizationNameClaim) ?? string.Empty;
            claims[OrganizationRoleClaim] = User.GetClaim(OrganizationRoleClaim) ?? string.Empty;
        }

        return Ok(claims);
    }

    /// <summary>The form field the portal's confirmation page adds when the user agreed to sign out.</summary>
    public const string ConfirmSignOutParameter = "confirm";

    /// <summary>
    /// Ends the portal session and returns to the application's registered post-logout URI (OpenIddict
    /// checks it against the client) or, without one, to the portal's sign-in page.
    /// <para>
    /// A request any site could make cannot end a session on its own (logout CSRF): it needs an
    /// <c>id_token_hint</c> naming the signed-in user (OpenIddict has validated its signature), or the user's own
    /// confirmation on the portal's <c>/sign-out</c> page, which posts back here with the antiforgery token.
    /// The applications the user signed in to are told through back-channel logout.
    /// </para>
    /// </summary>
    [HttpGet("~/connect/endsession")]
    [HttpPost("~/connect/endsession")]
    public async Task<IActionResult> EndSessionAsync(CancellationToken cancellationToken)
    {
        var session = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
        var userId = session.Succeeded ? session.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) : null;

        if (userId is not null)
        {
            var hint = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);
            var hinted = string.Equals(hint.Principal?.GetClaim(Claims.Subject), userId, StringComparison.Ordinal);

            var confirmed = HttpMethods.IsPost(Request.Method)
                && Request.HasFormContentType
                && string.Equals(Request.Form[ConfirmSignOutParameter], "true", StringComparison.Ordinal)
                && await _antiforgery.IsRequestValidAsync(HttpContext).ConfigureAwait(false);

            if (!hinted && !confirmed)
            {
                var parameters = (Request.HasFormContentType ? Request.Form.AsEnumerable() : Request.Query.AsEnumerable())
                    .Where(pair => pair.Key != ConfirmSignOutParameter && pair.Key != "__RequestVerificationToken")
                    .ToList();

                return Redirect($"{Request.PathBase}/sign-out{QueryString.Create(parameters)}");
            }

            // A support session ends itself only; it never signs the account holder out of their applications.
            if (Guid.TryParse(userId, out var id) && session.Principal?.FindFirst(PortalClaimTypes.ImpersonatorId) is null)
            {
                await _logout.NotifyAsync(id, cancellationToken).ConfigureAwait(false);
            }

            await _authentication.SignOutAsync(cancellationToken).ConfigureAwait(false);
        }

        return SignOut(
            new AuthenticationProperties { RedirectUri = $"{Request.PathBase}/sign-in" },
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// The application's own token (client credentials; OpenIddict has already checked the secret and that the
    /// client may use this grant and these scopes). It carries no user: the subject is the client id, marked so
    /// the application API can tell it from a user's token. The application must be active and the scopes still
    /// enabled for it; without a <c>scope</c> parameter the token gets every enabled scope.
    /// </summary>
    private async Task<IActionResult> IssueApplicationTokenAsync(OpenIddictRequest request, CancellationToken cancellationToken)
    {
        var clientId = request.ClientId!;
        var decision = await _access.EvaluateApplicationAsync(clientId, cancellationToken).ConfigureAwait(false);

        if (!decision.Allowed)
        {
            await RecordApplicationRefusalAsync(clientId, decision.ApplicationName, "application_not_allowed", cancellationToken).ConfigureAwait(false);

            return Refuse(Errors.UnauthorizedClient, "The application is not active or has no application API scopes.");
        }

        var requested = request.GetScopes();
        if (requested.Any(scope => !decision.ApiScopes.Contains(scope, StringComparer.Ordinal)))
        {
            await RecordApplicationRefusalAsync(clientId, decision.ApplicationName, "scope_not_enabled", cancellationToken).ConfigureAwait(false);

            return Refuse(Errors.InvalidScope, "A requested scope is not enabled for this application.");
        }

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, clientId)
            .SetClaim(Claims.Name, decision.ApplicationName)
            .SetClaim(ApplicationTokens.CallerClaim, ApplicationTokens.ApplicationCaller);

        IEnumerable<string> scopes = requested.IsEmpty ? decision.ApiScopes : requested;
        identity.SetScopes(scopes);
        identity.SetDestinations(_ => [Destinations.AccessToken]);

        PortalTelemetry.TokensIssued.Add(1, new KeyValuePair<string, object?>("grant", GrantTypes.ClientCredentials));

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private Task RecordApplicationRefusalAsync(string clientId, string? applicationName, string reason, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            new AuditEvent
            {
                Action = OidcAuditActions.TokenRefused,
                Outcome = AuditOutcome.Failure,
                Actor = ApplicationSubject(clientId, applicationName),
                Target = ApplicationSubject(clientId, applicationName),
                Details = new Dictionary<string, string?> { ["reason"] = reason, ["grant"] = GrantTypes.ClientCredentials },
            },
            cancellationToken);

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

    private async Task RecordRefusalAsync(
        Guid userId,
        string? email,
        string clientId,
        string? applicationName,
        string reason,
        CancellationToken cancellationToken)
    {
        PortalTelemetry.TokensRefused.Add(1, new KeyValuePair<string, object?>("reason", reason));

        await _audit.RecordAsync(
                new AuditEvent
                {
                    Action = OidcAuditActions.TokenRefused,
                    Outcome = AuditOutcome.Failure,

                    // Called by the application's server, not the user's browser, so the user is named explicitly.
                    Actor = new AuditSubject(AuditSubjectTypes.User, userId.ToString(), email),
                    Target = ApplicationSubject(clientId, applicationName),
                    Details = new Dictionary<string, string?> { ["reason"] = reason },
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static AuditSubject ApplicationSubject(string clientId, string? applicationName) =>
        new(AuditSubjectTypes.Application, clientId, applicationName);

    private static ClaimsIdentity BuildIdentity(
        UserReferenceDto user,
        IEnumerable<string> scopes,
        AccessDecision decision,
        OrganizationMembershipDto? organization,
        IEnumerable<string> authenticationMethods)
    {
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(Claims.PreferredUsername, user.Email);

        // The application's roles for this user, and the groups its setting lets it see. Several values make
        // a JSON array in the token, one value a string; ASP.NET Core reads both as separate claims.
        identity.SetClaims(Claims.Role, [.. decision.Roles]);
        identity.SetClaims(GroupsClaim, [.. decision.Groups]);

        // The tenant the application keeps the user's data apart by. Always in the access token (the refresh check
        // reads it back); in the identity token when the application asked for the organization scope.
        if (organization is not null)
        {
            identity.SetClaim(OrganizationIdClaim, organization.Id.ToString())
                .SetClaim(OrganizationSlugClaim, organization.Slug)
                .SetClaim(OrganizationNameClaim, organization.Name)
                .SetClaim(OrganizationRoleClaim, organization.Role);
        }

        // How the user signed in to the portal (RFC 8176), so an application can tell a two-factor sign-in apart.
        // Kept from the original sign-in on every refresh.
        identity.SetClaims(AuthenticationMethods.ClaimType, [.. authenticationMethods.Distinct(StringComparer.Ordinal)]);

        identity.SetScopes(scopes);
        identity.SetDestinations(claim => DestinationsFor(claim, identity));

        return identity;
    }

    /// <summary>
    /// Every claim goes into the access token, which only the portal's userinfo endpoint reads. Profile, email,
    /// role and group claims also go into the identity token when the application asked for that scope.
    /// </summary>
    private static IEnumerable<string> DestinationsFor(Claim claim, ClaimsIdentity identity)
    {
        yield return Destinations.AccessToken;

        var scope = claim.Type switch
        {
            Claims.Name or Claims.PreferredUsername => Scopes.Profile,
            Claims.Email => Scopes.Email,
            Claims.Role => PortalScopes.Roles,
            GroupsClaim => PortalScopes.Groups,
            OrganizationIdClaim or OrganizationSlugClaim or OrganizationNameClaim or OrganizationRoleClaim => PortalScopes.Organization,
            _ => null,
        };

        if (claim.Type is Claims.Subject or AuthenticationMethods.ClaimType || (scope is not null && identity.HasScope(scope)))
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
