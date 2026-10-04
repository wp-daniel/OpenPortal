using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// Administrator-only access grants. Grants are idempotent PUT/DELETE pairs, so a retried request or a
/// double click cannot fail or create a duplicate.
/// </summary>
[ApiController]
[Route("api/admin/access")]
[Authorize(Policy = Policies.AdministratorOnly)]
[Produces("application/json")]
public sealed class AdminAccessController : ControllerBase
{
    private readonly IAccessAdministrationService _access;

    public AdminAccessController(IAccessAdministrationService access)
    {
        _access = access;
    }

    /// <summary>Every application with its groups (and their members) and its directly granted users.</summary>
    [HttpGet("tree")]
    [ProducesResponseType<AccessTreeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AccessTreeDto>> GetTreeAsync(CancellationToken cancellationToken)
    {
        var tree = await _access.GetTreeAsync(cancellationToken).ConfigureAwait(false);

        return tree.IsSuccess ? Ok(tree.Value) : ProblemResults.FromResult(HttpContext, tree);
    }

    /// <summary>What one user can open, and why.</summary>
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType<UserAccessDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserAccessDto>> GetUserAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var access = await _access.GetUserAccessAsync(userId, cancellationToken).ConfigureAwait(false);

        return access.IsSuccess ? Ok(access.Value) : ProblemResults.FromResult(HttpContext, access);
    }

    [HttpPut("applications/{applicationId:guid}/users/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantUserAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken) =>
        Respond(await _access.GrantUserAsync(applicationId, userId, cancellationToken).ConfigureAwait(false));

    [HttpDelete("applications/{applicationId:guid}/users/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeUserAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken) =>
        Respond(await _access.RevokeUserAsync(applicationId, userId, cancellationToken).ConfigureAwait(false));

    [HttpPut("applications/{applicationId:guid}/groups/{groupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantGroupAsync(Guid applicationId, Guid groupId, CancellationToken cancellationToken) =>
        Respond(await _access.GrantGroupAsync(applicationId, groupId, cancellationToken).ConfigureAwait(false));

    [HttpDelete("applications/{applicationId:guid}/groups/{groupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeGroupAsync(Guid applicationId, Guid groupId, CancellationToken cancellationToken) =>
        Respond(await _access.RevokeGroupAsync(applicationId, groupId, cancellationToken).ConfigureAwait(false));

    private IActionResult Respond(Result result) =>
        result.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, result);
}
