using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Audit.Infrastructure.Configuration;
using OpenPortal.Audit.Infrastructure.Persistence;
using OpenPortal.Audit.Infrastructure.Services;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Audit.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Audit module: the <see cref="IAuditTrail"/> every module records with, the log reader and
/// the retention sweep.
/// <para>
/// The host supplies <see cref="IAuditRequestContext"/> (who is calling, from where) and
/// <see cref="IAuditLogAuthorization"/> (who may read the log).
/// </para>
/// </summary>
public static class AuditModuleServiceCollectionExtensions
{
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Supplies the <c>Audit</c> section.</param>
    /// <param name="configureContext">Callback that configures the context against the provider the host selected.</param>
    public static IServiceCollection AddAuditModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder> configureContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureContext);

        services.AddOptions<AuditModuleOptions>()
            .Bind(configuration.GetSection(AuditModuleOptions.SectionName));

        services.AddDbContext<AuditDbContext>(configureContext);

        services.TryAddSingleton<IClock, SystemClock>();

        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddSingleton<AuditRetentionService>();
        services.AddHostedService(provider => provider.GetRequiredService<AuditRetentionService>());

        return services;
    }
}
