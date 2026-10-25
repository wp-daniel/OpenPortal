using System.ComponentModel.DataAnnotations;

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
/// <param name="ApiScopes">Application API scopes its backend may request with client credentials; empty for none.</param>
/// <param name="AllowAccessRequests">Users who cannot open it see it in their catalog and may ask for it.</param>
/// <param name="Organizations"><c>none</c>, <c>optional</c> or <c>required</c>: whether tokens name the user's organization.</param>
/// <param name="ClientType"><c>confidential</c> (with a secret) or <c>public</c> (PKCE only).</param>
/// <param name="BackChannelLogoutUri">Where the portal posts logout tokens when a user signs out; null for none.</param>
/// <param name="AnnouncedBackChannelLogoutUri">The back-channel logout URI the application last proposed.</param>
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
    string GroupClaims,
    IReadOnlyList<string> ApiScopes,
    bool AllowAccessRequests,
    string Organizations,
    string ClientType = ApplicationClientTypes.Confidential,
    string? BackChannelLogoutUri = null,
    string? AnnouncedBackChannelLogoutUri = null,
    string? WebhookUrl = null,
    IReadOnlyList<string>? WebhookEvents = null,
    bool HasWebhookSecret = false,
    bool RequireConsent = false,
    bool AllowDeviceFlow = false);

/// <summary>A new webhook signing secret (<c>whsec_…</c>), shown once.</summary>
public sealed record WebhookSecretDto(string Secret);

/// <summary>Where and how to deliver a webhook to an application, with the secret in clear. Never leaves the server.</summary>
public sealed record WebhookTarget(string ClientId, string Url, string Secret);

/// <summary>Values of <see cref="ApplicationDto.ClientType"/>.</summary>
public static class ApplicationClientTypes
{
    public const string Confidential = "confidential";
    public const string Public = "public";
}

/// <summary>Values of <see cref="ApplicationDto.Organizations"/>.</summary>
public static class OrganizationModes
{
    public const string None = "none";
    public const string Optional = "optional";
    public const string Required = "required";
}

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
/// <param name="ClientSecret">Null for a public client, which has none.</param>
/// <param name="PreviousSecretValidUntilUtc">After a replacement with an overlap, until when the replaced secret still works.</param>
public sealed record ApplicationSecretDto(ApplicationDto Application, string? ClientSecret, DateTimeOffset? PreviousSecretValidUntilUtc = null);

/// <summary>Replaces an application's client secret.</summary>
/// <param name="KeepPreviousHours">
/// How long the replaced secret keeps working (0 to <see cref="MaxKeepPreviousHours"/>), so the application's servers can
/// be given the new one without downtime; 0 (the default) stops it at once, as after a leak.
/// </param>
public sealed record RegenerateSecretRequest(int KeepPreviousHours = 0)
{
    public const int MaxKeepPreviousHours = 720;
}

/// <summary>Field set for registering an application by hand.</summary>
/// <param name="Roles">The roles the application understands; none when absent.</param>
/// <param name="GroupClaims"><c>none</c> (the default), <c>granted</c> or <c>all</c>.</param>
/// <param name="ApiScopes">Application API scopes for the application's backend; none when absent.</param>
/// <param name="AllowAccessRequests">Whether users may ask for it; no when absent.</param>
/// <param name="Organizations"><c>none</c> (the default), <c>optional</c> or <c>required</c>.</param>
/// <param name="ClientType"><c>confidential</c> (the default) or <c>public</c>; fixed once registered.</param>
/// <param name="BackChannelLogoutUri">Where the portal posts logout tokens; none when absent.</param>
public sealed record CreateApplicationRequest(
    string ClientId,
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<ApplicationRoleDto>? Roles = null,
    string? GroupClaims = null,
    IReadOnlyList<string>? ApiScopes = null,
    bool? AllowAccessRequests = null,
    string? Organizations = null,
    string? ClientType = null,
    string? BackChannelLogoutUri = null,
    string? WebhookUrl = null,
    IReadOnlyList<string>? WebhookEvents = null,
    bool? RequireConsent = null,
    bool? AllowDeviceFlow = null);

