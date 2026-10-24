using System.ComponentModel.DataAnnotations;

namespace OpenPortal.Notifications.Application.Contracts;

/// <summary>
/// The email settings as administrators see them. The password is never returned, only whether one is stored.
/// </summary>
/// <param name="Source">
/// <c>portal</c> when these settings are in use, <c>configuration</c> when the portal has no SMTP server of its own
/// and sends through the configuration file's, <c>none</c> when email is off.
/// </param>
public sealed record MailSettingsDto(
    string? Host,
    int Port,
    string Security,
    string? Username,
    bool HasPassword,
    string? FromAddress,
    string? FromName,
    string Source,
    DateTimeOffset? UpdatedAtUtc);

public class MailServerRequest
{
    /// <summary>The SMTP server; empty to fall back to the configuration file.</summary>
    [StringLength(255, ErrorMessage = "validation.too_long")]
    public string? Host { get; init; }

    [Range(1, 65535, ErrorMessage = "validation.port")]
    public int Port { get; init; } = 587;

    /// <summary><c>auto</c>, <c>starttls</c>, <c>ssl</c> or <c>none</c>.</summary>
    public string Security { get; init; } = "auto";

    [StringLength(255, ErrorMessage = "validation.too_long")]
    public string? Username { get; init; }

    /// <summary>A new password; empty keeps the stored one (see <see cref="ClearPassword"/>).</summary>
    [StringLength(1024, ErrorMessage = "validation.too_long")]
    public string? Password { get; init; }

    public bool ClearPassword { get; init; }

    [StringLength(254, ErrorMessage = "validation.too_long")]
    public string? FromAddress { get; init; }

    [StringLength(128, ErrorMessage = "validation.too_long")]
    public string? FromName { get; init; }
}

public sealed class UpdateMailSettingsRequest : MailServerRequest
{
}

/// <summary>
/// Sends one email with the given settings, saved or not, so an administrator can check them before saving. An
/// empty password means the stored one.
/// </summary>
public sealed class TestMailSettingsRequest : MailServerRequest
{
    [Required(ErrorMessage = "validation.required")]
    [StringLength(254, ErrorMessage = "validation.too_long")]
    public string To { get; init; } = string.Empty;
}

/// <summary>The outcome of a test email: the server's own message when it failed.</summary>
public sealed record MailTestResultDto(bool Delivered, string? Error);

/// <summary>What the host's mail transport needs, with the password in clear. Never leaves the server.</summary>
public sealed record MailDeliverySettings(
    string Host,
    int Port,
    string Security,
    string? Username,
    string? Password,
    string FromAddress,
    string? FromName);

public sealed record OutboxMessageDto(
    Guid Id,
    string Type,
    string? Description,
    string Status,
    int Attempts,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    string? LastError);

public sealed record OutboxSummaryDto(int Pending, int Failed, int DeliveredLastDay, DateTimeOffset? OldestPendingAtUtc);

public sealed record OutboxPageDto(OutboxSummaryDto Summary, IReadOnlyList<OutboxMessageDto> Items, int Total);

public sealed class OutboxQuery
{
    /// <summary><c>pending</c>, <c>failed</c>, <c>delivered</c> or empty for every status.</summary>
    public string? Status { get; init; }

    public string? Type { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}
