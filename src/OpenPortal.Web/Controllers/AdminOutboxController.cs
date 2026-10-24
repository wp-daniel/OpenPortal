using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The delivery queue (emails, webhooks, logout notices to applications): what is waiting, what failed and why,
/// and a retry. Administrators only, next to the email settings.
/// </summary>
[ApiController]
[Route("api/admin/outbox")]
[Authorize(Policy = Policies.AdministratorOnly)]
[Produces("application/json")]
public sealed class AdminOutboxController : ControllerBase
{
    private readonly IOutboxAdministration _outbox;

    public AdminOutboxController(IOutboxAdministration outbox)
    {
        _outbox = outbox;
    }

    [HttpGet]
    [ProducesResponseType<OutboxPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OutboxPageDto>> ListAsync([FromQuery] OutboxQuery query, CancellationToken cancellationToken)
    {
        var page = await _outbox.ListAsync(query, cancellationToken).ConfigureAwait(false);

        return page.IsSuccess ? Ok(page.Value) : ProblemResults.FromResult(HttpContext, page);
    }

    [HttpPost("{messageId:guid}/retry")]
    [ProducesResponseType<OutboxMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OutboxMessageDto>> RetryAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await _outbox.RetryAsync(messageId, cancellationToken).ConfigureAwait(false);

        return message.IsSuccess ? Ok(message.Value) : ProblemResults.FromResult(HttpContext, message);
    }

    /// <summary>Puts every failed message back in the queue.</summary>
    [HttpPost("retry-failed")]
    [ProducesResponseType<RetriedDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RetriedDto>> RetryFailedAsync(CancellationToken cancellationToken)
    {
        var count = await _outbox.RetryFailedAsync(cancellationToken).ConfigureAwait(false);

        return count.IsSuccess ? Ok(new RetriedDto(count.Value)) : ProblemResults.FromResult(HttpContext, count);
    }

    [HttpDelete("{messageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var deleted = await _outbox.DeleteAsync(messageId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    public sealed record RetriedDto(int Count);
}
