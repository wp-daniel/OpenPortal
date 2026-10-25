using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// People creating their own account, and confirming its address. Anonymous by nature: the portal's settings decide
/// whether it is allowed at all (<see cref="IRegistrationSettingsService"/>).
/// </summary>
public interface IRegistrationService
{
    /// <summary>Whether registration is offered, for the sign-in page.</summary>
    Task<RegistrationInfoDto> GetInfoAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the account as the settings say: signed in at once, waiting for its address to be confirmed, or waiting
    /// for an administrator. When confirmation is required, an address already registered gets the same answer as a
    /// new one, so the endpoint cannot be used to find out who has an account.
    /// </summary>
    Task<Result<RegistrationResultDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>Confirms the address of a self-registered account from the emailed link.</summary>
    Task<Result<RegistrationResultDto>> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken);

    /// <summary>Sends the confirmation link again. Always succeeds, whatever the address, for the same reason.</summary>
    Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken);
}

/// <summary>Registration and invitation settings. Administrators only: who may get an account is never delegated.</summary>
public interface IRegistrationSettingsService
{
    Task<Result<RegistrationSettingsDto>> GetAsync(CancellationToken cancellationToken);

    Task<Result<RegistrationSettingsDto>> UpdateAsync(UpdateRegistrationSettingsRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Invitations by email. The administration side re-checks the caller (<see cref="UserAdministrationOperation.ManageUsers"/>);
/// <see cref="PreviewAsync"/> and <see cref="AcceptAsync"/> are anonymous and authenticated by the token itself.
/// </summary>
public interface IInvitationService
{
    Task<Result<PagedResult<InvitationDto>>> ListAsync(InvitationQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Invites an address that has no account and no waiting invitation. The memberships it promises are the host's
    /// to validate first. Emails the link when the portal can.
    /// </summary>
    Task<Result<InvitationIssuedDto>> CreateAsync(CreateInvitationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Invites an address into one organization, for that organization's own administrators (no users page needed). The
    /// invitation promises a membership of that organization only.
    /// </summary>
    Task<Result<InvitationIssuedDto>> CreateForOrganizationAsync(
        Guid organizationId,
        OrganizationInvitationRequest request,
        CancellationToken cancellationToken);

    /// <summary>Issues a new link (the previous one stops working) with a fresh validity period, and emails it.</summary>
    Task<Result<InvitationIssuedDto>> ResendAsync(Guid invitationId, CancellationToken cancellationToken);

    Task<Result> RevokeAsync(Guid invitationId, CancellationToken cancellationToken);

    /// <summary>What the invitation behind <paramref name="token"/> says, while it can still be accepted.</summary>
    Task<Result<InvitationPreviewDto>> PreviewAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the account (its address confirmed by the link), signs the caller in as it and closes the invitation.
    /// The answer lists what the host still has to apply (memberships) and whom to tell.
    /// </summary>
    Task<Result<AcceptedInvitationDto>> AcceptAsync(AcceptInvitationRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Whether the caller may invite people into an organization: the host answers from the Access module (its portal page,
/// or being one of the organization's administrators), which Identity does not reference.
/// </summary>
public interface IOrganizationInvitationAuthorization
{
    Task<Result> EnsureCanInviteToAsync(Guid organizationId, CancellationToken cancellationToken);
}

/// <summary>Accounts counted, for the statistics page. Re-checks the caller (<see cref="UserAdministrationOperation.ViewStatistics"/>).</summary>
public interface IUserStatisticsService
{
    /// <param name="periodStart">Start of the period the "new" and "signed in" counts cover.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<Result<UserStatisticsDto>> GetAsync(DateTimeOffset periodStart, CancellationToken cancellationToken);
}

/// <summary>The kinds of email the Identity module asks the host to send.</summary>
public static class AccountMessageKinds
{
    /// <summary>Values: <c>inviter</c>, <c>message</c>, <c>expires</c> (an ISO date).</summary>
    public const string Invitation = "invitation";

    /// <summary>Values: <c>name</c>.</summary>
    public const string EmailConfirmation = "email_confirmation";

    /// <summary>Values: <c>name</c>.</summary>
    public const string RegistrationApproved = "registration_approved";

    /// <summary>Values: <c>name</c>, <c>expires</c> (the scheduled deletion, an ISO date).</summary>
    public const string DeletionScheduled = "deletion_scheduled";

    /// <summary>Values: <c>name</c>, <c>minutes</c> (how long the link works).</summary>
    public const string PasswordReset = "password_reset";

    /// <summary>Sent after the password changed, by its owner or through a reset link. Values: <c>name</c>.</summary>
    public const string PasswordChanged = "password_changed";
}

/// <summary>One email about an account.</summary>
/// <param name="Kind">One of <see cref="AccountMessageKinds"/>.</param>
/// <param name="Language">The recipient's language when known; the host falls back to the request's.</param>
/// <param name="LinkPath">A local path of the portal the email leads to (with its query string).</param>
/// <param name="Values">What the text mentions.</param>
public sealed record AccountMessage(
    string Kind,
    string ToEmail,
    string? ToName,
    string? Language,
    string LinkPath,
    IReadOnlyDictionary<string, string?> Values);

/// <summary>
/// Email for accounts, sent by the host, which owns the delivery configuration, the portal's public address and the
/// texts. The module decides what to send and when.
/// </summary>
public interface IAccountMessaging
{
    /// <summary>Whether email can be sent at all (an SMTP server or pickup folder is configured).</summary>
    Task<bool> CanSendEmailAsync(CancellationToken cancellationToken);

    /// <summary>The absolute address of a portal page, for a link shown to an administrator.</summary>
    string LinkTo(string pathAndQuery);

    /// <summary>
    /// Queues the message for delivery (retried until the mail server takes it); false when email is off. Never
    /// throws for delivery.
    /// </summary>
    Task<bool> SendAsync(AccountMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// "Forgot your password?": a link by email that lets the owner of the address choose a new password. Anonymous by
/// nature; asking never reveals whether an account exists.
/// </summary>
public interface IPasswordRecoveryService
{
    /// <summary>Whether recovery is offered at all: it needs email delivery.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Emails a reset link when the address belongs to an account that may sign in. Completes the same way whatever
    /// the address, so it reveals nothing.
    /// </summary>
    Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the new password when the link is valid (single use, short-lived), signs the account out everywhere and
    /// tells its owner by email.
    /// </summary>
    Task<Result> CompleteAsync(CompletePasswordResetRequest request, CancellationToken cancellationToken);
}
