using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Infrastructure.Configuration;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.Notifications.Infrastructure.Services;
using OpenPortal.SharedKernel.Messaging;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Privacy;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Notifications module: the <see cref="INotifier"/> every module notifies with, the signed-in
/// user's notification center, the announcements shown across the portal and the retention sweep.
/// <para>
/// The host supplies <see cref="INotificationContext"/> (who is calling, and who the roles of a notice's audience
/// name) and <see cref="IAnnouncementAuthorization"/> (who may manage announcements).
/// </para>
/// </summary>
public static class NotificationsModuleServiceCollectionExtensions
{
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Supplies the <c>Notifications</c> section.</param>
    /// <param name="configureContext">Callback that configures the context against the provider the host selected.</param>
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder> configureContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureContext);

        services.AddOptions<NotificationsModuleOptions>()
            .Bind(configuration.GetSection(NotificationsModuleOptions.SectionName));

        services.AddDbContext<NotificationsDbContext>(configureContext);

        services.TryAddSingleton<IClock, SystemClock>();

        // Nobody is told in real time unless the host supplies a publisher (it registers one before this module).
        services.TryAddSingleton<INotificationPublisher, NoNotificationPublisher>();
        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationMaintenance, NotificationMaintenanceService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IPersonalDataSource, NotificationPersonalData>();

        services.AddSingleton<NotificationRetentionService>();
        services.AddHostedService(provider => provider.GetRequiredService<NotificationRetentionService>());

        // The delivery queue: modules and the host queue work (IOutbox), the host registers an IOutboxHandler per
        // message type, and every instance runs the dispatcher.
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName));
        services.AddSingleton<OutboxSignal>();
        services.AddSingleton<OutboxProcessor>();
        services.AddSingleton<IOutbox, Outbox>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddScoped<IOutboxAdministration, OutboxAdministration>();
        services.AddScoped<IOutboxMonitor, OutboxMonitor>();

        // The portal's own SMTP server, edited by administrators; the password is kept encrypted.
        services.AddDataProtection();
        services.AddSingleton<MailPasswordProtector>();
        services.AddSingleton<MailSettingsCache>();
        services.AddScoped<IMailSettingsReader, MailSettingsReader>();
        services.AddScoped<IMailSettingsService, MailSettingsService>();

        return services;
    }
}

/// <summary>The default: notifications reach browsers at their next poll.</summary>
internal sealed class NoNotificationPublisher : INotificationPublisher
{
    public Task PublishAsync(IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) => Task.CompletedTask;
}
