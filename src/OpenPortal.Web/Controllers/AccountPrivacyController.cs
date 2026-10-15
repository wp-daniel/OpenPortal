using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Privacy;
using OpenPortal.Web.Security;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The account holder's privacy rights: a copy of their data, and the deletion of their account (after a grace
/// period they can cancel in by signing in again). Always the caller's own account, and only from their own
/// browser session: never through a personal access token or a support session.
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize]
[InteractiveSessionOnly]
[Produces("application/json")]
public sealed class AccountPrivacyController : ControllerBase
{
    private readonly IAccountPrivacyService _privacy;
    private readonly ICurrentUser _currentUser;
    private readonly PersonalDataExport _export;
    private readonly IAccessUserMaintenance _access;
    private readonly IAuthenticationService _authentication;
    private readonly IAuditTrail _audit;

    public AccountPrivacyController(
        IAccountPrivacyService privacy,
        ICurrentUser currentUser,
        PersonalDataExport export,
        IAccessUserMaintenance access,
        IAuthenticationService authentication,
        IAuditTrail audit)
    {
        _privacy = privacy;
        _currentUser = currentUser;
        _export = export;
        _access = access;
        _authentication = authentication;
        _audit = audit;
    }

    /// <summary>
    /// A ZIP with everything the portal holds about the caller (<c>data.json</c>, the profile picture, a README).
    /// The password is asked again; a few downloads per hour at most.
    /// </summary>
    [HttpPost("data-export")]
    [EnableRateLimiting(RateLimitPolicies.Export)]
    [Produces("application/zip", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ExportAsync([FromBody] PasswordConfirmationRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var confirmed = await _privacy.ConfirmPasswordAsync(request.Password, IdentityAuditActions.DataExported, cancellationToken).ConfigureAwait(false);
        if (confirmed.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, confirmed);
        }

        var (content, fileName) = await _export.BuildAsync(userId, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.DataExported), cancellationToken).ConfigureAwait(false);

        return File(content, "application/zip", fileName);
    }

    /// <summary>
    /// Schedules the deletion of the caller's account after the grace period, ends its sessions here and in every
    /// application, and signs the caller out. Signing in again before the date lets them cancel it.
    /// </summary>
    [HttpPost("deletion")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType<AccountDeletionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountDeletionDto>> RequestDeletionAsync(
        [FromBody] PasswordConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var scheduled = await _privacy.RequestDeletionAsync(request, cancellationToken).ConfigureAwait(false);
        if (scheduled.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, scheduled);
        }

        await _access.RevokeTokensAsync(userId, cancellationToken).ConfigureAwait(false);
        await _authentication.SignOutAsync(cancellationToken).ConfigureAwait(false);

        return Ok(scheduled.Value);
    }

    /// <summary>Withdraws the caller's scheduled deletion.</summary>
    [HttpDelete("deletion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelDeletionAsync(CancellationToken cancellationToken)
    {
        var cancelled = await _privacy.CancelDeletionAsync(cancellationToken).ConfigureAwait(false);

        return cancelled.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, cancelled);
    }
}
