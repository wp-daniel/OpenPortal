namespace OpenPortal.Access.Application.Contracts;

// ---------------------------------------------------------------------------
// Applications.
// ---------------------------------------------------------------------------

/// <summary>An application as the administrator sees it.</summary>
/// <param name="Status"><c>pending</c>, <c>active</c> or <c>disabled</c>.</param>
/// <param name="Source"><c>manual</c> or <c>announced</c>.</param>
/// <param name="HasManifestChanges">The application proposed redirect URIs that differ from the ones in force.</param>
/// <param name="UserCount">Users granted access directly.</param>
/// <param name="GroupCount">Groups granted access.</param>
/// <param name="Roles">The roles the application understands.</param>
/// <param name="GroupClaims"><c>none</c>, <c>granted</c> or <c>all</c>: which groups reach the application in its tokens.</param>
public sealed record ApplicationDto(
    Guid Id,
    string ClientId,
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    string Status,
    string Source,
    string? Version,
    IReadOnlyList<string> AnnouncedRedirectUris,
    IReadOnlyList<string> AnnouncedPostLogoutRedirectUris,
    bool HasManifestChanges,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? LastSeenAtUtc,
    int UserCount,
    int GroupCount,
    IReadOnlyList<ApplicationRoleDto> Roles,
    string GroupClaims);

/// <summary>A role an application understands. The key is what reaches the application in the <c>role</c> claim.</summary>
public sealed record ApplicationRoleDto(string Key, string? DisplayName, string? Description);

/// <summary>Values of <see cref="ApplicationDto.GroupClaims"/>.</summary>
public static class GroupClaimModes
{
    public const string None = "none";
    public const string Granted = "granted";
    public const string All = "all";
}

/// <summary>
/// Returned by the operations that issue a client secret. The secret is never stored in clear and cannot be
/// read again: the administrator copies it now or generates a new one.
/// </summary>
public sealed record ApplicationSecretDto(ApplicationDto Application, string ClientSecret);

/// <summary>Field set for registering an application by hand.</summary>
/// <param name="Roles">The roles the application understands; none when absent.</param>
/// <param name="GroupClaims"><c>none</c> (the default), <c>granted</c> or <c>all</c>.</param>
public sealed record CreateApplicationRequest(
    string ClientId,
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<ApplicationRoleDto>? Roles = null,
    string? GroupClaims = null);

/// <summary>Field set for editing an application. The client id cannot change.</summary>
/// <param name="Roles">
/// The complete list of roles; a role left out is removed, with its assignments. Absent (null) leaves the
/// roles as they are.
/// </param>
/// <param name="GroupClaims"><c>none</c>, <c>granted</c> or <c>all</c>; absent leaves it as it is.</param>
public sealed record UpdateApplicationRequest(
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<ApplicationRoleDto>? Roles = null,
    string? GroupClaims = null);

/// <summary>What a running application reports about itself when it announces to the portal.</summary>
/// <param name="Roles">
/// The roles the application checks. A pending application takes them as they are; an approved one only
/// gains the new ones.
/// </param>
public sealed record ApplicationManifest(
    string ClientId,
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    string? Version,
    IReadOnlyList<ApplicationRoleDto>? Roles = null);

/// <param name="Status">The application's status after the announcement, so it can log "waiting for approval".</param>
public sealed record AnnouncementResultDto(string ClientId, string Status);

// ---------------------------------------------------------------------------
// Groups.
// ---------------------------------------------------------------------------

public sealed record GroupSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    int MemberCount,
    int ApplicationCount,
    DateTimeOffset CreatedAtUtc);

public sealed record GroupDetailDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<UserReferenceDto> Members,
    IReadOnlyList<ApplicationReferenceDto> Applications,
    IReadOnlyList<string> Pages,
    DateTimeOffset CreatedAtUtc);

public sealed record SaveGroupRequest(string Name, string? Description);

// ---------------------------------------------------------------------------
// Access.
// ---------------------------------------------------------------------------

/// <summary>A user as shown next to a grant or a membership.</summary>
/// <param name="IsKnown">False when the account no longer exists; the grant is shown so it can be removed.</param>
public sealed record UserReferenceDto(Guid Id, string Email, string DisplayName, bool IsKnown);

public sealed record ApplicationReferenceDto(Guid Id, string ClientId, string DisplayName, string Status);

public sealed record GroupReferenceDto(Guid Id, string Name);

/// <summary>Every application with who can open it: groups (with their members) and users granted directly.</summary>
public sealed record AccessTreeDto(IReadOnlyList<AccessTreeApplicationDto> Applications);

