using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Content.Domain.Profiles;

namespace OpenPortal.Content.Infrastructure.Persistence;

internal sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Profiles");

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.DisplayName)
            .IsRequired()
            .HasMaxLength(Profile.DisplayNameMaxLength);

        builder.Property(profile => profile.Headline)
            .HasMaxLength(Profile.HeadlineMaxLength);

        // Bounded to SummaryMaxLength characters by the domain; the column is sized to hold it on
        // every provider. SQLite ignores length, PostgreSQL enforces it as a check.
        builder.Property(profile => profile.Summary)
            .HasMaxLength(Profile.SummaryMaxLength);

        builder.Property(profile => profile.Location)
            .HasMaxLength(Profile.LocationMaxLength);

        builder.Property(profile => profile.Email)
            .HasMaxLength(Profile.EmailMaxLength);

        builder.Property(profile => profile.AvatarUrl)
            .HasMaxLength(Profile.AvatarUrlMaxLength);

        builder.Property(profile => profile.CreatedAtUtc)
            .IsRequired();

        builder.Property(profile => profile.UpdatedAtUtc)
            .IsRequired();

        builder.HasMany(profile => profile.SocialLinks)
            .WithOne(link => link.Profile)
            .HasForeignKey(link => link.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Profile.SocialLinks))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // The portal renders one profile, so the list query almost always has no ordering to apply.
        builder.HasIndex(profile => profile.DisplayName);
    }
}

internal sealed class SocialLinkConfiguration : IEntityTypeConfiguration<SocialLink>
{
    public void Configure(EntityTypeBuilder<SocialLink> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SocialLinks");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.Platform)
            .IsRequired()
            .HasMaxLength(SocialLink.PlatformMaxLength);

        builder.Property(link => link.Url)
            .IsRequired()
            .HasMaxLength(SocialLink.UrlMaxLength);

        builder.Property(link => link.Label)
            .HasMaxLength(SocialLink.LabelMaxLength);

        builder.Property(link => link.Position)
            .IsRequired();

        builder.Property(link => link.CreatedAtUtc)
            .IsRequired();

        // Position is only meaningful within a profile, so uniqueness is scoped to the parent.
        builder.HasIndex(link => new { link.ProfileId, link.Position })
            .IsUnique()
            .HasDatabaseName("IX_SocialLinks_ProfileId_Position");

        // Uniqueness of the platform per profile keeps a duplicated entry from being added twice.
        builder.HasIndex(link => new { link.ProfileId, link.Platform })
            .IsUnique()
            .HasDatabaseName("IX_SocialLinks_ProfileId_Platform");
    }
}