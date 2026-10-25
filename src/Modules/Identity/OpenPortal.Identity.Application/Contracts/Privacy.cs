using System.ComponentModel.DataAnnotations;
using OpenPortal.Identity.Domain.Settings;

namespace OpenPortal.Identity.Application.Contracts;

/// <summary>What account holders may do with their own data.</summary>
public sealed record PrivacySettingsDto(bool SelfDeletionEnabled, int DeletionGraceDays, DateTimeOffset? UpdatedAtUtc);

public sealed class UpdatePrivacySettingsRequest
{
    public bool SelfDeletionEnabled { get; init; }

    [Range(0, PrivacySettings.MaxGraceDays, ErrorMessage = "validation.graceDays.range")]
    public int DeletionGraceDays { get; init; }
}

/// <summary>The caller's password, asked again before an operation on the whole account.</summary>
public sealed class PasswordConfirmationRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.password.required")]
    [StringLength(256, ErrorMessage = "validation.password.max")]
    public string Password { get; init; } = string.Empty;
}

/// <summary>A deletion the account holder asked for, and when it happens.</summary>
public sealed record AccountDeletionDto(DateTimeOffset RequestedAtUtc, DateTimeOffset ScheduledAtUtc);

/// <summary>Optional features the portal offers, so the client shows only what works.</summary>
/// <param name="SelfDeletion">Account holders may ask for their account to be deleted.</param>
/// <param name="PersonalAccessTokens">Account holders may create personal access tokens for the API.</param>
/// <param name="IdleTimeoutMinutes">Minutes without activity after which a session ends (0: never), so the client can warn first.</param>
public sealed record SessionFeaturesDto(bool SelfDeletion, bool PersonalAccessTokens, int IdleTimeoutMinutes = 0)
{
    public static SessionFeaturesDto None { get; } = new(false, false);
}
