using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Legal;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The privacy notice and the terms of use in force, for everyone (the registration form links them), and the
/// signed-in user's acceptance of them.
/// </summary>
[ApiController]
[Route("api/legal")]
[Produces("application/json")]
public sealed class LegalController : ControllerBase
{
    private readonly ILegalDocumentService _legal;

    public LegalController(ILegalDocumentService legal)
    {
        _legal = legal;
    }

    /// <summary>The version of <c>privacy</c> or <c>terms</c> in force, in the request's language.</summary>
    [AllowAnonymous]
    [HttpGet("{kind}")]
    [ProducesResponseType<PublishedLegalDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishedLegalDocumentDto>> GetAsync(string kind, CancellationToken cancellationToken)
    {
        var document = await _legal.GetCurrentAsync(kind, cancellationToken).ConfigureAwait(false);

        return document is not null
            ? Ok(document)
            : ProblemResults.FromResult(HttpContext, Result.Failure(LegalErrors.NotFound));
    }

    /// <summary>
    /// Accepts the given versions in force for the signed-in account and returns what is still pending (nothing,
    /// normally). Refused (400 <c>legal.acceptance_required</c>) when a version is no longer the one in force. Only the
    /// account holder accepts: never a support session or a token.
    /// </summary>
    [Authorize]
    [InteractiveSessionOnly]
    [HttpPost("~/api/account/legal/accept")]
    [ProducesResponseType<IReadOnlyList<PendingLegalDocumentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PendingLegalDocumentDto>>> AcceptAsync(
        [FromBody] AcceptLegalDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        var pending = await _legal.AcceptAsync(request, cancellationToken).ConfigureAwait(false);

        return pending.IsSuccess ? Ok(pending.Value) : ProblemResults.FromResult(HttpContext, pending);
    }
}

/// <summary>Writing and publishing the privacy notice and the terms of use, for holders of the legal page.</summary>
[ApiController]
[Route("api/admin/legal")]
[Authorize]
[Produces("application/json")]
public sealed class AdminLegalController : ControllerBase
{
    private readonly ILegalDocumentService _legal;

    public AdminLegalController(ILegalDocumentService legal)
    {
        _legal = legal;
    }

    /// <summary>Every version of both documents: drafts, the one in force and those it superseded.</summary>
    [RequirePortalPage(PortalPages.Legal)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LegalDocumentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LegalDocumentDto>>> ListAsync(CancellationToken cancellationToken) =>
        Respond(await _legal.ListAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Starts the next version of <c>privacy</c> or <c>terms</c> as a draft (one draft per kind).</summary>
    [RequirePortalPage(PortalPages.Legal)]
    [HttpPost("{kind}")]
    [ProducesResponseType<LegalDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LegalDocumentDto>> CreateDraftAsync(
        string kind,
        [FromBody] SaveLegalDocumentRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _legal.CreateDraftAsync(kind, request, cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.Legal)]
    [HttpPut("{documentId:guid}")]
    [ProducesResponseType<LegalDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LegalDocumentDto>> UpdateDraftAsync(
        Guid documentId,
        [FromBody] SaveLegalDocumentRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _legal.UpdateDraftAsync(documentId, request, cancellationToken).ConfigureAwait(false));

    /// <summary>Puts a draft in force. When it requires acceptance, every account is asked to accept it at its next visit.</summary>
    [RequirePortalPage(PortalPages.Legal)]
    [HttpPost("{documentId:guid}/publish")]
    [ProducesResponseType<LegalDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LegalDocumentDto>> PublishAsync(Guid documentId, CancellationToken cancellationToken) =>
        Respond(await _legal.PublishAsync(documentId, cancellationToken).ConfigureAwait(false));

    [RequirePortalPage(PortalPages.Legal)]
    [HttpDelete("{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteDraftAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var deleted = await _legal.DeleteDraftAsync(documentId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}
