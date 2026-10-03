namespace OpenPortal.Content.Application.Contracts;

/// <summary>Public projection of a technology attached to a project.</summary>
/// <param name="Name">Display form, preserving the casing the owner chose.</param>
public sealed record TechnologyDto(string Name);

/// <summary>Public projection of one social link.</summary>
/// <param name="Platform">Network name, for example "GitHub".</param>
/// <param name="Url">Absolute http or https link.</param>
/// <param name="Label">Anchor text override, or <see langword="null"/> to use the platform.</param>
public sealed record SocialLinkDto(string Platform, string Url, string? Label);

/// <summary>Public projection of a project.</summary>
/// <param name="Slug">URL-safe identifier used in links.</param>
/// <param name="Name">Display name.</param>
/// <param name="Summary">One-line description, when set.</param>
/// <param name="Description">Long-form description, when set.</param>
/// <param name="Url">Link to the running project, when set.</param>
/// <param name="RepositoryUrl">Link to the source repository, when set.</param>
/// <param name="StartedOn">Start date, when known.</param>
/// <param name="CompletedOn">Completion date, or <see langword="null"/> if ongoing.</param>
/// <param name="Technologies">Technologies associated with the project.</param>
public sealed record ProjectDto(
    string Slug,
    string Name,
    string? Summary,
    string? Description,
    string? Url,
    string? RepositoryUrl,
    DateOnly? StartedOn,
    DateOnly? CompletedOn,
    IReadOnlyList<TechnologyDto> Technologies);

/// <summary>Public projection of the portal's profile, including its published work.</summary>
/// <param name="DisplayName">Name shown as the page heading.</param>
/// <param name="Headline">Short positioning statement.</param>
/// <param name="Summary">Long-form biography.</param>
/// <param name="Location">Free-text location.</param>
/// <param name="Email">Public contact address. Never the sign-in address.</param>
/// <param name="AvatarUrl">Avatar image URL.</param>
/// <param name="SocialLinks">External links, in display order.</param>
/// <param name="Projects">Published projects, in display order.</param>
public sealed record ProfileDto(
    string DisplayName,
    string? Headline,
    string? Summary,
    string? Location,
    string? Email,
    string? AvatarUrl,
    IReadOnlyList<SocialLinkDto> SocialLinks,
    IReadOnlyList<ProjectDto> Projects);

/// <summary>
/// Everything the public site renders, fetched as a single payload so that the landing page needs one
/// round trip instead of one per section.
/// </summary>
/// <param name="Profile">The profile, or <see langword="null"/> when none has been created yet.</param>
/// <param name="Projects">Published projects, in display order.</param>
public sealed record PublicContentDto(ProfileDto? Profile, IReadOnlyList<ProjectDto> Projects);