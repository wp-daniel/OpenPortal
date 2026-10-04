using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace OpenPortal.Web.Geo;

public static class GeoSetup
{
    /// <summary>Registers <see cref="GeoLookup"/> and one named HttpClient per provider.</summary>
    public static IServiceCollection AddOpenPortalGeo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GeoOptions>().Bind(configuration.GetSection(GeoOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<GeoLookup>();

        services.AddHttpClient(GeoLookup.PostalCodeClient, (provider, client) =>
            Configure(client, provider.GetRequiredService<IOptions<GeoOptions>>().Value.PostalCodeBaseUrl));

        services.AddHttpClient(GeoLookup.PlacesClient, (provider, client) =>
            Configure(client, provider.GetRequiredService<IOptions<GeoOptions>>().Value.PlacesBaseUrl));

        return services;
    }

    private static void Configure(HttpClient client, Uri baseAddress)
    {
        client.BaseAddress = baseAddress;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Free public services ask callers to identify themselves (Photon's fair-use policy among them).
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenPortal", "1.0"));
    }
}
