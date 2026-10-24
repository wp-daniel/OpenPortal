using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// Access reviews (recertification): a snapshot of an application's grants that reviewers confirm or revoke; the
/// revocations apply when the review is completed.
/// </summary>
[ApiController]
[Route("api/admin/access-reviews")]
[Authorize]
[Produces("application/json")]
public sealed class AdminAccessReviewsController : ControllerBase
{
    private readonly IAccessReviewService _reviews;

    public AdminAccessReviewsController(IAccessReviewService reviews)
    {
        _reviews = reviews;
    }

    [RequirePortalPage(PortalPages.AccessReviews)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccessReviewSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccessReviewSummaryDto>>> ListAsync(CancellationToken cancellationToken) =>
        Respond(await _reviews.ListAsync(cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.AccessReviews)]
    [HttpGet("{reviewId:guid}")]
    [ProducesResponseType<AccessReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccessReviewDto>> GetAsync(Guid reviewId, CancellationToken cancellationToken) =>
        Respond(await _reviews.GetAsync(reviewId, cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.AccessReviews)]
    [HttpPost]
    [ProducesResponseType<AccessReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccessReviewDto>> StartAsync([FromBody] StartAccessReviewRequest request, CancellationToken cancellationToken) =>
        Respond(await _reviews.StartAsync(request, cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.AccessReviews)]
    [HttpPut("{reviewId:guid}/items/{itemId:guid}")]
    [ProducesResponseType<AccessReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccessReviewDto>> DecideAsync(
        Guid reviewId,
        Guid itemId,
        [FromBody] DecideAccessReviewItemRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _reviews.DecideAsync(reviewId, itemId, request, cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.AccessReviews)]
    [HttpPost("{reviewId:guid}/complete")]
    [ProducesResponseType<AccessReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccessReviewDto>> CompleteAsync(Guid reviewId, CancellationToken cancellationToken) =>
        Respond(await _reviews.CompleteAsync(reviewId, cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.AccessReviews)]
    [HttpPost("{reviewId:guid}/cancel")]
    [ProducesResponseType<AccessReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccessReviewDto>> CancelAsync(Guid reviewId, CancellationToken cancellationToken) =>
        Respond(await _reviews.CancelAsync(reviewId, cancellationToken).ConfigureAwait(false));

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}
