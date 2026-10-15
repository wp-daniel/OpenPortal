namespace OpenPortal.Identity.Application.Auditing;

/// <summary>
/// The audit actions the Identity module records. Each needs an <c>audit.action.&lt;code&gt;</c> label in the
/// resource files (a test checks it).
/// </summary>
public static class IdentityAuditActions
{
    /// <summary>A portal sign-in with a password. A failure carries a <c>reason</c>.</summary>
    public const string SignIn = "auth.sign_in";

    public const string SignOut = "auth.sign_out";

    public const string ProfileUpdated = "account.profile_updated";

    /// <summary>The caller changed their own password. A failure is usually a wrong current password.</summary>
    public const string PasswordChanged = "account.password_changed";

    public const string OwnAvatarChanged = "account.avatar_changed";

    public const string OwnAvatarRemoved = "account.avatar_removed";

    public const string TwoFactorEnabled = "account.two_factor_enabled";

    /// <summary>A failure is a wrong password.</summary>
    public const string TwoFactorDisabled = "account.two_factor_disabled";

    public const string RecoveryCodesGenerated = "account.recovery_codes_generated";

    public const string UserCreated = "user.created";

    public const string UserUpdated = "user.updated";

    public const string AdministratorGranted = "user.administrator_granted";

    public const string AdministratorRevoked = "user.administrator_revoked";

    public const string PasswordReset = "user.password_reset";

    public const string UserDeleted = "user.deleted";

    public const string AvatarChanged = "user.avatar_changed";

    public const string AvatarRemoved = "user.avatar_removed";

    /// <summary>An administrator turned two-factor authentication off for an account (a lost phone).</summary>
    public const string TwoFactorReset = "user.two_factor_reset";

    /// <summary>The portal's security settings changed. Details carry the new values.</summary>
    public const string SecuritySettingsUpdated = "settings.security_updated";

    /// <summary>The registration and invitation settings changed. Details carry the new values.</summary>
    public const string RegistrationSettingsUpdated = "settings.registration_updated";

    /// <summary>Someone created their own account. The actor is the new account; a failure is a refused address.</summary>
    public const string Registered = "user.registered";

    /// <summary>An administrator let a self-registered account sign in.</summary>
    public const string RegistrationApproved = "user.registration_approved";

    /// <summary>A self-registered account confirmed its address.</summary>
    public const string EmailConfirmed = "account.email_confirmed";

    /// <summary>An address was invited; the target is the invitation (labelled with the address).</summary>
    public const string Invited = "user.invited";

    public const string InvitationResent = "user.invitation_resent";

    public const string InvitationRevoked = "user.invitation_revoked";

    /// <summary>The invitation became an account; the actor is the new account.</summary>
    public const string InvitationAccepted = "user.invitation_accepted";

    /// <summary>A version of the privacy notice or the terms of use was put in force; the target is the version.</summary>
    public const string LegalDocumentPublished = "legal.document_published";

    /// <summary>The caller accepted the versions in force named in the details.</summary>
    public const string LegalAccepted = "account.legal_accepted";

    /// <summary>The caller downloaded a copy of their personal data.</summary>
    public const string DataExported = "account.data_exported";

    /// <summary>The caller asked for their account to be deleted; details carry the scheduled date.</summary>
    public const string DeletionRequested = "account.deletion_requested";

    public const string DeletionCancelled = "account.deletion_cancelled";

    /// <summary>The privacy settings (self-service deletion, grace period) changed.</summary>
    public const string PrivacySettingsUpdated = "settings.privacy_updated";

    /// <summary>An administrator started acting as an account for support; details carry the reason.</summary>
    public const string ImpersonationStarted = "user.impersonation_started";

    public const string ImpersonationEnded = "user.impersonation_ended";

    /// <summary>The user list was downloaded; details carry the filter and the row count.</summary>
    public const string UsersExported = "user.exported";

    /// <summary>A user file was imported; details carry how many rows were invited, updated, skipped or refused.</summary>
    public const string UsersImported = "user.import_completed";

    /// <summary>Every session and token of an account was ended by an administrator.</summary>
    public const string SessionsRevoked = "user.sessions_revoked";

    /// <summary>The caller created a personal access token; the target is the token.</summary>
    public const string TokenCreated = "account.token_created";

    /// <summary>The caller revoked one of their personal access tokens.</summary>
    public const string TokenRevoked = "account.token_revoked";

    /// <summary>An administrator revoked someone else's personal access token.</summary>
    public const string UserTokenRevoked = "user.token_revoked";
}

/// <summary>Values of the <c>method</c> detail on a sign-in that needed a second factor.</summary>
public static class SignInMethods
{
    public const string Authenticator = "authenticator";
    public const string RecoveryCode = "recovery_code";
}

/// <summary>Values of the <c>reason</c> detail on a failed sign-in.</summary>
public static class SignInFailureReasons
{
    public const string UnknownAccount = "unknown_account";
    public const string InvalidPassword = "invalid_password";
    public const string LockedOut = "locked_out";
    public const string NotAllowed = "not_allowed";
    public const string PendingApproval = "pending_approval";
    public const string EmailNotConfirmed = "email_not_confirmed";
    public const string InvalidCode = "invalid_code";
}
