using Microsoft.EntityFrameworkCore;
using OpenPortal.Content.Domain.Profiles;
using OpenPortal.Content.Domain.Projects;

namespace OpenPortal.Content.Infrastructure.Persistence;

/// <summary>
/// Persistence boundary for the Content module.
/// <para>
/// No provider is referenced here. The concrete database is chosen by the host through
/// <c>AddContentModule</c>, which registers this context against whichever provider the composition root
/// has selected.
/// </para>
/// </summary>
public sealed class ContentDbContext : DbContext
{
    public ContentDbContext(DbContextOptions<ContentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<SocialLink> SocialLinks => Set<SocialLink>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Technology> Technologies => Set<Technology>();

    public DbSet<ProjectTechnology> ProjectTechnologies => Set<ProjectTechnology>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);

        SqliteDateTimeOffsetCompatibility.ApplyIfSqlite(modelBuilder, Database.ProviderName);

        base.OnModelCreating(modelBuilder);
    }
}