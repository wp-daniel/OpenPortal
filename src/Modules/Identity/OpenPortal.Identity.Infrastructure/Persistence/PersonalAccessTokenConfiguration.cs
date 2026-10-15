using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Identity.Domain.Tokens;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Infrastructure.Persistence;

internal sealed class PersonalAccessTokenConfiguration : IEntityTypeConfiguration<PersonalAccessToken>
{
    public void Configure(EntityTypeBuilder<PersonalAccessToken> builder)
    {
        builder.ToTable("PersonalAccessTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Name).IsRequired().HasMaxLength(PersonalAccessToken.NameMaxLength);
        builder.Property(token => token.DisplayPrefix).IsRequired().HasMaxLength(PersonalAccessToken.DisplayPrefixLength);
        builder.Property(token => token.TokenHash).IsRequired().HasMaxLength(PersonalAccessToken.HashLength);
        builder.Property(token => token.Scopes).AsStringList();

        // Every API call looks its token up by hash.
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.UserId);

        // The tokens go with their account.
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
