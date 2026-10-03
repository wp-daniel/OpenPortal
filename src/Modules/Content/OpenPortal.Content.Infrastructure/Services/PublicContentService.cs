using Microsoft.EntityFrameworkCore;
using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Content.Application.Contracts;
using OpenPortal.Content.Domain;
using OpenPortal.Content.Domain.Profiles;
using OpenPortal.Content.Domain.Projects;
using OpenPortal.Content.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Content.Infrastructure.Services;

/// <summary>
/// Read side of the Content module. Every query filters unpublished rows here rather than accepting a flag
/// from the caller, so a published-only guarantee holds regardless of who asks.
/// </summary>
internal sealed class PublicContentService : IPublicContentService
{
    private readonly ContentDbContext _dbContext;

    public PublicContentService(ContentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PublicContentDto>> GetPublicContentAsync(CancellationToken cancellationToken)
    {
        // The portal shows exactly one profile, so ordering by key is deterministic even though the schema
        // does not yet enforce a singleton.
        var profile = await _dbContext.Profiles
            .AsNoTracking()
            .Include(entity => entity.SocialLinks)
            .OrderBy(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var projects = await QueryPublishedProjectsAsync(cancellationToken).ConfigureAwait(false);

        return Result<PublicContentDto>.Success(new PublicContentDto(
            profile is null ? null : ToProfileDto(profile, projects),
            projects.Select(ToProjectDto).ToArray()));
    }

    public async Task<Result<ProjectDto>> GetProjectAsync(string? slug, CancellationToken cancellationToken)
    {
        // An unparseable slug cannot match any row, so it is reported as "not found" rather than as a
        // validation error: from the caller's point of view the resource simply does not exist.
        if (!TextRules.TryNormaliseSlug(slug, out var normalisedSlug))
        {
            return Result<ProjectDto>.Failure(ContentErrors.ProjectNotFound);
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .Include(entity => entity.Technologies)
                .ThenInclude(link => link.Technology)
            .Where(entity => entity.IsPublished && entity.Slug == normalisedSlug)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return project is null
            ? Result<ProjectDto>.Failure(ContentErrors.ProjectNotFound)
            : Result<ProjectDto>.Success(ToProjectDto(project));
    }

    private Task<List<Project>> QueryPublishedProjectsAsync(CancellationToken cancellationToken) =>
        _dbContext.Projects
            .AsNoTracking()
            .WithTechnologies()
            .Where(entity => entity.IsPublished)
            .ApplyDisplayOrder()
            .ThenByDescending(entity => entity.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    private static ProfileDto ToProfileDto(Profile profile, IReadOnlyList<Project> publishedProjects) => new(
        profile.DisplayName,
        profile.Headline,
        profile.Summary,
        profile.Location,
        profile.Email,
        profile.AvatarUrl,
        profile.SocialLinks
            .OrderBy(link => link.Position)
            .ThenBy(link => link.Platform, StringComparer.Ordinal)
            .Select(link => new SocialLinkDto(link.Platform, link.Url, link.Label))
            .ToArray(),
        publishedProjects.Select(ToProjectDto).ToArray());

    internal static ProjectDto ToProjectDto(Project project) => new(
        project.Slug,
        project.Name,
        project.Summary,
        project.Description,
        project.Url,
        project.RepositoryUrl,
        project.StartedOn,
        project.CompletedOn,
        project.Technologies
            .Select(link => link.Technology?.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => new TechnologyDto(name!))
            .OrderBy(technology => technology.Name, StringComparer.Ordinal)
            .ToArray());
}