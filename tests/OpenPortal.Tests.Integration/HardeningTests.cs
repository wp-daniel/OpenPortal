using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The protections around every request: browser security headers, rate limits on password checks and on the
/// rest of the API, and the health endpoints a load balancer probes.
/// </summary>
public sealed class HardeningTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private const string Password = "Correct-Horse-9";

    [Fact]
    public async Task Api_answers_carry_the_security_headers_and_are_not_cached()
    {
        using var http = factory.CreateClient();
        using var response = await http.GetAsync("/api/auth/session");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Header(response, "Content-Security-Policy").ShouldContain("script-src 'self'");
        Header(response, "Content-Security-Policy").ShouldContain("frame-ancestors 'none'");
        Header(response, "X-Frame-Options").ShouldBe("DENY");
        Header(response, "X-Content-Type-Options").ShouldBe("nosniff");
        Header(response, "Referrer-Policy").ShouldBe("no-referrer");
        Header(response, "Permissions-Policy").ShouldContain("camera=()");
        Header(response, "Cross-Origin-Opener-Policy").ShouldBe("same-origin");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Errors_get_the_headers_too_and_the_protocol_endpoints_a_policy_that_does_not_break_them()
    {
        using var http = factory.CreateClient();

        using (var missing = await http.GetAsync("/api/does-not-exist"))
        {
            missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            Header(missing, "X-Frame-Options").ShouldBe("DENY");
        }

        using var discovery = await http.GetAsync("/.well-known/openid-configuration");
        discovery.StatusCode.ShouldBe(HttpStatusCode.OK);
        Header(discovery, "Content-Security-Policy").ShouldBe("frame-ancestors 'none'");
        discovery.Headers.Contains("Cross-Origin-Resource-Policy").ShouldBeFalse();

        var document = await discovery.Content.ReadFromJsonAsync<JsonElement>();
        document.GetProperty("scopes_supported").EnumerateArray().Select(scope => scope.GetString()).ShouldContain("roles");
        document.GetProperty("claims_supported").EnumerateArray().Select(claim => claim.GetString()).ShouldContain("groups");
    }

    [Fact]
    public async Task Sign_in_attempts_from_one_address_are_limited_with_retry_after()
    {
        await using var limited = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:SignIn:PermitLimit", "3");
            builder.UseSetting("RateLimiting:SignIn:WindowSeconds", "600");
        });

        using var client = ApiClient.For(limited.CreateClient());
        await client.RefreshAntiforgeryTokenAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var refused = await client.SignInAsync($"nobody-{attempt}@example.com", Password);
            refused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var limitedResponse = await client.SignInAsync("nobody@example.com", Password);

        limitedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limitedResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await limitedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().ShouldBe("http.too_many_requests");
        limitedResponse.Headers.RetryAfter.ShouldNotBeNull();

        // Other endpoints are not held back by the sign-in limit.
        using var session = await client.GetAsync("/api/auth/session");
        session.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_general_limit_covers_the_api_but_not_the_health_probes()
    {
        await using var limited = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:General:PermitLimit", "5");
            builder.UseSetting("RateLimiting:General:WindowSeconds", "600");
        });

        using var http = limited.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var request = 0; request < 7; request++)
        {
            using var response = await http.GetAsync("/api/auth/session");
            statuses.Add(response.StatusCode);
        }

        statuses.Take(5).ShouldAllBe(status => status == HttpStatusCode.OK);
        statuses.Last().ShouldBe(HttpStatusCode.TooManyRequests);

        using var live = await http.GetAsync("/health/live");
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_endpoints_report_the_process_and_every_database()
    {
        using var http = factory.CreateClient();

        using (var live = await http.GetAsync("/health/live"))
        {
            live.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().ShouldBe("Healthy");
        }

        using var ready = await http.GetAsync("/health/ready");
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);

        var report = await ready.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("status").GetString().ShouldBe("Healthy");
        report.GetProperty("checks").EnumerateArray().Select(check => check.GetProperty("name").GetString())
            .ShouldBe(["database-identity", "database-content", "database-access", "database-audit"], ignoreOrder: true);

        using var unknown = await http.GetAsync("/health/unknown");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Behind_a_trusted_proxy_the_forwarded_address_is_the_one_recorded()
    {
        await using var proxied = factory.WithWebHostBuilder(builder => builder.UseSetting("ReverseProxy:Enabled", "true"));

        var email = $"forwarded-{Guid.NewGuid():N}@example.com";
        using (var http = proxied.CreateClient())
        {
            var client = ApiClient.For(http);
            await client.RefreshAntiforgeryTokenAsync();
            http.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");
            (await client.SignInAsync(email, Password)).Dispose();
        }

        var admin = await factory.CreateUserAsync($"admin-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        using var adminClient = await ApiClient.CreateAsync(factory);
        (await adminClient.SignInAsync(admin.Email, admin.Password)).Dispose();

        using var entries = await adminClient.GetAsync($"/api/admin/audit?search={Uri.EscapeDataString(email)}");
        var entry = (await entries.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().Single();
        entry.GetProperty("ipAddress").GetString().ShouldBe("203.0.113.7");
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : string.Empty;
}
