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

    /// <summary>A new webhook signing secret was generated for the application.</summary>
    public const string ApplicationWebhookSecretGenerated = "application.webhook_secret_generated";

    public const string ApplicationManifestApplied = "application.manifest_applied";

    public const string ApplicationDisabled = "application.disabled";

    public const string ApplicationEnabled = "application.enabled";

    public const string ApplicationDeleted = "application.deleted";

    public const string GroupCreated = "group.created";

    public const string GroupUpdated = "group.updated";

    public const string GroupDeleted = "group.deleted";

    public const string GroupMemberAdded = "group.member_added";

    public const string GroupMemberRemoved = "group.member_removed";

    /// <summary>A group was put inside another (target: the parent; details: the subgroup).</summary>
    public const string SubgroupAdded = "group.subgroup_added";

    public const string SubgroupRemoved = "group.subgroup_removed";

    /// <summary>An access review was started on an application (details: name, entries).</summary>
    public const string ReviewStarted = "access.review_started";

    /// <summary>An access review was completed (details: revoked count).</summary>
    public const string ReviewCompleted = "access.review_completed";

    public const string ReviewCancelled = "access.review_cancelled";


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

    public const string OrganizationCreated = "organization.created";

    public const string OrganizationUpdated = "organization.updated";

    public const string OrganizationDeleted = "organization.deleted";

    /// <summary>The target is the member; the details name the organization and the role.</summary>
    public const string OrganizationMemberAdded = "organization.member_added";

    public const string OrganizationMemberRoleChanged = "organization.member_role_changed";

    public const string OrganizationMemberRemoved = "organization.member_removed";

    /// <summary>A user asked for an application. The target is the application.</summary>
    public const string RequestSubmitted = "access.request_submitted";

    /// <summary>A reviewer approved a request (and so granted the application). The target is the requester.</summary>
    public const string RequestApproved = "access.request_approved";

    /// <summary>A reviewer rejected a request. The target is the requester.</summary>
    public const string RequestRejected = "access.request_rejected";

    /// <summary>The requester withdrew their request. The target is the application.</summary>
    public const string RequestCancelled = "access.request_cancelled";
}
