using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Access.Application.Abstractions;

/// <summary>
/// The applications that sign in through the portal. Every method re-checks the caller through
/// <see cref="IAccessAdminAuthorization"/>.
/// </summary>
public interface IApplicationRegistryService
{
    /// <summary>Lists every application, waiting ones first.</summary>
    Task<Result<IReadOnlyList<ApplicationDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<ApplicationDto>> GetAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Registers an application by hand. It is active at once and receives a client secret.</summary>
    Task<Result<ApplicationSecretDto>> CreateAsync(CreateApplicationRequest request, CancellationToken cancellationToken);

    Task<Result<ApplicationDto>> UpdateAsync(
        Guid applicationId,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken);

    /// <summary>Approves an announced application and issues its client secret.</summary>
    Task<Result<ApplicationSecretDto>> ApproveAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Replaces the client secret. The old one stops working immediately.</summary>
    Task<Result<ApplicationSecretDto>> RegenerateSecretAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Generates the secret the application's webhook deliveries are signed with, and returns it once.</summary>
    Task<Result<WebhookSecretDto>> GenerateWebhookSecretAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Adopts the redirect URIs the application last proposed.</summary>
    Task<Result<ApplicationDto>> ApplyManifestAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Stops all sign-ins to the application and revokes its tokens. Grants are kept.</summary>
    Task<Result<ApplicationDto>> DisableAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<Result<ApplicationDto>> EnableAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Removes the application, its grants and its tokens.</summary>
    Task<Result> DeleteAsync(Guid applicationId, CancellationToken cancellationToken);
}

/// <summary>Groups of users. Every method re-checks the caller through <see cref="IAccessAdminAuthorization"/>.</summary>
public interface IGroupService
{
    Task<Result<IReadOnlyList<GroupSummaryDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<GroupDetailDto>> GetAsync(Guid groupId, CancellationToken cancellationToken);

    Task<Result<GroupDetailDto>> CreateAsync(SaveGroupRequest request, CancellationToken cancellationToken);

    Task<Result<GroupDetailDto>> UpdateAsync(Guid groupId, SaveGroupRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes the group and its grants. Its members keep any access they hold another way.</summary>
    Task<Result> DeleteAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>Adds a member. Adding an existing member succeeds and changes nothing.</summary>
    Task<Result<GroupDetailDto>> AddMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken, DateTimeOffset? expiresAtUtc = null);

    /// <summary>Puts <paramref name="childGroupId"/> inside the group: its members get what the group is given.</summary>
    Task<Result<GroupDetailDto>> AddSubgroupAsync(Guid groupId, Guid childGroupId, CancellationToken cancellationToken);

    Task<Result<GroupDetailDto>> RemoveSubgroupAsync(Guid groupId, Guid childGroupId, CancellationToken cancellationToken);

    Task<Result<GroupDetailDto>> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Who may open which application. Grants are idempotent: granting twice or revoking something that was
/// never granted succeeds and changes nothing.
/// </summary>
public interface IAccessAdministrationService
{
    /// <summary>Every application with its groups (and their members) and its directly granted users.</summary>
    Task<Result<AccessTreeDto>> GetTreeAsync(CancellationToken cancellationToken);

    /// <summary>The applications one user can open, each with the reasons it is allowed.</summary>
    Task<Result<UserAccessDto>> GetUserAccessAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Grants the application to the user. With <paramref name="roles"/>, the grant carries exactly those roles
    /// (granting again changes only the roles); without, an existing grant keeps its roles.
    /// </summary>
    Task<Result> GrantUserAsync(
        Guid applicationId,
        Guid userId,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken,
        ExpiryChange? expiry = null);

    Task<Result> RevokeUserAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Grants the application to every member of the group; <paramref name="roles"/> as for users.</summary>
    Task<Result> GrantGroupAsync(
        Guid applicationId,
        Guid groupId,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken,
        ExpiryChange? expiry = null);

