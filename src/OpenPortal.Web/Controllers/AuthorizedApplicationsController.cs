using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The applications the signed-in user let sign them in, and taking that back (Account → Security). Taking it back
/// ends what the application holds (its refresh tokens); whether the user may open the application is unchanged.
/// </summary>
[ApiController]
[Route("api/account/authorized-applications")]
[Authorize]
[Produces("application/json")]
public sealed class AuthorizedApplicationsController : ControllerBase
{
    private readonly IAuthorizedApplicationService _authorized;

    public AuthorizedApplicationsController(IAuthorizedApplicationService authorized)
    {
        _authorized = authorized;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuthorizedApplicationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuthorizedApplicationDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await _authorized.ListMineAsync(cancellationToken).ConfigureAwait(false);

        return list.IsSuccess ? Ok(list.Value) : ProblemResults.FromResult(HttpContext, list);
    }

    [InteractiveSessionOnly]
    [HttpDelete("{clientId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAsync(string clientId, CancellationToken cancellationToken)
    {
        var revoked = await _authorized.RevokeMineAsync(clientId, cancellationToken).ConfigureAwait(false);

        return revoked.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, revoked);
    }
}
