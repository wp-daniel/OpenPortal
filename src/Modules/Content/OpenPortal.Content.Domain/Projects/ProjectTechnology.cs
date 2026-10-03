namespace OpenPortal.Content.Domain.Projects;

/// <summary>
/// Join row between a project and a technology.
/// <para>
/// The composite key (<see cref="ProjectId"/>, <see cref="TechnologyId"/>) makes a duplicate association
/// impossible at the database level, so the invariant does not depend on application code alone.
/// </para>
/// </summary>
public sealed class ProjectTechnology
{
    // Required by EF Core.
    private ProjectTechnology()
    {
    }

    internal ProjectTechnology(Guid projectId, Technology technology)
    {
        ProjectId = projectId;
        Technology = technology;
    }

    public Guid ProjectId { get; private set; }

    public Guid TechnologyId { get; private set; }

    public Project Project { get; private set; } = null!;

    /// <summary>
    /// The shared technology row. Resolved by the persistence layer before the association is created,
    /// because a <see cref="Technology"/> cannot exist until the owning project has an id, while the
    /// association must be prepared in the same unit of work.
    /// </summary>
    public Technology Technology { get; private set; } = null!;
}