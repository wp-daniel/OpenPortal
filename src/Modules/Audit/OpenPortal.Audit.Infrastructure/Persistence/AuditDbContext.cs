using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Audit.Domain;

namespace OpenPortal.Audit.Infrastructure.Persistence;

/// <summary>
/// Persistence boundary for the Audit module: one append-only table. No provider is referenced here; the
/// host chooses it through <c>AddAuditModule</c>.
/// </summary>
public sealed class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditEntry> Entries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);

        SqliteDateTimeOffsetCompatibility.ApplyIfSqlite(modelBuilder, Database.ProviderName);

        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Action).IsRequired().HasMaxLength(AuditEntry.ActionMaxLength);

        builder.Property(entry => entry.Outcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(entry => entry.ActorType).HasMaxLength(AuditEntry.SubjectTypeMaxLength);
        builder.Property(entry => entry.ActorId).HasMaxLength(AuditEntry.SubjectIdMaxLength);
        builder.Property(entry => entry.ActorLabel).HasMaxLength(AuditEntry.LabelMaxLength);
        builder.Property(entry => entry.TargetType).HasMaxLength(AuditEntry.SubjectTypeMaxLength);
        builder.Property(entry => entry.TargetId).HasMaxLength(AuditEntry.SubjectIdMaxLength);
        builder.Property(entry => entry.TargetLabel).HasMaxLength(AuditEntry.LabelMaxLength);
        builder.Property(entry => entry.Details).HasMaxLength(AuditEntry.DetailsMaxLength);
        builder.Property(entry => entry.IpAddress).HasMaxLength(AuditEntry.IpAddressMaxLength);
        builder.Property(entry => entry.UserAgent).HasMaxLength(AuditEntry.UserAgentMaxLength);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(AuditEntry.CorrelationIdMaxLength);

        // The log is read newest first, and the retention sweep deletes by age.
        builder.HasIndex(entry => entry.OccurredAtUtc).HasDatabaseName("IX_AuditEntries_OccurredAtUtc");

        // "Everything about this user" looks the id up on both sides.
        builder.HasIndex(entry => entry.ActorId).HasDatabaseName("IX_AuditEntries_ActorId");
        builder.HasIndex(entry => entry.TargetId).HasDatabaseName("IX_AuditEntries_TargetId");
    }
}
