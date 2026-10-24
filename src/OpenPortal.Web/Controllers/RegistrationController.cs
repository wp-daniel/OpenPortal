using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Security;

using OpenPortal.Web.Authorization;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// How people get an account without an administrator typing it in: registering (when the portal allows it) and
/// accepting an invitation. Anonymous by nature, rate limited like sign-in, and antiforgery-checked like every other
/// unsafe request.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[AllowDuringTwoFactorSetup]
[Produces("application/json")]
public sealed class RegistrationController : ControllerBase
{
    private readonly IRegistrationService _registration;
    private readonly IInvitationService _invitations;
    private readonly IAuthenticationService _authentication;
    private readonly IAccessProvisioningService _provisioning;
    private readonly IPasswordRecoveryService _recovery;

    public RegistrationController(
        IRegistrationService registration,
        IInvitationService invitations,
        IAuthenticationService authentication,
        IAccessProvisioningService provisioning,
        IPasswordRecoveryService recovery)
    {
        _recovery = recovery;
        _registration = registration;
        _invitations = invitations;
        _authentication = authentication;
        _provisioning = provisioning;
    }

    /// <summary>
    /// Creates an account. The outcome says what happens next: signed in, a confirmation link sent, or waiting for
    /// an administrator.
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType<RegistrationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistrationResultDto>> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var registered = await _registration.RegisterAsync(request, cancellationToken).ConfigureAwait(false);

        return registered.IsSuccess ? Ok(registered.Value) : ProblemResults.FromResult(HttpContext, registered);
    }

    /// <summary>Confirms the address of a self-registered account (the link in the confirmation email).</summary>
    [HttpPost("confirm-email")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType<RegistrationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegistrationResultDto>> ConfirmEmailAsync(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var confirmed = await _registration.ConfirmEmailAsync(request, cancellationToken).ConfigureAwait(false);

        return confirmed.IsSuccess ? Ok(confirmed.Value) : ProblemResults.FromResult(HttpContext, confirmed);
    }

    /// <summary>Sends the confirmation link again. Always 204, whatever the address, so it reveals nothing.</summary>
    [HttpPost("confirm-email/resend")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendConfirmationAsync(
        [FromBody] ResendConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        await _registration.ResendConfirmationAsync(request, cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Emails a password reset link when the address has an account that may sign in. Always 204 (or 409 when the
    /// portal cannot send email at all), whatever the address, so it reveals nothing.
    /// </summary>
    [HttpPost("password/forgot")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _recovery.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return ProblemResults.FromResult(HttpContext, Result.Failure(UserErrors.PasswordRecoveryUnavailable));
        }

        await _recovery.RequestAsync(request, cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>Sets a new password with the link from the reset email. The account is signed out everywhere.</summary>
    [HttpPost("password/reset")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordAsync(
        [FromBody] CompletePasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var reset = await _recovery.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

        return reset.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, reset);
    }

    /// <summary>What an invitation says, while it can be accepted. POST so the token stays out of URLs and logs.</summary>
    [HttpPost("invitations/preview")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType<InvitationPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationPreviewDto>> PreviewInvitationAsync(
        [FromBody] InvitationTokenRequest request,
        CancellationToken cancellationToken)
    {
        var preview = await _invitations.PreviewAsync(request.Token, cancellationToken).ConfigureAwait(false);

        return preview.IsSuccess ? Ok(preview.Value) : ProblemResults.FromResult(HttpContext, preview);
    }

    /// <summary>
    /// Creates the invited account, signs the caller in as it and applies the memberships the invitation promised.
    /// Answers with the new session, like a sign-in.
    /// </summary>
    [HttpPost("invitations/accept")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessionDto>> AcceptInvitationAsync(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var accepted = await _invitations.AcceptAsync(request, cancellationToken).ConfigureAwait(false);
        if (accepted.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, accepted);
        }

        // The account exists whatever happens next; the memberships were checked when the invitation was made.
        await _provisioning.ApplyMembershipsAsync(
                accepted.Value.UserId,
                accepted.Value.Email,
                accepted.Value.GroupIds,
                accepted.Value.OrganizationIds,
                cancellationToken)
            .ConfigureAwait(false);

        // As after a sign-in: the session the cookie now stands for (the client refreshes it either way).
        var session = await _authentication.GetSessionAsync(cancellationToken).ConfigureAwait(false);

        return Ok(session.IsSuccess ? session.Value : SessionDto.AnonymousFor(_authentication.PasswordPolicy));
    }
}