/// <param name="Roles">The roles the application defines, for assigning them.</param>
public sealed record AccessTreeApplicationDto(
    Guid Id,
    string ClientId,
    string DisplayName,
    string Status,
    DateTimeOffset? LastSeenAtUtc,
    IReadOnlyList<AccessTreeGroupDto> Groups,
    IReadOnlyList<AccessTreeUserDto> Users,
    IReadOnlyList<ApplicationRoleDto> Roles);

/// <param name="Roles">Role keys that come with the group's grant.</param>
public sealed record AccessTreeGroupDto(Guid Id, string Name, IReadOnlyList<UserReferenceDto> Members, IReadOnlyList<string> Roles);

/// <summary>A user granted an application directly.</summary>
/// <param name="Roles">Role keys that come with the user's own grant.</param>
public sealed record AccessTreeUserDto(Guid Id, string Email, string DisplayName, bool IsKnown, IReadOnlyList<string> Roles);

/// <summary>Grants an application, optionally with roles.</summary>
/// <param name="Roles">
/// Role keys to come with the grant, replacing the ones it had. Absent (null) keeps an existing grant's roles
/// and gives a new grant none.
/// </param>
public sealed record GrantRequest(IReadOnlyList<string>? Roles);

/// <summary>What one user can open, and why.</summary>
public sealed record UserAccessDto(
    Guid UserId,
    IReadOnlyList<GroupReferenceDto> Groups,
    IReadOnlyList<UserApplicationAccessDto> Applications);

/// <param name="Direct">Granted to the user personally.</param>
/// <param name="ViaGroups">Groups of the user that are granted the application.</param>
/// <param name="Roles">The user's roles in the application, from every grant together.</param>
public sealed record UserApplicationAccessDto(
    Guid ApplicationId,
    string ClientId,
    string DisplayName,
    string Status,
    bool Direct,
    IReadOnlyList<GroupReferenceDto> ViaGroups,
    IReadOnlyList<string> Roles);

/// <summary>An application on a user's launchpad.</summary>
public sealed record LaunchpadItemDto(Guid Id, string ClientId, string DisplayName, string? Description, string BaseUrl);

/// <summary>The answer to "may this user sign in to this client".</summary>
/// <param name="Allowed">True only when the application is active and the user holds a grant.</param>
/// <param name="ApplicationName">Display name to show on a refusal; null for an unknown client.</param>
/// <param name="Roles">The user's role keys in the application (empty when refused).</param>
/// <param name="Groups">
/// The group names the application receives, as its <see cref="ApplicationDto.GroupClaims"/> setting allows
/// (empty when refused or when the setting is <c>none</c>).
/// </param>
public sealed record AccessDecision(
    bool Allowed,
    string? ApplicationName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Groups)
{
    public static AccessDecision Denied(string? applicationName) => new(false, applicationName, [], []);
}

// ---------------------------------------------------------------------------
// Page permissions.
// ---------------------------------------------------------------------------

/// <summary>A page of the portal that can be granted to groups.</summary>
/// <param name="Key">Stable identifier, e.g. <c>users</c> or <c>content.projects</c>.</param>
/// <param name="LabelKey">Translation key of the page name.</param>
/// <param name="AreaKey">Translation key of the area the page belongs to.</param>
public sealed record PortalPageDto(string Key, string LabelKey, string AreaKey);

public sealed record PageGrantDto(string PageKey, Guid GroupId);

/// <summary>The complete set of pages a group should hold.</summary>
public sealed record SetGroupPagesRequest(IReadOnlyList<string>? Pages);

/// <summary>The page permission matrix: catalog pages × groups, and the grants between them.</summary>
public sealed record PagePermissionMatrixDto(
    IReadOnlyList<PortalPageDto> Pages,
    IReadOnlyList<GroupReferenceDto> Groups,
    IReadOnlyList<PageGrantDto> Grants);

// ---------------------------------------------------------------------------
// Ports implemented by the host.
// ---------------------------------------------------------------------------

/// <summary>A user account as the host's identity store describes it.</summary>
public sealed record DirectoryUser(Guid Id, string Email, string DisplayName);

// ---------------------------------------------------------------------------
// OpenID Connect.
// ---------------------------------------------------------------------------

/// <summary>The scopes, beyond the standard ones, an application can ask the portal for.</summary>
public static class PortalScopes
{
    /// <summary>The user's roles in the application, as <c>role</c> claims.</summary>
    public const string Roles = "roles";

    /// <summary>The user's groups, as <c>groups</c> claims, when the application's setting allows it.</summary>
    public const string Groups = "groups";
}
