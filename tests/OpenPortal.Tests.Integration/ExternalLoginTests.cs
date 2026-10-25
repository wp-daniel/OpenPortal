using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Web.Authentication;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Signing in with Google and Microsoft. The providers themselves are not reachable from a test, so the round trip is
/// cut where the handler hands over: a test endpoint puts an external identity in Identity's external cookie exactly
/// as the handler would (claims already normalised), and the tests then follow the callback. The challenge is checked
/// against a stub of the provider's discovery document, and the claim rules against the handler's own normaliser.
/// The tests share one settings table, so each configures the provider it uses first.
/// </summary>
public sealed class ExternalLoginTests : IClassFixture<OpenPortalFactory>, IAsyncDisposable
{
    private const string SettingsPath = "/api/admin/settings/external-logins";

    private readonly OpenPortalFactory _factory;
    private readonly WebApplicationFactory<Program> _host;

    public ExternalLoginTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, FakeProviderReturn>();
            services.ConfigureAll<OpenIdConnectOptions>(options => options.BackchannelHttpHandler = new FakeDiscovery());
        }));
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Providers_are_configured_by_administrators_only_and_never_return_the_secret()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);

        var providers = await Scenario.GetJsonAsync(admin, SettingsPath);
        providers.EnumerateArray().Select(provider => provider.GetProperty("key").GetString()).ShouldBe(["google", "microsoft"]);
        providers[0].GetProperty("callbackPath").GetString().ShouldBe("/signin-google");

        using (var incomplete = await admin.PutAsync($"{SettingsPath}/microsoft", new { enabled = true, clientId = "abc" }))
        {
            await Scenario.ExpectErrorAsync(incomplete, HttpStatusCode.BadRequest, "identity.external_provider_incomplete");
        }

        using (var badTenant = await admin.PutAsync($"{SettingsPath}/microsoft", new { enabled = false, tenant = "not a tenant" }))
        {
            await Scenario.ExpectErrorAsync(badTenant, HttpStatusCode.BadRequest, "identity.external_tenant_invalid");
        }

        using (var saved = await admin.PutAsync($"{SettingsPath}/google", Google("open")))
        {
            var text = await saved.Content.ReadAsStringAsync();
            saved.StatusCode.ShouldBe(HttpStatusCode.OK, text);
            text.ShouldNotContain("google-secret");
            var body = JsonDocument.Parse(text).RootElement;
            body.GetProperty("hasClientSecret").GetBoolean().ShouldBeTrue();
            body.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        }

        using var anonymous = await Scenario.ClientAsync(_host);
        var session = await Scenario.GetJsonAsync(anonymous, "/api/auth/session");
        session.GetProperty("registration").GetProperty("externalProviders").EnumerateArray()
            .Select(provider => provider.GetProperty("key").GetString())
            .ShouldContain("google");

        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("plain"), Scenario.Password);
        using var notAdmin = await Scenario.SignedInAsync(_host, user);
        using var forbidden = await notAdmin.GetAsync(SettingsPath);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_challenge_sends_the_browser_to_the_provider_with_pkce()
    {
        await ConfigureGoogleAsync("disabled");

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        using var response = await browser.PostFormAsync("/api/auth/external/google", new Dictionary<string, string>
        {
            ["returnUrl"] = "/connect/authorize?client_id=crm",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe("https://accounts.google.com/o/oauth2/v2/auth");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query);
        query["client_id"].ToString().ShouldBe("google-client");
        query["code_challenge_method"].ToString().ShouldBe("S256");
        // Query is the default response mode of the code flow, so the parameter may be left out.
        (query.TryGetValue("response_mode", out var mode) ? mode.ToString() : "query").ShouldBe("query");
        query["redirect_uri"].ToString().ShouldEndWith("/signin-google");
        query["scope"].ToString().ShouldContain("email");
    }

    [Fact]
    public async Task A_provider_that_is_off_cannot_be_started()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        using (var off = await admin.PutAsync($"{SettingsPath}/microsoft", new { enabled = false }))
        {
            off.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        using var response = await browser.PostFormAsync("/api/auth/external/microsoft", new Dictionary<string, string>());

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/sign-in?externalError=identity.external_provider_disabled");
    }

    [Fact]
    public async Task The_challenge_needs_the_antiforgery_token()
    {
        await ConfigureGoogleAsync("disabled");

        using var http = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await http.PostAsync("/api/auth/external/google", new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_person_gets_an_account_when_the_provider_creates_them()
    {
        await ConfigureGoogleAsync("open");
        var email = Scenario.UniqueEmail("ada.lovelace");
        var subject = Subject();

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(browser, "google", subject, email, verified: true, given: "Ada", family: "Lovelace");

        using var callback = await browser.GetAsync("/api/auth/external/callback?returnUrl=%2Faccount");
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.ToString().ShouldBe("/account");

        var session = await Scenario.GetJsonAsync(browser, "/api/auth/session");
        session.GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
        var user = session.GetProperty("user");
        user.GetProperty("email").GetString().ShouldBe(email);
        user.GetProperty("displayName").GetString().ShouldBe("Ada Lovelace");
        user.GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
        user.GetProperty("authenticationMethods").EnumerateArray().Select(value => value.GetString()).ShouldBe(["fed"]);

        await browser.RefreshAntiforgeryTokenAsync();
        var logins = await Scenario.GetJsonAsync(browser, "/api/account/external-logins");
        logins.GetProperty("hasPassword").GetBoolean().ShouldBeFalse();
        logins.GetProperty("logins")[0].GetProperty("provider").GetString().ShouldBe("google");
        logins.GetProperty("logins")[0].GetProperty("accountName").GetString().ShouldBe(email);

        // The same external account signs in to the same portal account next time, even with another address.
        using var again = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(again, "google", subject, Scenario.UniqueEmail("renamed"), verified: true);
        using (var second = await again.GetAsync("/api/auth/external/callback"))
        {
            second.Headers.Location!.ToString().ShouldBe("/");
        }

        (await Scenario.GetJsonAsync(again, "/api/auth/session")).GetProperty("user").GetProperty("id").GetGuid()
            .ShouldBe(user.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task An_unknown_person_is_refused_when_the_provider_does_not_create_accounts()
    {
        await ConfigureGoogleAsync("disabled");

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(browser, "google", Subject(), Scenario.UniqueEmail("stranger"), verified: true);

        using var callback = await browser.GetAsync("/api/auth/external/callback");
        callback.Headers.Location!.ToString().ShouldBe("/sign-in?externalError=identity.external_sign_up_closed");
        (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task New_accounts_are_limited_to_the_allowed_domains_and_to_verified_addresses()
    {
        await ConfigureGoogleAsync("open", domains: ["example.org"]);

        using (var browser = await Scenario.ClientAsync(_host, allowRedirects: false))
        {
            await ReturnFromProviderAsync(browser, "google", Subject(), Scenario.UniqueEmail("outsider"), verified: true);
            using var callback = await browser.GetAsync("/api/auth/external/callback");
            callback.Headers.Location!.ToString().ShouldBe("/sign-in?externalError=identity.external_domain_not_allowed");
        }

        await ConfigureGoogleAsync("open");

        using var unverified = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(unverified, "google", Subject(), Scenario.UniqueEmail("unverified"), verified: false);
        using var refused = await unverified.GetAsync("/api/auth/external/callback");
        refused.Headers.Location!.ToString().ShouldBe("/sign-in?externalError=identity.external_email_not_verified");
    }

    [Fact]
    public async Task With_approval_the_new_account_waits_and_cannot_sign_in()
    {
        await ConfigureGoogleAsync("approval");
        var email = Scenario.UniqueEmail("waiting");
        var subject = Subject();

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(browser, "google", subject, email, verified: true);
        using var callback = await browser.GetAsync("/api/auth/external/callback");
        callback.Headers.Location!.ToString().ShouldBe("/sign-in?notice=approval_pending");

        // The account exists now: signing in again is refused with the reason.
        using var retry = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(retry, "google", subject, email, verified: true);
        using var again = await retry.GetAsync("/api/auth/external/callback");
        again.Headers.Location!.ToString().ShouldBe("/sign-in?externalError=identity.account_pending_approval");
    }

    [Fact]
    public async Task An_existing_account_is_connected_by_a_verified_address_but_not_by_an_unverified_one()
    {
        await ConfigureGoogleAsync("disabled");
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("existing"), Scenario.Password);

        using (var unverified = await Scenario.ClientAsync(_host, allowRedirects: false))
        {
            await ReturnFromProviderAsync(unverified, "google", Subject(), user.Email, verified: false);
            using var refused = await unverified.GetAsync("/api/auth/external/callback");
            refused.Headers.Location!.ToString().ShouldBe("/sign-in?externalError=identity.external_email_unverified");
        }

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(browser, "google", Subject(), user.Email.ToUpperInvariant(), verified: true);
        using var callback = await browser.GetAsync("/api/auth/external/callback");
        callback.Headers.Location!.ToString().ShouldBe("/");

        var session = await Scenario.GetJsonAsync(browser, "/api/auth/session");
        session.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(user.Id);

        await browser.RefreshAntiforgeryTokenAsync();
        var logins = await Scenario.GetJsonAsync(browser, "/api/account/external-logins");
        logins.GetProperty("hasPassword").GetBoolean().ShouldBeTrue();
        logins.GetProperty("logins").GetArrayLength().ShouldBe(1);
        (await Scenario.NotificationTypesAsync(browser)).ShouldContain("account.external_login_added");
    }

    [Fact]
    public async Task An_account_with_an_authenticator_app_is_asked_for_its_code()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        using (var security = await admin.PutAsync("/api/admin/settings/security", new
               {
                   twoFactorEnabled = true,
                   twoFactorIssuer = "OpenPortal",
                   passkeysEnabled = true,
                   twoFactorRequirement = "none",
               }))
        {
            security.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await ConfigureGoogleAsync("disabled");
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("totp"), Scenario.Password);
        using (var scope = _host.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenPortal.Identity.Domain.Users.ApplicationUser>>();
            var account = await users.FindByIdAsync(user.Id.ToString());
            (await users.SetTwoFactorEnabledAsync(account!, true)).Succeeded.ShouldBeTrue();
        }

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(browser, "google", Subject(), user.Email, verified: true);
        using var callback = await browser.GetAsync("/api/auth/external/callback?returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dcrm");

        callback.Headers.Location!.ToString().ShouldBe("/sign-in?step=two-factor&returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dcrm");
        (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_return_url_to_another_site_is_ignored()
    {
        await ConfigureGoogleAsync("open");

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(browser, "google", Subject(), Scenario.UniqueEmail("redirect"), verified: true);
        using var callback = await browser.GetAsync("/api/auth/external/callback?returnUrl=%2F%2Fevil.example.com%2F");

        callback.Headers.Location!.ToString().ShouldBe("/");
    }

    [Fact]
    public async Task The_owner_connects_and_disconnects_an_account_and_cannot_take_another_ones()
    {
        await ConfigureGoogleAsync("disabled");
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("linker"), Scenario.Password);
        var other = await _factory.CreateUserAsync(Scenario.UniqueEmail("other"), Scenario.Password);
        var subject = Subject();

        using var client = await SignedInWithoutRedirectsAsync(user);
        var before = await Scenario.GetJsonAsync(client, "/api/account/external-logins");
        before.GetProperty("available").EnumerateArray().Select(provider => provider.GetProperty("key").GetString()).ShouldContain("google");

        // Started by this account (the challenge names it), finished in this account.
        await ReturnFromProviderAsync(client, "google", subject, "someone@gmail.example", verified: false, linkFor: user.Id);
        using (var linked = await client.Http.GetAsync("/api/account/external-logins/callback"))
        {
            linked.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            linked.Headers.Location!.ToString().ShouldBe("/account?tab=security&external=linked");
        }

        await client.RefreshAntiforgeryTokenAsync();
        var after = await Scenario.GetJsonAsync(client, "/api/account/external-logins");
        after.GetProperty("logins")[0].GetProperty("accountName").GetString().ShouldBe("someone@gmail.example");

        // The same external account cannot be attached to a second portal account.
        using (var otherClient = await SignedInWithoutRedirectsAsync(other))
        {
            await ReturnFromProviderAsync(otherClient, "google", subject, "someone@gmail.example", verified: true, linkFor: other.Id);
            using var taken = await otherClient.Http.GetAsync("/api/account/external-logins/callback");
            taken.Headers.Location!.ToString().ShouldBe("/account?tab=security&externalError=identity.external_login_in_use");

            // A connection started for someone else is not finished in this account.
            await ReturnFromProviderAsync(otherClient, "google", Subject(), "x@gmail.example", verified: true, linkFor: user.Id);
            using var forged = await otherClient.Http.GetAsync("/api/account/external-logins/callback");
            forged.Headers.Location!.ToString().ShouldBe("/account?tab=security&externalError=identity.external_sign_in_failed");
        }

        // The account has a password, so the provider can go.
        using var removed = await client.DeleteAsync("/api/account/external-logins/google");
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Scenario.GetJsonAsync(client, "/api/account/external-logins")).GetProperty("logins").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task An_account_without_a_password_keeps_its_last_way_in_until_it_sets_a_password()
    {
        await ConfigureGoogleAsync("open");

        using var browser = await Scenario.ClientAsync(_host, allowRedirects: false);
        await ReturnFromProviderAsync(browser, "google", Subject(), Scenario.UniqueEmail("passwordless"), verified: true);
        using (var callback = await browser.GetAsync("/api/auth/external/callback"))
        {
            callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        await browser.RefreshAntiforgeryTokenAsync();
        using (var refused = await browser.DeleteAsync("/api/account/external-logins/google"))
        {
            await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Conflict, "identity.external_login_last_method");
        }

        using (var set = await browser.PostAsync("/api/account/set-password", new { newPassword = Scenario.Password }))
        {
            set.StatusCode.ShouldBe(HttpStatusCode.NoContent, await set.Content.ReadAsStringAsync());
        }

        using (var twice = await browser.PostAsync("/api/account/set-password", new { newPassword = Scenario.Password }))
        {
            await Scenario.ExpectErrorAsync(twice, HttpStatusCode.Conflict, "identity.password_already_set");
        }

        using var removed = await browser.DeleteAsync("/api/account/external-logins/google");
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_administrator_sees_and_disconnects_an_accounts_external_logins()
    {
        await ConfigureGoogleAsync("disabled");
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("managed"), Scenario.Password);

        using (var browser = await Scenario.ClientAsync(_host, allowRedirects: false))
        {
            await ReturnFromProviderAsync(browser, "google", Subject(), user.Email, verified: true);
            using var callback = await browser.GetAsync("/api/auth/external/callback");
            callback.Headers.Location!.ToString().ShouldBe("/");
        }

        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var logins = await Scenario.GetJsonAsync(admin, $"/api/admin/users/{user.Id}/external-logins");
        logins.GetArrayLength().ShouldBe(1);

        using (var removed = await admin.DeleteAsync($"/api/admin/users/{user.Id}/external-logins/google"))
        {
            removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await Scenario.GetJsonAsync(admin, $"/api/admin/users/{user.Id}/external-logins")).GetArrayLength().ShouldBe(0);

        using var missing = await admin.DeleteAsync($"/api/admin/users/{user.Id}/external-logins/google");
        await Scenario.ExpectErrorAsync(missing, HttpStatusCode.NotFound, "identity.external_login_not_found");
    }

    [Theory]
    [InlineData(ExternalLogins.MicrosoftConsumersTenantId, null, false, true)]
    [InlineData("72f988bf-86f1-41af-91ab-2d7cd011db47", null, false, false)]
    [InlineData("72f988bf-86f1-41af-91ab-2d7cd011db47", "true", false, true)]
    [InlineData("72f988bf-86f1-41af-91ab-2d7cd011db47", null, true, true)]
    public void Microsoft_addresses_are_trusted_only_where_ownership_is_proven(string tenant, string? edov, bool singleTenant, bool expected)
    {
        var claims = new List<Claim>
        {
            new("tid", tenant),
            new("oid", "00000000-0000-0000-0000-0000000000aa"),
            new("email", "someone@contoso.example"),
            new("name", "Some One"),
        };
        if (edov is not null)
        {
            claims.Add(new Claim("xms_edov", edov));
        }

        var settings = new OpenPortal.Identity.Application.Abstractions.ExternalProviderConfiguration(
            "microsoft", "id", "secret", singleTenant ? "contoso.example" : "organizations", singleTenant);

        var principal = ExternalLoginOptionsSetup.Normalise("microsoft", settings, new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));

        principal.FindFirst(ExternalLoginClaimTypes.Subject)!.Value.ShouldBe($"{tenant}:00000000-0000-0000-0000-0000000000aa");
        principal.FindFirst(ExternalLoginClaimTypes.EmailVerified)!.Value.ShouldBe(expected ? "true" : "false");
        principal.FindFirst(ExternalLoginClaimTypes.Name)!.Value.ShouldBe("Some One");
    }

    [Fact]
    public void Google_addresses_are_trusted_when_google_says_they_are_verified()
    {
        var principal = ExternalLoginOptionsSetup.Normalise(
            "google",
            null,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "1234"), new Claim("email", "ada@gmail.example"), new Claim("email_verified", "true", ClaimValueTypes.Boolean)],
                "test")));

        principal.FindFirst(ExternalLoginClaimTypes.Subject)!.Value.ShouldBe("1234");
        principal.FindFirst(ExternalLoginClaimTypes.EmailVerified)!.Value.ShouldBe("true");
    }

    private async Task<ApiClient> SignedInWithoutRedirectsAsync(TestUser user)
    {
        var client = await Scenario.ClientAsync(_host, allowRedirects: false);
        using var response = await client.SignInAsync(user.Email, user.Password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return client;
    }

    private async Task ConfigureGoogleAsync(string signUp, string[]? domains = null)
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        using var saved = await admin.PutAsync($"{SettingsPath}/google", Google(signUp, domains));
        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        admin.Dispose();
    }

    private static object Google(string signUp, string[]? domains = null) => new
    {
        enabled = true,
        clientId = "google-client",
        clientSecret = "google-secret",
        signUp,
        allowedDomains = domains ?? [],
    };

    private static string Subject() => Guid.NewGuid().ToString("N");

    /// <summary>Puts an external identity in the browser's external cookie, as the provider's handler would.</summary>
    private static async Task ReturnFromProviderAsync(
        ApiClient browser,
        string provider,
        string subject,
        string email,
        bool verified,
        string? given = null,
        string? family = null,
        Guid? linkFor = null)
    {
        var query = new Dictionary<string, string?>
        {
            ["provider"] = provider,
            ["subject"] = subject,
            ["email"] = email,
            ["verified"] = verified ? "true" : "false",
            ["given"] = given,
            ["family"] = family,
            ["link"] = linkFor?.ToString(),
        };

        using var response = await browser.Http.GetAsync(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(FakeProviderReturn.Path, query));
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>Adds the test endpoint that stands in for the provider's handler.</summary>
    private sealed class FakeProviderReturn : IStartupFilter
    {
        public const string Path = "/test/external-return";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // Not app.Map: that would make the path a PathBase, and the cookie would be scoped to it.
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path != Path)
                {
                    await nextMiddleware(context);
                    return;
                }

                var query = context.Request.Query;
                var provider = query["provider"].ToString();
                var identity = new ClaimsIdentity(provider);
                identity.AddClaim(new Claim(ExternalLoginClaimTypes.Subject, query["subject"].ToString()));
                identity.AddClaim(new Claim(ExternalLoginClaimTypes.Email, query["email"].ToString()));
                identity.AddClaim(new Claim(ExternalLoginClaimTypes.EmailVerified, query["verified"].ToString()));
                if (!string.IsNullOrEmpty(query["given"]))
                {
                    identity.AddClaim(new Claim(ExternalLoginClaimTypes.GivenName, query["given"].ToString()));
                }

                if (!string.IsNullOrEmpty(query["family"]))
                {
                    identity.AddClaim(new Claim(ExternalLoginClaimTypes.FamilyName, query["family"].ToString()));
                }

                var properties = new AuthenticationProperties();
                properties.Items["LoginProvider"] = provider;
                if (!string.IsNullOrEmpty(query["link"]))
                {
                    properties.Items["XsrfId"] = query["link"].ToString();
                }

                await context.SignInAsync(IdentityConstants.ExternalScheme, new ClaimsPrincipal(identity), properties);
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            });

            next(app);
        };
    }

    /// <summary>Answers the providers' discovery documents, so a challenge can be built without the network.</summary>
    private sealed class FakeDiscovery : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var issuer = request.RequestUri!.GetLeftPart(UriPartial.Authority)
                + request.RequestUri.AbsolutePath.Replace("/.well-known/openid-configuration", string.Empty, StringComparison.Ordinal);

            var document = request.RequestUri.Host == "accounts.google.com"
                ? new
                {
                    issuer = "https://accounts.google.com",
                    authorization_endpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                    token_endpoint = "https://oauth2.googleapis.com/token",
                    jwks_uri = "https://www.googleapis.com/oauth2/v3/certs",
                }
                : new
                {
                    issuer,
                    authorization_endpoint = issuer.Replace("/v2.0", "/oauth2/v2.0/authorize", StringComparison.Ordinal),
                    token_endpoint = issuer.Replace("/v2.0", "/oauth2/v2.0/token", StringComparison.Ordinal),
                    jwks_uri = issuer.Replace("/v2.0", "/discovery/v2.0/keys", StringComparison.Ordinal),
                };

            var body = request.RequestUri.AbsolutePath.EndsWith("openid-configuration", StringComparison.Ordinal)
                ? JsonSerializer.Serialize(document)
                : """{"keys":[]}""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
