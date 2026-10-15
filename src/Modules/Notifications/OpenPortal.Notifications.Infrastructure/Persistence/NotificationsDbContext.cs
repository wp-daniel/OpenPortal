using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Notifications.Domain;

namespace OpenPortal.Notifications.Infrastructure.Persistence;

/// <summary>
/// Persistence boundary for the Notifications module: the notifications and the announcements. No provider is referenced here; the host
/// chooses it through <c>AddNotificationsModule</c>.
/// </summary>
public sealed class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

        SqliteDateTimeOffsetCompatibility.ApplyIfSqlite(modelBuilder, Database.ProviderName);

        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Type).IsRequired().HasMaxLength(Notification.TypeMaxLength);
        builder.Property(notification => notification.Parameters).HasMaxLength(Notification.ParametersMaxLength);
        builder.Property(notification => notification.Link).HasMaxLength(Notification.LinkMaxLength);

        builder.Ignore(notification => notification.IsRead);

        // Every read is "this user's notifications, newest first"; the bell counts the unread ones.
        builder.HasIndex(notification => new { notification.RecipientId, notification.CreatedAtUtc })
            .HasDatabaseName("IX_Notifications_RecipientId_CreatedAtUtc");

        builder.HasIndex(notification => new { notification.RecipientId, notification.ReadAtUtc })
            .HasDatabaseName("IX_Notifications_RecipientId_ReadAtUtc");

        // The retention sweep deletes by age.
        builder.HasIndex(notification => notification.CreatedAtUtc)
            .HasDatabaseName("IX_Notifications_CreatedAtUtc");
    }
}


internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Announcements");

        builder.HasKey(announcement => announcement.Id);

        builder.Property(announcement => announcement.Severity).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(announcement => announcement.Audience).IsRequired().HasConversion<string>().HasMaxLength(16);

        builder.OwnsMany(announcement => announcement.Texts, texts =>
        {
            texts.ToTable("AnnouncementTexts");
            texts.WithOwner().HasForeignKey("AnnouncementId");
            texts.HasKey("AnnouncementId", nameof(AnnouncementText.Language));
            texts.Property(text => text.Language).HasMaxLength(AnnouncementText.LanguageMaxLength);
            texts.Property(text => text.Title).IsRequired().HasMaxLength(AnnouncementText.TitleMaxLength);
            texts.Property(text => text.Body).HasMaxLength(AnnouncementText.BodyMaxLength);
        });

        builder.Navigation(announcement => announcement.Texts).HasField("_texts").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
