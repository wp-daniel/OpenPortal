using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Notifications.Domain;

/// <summary>
/// Something that happened and concerns one person, shown in their notification center until they delete it
/// or the retention sweep does.
/// <para>
/// A notification stores a type and the values its text mentions, never the text itself: the reader's client
/// renders it in the reader's language, and the row stays meaningful when they change language. The values are
/// snapshots (an application name at the time), like the audit log's labels.
/// </para>
/// </summary>
public sealed class Notification
{
    public const int TypeMaxLength = 64;
    public const int ParametersMaxLength = 4000;
    public const int LinkMaxLength = 512;

    // Required by EF Core.
    private Notification()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The account that receives it.</summary>
    public Guid RecipientId { get; private set; }

    /// <summary>Dotted type, e.g. <c>access.request_submitted</c>.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>The values the text mentions, as a JSON object of strings, or null.</summary>
    public string? Parameters { get; private set; }

    /// <summary>A local path of the portal the notification leads to, or null.</summary>
    public string? Link { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>When the recipient read it; null while unread.</summary>
    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    /// <param name="parameters">The values already serialised as JSON (too large a document is dropped).</param>
    public static Result<Notification> Create(
        Guid id,
        Guid recipientId,
        string? type,
        string? parameters,
        string? link,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);

        if (recipientId == Guid.Empty)
        {
            return Result<Notification>.Failure(NotificationErrors.RecipientRequired);
        }

        var trimmedType = type?.Trim();
        if (string.IsNullOrEmpty(trimmedType) || trimmedType.Length > TypeMaxLength || !IsWellFormedType(trimmedType))
        {
            return Result<Notification>.Failure(NotificationErrors.TypeInvalid);
        }

        return Result<Notification>.Success(new Notification
        {
            Id = id,
            RecipientId = recipientId,
            Type = trimmedType,

            // The texts can do without their values better than the recipient can do without the notice.
            Parameters = parameters is { Length: > ParametersMaxLength } ? null : parameters,
            Link = NormaliseLink(link),
            CreatedAtUtc = now,
        });
    }

    /// <summary>Marks it read. Reading it again changes nothing and returns false.</summary>
    public bool MarkRead(DateTimeOffset now)
    {
        if (ReadAtUtc is not null)
        {
            return false;
        }

        ReadAtUtc = now;
        return true;
    }

    /// <summary>Marks it unread again. Returns false when it already was.</summary>
    public bool MarkUnread()
    {
        if (ReadAtUtc is null)
        {
            return false;
        }

        ReadAtUtc = null;
        return true;
    }

    /// <summary>The category of a type: the part before the first dot (<c>access</c> for <c>access.request_submitted</c>).</summary>
    public static string CategoryOf(string type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var dot = type.IndexOf('.', StringComparison.Ordinal);

        return dot < 0 ? type : type[..dot];
    }

    /// <summary>
    /// A local path of the portal (<c>/admin/access-requests?status=pending</c>), or null. Absolute URLs and
    /// protocol-relative ones (<c>//host</c>) are dropped, so a notice can never send its reader off-site.
    /// </summary>
    public static string? NormaliseLink(string? link)
    {
        var trimmed = link?.Trim();

        if (string.IsNullOrEmpty(trimmed)
            || trimmed.Length > LinkMaxLength
            || trimmed[0] != '/'
            || trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/\\", StringComparison.Ordinal)
            || trimmed.Any(char.IsControl))
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>Lower-case ASCII letters, digits and underscores, in dot-separated segments.</summary>
    private static bool IsWellFormedType(string type) =>
        type.Split('.').All(segment =>
            segment.Length > 0 && segment.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'));
}

/// <summary>Stable, machine-readable failure codes for the Notifications module.</summary>
public static class NotificationErrors
{
    private const string Prefix = "notifications.";

    public static Error NotFound { get; } =
        Error.NotFound(Prefix + "not_found", "The notification does not exist.");

    public static Error SignInRequired { get; } =
        Error.Unauthorized(Prefix + "sign_in_required", "Notifications belong to a signed-in user.");

    public static Error RecipientRequired { get; } =
        Error.Validation(Prefix + "recipient_required", "A notification needs a recipient.");

    public static Error TypeInvalid { get; } =
        Error.Validation(
            Prefix + "type_invalid",
            $"A notification type is lower-case letters, digits and underscores in dot-separated segments, at most {Notification.TypeMaxLength} characters.");
}
