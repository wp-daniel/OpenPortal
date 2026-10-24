using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Identity.Domain.Settings;

namespace OpenPortal.Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core configuration for <see cref="SecuritySettings"/>: at most one row, keyed by
/// <see cref="SecuritySettings.SingletonId"/>. No row means the defaults.
/// </summary>
internal sealed class SecuritySettingsConfiguration : IEntityTypeConfiguration<SecuritySettings>
{
    public void Configure(EntityTypeBuilder<SecuritySettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SecuritySettings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();

        builder.Property(settings => settings.TwoFactorEnabled)
            .IsRequired();

        builder.Property(settings => settings.TwoFactorIssuer)
            .HasMaxLength(SecuritySettings.IssuerMaxLength)
            .IsRequired();

        builder.Property(settings => settings.TwoFactorRequirement)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
    }
}

/// <summary>
/// EF Core configuration for <see cref="PrivacySettings"/>: at most one row, keyed by
/// <see cref="PrivacySettings.SingletonId"/>. No row means the defaults.
/// </summary>
internal sealed class PrivacySettingsConfiguration : IEntityTypeConfiguration<PrivacySettings>
{
    public void Configure(EntityTypeBuilder<PrivacySettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PrivacySettings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();
    }
}
