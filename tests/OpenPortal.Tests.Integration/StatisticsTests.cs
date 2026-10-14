using System.Net;
using OpenPortal.Web.Authorization;

namespace OpenPortal.Tests.Integration;

/// <summary>The administrators' overview, computed from the audit log and the modules' own data.</summary>
public sealed class StatisticsTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task Sign_ins_failures_and_application_use_are_counted_per_day()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("active"), Scenario.Password);

        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { }))
        {
            granted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var anonymous = await ApiClient.CreateAsync(factory))
        using (var failed = await anonymous.SignInAsync(user.Email, "Wrong-Horse-99"))
        {
            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        await Scenario.SignInToApplicationAsync(factory, user, clientId, secret, "openid profile");

        var statistics = await Scenario.GetJsonAsync(admin, "/api/admin/statistics?days=7&timeZone=Europe/Rome");
        var activity = statistics.GetProperty("activity");

        activity.GetProperty("timeZone").GetString().ShouldNotBeNullOrEmpty();
        activity.GetProperty("days").GetArrayLength().ShouldBe(7);
        activity.GetProperty("isComplete").GetBoolean().ShouldBeTrue();

        var current = activity.GetProperty("current");
        current.GetProperty("signIns").GetInt32().ShouldBeGreaterThanOrEqualTo(2);
        current.GetProperty("failedSignIns").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        current.GetProperty("applicationSignIns").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        current.GetProperty("activeUsers").GetInt32().ShouldBeGreaterThanOrEqualTo(2);

        var today = activity.GetProperty("days").EnumerateArray().Last();
        today.GetProperty("failedSignIns").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        activity.GetProperty("topApplications").EnumerateArray()
            .ShouldContain(usage => usage.GetProperty("clientId").GetString() == clientId && usage.GetProperty("users").GetInt32() == 1);

        activity.GetProperty("failureReasons").EnumerateArray()
            .ShouldContain(reason => reason.GetProperty("value").GetString() == "invalid_password");

        activity.GetProperty("topFailedAccounts").EnumerateArray()
            .ShouldContain(account => account.GetProperty("value").GetString() == user.Email);

        var users = statistics.GetProperty("users");
        users.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(2);
        users.GetProperty("administrators").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        users.GetProperty("newInPeriod").GetInt32().ShouldBeGreaterThanOrEqualTo(2);

        statistics.GetProperty("access").GetProperty("applications").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task The_statistics_need_the_statistics_page()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("viewer"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, user);

        using (var refused = await client.GetAsync("/api/admin/statistics"))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        await Scenario.GrantPageAsync(admin, PortalPages.Statistics, await Scenario.CreateGroupAsync(admin, user.Id));

        using (var allowed = await client.GetAsync("/api/admin/statistics?days=30"))
        {
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
        }

        using var tooLong = await client.GetAsync("/api/admin/statistics?days=365");
        tooLong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
