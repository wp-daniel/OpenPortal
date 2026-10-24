using System.ComponentModel.DataAnnotations;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Application.Contracts;

// ---------------------------------------------------------------------------
// Settings.
// ---------------------------------------------------------------------------

/// <summary>Values of <see cref="RegistrationSettingsDto.Mode"/>.</summary>
public static class RegistrationModes
{
    public const string Disabled = "disabled";
    public const string Open = "open";
    public const string Approval = "approval";
}

/// <summary>How accounts come to exist besides an administrator creating them.</summary>
/// <param name="Mode"><c>disabled</c>, <c>open</c> or <c>approval</c>.</param>
/// <param name="AllowedDomains">Email domains that may register; empty allows any.</param>
/// <param name="RequireEmailConfirmation">Self-registered accounts confirm their address before signing in.</param>
/// <param name="InvitationLifetimeDays">How long an invitation link stays valid.</param>
/// <param name="EmailDeliveryAvailable">Whether the portal can send email (confirmations, invitations).</param>
public sealed record RegistrationSettingsDto(
    string Mode,
    IReadOnlyList<string> AllowedDomains,
    bool RequireEmailConfirmation,
    int InvitationLifetimeDays,
    bool EmailDeliveryAvailable,
    DateTimeOffset? UpdatedAtUtc);

public sealed class UpdateRegistrationSettingsRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.registrationMode.required")]
    [StringLength(16, ErrorMessage = "validation.registrationMode.required")]
    public string Mode { get; init; } = RegistrationModes.Disabled;

    public IReadOnlyList<string>? AllowedDomains { get; init; }

    public bool RequireEmailConfirmation { get; init; } = true;

    [Range(1, 90, ErrorMessage = "validation.invitationLifetime.range")]
    public int InvitationLifetimeDays { get; init; } = 7;
}

/// <summary>What the sign-in page needs to know about registration. Public, like the password policy.</summary>
/// <param name="Enabled">Whether the "create an account" link is offered.</param>
/// <param name="RequiresApproval">Whether a new account waits for an administrator.</param>
/// <param name="PasswordReset">Whether the sign-in page offers "forgot your password?" (it needs email delivery).</param>
/// <param name="Passkeys">Whether the sign-in page offers "sign in with a passkey".</param>
public sealed record RegistrationInfoDto(bool Enabled, bool RequiresApproval, bool PasswordReset = false, bool Passkeys = false)
{
    public static RegistrationInfoDto Closed { get; } = new(false, false);
}

// ---------------------------------------------------------------------------
// Self-registration.
// ---------------------------------------------------------------------------

