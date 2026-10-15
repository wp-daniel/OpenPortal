using System.ComponentModel.DataAnnotations;

namespace OpenPortal.Notifications.Application.Contracts;

/// <summary>An announcement's text in one language.</summary>
/// <remarks>Checked by the domain (language code, title, lengths), so a bad text is a 400 with its own code.</remarks>
public sealed record AnnouncementTextDto(string Language, string Title, string? Body);

/// <summary>An announcement as the management screen edits it.</summary>
/// <param name="Severity"><c>info</c>, <c>warning</c> or <c>maintenance</c>.</param>
/// <param name="Audience"><c>everyone</c> (the sign-in page too) or <c>signed_in</c>.</param>
/// <param name="Status"><c>scheduled</c>, <c>active</c> or <c>ended</c>, at the time of the request.</param>
public sealed record AnnouncementDto(
    Guid Id,
    string Severity,
    string Audience,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    bool Dismissible,
    IReadOnlyList<AnnouncementTextDto> Texts,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>An announcement showing now, in the reader's language.</summary>
/// <param name="Version">Changes whenever the announcement is edited, so a reader who hid it sees the new text.</param>
public sealed record ActiveAnnouncementDto(
    Guid Id,
    string Severity,
    string Title,
    string? Body,
    bool Dismissible,
    DateTimeOffset? EndsAtUtc,
    DateTimeOffset Version);

/// <summary>Creates or replaces an announcement.</summary>
public sealed class SaveAnnouncementRequest
{
    [Required(ErrorMessage = "validation.required")]
    public string Severity { get; init; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    public string Audience { get; init; } = string.Empty;

    public DateTimeOffset? StartsAtUtc { get; init; }

    public DateTimeOffset? EndsAtUtc { get; init; }

    public bool Dismissible { get; init; }

    [Required(ErrorMessage = "validation.required")]
    public IReadOnlyList<AnnouncementTextDto> Texts { get; init; } = [];
}

/// <summary>The wire names of the announcement enums.</summary>
public static class AnnouncementNames
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Maintenance = "maintenance";
    public const string Everyone = "everyone";
    public const string SignedIn = "signed_in";
    public const string Scheduled = "scheduled";
    public const string Active = "active";
    public const string Ended = "ended";
}
