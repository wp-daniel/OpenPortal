using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Infrastructure.Persistence;

/// <summary>
/// Identity module's unit of work. Owns the Identity tables exclusively.
/// <para>
/// Table names are renamed from the <c>AspNet*</c> defaults so the schema reads like the domain, and so that
/// two modules can share one database without risking name collisions.
/// </para>
/// <para>
/// The model is provider-agnostic: no column types, no provider-specific annotations and no raw SQL.
/// Switching provider is a composition-root concern, not a change to this class.
/// </para>
/// </summary>
public sealed class IdentityDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAvatar> UserAvatars => Set<UserAvatar>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new UserAvatarConfiguration());

        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        SqliteDateTimeOffsetCompatibility.ApplyIfSqlite(builder, Database.ProviderName);
    }
}