/// <summary>Field set for editing an application. The client id cannot change.</summary>
/// <param name="Roles">
/// The complete list of roles; a role left out is removed, with its assignments. Absent (null) leaves the
/// roles as they are.
/// </param>
/// <param name="GroupClaims"><c>none</c>, <c>granted</c> or <c>all</c>; absent leaves it as it is.</param>
/// <param name="ApiScopes">The complete list of application API scopes; absent (null) leaves them as they are.</param>
/// <param name="AllowAccessRequests">Whether users may ask for it; absent leaves it as it is.</param>
/// <param name="Organizations"><c>none</c>, <c>optional</c> or <c>required</c>; absent leaves it as it is.</param>
/// <param name="BackChannelLogoutUri">Where the portal posts logout tokens; empty or absent for none.</param>
public sealed record UpdateApplicationRequest(
    string DisplayName,
    string? Description,
    string BaseUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<ApplicationRoleDto>? Roles = null,
    string? GroupClaims = null,
    IReadOnlyList<string>? ApiScopes = null,
    bool? AllowAccessRequests = null,
    string? Organizations = null,
    string? BackChannelLogoutUri = null,
    string? WebhookUrl = null,
    IReadOnlyList<string>? WebhookEvents = null,
    bool? RequireConsent = null,
    bool? AllowDeviceFlow = null);

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
    IReadOnlyList<ApplicationRoleDto>? Roles = null,
    string? BackChannelLogoutUri = null);

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
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<GroupReferenceDto>? Subgroups = null,
    IReadOnlyList<GroupReferenceDto>? ParentGroups = null,
    IReadOnlyDictionary<Guid, DateTimeOffset>? MemberExpiries = null);

public sealed record SaveGroupRequest(string Name, string? Description);

/// <summary>Adds a member (or changes when their membership ends): no end date means until removed.</summary>
public sealed record GroupMemberRequest(DateTimeOffset? ExpiresAtUtc = null);

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
/// <param name="ExpiresAtUtc">When the grant ends by itself (temporary access); absent leaves the end as it is.</param>
/// <param name="ClearExpiry">Removes the end date: the grant lasts until it is removed.</param>
public sealed record GrantRequest(IReadOnlyList<string>? Roles, DateTimeOffset? ExpiresAtUtc = null, bool ClearExpiry = false)
{
    /// <summary>The change to the end date the request asks for, or null for none.</summary>
    public ExpiryChange? Expiry => ExpiresAtUtc is { } at ? new ExpiryChange(at) : ClearExpiry ? new ExpiryChange(null) : null;
}

/// <summary>A change of a grant's end date: present means "set it to this" (null: no end); absent leaves it.</summary>
public sealed record ExpiryChange(DateTimeOffset? ExpiresAtUtc);

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

/// <summary>An application the signed-in user let sign them in (it holds a lasting authorization, and may hold tokens).</summary>
/// <param name="Scopes">What the application receives (<c>openid</c>, <c>email</c>, <c>offline_access</c>…).</param>
public sealed record AuthorizedApplicationDto(
    string ClientId,
    string DisplayName,
    string? BaseUrl,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? AuthorizedAtUtc);

/// <summary>
/// A slice of a long list (groups, organizations): a search on the name and a window, so a screen or a picker reads a
/// page instead of everything.
/// </summary>
/// <param name="Search">Part of the name (or description), case-insensitive; empty for all.</param>
public sealed record ListSlice(string? Search = null, int Skip = 0, int? Take = null)
{
    public const int MaxTake = 200;

    /// <summary>The whole list (the screens that need every entry).</summary>
    public static ListSlice All { get; } = new();
}

/// <summary>One slice of a list, with how many entries match in all.</summary>
public sealed record SliceDto<T>(IReadOnlyList<T> Items, int TotalCount);

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
/// <param name="RequireConsent">The application asks the user to agree to what it receives before its first sign-in.</param>
public sealed record AccessDecision(
    bool Allowed,
    string? ApplicationName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Groups,
    bool RequireConsent = false)
{
    public static AccessDecision Denied(string? applicationName) => new(false, applicationName, [], []);
}

// ---------------------------------------------------------------------------
// Statistics.
// ---------------------------------------------------------------------------

/// <summary>What the Access module holds, counted.</summary>
public sealed record AccessStatisticsDto(
    int Applications,
    int ActiveApplications,
    int PendingApplications,
    int DisabledApplications,
    int Groups,
    int Organizations,
    int UserGrants,
    int GroupGrants,
    int PendingAccessRequests);

// ---------------------------------------------------------------------------
// Organizations.
// ---------------------------------------------------------------------------

/// <summary>Values of an organization member's role.</summary>
public static class OrganizationRoles
{
    public const string Member = "member";
    public const string Admin = "admin";
}

