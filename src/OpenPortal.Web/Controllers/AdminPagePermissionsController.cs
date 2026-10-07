using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// Which groups may open which portal pages. Administrator-only and never delegated: whoever could grant
/// pages could grant themselves every page. Grants are idempotent PUT/DELETE pairs, like application grants.
/// </summary>
[ApiController]
[Route("api/admin/page-permissions")]
[Authorize(Policy = Policies.AdministratorOnly)]
[Produces("application/json")]
public sealed class AdminPagePermissionsController : ControllerBase
{
    private readonly IPagePermissionService _pages;

    public AdminPagePermissionsController(IPagePermissionService pages)
    {
        _pages = pages;
    }

    /// <summary>Every grantable page, every group, and the grants between them.</summary>
    [HttpGet]
    [ProducesResponseType<PagePermissionMatrixDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagePermissionMatrixDto>> GetAsync(CancellationToken cancellationToken)
    {
        var matrix = await _pages.GetMatrixAsync(cancellationToken).ConfigureAwait(false);

        return matrix.IsSuccess ? Ok(matrix.Value) : ProblemResults.FromResult(HttpContext, matrix);
    }

    [HttpPut("{pageKey}/groups/{groupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantAsync(string pageKey, Guid groupId, CancellationToken cancellationToken) =>
        Respond(await _pages.GrantAsync(pageKey, groupId, cancellationToken).ConfigureAwait(false));

    [HttpDelete("{pageKey}/groups/{groupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAsync(string pageKey, Guid groupId, CancellationToken cancellationToken) =>
        Respond(await _pages.RevokeAsync(pageKey, groupId, cancellationToken).ConfigureAwait(false));

    /// <summary>Replaces every page of one group, so a whole area can be switched in one change.</summary>
    [HttpPut("groups/{groupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetGroupPagesAsync(
        Guid groupId,
        [FromBody] SetGroupPagesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Respond(await _pages
            .SetGroupPagesAsync(groupId, request.Pages ?? [], cancellationToken)
            .ConfigureAwait(false));
    }

    private IActionResult Respond(Result result) =>
        result.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, result);
}
