using System.Net;
using System.Text.Json;
using System.Web;
using OpenPortal.Web;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// What a third party's application and a tool without a browser need from the provider: the person's consent (and
/// taking it back from the account), the device flow, and replacing a client secret without downtime.
/// </summary>
public sealed class OidcConsentAndDeviceTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private const string Scope = "openid profile email offline_access";

    [Fact]
    public async Task A_replaced_secret_keeps_working_during_its_overlap_only()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, original) = await Scenario.CreateApplicationAsync(admin);

        var withOverlap = await PostJsonAsync(admin, $"/api/admin/applications/{applicationId}/secret", new { keepPreviousHours = 24 });
        var second = withOverlap.GetProperty("clientSecret").GetString()!;
        withOverlap.GetProperty("previousSecretValidUntilUtc").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddHours(23));

        (await AuthenticatesAsync(clientId, original)).ShouldBeTrue("the replaced secret works during the overlap");
        (await AuthenticatesAsync(clientId, second)).ShouldBeTrue();

        // Replacing it again without an overlap stops every older secret at once (a leak).
        var immediate = await PostJsonAsync(admin, $"/api/admin/applications/{applicationId}/secret", new { });
        var third = immediate.GetProperty("clientSecret").GetString()!;

        (await AuthenticatesAsync(clientId, original)).ShouldBeFalse();
        (await AuthenticatesAsync(clientId, second)).ShouldBeFalse();
        (await AuthenticatesAsync(clientId, third)).ShouldBeTrue();

        using var tooLong = await admin.PostAsync($"/api/admin/applications/{applicationId}/secret", new { keepPreviousHours = 10_000 });
        await Scenario.ExpectErrorAsync(tooLong, HttpStatusCode.BadRequest, "access.secret_overlap_invalid");
    }

    [Fact]
    public async Task An_application_that_asks_for_consent_gets_it_once_and_loses_it_when_taken_back()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin, new { requireConsent = true });
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("consent"), Scenario.Password);
        await GrantAsync(admin, applicationId, user.Id);

        // The first sign-in stops at the consent page.
        var browser = await Scenario.ClientAsync(factory, allowRedirects: false);
        (await browser.SignInAsync(user.Email, user.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await browser.RefreshAntiforgeryTokenAsync();

        var authorize = AuthorizeUrl(clientId);
        using (var first = await browser.GetAsync(authorize.Url))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            first.Headers.Location!.ToString().ShouldStartWith("/consent?app=");
        }

        // Saying no goes back to the application with access_denied.
        using (var denied = await PostConsentAsync(browser, authorize, "denied"))
        {
            denied.Headers.Location!.ToString().ShouldStartWith(Scenario.RedirectUri);
            denied.Headers.Location!.Query.ShouldContain("error=access_denied");
        }

        // Saying yes issues the code; the next sign-in does not ask again.
        using (var granted = await PostConsentAsync(browser, authorize, "granted"))
        {
            HttpUtility.ParseQueryString(granted.Headers.Location!.Query)["code"].ShouldNotBeNullOrEmpty();
        }

        using (var again = await browser.GetAsync(AuthorizeUrl(clientId).Url))
        {
            HttpUtility.ParseQueryString(again.Headers.Location!.Query)["code"].ShouldNotBeNullOrEmpty();
        }

        // The account lists the application and can take the permission back; then it asks again.
        var authorized = await Scenario.GetJsonAsync(browser, "/api/account/authorized-applications");
        authorized.EnumerateArray().Single().GetProperty("clientId").GetString().ShouldBe(clientId);

        using (var revoked = await browser.DeleteAsync($"/api/account/authorized-applications/{clientId}"))
        {
            revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await Scenario.GetJsonAsync(browser, "/api/account/authorized-applications")).GetArrayLength().ShouldBe(0);

        using (var asksAgain = await browser.GetAsync(AuthorizeUrl(clientId).Url))
        {
            asksAgain.Headers.Location!.ToString().ShouldStartWith("/consent?app=");
        }

        _ = secret;
        browser.Dispose();
    }

    [Fact]
    public async Task A_tool_signs_in_with_the_device_flow_once_the_person_approves_its_code()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin, new { allowDeviceFlow = true });
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("device"), Scenario.Password);
        await GrantAsync(admin, applicationId, user.Id);

        using var tool = factory.CreateClient();
        using var started = await tool.PostAsync("/connect/device", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = Scope,
        }));
        var device = await Scenario.JsonAsync(started, HttpStatusCode.OK);
        var deviceCode = device.GetProperty("device_code").GetString()!;
        var userCode = device.GetProperty("user_code").GetString()!;
        device.GetProperty("verification_uri").GetString()!.ShouldEndWith("/connect/verify");

        // Before approval the tool is told to keep waiting.
        using (var pending = await PollAsync(tool, clientId, secret, deviceCode))
        {
            pending.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await pending.Content.ReadAsStringAsync()).ShouldContain("authorization_pending");
        }

        var browser = await Scenario.ClientAsync(factory, allowRedirects: false);
        (await browser.SignInAsync(user.Email, user.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await browser.RefreshAntiforgeryTokenAsync();

        using (var shown = await browser.GetAsync($"/connect/verify?user_code={Uri.EscapeDataString(userCode)}"))
        {
            shown.Headers.Location!.ToString().ShouldStartWith("/device?user_code=");
        }

        using (var wrong = await browser.GetAsync("/connect/verify?user_code=NOPE-NOPE"))
        {
            wrong.Headers.Location!.ToString().ShouldBe("/device?error=invalid_code");
        }

        using (var approved = await browser.PostFormAsync("/connect/verify", new Dictionary<string, string>
               {
                   ["user_code"] = userCode,
                   ["decision"] = "approve",
                   ["__RequestVerificationToken"] = await TokenAsync(browser),
               }))
        {
            approved.Headers.Location!.ToString().ShouldBe("/device?status=approved");
        }

        using var tokens = await PollAsync(tool, clientId, secret, deviceCode);
        var issued = await Scenario.JsonAsync(tokens, HttpStatusCode.OK);
        Scenario.ReadJwtPayload(issued.GetProperty("id_token").GetString()!).GetProperty("sub").GetString().ShouldBe(user.Id.ToString());
        browser.Dispose();
    }

    [Fact]
    public async Task An_application_without_the_device_flow_cannot_start_one()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (_, clientId, secret) = await Scenario.CreateApplicationAsync(admin);

        using var tool = factory.CreateClient();
        using var refused = await tool.PostAsync("/connect/device", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = "openid",
        }));

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("unauthorized_client");
    }

    /// <summary>Whether the token endpoint accepts the client's credentials (a revocation of nothing needs only that).</summary>
    private async Task<bool> AuthenticatesAsync(string clientId, string secret)
    {
        using var http = factory.CreateClient();
        using var response = await http.PostAsync("/connect/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["token"] = "not-a-token",
        }));

        return response.StatusCode == HttpStatusCode.OK;
    }

    private static (string Url, string Verifier) AuthorizeUrl(string clientId)
    {
        var verifier = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var challenge = System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));

        return ("/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + "&response_type=code"
            + $"&redirect_uri={Uri.EscapeDataString(Scenario.RedirectUri)}"
            + $"&scope={Uri.EscapeDataString(Scope)}"
            + "&state=s"
            + $"&code_challenge={challenge}"
            + "&code_challenge_method=S256", verifier);
    }

    private static async Task<HttpResponseMessage> PostConsentAsync(ApiClient browser, (string Url, string Verifier) authorize, string answer)
    {
        var fields = HttpUtility.ParseQueryString(new Uri("http://localhost" + authorize.Url).Query);
        var form = fields.AllKeys.ToDictionary(key => key!, key => fields[key]!);
        form["consent"] = answer;
        form["__RequestVerificationToken"] = await TokenAsync(browser);

        return await browser.PostFormAsync("/connect/authorize", form);
    }

    private static async Task<string> TokenAsync(ApiClient browser)
    {
        using var response = await browser.GetAsync("/api/auth/antiforgery");

        return (await Scenario.JsonAsync(response, HttpStatusCode.OK)).GetProperty("requestToken").GetString()!;
    }

    private static Task<HttpResponseMessage> PollAsync(HttpClient tool, string clientId, string secret, string deviceCode) =>
        tool.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["device_code"] = deviceCode,
        }));

    private static async Task GrantAsync(ApiClient admin, Guid applicationId, Guid userId)
    {
        using var response = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{userId}", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> PostJsonAsync(ApiClient client, string path, object body)
    {
        using var response = await client.PostAsync(path, body);

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }
}