public sealed record OrganizationSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int MemberCount,
    DateTimeOffset CreatedAtUtc);

/// <param name="RequireMultiFactor">Members sign in to the organization's applications with two factors only.</param>
/// <param name="Domains">The email domains the organization claims, and whether people at them join by themselves.</param>
/// <param name="Applications">The applications that work per organization and define roles: what members can be given here.</param>
public sealed record OrganizationDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    IReadOnlyList<OrganizationMemberDto> Members,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    bool RequireMultiFactor = false,
    IReadOnlyList<OrganizationDomainDto>? Domains = null,
    IReadOnlyList<OrganizationApplicationDto>? Applications = null);

/// <summary>An email domain an organization claims.</summary>
/// <param name="RecordValue">The TXT record to publish at the domain to verify it.</param>
/// <param name="VerifiedBy"><c>dns</c> or <c>administrator</c>.</param>
/// <param name="AutoJoinRole"><c>member</c> or <c>admin</c>: what someone joining by the domain becomes.</param>
public sealed record OrganizationDomainDto(
    Guid Id,
    string Domain,
    bool Verified,
    DateTimeOffset? VerifiedAtUtc,
    string? VerifiedBy,
    string RecordValue,
    bool AutoJoin,
    string AutoJoinRole);

/// <summary>An application that works per organization, with the roles a member can hold in it within the organization.</summary>
public sealed record OrganizationApplicationDto(Guid Id, string DisplayName, IReadOnlyList<ApplicationRoleDto> Roles);

/// <summary>The roles a member holds in one application within the organization.</summary>
public sealed record OrganizationMemberApplicationRolesDto(Guid ApplicationId, IReadOnlyList<string> Roles);

/// <param name="Role"><c>member</c> or <c>admin</c>.</param>
/// <param name="IsKnown">False when the account no longer exists; the membership is shown so it can be removed.</param>
/// <param name="ApplicationRoles">The roles the member holds in the organization's applications (within this organization only).</param>
public sealed record OrganizationMemberDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsKnown,
    string Role,
    DateTimeOffset AddedAtUtc,
    IReadOnlyList<OrganizationMemberApplicationRolesDto>? ApplicationRoles = null);

/// <param name="Slug">Lower-case short name, immutable once created.</param>
public sealed record CreateOrganizationRequest(string Name, string Slug, string? Description);

/// <summary>The short name (slug) cannot change.</summary>
/// <param name="RequireMultiFactor">Members sign in to the organization's applications with two factors only; absent leaves it.</param>
public sealed record UpdateOrganizationRequest(string Name, string? Description, bool? RequireMultiFactor = null);

/// <summary>Claims an email domain for the organization (unverified until its DNS shows the record).</summary>
public sealed record AddOrganizationDomainRequest(string Domain);

/// <summary>Whether people with a confirmed address at the domain join the organization when they sign in, and as what.</summary>
/// <param name="Role"><c>member</c> (the default) or <c>admin</c>.</param>
public sealed record UpdateOrganizationDomainRequest(bool AutoJoin, string? Role);

/// <summary>Checks the domain's DNS, or (<paramref name="Manual"/>, portal administrators only) vouches for it.</summary>
public sealed record VerifyOrganizationDomainRequest(bool Manual = false);

/// <summary>The roles a member holds in one application within the organization (replaces them).</summary>
public sealed record SetMemberApplicationRolesRequest(IReadOnlyList<string> Roles);

/// <summary>Adds a member or changes their role.</summary>
/// <param name="Role"><c>member</c> (the default) or <c>admin</c>.</param>
public sealed record SetOrganizationMemberRequest(string? Role);

/// <summary>Adds an existing account to an organization by its address.</summary>
public sealed record AddOrganizationMemberRequest(string Email, string? Role);

/// <summary>An organization the signed-in user belongs to, as applications are told about it.</summary>
/// <param name="Role"><c>member</c> or <c>admin</c>.</param>
public sealed record OrganizationMembershipDto(Guid Id, string Slug, string Name, string Role);

/// <summary>What the token endpoints should do about organizations for one sign-in.</summary>
public enum OrganizationOutcome
{
    /// <summary>The application does not work per organization; tokens carry no organization.</summary>
    NotUsed,

    /// <summary>Tokens name <see cref="OrganizationDecision.Organization"/>.</summary>
    Selected,

    /// <summary>The user belongs to no organization and the application can do without one.</summary>
    Unaffiliated,

