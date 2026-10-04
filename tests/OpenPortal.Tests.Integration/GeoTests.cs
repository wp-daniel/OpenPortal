using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Web.Geo;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Address suggestions. The external providers are replaced by a stub, so these tests pin down what the
/// portal does with their answers (and their outages) without depending on the internet.
/// </summary>
public sealed class GeoTests : IClassFixture<OpenPortalFactory>
{
    private readonly OpenPortalFactory _factory;

    public GeoTests(OpenPortalFactory factory) => _factory = factory;

    private sealed class StubProvider : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public Func<Uri, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);

            return Task.FromResult(Respond(request.RequestUri!));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private async Task<(ApiClient Client, StubProvider Stub)> SignedInWithStubAsync(string email)
    {
        var stub = new StubProvider();
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(GeoLookup.PostalCodeClient).ConfigurePrimaryHttpMessageHandler(() => stub);
            services.AddHttpClient(GeoLookup.PlacesClient).ConfigurePrimaryHttpMessageHandler(() => stub);
        }));

        var user = await _factory.CreateUserAsync(email, Passwords.Valid, "User");
        var client = ApiClient.For(host.CreateClient());
        await client.RefreshAntiforgeryTokenAsync();
        using var signIn = await client.SignInAsync(user.Email, user.Password);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (client, stub);
    }

    [Fact]
    public async Task A_postal_code_lists_every_place_it_covers_and_is_cached()
    {
        var (client, stub) = await SignedInWithStubAsync("geo-postal@example.com");
        stub.Respond = _ => Json("""
            {"post code": "24020", "places": [
              {"place name": "Gorle", "state": "Lombardia"},
              {"place name": "Vilminore Di Scalve", "state": "Lombardia"}]}
            """);

        var first = await client.Http.GetFromJsonAsync<JsonElement>("/api/geo/postal-codes/IT/24020");
        await client.Http.GetFromJsonAsync<JsonElement>("/api/geo/postal-codes/it/24020");

        first.GetProperty("available").GetBoolean().ShouldBeTrue();
        var places = first.GetProperty("places").EnumerateArray().ToArray();
        places.Select(place => place.GetProperty("city").GetString()).ShouldBe(["Gorle", "Vilminore di Scalve"]);
        places[0].GetProperty("region").GetString().ShouldBe("Lombardia");
        places[0].GetProperty("postalCode").GetString().ShouldBe("24020");

        stub.Requests.Count.ShouldBe(1, "the second lookup of the same code must be served from the cache");
        stub.Requests[0].AbsolutePath.ShouldBe("/it/24020");
    }

    [Fact]
    public async Task An_unknown_postal_code_is_an_empty_answer_not_an_error()
    {
        var (client, _) = await SignedInWithStubAsync("geo-unknown@example.com");

        var answer = await client.Http.GetFromJsonAsync<JsonElement>("/api/geo/postal-codes/IT/99999");

        answer.GetProperty("available").GetBoolean().ShouldBeTrue();
        answer.GetProperty("places").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_provider_outage_is_reported_as_unavailable_and_not_cached()
    {
        var (client, stub) = await SignedInWithStubAsync("geo-outage@example.com");
        stub.Respond = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        using var response = await client.GetAsync("/api/geo/cities?country=IT&q=Berg");
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        answer.GetProperty("available").GetBoolean().ShouldBeFalse();

        stub.Respond = _ => Json("""{"features": []}""");
        var retried = await client.Http.GetFromJsonAsync<JsonElement>("/api/geo/cities?country=IT&q=Berg");
        retried.GetProperty("available").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task City_search_is_scoped_to_the_country_and_keeps_only_usable_postcodes()
    {
        var (client, stub) = await SignedInWithStubAsync("geo-cities@example.com");
        stub.Respond = _ => Json("""
            {"features": [
              {"properties": {"name": "Bergamo", "state": "Lombardia", "countrycode": "IT"}},
              {"properties": {"name": "Bergamasco", "state": "Piemonte", "postcode": "15022"}},
              {"properties": {"name": "Milano", "state": "Lombardia", "postcode": "20121;20122"}}]}
            """);

        var answer = await client.Http.GetFromJsonAsync<JsonElement>("/api/geo/cities?country=it&q=Berg");

        var query = stub.Requests.Single().Query;
        query.ShouldContain("countrycode=IT");
        query.ShouldContain("q=Berg");
        var places = answer.GetProperty("places").EnumerateArray().ToArray();
        places.Length.ShouldBe(3);
        places[1].GetProperty("postalCode").GetString().ShouldBe("15022");
        places[2].GetProperty("postalCode").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("/api/geo/postal-codes/ITA/20121", "geo.invalid_country")]
    [InlineData("/api/geo/postal-codes/IT/2%3C1", "geo.invalid_postal_code")]
    [InlineData("/api/geo/cities?country=IT&q=B", "geo.invalid_query")]
    [InlineData("/api/geo/cities?q=Bergamo", "geo.invalid_country")]
    public async Task Invalid_input_is_a_400_problem_without_calling_the_provider(string path, string errorCode)
    {
        var (client, stub) = await SignedInWithStubAsync($"geo-invalid-{errorCode.Length}-{path.Length}@example.com");

        using var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().ShouldBe(errorCode);
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Anonymous_callers_get_no_suggestions()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/geo/postal-codes/IT/20121");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
