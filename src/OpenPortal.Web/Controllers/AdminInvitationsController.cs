using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>Invitations by email, for administrators and holders of the users page.</summary>
[ApiController]
[Route("api/admin/invitations")]
[Authorize]
[Produces("application/json")]
public sealed class AdminInvitationsController : ControllerBase
{
    private readonly IInvitationService _invitations;
    private readonly IAccessProvisioningService _provisioning;

    public AdminInvitationsController(IInvitationService invitations, IAccessProvisioningService provisioning)
    {
        _invitations = invitations;
        _provisioning = provisioning;
    }

    [RequirePortalPage(PortalPages.Users)]
    [HttpGet]
    [ProducesResponseType<PagedResult<InvitationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<InvitationDto>>> ListAsync([FromQuery] InvitationQuery query, CancellationToken cancellationToken)
    {
        var page = await _invitations.ListAsync(query, cancellationToken).ConfigureAwait(false);

        return page.IsSuccess ? Ok(page.Value) : ProblemResults.FromResult(HttpContext, page);
    }

    /// <summary>
    /// Invites an address. The groups and organizations it promises must exist and be the caller's to give (the
    /// groups or organizations page). The answer carries the link, once.
    /// </summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpPost]
    [ProducesResponseType<InvitationIssuedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationIssuedDto>> CreateAsync(
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var memberships = await _provisioning.ValidateMembershipsAsync(
                request.GroupIds ?? [],
                request.OrganizationIds ?? [],
                cancellationToken)
            .ConfigureAwait(false);

        if (memberships.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, memberships);
        }

        var issued = await _invitations.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        return issued.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, issued.Value)
            : ProblemResults.FromResult(HttpContext, issued);
    }

    /// <summary>Issues a new link (the old one stops working), restarts its validity and emails it again.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpPost("{invitationId:guid}/resend")]
    [ProducesResponseType<InvitationIssuedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationIssuedDto>> ResendAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var issued = await _invitations.ResendAsync(invitationId, cancellationToken).ConfigureAwait(false);

        return issued.IsSuccess ? Ok(issued.Value) : ProblemResults.FromResult(HttpContext, issued);
    }

    /// <summary>Withdraws an invitation; its link stops working.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpDelete("{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var revoked = await _invitations.RevokeAsync(invitationId, cancellationToken).ConfigureAwait(false);

        return revoked.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, revoked);
    }
}
