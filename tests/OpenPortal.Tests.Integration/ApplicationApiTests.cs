using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The application API: an application's backend gets a token of its own (client credentials) once an
/// administrator enables API scopes, and reads and changes who can open that application, and only that one.
/// Also the documents and the reference page that describe it.
/// </summary>
public sealed partial class ApplicationApiTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private const string Password = "Correct-Horse-9";
    private const string RedirectUri = "https://crm.example.com/signin-oidc";

    private static readonly string[] AllScopes = ["portal.users.read", "portal.access.manage", "portal.users.create"];

    [Fact]
    public async Task An_application_reads_grants_creates_and_revokes_its_own_users()
    {
        using var admin = await AdminAsync();
        var app = await CreateApplicationAsync(admin, AllScopes);

        using var api = await ApplicationClientAsync(app);

        using (var self = await api.GetAsync("/api/apps/me"))
        {
            self.StatusCode.ShouldBe(HttpStatusCode.OK, await self.Content.ReadAsStringAsync());
            var body = await self.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("clientId").GetString().ShouldBe(app.ClientId);
            Strings(body.GetProperty("apiScopes")).ShouldBe(AllScopes);
        }

        // One user through a group (role "sales"), one granted by the application itself (role "billing").
        var viaGroup = await factory.CreateUserAsync($"group-{Guid.NewGuid():N}@example.com", Password);
        var direct = await factory.CreateUserAsync($"direct-{Guid.NewGuid():N}@example.com", Password);
        var groupId = await CreateGroupAsync(admin, $"Sales {Guid.NewGuid():N}"[..16], viaGroup.Id);
        await PutOkAsync(admin, $"/api/admin/access/applications/{app.Id}/groups/{groupId}", new { roles = new[] { "sales" } });

        using (var granted = await api.PutAsJsonAsync($"/api/apps/me/users/{direct.Id}/access", new { roles = new[] { "Billing" } }))
        {
            granted.StatusCode.ShouldBe(HttpStatusCode.OK, await granted.Content.ReadAsStringAsync());
            var body = await granted.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("changed").GetBoolean().ShouldBeTrue();
            body.GetProperty("user").GetProperty("direct").GetBoolean().ShouldBeTrue();
            Strings(body.GetProperty("user").GetProperty("roles")).ShouldBe(["billing"]);
        }

        using (var unknownRole = await api.PutAsJsonAsync($"/api/apps/me/users/{direct.Id}/access", new { roles = new[] { "root" } }))
        {
            unknownRole.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ErrorCodeAsync(unknownRole)).ShouldBe("access.role_not_found");
        }

        using (var list = await api.GetAsync("/api/apps/me/users?pageSize=50"))
        {
            list.StatusCode.ShouldBe(HttpStatusCode.OK, await list.Content.ReadAsStringAsync());
            var body = await list.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("totalCount").GetInt32().ShouldBe(2);

            var users = body.GetProperty("items").EnumerateArray().ToDictionary(user => user.GetProperty("id").GetGuid());
            users[viaGroup.Id].GetProperty("direct").GetBoolean().ShouldBeFalse();
            Strings(users[viaGroup.Id].GetProperty("roles")).ShouldBe(["sales"]);
            users[direct.Id].GetProperty("email").GetString().ShouldBe(direct.Email);
        }

        using (var search = await api.GetAsync($"/api/apps/me/users?search={Uri.EscapeDataString(direct.Email[..10].ToUpperInvariant())}"))
        {
            var items = (await search.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
            items.Select(user => user.GetProperty("id").GetGuid()).ShouldBe([direct.Id]);
        }

        // Someone without access is not one of the application's users.
        var stranger = await factory.CreateUserAsync($"stranger-{Guid.NewGuid():N}@example.com", Password);
        using (var notFound = await api.GetAsync($"/api/apps/me/users/{stranger.Id}"))
        {
            notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await ErrorCodeAsync(notFound)).ShouldBe("access.user_has_no_access");
        }

        // Adding an existing account by email grants it; nothing new is created.
        using (var added = await api.PostAsJsonAsync("/api/apps/me/users", new { email = stranger.Email.ToUpperInvariant() }))
        {
            added.StatusCode.ShouldBe(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
            var body = await added.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("created").GetBoolean().ShouldBeFalse();
            body.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(stranger.Id);
        }

        // A new person needs a password and a name.
        var newEmail = $"new-{Guid.NewGuid():N}@example.com";
        using (var incomplete = await api.PostAsJsonAsync("/api/apps/me/users", new { email = newEmail }))
        {
            incomplete.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var errors = (await incomplete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            errors.TryGetProperty("password", out _).ShouldBeTrue(errors.ToString());
            errors.TryGetProperty("firstName", out _).ShouldBeTrue(errors.ToString());
        }

        Guid createdId;
        using (var created = await api.PostAsJsonAsync("/api/apps/me/users", new
               {
                   email = newEmail,
                   password = Password,
                   firstName = "Nora",
                   lastName = "New",
                   roles = new[] { "sales" },
               }))
        {
            created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("created").GetBoolean().ShouldBeTrue();
            createdId = body.GetProperty("user").GetProperty("id").GetGuid();
            body.GetProperty("user").GetProperty("displayName").GetString().ShouldBe("Nora New");
            Strings(body.GetProperty("user").GetProperty("roles")).ShouldBe(["sales"]);
        }

        // An ordinary account, which can sign in to the portal with the password the application set.
        using (var account = await admin.GetAsync($"/api/admin/users/{createdId}"))
        {
            (await account.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isAdministrator").GetBoolean().ShouldBeFalse();
        }

        using (var newcomer = await ApiClient.CreateAsync(factory))
        {
            (await newcomer.SignInAsync(newEmail, Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Withdrawing: a personal grant goes; access through a group stays, and the answer says so.
        using (var revoked = await api.DeleteAsync($"/api/apps/me/users/{direct.Id}/access"))
        {
            var body = await revoked.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("changed").GetBoolean().ShouldBeTrue();
            body.GetProperty("stillHasAccess").GetBoolean().ShouldBeFalse();
        }

        using (var groupOnly = await api.DeleteAsync($"/api/apps/me/users/{viaGroup.Id}/access"))
        {
            var body = await groupOnly.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("changed").GetBoolean().ShouldBeFalse();
            body.GetProperty("stillHasAccess").GetBoolean().ShouldBeTrue();
        }

        // The audit log names the application as the one who did it.
        using (var audit = await admin.GetAsync($"/api/admin/audit?subject={direct.Id}"))
        {
            var text = await audit.Content.ReadAsStringAsync();
            audit.StatusCode.ShouldBe(HttpStatusCode.OK, text);
            text.ShouldContain(app.ClientId);
        }
    }

    [Fact]
    public async Task Each_operation_needs_its_scope_and_the_scope_is_rechecked_on_every_request()
    {
        using var admin = await AdminAsync();
        var app = await CreateApplicationAsync(admin, ["portal.users.read"]);

        // A scope the application was not given cannot be asked for.
        using (var refused = await TokenResponseAsync(app, "portal.access.manage"))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync());
        }

        using var api = await ApplicationClientAsync(app);
        var user = await factory.CreateUserAsync($"user-{Guid.NewGuid():N}@example.com", Password);

        (await api.GetAsync("/api/apps/me/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await api.PutAsJsonAsync($"/api/apps/me/users/{user.Id}/access", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await api.PostAsJsonAsync("/api/apps/me/users", new { email = user.Email })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Switching the scope off takes effect at once, although the token is still valid.
        await PutOkAsync(admin, $"/api/admin/applications/{app.Id}", UpdateBody(app.ClientId, apiScopes: ["portal.access.manage"]));

        using (var withdrawn = await api.GetAsync("/api/apps/me/users"))
        {
            withdrawn.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await ErrorCodeAsync(withdrawn)).ShouldBe("access.api_scope_missing");
        }

        // Disabling the application stops it altogether.
        (await admin.PostAsync($"/api/admin/applications/{app.Id}/disable")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var disabled = await api.GetAsync("/api/apps/me"))
        {
            disabled.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        using (var noToken = await TokenResponseAsync(app, scope: null))
        {
            noToken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task An_application_without_api_scopes_gets_no_token_of_its_own()
    {
        using var admin = await AdminAsync();
        var app = await CreateApplicationAsync(admin, []);

        using var refused = await TokenResponseAsync(app, scope: null);

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_users_token_or_session_never_opens_the_application_api()
    {
        using var admin = await AdminAsync();
        var app = await CreateApplicationAsync(admin, AllScopes);

        var user = await factory.CreateUserAsync($"user-{Guid.NewGuid():N}@example.com", Password);
        await PutOkAsync(admin, $"/api/admin/access/applications/{app.Id}/users/{user.Id}", new { });

        // The client may request the API scopes, but a user's token never carries them.
        var tokens = await SignInThroughCodeFlowAsync(user, app, "openid profile portal.users.read");
        tokens.GetProperty("scope").GetString()!.Split(' ').ShouldNotContain("portal.users.read");

        using var withUserToken = factory.CreateClient();
        withUserToken.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        (await withUserToken.GetAsync("/api/apps/me")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Nor does the portal session, even an administrator's.
        (await admin.GetAsync("/api/apps/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_apps_document_describes_sign_in_and_the_application_api_and_nothing_else()
    {
        using var http = factory.CreateClient();

        using var apps = await http.GetAsync("/openapi/apps.json");
        apps.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paths = (await apps.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();

        paths.ShouldContain("/connect/token");
        paths.ShouldContain("/connect/authorize");
        paths.ShouldContain("/api/apps/me/users");
        paths.ShouldContain("/api/apps/announce");
        paths.ShouldNotContain(path => path.StartsWith("/api/admin", StringComparison.Ordinal));
        paths.ShouldNotContain(path => path.StartsWith("/api/account", StringComparison.Ordinal));

        using var portal = await http.GetAsync("/openapi/v1.json");
        var portalPaths = (await portal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();
        portalPaths.ShouldNotContain(path => path.StartsWith("/api/apps/me", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_reference_page_allows_only_its_own_nonced_start_up_script()
    {
        using var http = factory.CreateClient();

        using var page = await http.GetAsync("/scalar");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);

        var html = await page.Content.ReadAsStringAsync();
        var csp = page.Headers.GetValues("Content-Security-Policy").Single();

        var nonce = NoncePattern().Match(csp);
        nonce.Success.ShouldBeTrue(csp);
        html.ShouldContain($"nonce=\"{nonce.Groups[1].Value}\"");
        ScriptSourcePattern().Match(csp).Value.ShouldNotContain("unsafe-inline");
        csp.ShouldContain("script-src 'nonce-");
        html.ShouldNotContain("cdn.jsdelivr.net");
    }

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex NoncePattern();

    [GeneratedRegex("script-src [^;]*")]
    private static partial Regex ScriptSourcePattern();

    // -----------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------

    private sealed record TestApplication(Guid Id, string ClientId, string Secret);

    private static async Task<TestApplication> CreateApplicationAsync(ApiClient admin, string[] apiScopes)
    {
        var clientId = $"app-{Guid.NewGuid():N}"[..20];

        using var created = await admin.PostAsync("/api/admin/applications", new
        {
            clientId,
            displayName = "CRM",
            baseUrl = "https://crm.example.com",
            redirectUris = new[] { RedirectUri },
            roles = new[]
            {
                new { key = "sales", displayName = (string?)"Sales", description = (string?)null },
                new { key = "billing", displayName = (string?)"Billing", description = (string?)null },
            },
            apiScopes,
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Strings(body.GetProperty("application").GetProperty("apiScopes")).ShouldBe(apiScopes);

        return new TestApplication(
            body.GetProperty("application").GetProperty("id").GetGuid(),
            clientId,
            body.GetProperty("clientSecret").GetString()!);
    }

    private static object UpdateBody(string clientId, string[] apiScopes) => new
    {
        displayName = "CRM",
        baseUrl = "https://crm.example.com",
        redirectUris = new[] { RedirectUri },
        apiScopes,
    };

    private Task<HttpResponseMessage> TokenResponseAsync(TestApplication app, string? scope)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = app.ClientId,
            ["client_secret"] = app.Secret,
        };

        if (scope is not null)
        {
            form["scope"] = scope;
        }

        var backChannel = factory.CreateClient();

        return backChannel.PostAsync("/connect/token", new FormUrlEncodedContent(form));
    }

    /// <summary>A client calling as the application, with a client credentials token for every enabled scope.</summary>
    private async Task<HttpClient> ApplicationClientAsync(TestApplication app)
    {
        using var response = await TokenResponseAsync(app, scope: null);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);

        var token = JsonDocument.Parse(text).RootElement;
        token.TryGetProperty("refresh_token", out _).ShouldBeFalse("an application's own token is never refreshed");
        token.TryGetProperty("id_token", out _).ShouldBeFalse("an application's own token names no user");

        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());

        return http;
    }

    private async Task<JsonElement> SignInThroughCodeFlowAsync(TestUser user, TestApplication app, string scope)
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var browser = ApiClient.For(http);
        await browser.RefreshAntiforgeryTokenAsync();
        (await browser.SignInAsync(user.Email, user.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        using var authorized = await http.GetAsync(
            "/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(app.ClientId)}"
            + "&response_type=code"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + $"&scope={Uri.EscapeDataString(scope)}"
            + "&state=s"
            + $"&code_challenge={challenge}"
            + "&code_challenge_method=S256");
        authorized.StatusCode.ShouldBe(HttpStatusCode.Found, await authorized.Content.ReadAsStringAsync());

        var code = System.Web.HttpUtility.ParseQueryString(authorized.Headers.Location!.Query)["code"];
        code.ShouldNotBeNullOrWhiteSpace(authorized.Headers.Location!.ToString());

        using var backChannel = factory.CreateClient();
        using var response = await backChannel.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = app.ClientId,
            ["client_secret"] = app.Secret,
            ["code_verifier"] = verifier,
        }));
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static IReadOnlyList<string?> Strings(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()).ToList();

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString();

    private static async Task PutOkAsync(ApiClient client, string path, object body)
    {
        using var response = await client.PutAsync(path, body);
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> CreateGroupAsync(ApiClient admin, string name, params Guid[] memberIds)
    {
        using var created = await admin.PostAsync("/api/admin/groups", new { name });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var groupId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        foreach (var memberId in memberIds)
        {
            await PutOkAsync(admin, $"/api/admin/groups/{groupId}/members/{memberId}", new { });
        }

        return groupId;
    }

    private async Task<ApiClient> AdminAsync()
    {
        var admin = await factory.CreateUserAsync($"admin-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        var client = await ApiClient.CreateAsync(factory);
        (await client.SignInAsync(admin.Email, admin.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);

        return client;
    }
}
