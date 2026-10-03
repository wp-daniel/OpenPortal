using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Content.Domain.Projects;

/// <summary>
/// A body of work shown on the public portal. Projects are addressed by a stable slug rather than by id, so
/// links keep working when the underlying row is recreated.
/// </summary>
public sealed class Project
{
    public const int NameMaxLength = 160;
    public const int SlugMaxLength = 160;
    public const int SummaryMaxLength = 400;
    public const int DescriptionMaxLength = 8_000;
    public const int UrlMaxLength = 512;
    public const int RepositoryUrlMaxLength = 512;

    private readonly List<ProjectTechnology> _technologies = [];

    // Required by EF Core.
    private Project()
    {
    }

    public Project(Guid id, string name, string slug, DateTimeOffset createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);

        var validatedName = ValidateName(name);
        if (validatedName.IsFailure)
        {
            throw new ArgumentException(validatedName.Error.Description, nameof(name));
        }

        var validatedSlug = ValidateSlug(slug);
        if (validatedSlug.IsFailure)
        {
            throw new ArgumentException(validatedSlug.Error.Description, nameof(slug));
        }

        Id = id;
        Name = validatedName.Value;
        Slug = validatedSlug.Value;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>URL-safe identifier, unique across published and unpublished projects alike.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>One-line description used in cards and list views.</summary>
    public string? Summary { get; private set; }

    /// <summary>Long-form description. Plain text.</summary>
    public string? Description { get; private set; }

    /// <summary>Canonical link to the running project.</summary>
    public string? Url { get; private set; }

    /// <summary>Link to the source repository.</summary>
    public string? RepositoryUrl { get; private set; }

    /// <summary>
    /// Explicit display order. Kept nullable so that "no opinion" stays distinct from "position zero" when
    /// rows are ordered with a secondary key.
    /// </summary>
    public int? Position { get; private set; }

    public bool IsPublished { get; private set; }

    public DateOnly? StartedOn { get; private set; }

