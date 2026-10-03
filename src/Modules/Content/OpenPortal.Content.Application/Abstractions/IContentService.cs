using OpenPortal.Content.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Content.Application.Abstractions;

/// <summary>
/// Read side of the Content module. Every method is anonymous-safe: the implementation filters
/// <c>IsPublished</c> itself rather than trusting a caller-supplied filter, so a published-only guarantee
/// cannot be bypassed by passing a different flag.
/// </summary>
public interface IPublicContentService
{
    /// <summary>Fetches everything the public site renders in one payload.</summary>
    Task<Result<PublicContentDto>> GetPublicContentAsync(CancellationToken cancellationToken);

    /// <summary>Fetches a single published project by slug.</summary>
    Task<Result<ProjectDto>> GetProjectAsync(string slug, CancellationToken cancellationToken);
}

/// <summary>
/// Write side of the Content module. Callers are authorised by the host before these methods are reached;
/// the implementation still re-checks through <see cref="IContentEditAuthorization"/>, because an
/// abstraction reachable from more than one pipeline must not depend on each caller remembering to guard it.
/// </summary>
public interface IContentManagementService
{
    Task<Result<ProfileDto>> GetProfileAsync(CancellationToken cancellationToken);

    Task<Result<ProfileDto>> SaveProfileAsync(ProfileRequest request, CancellationToken cancellationToken);

    /// <summary>Lists every project, published or not, in display order.</summary>
    /// <remarks>
    /// Returns <see cref="ManagedProjectDto"/> rather than the public projection: the editor has to know a
    /// project's id and published state to act on it, and neither belongs in a public payload.
    /// </remarks>
    Task<Result<IReadOnlyList<ManagedProjectDto>>> ListProjectsAsync(CancellationToken cancellationToken);

    Task<Result<ManagedProjectDto>> SaveProjectAsync(ProjectRequest request, CancellationToken cancellationToken);

    /// <summary>Flips a project's published state without touching its other fields.</summary>
    Task<Result<ManagedProjectDto>> SetProjectPublishedAsync(
        Guid projectId,
        bool isPublished,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a project and its technology associations. Technology rows themselves are kept, because they
    /// are shared with other projects.
    /// </summary>
    Task<Result> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken);
}