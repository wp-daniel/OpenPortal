using System.ComponentModel.DataAnnotations;

namespace OpenPortal.Notifications.Application.Contracts;

/// <summary>One notification of the signed-in user.</summary>
/// <param name="Type">Dotted type; the client renders <c>notification.&lt;type&gt;.title</c> and <c>.body</c>.</param>
/// <param name="Parameters">The values the texts mention.</param>
/// <param name="Link">A local path of the portal to open, or null.</param>
/// <param name="ReadAtUtc">When it was read; null while unread.</param>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    IReadOnlyDictionary<string, string?> Parameters,
    string? Link,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);

/// <summary>A page of the signed-in user's notifications, newest first.</summary>
/// <param name="UnreadCount">Unread notifications in total, whatever the filter.</param>
public sealed record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnreadCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

/// <summary>How many notifications wait to be read: what the bell shows, polled cheaply.</summary>
/// <param name="LatestAtUtc">When the newest notification arrived (read or not), so a client can tell something new came.</param>
public sealed record NotificationCountDto(int UnreadCount, DateTimeOffset? LatestAtUtc);

/// <summary>Filter and paging for the notification list.</summary>
public sealed class NotificationQuery
{
    public const int MaxPageSize = 50;

    [Range(1, int.MaxValue, ErrorMessage = "validation.page.min")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "validation.pageSize.range")]
    public int PageSize { get; init; } = 20;

    /// <summary>Only the unread ones.</summary>
    public bool UnreadOnly { get; init; }
}
