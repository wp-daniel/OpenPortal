using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The signed-in user's side of access requests: the applications they may ask for, their requests, asking and
/// withdrawing. Always about the caller.
/// </summary>
[ApiController]
[Route("api/account/access-requests")]
[Authorize]
[Produces("application/json")]
public sealed class AccessRequestsController : ControllerBase
{
    private readonly IAccessRequestService _requests;

    public AccessRequestsController(IAccessRequestService requests)
    {
        _requests = requests;
    }

    /// <summary>Applications the caller cannot open but may ask for, each with their latest request.</summary>
    [HttpGet("catalog")]
    [ProducesResponseType<IReadOnlyList<RequestableApplicationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequestableApplicationDto>>> CatalogAsync(CancellationToken cancellationToken) =>
        Respond(await _requests.GetCatalogAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>The caller's requests, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MyAccessRequestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyAccessRequestDto>>> ListAsync(CancellationToken cancellationToken) =>
        Respond(await _requests.ListMineAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Asks for an application. The reviewers are notified.</summary>
    [HttpPost]
    [ProducesResponseType<MyAccessRequestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyAccessRequestDto>> SubmitAsync(
        [FromBody] SubmitAccessRequest request,
        CancellationToken cancellationToken)
    {
        var submitted = await _requests.SubmitAsync(request, cancellationToken).ConfigureAwait(false);

        return submitted.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, submitted.Value)
            : ProblemResults.FromResult(HttpContext, submitted);
    }

    /// <summary>Withdraws a pending request.</summary>
    [HttpDelete("{requestId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var cancelled = await _requests.CancelAsync(requestId, cancellationToken).ConfigureAwait(false);

        return cancelled.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, cancelled);
    }

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}

/// <summary>The reviewers' side of access requests, for administrators and holders of the access requests page.</summary>
[ApiController]
[Route("api/admin/access-requests")]
[Authorize]
[Produces("application/json")]
public sealed class AdminAccessRequestsController : ControllerBase
{
    private readonly IAccessRequestService _requests;

    public AdminAccessRequestsController(IAccessRequestService requests)
    {
        _requests = requests;
    }

    /// <summary>Requests, filtered by status and application: pending ones first.</summary>
    [RequirePortalPage(PortalPages.AccessRequests)]
    [HttpGet]
    [ProducesResponseType<AccessRequestPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AccessRequestPageDto>> ListAsync([FromQuery] AccessRequestQuery query, CancellationToken cancellationToken) =>
        Respond(await _requests.ListAsync(query, cancellationToken).ConfigureAwait(false));

    /// <summary>Grants the application to the requester with the chosen roles, and tells them.</summary>
    [RequirePortalPage(PortalPages.AccessRequests)]
    [HttpPost("{requestId:guid}/approve")]
    [ProducesResponseType<AccessRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccessRequestDto>> ApproveAsync(
        Guid requestId,
        [FromBody] ReviewAccessRequest review,
        CancellationToken cancellationToken) =>
        Respond(await _requests.ApproveAsync(requestId, review, cancellationToken).ConfigureAwait(false));

    /// <summary>Refuses the request, and tells the requester.</summary>
    [RequirePortalPage(PortalPages.AccessRequests)]
    [HttpPost("{requestId:guid}/reject")]
    [ProducesResponseType<AccessRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccessRequestDto>> RejectAsync(
        Guid requestId,
        [FromBody] ReviewAccessRequest review,
        CancellationToken cancellationToken) =>
        Respond(await _requests.RejectAsync(requestId, review, cancellationToken).ConfigureAwait(false));

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}
