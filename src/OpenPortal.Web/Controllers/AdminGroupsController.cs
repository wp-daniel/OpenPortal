using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>Administrator-only management of user groups.</summary>
[ApiController]
[Route("api/admin/groups")]
[Authorize(Policy = Policies.AdministratorOnly)]
[Produces("application/json")]
public sealed class AdminGroupsController : ControllerBase
{
    private readonly IGroupService _groups;

    public AdminGroupsController(IGroupService groups)
    {
        _groups = groups;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GroupSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GroupSummaryDto>>> ListAsync(CancellationToken cancellationToken) =>
        Respond(await _groups.ListAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("{groupId:guid}")]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDetailDto>> GetAsync(Guid groupId, CancellationToken cancellationToken) =>
        Respond(await _groups.GetAsync(groupId, cancellationToken).ConfigureAwait(false));

    [HttpPost]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupDetailDto>> CreateAsync(
        [FromBody] SaveGroupRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _groups.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, created);
        }

        return CreatedAtAction(nameof(GetAsync), new { groupId = created.Value.Id }, created.Value);
    }

    [HttpPut("{groupId:guid}")]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupDetailDto>> UpdateAsync(
        Guid groupId,
        [FromBody] SaveGroupRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _groups.UpdateAsync(groupId, request, cancellationToken).ConfigureAwait(false));

    [HttpDelete("{groupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var deleted = await _groups.DeleteAsync(groupId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    /// <summary>Adds a member. Idempotent.</summary>
    [HttpPut("{groupId:guid}/members/{userId:guid}")]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDetailDto>> AddMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken) =>
        Respond(await _groups.AddMemberAsync(groupId, userId, cancellationToken).ConfigureAwait(false));

    /// <summary>Removes a member. Idempotent.</summary>
    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDetailDto>> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken) =>
        Respond(await _groups.RemoveMemberAsync(groupId, userId, cancellationToken).ConfigureAwait(false));

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}
