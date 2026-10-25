using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Identity.Domain.Settings;

namespace OpenPortal.Identity.Infrastructure.Persistence;

/// <summary>One row per configured provider, keyed by its name (<c>google</c>, <c>microsoft</c>).</summary>
internal sealed class ExternalLoginProviderConfiguration : IEntityTypeConfiguration<ExternalLoginProvider>
{
    public void Configure(EntityTypeBuilder<ExternalLoginProvider> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ExternalLoginProviders");

        builder.HasKey(provider => provider.Key);
        builder.Property(provider => provider.Key).HasMaxLength(20).ValueGeneratedNever();

        builder.Property(provider => provider.ClientId).HasMaxLength(ExternalLoginProvider.ClientIdMaxLength);
        builder.Property(provider => provider.ProtectedClientSecret).HasMaxLength(ExternalLoginProvider.ProtectedSecretMaxLength);
        builder.Property(provider => provider.Tenant).HasMaxLength(ExternalLoginProvider.TenantMaxLength);

        builder.Property(provider => provider.SignUp)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(provider => provider.AllowedDomains).AsStringList();

        builder.Ignore(provider => provider.HasClientSecret);
        builder.Ignore(provider => provider.IsSingleTenant);
    }
}
