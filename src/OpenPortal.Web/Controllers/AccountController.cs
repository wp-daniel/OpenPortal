using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenPortal.Web.Security;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// Self-service operations on the signed-in account.
/// <para>
/// Every endpoint requires authentication, and every action addresses the caller's own account only. There
/// is no route parameter for a user id: the service resolves the caller from the session, so it is not
/// possible to reach another account through this controller.
/// </para>
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize]
[Produces("application/json")]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountService _account;
    private readonly ITwoFactorService _twoFactor;

    public AccountController(IAccountService account, ITwoFactorService twoFactor)
    {
        _account = account;
        _twoFactor = twoFactor;
    }

    /// <summary>Whether the portal offers two-factor authentication and whether this account uses it.</summary>
    [HttpGet("two-factor")]
    [ProducesResponseType<TwoFactorStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TwoFactorStatusDto>> GetTwoFactorAsync(CancellationToken cancellationToken)
    {
        var status = await _twoFactor.GetStatusAsync(cancellationToken).ConfigureAwait(false);

        return status.IsSuccess ? Ok(status.Value) : ProblemResults.FromResult(HttpContext, status);
    }

    /// <summary>Returns the key (and otpauth URI for the QR code) to add to an authenticator app.</summary>
    [HttpPost("two-factor/setup")]
    [ProducesResponseType<TwoFactorSetupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TwoFactorSetupDto>> BeginTwoFactorSetupAsync(CancellationToken cancellationToken)
    {
        var setup = await _twoFactor.BeginSetupAsync(cancellationToken).ConfigureAwait(false);

        return setup.IsSuccess ? Ok(setup.Value) : ProblemResults.FromResult(HttpContext, setup);
    }

    /// <summary>Turns two-factor on with a code from the app; the response holds the recovery codes, shown once.</summary>
    [HttpPost("two-factor/enable")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType<RecoveryCodesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecoveryCodesDto>> EnableTwoFactorAsync(
        [FromBody] EnableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var enabled = await _twoFactor.EnableAsync(request, cancellationToken).ConfigureAwait(false);

        return enabled.IsSuccess ? Ok(enabled.Value) : ProblemResults.FromResult(HttpContext, enabled);
    }

    /// <summary>Turns two-factor off after checking the password.</summary>
    [HttpPost("two-factor/disable")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DisableTwoFactorAsync(
        [FromBody] TwoFactorPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var disabled = await _twoFactor.DisableAsync(request, cancellationToken).ConfigureAwait(false);

        return disabled.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, disabled);
    }

    /// <summary>Replaces every recovery code after checking the password.</summary>
    [HttpPost("two-factor/recovery-codes")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType<RecoveryCodesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(
        [FromBody] TwoFactorPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var codes = await _twoFactor.RegenerateRecoveryCodesAsync(request, cancellationToken).ConfigureAwait(false);

        return codes.IsSuccess ? Ok(codes.Value) : ProblemResults.FromResult(HttpContext, codes);
    }

    /// <summary>Returns the signed-in account's profile.</summary>
    [HttpGet("profile")]
    [ProducesResponseType<AccountProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountProfileDto>> GetProfileAsync(CancellationToken cancellationToken)
    {
        var profile = await _account.GetProfileAsync(cancellationToken).ConfigureAwait(false);

        return profile.IsSuccess
            ? Ok(profile.Value)
            : ProblemResults.FromResult(HttpContext, profile);
    }

    /// <summary>Updates the signed-in account's display name.</summary>
    [HttpPut("profile")]
    [ProducesResponseType<AccountProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountProfileDto>> UpdateProfileAsync(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _account.UpdateProfileAsync(request, cancellationToken).ConfigureAwait(false);

        return updated.IsSuccess
            ? Ok(updated.Value)
            : ProblemResults.FromResult(HttpContext, updated);
    }

    /// <summary>Stores the signed-in account's preferred UI language (null clears it).</summary>
    [HttpPut("language")]
    [ProducesResponseType<AccountProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountProfileDto>> UpdateLanguageAsync(
        [FromBody] UpdateLanguageRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _account.UpdateLanguageAsync(request, cancellationToken).ConfigureAwait(false);

        return updated.IsSuccess
            ? Ok(updated.Value)
            : ProblemResults.FromResult(HttpContext, updated);
    }

    /// <summary>
    /// Replaces the signed-in account's password.
    /// <para>
    /// Changing the password rotates the security stamp, which invalidates every other session for that
    /// account. The current request completes normally, so the caller should be told to sign in again rather
    /// than be logged out mid-response.
    /// </para>
    /// </summary>
    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var changed = await _account.ChangePasswordAsync(request, cancellationToken).ConfigureAwait(false);

        return changed.IsSuccess
            ? NoContent()
            : ProblemResults.FromResult(HttpContext, changed);
    }
}