namespace OpenPortal.Content.Application.Contracts;

/// <summary>Field set for creating or updating a profile. <c>Id</c> is ignored on create.</summary>
/// <param name="Id">Profile id when updating; ignored when creating.</param>
/// <param name="DisplayName">Name shown as the page heading. Required.</param>
/// <param name="Headline">Short positioning statement.</param>
/// <param name="Summary">Long-form biography.</param>
/// <param name="Location">Free-text location.</param>
/// <param name="Email">Public contact address.</param>
/// <param name="AvatarUrl">Avatar image URL.</param>
/// <param name="SocialLinks">Replacement link set. Submitting the full set keeps ordering unambiguous.</param>
public sealed record ProfileRequest(
    Guid? Id,
    string DisplayName,
    string? Headline,
    string? Summary,
    string? Location,
    string? Email,
    string? AvatarUrl,
    IReadOnlyList<SocialLinkRequest> SocialLinks);

/// <summary>One social link within a <see cref="ProfileRequest"/>.</summary>
public sealed record SocialLinkRequest(string Platform, string Url, string? Label);

/// <summary>Field set for creating or updating a project. <c>Id</c> is ignored on create.</summary>
/// <param name="Id">Project id when updating; ignored when creating.</param>
/// <param name="Name">Display name. Required.</param>
/// <param name="Slug">URL-safe identifier. Required.</param>
/// <param name="Summary">One-line description.</param>
/// <param name="Description">Long-form description.</param>
/// <param name="Url">Link to the running project.</param>
/// <param name="RepositoryUrl">Link to the source repository.</param>
/// <param name="Position">Explicit display order. When null the project sorts after explicitly positioned ones.</param>
/// <param name="IsPublished">Whether the project appears on the public site.</param>
/// <param name="StartedOn">Start date.</param>
/// <param name="CompletedOn">Completion date. Must not precede <paramref name="StartedOn"/>.</param>
/// <param name="Technologies">Replacement technology set, by display name.</param>
public sealed record ProjectRequest(
    Guid? Id,
    string Name,
    string Slug,
    string? Summary,
    string? Description,
    string? Url,
    string? RepositoryUrl,
    int? Position,
    bool IsPublished,
    DateOnly? StartedOn,
    DateOnly? CompletedOn,
    IReadOnlyList<string> Technologies);