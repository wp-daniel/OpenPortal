using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Content.Domain.Projects;

namespace OpenPortal.Content.Infrastructure.Persistence;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Name)
            .IsRequired()
            .HasMaxLength(Project.NameMaxLength);

        builder.Property(project => project.Slug)
            .IsRequired()
            .HasMaxLength(Project.SlugMaxLength);

        builder.Property(project => project.Summary)
            .HasMaxLength(Project.SummaryMaxLength);

        builder.Property(project => project.Description)
            .HasMaxLength(Project.DescriptionMaxLength);

        builder.Property(project => project.Url)
            .HasMaxLength(Project.UrlMaxLength);

        builder.Property(project => project.RepositoryUrl)
            .HasMaxLength(Project.RepositoryUrlMaxLength);

        builder.Property(project => project.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(project => project.CreatedAtUtc)
            .IsRequired();

        builder.Property(project => project.UpdatedAtUtc)
            .IsRequired();

        builder.HasMany(project => project.Technologies)
            .WithOne(link => link.Project)
            .HasForeignKey(link => link.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Project.Technologies))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Slug is a public identifier: it is looked up directly and must be unique case-insensitively.
        // The domain already lower-cases it, so a plain unique index is sufficient.
        builder.HasIndex(project => project.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Projects_Slug");

        // Supports the public listing, which filters on IsPublished and orders by Position.
        builder.HasIndex(project => new { project.IsPublished, project.Position })
            .HasDatabaseName("IX_Projects_IsPublished_Position");
    }
}

internal sealed class TechnologyConfiguration : IEntityTypeConfiguration<Technology>
{
    public void Configure(EntityTypeBuilder<Technology> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Technologies");

        builder.HasKey(technology => technology.Id);

        builder.Property(technology => technology.Name)
            .IsRequired()
            .HasMaxLength(Technology.NameMaxLength);

        builder.Property(technology => technology.NormalisedName)
            .IsRequired()
            .HasMaxLength(Technology.NameMaxLength);

        // Uniqueness on the upper-cased name makes ".NET" and ".net" resolve to a single shared row,
        // which is what lets two projects reference the same technology.
        builder.HasIndex(technology => technology.NormalisedName)
            .IsUnique()
            .HasDatabaseName("IX_Technologies_NormalisedName");
    }
}

internal sealed class ProjectTechnologyConfiguration : IEntityTypeConfiguration<ProjectTechnology>
{
    public void Configure(EntityTypeBuilder<ProjectTechnology> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProjectTechnologies");

        // Composite key: the same technology cannot be attached to one project twice, enforced by the
        // database rather than by application code alone.
        builder.HasKey(link => new { link.ProjectId, link.TechnologyId });

        builder.HasOne(link => link.Technology)
            .WithMany()
            .HasForeignKey(link => link.TechnologyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Technology rows are shared, so removing the last project that uses one must not fail.
        builder.HasIndex(link => link.TechnologyId)
            .HasDatabaseName("IX_ProjectTechnologies_TechnologyId");
    }
}