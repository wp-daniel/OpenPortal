using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>Organizations (tenants) and their members, for administrators and holders of the organizations page.</summary>
[ApiController]
[Route("api/admin/organizations")]
[Authorize]
[Produces("application/json")]
public sealed class AdminOrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizations;

    public AdminOrganizationsController(IOrganizationService organizations)
    {
        _organizations = organizations;
    }

    [RequirePortalPage(PortalPages.Organizations, PortalPages.Users)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OrganizationSummaryDto>>> ListAsync(CancellationToken cancellationToken) =>
        Respond(await _organizations.ListAsync(cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.Organizations)]
    [HttpGet("{organizationId:guid}")]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDetailDto>> GetAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Respond(await _organizations.GetAsync(organizationId, cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.Organizations)]
    [HttpPost]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationDetailDto>> CreateAsync(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _organizations.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, created);
        }

        return CreatedAtAction(nameof(GetAsync), new { organizationId = created.Value.Id }, created.Value);
    }

    [RequirePortalPage(PortalPages.Organizations)]
    [HttpPut("{organizationId:guid}")]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDetailDto>> UpdateAsync(
        Guid organizationId,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _organizations.UpdateAsync(organizationId, request, cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.Organizations)]
    [HttpDelete("{organizationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var deleted = await _organizations.DeleteAsync(organizationId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    /// <summary>Adds a member, or changes the role of one (<c>{ "role": "member" | "admin" }</c>). Idempotent.</summary>
    [RequirePortalPage(PortalPages.Organizations)]
    [HttpPut("{organizationId:guid}/members/{userId:guid}")]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDetailDto>> SetMemberAsync(
        Guid organizationId,
        Guid userId,
        [FromBody] SetOrganizationMemberRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _organizations.SetMemberAsync(organizationId, userId, request, cancellationToken).ConfigureAwait(false));

    /// <summary>Removes a member. Idempotent.</summary>
    [RequirePortalPage(PortalPages.Organizations)]
    [HttpDelete("{organizationId:guid}/members/{userId:guid}")]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDetailDto>> RemoveMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        Respond(await _organizations.RemoveMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false));

    /// <summary>The organizations one user belongs to.</summary>
    [RequirePortalPage(PortalPages.Organizations, PortalPages.Users)]
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType<IReadOnlyList<OrganizationMembershipDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizationMembershipDto>>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        Respond(await _organizations.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false));

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}

/// <summary>The organizations the signed-in user belongs to (their account page, the organization picker).</summary>
[ApiController]
[Route("api/account/organizations")]
[Authorize]
[Produces("application/json")]
public sealed class MyOrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizations;

    public MyOrganizationsController(IOrganizationService organizations)
    {
        _organizations = organizations;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationMembershipDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizationMembershipDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var mine = await _organizations.ListMineAsync(cancellationToken).ConfigureAwait(false);

        return mine.IsSuccess ? Ok(mine.Value) : ProblemResults.FromResult(HttpContext, mine);
    }

    // Delegated management: an organization's own administrators manage its members from their account, without
    // any portal page. The service checks that the caller administers that organization.

    [HttpGet("{organizationId:guid}")]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationDetailDto>> GetAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Respond(await _organizations.GetAsync(organizationId, cancellationToken).ConfigureAwait(false));

    [HttpPost("{organizationId:guid}/members")]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDetailDto>> AddMemberAsync(
        Guid organizationId,
        [FromBody] AddOrganizationMemberRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _organizations.AddMemberByEmailAsync(organizationId, request, cancellationToken).ConfigureAwait(false));

    [HttpPut("{organizationId:guid}/members/{userId:guid}")]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationDetailDto>> SetMemberAsync(
        Guid organizationId,
        Guid userId,
        [FromBody] SetOrganizationMemberRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _organizations.SetMemberAsync(organizationId, userId, request, cancellationToken).ConfigureAwait(false));

    [HttpDelete("{organizationId:guid}/members/{userId:guid}")]
    [ProducesResponseType<OrganizationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationDetailDto>> RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        Respond(await _organizations.RemoveMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false));

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}
