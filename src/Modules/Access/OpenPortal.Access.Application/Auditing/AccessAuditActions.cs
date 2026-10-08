namespace OpenPortal.Access.Application.Auditing;

/// <summary>
/// The audit actions the Access module records. Each needs an <c>audit.action.&lt;code&gt;</c> label in the
/// resource files (a test checks it).
/// </summary>
public static class AccessAuditActions
{
    public const string ApplicationCreated = "application.created";

    /// <summary>An application announced itself for the first time and waits for approval.</summary>
    public const string ApplicationAnnounced = "application.announced";

    public const string ApplicationUpdated = "application.updated";

    public const string ApplicationRolesChanged = "application.roles_changed";

    public const string ApplicationApproved = "application.approved";

    public const string ApplicationSecretRegenerated = "application.secret_regenerated";

    public const string ApplicationManifestApplied = "application.manifest_applied";

    public const string ApplicationDisabled = "application.disabled";

    public const string ApplicationEnabled = "application.enabled";

    public const string ApplicationDeleted = "application.deleted";

    public const string GroupCreated = "group.created";

    public const string GroupUpdated = "group.updated";

    public const string GroupDeleted = "group.deleted";

    public const string GroupMemberAdded = "group.member_added";

    public const string GroupMemberRemoved = "group.member_removed";

    public const string UserGranted = "access.user_granted";

    public const string UserRevoked = "access.user_revoked";

    public const string UserRolesChanged = "access.user_roles_changed";

    public const string GroupGranted = "access.group_granted";

    public const string GroupRevoked = "access.group_revoked";

    public const string GroupRolesChanged = "access.group_roles_changed";

    public const string PageGranted = "pages.granted";

    public const string PageRevoked = "pages.revoked";

    /// <summary>A group's pages were replaced as a whole; the details list what was added and removed.</summary>
    public const string GroupPagesChanged = "pages.group_pages_changed";
}
