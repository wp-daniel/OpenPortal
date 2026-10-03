using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public AccountController(IAccountService account)
    {
        _account = account;
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

    /// <summary>
    /// Replaces the signed-in account's password.
    /// <para>
    /// Changing the password rotates the security stamp, which invalidates every other session for that
    /// account. The current request completes normally, so the caller should be told to sign in again rather
    /// than be logged out mid-response.
    /// </para>
    /// </summary>
    [HttpPost("change-password")]
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