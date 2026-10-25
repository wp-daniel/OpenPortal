using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The signed-in account's sessions: where it is signed in, and ending a session left open on another browser. Only
/// the account holder, signed in themselves, may end sessions (not a support session, not a token).
/// </summary>
[ApiController]
[Route("api/account/sessions")]
[Authorize]
[Produces("application/json")]
public sealed class AccountSessionsController : ControllerBase
{
    private readonly ISessionService _sessions;

    public AccountSessionsController(ISessionService sessions)
    {
        _sessions = sessions;
    }

    [HttpGet]
    [ProducesResponseType<AccountSessionsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountSessionsDto>> ListAsync(CancellationToken cancellationToken)
    {
        var sessions = await _sessions.ListOwnAsync(cancellationToken).ConfigureAwait(false);

        return sessions.IsSuccess ? Ok(sessions.Value) : ProblemResults.FromResult(HttpContext, sessions);
    }

    /// <summary>Ends another session of this account; the browser holding it is signed out at its next request.</summary>
    [InteractiveSessionOnly]
    [HttpDelete("{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeAsync(string sessionId, CancellationToken cancellationToken)
    {
        var revoked = await _sessions.RevokeOwnAsync(sessionId, cancellationToken).ConfigureAwait(false);

        return revoked.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, revoked);
    }

    /// <summary>Ends every session of this account except the one making the request.</summary>
    [InteractiveSessionOnly]
    [HttpPost("revoke-others")]
    [ProducesResponseType<RevokedSessionsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RevokedSessionsDto>> RevokeOthersAsync(CancellationToken cancellationToken)
    {
        var revoked = await _sessions.RevokeOtherOwnAsync(cancellationToken).ConfigureAwait(false);

        return revoked.IsSuccess ? Ok(new RevokedSessionsDto(revoked.Value)) : ProblemResults.FromResult(HttpContext, revoked);
    }
}

/// <summary>How many sessions ended.</summary>
public sealed record RevokedSessionsDto(int Count);
