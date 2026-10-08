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

    public AdminSettingsController(ISecuritySettingsService security)
    {
        _security = security;
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
}
