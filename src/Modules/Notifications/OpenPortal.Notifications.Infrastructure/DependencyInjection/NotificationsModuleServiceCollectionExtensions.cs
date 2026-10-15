using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Infrastructure.Configuration;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.Notifications.Infrastructure.Services;
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

        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationMaintenance, NotificationMaintenanceService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IPersonalDataSource, NotificationPersonalData>();

        services.AddSingleton<NotificationRetentionService>();
        services.AddHostedService(provider => provider.GetRequiredService<NotificationRetentionService>());

        return services;
    }
}
