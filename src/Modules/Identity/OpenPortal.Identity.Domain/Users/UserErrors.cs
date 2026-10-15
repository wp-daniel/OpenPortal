using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Users;

/// <summary>
/// Stable, machine-readable failure codes for the Identity module. Clients branch on
/// <see cref="Error.Code"/>; <see cref="Error.Description"/> is for logs only.
/// </summary>
public static class UserErrors
{
    private const string Prefix = "identity.";

    public static Error InvalidCredentials { get; } =
        Error.Unauthorized(Prefix + "invalid_credentials", "The supplied email or password is not valid.");

    public static Error AccountLockedOut { get; } =
        Error.Unauthorized(Prefix + "account_locked_out", "The account is temporarily locked after too many failed attempts.");

    public static Error AccountNotAllowed { get; } =
        Error.Unauthorized(Prefix + "account_not_allowed", "The account is not allowed to sign in.");

    public static Error AccountPendingApproval { get; } =
        Error.Unauthorized(Prefix + "account_pending_approval", "The account is waiting for an administrator's approval.");

    public static Error EmailNotConfirmed { get; } =
        Error.Unauthorized(Prefix + "email_not_confirmed", "Confirm your email address with the link we sent before signing in.");

    public static Error RegistrationClosed { get; } =
        Error.Forbidden(Prefix + "registration_closed", "This portal does not accept new registrations.");

    public static Error RegistrationDomainNotAllowed { get; } =
        Error.Validation(Prefix + "registration_domain_not_allowed", "Accounts with this email domain cannot register here.");

    public static Error RegistrationNotPending { get; } =
        Error.Conflict(Prefix + "registration_not_pending", "The account is not waiting for approval.");

    public static Error RegistrationModeInvalid { get; } =
        Error.Validation(Prefix + "registration_mode_invalid", "The registration setting must be disabled, open or approval.");

    public static Error RegistrationDomainInvalid { get; } =
        Error.Validation(
            Prefix + "registration_domain_invalid",
            $"Each allowed domain must be a host name such as example.com, at most {Settings.RegistrationSettings.MaxDomains} of them.");

    public static Error EmailDeliveryUnavailable { get; } =
        Error.Conflict(
            Prefix + "email_delivery_unavailable",
            "Email confirmation needs email delivery, which is not configured (Email:Smtp:Host).");

    public static Error EmailConfirmationInvalid { get; } =
        Error.Validation(Prefix + "email_confirmation_invalid", "The confirmation link is not valid or has expired.");

    public static Error InvitationLifetimeInvalid { get; } =
        Error.Validation(
            Prefix + "invitation_lifetime_invalid",
            $"Invitations must stay valid between {Settings.RegistrationSettings.MinInvitationDays} and {Settings.RegistrationSettings.MaxInvitationDays} days.");

    public static Error InvitationNotFound { get; } =
        Error.NotFound(Prefix + "invitation_not_found", "The invitation does not exist.");

    public static Error InvitationInvalid { get; } =
        Error.NotFound(Prefix + "invitation_invalid", "This invitation is not valid: it was used, withdrawn or never existed.");

    public static Error InvitationExpired { get; } =
        Error.Conflict(Prefix + "invitation_expired", "This invitation has expired. Ask for a new one.");

    public static Error InvitationClosed { get; } =
        Error.Conflict(Prefix + "invitation_closed", "The invitation was already accepted or withdrawn.");

    public static Error InvitationAlreadyPending { get; } =
        Error.Conflict(Prefix + "invitation_already_pending", "An invitation to this address is already waiting; send it again instead.");

    public static Error InvitationEmailInvalid { get; } =
        Error.Validation(Prefix + "invitation_email_invalid", "The email address is not valid.");

    public static Error InvitationMessageTooLong { get; } =
        Error.Validation(
            Prefix + "invitation_message_too_long",
            $"The message must not exceed {Invitations.UserInvitation.MessageMaxLength} characters.");

    public static Error InvitationTooManyMemberships { get; } =
        Error.Validation(
            Prefix + "invitation_too_many_memberships",
            $"An invitation may name at most {Invitations.UserInvitation.MaxMemberships} groups and {Invitations.UserInvitation.MaxMemberships} organizations.");

    public static Error TwoFactorRequired { get; } =
        Error.Unauthorized(Prefix + "two_factor_required", "The account requires an additional verification step.");

    public static Error TwoFactorCodeInvalid { get; } =
        Error.Unauthorized(Prefix + "two_factor_code_invalid", "The verification code is not valid.");

    public static Error TwoFactorSessionExpired { get; } =
        Error.Unauthorized(Prefix + "two_factor_session_expired", "The sign-in attempt expired. Enter your email and password again.");

    public static Error TwoFactorSetupCodeInvalid { get; } =
        Error.Validation(Prefix + "two_factor_setup_code_invalid", "The code does not match the authenticator app. Check the time on the phone and try again.");

    public static Error TwoFactorUnavailable { get; } =
        Error.Conflict(Prefix + "two_factor_unavailable", "Two-factor authentication is turned off for this portal.");

    public static Error TwoFactorAlreadyEnabled { get; } =
        Error.Conflict(Prefix + "two_factor_already_enabled", "Two-factor authentication is already on for this account.");

