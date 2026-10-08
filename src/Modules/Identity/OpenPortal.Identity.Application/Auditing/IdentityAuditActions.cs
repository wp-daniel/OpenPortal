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

    public const string UserCreated = "user.created";

    public const string UserUpdated = "user.updated";

    public const string AdministratorGranted = "user.administrator_granted";

    public const string AdministratorRevoked = "user.administrator_revoked";

    public const string PasswordReset = "user.password_reset";

    public const string UserDeleted = "user.deleted";

    public const string AvatarChanged = "user.avatar_changed";

    public const string AvatarRemoved = "user.avatar_removed";
}

/// <summary>Values of the <c>reason</c> detail on a failed sign-in.</summary>
public static class SignInFailureReasons
{
    public const string UnknownAccount = "unknown_account";
    public const string InvalidPassword = "invalid_password";
    public const string LockedOut = "locked_out";
    public const string NotAllowed = "not_allowed";
    public const string TwoFactorRequired = "two_factor_required";
}
