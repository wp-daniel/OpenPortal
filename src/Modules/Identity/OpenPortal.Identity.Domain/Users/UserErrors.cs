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

    public static Error TwoFactorRequirementInvalid { get; } =
        Error.Validation(Prefix + "two_factor_requirement_invalid", "The second-factor requirement must be none, administrators or everyone.");

    public static Error TwoFactorRequirementUnsatisfiable { get; } =
        Error.Validation(Prefix + "two_factor_requirement_unsatisfiable", "Requiring a second factor needs authenticator apps or passkeys to be on.");

    public static Error TwoFactorRequirementSelf { get; } =
        Error.Conflict(Prefix + "two_factor_requirement_self", "Set up an authenticator app or a passkey on your own account before requiring one.");

    public static Error TwoFactorSetupRequired { get; } =
        Error.Forbidden(Prefix + "two_factor_setup_required", "This account must set up an authenticator app or a passkey before anything else.");

    public static Error TwoFactorLastFactor { get; } =
        Error.Conflict(Prefix + "two_factor_last_factor", "This is the account's last second factor, which the portal requires: add another one first.");

    public static Error PasskeyRequired { get; } =
        Error.Unauthorized(Prefix + "passkey_required", "This account signs in with a passkey: the password alone is not enough.");

    public static Error PasskeysDisabled { get; } =
        Error.Forbidden(Prefix + "passkeys_disabled", "Passkeys are turned off for this portal.");

    public static Error PasskeyInvalid { get; } =
        Error.Validation(Prefix + "passkey_invalid", "The passkey could not be verified.");

    public static Error PasskeyNotFound { get; } =
        Error.NotFound(Prefix + "passkey_not_found", "The passkey does not exist.");

    public static Error PasskeyNameInvalid { get; } =
        Error.Validation(Prefix + "passkey_name_invalid", "A passkey name is 1 to 64 characters.");

    public static Error PasskeySignInFailed { get; } =
        Error.Unauthorized(Prefix + "passkey_sign_in_failed", "This passkey does not belong to an account that can sign in.");

    public static Error PasswordResetInvalid { get; } =
        Error.Validation(Prefix + "password_reset_invalid", "The reset link is not valid, was used already or has expired.");

    public static Error PasswordRecoveryUnavailable { get; } =
        Error.Conflict(Prefix + "password_recovery_unavailable", "Password recovery needs email delivery, which is not configured.");

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

    public static Error IdleTimeoutInvalid { get; } =
        Error.Validation(Prefix + "idle_timeout_invalid", "The idle timeout is 0 (off) or between 5 and 1440 minutes.");

    public static Error SessionNotFound { get; } =
        Error.NotFound(Prefix + "session_not_found", "There is no such open session.");

    public static Error SessionIsCurrent { get; } =
        Error.Conflict(Prefix + "session_is_current", "This is the session you are using: sign out instead.");

    public static Error PasswordAlreadySet { get; } =
        Error.Conflict(Prefix + "password_already_set", "The account already has a password; change it with the current one.");

    public static Error ExternalProviderNotFound { get; } =
        Error.NotFound(Prefix + "external_provider_not_found", "There is no such sign-in provider.");

    public static Error ExternalProviderDisabled { get; } =
        Error.Forbidden(Prefix + "external_provider_disabled", "Signing in with this provider is turned off.");

    public static Error ExternalProviderIncomplete { get; } =
        Error.Validation(Prefix + "external_provider_incomplete", "A sign-in provider can only be turned on with its client id and client secret.");

    public static Error ExternalClientIdInvalid { get; } =
        Error.Validation(Prefix + "external_client_id_invalid", "The client id is at most 200 characters.");

    public static Error ExternalClientSecretInvalid { get; } =
        Error.Validation(Prefix + "external_client_secret_invalid", "The client secret is too long.");

    public static Error ExternalTenantInvalid { get; } =
        Error.Validation(
            Prefix + "external_tenant_invalid",
            "The tenant is organizations, common, consumers, a directory id or a directory's domain.");

    public static Error ExternalSignUpModeInvalid { get; } =
        Error.Validation(Prefix + "external_sign_up_invalid", "Account creation must be disabled, open or approval.");

    public static Error ExternalSignInFailed { get; } =
        Error.Unauthorized(Prefix + "external_sign_in_failed", "The sign-in with the external provider did not complete.");

    public static Error ExternalEmailMissing { get; } =
        Error.Validation(Prefix + "external_email_missing", "The external account did not share an email address.");

    public static Error ExternalEmailUnverified { get; } =
        Error.Conflict(
            Prefix + "external_email_unverified",
            "An account with this address exists, but the provider does not vouch for the address: sign in with your password and connect the account from your security settings.");

    public static Error ExternalEmailNotVerified { get; } =
        Error.Forbidden(Prefix + "external_email_not_verified", "The provider does not vouch for this email address, so no account can be created with it.");

    public static Error ExternalSignUpClosed { get; } =
        Error.Forbidden(Prefix + "external_sign_up_closed", "There is no account for this external account, and this provider does not create new ones.");

    public static Error ExternalDomainNotAllowed { get; } =
        Error.Forbidden(Prefix + "external_domain_not_allowed", "Accounts with this email domain cannot be created through this provider.");

    public static Error ExternalLoginInUse { get; } =
        Error.Conflict(Prefix + "external_login_in_use", "This external account is already connected to another portal account.");

    public static Error ExternalLoginNotFound { get; } =
        Error.NotFound(Prefix + "external_login_not_found", "This account is not connected to that provider.");

    public static Error ExternalLoginLastMethod { get; } =
        Error.Conflict(
            Prefix + "external_login_last_method",
            "This is the only way the account can sign in: set a password or add a passkey before disconnecting it.");
}