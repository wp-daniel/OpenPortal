using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Language selection end to end: which languages are offered, what a client downloads, what the server
/// answers in, and what is saved to the profile.
/// </summary>
public sealed class LocalizationTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private static async Task<ApiClient> ClientWithLanguageAsync(OpenPortalFactory factory, string acceptLanguage)
    {
        var client = await ApiClient.CreateAsync(factory);
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);

        return client;
    }

    [Fact]
    public async Task Languages_are_offered_to_an_anonymous_caller()
    {
        using var client = await ApiClient.CreateAsync(factory);

        using var response = await client.GetAsync("/api/i18n/languages");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        payload.GetProperty("defaultLanguage").GetString().ShouldBe("en");

        var codes = payload.GetProperty("languages").EnumerateArray()
            .Select(language => language.GetProperty("code").GetString())
            .ToArray();

        codes.ShouldBe(["en", "it", "es", "fr"]);
    }

    [Theory]
    [InlineData("it", "Utenti")]
    [InlineData("es", "Usuarios")]
    [InlineData("fr", "Utilisateurs")]
    [InlineData("en", "Users")]
    public async Task Messages_are_served_in_the_requested_language(string language, string expectedUsers)
    {
        using var client = await ApiClient.CreateAsync(factory);

        using var response = await client.GetAsync($"/api/i18n/messages?lang={language}");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        payload.GetProperty("messages").GetProperty("nav.users").GetString().ShouldBe(expectedUsers);
    }

    [Fact]
    public async Task An_unsupported_language_falls_back_to_the_default()
    {
        using var client = await ApiClient.CreateAsync(factory);

        using var response = await client.GetAsync("/api/i18n/messages?lang=xx");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("language").GetString().ShouldBe("en");
        payload.GetProperty("messages").GetProperty("nav.users").GetString().ShouldBe("Users");
    }

    [Fact]
    public async Task Messages_can_be_revalidated_with_an_etag()
    {
        using var client = await ApiClient.CreateAsync(factory);

        using var first = await client.GetAsync("/api/i18n/messages?lang=it");
        var etag = first.Headers.ETag;
        etag.ShouldNotBeNull();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/i18n/messages?lang=it");
        request.Headers.IfNoneMatch.Add(etag);
        using var second = await client.Http.SendAsync(request);

        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Server_errors_follow_the_accept_language_header_and_keep_their_error_code()
    {
        using var english = await ClientWithLanguageAsync(factory, "en");
        using var italian = await ClientWithLanguageAsync(factory, "it");

        using var englishResponse = await english.SignInAsync("nobody@example.com", Passwords.Valid);
        using var italianResponse = await italian.SignInAsync("nobody@example.com", Passwords.Valid);

        var englishBody = await englishResponse.Content.ReadFromJsonAsync<JsonElement>();
        var italianBody = await italianResponse.Content.ReadFromJsonAsync<JsonElement>();

        englishResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        italianResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Clients branch on the code, so translation must never touch it.
        italianBody.GetProperty("errorCode").GetString().ShouldBe(englishBody.GetProperty("errorCode").GetString());
        englishBody.GetProperty("detail").GetString().ShouldBe("The supplied email or password is not valid.");
        var italianDetail = italianBody.GetProperty("detail").GetString();
        italianDetail.ShouldNotBeNull();
        italianDetail.ShouldContain("password");
        italianDetail.ShouldNotBe(englishBody.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Validation_messages_are_translated()
    {
        using var client = await ClientWithLanguageAsync(factory, "fr");

        using var response = await client.PostAsync("/api/auth/login", new { email = "", password = "" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var message = body.GetProperty("errors").GetProperty("Email")[0].GetString();
        message.ShouldNotBeNull();
        message.ShouldNotStartWith("validation.");
        message.ShouldContain("e-mail");
    }

    [Fact]
    public async Task The_language_is_saved_to_the_profile_and_reported_by_the_session()
    {
        var user = await factory.CreateUserAsync("polyglot@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(factory);
        await client.SignInAsync(user.Email, user.Password);

        using (var before = await client.GetAsync("/api/auth/session"))
        {
            var session = await before.Content.ReadFromJsonAsync<JsonElement>();
            session.GetProperty("user").GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        // Upper case is accepted and stored in the configured spelling.
        using (var save = await client.PutAsync("/api/account/language", new { language = "FR" }))
        {
            save.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await save.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("language").GetString().ShouldBe("fr");
        }

        using (var after = await client.GetAsync("/api/auth/session"))
        {
            var session = await after.Content.ReadFromJsonAsync<JsonElement>();
            session.GetProperty("user").GetProperty("language").GetString().ShouldBe("fr");
        }

        using var clear = await client.PutAsync("/api/account/language", new { language = (string?)null });
        (await clear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task An_unsupported_language_is_rejected_with_a_problem_response()
    {
        var user = await factory.CreateUserAsync("badlang@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(factory);
        await client.SignInAsync(user.Email, user.Password);

        using var response = await client.PutAsync("/api/account/language", new { language = "xx" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        body.GetProperty("errorCode").GetString().ShouldBe("identity.unsupported_language");
    }

    [Fact]
    public async Task Saving_a_language_requires_a_session()
    {
        using var client = await ApiClient.CreateAsync(factory);

        using var response = await client.PutAsync("/api/account/language", new { language = "it" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