    public static Error TwoFactorNotEnabled { get; } =
        Error.Conflict(Prefix + "two_factor_not_enabled", "Two-factor authentication is not on for this account.");

    public static Error TwoFactorIssuerInvalid { get; } =
        Error.Validation(Prefix + "two_factor_issuer_invalid", "The name shown in authenticator apps must be 1 to 64 characters and contain no colon.");

    public static Error NotAuthenticated { get; } =
        Error.Unauthorized(Prefix + "not_authenticated", "No authenticated session was found.");

    public static Error Forbidden { get; } =
        Error.Forbidden(Prefix + "forbidden", "The caller is not allowed to perform this operation.");

    public static Error UserNotFound { get; } =
        Error.NotFound(Prefix + "user_not_found", "The requested user does not exist.");

    public static Error DuplicateEmail { get; } =
        Error.Conflict(Prefix + "duplicate_email", "An account with this email address already exists.");

    public static Error UnsupportedLanguage { get; } =
        Error.Validation(Prefix + "unsupported_language", "The requested language is not supported.");

    public static Error AdministratorRequired { get; } =
        Error.Forbidden(
            Prefix + "administrator_required",
            "Only an administrator can grant the administrator role or change an administrator's account.");

    public static Error CannotDeleteSelf { get; } =
        Error.Conflict(Prefix + "cannot_delete_self", "You cannot delete your own account.");

    public static Error CannotModifyOwnRoles { get; } =
        Error.Conflict(Prefix + "cannot_modify_own_roles", "An administrator cannot change their own roles.");

    public static Error PasswordComplexity { get; } =
        Error.Validation(Prefix + "password_complexity", "The password does not satisfy the configured complexity policy.");

    public static Error CurrentPasswordIncorrect { get; } =
        Error.Validation(Prefix + "current_password_incorrect", "The current password is not correct.");

    public static Error DisplayNameTooLong { get; } =
        Error.Validation(Prefix + "display_name_too_long", $"The full name (first and last name) must not exceed {ApplicationUser.DisplayNameMaxLength} characters.");

    public static Error FirstNameRequired { get; } =
        Error.Validation(Prefix + "first_name_required", "A first name is required.");

    public static Error LastNameRequired { get; } =
        Error.Validation(Prefix + "last_name_required", "A last name is required.");

    public static Error NameTooLong { get; } =
        Error.Validation(Prefix + "name_too_long", $"First and last names must not exceed {ApplicationUser.NameMaxLength} characters.");

    public static Error InvalidPhoneNumber { get; } =
        Error.Validation(Prefix + "invalid_phone_number", "The phone number is not valid.");

    public static Error DetailTooLong { get; } =
        Error.Validation(Prefix + "detail_too_long", $"Work and address fields must not exceed {ApplicationUser.DetailMaxLength} characters.");

    public static Error AvatarRequired { get; } =
        Error.Validation(Prefix + "avatar_required", "An image file is required.");

    public static Error AvatarTooLarge { get; } =
        Error.Validation(Prefix + "avatar_too_large", $"The image must not exceed {UserAvatar.MaxBytes / 1024} KB.");

    public static Error AvatarUnsupportedType { get; } =
        Error.Validation(Prefix + "avatar_unsupported_type", "The image must be a PNG, JPEG or WebP file.");

    public static Error AvatarNotFound { get; } =
        Error.NotFound(Prefix + "avatar_not_found", "The user has no profile picture.");

    public static Error SaveFailed { get; } =
        Error.Failure(Prefix + "save_failed", "The account could not be saved.");

    public static Error RoleAssignmentFailed { get; } =
        Error.Failure(
            Prefix + "role_assignment_failed",
            "The account was created but its roles could not be assigned, so it has been removed.");

    public static Error RoleUpdateFailed { get; } =
        Error.Failure(Prefix + "role_update_failed", "The account's roles could not be updated.");

    public static Error DeleteFailed { get; } =
        Error.Failure(Prefix + "delete_failed", "The account could not be deleted.");

    public static Error PasswordIncorrect { get; } =
        Error.Validation(Prefix + "password_incorrect", "The password is not correct.");

    public static Error SelfDeletionDisabled { get; } =
        Error.Forbidden(Prefix + "self_deletion_disabled", "Accounts cannot be deleted by their owners on this portal; ask an administrator.");

    public static Error LastAdministrator { get; } =
        Error.Conflict(Prefix + "last_administrator", "The only administrator cannot delete their account; make someone else an administrator first.");

    public static Error DeletionNotScheduled { get; } =
        Error.Conflict(Prefix + "deletion_not_scheduled", "No deletion is scheduled for this account.");

    public static Error GraceDaysInvalid { get; } =
        Error.Validation(Prefix + "grace_days_invalid", "The grace period is between 0 and 365 days.");

    public static Error ImpersonationRefused { get; } =
        Error.Forbidden(Prefix + "impersonation_refused", "This account cannot be impersonated: it is yours, an administrator, or not active.");

    public static Error NotImpersonating { get; } =
        Error.Conflict(Prefix + "not_impersonating", "This session is not impersonating anyone.");

    public static Error ReasonRequired { get; } =
        Error.Validation(Prefix + "reason_required", "Say why you need to act as this account (at most 200 characters).");
}