using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Web.Infrastructure;

using OpenPortal.Web.Authorization;

namespace OpenPortal.Web.Controllers;

/// <summary>Ending a support session. Starting one is an administrator's action on a user (<c>/api/admin/users</c>).</summary>
[ApiController]
[Route("api/auth/impersonation")]
[Authorize]
[AllowDuringTwoFactorSetup]
[Produces("application/json")]
public sealed class ImpersonationController : ControllerBase
{
    private readonly IImpersonationService _impersonation;

    public ImpersonationController(IImpersonationService impersonation)
    {
        _impersonation = impersonation;
    }

    /// <summary>Ends the support session and signs the administrator back in as themselves.</summary>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StopAsync(CancellationToken cancellationToken)
    {
        var stopped = await _impersonation.StopAsync(cancellationToken).ConfigureAwait(false);

        return stopped.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, stopped);
    }
}
