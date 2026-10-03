using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core configuration for <see cref="ApplicationUser"/>. Contains only relational mappings that are
/// valid on every supported provider.
/// </summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Users");

        builder.Property(user => user.DisplayName)
            .HasMaxLength(ApplicationUser.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(user => user.Language)
            .HasMaxLength(ApplicationUser.LanguageMaxLength);

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        // Names are set explicitly so the schema does not change if Identity's default conventions do.
        builder.HasIndex(user => user.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("IX_Users_NormalizedUserName");

        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("IX_Users_NormalizedEmail");

        builder.HasIndex(user => user.DisplayName)
            .HasDatabaseName("IX_Users_DisplayName");
    }
}