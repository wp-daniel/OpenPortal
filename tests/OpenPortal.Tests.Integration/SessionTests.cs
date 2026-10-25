using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Identity.Infrastructure.Persistence;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Sessions kept on the server: listing where an account is signed in, ending one (by its owner or an administrator)
/// with effect on the very next request, the idle timeout, and the alert for a browser the account never used. The
/// tests share one settings row, so the idle timeout is set back to off by the test that changes it.
/// </summary>
public sealed class SessionTests : IClassFixture<OpenPortalFactory>
{
    private const string Firefox = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:131.0) Gecko/20100101 Firefox/131.0";
    private const string SafariPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";

    private readonly OpenPortalFactory _factory;

    public SessionTests(OpenPortalFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Each_sign_in_is_a_session_listed_with_its_browser()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("sessions"), Scenario.Password);
        using var laptop = await SignedInAsync(user, Firefox);
        using var phone = await SignedInAsync(user, SafariPhone);

        var sessions = await Scenario.GetJsonAsync(laptop, "/api/account/sessions");
        var active = sessions.GetProperty("active").EnumerateArray().ToList();

        active.Count.ShouldBe(2);
        active[0].GetProperty("current").GetBoolean().ShouldBeTrue();
        active[0].GetProperty("device").GetString().ShouldBe("Firefox on Windows");
        active[1].GetProperty("device").GetString().ShouldBe("Safari on iOS");
        active[0].GetProperty("methods").EnumerateArray().Select(value => value.GetString()).ShouldContain("pwd");
    }

    [Fact]
    public async Task Ending_another_session_signs_that_browser_out_at_once()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("revoke"), Scenario.Password);
        using var laptop = await SignedInAsync(user, Firefox);
        using var phone = await SignedInAsync(user, SafariPhone);

        var phoneSession = await CurrentSessionIdAsync(phone);

        using (var current = await laptop.DeleteAsync($"/api/account/sessions/{await CurrentSessionIdAsync(laptop)}"))
        {
            await Scenario.ExpectErrorAsync(current, HttpStatusCode.Conflict, "identity.session_is_current");
        }

        using (var revoked = await laptop.DeleteAsync($"/api/account/sessions/{phoneSession}"))
        {
            revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var refused = await phone.GetAsync("/api/account/profile"))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var sessions = await Scenario.GetJsonAsync(laptop, "/api/account/sessions");
        sessions.GetProperty("active").GetArrayLength().ShouldBe(1);
        sessions.GetProperty("recent").EnumerateArray()
            .ShouldContain(session => session.GetProperty("id").GetString() == phoneSession
                && session.GetProperty("endReason").GetString() == "revoked");

        using var again = await laptop.DeleteAsync($"/api/account/sessions/{phoneSession}");
        await Scenario.ExpectErrorAsync(again, HttpStatusCode.NotFound, "identity.session_not_found");
    }

    [Fact]
    public async Task Signing_out_everywhere_else_keeps_only_this_session()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("others"), Scenario.Password);
        using var first = await SignedInAsync(user, Firefox);
        using var second = await SignedInAsync(user, SafariPhone);
        using var third = await SignedInAsync(user, SafariPhone);

        using (var revoked = await first.PostAsync("/api/account/sessions/revoke-others"))
        {
            (await Scenario.JsonAsync(revoked, HttpStatusCode.OK)).GetProperty("count").GetInt32().ShouldBe(2);
        }

        (await second.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await third.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await first.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Signing_out_ends_the_session_and_changing_the_password_ends_the_others()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("password"), Scenario.Password);
        using var laptop = await SignedInAsync(user, Firefox);
        using var phone = await SignedInAsync(user, SafariPhone);
        using var tablet = await SignedInAsync(user, SafariPhone);

        await tablet.SignOutAsync();

        using (var changed = await laptop.PostAsync("/api/account/change-password", new { currentPassword = Scenario.Password, newPassword = "Another-Horse-42" }))
        {
            changed.StatusCode.ShouldBe(HttpStatusCode.NoContent, await changed.Content.ReadAsStringAsync());
        }

        (await phone.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The session that changed the password goes on (re-issued under a new id, still one sign-in).
        var sessions = await Scenario.GetJsonAsync(laptop, "/api/account/sessions");
        sessions.GetProperty("active").GetArrayLength().ShouldBe(1);
        var reasons = sessions.GetProperty("recent").EnumerateArray().Select(session => session.GetProperty("endReason").GetString()).ToList();
        reasons.ShouldContain("signed_out");
        reasons.ShouldContain("revoked");
    }

    [Fact]
    public async Task An_administrator_lists_and_ends_someones_sessions_and_nobody_else_can()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("managed"), Scenario.Password);
        using var browser = await SignedInAsync(user, Firefox);
        var (admin, _) = await Scenario.AdminAsync(_factory);

        var sessions = await Scenario.GetJsonAsync(admin, $"/api/admin/users/{user.Id}/sessions");
        var id = sessions.GetProperty("active")[0].GetProperty("id").GetString();
        sessions.GetProperty("active")[0].GetProperty("current").GetBoolean().ShouldBeFalse();

        using (var revoked = await admin.DeleteAsync($"/api/admin/users/{user.Id}/sessions/{id}"))
        {
            revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await browser.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var other = await _factory.CreateUserAsync(Scenario.UniqueEmail("nosy"), Scenario.Password);
        using var nosy = await Scenario.SignedInAsync(_factory, other);
        (await nosy.GetAsync($"/api/admin/users/{user.Id}/sessions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_session_idle_for_longer_than_the_timeout_ends_and_background_calls_do_not_keep_it_alive()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);
        await SetIdleAsync(admin, 5);

        try
        {
            var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("idle"), Scenario.Password);
            using var browser = await SignedInAsync(user, Firefox);
            var id = await CurrentSessionIdAsync(browser);

            (await Scenario.GetJsonAsync(browser, "/api/auth/session")).GetProperty("features").GetProperty("idleTimeoutMinutes").GetInt32().ShouldBe(5);

            // Three minutes of silence: the bell polling does not count as activity, a real request does.
            await SetLastSeenAsync(id, TimeSpan.FromMinutes(3));
            (await browser.GetAsync("/api/notifications/count")).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await LastSeenAsync(id)).ShouldBeLessThan(DateTimeOffset.UtcNow.AddMinutes(-2));
            (await browser.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await LastSeenAsync(id)).ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));

            await SetLastSeenAsync(id, TimeSpan.FromMinutes(6));
            (await browser.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            var sessions = await Scenario.GetJsonAsync(admin, $"/api/admin/users/{user.Id}/sessions");
            sessions.GetProperty("recent").EnumerateArray()
                .ShouldContain(session => session.GetProperty("id").GetString() == id && session.GetProperty("endReason").GetString() == "idle");
        }
        finally
        {
            await SetIdleAsync(admin, 0);
        }
    }

    [Fact]
    public async Task A_sign_in_from_a_browser_the_account_never_used_is_announced()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("newdevice"), Scenario.Password);

        using (var first = await SignedInAsync(user, Firefox))
        {
            (await Scenario.NotificationTypesAsync(first)).ShouldNotContain("account.new_sign_in");
        }

        using (var same = await SignedInAsync(user, Firefox))
        {
            (await Scenario.NotificationTypesAsync(same)).ShouldNotContain("account.new_sign_in");
        }

        using var phone = await SignedInAsync(user, SafariPhone);
        (await Scenario.NotificationTypesAsync(phone)).ShouldContain("account.new_sign_in");
    }

    [Fact]
    public async Task Idle_timeouts_outside_the_range_are_refused()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);

        using var refused = await admin.PutAsync("/api/admin/settings/security", Security(2));
        await Scenario.ExpectErrorAsync(refused, HttpStatusCode.BadRequest, "identity.idle_timeout_invalid");
    }

    private async Task<ApiClient> SignedInAsync(TestUser user, string userAgent)
    {
        var client = await Scenario.ClientAsync(_factory);
        client.Http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        using var response = await client.SignInAsync(user.Email, user.Password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return client;
    }

    private static async Task<string> CurrentSessionIdAsync(ApiClient client)
    {
        var sessions = await Scenario.GetJsonAsync(client, "/api/account/sessions");

        return sessions.GetProperty("active").EnumerateArray().Single(session => session.GetProperty("current").GetBoolean())
            .GetProperty("id").GetString()!;
    }

    private static object Security(int idle) => new
    {
        twoFactorEnabled = false,
        twoFactorIssuer = "OpenPortal",
        passkeysEnabled = true,
        twoFactorRequirement = "none",
        idleTimeoutMinutes = idle,
    };

    private static async Task SetIdleAsync(ApiClient admin, int minutes)
    {
        using var saved = await admin.PutAsync("/api/admin/settings/security", Security(minutes));
        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
    }

    private async Task SetLastSeenAsync(string sessionId, TimeSpan ago)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var session = await db.UserSessions.SingleAsync(row => row.Id == sessionId);
        db.Entry(session).Property(row => row.LastSeenAtUtc).CurrentValue = DateTimeOffset.UtcNow - ago;
        await db.SaveChangesAsync();
    }

    private async Task<DateTimeOffset> LastSeenAsync(string sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        return (await db.UserSessions.AsNoTracking().SingleAsync(row => row.Id == sessionId)).LastSeenAtUtc;
    }
}
