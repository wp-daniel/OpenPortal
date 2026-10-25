using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Web.Email;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// Portal settings stored in the database and edited from the portal. Administrators only: sign-in security is
/// never delegated through page grants.
/// </summary>
[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = Policies.AdministratorOnly)]
[Produces("application/json")]
public sealed class AdminSettingsController : ControllerBase
{
    private readonly ISecuritySettingsService _security;
    private readonly IRegistrationSettingsService _registration;
    private readonly IPrivacySettingsService _privacy;
    private readonly IMailSettingsService _mail;
    private readonly MailTester _mailTester;

    public AdminSettingsController(
        ISecuritySettingsService security,
        IRegistrationSettingsService registration,
        IPrivacySettingsService privacy,
        IMailSettingsService mail,
        MailTester mailTester)
    {
        _security = security;
        _registration = registration;
        _privacy = privacy;
        _mail = mail;
        _mailTester = mailTester;
    }

    /// <summary>The external sign-in providers (Google, Microsoft); client secrets are never returned.</summary>
    [HttpGet("external-logins")]
    [ProducesResponseType<IReadOnlyList<ExternalProviderSettingsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ExternalProviderSettingsDto>>> ListExternalLoginsAsync(
        [FromServices] IExternalLoginSettingsService externalLogins,
        CancellationToken cancellationToken)
    {
        var providers = await externalLogins.ListAsync(cancellationToken).ConfigureAwait(false);

        return providers.IsSuccess ? Ok(providers.Value) : ProblemResults.FromResult(HttpContext, providers);
    }

    /// <summary>Configures one provider; the change applies to the sign-in page at once, without a restart.</summary>
    [HttpPut("external-logins/{provider}")]
    [ProducesResponseType<ExternalProviderSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExternalProviderSettingsDto>> UpdateExternalLoginAsync(
        string provider,
        [FromBody] UpdateExternalProviderRequest request,
        [FromServices] IExternalLoginSettingsService externalLogins,
        CancellationToken cancellationToken)
    {
        var updated = await externalLogins.UpdateAsync(provider, request, cancellationToken).ConfigureAwait(false);

        return updated.IsSuccess ? Ok(updated.Value) : ProblemResults.FromResult(HttpContext, updated);
    }

    /// <summary>Self-registration and invitations, and whether the portal can send email for them.</summary>
    [HttpGet("registration")]
    [ProducesResponseType<RegistrationSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RegistrationSettingsDto>> GetRegistrationAsync(CancellationToken cancellationToken)
    {
        var settings = await _registration.GetAsync(cancellationToken).ConfigureAwait(false);

        return settings.IsSuccess ? Ok(settings.Value) : ProblemResults.FromResult(HttpContext, settings);
    }

    [HttpPut("registration")]
    [ProducesResponseType<RegistrationSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistrationSettingsDto>> UpdateRegistrationAsync(
        [FromBody] UpdateRegistrationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _registration.UpdateAsync(request, cancellationToken).ConfigureAwait(false);

        return settings.IsSuccess ? Ok(settings.Value) : ProblemResults.FromResult(HttpContext, settings);
    }

    [HttpGet("security")]
    [ProducesResponseType<SecuritySettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SecuritySettingsDto>> GetSecurityAsync(CancellationToken cancellationToken)
    {
        var settings = await _security.GetAsync(cancellationToken).ConfigureAwait(false);

        return settings.IsSuccess ? Ok(settings.Value) : ProblemResults.FromResult(HttpContext, settings);
    }

    [HttpPut("security")]
    [ProducesResponseType<SecuritySettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SecuritySettingsDto>> UpdateSecurityAsync(
        [FromBody] UpdateSecuritySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _security.UpdateAsync(request, cancellationToken).ConfigureAwait(false);

        return settings.IsSuccess ? Ok(settings.Value) : ProblemResults.FromResult(HttpContext, settings);
    }

    /// <summary>Whether account holders may delete their own account, and the grace period before it goes.</summary>
    [HttpGet("privacy")]
    [ProducesResponseType<PrivacySettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PrivacySettingsDto>> GetPrivacyAsync(CancellationToken cancellationToken)
    {
        var settings = await _privacy.GetAsync(cancellationToken).ConfigureAwait(false);

        return settings.IsSuccess ? Ok(settings.Value) : ProblemResults.FromResult(HttpContext, settings);
    }

    [HttpPut("privacy")]
    [ProducesResponseType<PrivacySettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PrivacySettingsDto>> UpdatePrivacyAsync(
        [FromBody] UpdatePrivacySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _privacy.UpdateAsync(request, cancellationToken).ConfigureAwait(false);

        return settings.IsSuccess ? Ok(settings.Value) : ProblemResults.FromResult(HttpContext, settings);
    }

    /// <summary>The SMTP server and sender the portal sends email with. The password is never returned.</summary>
    [HttpGet("email")]
    [ProducesResponseType<MailSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MailSettingsDto>> GetEmailAsync(CancellationToken cancellationToken)
    {
        var settings = await _mail.GetAsync(cancellationToken).ConfigureAwait(false);

        return settings.IsSuccess ? Ok(settings.Value) : ProblemResults.FromResult(HttpContext, settings);
    }

    /// <summary>Saves the SMTP settings; an empty password keeps the stored one, <c>clearPassword</c> removes it.</summary>
    [HttpPut("email")]
    [ProducesResponseType<MailSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MailSettingsDto>> UpdateEmailAsync(
        [FromBody] UpdateMailSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _mail.UpdateAsync(request, cancellationToken).ConfigureAwait(false);

        return settings.IsSuccess ? Ok(settings.Value) : ProblemResults.FromResult(HttpContext, settings);
    }

    /// <summary>
    /// Sends a test email with the settings in the request (saved or not) and returns the mail server's answer.
    /// A failed delivery is a 200 with <c>delivered: false</c> and the server's message.
    /// </summary>
    [HttpPost("email/test")]
    [ProducesResponseType<MailTestResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MailTestResultDto>> TestEmailAsync(
        [FromBody] TestMailSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mailTester.SendAsync(request, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
    }
}
