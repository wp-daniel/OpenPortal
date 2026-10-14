using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>The signed-in user's notification center. Every endpoint works on the caller's own notifications.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <summary>The caller's notifications, newest first, with the number still unread.</summary>
    [HttpGet]
    [ProducesResponseType<NotificationPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationPageDto>> ListAsync([FromQuery] NotificationQuery query, CancellationToken cancellationToken) =>
        Respond(await _notifications.ListAsync(query, cancellationToken).ConfigureAwait(false));

    /// <summary>How many notifications are unread: cheap enough to poll.</summary>
    [HttpGet("count")]
    [ProducesResponseType<NotificationCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationCountDto>> CountAsync(CancellationToken cancellationToken) =>
        Respond(await _notifications.CountAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Marks a notification read. Idempotent.</summary>
    [HttpPut("{notificationId:guid}/read")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken) =>
        Respond(await _notifications.SetReadAsync(notificationId, read: true, cancellationToken).ConfigureAwait(false));

    /// <summary>Marks a notification unread again. Idempotent.</summary>
    [HttpDelete("{notificationId:guid}/read")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> MarkUnreadAsync(Guid notificationId, CancellationToken cancellationToken) =>
        Respond(await _notifications.SetReadAsync(notificationId, read: false, cancellationToken).ConfigureAwait(false));

    /// <summary>Marks every notification read.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var marked = await _notifications.MarkAllReadAsync(cancellationToken).ConfigureAwait(false);

        return marked.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, marked);
    }

    [HttpDelete("{notificationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var deleted = await _notifications.DeleteAsync(notificationId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    /// <summary>Deletes every notification already read.</summary>
    [HttpDelete("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteReadAsync(CancellationToken cancellationToken)
    {
        var deleted = await _notifications.DeleteReadAsync(cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}
