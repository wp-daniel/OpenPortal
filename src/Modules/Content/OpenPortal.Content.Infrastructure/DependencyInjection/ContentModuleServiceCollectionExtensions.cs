using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Content.Infrastructure.Persistence;
using OpenPortal.Content.Infrastructure.Services;

namespace OpenPortal.Content.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Content module.
/// <para>
/// The context is registered without a provider: the host supplies one, which is what confines the
/// SQLite-to-PostgreSQL switch to the composition root.
/// </para>
/// </summary>
public static class ContentModuleServiceCollectionExtensions
{
    /// <param name="services">The host's service collection.</param>
    /// <param name="configureContext">
    /// Callback that configures the context against the provider the host has selected, for example
    /// <c>options =&gt; options.UseSqlite(connectionString)</c>.
    /// </param>
    public static IServiceCollection AddContentModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureContext);

        services.AddDbContext<ContentDbContext>(configureContext);

        services.AddScoped<IPublicContentService, PublicContentService>();
        services.AddScoped<IContentManagementService, ContentManagementService>();

        return services;
    }
}