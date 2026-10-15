using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Identity.Domain.Legal;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Infrastructure.Persistence;

internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("LegalDocuments");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Kind).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(document => document.Status).IsRequired().HasConversion<string>().HasMaxLength(16);

        // One row per version of each kind.
        builder.HasIndex(document => new { document.Kind, document.Version }).IsUnique();

        builder.OwnsMany(document => document.Texts, texts =>
        {
            texts.ToTable("LegalDocumentTexts");
            texts.WithOwner().HasForeignKey("DocumentId");
            texts.HasKey("DocumentId", nameof(LegalDocumentText.Language));
            texts.Property(text => text.Language).HasMaxLength(LegalDocumentText.LanguageMaxLength);
            texts.Property(text => text.Title).IsRequired().HasMaxLength(LegalDocumentText.TitleMaxLength);
            texts.Property(text => text.Body).IsRequired().HasMaxLength(LegalDocumentText.BodyMaxLength);
        });

        builder.Navigation(document => document.Texts).HasField("_texts").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LegalAcceptanceConfiguration : IEntityTypeConfiguration<LegalAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalAcceptance> builder)
    {
        builder.ToTable("LegalAcceptances");

        builder.HasKey(acceptance => new { acceptance.UserId, acceptance.DocumentId });

        // An acceptance goes with its account; a version that was accepted is never deleted (only drafts are).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(acceptance => acceptance.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LegalDocument>().WithMany().HasForeignKey(acceptance => acceptance.DocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(acceptance => acceptance.DocumentId);
    }
}