    public DateOnly? CompletedOn { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<ProjectTechnology> Technologies => _technologies;

    /// <summary>Applies a full field set atomically; the entity is untouched if any field is invalid.</summary>
    public Result Update(
        string? name,
        string? slug,
        string? summary,
        string? description,
        string? url,
        string? repositoryUrl,
        int? position,
        bool isPublished,
        DateOnly? startedOn,
        DateOnly? completedOn,
        DateTimeOffset now)
    {
        var validatedName = ValidateName(name);
        if (validatedName.IsFailure)
        {
            return Result.Failure(validatedName.Error);
        }

        var validatedSlug = ValidateSlug(slug);
        if (validatedSlug.IsFailure)
        {
            return Result.Failure(validatedSlug.Error);
        }

        var trimmedSummary = TextRules.Normalise(summary);
        if (trimmedSummary?.Length > SummaryMaxLength)
        {
            return Result.Failure(ContentErrors.ProjectSummaryTooLong);
        }

        var trimmedDescription = TextRules.Normalise(description);
        if (trimmedDescription?.Length > DescriptionMaxLength)
        {
            return Result.Failure(ContentErrors.DescriptionTooLong);
        }

        var trimmedUrl = TextRules.Normalise(url);
        if (trimmedUrl is not null
            && (trimmedUrl.Length > UrlMaxLength || !TextRules.IsHttpUrl(trimmedUrl)))
        {
            return Result.Failure(ContentErrors.UrlInvalid);
        }

        var trimmedRepository = TextRules.Normalise(repositoryUrl);
        if (trimmedRepository is not null
            && (trimmedRepository.Length > RepositoryUrlMaxLength || !TextRules.IsHttpUrl(trimmedRepository)))
        {
            return Result.Failure(ContentErrors.UrlInvalid);
        }

        if (startedOn is not null && completedOn is not null && completedOn < startedOn)
        {
            return Result.Failure(ContentErrors.CompletionBeforeStart);
        }

        Name = validatedName.Value;
        Slug = validatedSlug.Value;
        Summary = trimmedSummary;
        Description = trimmedDescription;
        Url = trimmedUrl;
        RepositoryUrl = trimmedRepository;
        Position = position;
        IsPublished = isPublished;
        StartedOn = startedOn;
        CompletedOn = completedOn;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    public void SetPublished(bool isPublished, DateTimeOffset now)
    {
        IsPublished = isPublished;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Replaces the technology set with the supplied, already-persisted (or about-to-be-created)
    /// <see cref="Technology"/> rows. Technologies are shared across projects, so resolution happens in the
    /// persistence layer: this method only validates the association and cannot create technologies itself.
    /// </summary>
    public Result ReplaceTechnologies(IEnumerable<Technology> technologies)
    {
        ArgumentNullException.ThrowIfNull(technologies);

        var materialised = technologies as IReadOnlyCollection<Technology> ?? technologies.ToArray();

        foreach (var technology in materialised)
        {
            if (technology is null)
            {
                throw new ArgumentException("Technology associations cannot be null.", nameof(technologies));
            }
        }

        // A technology cannot be listed twice for the same project, which the composite key would also
        // reject. Checking here produces a usable message instead of a database constraint violation.
        var distinct = new HashSet<Guid>();
        foreach (var technology in materialised)
        {
            if (!distinct.Add(technology.Id))
            {
                return Result.Failure(ContentErrors.DuplicateTechnology);
            }
        }

        _technologies.Clear();
        _technologies.AddRange(materialised.Select(technology => new ProjectTechnology(Id, technology)));

        return Result.Success();
    }

    /// <summary>
    /// Normalises the technology names requested by an editor: trims each entry, rejects blanks and
    /// over-long names, and removes case-insensitive duplicates while keeping the first spelling seen.
    /// </summary>
    public static Result<IReadOnlyList<string>> NormaliseTechnologyNames(IEnumerable<string>? names)
    {
        if (names is null)
        {
            return Result<IReadOnlyList<string>>.Failure(ContentErrors.TechnologyNameRequired);
        }

        var normalised = new List<string>();

        foreach (var name in names)
        {
            var trimmed = TextRules.Normalise(name);
            if (trimmed is null)
            {
                return Result<IReadOnlyList<string>>.Failure(ContentErrors.TechnologyNameRequired);
            }

            if (trimmed.Length > Technology.NameMaxLength)
            {
                return Result<IReadOnlyList<string>>.Failure(ContentErrors.TechnologyNameTooLong);
            }

            if (!normalised.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                normalised.Add(trimmed);
            }
        }

        return Result<IReadOnlyList<string>>.Success(normalised);
    }

    /// <summary>
    /// Validates a name without needing an instance, for callers that are about to create a project.
    /// </summary>
    public static Result<string> ValidateName(string? name)
    {
        var trimmed = TextRules.Normalise(name);

        if (trimmed is null)
        {
            return Result<string>.Failure(ContentErrors.ProjectNameRequired);
        }

        return trimmed.Length > NameMaxLength
            ? Result<string>.Failure(ContentErrors.ProjectNameTooLong)
            : Result<string>.Success(trimmed);
    }

    /// <summary>
    /// Validates and normalises a slug without needing an instance. Returns the normalised, lower-cased
    /// form so callers do not have to repeat the normalisation.
    /// </summary>
    public static Result<string> ValidateSlug(string? slug)
    {
        var trimmed = TextRules.Normalise(slug);
        if (trimmed is null)
        {
            return Result<string>.Failure(ContentErrors.SlugRequired);
        }

        if (trimmed.Length > SlugMaxLength)
        {
            return Result<string>.Failure(ContentErrors.SlugTooLong);
        }

        return TextRules.TryNormaliseSlug(trimmed, out var normalised)
            ? Result<string>.Success(normalised)
            : Result<string>.Failure(ContentErrors.SlugInvalid);
    }
}