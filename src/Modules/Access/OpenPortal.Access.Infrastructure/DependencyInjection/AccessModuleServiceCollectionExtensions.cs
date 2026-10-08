using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Infrastructure.Configuration;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.Access.Infrastructure.Services;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Access module: applications, groups, grants and OpenIddict's client store.
/// <para>
/// The host still supplies <see cref="IAccessAdminAuthorization"/>, <see cref="IUserDirectory"/> and
/// <see cref="IPortalPageCatalog"/>, and
/// configures the OpenIddict <em>server</em> (endpoints, signing keys) itself, because those depend on its
/// authentication stack and its environment.
/// </para>
/// </summary>
public static class AccessModuleServiceCollectionExtensions
{
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Supplies the <c>Access</c> section.</param>
    /// <param name="configureContext">
    /// Callback that configures the context against the provider the host has selected.
    /// </param>
    public static IServiceCollection AddAccessModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder> configureContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureContext);

        services.AddOptions<AccessModuleOptions>()
            .Bind(configuration.GetSection(AccessModuleOptions.SectionName));

        services.AddDbContext<AccessDbContext>(configureContext);

        services.AddOpenIddict()
            .AddCore(options => options
                .UseEntityFrameworkCore()
                .UseDbContext<AccessDbContext>()
                .ReplaceDefaultEntities<Guid>());

        services.TryAddSingleton<IClock, SystemClock>();

        services.AddScoped<OidcClientRegistry>();
        services.AddScoped<AccessQueries>();
        services.AddScoped<IApplicationRegistryService, ApplicationRegistryService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IAccessAdministrationService, AccessAdministrationService>();
        services.AddScoped<IAccessEvaluator, AccessEvaluator>();
        services.AddScoped<IApplicationAnnouncementService, ApplicationAnnouncementService>();
        services.AddScoped<IPagePermissionService, PagePermissionService>();
        services.AddScoped<IPagePermissionEvaluator, PagePermissionEvaluator>();

        services.AddHostedService<OidcClientUpgradeService>();

        return services;
    }
}
