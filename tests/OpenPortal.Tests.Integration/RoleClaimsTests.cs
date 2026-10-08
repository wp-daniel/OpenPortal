using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenPortal.Web.Controllers;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Application roles and groups reaching the application in its tokens: roles are defined on the application,
/// assigned on grants (to users and to groups), and recomputed on every refresh; the groups an application
/// sees follow its own setting.
/// </summary>
public sealed class RoleClaimsTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private const string Password = "Correct-Horse-9";
    private const string RedirectUri = "https://crm.example.com/signin-oidc";

    [Fact]
    public async Task Roles_from_the_user_and_group_grants_reach_the_tokens_and_follow_changes_at_refresh()
    {
        using var admin = await AdminAsync();
        var clientId = $"crm-{Guid.NewGuid():N}"[..20];

        using var created = await admin.PostAsync("/api/admin/applications", new
        {
            clientId,
            displayName = "CRM",
            baseUrl = "https://crm.example.com",
            redirectUris = new[] { RedirectUri },
            roles = new[]
            {
                new { key = "sales", displayName = (string?)"Sales", description = (string?)null },
                new { key = "billing", displayName = (string?)"Billing", description = (string?)"Invoices" },
                new { key = "admin", displayName = (string?)null, description = (string?)null },
            },
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var secret = body.GetProperty("clientSecret").GetString()!;
        var application = body.GetProperty("application");
        var applicationId = application.GetProperty("id").GetGuid();
        application.GetProperty("groupClaims").GetString().ShouldBe("none");
        application.GetProperty("roles").EnumerateArray().Select(role => role.GetProperty("key").GetString())
            .ShouldBe(["admin", "billing", "sales"], ignoreOrder: true);

        var user = await factory.CreateUserAsync($"seller-{Guid.NewGuid():N}@example.com", Password);
        var groupName = $"Sales team {Guid.NewGuid():N}"[..20];
        var groupId = await CreateGroupAsync(admin, groupName, user.Id);

        await PutOkAsync(admin, $"/api/admin/access/applications/{applicationId}/groups/{groupId}", new { roles = new[] { "sales" } });
        await PutOkAsync(admin, $"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { roles = new[] { "Billing" } });

        // A role the application does not define is refused, and the grant keeps what it had.
        using (var unknown = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { roles = new[] { "root" } }))
        {
            unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("access.role_not_found");
        }

        var tokens = await SignInThroughCodeFlowAsync(user, clientId, secret, "openid profile email offline_access roles groups");

        var claims = ReadJwtPayload(tokens.GetProperty("id_token").GetString()!);
        Strings(claims, "role").ShouldBe(["billing", "sales"]);
        claims.TryGetProperty("groups", out _).ShouldBeFalse("the application's setting is none");

        using (var userInfo = await UserInfoAsync(tokens.GetProperty("access_token").GetString()!))
        {
            Strings(userInfo.RootElement, "role").ShouldBe(["billing", "sales"]);
        }

        // The tree shows the roles on each grant.
        using (var treeResponse = await admin.GetAsync("/api/admin/access/tree"))
        {
            var node = (await treeResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("applications").EnumerateArray().Single(entry => entry.GetProperty("id").GetGuid() == applicationId);

            node.GetProperty("users").EnumerateArray().Single().GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["billing"]);
            node.GetProperty("groups").EnumerateArray().Single().GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["sales"]);
        }

        // Removing a role from the application takes it off the grants; showing granted groups is switched on.
        await PutOkAsync(admin, $"/api/admin/applications/{applicationId}", new
        {
            displayName = "CRM",
            baseUrl = "https://crm.example.com",
            redirectUris = new[] { RedirectUri },
            roles = new[] { new { key = "sales", displayName = "Sales", description = (string?)null } },
            groupClaims = "granted",
        });

        var refreshed = await RefreshAsync(clientId, secret, tokens.GetProperty("refresh_token").GetString()!);
        var after = ReadJwtPayload(refreshed.GetProperty("id_token").GetString()!);
        Strings(after, "role").ShouldBe(["sales"]);
        Strings(after, "groups").ShouldBe([groupName]);

        using (var userAccess = await admin.GetAsync($"/api/admin/access/users/{user.Id}"))
        {
            (await userAccess.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("applications").EnumerateArray().Single()
                .GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["sales"]);
        }

        // Granting again without roles leaves the roles alone.
        await PutOkAsync(admin, $"/api/admin/access/applications/{applicationId}/groups/{groupId}", new { });
        var again = await RefreshAsync(clientId, secret, refreshed.GetProperty("refresh_token").GetString()!);
        Strings(ReadJwtPayload(again.GetProperty("id_token").GetString()!), "role").ShouldBe(["sales"]);
    }

    [Fact]
    public async Task An_application_without_the_roles_scope_gets_no_role_claims_in_its_identity_token()
    {
        using var admin = await AdminAsync();
        var clientId = $"plain-{Guid.NewGuid():N}"[..20];

        using var created = await admin.PostAsync("/api/admin/applications", new
        {
            clientId,
            displayName = "Plain",
            baseUrl = "https://crm.example.com",
            redirectUris = new[] { RedirectUri },
            roles = new[] { new { key = "reader", displayName = "Reader", description = (string?)null } },
            groupClaims = "all",
        });
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var applicationId = body.GetProperty("application").GetProperty("id").GetGuid();

        var user = await factory.CreateUserAsync($"reader-{Guid.NewGuid():N}@example.com", Password);
        await PutOkAsync(admin, $"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { roles = new[] { "reader" } });

        var tokens = await SignInThroughCodeFlowAsync(user, clientId, body.GetProperty("clientSecret").GetString()!, "openid profile");
        var claims = ReadJwtPayload(tokens.GetProperty("id_token").GetString()!);

        claims.TryGetProperty("role", out _).ShouldBeFalse();
        claims.TryGetProperty("groups", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Announced_roles_are_taken_whole_while_pending_and_only_added_once_approved()
    {
        using var admin = await AdminAsync();
        var clientId = $"ann-{Guid.NewGuid():N}"[..20];
        using var http = factory.CreateClient();

        await AnnounceAsync(http, clientId, ("viewer", "Viewer"), ("editor", "Editor"));
        await AnnounceAsync(http, clientId, ("viewer", "Viewer"));

        var applicationId = await FindApplicationIdAsync(admin, clientId);
        (await RoleKeysAsync(admin, clientId)).ShouldBe(["viewer"]);

        using (var approved = await admin.PostAsync($"/api/admin/applications/{applicationId}/approve"))
        {
            approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        }

        // Approved: a new role is added, a role missing from the announcement is not removed.
        await AnnounceAsync(http, clientId, ("auditor", "Auditor"));
        (await RoleKeysAsync(admin, clientId)).ShouldBe(["auditor", "viewer"], ignoreOrder: true);

        // An invalid role key is a validation error for the application.
        using var invalid = await AnnounceRawAsync(http, clientId, ("Not a key!", "Bad"));
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------

    private async Task<JsonElement> SignInThroughCodeFlowAsync(TestUser user, string clientId, string secret, string scope)
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var browser = ApiClient.For(http);
        await browser.RefreshAntiforgeryTokenAsync();
        (await browser.SignInAsync(user.Email, user.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        using var authorized = await http.GetAsync(
            "/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + "&response_type=code"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + $"&scope={Uri.EscapeDataString(scope)}"
            + "&state=s"
            + $"&code_challenge={challenge}"
            + "&code_challenge_method=S256");
        authorized.StatusCode.ShouldBe(HttpStatusCode.Found, await authorized.Content.ReadAsStringAsync());

        var code = System.Web.HttpUtility.ParseQueryString(authorized.Headers.Location!.Query)["code"];
        code.ShouldNotBeNullOrWhiteSpace(authorized.Headers.Location!.ToString());

        return await TokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["code_verifier"] = verifier,
        });
    }

    private Task<JsonElement> RefreshAsync(string clientId, string secret, string refreshToken) =>
        TokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        });

    private async Task<JsonElement> TokenAsync(Dictionary<string, string> form)
    {
        using var backChannel = factory.CreateClient();
        using var response = await backChannel.PostAsync("/connect/token", new FormUrlEncodedContent(form));
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task<JsonDocument> UserInfoAsync(string accessToken)
    {
        using var backChannel = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await backChannel.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>A claim that may be one string or an array, as a sorted list.</summary>
    private static IReadOnlyList<string?> Strings(JsonElement claims, string name)
    {
        if (!claims.TryGetProperty(name, out var value))
        {
            return [];
        }

        var values = value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.GetString())
            : [value.GetString()];

        return values.Order(StringComparer.Ordinal).ToList();
    }

    private static JsonElement ReadJwtPayload(string jwt) =>
        JsonDocument.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[1])).RootElement.Clone();

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

    private static async Task<Guid> FindApplicationIdAsync(ApiClient admin, string clientId) =>
        (await ApplicationAsync(admin, clientId)).GetProperty("id").GetGuid();

    private static async Task<IReadOnlyList<string?>> RoleKeysAsync(ApiClient admin, string clientId) =>
        (await ApplicationAsync(admin, clientId)).GetProperty("roles").EnumerateArray()
            .Select(role => role.GetProperty("key").GetString())
            .ToList();

    private static async Task<JsonElement> ApplicationAsync(ApiClient admin, string clientId)
    {
        using var response = await admin.GetAsync("/api/admin/applications");

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Single(item => item.GetProperty("clientId").GetString() == clientId)
            .Clone();
    }

    private static async Task AnnounceAsync(HttpClient http, string clientId, params (string Key, string Name)[] roles)
    {
        using var response = await AnnounceRawAsync(http, clientId, roles);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> AnnounceRawAsync(HttpClient http, string clientId, params (string Key, string Name)[] roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/apps/announce")
        {
            Content = JsonContent.Create(new
            {
                clientId,
                displayName = "Announced",
                baseUrl = "https://crm.example.com",
                redirectUris = new[] { RedirectUri },
                version = "1.0.0",
                roles = roles.Select(role => new { key = role.Key, displayName = role.Name }).ToArray(),
            }),
        };
        request.Headers.Add(ApplicationAnnouncementsController.ProvisioningKeyHeader, OpenPortalFactory.ProvisioningKey);

        return http.SendAsync(request);
    }

    private async Task<ApiClient> AdminAsync()
    {
        var admin = await factory.CreateUserAsync($"admin-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        var client = await ApiClient.CreateAsync(factory);
        (await client.SignInAsync(admin.Email, admin.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);

        return client;
    }
}