/// <summary>Someone creating their own account.</summary>
public sealed class RegisterRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.password.required")]
    [StringLength(256, ErrorMessage = "validation.password.max")]
    public string Password { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.firstName.required")]
    [StringLength(ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string FirstName { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.lastName.required")]
    [StringLength(ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// The versions of the privacy notice and the terms of use the person accepted (their ids, from
    /// <c>GET /api/legal/{kind}</c>). Every version in force that requires acceptance must be among them.
    /// </summary>
    public IReadOnlyList<Guid> AcceptedLegalDocumentIds { get; init; } = [];
}

/// <summary>Values of <see cref="RegistrationResultDto.Outcome"/>.</summary>
public static class RegistrationOutcomes
{
    /// <summary>The account is ready and the caller is signed in.</summary>
    public const string SignedIn = "signed_in";

    /// <summary>A confirmation link was sent to the address (also the answer for an address already registered).</summary>
    public const string ConfirmationSent = "confirmation_sent";

    /// <summary>The account waits for an administrator.</summary>
    public const string ApprovalPending = "approval_pending";

    /// <summary>The address is confirmed and the account can sign in.</summary>
    public const string Confirmed = "confirmed";
}

/// <param name="Outcome">One of <see cref="RegistrationOutcomes"/>.</param>
public sealed record RegistrationResultDto(string Outcome);

/// <summary>The link of a confirmation email.</summary>
public sealed class ConfirmEmailRequest
{
    [Required(ErrorMessage = "validation.token.required")]
    public Guid UserId { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.token.required")]
    [StringLength(2048, ErrorMessage = "validation.token.required")]
    public string Token { get; init; } = string.Empty;
}

public sealed class ResendConfirmationRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;
}

// ---------------------------------------------------------------------------
// Invitations.
// ---------------------------------------------------------------------------

/// <summary>Values of <see cref="InvitationDto.Status"/>.</summary>
public static class InvitationStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Revoked = "revoked";
    public const string Expired = "expired";
}

/// <summary>An invitation as administrators see it. The link itself is shown once, when it is issued.</summary>
/// <param name="Status"><c>pending</c>, <c>accepted</c>, <c>revoked</c> or <c>expired</c>.</param>
/// <param name="GroupIds">Groups the account joins on acceptance.</param>
/// <param name="OrganizationIds">Organizations the account joins on acceptance.</param>
public sealed record InvitationDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string? Message,
    string Status,
    Guid InvitedById,
    string? InvitedByName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset LastSentAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    Guid? AcceptedUserId,
    IReadOnlyList<Guid> GroupIds,
    IReadOnlyList<Guid> OrganizationIds);

/// <summary>An invitation just issued (or issued again), with its link.</summary>
/// <param name="Link">The address the invited person opens. Shown now only: the portal keeps no copy of the token.</param>
/// <param name="EmailSent">Whether the portal emailed the link; when not, the administrator passes it on.</param>
public sealed record InvitationIssuedDto(InvitationDto Invitation, string Link, bool EmailSent);

public sealed class CreateInvitationRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;

    [StringLength(ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string? FirstName { get; init; }

    [StringLength(ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string? LastName { get; init; }

    [StringLength(500, ErrorMessage = "validation.message.max")]
    public string? Message { get; init; }

    public IReadOnlyList<Guid>? GroupIds { get; init; }

    public IReadOnlyList<Guid>? OrganizationIds { get; init; }
}

/// <summary>Filter and paging for the invitation list.</summary>
public sealed class InvitationQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "validation.page.min")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "validation.pageSize.range")]
    public int PageSize { get; init; } = 25;

    /// <summary>One of <see cref="InvitationStatuses"/>; anything else lists them all.</summary>
    [StringLength(16, ErrorMessage = "validation.search.max")]
    public string? Status { get; init; }

    /// <summary>Matched against the email.</summary>
    [StringLength(256, ErrorMessage = "validation.search.max")]
    public string? Search { get; init; }
}

/// <summary>What the invited person sees before accepting. Read with the token, without signing in.</summary>
public sealed record InvitationPreviewDto(
    string Email,
    string? FirstName,
    string? LastName,
    string? InvitedByName,
    string? Message,
    DateTimeOffset ExpiresAtUtc);

public sealed class InvitationTokenRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.token.required")]
    [StringLength(256, ErrorMessage = "validation.token.required")]
    public string Token { get; init; } = string.Empty;
}

/// <summary>The invited person creates their account.</summary>
public sealed class AcceptInvitationRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.token.required")]
    [StringLength(256, ErrorMessage = "validation.token.required")]
    public string Token { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.password.required")]
    [StringLength(256, ErrorMessage = "validation.password.max")]
    public string Password { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.firstName.required")]
    [StringLength(ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string FirstName { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.lastName.required")]
    [StringLength(ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// The versions of the privacy notice and the terms of use the person accepted (their ids, from
    /// <c>GET /api/legal/{kind}</c>). Every version in force that requires acceptance must be among them.
    /// </summary>
    public IReadOnlyList<Guid> AcceptedLegalDocumentIds { get; init; } = [];
}

/// <summary>The account an invitation became, and what the host still has to apply.</summary>
public sealed record AcceptedInvitationDto(
    Guid UserId,
    string Email,
    Guid InvitedByUserId,
    IReadOnlyList<Guid> GroupIds,
    IReadOnlyList<Guid> OrganizationIds);

// ---------------------------------------------------------------------------
// Statistics.
// ---------------------------------------------------------------------------

/// <summary>Accounts counted, for the statistics page.</summary>
/// <param name="NewInPeriod">Accounts created since the start of the period.</param>
/// <param name="SignedInInPeriod">Accounts that signed in to the portal since the start of the period.</param>
/// <param name="NeverSignedIn">Accounts that never signed in.</param>
public sealed record UserStatisticsDto(
    int Total,
    int Administrators,
    int Locked,
    int PendingApproval,
    int TwoFactorEnabled,
    int NewInPeriod,
    int SignedInInPeriod,
    int NeverSignedIn,
    int PendingInvitations);

// ---------------------------------------------------------------------------
// Password recovery.
// ---------------------------------------------------------------------------

/// <summary>Someone who forgot their password asks for a reset link.</summary>
public sealed class ForgotPasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;
}

/// <summary>The reset link's values and the new password.</summary>
public sealed class CompletePasswordResetRequest
{
    [Required(ErrorMessage = "validation.token.required")]
    public Guid UserId { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.token.required")]
    [StringLength(2048, ErrorMessage = "validation.token.required")]
    public string Token { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.newPassword.required")]
    [StringLength(256, ErrorMessage = "validation.newPassword.max")]
    public string NewPassword { get; init; } = string.Empty;
}
