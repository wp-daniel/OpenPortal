using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;

namespace OpenPortal.Web.Localization;

public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the language catalog, resource-based localization and the request culture rules.
    /// <para>
    /// The culture comes from <c>?lang=</c> when present, otherwise from <c>Accept-Language</c>, which the
    /// client sets from the user's saved language. No cookie is involved, so changing language never needs a
    /// new sign-in or a new token.
    /// </para>
    /// </summary>
    public static IServiceCollection AddOpenPortalLocalization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<LanguageSettings>(configuration.GetSection(LanguageSettings.SectionName));
        services.AddSingleton<LanguageCatalog>();
        services.AddSingleton<ILanguageCatalog>(provider => provider.GetRequiredService<LanguageCatalog>());

        services.AddLocalization();

        services.AddOptions<RequestLocalizationOptions>()
            .Configure<LanguageCatalog>((options, catalog) =>
            {
                var cultures = catalog.Languages
                    .Select(language => CultureInfo.GetCultureInfo(language.Code))
                    .ToList();

                options.DefaultRequestCulture = new RequestCulture(catalog.DefaultLanguage);
                options.SupportedCultures = cultures;
                options.SupportedUICultures = cultures;
                options.FallBackToParentCultures = true;
                options.FallBackToParentUICultures = true;

                // Only the two providers this API documents: the default list also reads a cookie, which
                // would be a second, invisible source of truth.
                options.RequestCultureProviders =
                [
                    new QueryStringRequestCultureProvider { QueryStringKey = "lang", UIQueryStringKey = "lang" },
                    new AcceptLanguageHeaderRequestCultureProvider(),
                ];
            });

        return services;
    }
}
