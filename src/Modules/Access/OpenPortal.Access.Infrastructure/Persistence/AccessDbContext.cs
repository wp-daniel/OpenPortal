using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Grants;
using OpenPortal.Access.Domain.Groups;

namespace OpenPortal.Access.Infrastructure.Persistence;

/// <summary>
/// Persistence boundary for the Access module: applications, groups, grants, and the OpenIddict tables
/// (clients, authorizations, tokens, scopes).
/// <para>
/// OpenIddict shares this context so that registering a client and recording the matching
/// <see cref="PortalApplication"/> commit to the same database. No provider is referenced here; the host
/// chooses it through <c>AddAccessModule</c>.
/// </para>
/// </summary>
public sealed class AccessDbContext : DbContext
{
    public AccessDbContext(DbContextOptions<AccessDbContext> options)
        : base(options)
    {
    }

    public DbSet<PortalApplication> Applications => Set<PortalApplication>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    public DbSet<ApplicationUserGrant> UserGrants => Set<ApplicationUserGrant>();

    public DbSet<ApplicationGroupGrant> GroupGrants => Set<ApplicationGroupGrant>();

    public DbSet<PageGroupGrant> PageGrants => Set<PageGroupGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);

        // Default OpenIddict entities keyed by Guid, matching the rest of the schema.
        modelBuilder.UseOpenIddict<Guid>();

        SqliteDateTimeOffsetCompatibility.ApplyIfSqlite(modelBuilder, Database.ProviderName);

        base.OnModelCreating(modelBuilder);
    }
}
