namespace OpenPortal.Content.Application.Contracts;

/// <summary>
/// A project as the management screens need it.
/// <para>
/// Deliberately not <see cref="ProjectDto"/>: that projection is the public shape, addressed by slug and
/// carrying only published, reader-facing fields. The editor needs the row id, the published flag and the
/// display position, none of which belong in a public payload. Merging the two would mean publishing either
/// internal identifiers or editing controls to anonymous readers.
/// </para>
/// </summary>
/// <param name="Id">Row identity. Required by every mutation that addresses an existing project.</param>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Summary">One-line description.</param>
/// <param name="Description">Long-form description.</param>
/// <param name="Url">Link to the running project.</param>
/// <param name="RepositoryUrl">Link to the source repository.</param>
/// <param name="Position">Explicit display order, or null when unpositioned.</param>
/// <param name="IsPublished">Whether the project currently appears on the public site.</param>
/// <param name="StartedOn">Start date.</param>
/// <param name="CompletedOn">Completion date, or null while ongoing.</param>
/// <param name="UpdatedAtUtc">Last modification timestamp, used to warn about concurrent edits.</param>
/// <param name="Technologies">Associated technologies, by display name.</param>
public sealed record ManagedProjectDto(
    Guid Id,
    string Slug,
    string Name,
    string? Summary,
    string? Description,
    string? Url,
    string? RepositoryUrl,
    int? Position,
    bool IsPublished,
    DateOnly? StartedOn,
    DateOnly? CompletedOn,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<TechnologyDto> Technologies);