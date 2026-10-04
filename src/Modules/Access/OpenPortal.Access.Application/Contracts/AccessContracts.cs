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
    int GroupCount);

/// <summary>
/// Returned by the operations that issue a client secret. The secret is never stored in clear and cannot be
/// read again: the administrator copies it now or generates a new one.
/// </summary>
public sealed record ApplicationSecretDto(ApplicationDto Application, string ClientSecret);

/// <summary>Field set for registering an application by hand.</summary>
public sealed record CreateApplicationRequest(
    string ClientId,
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris);

/// <summary>Field set for editing an application. The client id cannot change.</summary>
public sealed record UpdateApplicationRequest(
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris);

/// <summary>What a running application reports about itself when it announces to the portal.</summary>
public sealed record ApplicationManifest(
    string ClientId,
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    string? Version);

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

public sealed record AccessTreeApplicationDto(
    Guid Id,
    string ClientId,
    string DisplayName,
    string Status,
    DateTimeOffset? LastSeenAtUtc,
    IReadOnlyList<AccessTreeGroupDto> Groups,
    IReadOnlyList<UserReferenceDto> Users);

public sealed record AccessTreeGroupDto(Guid Id, string Name, IReadOnlyList<UserReferenceDto> Members);

/// <summary>What one user can open, and why.</summary>
public sealed record UserAccessDto(
    Guid UserId,
    IReadOnlyList<GroupReferenceDto> Groups,
    IReadOnlyList<UserApplicationAccessDto> Applications);

/// <param name="Direct">Granted to the user personally.</param>
/// <param name="ViaGroups">Groups of the user that are granted the application.</param>
public sealed record UserApplicationAccessDto(
    Guid ApplicationId,
    string ClientId,
    string DisplayName,
    string Status,
    bool Direct,
    IReadOnlyList<GroupReferenceDto> ViaGroups);

/// <summary>An application on a user's launchpad.</summary>
public sealed record LaunchpadItemDto(Guid Id, string ClientId, string DisplayName, string? Description, string BaseUrl);

/// <summary>The answer to "may this user sign in to this client".</summary>
/// <param name="Allowed">True only when the application is active and the user holds a grant.</param>
/// <param name="ApplicationName">Display name to show on a refusal; null for an unknown client.</param>
public sealed record AccessDecision(bool Allowed, string? ApplicationName);

// ---------------------------------------------------------------------------
// Ports implemented by the host.
// ---------------------------------------------------------------------------

/// <summary>A user account as the host's identity store describes it.</summary>
public sealed record DirectoryUser(Guid Id, string Email, string DisplayName);
