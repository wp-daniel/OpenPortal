using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Web.Authentication;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Security;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// Signing in with an external account (Google, Microsoft). The browser leaves for the provider and comes back, so
/// these are page navigations, not JSON calls: the start is a classic form post (with the antiforgery field, so
/// another site cannot start a sign-in in someone's browser) and every outcome is a redirect to a page of the client.
/// </summary>
[ApiController]
[Route("api/auth/external")]
[AllowAnonymous]
[AllowDuringTwoFactorSetup]
public sealed class ExternalLoginController : ControllerBase
{
    private readonly IExternalLoginService _externalLogins;
    private readonly IExternalLoginProviders _providers;

    public ExternalLoginController(IExternalLoginService externalLogins, IExternalLoginProviders providers)
    {
        _externalLogins = externalLogins;
        _providers = providers;
    }

    /// <summary>Sends the browser to the provider's sign-in page.</summary>
    /// <param name="provider"><c>google</c> or <c>microsoft</c>.</param>
    /// <param name="returnUrl">A local path to land on once signed in (an application's <c>/connect/authorize</c>).</param>
    /// <param name="rememberMe">Keep the session after the browser closes.</param>
    [HttpPost("{provider}")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> ChallengeAsync(
        string provider,
        [FromForm] string? returnUrl,
        [FromForm] bool rememberMe,
        CancellationToken cancellationToken)
    {
        await _providers.RefreshAsync(cancellationToken).ConfigureAwait(false);

        var target = ExternalLogins.LocalReturnUrl(returnUrl);
        if (_providers.Find(provider) is null)
        {
            return Redirect(SignInPage("identity.external_provider_disabled", target));
        }

        var callback = "/api/auth/external/callback" + (target is null ? string.Empty : "?returnUrl=" + Uri.EscapeDataString(target));
        var properties = new AuthenticationProperties { RedirectUri = callback };
        properties.Items["LoginProvider"] = provider;
        properties.Items[ExternalLoginProperties.RememberMe] = rememberMe ? "true" : "false";

        return Challenge(properties, provider);
    }

    /// <summary>Where the provider's handler sends the browser back: signs in, or explains why not.</summary>
    [HttpGet("callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> CallbackAsync([FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        var target = ExternalLogins.LocalReturnUrl(returnUrl);
        var signedIn = await _externalLogins.SignInAsync(cancellationToken).ConfigureAwait(false);

        if (signedIn.IsFailure)
        {
            return Redirect(SignInPage(signedIn.Error.Code, target));
        }

        return signedIn.Value switch
        {
            ExternalSignInOutcomes.TwoFactorRequired =>
                Redirect("/sign-in?step=two-factor" + (target is null ? string.Empty : "&returnUrl=" + Uri.EscapeDataString(target))),
            ExternalSignInOutcomes.ApprovalPending => Redirect("/sign-in?notice=approval_pending"),
            _ => Redirect(target ?? "/"),
        };
    }

    private static string SignInPage(string errorCode, string? returnUrl) =>
        "/sign-in?externalError=" + Uri.EscapeDataString(errorCode)
        + (returnUrl is null ? string.Empty : "&returnUrl=" + Uri.EscapeDataString(returnUrl));
}

/// <summary>
/// The signed-in account's external sign-ins: which are connected, connecting another (a round trip to the provider
/// that must start and end in the same account), and disconnecting one.
/// </summary>
[ApiController]
[Route("api/account/external-logins")]
[Authorize]
public sealed class AccountExternalLoginsController : ControllerBase
{
    private readonly IExternalLoginService _externalLogins;
    private readonly IExternalLoginProviders _providers;
    private readonly ICurrentUser _currentUser;

    public AccountExternalLoginsController(IExternalLoginService externalLogins, IExternalLoginProviders providers, ICurrentUser currentUser)
    {
        _externalLogins = externalLogins;
        _providers = providers;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType<AccountExternalLoginsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountExternalLoginsDto>> ListAsync(CancellationToken cancellationToken)
    {
        await _providers.RefreshAsync(cancellationToken).ConfigureAwait(false);
        var logins = await _externalLogins.ListOwnAsync(cancellationToken).ConfigureAwait(false);

        return logins.IsSuccess ? Ok(logins.Value) : ProblemResults.FromResult(HttpContext, logins);
    }

    /// <summary>Sends the browser to the provider to connect one of its accounts (a form post, like the sign-in).</summary>
    [InteractiveSessionOnly]
    [HttpPost("{provider}")]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> ChallengeAsync(string provider, CancellationToken cancellationToken)
    {
        await _providers.RefreshAsync(cancellationToken).ConfigureAwait(false);

        if (_providers.Find(provider) is null || _currentUser.UserId is not { } userId)
        {
            return Redirect("/account?tab=security&externalError=identity.external_provider_disabled");
        }

        var properties = new AuthenticationProperties { RedirectUri = "/api/account/external-logins/callback" };
        properties.Items["LoginProvider"] = provider;
        properties.Items[ExternalLogins.PurposeItem] = ExternalLogins.LinkPurpose;

        // Identity checks this on the way back: only the account that started the connection can finish it.
        properties.Items["XsrfId"] = userId.ToString();

        return Challenge(properties, provider);
    }

    [InteractiveSessionOnly]
    [HttpGet("callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> CallbackAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Redirect("/sign-in");
        }

        var linked = await _externalLogins.LinkAsync(userId, cancellationToken).ConfigureAwait(false);

        return Redirect(linked.IsSuccess
            ? "/account?tab=security&external=linked"
            : "/account?tab=security&externalError=" + Uri.EscapeDataString(linked.Error.Code));
    }

    [InteractiveSessionOnly]
    [HttpDelete("{provider}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveAsync(string provider, CancellationToken cancellationToken)
    {
        var removed = await _externalLogins.RemoveOwnAsync(provider, cancellationToken).ConfigureAwait(false);

        return removed.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, removed);
    }
}
