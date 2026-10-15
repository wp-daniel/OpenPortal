using System.Net;
using System.Text.Json;
using OpenPortal.Identity.Application.Notifications;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Support sessions: an administrator acting as an ordinary account, marked as such, audited with the administrator
/// as actor, kept away from the account's sensitive settings and from the applications.
/// </summary>
public sealed class ImpersonationTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task An_administrator_acts_as_a_user_and_comes_back()
    {
        var (admin, adminUser) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("customer"), Scenario.Password);

        await StartAsync(admin, user.Id);

        var session = await Scenario.GetJsonAsync(admin, "/api/auth/session");
        session.GetProperty("user").GetProperty("email").GetString().ShouldBe(user.Email);
        var impersonator = session.GetProperty("user").GetProperty("impersonator");
        impersonator.GetProperty("id").GetGuid().ShouldBe(adminUser.Id);
        impersonator.GetProperty("email").GetString().ShouldBe(adminUser.Email);

        // A change made in the session is the administrator's, on behalf of the user.
        using (var profile = await admin.PutAsync("/api/account/profile", new { firstName = "Changed", lastName = "BySupport" }))
        {
            profile.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var stop = await admin.PostAsync("/api/auth/impersonation/stop"))
        {
            stop.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await admin.RefreshAntiforgeryTokenAsync();
        var back = await Scenario.GetJsonAsync(admin, "/api/auth/session");
        back.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(adminUser.Id);
        back.GetProperty("user").GetProperty("impersonator").ValueKind.ShouldBe(JsonValueKind.Null);

        var entries = (await Scenario.GetJsonAsync(admin, $"/api/admin/audit?subject={user.Id}&pageSize=50"))
            .GetProperty("items").EnumerateArray().ToList();

        entries.Select(Action).ShouldContain("user.impersonation_started");
        entries.Select(Action).ShouldContain("user.impersonation_ended");

        var change = entries.First(entry => Action(entry) == "account.profile_updated");
        change.GetProperty("actor").GetProperty("id").GetString().ShouldBe(adminUser.Id.ToString());
        change.GetProperty("onBehalfOf").GetProperty("id").GetString().ShouldBe(user.Id.ToString());

        using var owner = await Scenario.SignedInAsync(factory, user);
        (await Scenario.NotificationTypesAsync(owner)).ShouldContain(IdentityNotificationTypes.Impersonated);
    }

    [Fact]
    public async Task A_support_session_cannot_touch_the_account_secrets()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("protected"), Scenario.Password);

        await StartAsync(admin, user.Id);

        using (var password = await admin.PostAsync("/api/account/change-password", new { currentPassword = Scenario.Password, newPassword = "Another-Password-2026!" }))
        {
            await Scenario.ExpectErrorAsync(password, HttpStatusCode.Forbidden, "auth.interactive_session_required");
        }

        using (var export = await admin.PostAsync("/api/account/data-export", new { password = Scenario.Password }))
        {
            await Scenario.ExpectErrorAsync(export, HttpStatusCode.Forbidden, "auth.interactive_session_required");
        }

        using (var deletion = await admin.PostAsync("/api/account/deletion", new { password = Scenario.Password }))
        {
            await Scenario.ExpectErrorAsync(deletion, HttpStatusCode.Forbidden, "auth.interactive_session_required");
        }

        using (var legal = await admin.PostAsync("/api/account/legal/accept", new { documentIds = new[] { Guid.NewGuid() } }))
        {
            await Scenario.ExpectErrorAsync(legal, HttpStatusCode.Forbidden, "auth.interactive_session_required");
        }

        using (var nested = await admin.PostAsync($"/api/admin/users/{user.Id}/impersonate", new { reason = "again" }))
        {
            nested.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task A_support_session_never_signs_in_to_an_application()
    {
        var (admin, adminUser) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("appuser"), Scenario.Password);
        var app = await Scenario.CreateApplicationAsync(admin);
        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{app.Id}/users/{user.Id}", new { }))
        {
            granted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var browser = await Scenario.ClientAsync(factory, allowRedirects: false);
        (await browser.SignInAsync(adminUser.Email, adminUser.Password)).Dispose();
        await StartAsync(browser, user.Id);

        using var authorize = await browser.GetAsync(
            $"/connect/authorize?client_id={app.ClientId}&response_type=code&redirect_uri={Uri.EscapeDataString(Scenario.RedirectUri)}"
            + "&scope=openid&state=s&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256");

        authorize.StatusCode.ShouldBe(HttpStatusCode.Found);
        authorize.Headers.Location!.OriginalString.ShouldBe("/access-denied?reason=impersonation");
    }

    [Fact]
    public async Task Administrators_and_oneself_cannot_be_impersonated_and_a_reason_is_required()
    {
        var (admin, adminUser) = await Scenario.AdminAsync(factory);
        var other = await factory.CreateUserAsync(Scenario.UniqueEmail("otheradmin"), Scenario.Password, "Administrator");
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("plain"), Scenario.Password);

        using (var self = await admin.PostAsync($"/api/admin/users/{adminUser.Id}/impersonate", new { reason = "test" }))
        {
            await Scenario.ExpectErrorAsync(self, HttpStatusCode.Forbidden, "identity.impersonation_refused");
        }

        using (var administrator = await admin.PostAsync($"/api/admin/users/{other.Id}/impersonate", new { reason = "test" }))
        {
            await Scenario.ExpectErrorAsync(administrator, HttpStatusCode.Forbidden, "identity.impersonation_refused");
        }

        using (var noReason = await admin.PostAsync($"/api/admin/users/{user.Id}/impersonate", new { reason = "" }))
        {
            noReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // Not delegated: the users page is not enough.
        var manager = await factory.CreateUserAsync(Scenario.UniqueEmail("manager"), Scenario.Password);
        var group = await Scenario.CreateGroupAsync(admin, manager.Id);
        await Scenario.GrantPageAsync(admin, "users", group);
        using var delegated = await Scenario.SignedInAsync(factory, manager);
        using var refused = await delegated.PostAsync($"/api/admin/users/{user.Id}/impersonate", new { reason = "test" });
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static string? Action(JsonElement entry) => entry.GetProperty("action").GetString();

    private static async Task StartAsync(ApiClient admin, Guid userId)
    {
        using (var started = await admin.PostAsync($"/api/admin/users/{userId}/impersonate", new { reason = "Ticket #42" }))
        {
            started.StatusCode.ShouldBe(HttpStatusCode.NoContent, await started.Content.ReadAsStringAsync());
        }

        // The antiforgery token is bound to the identity, which just changed.
        await admin.RefreshAntiforgeryTokenAsync();
    }
}
