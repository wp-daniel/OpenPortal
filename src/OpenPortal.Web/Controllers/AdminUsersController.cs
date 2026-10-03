using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>Administrator-only account management.</summary>
/// <para>
/// The policy here is the first gate. Every service behind these routes re-checks the caller's role before
/// mutating anything, so the endpoints cannot be bypassed by calling the service from elsewhere.
/// </para>
/// </remarks>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.AdministratorOnly)]
[Produces("application/json")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserAdministrationService _users;

    public AdminUsersController(IUserAdministrationService users)
    {
        _users = users;
    }

    /// <summary>Lists accounts, filtered and paged.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> ListAsync(
        [FromQuery] UserListQuery query,
        CancellationToken cancellationToken)
    {
        var users = await _users.ListUsersAsync(query, cancellationToken).ConfigureAwait(false);

        return users.IsSuccess
            ? Ok(users.Value)
            : ProblemResults.FromResult(HttpContext, users);
    }

    /// <summary>Returns one account.</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserSummaryDto>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return user.IsSuccess
            ? Ok(user.Value)
            : ProblemResults.FromResult(HttpContext, user);
    }

    /// <summary>Creates an account.</summary>
    [HttpPost]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserSummaryDto>> CreateAsync(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _users.CreateUserAsync(request, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, created);
        }

        return CreatedAtAction(nameof(GetAsync), new { userId = created.Value.Id }, created.Value);
    }

    /// <summary>Updates an account's display name and role membership.</summary>
    [HttpPut("{userId:guid}")]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserSummaryDto>> UpdateAsync(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _users.UpdateUserAsync(userId, request, cancellationToken).ConfigureAwait(false);

        return updated.IsSuccess
            ? Ok(updated.Value)
            : ProblemResults.FromResult(HttpContext, updated);
    }

    /// <summary>Sets a new password and ends that account's existing sessions.</summary>
    [HttpPost("{userId:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetPasswordAsync(
        Guid userId,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var reset = await _users.ResetPasswordAsync(userId, request, cancellationToken).ConfigureAwait(false);

        return reset.IsSuccess
            ? NoContent()
            : ProblemResults.FromResult(HttpContext, reset);
    }
}