    /// <summary>The user belongs to several and must choose one among <see cref="OrganizationDecision.Choices"/>.</summary>
    ChoiceNeeded,

    /// <summary>The application needs an organization and the user belongs to none: refuse the sign-in.</summary>
    Missing,

    /// <summary>The organization asked for is not one of the user's (or no longer exists): refuse the sign-in.</summary>
    NotMember,
}

/// <param name="Organization">The organization the tokens name, when <see cref="OrganizationOutcome.Selected"/>.</param>
/// <param name="Choices">The user's organizations, when a choice is needed.</param>
/// <param name="Roles">The application roles the user holds within the selected organization (added to the grants' roles).</param>
/// <param name="RequireMultiFactor">The selected organization signs its members in with two factors only.</param>
public sealed record OrganizationDecision(
    OrganizationOutcome Outcome,
    OrganizationMembershipDto? Organization,
    IReadOnlyList<OrganizationMembershipDto> Choices,
    IReadOnlyList<string>? Roles = null,
    bool RequireMultiFactor = false);

// ---------------------------------------------------------------------------
// Access requests.
// ---------------------------------------------------------------------------

/// <summary>Values of the access request status.</summary>
public static class AccessRequestStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
}

/// <summary>An application the signed-in user cannot open but may ask for.</summary>
/// <param name="LatestRequest">The user's most recent request for it, if any (pending, or how the last one ended).</param>
public sealed record RequestableApplicationDto(
    Guid Id,
    string ClientId,
    string DisplayName,
    string? Description,
    MyAccessRequestDto? LatestRequest);

/// <summary>One of the signed-in user's own requests.</summary>
/// <param name="Status"><c>pending</c>, <c>approved</c>, <c>rejected</c> or <c>cancelled</c>.</param>
/// <param name="DecisionNote">The reviewer's message, once decided.</param>
public sealed record MyAccessRequestDto(
    Guid Id,
    Guid ApplicationId,
    string ApplicationName,
    string? Reason,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionNote);

/// <summary>A request as a reviewer sees it.</summary>
/// <param name="ApplicationRoles">The roles the application defines, to choose from when approving.</param>
/// <param name="HasAccess">Whether the user can open the application now (granted some other way meanwhile).</param>
/// <param name="Roles">The role keys the approval granted.</param>
public sealed record AccessRequestDto(
    Guid Id,
    ApplicationReferenceDto Application,
    UserReferenceDto User,
    string? Reason,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    UserReferenceDto? DecidedBy,
    string? DecisionNote,
    IReadOnlyList<string> Roles,
    IReadOnlyList<ApplicationRoleDto> ApplicationRoles,
    bool HasAccess);

/// <summary>A page of access requests, oldest pending first, then newest decided.</summary>
/// <param name="PendingCount">Requests waiting for a decision in total, whatever the filter.</param>
public sealed record AccessRequestPageDto(
    IReadOnlyList<AccessRequestDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int PendingCount);

/// <summary>Filter and paging for the reviewers' list.</summary>
public sealed class AccessRequestQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "validation.page.min")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "validation.pageSize.range")]
    public int PageSize { get; init; } = 25;

    /// <summary>One of <see cref="AccessRequestStatuses"/>; absent or anything else lists every request.</summary>
    [StringLength(16, ErrorMessage = "validation.search.max")]
    public string? Status { get; init; }

    /// <summary>Only the requests for one application.</summary>
    public Guid? ApplicationId { get; init; }
}

/// <summary>The signed-in user asks for an application.</summary>
public sealed class SubmitAccessRequest
{
    [Required(ErrorMessage = "validation.application.required")]
    public Guid ApplicationId { get; init; }

    /// <summary>Why they need it, for the reviewer.</summary>
    [StringLength(500, ErrorMessage = "validation.reason.max")]
    public string? Reason { get; init; }
}

/// <summary>A reviewer's decision.</summary>
/// <param name="Roles">On approval, the role keys to grant (none when absent).</param>
/// <param name="Note">A message for the requester.</param>
public sealed record ReviewAccessRequest(IReadOnlyList<string>? Roles, string? Note);

// ---------------------------------------------------------------------------
// Application API: what an application's backend reads and changes with a token of its own.
// ---------------------------------------------------------------------------

