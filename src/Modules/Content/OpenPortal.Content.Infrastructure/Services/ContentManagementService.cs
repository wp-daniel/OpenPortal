using Microsoft.EntityFrameworkCore;
using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Content.Application.Contracts;
using OpenPortal.Content.Domain;
using OpenPortal.Content.Domain.Profiles;
using OpenPortal.Content.Domain.Projects;
using OpenPortal.Content.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Content.Infrastructure.Services;

/// <summary>
/// Write side of the Content module.
/// <para>
/// Authorisation is re-checked through <see cref="IContentEditAuthorization"/> rather than trusted from the
/// caller, so the guarantee holds even if a future pipeline reaches this service without the host's
/// authorization filter.
/// </para>
/// </summary>
internal sealed class ContentManagementService : IContentManagementService
{
    private readonly ContentDbContext _dbContext;
    private readonly IContentEditAuthorization _authorization;
    private readonly IClock _clock;

    public ContentManagementService(
        ContentDbContext dbContext,
        IContentEditAuthorization authorization,
        IClock clock)
    {
        _dbContext = dbContext;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<ProfileDto>> GetProfileAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanEditAsync(ContentArea.Profile, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ProfileDto>.Failure(guard.Error);
        }

        var profile = await LoadProfileAsync(cancellationToken).ConfigureAwait(false);

        return profile is null
            ? Result<ProfileDto>.Failure(ContentErrors.ProfileNotFound)
            : Result<ProfileDto>.Success(ToProfileDto(profile));
    }

