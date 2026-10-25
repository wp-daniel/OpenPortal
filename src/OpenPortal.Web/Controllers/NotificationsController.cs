using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Notifications;

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

    /// <summary>
    /// A server-sent event stream (<c>text/event-stream</c>) that sends <c>count</c> with the unread number at once and
    /// whenever the caller's notifications change, and a comment every 25 seconds so proxies keep it open. The
    /// browser's EventSource reconnects by itself; the bell still polls now and then in case it cannot.
    /// </summary>
    [HttpGet("stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task StreamAsync([FromServices] NotificationHub hub, [FromServices] ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = hub.Subscribe(userId);

        try
        {
            await WriteCountAsync(cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var changed = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();
                var finished = await Task.WhenAny(changed, Task.Delay(TimeSpan.FromSeconds(25), cancellationToken)).ConfigureAwait(false);

                if (finished == changed && await changed.ConfigureAwait(false))
                {
                    while (subscription.Reader.TryRead(out _))
                    {
                    }

                    await WriteCountAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Response.WriteAsync(": keep-alive\n\n", cancellationToken).ConfigureAwait(false);
                    await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The browser went away.
        }
    }

    private async Task WriteCountAsync(CancellationToken cancellationToken)
    {
        var count = await _notifications.CountAsync(cancellationToken).ConfigureAwait(false);
        if (count.IsFailure)
        {
            return;
        }

        await Response.WriteAsync(
                $"event: count\ndata: {System.Text.Json.JsonSerializer.Serialize(count.Value, System.Text.Json.JsonSerializerOptions.Web)}\n\n",
                cancellationToken)
            .ConfigureAwait(false);
        await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Marks a notification read. Idempotent.</summary>
    [PublishesNotificationChange]
    [HttpPut("{notificationId:guid}/read")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken) =>
        Respond(await _notifications.SetReadAsync(notificationId, read: true, cancellationToken).ConfigureAwait(false));

    /// <summary>Marks a notification unread again. Idempotent.</summary>
    [PublishesNotificationChange]
    [HttpDelete("{notificationId:guid}/read")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> MarkUnreadAsync(Guid notificationId, CancellationToken cancellationToken) =>
        Respond(await _notifications.SetReadAsync(notificationId, read: false, cancellationToken).ConfigureAwait(false));

    /// <summary>Marks every notification read.</summary>
    [PublishesNotificationChange]
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var marked = await _notifications.MarkAllReadAsync(cancellationToken).ConfigureAwait(false);

        return marked.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, marked);
    }

    [PublishesNotificationChange]
    [HttpDelete("{notificationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var deleted = await _notifications.DeleteAsync(notificationId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    /// <summary>Deletes every notification already read.</summary>
    [PublishesNotificationChange]
    [HttpDelete("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteReadAsync(CancellationToken cancellationToken)
    {
        var deleted = await _notifications.DeleteReadAsync(cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    /// <summary>Which notifications are emailed to the caller: <c>security</c> (the default), <c>instant</c>, <c>daily</c> or <c>off</c>.</summary>
    [HttpGet("preferences")]
    [ProducesResponseType<NotificationPreferencesDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferencesAsync(
        [FromServices] INotificationPreferenceService preferences,
        CancellationToken cancellationToken) =>
        Respond(await preferences.GetMineAsync(cancellationToken).ConfigureAwait(false));

    [HttpPut("preferences")]
    [ProducesResponseType<NotificationPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdatePreferencesAsync(
        [FromBody] UpdateNotificationPreferencesRequest request,
        [FromServices] INotificationPreferenceService preferences,
        CancellationToken cancellationToken) =>
        Respond(await preferences.UpdateMineAsync(request, cancellationToken).ConfigureAwait(false));

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}
