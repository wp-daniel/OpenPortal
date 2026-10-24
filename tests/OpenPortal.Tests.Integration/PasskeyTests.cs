using System.Net;
using System.Text.Json;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Passkeys (WebAuthn) end to end with a software authenticator, the portal's second-factor requirement, and how
/// the sign-in method reaches applications (<c>amr</c>, <c>acr_values=mfa</c>). The tests of this class share one
/// settings row, so each sets the requirement it needs first.
/// </summary>
public sealed class PasskeyTests : IClassFixture<OpenPortalFactory>
{
    private const string SettingsPath = "/api/admin/settings/security";

    private readonly OpenPortalFactory _factory;

    public PasskeyTests(OpenPortalFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_registered_passkey_signs_in_without_password_and_counts_as_two_factors()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);
        await RequireAsync(admin, "none");

        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("passkey"), Scenario.Password);
        using var authenticator = new SoftwareAuthenticator();

        using (var client = await SignedInWithOriginAsync(user))
        {
            await RegisterAsync(client, authenticator, "Laptop");

            var passkeys = await Scenario.GetJsonAsync(client, "/api/account/passkeys");
            passkeys.GetArrayLength().ShouldBe(1);
            passkeys[0].GetProperty("name").GetString().ShouldBe("Laptop");
        }

        using var browser = await BrowserAsync();
        var session = await PasskeySignInAsync(browser, authenticator);

        session.GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
        session.GetProperty("user").GetProperty("email").GetString().ShouldBe(user.Email);
        var methods = session.GetProperty("user").GetProperty("authenticationMethods").EnumerateArray().Select(value => value.GetString()).ToList();
        methods.ShouldContain("mfa");
        methods.ShouldContain("hwk");
    }

    [Fact]
    public async Task A_forged_signature_does_not_sign_in()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("forged"), Scenario.Password);
        using var authenticator = new SoftwareAuthenticator();

        using (var client = await SignedInWithOriginAsync(user))
        {
            await RegisterAsync(client, authenticator, null);
        }

        using var browser = await BrowserAsync();
        using var options = await browser.PostAsync("/api/auth/passkey/options");
        var optionsJson = await options.Content.ReadAsStringAsync();

        using var signIn = await browser.PostAsync("/api/auth/login/passkey", new { credential = authenticator.Get(optionsJson, tamper: true) });

        await Scenario.ExpectErrorAsync(signIn, HttpStatusCode.Unauthorized, "identity.passkey_sign_in_failed");
        (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task When_required_an_account_without_a_second_factor_can_only_set_one_up()
    {
        using var adminKey = new SoftwareAuthenticator();
        using var admin = await AdminWithPasskeyAsync(adminKey);
        await RequireAsync(admin, "everyone");

        try
        {
            var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("required"), Scenario.Password);
            using var client = await SignedInWithOriginAsync(user);

            (await Scenario.GetJsonAsync(client, "/api/auth/session"))
                .GetProperty("user").GetProperty("twoFactorSetupRequired").GetBoolean().ShouldBeTrue();

            using (var blocked = await client.GetAsync("/api/account/profile"))
            {
                await Scenario.ExpectErrorAsync(blocked, HttpStatusCode.Forbidden, "identity.two_factor_setup_required");
            }

            // Setting up a passkey is allowed, and lifts the restriction.
            using var authenticator = new SoftwareAuthenticator();
            await RegisterAsync(client, authenticator, "Phone");

            using (var allowed = await client.GetAsync("/api/account/profile"))
            {
                allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            // The passkey is now its only second factor: the password alone no longer signs in.
            using var other = await Scenario.ClientAsync(_factory);
            using (var password = await other.SignInAsync(user.Email, user.Password))
            {
                await Scenario.ExpectErrorAsync(password, HttpStatusCode.Unauthorized, "identity.passkey_required");
            }

            // And it cannot be removed while it is the last one.
            var id = (await Scenario.GetJsonAsync(client, "/api/account/passkeys"))[0].GetProperty("id").GetString();
            using var removed = await client.DeleteAsync($"/api/account/passkeys/{id}");
            await Scenario.ExpectErrorAsync(removed, HttpStatusCode.Conflict, "identity.two_factor_last_factor");
        }
        finally
        {
            await RequireAsync(admin, "none");
        }
    }

    [Fact]
    public async Task A_requirement_for_administrators_leaves_other_accounts_alone()
    {
        using var adminKey = new SoftwareAuthenticator();
        using var admin = await AdminWithPasskeyAsync(adminKey);
        await RequireAsync(admin, "administrators");

        try
        {
            var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("plain"), Scenario.Password);
            using var client = await Scenario.SignedInAsync(_factory, user);
            using var profile = await client.GetAsync("/api/account/profile");
            profile.StatusCode.ShouldBe(HttpStatusCode.OK);

            // The administrator who set it has a passkey; another one without a second factor is held back.
            using (var allowed = await admin.GetAsync("/api/admin/users"))
            {
                allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            var (other, _) = await Scenario.AdminAsync(_factory);
            using var blocked = await other.GetAsync("/api/admin/users");
            await Scenario.ExpectErrorAsync(blocked, HttpStatusCode.Forbidden, "identity.two_factor_setup_required");
        }
        finally
        {
            await RequireAsync(admin, "none");
        }
    }

    [Fact]
    public async Task An_administrator_cannot_require_a_second_factor_they_lack()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);

        using var response = await admin.PutAsync(SettingsPath, new
        {
            twoFactorEnabled = true,
            twoFactorIssuer = "OpenPortal",
            passkeysEnabled = true,
            twoFactorRequirement = "administrators",
        });

        await Scenario.ExpectErrorAsync(response, HttpStatusCode.Conflict, "identity.two_factor_requirement_self");
    }

    [Fact]
    public async Task A_requirement_nobody_can_meet_is_refused()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);

        using var response = await admin.PutAsync(SettingsPath, new
        {
            twoFactorEnabled = false,
            twoFactorIssuer = "OpenPortal",
            passkeysEnabled = false,
            twoFactorRequirement = "everyone",
        });

        await Scenario.ExpectErrorAsync(response, HttpStatusCode.BadRequest, "identity.two_factor_requirement_unsatisfiable");
    }

    [Fact]
    public async Task Applications_see_how_the_user_signed_in_and_may_demand_two_factors()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);
        await RequireAsync(admin, "none");

        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("amr"), Scenario.Password);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin);
        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { }))
        {
            granted.IsSuccessStatusCode.ShouldBeTrue(await granted.Content.ReadAsStringAsync());
        }

        // A password sign-in: amr says so.
        var tokens = await Scenario.SignInToApplicationAsync(_factory, user, clientId, secret, "openid profile");
        var idToken = Scenario.ReadJwtPayload(tokens.GetProperty("id_token").GetString()!);
        Amr(idToken).ShouldContain("pwd");
        Amr(idToken).ShouldNotContain("mfa");

        // The same session cannot open an application that asks for two factors.
        var browser = await Scenario.ClientAsync(_factory, allowRedirects: false);
        using (await browser.SignInAsync(user.Email, user.Password))
        {
        }

        using var authorize = await browser.GetAsync(
            $"/connect/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(Scenario.RedirectUri)}"
            + "&scope=openid&state=s&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&acr_values=mfa");

        authorize.StatusCode.ShouldBe(HttpStatusCode.Found);
        authorize.Headers.Location!.ToString().ShouldContain("/access-denied?reason=mfa");
    }

    private static IReadOnlyList<string?> Amr(JsonElement payload) =>
        payload.TryGetProperty("amr", out var amr)
            ? amr.ValueKind == JsonValueKind.Array ? amr.EnumerateArray().Select(value => value.GetString()).ToList() : [amr.GetString()]
            : [];

    private async Task<ApiClient> AdminWithPasskeyAsync(SoftwareAuthenticator authenticator)
    {
        var admin = await _factory.CreateUserAsync(Scenario.UniqueEmail("keyadmin"), Scenario.Password, "Administrator");
        var client = await SignedInWithOriginAsync(admin);
        await RegisterAsync(client, authenticator, "Admin key");

        return client;
    }

    private async Task RequireAsync(ApiClient admin, string requirement)
    {
        using var response = await admin.PutAsync(SettingsPath, new
        {
            twoFactorEnabled = true,
            twoFactorIssuer = "OpenPortal",
            passkeysEnabled = true,
            twoFactorRequirement = requirement,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// <summary>A browser: WebAuthn ceremonies carry the page's origin, which the portal checks.</summary>
    private async Task<ApiClient> BrowserAsync()
    {
        var client = await Scenario.ClientAsync(_factory);
        client.Http.DefaultRequestHeaders.Add("Origin", SoftwareAuthenticator.Origin);

        return client;
    }

    private async Task<ApiClient> SignedInWithOriginAsync(TestUser user)
    {
        var client = await BrowserAsync();
        using var response = await client.SignInAsync(user.Email, user.Password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return client;
    }

    private static async Task RegisterAsync(ApiClient client, SoftwareAuthenticator authenticator, string? name)
    {
        using var options = await client.PostAsync("/api/account/passkeys/creation-options");
        var optionsJson = await options.Content.ReadAsStringAsync();
        options.StatusCode.ShouldBe(HttpStatusCode.OK, optionsJson);

        using var registered = await client.PostAsync("/api/account/passkeys", new { credential = authenticator.Create(optionsJson), name });
        registered.StatusCode.ShouldBe(HttpStatusCode.OK, await registered.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> PasskeySignInAsync(ApiClient browser, SoftwareAuthenticator authenticator)
    {
        using var options = await browser.PostAsync("/api/auth/passkey/options");
        var optionsJson = await options.Content.ReadAsStringAsync();
        options.StatusCode.ShouldBe(HttpStatusCode.OK, optionsJson);

        using var signIn = await browser.PostAsync("/api/auth/login/passkey", new { credential = authenticator.Get(optionsJson) });
        var session = await Scenario.JsonAsync(signIn, HttpStatusCode.OK);
        await browser.RefreshAntiforgeryTokenAsync();

        return session;
    }
}
