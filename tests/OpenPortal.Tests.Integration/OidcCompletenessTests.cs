using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenPortal.Web.Oidc;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The parts of the OpenID Connect provider an application's API and sign-out rely on: signed access tokens it can
/// validate itself, revocation, introspection, a sign-out no other site can trigger, back-channel logout and public
/// clients (single-page and mobile applications).
/// </summary>
public sealed class OidcCompletenessTests : IClassFixture<OpenPortalFactory>
{
    private const string LogoutUri = "https://crm.example.com/signout-backchannel";

    private readonly OpenPortalFactory _factory;
    private readonly CapturingHandler _backChannel = new();
    private readonly WebApplicationFactory<Program> _host;

    public OidcCompletenessTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient(BackChannelLogoutHandler.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _backChannel)));
    }

    [Fact]
    public async Task Discovery_advertises_revocation_introspection_and_back_channel_logout()
    {
        using var client = _host.CreateClient();
        var discovery = JsonDocument.Parse(await client.GetStringAsync("/.well-known/openid-configuration")).RootElement;

        discovery.GetProperty("revocation_endpoint").GetString()!.ShouldEndWith("/connect/revoke");
        discovery.GetProperty("introspection_endpoint").GetString()!.ShouldEndWith("/connect/introspect");
        discovery.GetProperty("backchannel_logout_supported").GetBoolean().ShouldBeTrue();
        discovery.GetProperty("claims_supported").EnumerateArray().Select(claim => claim.GetString()).ShouldContain("amr");
    }

    [Fact]
    public async Task Access_tokens_are_signed_jwts_an_api_can_validate_with_the_published_keys()
    {
        var (user, clientId, secret) = await UserWithApplicationAsync();
        var tokens = await Scenario.SignInToApplicationAsync(_host, user, clientId, secret, "openid profile offline_access");
        var accessToken = tokens.GetProperty("access_token").GetString()!;

        accessToken.Split('.').Length.ShouldBe(3, "an encrypted token (JWE) has five parts");

        using var http = _host.CreateClient();
        var discovery = JsonDocument.Parse(await http.GetStringAsync("/.well-known/openid-configuration")).RootElement;
        var keys = new JsonWebKeySet(await http.GetStringAsync(discovery.GetProperty("jwks_uri").GetString()));

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            ValidIssuer = discovery.GetProperty("issuer").GetString(),
            ValidateAudience = false,
            IssuerSigningKeys = keys.GetSigningKeys(),
        });

        validation.IsValid.ShouldBeTrue(validation.Exception?.Message);
        validation.ClaimsIdentity.FindFirst("sub")!.Value.ShouldBe(user.Id.ToString());
        validation.ClaimsIdentity.FindFirst("client_id")!.Value.ShouldBe(clientId);
    }

    [Fact]
    public async Task A_revoked_refresh_token_can_no_longer_be_redeemed()
    {
        var (user, clientId, secret) = await UserWithApplicationAsync();
        var tokens = await Scenario.SignInToApplicationAsync(_host, user, clientId, secret, "openid offline_access");
        var refreshToken = tokens.GetProperty("refresh_token").GetString()!;

        using var http = _host.CreateClient();
        using (var revoked = await http.PostAsync("/connect/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = refreshToken,
            ["token_type_hint"] = "refresh_token",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        })))
        {
            revoked.StatusCode.ShouldBe(HttpStatusCode.OK, await revoked.Content.ReadAsStringAsync());
        }

        using var refreshed = await Scenario.RefreshAsync(_host, clientId, secret, refreshToken);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Scenario.JsonAsync(refreshed, HttpStatusCode.BadRequest)).GetProperty("error").GetString().ShouldBe("invalid_grant");
    }

    [Fact]
    public async Task A_confidential_client_can_introspect_its_access_token()
    {
        var (user, clientId, secret) = await UserWithApplicationAsync();
        var tokens = await Scenario.SignInToApplicationAsync(_host, user, clientId, secret, "openid");

        using var http = _host.CreateClient();
        using var response = await http.PostAsync("/connect/introspect", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = tokens.GetProperty("access_token").GetString()!,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }));

        var body = await Scenario.JsonAsync(response, HttpStatusCode.OK);
        body.GetProperty("active").GetBoolean().ShouldBeTrue();
        body.GetProperty("sub").GetString().ShouldBe(user.Id.ToString());
    }

    [Fact]
    public async Task Another_site_cannot_sign_the_user_out_without_a_hint_or_confirmation()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("csrf"), Scenario.Password);
        var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        using (await browser.SignInAsync(user.Email, user.Password))
        {
        }

        // A plain link or image pointing at the end-session endpoint (logout CSRF).
        using var forged = await browser.GetAsync("/connect/endsession");

        forged.StatusCode.ShouldBe(HttpStatusCode.Found);
        forged.Headers.Location!.ToString().ShouldStartWith("/sign-out");
        (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();

        // The confirmation page posts back with the antiforgery token: that ends the session.
        var token = browser.Http.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").Single();
        using var confirmed = await browser.Http.PostAsync("/connect/endsession", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["confirm"] = "true",
            ["__RequestVerificationToken"] = token,
        }));

        confirmed.StatusCode.ShouldBe(HttpStatusCode.Found);
        (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task An_id_token_hint_for_the_signed_in_user_signs_out_at_once()
    {
        var (user, clientId, secret) = await UserWithApplicationAsync();
        var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        using (await browser.SignInAsync(user.Email, user.Password))
        {
        }

        var idToken = (await CodeFlowAsync(browser, clientId, secret)).GetProperty("id_token").GetString()!;

        using var ended = await browser.GetAsync($"/connect/endsession?id_token_hint={Uri.EscapeDataString(idToken)}");

        ended.StatusCode.ShouldBe(HttpStatusCode.Found);
        ended.Headers.Location!.ToString().ShouldNotStartWith("/sign-out");
        (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Signing_out_of_the_portal_sends_the_application_a_signed_logout_token()
    {
        var (user, clientId, secret) = await UserWithApplicationAsync(new { backChannelLogoutUri = LogoutUri });
        var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        using (await browser.SignInAsync(user.Email, user.Password))
        {
        }

        await CodeFlowAsync(browser, clientId, secret);

        await browser.SignOutAsync();

        var posted = _backChannel.Requests.Single(request => request.Uri == LogoutUri);
        var token = HttpUtility.ParseQueryString(posted.Body)["logout_token"]!;

        using var http = _host.CreateClient();
        var discovery = JsonDocument.Parse(await http.GetStringAsync("/.well-known/openid-configuration")).RootElement;
        var keys = new JsonWebKeySet(await http.GetStringAsync(discovery.GetProperty("jwks_uri").GetString()));

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = discovery.GetProperty("issuer").GetString(),
            ValidAudience = clientId,
            IssuerSigningKeys = keys.GetSigningKeys(),
            ValidTypes = ["logout+jwt"],
        });

        validation.IsValid.ShouldBeTrue(validation.Exception?.Message);
        validation.ClaimsIdentity.FindFirst("sub")!.Value.ShouldBe(user.Id.ToString());
        validation.ClaimsIdentity.FindFirst("events")!.Value.ShouldContain("backchannel-logout");
        validation.ClaimsIdentity.FindFirst("nonce").ShouldBeNull();
    }

    [Fact]
    public async Task A_public_client_signs_users_in_with_pkce_and_has_no_secret()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("spa"), Scenario.Password);

        var clientId = $"spa-{Guid.NewGuid():N}"[..20];
        using var created = await admin.PostAsync("/api/admin/applications", new
        {
            clientId,
            displayName = "SPA",
            baseUrl = "https://spa.example.com",
            redirectUris = new[] { Scenario.RedirectUri },
            clientType = "public",
        });
        var json = await Scenario.JsonAsync(created, HttpStatusCode.Created);
        json.GetProperty("clientSecret").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("application").GetProperty("clientType").GetString().ShouldBe("public");
        var applicationId = json.GetProperty("application").GetProperty("id").GetGuid();

        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { }))
        {
            granted.IsSuccessStatusCode.ShouldBeTrue();
        }

        var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        using (await browser.SignInAsync(user.Email, user.Password))
        {
        }

        var tokens = await CodeFlowAsync(browser, clientId, secret: null);
        tokens.GetProperty("access_token").GetString().ShouldNotBeNullOrEmpty();

        // There is no secret to regenerate, and no application API for a client that cannot keep one.
        using var regenerated = await admin.PostAsync($"/api/admin/applications/{applicationId}/secret");
        await Scenario.ExpectErrorAsync(regenerated, HttpStatusCode.Conflict, "access.public_client_has_no_secret");
    }

    private async Task<(TestUser User, string ClientId, string Secret)> UserWithApplicationAsync(object? extra = null)
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("oidc"), Scenario.Password);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin, extra);

        using var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { });
        granted.IsSuccessStatusCode.ShouldBeTrue(await granted.Content.ReadAsStringAsync());

        return (user, clientId, secret);
    }

    /// <summary>The code flow for a browser that is already signed in to the portal.</summary>
    private async Task<JsonElement> CodeFlowAsync(ApiClient browser, string clientId, string? secret)
    {
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        using var authorized = await browser.GetAsync(
            $"/connect/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(Scenario.RedirectUri)}"
            + $"&scope=openid%20offline_access&state=s&code_challenge={challenge}&code_challenge_method=S256");
        authorized.StatusCode.ShouldBe(HttpStatusCode.Found, await authorized.Content.ReadAsStringAsync());

        var code = HttpUtility.ParseQueryString(authorized.Headers.Location!.Query)["code"]!;
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = Scenario.RedirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
        };

        if (secret is not null)
        {
            form["client_secret"] = secret;
        }

        using var response = await Scenario.TokenAsync(_host, form);

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }

    /// <summary>Records what the portal posts to applications, and answers 200.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly List<(string Uri, string Body)> _requests = [];

        public IReadOnlyList<(string Uri, string Body)> Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToList();
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            lock (_requests)
            {
                _requests.Add((request.RequestUri!.ToString(), body));
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
