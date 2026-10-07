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
    Task<Result<GroupDetailDto>> AddMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken);

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

    Task<Result> GrantUserAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken);

    Task<Result> RevokeUserAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken);

    Task<Result> GrantGroupAsync(Guid applicationId, Guid groupId, CancellationToken cancellationToken);

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
}
