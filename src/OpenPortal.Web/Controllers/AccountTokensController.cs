using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The caller's own personal access tokens. Managed only from their browser session: a token can never create or
/// revoke tokens, and neither can a support session.
/// </summary>
[ApiController]
[Route("api/account/tokens")]
[Authorize]
[InteractiveSessionOnly]
[Produces("application/json")]
public sealed class AccountTokensController : ControllerBase
{
    private readonly IPersonalAccessTokenService _tokens;

    public AccountTokensController(IPersonalAccessTokenService tokens)
    {
        _tokens = tokens;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PersonalAccessTokenDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PersonalAccessTokenDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var tokens = await _tokens.ListMineAsync(cancellationToken).ConfigureAwait(false);

        return tokens.IsSuccess ? Ok(tokens.Value) : ProblemResults.FromResult(HttpContext, tokens);
    }

    /// <summary>Creates a token and returns its value, the only time it is shown.</summary>
    [HttpPost]
    [ProducesResponseType<CreatedPersonalAccessTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedPersonalAccessTokenDto>> CreateAsync(
        [FromBody] CreatePersonalAccessTokenRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _tokens.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        return created.IsSuccess ? Ok(created.Value) : ProblemResults.FromResult(HttpContext, created);
    }

    [HttpDelete("{tokenId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        var revoked = await _tokens.RevokeMineAsync(tokenId, cancellationToken).ConfigureAwait(false);

        return revoked.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, revoked);
    }
}
