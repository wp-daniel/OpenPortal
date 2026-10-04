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
/// Applications, groups, grants and the OpenID Connect sign-in they control, exercised end to end through
/// the real host: the access decision is only worth something if the token endpoint enforces it.
/// </summary>
public sealed class AccessTests : IClassFixture<OpenPortalFactory>
{
    private const string Password = "Correct-Horse-9";

    private readonly OpenPortalFactory _factory;

    public AccessTests(OpenPortalFactory factory) => _factory = factory;

    // -----------------------------------------------------------------------
    // Administration endpoints.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("/api/admin/applications")]
    [InlineData("/api/admin/groups")]
    [InlineData("/api/admin/access/tree")]
    public async Task Administration_endpoints_reject_anonymous_callers_and_plain_users(string path)
    {
        using var anonymous = await ApiClient.CreateAsync(_factory);
        using (var response = await anonymous.GetAsync(path))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var user = await _factory.CreateUserAsync($"plain-{Guid.NewGuid():N}@example.com", Password);
        using var client = await SignedInAsync(user);
        using (var response = await client.GetAsync(path))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Creating_an_application_returns_its_secret_once_and_rejects_a_duplicate_client_id()
    {
        using var admin = await AdminAsync();
        var clientId = NewClientId();

        var created = await CreateApplicationAsync(admin, clientId);
        created.GetProperty("clientSecret").GetString().ShouldNotBeNullOrWhiteSpace();
        created.GetProperty("application").GetProperty("status").GetString().ShouldBe("active");

        using var duplicate = await admin.PostAsync("/api/admin/applications", ApplicationBody(clientId));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(duplicate)).ShouldBe("access.client_id_in_use");

        using var listed = await admin.GetAsync("/api/admin/applications");
        var body = await listed.Content.ReadAsStringAsync();
        body.ShouldNotContain("clientSecret");
    }