/// <summary>The calling application, as it is configured in the portal.</summary>
/// <param name="Status">Always <c>active</c> for a caller that got this far.</param>
/// <param name="Roles">The roles the application defines, to assign on grants.</param>
/// <param name="GroupClaims"><c>none</c>, <c>granted</c> or <c>all</c>: which groups the user listings include.</param>
/// <param name="ApiScopes">The application API scopes currently enabled for it.</param>
public sealed record ApplicationSelfDto(
    string ClientId,
    string DisplayName,
    string? Description,
    string BaseUrl,
    string Status,
    IReadOnlyList<ApplicationRoleDto> Roles,
    string GroupClaims,
    IReadOnlyList<string> ApiScopes);

/// <summary>A user who can open the calling application.</summary>
/// <param name="Direct">Granted to the user personally (only such a grant can be changed through the API).</param>
/// <param name="Roles">The user's role keys in the application, from every grant together.</param>
/// <param name="Groups">The user's groups, as the application's groups claim setting allows (empty with <c>none</c>).</param>
public sealed record ApplicationUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool Direct,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Groups);

/// <summary>Filter and paging for the users of the calling application.</summary>
public sealed class ApplicationUserListQuery
{
    public const int MaxPageSize = 200;

    [Range(1, int.MaxValue, ErrorMessage = "validation.page.min")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "validation.pageSize.range")]
    public int PageSize { get; init; } = 50;

    /// <summary>Free text matched against email and name.</summary>
    [StringLength(256, ErrorMessage = "validation.search.max")]
    public string? Search { get; init; }
}

/// <summary>One page of the calling application's users, by name.</summary>
public sealed record ApplicationUserPageDto(
    IReadOnlyList<ApplicationUserDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>The outcome of a grant made through the application API.</summary>
/// <param name="Changed">False when the user already held the grant with the same roles.</param>
public sealed record ApplicationGrantResultDto(ApplicationUserDto User, bool Changed);

/// <summary>The outcome of withdrawing a personal grant through the application API.</summary>
/// <param name="Changed">False when the user held no personal grant.</param>
/// <param name="StillHasAccess">True when a group still grants the application to the user.</param>
public sealed record ApplicationRevokeResultDto(bool Changed, bool StillHasAccess);

/// <summary>The answer to "may this client have a token of its own".</summary>
/// <param name="Allowed">True only for an active application with at least one API scope.</param>
/// <param name="ApplicationName">Display name for the audit log; null for an unknown client.</param>
/// <param name="ApiScopes">The application API scopes enabled for it (empty when refused).</param>
public sealed record ApplicationTokenDecision(bool Allowed, string? ApplicationName, IReadOnlyList<string> ApiScopes);

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
/// <param name="EmailConfirmed">Whether the account proved it controls its address (what joining by email domain relies on).</param>
public sealed record DirectoryUser(Guid Id, string Email, string DisplayName, bool EmailConfirmed = false);

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

    /// <summary>
    /// The organization the user signs in for (<c>org_id</c>, <c>org_slug</c>, <c>org_name</c>, <c>org_role</c>), when
    /// the application works per organization.
    /// </summary>
    public const string Organization = "organization";
}

// ---------------------------------------------------------------------------
// Access reviews.
// ---------------------------------------------------------------------------

/// <param name="Status"><c>open</c>, <c>completed</c> or <c>cancelled</c>.</param>
public sealed record AccessReviewSummaryDto(
    Guid Id,
    string Name,
    Guid ApplicationId,
    string ApplicationName,
    string Status,
    int ItemCount,
    int DecidedCount,
    int RevokeCount,
    bool RevokeUndecided,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? ClosedAtUtc);

/// <param name="SubjectType"><c>user</c> or <c>group</c>.</param>
/// <param name="Decision"><c>pending</c>, <c>keep</c> or <c>revoke</c>.</param>
public sealed record AccessReviewItemDto(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    string SubjectLabel,
    IReadOnlyList<string> Roles,
    string Decision,
    string? Note,
    DateTimeOffset? DecidedAtUtc);

public sealed record AccessReviewDto(AccessReviewSummaryDto Review, IReadOnlyList<AccessReviewItemDto> Items);

/// <param name="RevokeUndecided">Completing the review also revokes what nobody confirmed.</param>
public sealed record StartAccessReviewRequest(Guid ApplicationId, string? Name, DateTimeOffset? DueAtUtc, bool RevokeUndecided = false);

/// <param name="Decision"><c>keep</c>, <c>revoke</c> or <c>pending</c> (to undo).</param>
public sealed record DecideAccessReviewItemRequest(string Decision, string? Note = null);
