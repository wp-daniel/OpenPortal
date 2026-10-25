using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// What an application learns about the person (the standard OpenID Connect claims, each behind its scope) and how
/// recently they proved who they are: <c>auth_time</c>, and <c>max_age</c> / <c>prompt=login</c> to ask for a new sign-in.
/// </summary>
public sealed class StandardClaimsTests : IClassFixture<OpenPortalFactory>
{
    private readonly OpenPortalFactory _factory;

    public StandardClaimsTests(OpenPortalFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Discovery_advertises_the_standard_scopes_and_claims()
    {
        using var client = _factory.CreateClient();
        var discovery = JsonDocument.Parse(await client.GetStringAsync("/.well-known/openid-configuration")).RootElement;

        var scopes = discovery.GetProperty("scopes_supported").EnumerateArray().Select(scope => scope.GetString()).ToList();
        scopes.ShouldContain("phone");
        scopes.ShouldContain("address");

        var claims = discovery.GetProperty("claims_supported").EnumerateArray().Select(claim => claim.GetString()).ToList();
        foreach (var claim in new[] { "given_name", "family_name", "locale", "updated_at", "email_verified", "phone_number", "address", "auth_time" })
        {
            claims.ShouldContain(claim);
        }
    }

    [Fact]
    public async Task The_identity_token_carries_the_profile_each_part_behind_its_scope()
    {
        var (user, clientId, secret) = await UserWithApplicationAsync();
        await FillProfileAsync(user);
        var before = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeSeconds();

        var everything = await Scenario.SignInToApplicationAsync(_factory, user, clientId, secret, "openid profile email phone address offline_access");
        var claims = Scenario.ReadJwtPayload(everything.GetProperty("id_token").GetString()!);

        claims.GetProperty("given_name").GetString().ShouldBe("Ada");
        claims.GetProperty("family_name").GetString().ShouldBe("Lovelace");
        claims.GetProperty("email_verified").ValueKind.ShouldBe(JsonValueKind.False);
        claims.GetProperty("updated_at").ValueKind.ShouldBe(JsonValueKind.Number);
        claims.GetProperty("phone_number").GetString().ShouldBe("+39 02 1234567");
        claims.GetProperty("phone_number_verified").ValueKind.ShouldBe(JsonValueKind.False);

        var address = claims.GetProperty("address");
        address.GetProperty("street_address").GetString().ShouldBe("Via Roma 1");
        address.GetProperty("locality").GetString().ShouldBe("Milano");
        address.GetProperty("postal_code").GetString().ShouldBe("20121");
        address.GetProperty("country").GetString().ShouldBe("IT");

        var authTime = claims.GetProperty("auth_time").GetInt64();
        authTime.ShouldBeGreaterThanOrEqualTo(before);

        // The userinfo endpoint says the same.
        using var http = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", everything.GetProperty("access_token").GetString());
        using var userInfo = await http.SendAsync(request);
        var info = await Scenario.JsonAsync(userInfo, HttpStatusCode.OK);
        info.GetProperty("given_name").GetString().ShouldBe("Ada");
        info.GetProperty("address").GetProperty("locality").GetString().ShouldBe("Milano");

        // A refresh is not a new authentication: the time stays the sign-in's.
        using var refreshed = await Scenario.RefreshAsync(_factory, clientId, secret, everything.GetProperty("refresh_token").GetString()!);
        var refreshedClaims = Scenario.ReadJwtPayload((await Scenario.JsonAsync(refreshed, HttpStatusCode.OK)).GetProperty("id_token").GetString()!);
        refreshedClaims.GetProperty("auth_time").GetInt64().ShouldBe(authTime);

        // Without the phone and address scopes, neither leaves the portal.
        var little = await Scenario.SignInToApplicationAsync(_factory, user, clientId, secret, "openid profile");
        var fewer = Scenario.ReadJwtPayload(little.GetProperty("id_token").GetString()!);
        fewer.TryGetProperty("phone_number", out _).ShouldBeFalse();
        fewer.TryGetProperty("address", out _).ShouldBeFalse();
        fewer.TryGetProperty("email_verified", out _).ShouldBeFalse();
        fewer.GetProperty("given_name").GetString().ShouldBe("Ada");
    }

    [Theory]
    [InlineData("&prompt=login")]
    [InlineData("&max_age=0")]
    public async Task Asking_for_a_new_sign_in_keeps_the_session_and_sends_the_person_to_sign_in_again(string parameter)
    {
        var (user, clientId, _) = await UserWithApplicationAsync();
        var browser = await SignedInBrowserAsync(user);

        using var authorized = await browser.GetAsync(AuthorizeUrl(clientId, parameter, out _));

        authorized.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = authorized.Headers.Location!.ToString();
        location.ShouldStartWith("/sign-in?reauth=1&returnUrl=");

        // The way back no longer asks for it, or coming back would loop.
        var returnUrl = HttpUtility.ParseQueryString(new Uri("https://portal" + location).Query)["returnUrl"]!;
        returnUrl.ShouldStartWith("/connect/authorize?");
        returnUrl.ShouldNotContain("prompt=");
        returnUrl.ShouldNotContain("max_age=");

        // A link any site can open must not sign anyone out.
        (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task A_recent_enough_sign_in_satisfies_max_age_and_the_token_says_when()
    {
        var (user, clientId, secret) = await UserWithApplicationAsync();
        var browser = await SignedInBrowserAsync(user);
        var signedInAt = DateTimeOffset.Parse(
            (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("user").GetProperty("authenticatedAtUtc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);

        using var authorized = await browser.GetAsync(AuthorizeUrl(clientId, "&max_age=3600", out var verifier));
        authorized.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = authorized.Headers.Location!;
        location.ToString().ShouldStartWith(Scenario.RedirectUri);

        var tokens = await RedeemAsync(clientId, secret, location, verifier);
        Scenario.ReadJwtPayload(tokens.GetProperty("id_token").GetString()!).GetProperty("auth_time").GetInt64()
            .ShouldBe(signedInAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Without_interaction_an_application_that_wants_a_new_sign_in_gets_login_required()
    {
        var (user, clientId, _) = await UserWithApplicationAsync();
        var browser = await SignedInBrowserAsync(user);

        using var authorized = await browser.GetAsync(AuthorizeUrl(clientId, "&max_age=0&prompt=none", out _));

        authorized.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = authorized.Headers.Location!;
        location.ToString().ShouldStartWith(Scenario.RedirectUri);
        HttpUtility.ParseQueryString(location.Query)["error"].ShouldBe("login_required");
    }

    [Fact]
    public async Task Signing_in_again_renews_the_authentication_time_but_a_password_change_does_not()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("timely"), Scenario.Password);
        var browser = await SignedInBrowserAsync(user);
        var first = await AuthenticatedAtAsync(browser);

        // Re-issuing the session after a password change is not a new authentication.
        const string NewPassword = "Another-Horse-77";
        using (var changed = await browser.PostAsync("/api/account/change-password", new { currentPassword = user.Password, newPassword = NewPassword }))
        {
            changed.StatusCode.ShouldBe(HttpStatusCode.NoContent, await changed.Content.ReadAsStringAsync());
        }

        await browser.RefreshAntiforgeryTokenAsync();
        (await AuthenticatedAtAsync(browser)).ShouldBe(first);

        // Proving it again is (the time has a resolution of a second).
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        using (var again = await browser.SignInAsync(user.Email, NewPassword))
        {
            again.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await AuthenticatedAtAsync(browser)).ShouldBeGreaterThan(first);
    }

    private static async Task<DateTimeOffset> AuthenticatedAtAsync(ApiClient browser) =>
        DateTimeOffset.Parse(
            (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("user").GetProperty("authenticatedAtUtc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);

    private async Task<ApiClient> SignedInBrowserAsync(TestUser user)
    {
        var browser = await Scenario.ClientAsync(_factory, allowRedirects: false);
        using var signedIn = await browser.SignInAsync(user.Email, user.Password);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return browser;
    }

    private static string AuthorizeUrl(string clientId, string extra, out string verifier)
    {
        verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        return $"/connect/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(Scenario.RedirectUri)}"
            + $"&scope=openid%20profile&state=s&code_challenge={challenge}&code_challenge_method=S256{extra}";
    }

    private async Task<JsonElement> RedeemAsync(string clientId, string secret, Uri location, string verifier)
    {
        using var response = await Scenario.TokenAsync(_factory, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = HttpUtility.ParseQueryString(location.Query)["code"]!,
            ["redirect_uri"] = Scenario.RedirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["code_verifier"] = verifier,
        });

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }

    private async Task FillProfileAsync(TestUser user)
    {
        using var browser = await Scenario.SignedInAsync(_factory, user);
        using var saved = await browser.PutAsync("/api/account/profile", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            phoneNumber = "+39 02 1234567",
            addressLine = "Via Roma 1",
            city = "Milano",
            postalCode = "20121",
            country = "IT",
        });

        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
    }

    private async Task<(TestUser User, string ClientId, string Secret)> UserWithApplicationAsync()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("claims"), Scenario.Password);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin);

        using var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { });
        granted.IsSuccessStatusCode.ShouldBeTrue(await granted.Content.ReadAsStringAsync());

        return (user, clientId, secret);
    }
}
