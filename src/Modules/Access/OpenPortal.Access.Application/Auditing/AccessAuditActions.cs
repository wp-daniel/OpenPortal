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

    /// <summary>The user took back an application's permission to sign them in; the target is the application.</summary>
    public const string AuthorizationRevoked = "access.authorization_revoked";

    /// <summary>The organization's sign-in policy changed (two factors required or not).</summary>
    public const string OrganizationPolicyChanged = "organization.policy_changed";

    /// <summary>An email domain was claimed for the organization; details carry the domain.</summary>
    public const string OrganizationDomainAdded = "organization.domain_added";

    /// <summary>A domain was verified, by its DNS record or by a portal administrator (details: <c>by</c>).</summary>
    public const string OrganizationDomainVerified = "organization.domain_verified";

    /// <summary>Joining by the domain was turned on or off.</summary>
    public const string OrganizationDomainUpdated = "organization.domain_updated";

    public const string OrganizationDomainRemoved = "organization.domain_removed";

    /// <summary>Someone joined the organization by the domain of their address; the actor is the account.</summary>
    public const string OrganizationJoinedByDomain = "organization.joined_by_domain";

    /// <summary>A member's roles in an application within the organization changed; details carry them.</summary>
    public const string OrganizationMemberRolesChanged = "organization.member_roles_changed";

    /// <summary>A user asked for an application. The target is the application.</summary>
    public const string RequestSubmitted = "access.request_submitted";

    /// <summary>A reviewer approved a request (and so granted the application). The target is the requester.</summary>
    public const string RequestApproved = "access.request_approved";

    /// <summary>A reviewer rejected a request. The target is the requester.</summary>
    public const string RequestRejected = "access.request_rejected";

    /// <summary>The requester withdrew their request. The target is the application.</summary>
    public const string RequestCancelled = "access.request_cancelled";
}