    Task<Result> RevokeGroupAsync(Guid applicationId, Guid groupId, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets a user whose account was deleted: their direct grants and group memberships go, and their
    /// tokens are revoked. Removing a user that holds nothing succeeds.
    /// </summary>
    Task<Result> RemoveUserAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Answers access questions about a given user. Used by the sign-in endpoints and the launchpad, which
/// identify the user themselves, so it performs no caller check of its own.
/// </summary>
public interface IAccessEvaluator
{
    /// <summary>Whether <paramref name="userId"/> may sign in to the client <paramref name="clientId"/> now.</summary>
    Task<AccessDecision> EvaluateAsync(Guid userId, string clientId, CancellationToken cancellationToken);

    /// <summary>The active applications the user can open, by name.</summary>
    Task<IReadOnlyList<LaunchpadItemDto>> GetLaunchpadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Which organization the tokens of <paramref name="clientId"/> name for the user. <paramref name="requested"/>
    /// is the organization the sign-in asked for (slug or id) or the one an earlier token named; without one, a
    /// user with a single organization gets it and one with several must choose, unless
    /// <paramref name="interactive"/> is false (a refresh), where no choice can be made.
    /// </summary>
    Task<OrganizationDecision> EvaluateOrganizationAsync(
        Guid userId,
        string clientId,
        string? requested,
        bool interactive,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the client <paramref name="clientId"/> may have a token of its own (client credentials): it must
    /// be active, and the answer lists the application API scopes enabled for it (empty when refused).
    /// </summary>
    Task<ApplicationTokenDecision> EvaluateApplicationAsync(string clientId, CancellationToken cancellationToken);
}

/// <summary>
/// Organizations (tenants): who belongs to which, with which role. Every method re-checks the caller through
/// <see cref="IAccessAdminAuthorization"/>, except <see cref="ListMineAsync"/>, which is the caller's own.
/// </summary>
public interface IOrganizationService
{
    Task<Result<IReadOnlyList<OrganizationSummaryDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<OrganizationDetailDto>> GetAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<Result<OrganizationDetailDto>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken);

    Task<Result<OrganizationDetailDto>> UpdateAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken);

    /// <summary>Deletes the organization; its members' sessions in the applications that work per organization end.</summary>
    Task<Result> DeleteAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Adds a member, or changes the role of one. Idempotent.</summary>
    Task<Result<OrganizationDetailDto>> SetMemberAsync(
        Guid organizationId,
        Guid userId,
        SetOrganizationMemberRequest request,
        CancellationToken cancellationToken);

    /// <summary>Removes a member, ending their sessions in the applications that work per organization. Idempotent.</summary>
    Task<Result<OrganizationDetailDto>> RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds an existing account by its address. Like the other member operations it is open to the portal's
    /// administrators and to the organization's own administrators (delegated management).
    /// </summary>
    Task<Result<OrganizationDetailDto>> AddMemberByEmailAsync(Guid organizationId, AddOrganizationMemberRequest request, CancellationToken cancellationToken);

    /// <summary>The organizations one user belongs to (administration screens).</summary>
    Task<Result<IReadOnlyList<OrganizationMembershipDto>>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The organizations the signed-in user belongs to (<see cref="IAccessCaller"/>).</summary>
    Task<Result<IReadOnlyList<OrganizationMembershipDto>>> ListMineAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Access requests: a user asks for an application they cannot open, a reviewer approves (which grants it, with
/// roles) or rejects it. The user side always works on the signed-in user, whom <see cref="IAccessCaller"/>
/// names, never on a parameter; the reviewer side re-checks the caller through <see cref="IAccessAdminAuthorization"/>
/// (<see cref="AccessOperation.ReviewAccessRequests"/>).
/// </summary>
public interface IAccessRequestService
{
    /// <summary>The active applications that accept requests and the caller cannot open yet.</summary>
    Task<Result<IReadOnlyList<RequestableApplicationDto>>> GetCatalogAsync(CancellationToken cancellationToken);

    /// <summary>The caller's own requests, newest first.</summary>
    Task<Result<IReadOnlyList<MyAccessRequestDto>>> ListMineAsync(CancellationToken cancellationToken);

    /// <summary>Asks for an application. One pending request per application; none for one the caller can open.</summary>
    Task<Result<MyAccessRequestDto>> SubmitAsync(SubmitAccessRequest request, CancellationToken cancellationToken);

    /// <summary>Withdraws one of the caller's pending requests.</summary>
    Task<Result> CancelAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>The requests reviewers see: pending ones first (oldest first), then the decided ones.</summary>
    Task<Result<AccessRequestPageDto>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken);

    /// <summary>Grants the application to the requester with the chosen roles and closes the request.</summary>
    Task<Result<AccessRequestDto>> ApproveAsync(Guid requestId, ReviewAccessRequest review, CancellationToken cancellationToken);

    Task<Result<AccessRequestDto>> RejectAsync(Guid requestId, ReviewAccessRequest review, CancellationToken cancellationToken);
}

/// <summary>Counts of applications, groups, organizations, grants and waiting requests, for the statistics page.</summary>
public interface IAccessStatisticsService
{
    /// <summary>Re-checks the caller (<see cref="AccessOperation.ViewStatistics"/>).</summary>
    Task<Result<AccessStatisticsDto>> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Group and organization memberships promised by an invitation. <see cref="ValidateMembershipsAsync"/> runs when
/// the invitation is made and checks that the inviter may give them; <see cref="ApplyMembershipsAsync"/> runs when
/// the invited person has an account and performs no caller check (nobody is signed in yet), so the host calls it
/// only for an accepted invitation.
/// </summary>
public interface IAccessProvisioningService
{
    /// <summary>Every group and organization exists, and the caller may manage groups (or organizations) if any is given.</summary>
    Task<Result> ValidateMembershipsAsync(
        IReadOnlyCollection<Guid> groupIds,
        IReadOnlyCollection<Guid> organizationIds,
        CancellationToken cancellationToken);

    /// <summary>Adds the user to the groups and (as a member) to the organizations that still exist.</summary>
    Task ApplyMembershipsAsync(
        Guid userId,
        string? userEmail,
        IReadOnlyCollection<Guid> groupIds,
        IReadOnlyCollection<Guid> organizationIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// The signed-in user, for the operations a user performs on their own behalf (asking for an application,
/// choosing an organization). Supplied by the host from the request, so the module never takes it as a parameter.
/// </summary>
public interface IAccessCaller
{
    /// <summary>The signed-in user's id, or null for an anonymous caller.</summary>
    Guid? UserId { get; }
}

/// <summary>
/// What an application's own backend reads and changes through the application API, always on the calling
/// application: which one is derived from <see cref="IApplicationApiCaller"/>, never from a parameter. Every
/// method re-checks that the caller is an active application whose token carries the needed scope, and that
/// the scope is still enabled for it.
/// </summary>
public interface IApplicationApiService
{
    /// <summary>The calling application with its roles and enabled scopes. Any application token.</summary>
    Task<Result<ApplicationSelfDto>> GetSelfAsync(CancellationToken cancellationToken);

    /// <summary>The users who can open the application, directly or through a group (<c>portal.users.read</c>).</summary>
    Task<Result<ApplicationUserPageDto>> ListUsersAsync(ApplicationUserListQuery query, CancellationToken cancellationToken);

    /// <summary>One user who can open the application; not found otherwise (<c>portal.users.read</c>).</summary>
    Task<Result<ApplicationUserDto>> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Grants the application to the user personally (<c>portal.access.manage</c>). With
    /// <paramref name="roles"/> the grant carries exactly those roles; without, an existing grant keeps its roles.
    /// </summary>
    Task<Result<ApplicationGrantResultDto>> GrantUserAsync(
        Guid userId,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the user's personal grant (<c>portal.access.manage</c>). Access through a group stays: the answer
    /// says whether the user can still open the application.
    /// </summary>
    Task<Result<ApplicationRevokeResultDto>> RevokeUserAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Who is calling the application API. Supplied by the host from the request's token, so the module stays
/// unaware of how applications authenticate.
/// </summary>
public interface IApplicationApiCaller
{
    /// <summary>The client id of an application calling with a token of its own; null for any other caller.</summary>
    string? ClientId { get; }

    /// <summary>Whether the caller's token was issued with <paramref name="scope"/>.</summary>
    bool HasScope(string scope);
}

/// <summary>Receives announcements from running applications, authenticated by the provisioning key.</summary>
public interface IApplicationAnnouncementService
{
    Task<Result<AnnouncementResultDto>> AnnounceAsync(
        ApplicationManifest manifest,
        string? provisioningKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Which portal pages the members of each group may open. Every method re-checks the caller through
/// <see cref="IAccessAdminAuthorization"/> (<see cref="AccessOperation.ManagePagePermissions"/>). Grants are
/// idempotent, like application grants.
/// </summary>
public interface IPagePermissionService
{
    /// <summary>Every page of the catalog, every group, and which group may open which page.</summary>
    Task<Result<PagePermissionMatrixDto>> GetMatrixAsync(CancellationToken cancellationToken);

    Task<Result> GrantAsync(string pageKey, Guid groupId, CancellationToken cancellationToken);

    Task<Result> RevokeAsync(string pageKey, Guid groupId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the pages of one group with <paramref name="pageKeys"/> in one change, so a whole area can be
    /// switched at once. Every key must be in the catalog.
    /// </summary>
    Task<Result> SetGroupPagesAsync(Guid groupId, IReadOnlyCollection<string> pageKeys, CancellationToken cancellationToken);
}

/// <summary>
/// Answers "which pages may this user open" through their groups. Used by the host's authorization handler
/// and the session, which identify the user themselves, so it performs no caller check of its own.
/// </summary>
public interface IPagePermissionEvaluator
{
    /// <summary>Keys of the catalog pages granted to any group the user belongs to.</summary>
    Task<IReadOnlyList<string>> GetPagesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The users who belong to a group granted any of <paramref name="pageKeys"/> (administrators, who hold every
    /// page without a grant, are not included).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetHoldersAsync(IReadOnlyCollection<string> pageKeys, CancellationToken cancellationToken);
}

/// <summary>
/// The pages of the portal that can be granted to groups. Supplied by the host, which owns the pages; the
/// module only stores grants against their keys (the same seam as Identity's <c>ILanguageCatalog</c>).
/// </summary>
public interface IPortalPageCatalog
{
    IReadOnlyList<PortalPageDto> Pages { get; }

    bool IsKnown(string? pageKey);
}

/// <summary>What the caller wants to do, so the host can decide which pages allow it.</summary>
public enum AccessOperation
{
    ListApplications,
    ManageApplications,
    ListGroups,
    ManageGroups,
    ViewTree,
    ViewUserAccess,
    ManageUserGrants,
    ManageGroupGrants,
    ManagePagePermissions,
    ReviewAccessRequests,
    ListOrganizations,
    ManageOrganizations,
    ViewStatistics,
    ReviewAccess,
}

/// <summary>
/// Decides whether the caller may perform an administrative operation. Supplied by the host, so this module
/// stays independent of the Identity module (the same seam as Content's <c>IContentEditAuthorization</c>).
/// </summary>
public interface IAccessAdminAuthorization
{
    Task<Result> EnsureCanAsync(AccessOperation operation, CancellationToken cancellationToken);
}

/// <summary>Looks up user accounts by id. Supplied by the host from its identity store.</summary>
public interface IUserDirectory
{
    /// <summary>Returns the accounts that exist among <paramref name="userIds"/>; unknown ids are left out.</summary>
    Task<IReadOnlyDictionary<Guid, DirectoryUser>> FindAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>The account with this exact address, or null.</summary>
    Task<DirectoryUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}

/// <summary>
/// Which applications should hear about an event, for the host's webhook delivery. Performs no caller check: the host
/// calls it while recording something that already happened, so it must never be put behind an endpoint.
/// </summary>
public interface IWebhookDirectory
{
    /// <summary>Active applications the user can open that subscribe to <paramref name="eventType"/>.</summary>
    Task<IReadOnlyList<WebhookTarget>> ForUserAsync(Guid userId, string eventType, CancellationToken cancellationToken);

    /// <summary>Active applications the group has been given that subscribe to <paramref name="eventType"/>.</summary>
    Task<IReadOnlyList<WebhookTarget>> ForGroupAsync(Guid groupId, string eventType, CancellationToken cancellationToken);

    /// <summary>The application itself, when it subscribes to <paramref name="eventType"/>.</summary>
    Task<IReadOnlyList<WebhookTarget>> ForApplicationAsync(string clientId, string eventType, CancellationToken cancellationToken);

    /// <summary>The application's webhook as it is now (for a delivery or a retry); null once it was turned off.</summary>
    Task<WebhookTarget?> FindAsync(string clientId, CancellationToken cancellationToken);
}

/// <summary>
/// Access reviews (recertification): a snapshot of an application's grants that reviewers confirm or revoke, applied
/// when the review is completed. Checked as <see cref="AccessOperation.ReviewAccess"/>.
/// </summary>
public interface IAccessReviewService
{
    Task<Result<IReadOnlyList<AccessReviewSummaryDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<AccessReviewDto>> GetAsync(Guid reviewId, CancellationToken cancellationToken);

    Task<Result<AccessReviewDto>> StartAsync(StartAccessReviewRequest request, CancellationToken cancellationToken);

    Task<Result<AccessReviewDto>> DecideAsync(Guid reviewId, Guid itemId, DecideAccessReviewItemRequest request, CancellationToken cancellationToken);

    /// <summary>Closes the review and removes the access marked for revocation (and, if so set, the undecided).</summary>
    Task<Result<AccessReviewDto>> CompleteAsync(Guid reviewId, CancellationToken cancellationToken);

    Task<Result<AccessReviewDto>> CancelAsync(Guid reviewId, CancellationToken cancellationToken);
}