    public async Task<Result<ProfileDto>> SaveProfileAsync(
        ProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanEditAsync(ContentArea.Profile, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ProfileDto>.Failure(guard.Error);
        }

        var now = _clock.UtcNow;
        var drafts = ToDrafts(request.SocialLinks);

        // Everything is validated before the change tracker is touched, so a rejected edit cannot leave a
        // partially applied profile behind.
        var links = Profile.ValidateSocialLinks(drafts);
        if (links.IsFailure)
        {
            return Result<ProfileDto>.Failure(links.Error);
        }

        var profile = await LoadProfileAsync(cancellationToken).ConfigureAwait(false);

        if (profile is null)
        {
            // Validated before construction, not after: the constructor can only throw, and an exception
            // escaping this service becomes a 500 for what is plainly a bad request.
            var newName = Profile.ValidateDisplayName(request.DisplayName);
            if (newName.IsFailure)
            {
                return Result<ProfileDto>.Failure(newName.Error);
            }

            profile = CreateProfile(newName.Value, now);
        }

        var updated = profile.Update(
            request.DisplayName,
            request.Headline,
            request.Summary,
            request.Location,
            request.Email,
            request.AvatarUrl,
            now);

        if (updated.IsFailure)
        {
            return Result<ProfileDto>.Failure(updated.Error);
        }

        var replaced = profile.ReplaceSocialLinks(drafts, now);
        if (replaced.IsFailure)
        {
            return Result<ProfileDto>.Failure(replaced.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ProfileDto>.Success(ToProfileDto(profile));
    }

    public async Task<Result<IReadOnlyList<ManagedProjectDto>>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanEditAsync(ContentArea.Projects, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<ManagedProjectDto>>.Failure(guard.Error);
        }

        var projects = await _dbContext.Projects
            .AsNoTracking()
            .WithTechnologies()
            .ApplyDisplayOrder()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<ManagedProjectDto>>.Success(
            projects.Select(ToManagedProjectDto).ToArray());
    }

    public async Task<Result<ManagedProjectDto>> SaveProjectAsync(
        ProjectRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanEditAsync(ContentArea.Projects, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ManagedProjectDto>.Failure(guard.Error);
        }

        if (!TextRules.TryNormaliseSlug(request.Slug, out var normalisedSlug))
        {
            var slugError = TextRules.Normalise(request.Slug) is null
                ? ContentErrors.SlugRequired
                : ContentErrors.SlugInvalid;

            return Result<ManagedProjectDto>.Failure(slugError);
        }

        var names = Project.NormaliseTechnologyNames(request.Technologies ?? []);
        if (names.IsFailure)
        {
            return Result<ManagedProjectDto>.Failure(names.Error);
        }

        var now = _clock.UtcNow;
        var project = await LoadProjectAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (request.Id is not null && project is null)
        {
            return Result<ManagedProjectDto>.Failure(ContentErrors.ProjectNotFound);
        }

        // The database enforces slug uniqueness, but a conflict deserves a stable, translatable code rather
        // than a provider-specific exception surfacing as a 500.
        if (await IsSlugTakenAsync(normalisedSlug, project?.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result<ManagedProjectDto>.Failure(ContentErrors.SlugAlreadyInUse);
        }

        if (project is null)
        {
            // Same reason as in SaveProfileAsync: an invalid name or slug on creation must be a 400, not the
            // 500 that the constructor's ArgumentException would produce.
            var newName = Project.ValidateName(request.Name);
            if (newName.IsFailure)
            {
                return Result<ManagedProjectDto>.Failure(newName.Error);
            }

            var newSlug = Project.ValidateSlug(normalisedSlug);
            if (newSlug.IsFailure)
            {
                return Result<ManagedProjectDto>.Failure(newSlug.Error);
            }

            project = CreateProject(newName.Value, newSlug.Value, now);
        }

        var updated = project.Update(
            request.Name,
            normalisedSlug,
            request.Summary,
            request.Description,
            request.Url,
            request.RepositoryUrl,
            request.Position,
            request.IsPublished,
            request.StartedOn,
            request.CompletedOn,
            now);

        if (updated.IsFailure)
        {
            return Result<ManagedProjectDto>.Failure(updated.Error);
        }

        var technologies = await ResolveTechnologiesAsync(names.Value, cancellationToken).ConfigureAwait(false);
        var associated = project.ReplaceTechnologies(technologies);
        if (associated.IsFailure)
        {
            return Result<ManagedProjectDto>.Failure(associated.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ManagedProjectDto>.Success(ToManagedProjectDto(project));
    }

    public async Task<Result<ManagedProjectDto>> SetProjectPublishedAsync(
        Guid projectId,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanEditAsync(ContentArea.Projects, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ManagedProjectDto>.Failure(guard.Error);
        }

        var project = await LoadProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return Result<ManagedProjectDto>.Failure(ContentErrors.ProjectNotFound);
        }

        project.SetPublished(isPublished, _clock.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ManagedProjectDto>.Success(ToManagedProjectDto(project));
    }

    public async Task<Result> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanEditAsync(ContentArea.Projects, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result.Failure(guard.Error);
        }

        var project = await _dbContext.Projects
            .WithTechnologies()
            .FirstOrDefaultAsync(entity => entity.Id == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (project is null)
        {
            return Result.Failure(ContentErrors.ProjectNotFound);
        }

        _dbContext.Projects.Remove(project);

        // Technology rows are shared across projects and are deliberately left in place: removing one could
        // break another project's association. An orphan is harmless and can be pruned by a separate step.
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private Task<Profile?> LoadProfileAsync(CancellationToken cancellationToken) =>
        _dbContext.Profiles
            .Include(entity => entity.SocialLinks)
            .OrderBy(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<Project?> LoadProjectAsync(Guid? projectId, CancellationToken cancellationToken) =>
        projectId is null
            ? Task.FromResult<Project?>(null)
            : _dbContext.Projects
                .WithTechnologies()
                .FirstOrDefaultAsync(entity => entity.Id == projectId.Value, cancellationToken);

    private Profile CreateProfile(string displayName, DateTimeOffset now)
    {
        var profile = new Profile(Guid.NewGuid(), displayName, now);
        _dbContext.Profiles.Add(profile);

        return profile;
    }

    private Project CreateProject(string name, string slug, DateTimeOffset now)
    {
        var project = new Project(Guid.NewGuid(), name, slug, now);
        _dbContext.Projects.Add(project);

        return project;
    }

    private Task<bool> IsSlugTakenAsync(string normalisedSlug, Guid? ownerId, CancellationToken cancellationToken) =>
        _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                entity => entity.Slug == normalisedSlug && entity.Id != (ownerId ?? Guid.Empty),
                cancellationToken);

    /// <summary>
    /// Maps requested names onto shared <see cref="Technology"/> rows, creating the ones that do not exist
    /// yet. Matching on the normalised name is what makes two projects share a single row. Runs inside the
    /// caller's unit of work, so the unique index on the normalised name is only at risk from a genuinely
    /// concurrent insert rather than from this code.
    /// </summary>
    private async Task<IReadOnlyList<Technology>> ResolveTechnologiesAsync(
        IReadOnlyList<string> names,
        CancellationToken cancellationToken)
    {
        if (names.Count == 0)
        {
            return [];
        }

        var normalised = names.Select(Technology.Normalise).ToArray();

        var resolved = new Dictionary<string, Technology>(StringComparer.Ordinal);

        var existing = await _dbContext.Technologies
            .Where(technology => normalised.Contains(technology.NormalisedName))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var technology in existing)
        {
            resolved[technology.NormalisedName] = technology;
        }

        foreach (var name in names)
        {
            var key = Technology.Normalise(name);
            if (resolved.ContainsKey(key))
            {
                continue;
            }

            var technology = new Technology(Guid.NewGuid(), name, key);
            _dbContext.Technologies.Add(technology);
            resolved[key] = technology;
        }

        return names.Select(name => resolved[Technology.Normalise(name)]).ToArray();
    }

    private static List<SocialLinkDraft> ToDrafts(IReadOnlyList<SocialLinkRequest>? links)
    {
        if (links is null || links.Count == 0)
        {
            return [];
        }

        var drafts = new List<SocialLinkDraft>(links.Count);
        foreach (var link in links)
        {
            drafts.Add(new SocialLinkDraft(link.Platform, link.Url, link.Label));
        }

        return drafts;
    }

    /// <summary>
    /// Projects the editor-facing view of a project.
    /// <para>
    /// Built from the tracked entity rather than by reusing the public mapper, because the editor needs the
    /// id, the published flag, the position and the concurrency-relevant timestamp that the public
    /// projection deliberately omits.
    /// </para>
    /// </summary>
    private static ManagedProjectDto ToManagedProjectDto(Project project) => new(
        project.Id,
        project.Slug,
        project.Name,
        project.Summary,
        project.Description,
        project.Url,
        project.RepositoryUrl,
        project.Position,
        project.IsPublished,
        project.StartedOn,
        project.CompletedOn,
        project.UpdatedAtUtc,
        project.Technologies
            .Select(link => link.Technology?.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => new TechnologyDto(name!))
            .OrderBy(technology => technology.Name, StringComparer.Ordinal)
            .ToArray());

    private static ProfileDto ToProfileDto(Profile profile) => new(
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
        []);
}