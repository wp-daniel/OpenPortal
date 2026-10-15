using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
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

    public AdminSettingsController(
        ISecuritySettingsService security,
        IRegistrationSettingsService registration,
        IPrivacySettingsService privacy)
    {
        _security = security;
        _registration = registration;
        _privacy = privacy;
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
}
