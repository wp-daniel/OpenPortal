namespace OpenPortal.Identity.Application.Notifications;

/// <summary>
/// The notifications the Identity module sends. Each needs <c>notification.&lt;type&gt;.title</c> and <c>.body</c>
/// labels in the resource files (a test checks it); the parameters each one fills are listed beside it.
/// </summary>
public static class IdentityNotificationTypes
{
    /// <summary>To the user reviewers: someone registered and waits for approval (<c>user</c>, <c>email</c>).</summary>
    public const string RegistrationPending = "user.registration_pending";

    /// <summary>To the user reviewers: someone registered and can sign in already (<c>user</c>, <c>email</c>).</summary>
    public const string UserRegistered = "user.registered";

    /// <summary>To the inviter: the invitation was accepted (<c>user</c>, <c>email</c>).</summary>
    public const string InvitationAccepted = "user.invitation_accepted";

    /// <summary>To the user: an administrator set a new password on their account.</summary>
    public const string PasswordReset = "account.password_reset";

    /// <summary>To the user: an administrator turned their two-factor authentication off.</summary>
    public const string TwoFactorReset = "account.two_factor_reset";

    /// <summary>To the user: they became an administrator.</summary>
    public const string AdministratorGranted = "account.administrator_granted";

    /// <summary>To the user: their self-registered account was approved.</summary>
    public const string RegistrationApproved = "account.registration_approved";

    /// <summary>To the user reviewers: someone asked for their account to be deleted (<c>user</c>, <c>email</c>, <c>date</c>).</summary>
    public const string DeletionRequested = "user.deletion_requested";

    /// <summary>To the user: an administrator acted as them for support (<c>admin</c>, <c>reason</c>).</summary>
    public const string Impersonated = "account.impersonated";

    /// <summary>To the user: an external account was connected to theirs (<c>provider</c>, <c>account</c>).</summary>
    public const string ExternalLoginAdded = "account.external_login_added";

    /// <summary>To the user: their account was signed in to from a browser it had not used before (<c>device</c>, <c>ip</c>).</summary>
    public const string NewSignIn = "account.new_sign_in";
}

/// <summary>The kinds of work Identity notices are addressed to; the host maps each to the people who may do it.</summary>
public static class IdentityNotificationRoles
{
    /// <summary>Whoever manages accounts (approves registrations).</summary>
    public const string UserReviewers = "identity.user_reviewers";
}