    [Fact]
    public async Task An_invalid_redirect_uri_is_a_validation_error()
    {
        using var admin = await AdminAsync();

        using var response = await admin.PostAsync(
            "/api/admin/applications",
            new
            {
                clientId = NewClientId(),
                displayName = "Insecure",
                baseUrl = "https://app.example.com",
                redirectUris = new[] { "http://app.example.com/signin-oidc" },
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).ShouldBe("access.redirect_uri_invalid");
    }

    [Fact]
    public async Task Group_membership_grants_access_and_the_user_sees_the_application_on_the_launchpad()
    {
        using var admin = await AdminAsync();
        var application = (await CreateApplicationAsync(admin, NewClientId())).GetProperty("application");
        var applicationId = application.GetProperty("id").GetGuid();

        var member = await _factory.CreateUserAsync($"member-{Guid.NewGuid():N}@example.com", Password);

        using var group = await admin.PostAsync("/api/admin/groups", new { name = $"Sales {Guid.NewGuid():N}" });
        group.StatusCode.ShouldBe(HttpStatusCode.Created);
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using (var added = await admin.PutAsync($"/api/admin/groups/{groupId}/members/{member.Id}", new { }))
        {
            added.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/groups/{groupId}", new { }))
        {
            granted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var memberClient = await SignedInAsync(member);
        using var launchpad = await memberClient.GetAsync("/api/account/applications");
        var items = await launchpad.Content.ReadFromJsonAsync<JsonElement>();
        items.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldContain(applicationId);

        using var access = await admin.GetAsync($"/api/admin/access/users/{member.Id}");
        var explained = (await access.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("applications")
            .EnumerateArray()
            .Single(item => item.GetProperty("applicationId").GetGuid() == applicationId);

        explained.GetProperty("direct").GetBoolean().ShouldBeFalse();
        explained.GetProperty("viaGroups").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(groupId);

        using var tree = await admin.GetAsync("/api/admin/access/tree");
        var node = (await tree.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("applications")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == applicationId);

        node.GetProperty("groups").EnumerateArray().Single()
            .GetProperty("members").EnumerateArray().Single()
            .GetProperty("id").GetGuid().ShouldBe(member.Id);
    }

    // -----------------------------------------------------------------------
    // Announcements.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_announcement_needs_the_provisioning_key()
    {
        using var http = _factory.CreateClient();

        using var response = await AnnounceAsync(http, NewClientId(), key: "wrong");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_announced_application_waits_for_approval_and_cannot_sign_anyone_in_until_then()
    {
        var clientId = NewClientId();
        using var http = _factory.CreateClient();

        using (var announced = await AnnounceAsync(http, clientId, OpenPortalFactory.ProvisioningKey))
        {
            announced.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await announced.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().ShouldBe("pending");
        }

        var user = await _factory.CreateUserAsync($"early-{Guid.NewGuid():N}@example.com", Password);
        using var browser = await BrowserAsync(user);

        // No OpenID Connect client exists before approval, so the protocol layer refuses the request.
        using (var refused = await browser.Http.GetAsync(AuthorizeUrl(clientId, Pkce.Create())))
        {
            refused.StatusCode.ShouldNotBe(HttpStatusCode.Found);
        }

        using var admin = await AdminAsync();
        var applicationId = await FindApplicationIdAsync(admin, clientId);

        using var approved = await admin.PostAsync($"/api/admin/applications/{applicationId}/approve");
        approved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await approved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString().ShouldNotBeNullOrWhiteSpace();

        // Approved but not granted: the user is sent to the portal's explanation page.
        using var denied = await browser.Http.GetAsync(AuthorizeUrl(clientId, Pkce.Create()));
        denied.StatusCode.ShouldBe(HttpStatusCode.Found);
        denied.Headers.Location!.OriginalString.ShouldStartWith("/access-denied?app=");
    }

    // -----------------------------------------------------------------------
    // OpenID Connect.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_anonymous_authorization_request_is_sent_to_the_sign_in_page_and_back()
    {
        using var admin = await AdminAsync();
        var clientId = NewClientId();
        await CreateApplicationAsync(admin, clientId);

        using var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await http.GetAsync(AuthorizeUrl(clientId, Pkce.Create()));

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = response.Headers.Location!;
        location.AbsolutePath.ShouldBe("/sign-in");
        Uri.UnescapeDataString(location.Query).ShouldContain("returnUrl=/connect/authorize?");
    }

    [Fact]
    public async Task A_granted_user_completes_the_code_flow_and_loses_the_session_when_access_is_revoked()
    {
        using var admin = await AdminAsync();
        var clientId = NewClientId();
        var created = await CreateApplicationAsync(admin, clientId);
        var secret = created.GetProperty("clientSecret").GetString()!;
        var applicationId = created.GetProperty("application").GetProperty("id").GetGuid();

        var user = await _factory.CreateUserAsync($"alice-{Guid.NewGuid():N}@example.com", Password);

        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { }))
        {
            granted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Editing the application afterwards must not disturb its secret.
        using (var edited = await admin.PutAsync(
                   $"/api/admin/applications/{applicationId}",
                   new
                   {
                       displayName = "Renamed",
                       baseUrl = "https://app.example.com",
                       redirectUris = new[] { RedirectUri, "https://app.example.com/other-callback" },
                   }))
        {
            edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var browser = await BrowserAsync(user);
        var pkce = Pkce.Create();

        using var authorized = await browser.Http.GetAsync(AuthorizeUrl(clientId, pkce));
        authorized.StatusCode.ShouldBe(HttpStatusCode.Found, await authorized.Content.ReadAsStringAsync());
        var callback = authorized.Headers.Location!;
        callback.GetLeftPart(UriPartial.Path).ShouldBe(RedirectUri);

        var query = System.Web.HttpUtility.ParseQueryString(callback.Query);
        query["state"].ShouldBe("state-123");
        var code = query["code"];
        code.ShouldNotBeNullOrWhiteSpace();

        using var backChannel = _factory.CreateClient();
        using var tokens = await backChannel.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["code_verifier"] = pkce.Verifier,
        }));

        var tokenBody = await tokens.Content.ReadAsStringAsync();
        tokens.StatusCode.ShouldBe(HttpStatusCode.OK, tokenBody);

        var payload = JsonDocument.Parse(tokenBody).RootElement;
        var idToken = payload.GetProperty("id_token").GetString()!;
        var refreshToken = payload.GetProperty("refresh_token").GetString()!;
        var accessToken = payload.GetProperty("access_token").GetString()!;

        var claims = ReadJwtPayload(idToken);
        claims.GetProperty("sub").GetString().ShouldBe(user.Id.ToString());
        claims.GetProperty("email").GetString().ShouldBe(user.Email);

        using (var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo"))
        {
            userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var userInfo = await backChannel.SendAsync(userInfoRequest);
            userInfo.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await userInfo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sub").GetString().ShouldBe(user.Id.ToString());
        }

        using (var revoked = await admin.DeleteAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}"))
        {
            revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var refreshed = await backChannel.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }));

        refreshed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("invalid_grant");
    }

    [Fact]
    public async Task The_discovery_document_is_served_and_unknown_connect_paths_are_404()
    {
        using var http = _factory.CreateClient();

        using (var discovery = await http.GetAsync("/.well-known/openid-configuration"))
        {
            discovery.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await discovery.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("authorization_endpoint").GetString().ShouldEndWith("/connect/authorize");
        }

        using var typo = await http.GetAsync("/connect/typo");
        typo.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        typo.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Antiforgery_is_still_required_on_ordinary_endpoints()
    {
        using var admin = await AdminAsync();

        using var response = await admin.PostWithoutTokenAsync("/api/admin/groups", new { name = "No token" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).ShouldBe("antiforgery.invalid_token");
    }

    // -----------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------

    private const string RedirectUri = "https://app.example.com/signin-oidc";

    private static string NewClientId() => $"app-{Guid.NewGuid():N}"[..20];

    private static object ApplicationBody(string clientId) => new
    {
        clientId,
        displayName = "Example",
        description = "An example application",
        baseUrl = "https://app.example.com",
        redirectUris = new[] { RedirectUri },
        postLogoutRedirectUris = new[] { "https://app.example.com/signout-callback-oidc" },
    };

    private static string AuthorizeUrl(string clientId, Pkce pkce) =>
        "/connect/authorize"
        + $"?client_id={Uri.EscapeDataString(clientId)}"
        + "&response_type=code"
        + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
        + $"&scope={Uri.EscapeDataString("openid profile email offline_access")}"
        + "&state=state-123"
        + $"&code_challenge={pkce.Challenge}"
        + "&code_challenge_method=S256";

    private async Task<ApiClient> AdminAsync()
    {
        var admin = await _factory.CreateUserAsync($"admin-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        return await SignedInAsync(admin);
    }

    private async Task<ApiClient> SignedInAsync(TestUser user)
    {
        var client = await ApiClient.CreateAsync(_factory);
        using var signedIn = await client.SignInAsync(user.Email, user.Password);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return client;
    }

    /// <summary>A signed-in browser that does not follow redirects, so each hop can be inspected.</summary>
    private async Task<ApiClient> BrowserAsync(TestUser user)
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var client = ApiClient.For(http);
        await client.RefreshAntiforgeryTokenAsync();

        using var signedIn = await client.SignInAsync(user.Email, user.Password);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return client;
    }

    private static async Task<JsonElement> CreateApplicationAsync(ApiClient admin, string clientId)
    {
        using var response = await admin.PostAsync("/api/admin/applications", ApplicationBody(clientId));
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> FindApplicationIdAsync(ApiClient admin, string clientId)
    {
        using var response = await admin.GetAsync("/api/admin/applications");
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Single(item => item.GetProperty("clientId").GetString() == clientId)
            .GetProperty("id")
            .GetGuid();
    }

    private static Task<HttpResponseMessage> AnnounceAsync(HttpClient http, string clientId, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/apps/announce")
        {
            Content = JsonContent.Create(new
            {
                clientId,
                displayName = "Announced app",
                baseUrl = "https://app.example.com",
                redirectUris = new[] { RedirectUri },
                version = "1.2.3",
            }),
        };
        request.Headers.Add(ApplicationAnnouncementsController.ProvisioningKeyHeader, key);

        return http.SendAsync(request);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString();

    private static JsonElement ReadJwtPayload(string jwt) =>
        JsonDocument.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[1])).RootElement;

    private sealed record Pkce(string Verifier, string Challenge)
    {
        public static Pkce Create()
        {
            var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            return new Pkce(verifier, challenge);
        }
    }
}
