using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Notifications.Domain;

namespace OpenPortal.Notifications.Infrastructure.Persistence;

/// <summary>
/// Persistence boundary for the Notifications module: the notifications, the announcements, the delivery queue and
/// the email settings. No provider is referenced here; the host chooses it through <c>AddNotificationsModule</c>.
/// </summary>
public sealed class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<MailSettings> MailSettings => Set<MailSettings>();

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

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OutboxMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Type).IsRequired().HasMaxLength(OutboxMessage.TypeMaxLength);
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.Description).HasMaxLength(OutboxMessage.DescriptionMaxLength);
        builder.Property(message => message.Status).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(message => message.LockedBy).HasMaxLength(OutboxMessage.OwnerMaxLength);
        builder.Property(message => message.LastError).HasMaxLength(OutboxMessage.ErrorMaxLength);

        // The dispatcher asks for "pending and due", oldest first.
        builder.HasIndex(message => new { message.Status, message.AvailableAtUtc })
            .HasDatabaseName("IX_OutboxMessages_Status_AvailableAtUtc");

        // The retention sweep deletes delivered messages by age.
        builder.HasIndex(message => message.CreatedAtUtc)
            .HasDatabaseName("IX_OutboxMessages_CreatedAtUtc");
    }
}

internal sealed class MailSettingsConfiguration : IEntityTypeConfiguration<MailSettings>
{
    public void Configure(EntityTypeBuilder<MailSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MailSettings");

        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();

        builder.Property(settings => settings.Host).HasMaxLength(MailSettings.HostMaxLength);
        builder.Property(settings => settings.Security).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(settings => settings.Username).HasMaxLength(MailSettings.UsernameMaxLength);
        builder.Property(settings => settings.ProtectedPassword).HasMaxLength(MailSettings.ProtectedPasswordMaxLength);
        builder.Property(settings => settings.FromAddress).HasMaxLength(MailSettings.AddressMaxLength);
        builder.Property(settings => settings.FromName).HasMaxLength(MailSettings.FromNameMaxLength);

        builder.Ignore(settings => settings.IsConfigured);
    }
}
