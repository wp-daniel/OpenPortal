using System.Net;

namespace OpenPortal.Tests.Integration;

/// <summary>The system page: this instance's checks and background jobs, and running a job now.</summary>
public sealed class SystemStatusTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task Administrators_see_the_checks_and_the_background_jobs()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);

        var status = await Scenario.GetJsonAsync(admin, "/api/admin/system");

        status.GetProperty("databaseProvider").GetString().ShouldNotBeNullOrEmpty();
        status.GetProperty("checks").EnumerateArray().Select(check => check.GetProperty("name").GetString()).ShouldContain("database-identity");
        status.GetProperty("jobs").EnumerateArray().Select(job => job.GetProperty("name").GetString())
            .ShouldBe(
                ["access.expiry", "audit.retention", "identity.session_retention", "notifications.digest", "notifications.retention", "privacy.account_deletion"],
                ignoreOrder: true);
        status.GetProperty("outbox").GetProperty("failed").GetInt32().ShouldBeGreaterThanOrEqualTo(0);

        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("not-admin"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, user);
        (await client.GetAsync("/api/admin/system")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_job_runs_on_demand_and_its_run_is_recorded()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);

        using (var run = await admin.PostAsync("/api/admin/system/jobs/audit.retention/run"))
        {
            var result = await Scenario.JsonAsync(run, HttpStatusCode.OK);
            result.GetProperty("succeeded").GetBoolean().ShouldBeTrue();
        }

        var job = (await Scenario.GetJsonAsync(admin, "/api/admin/system")).GetProperty("jobs").EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == "audit.retention");
        job.GetProperty("runs").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        job.GetProperty("lastSucceeded").GetBoolean().ShouldBeTrue();

        using var unknown = await admin.PostAsync("/api/admin/system/jobs/no.such.job/run");
        await Scenario.ExpectErrorAsync(unknown, HttpStatusCode.NotFound, "system.job_not_found");
    }
}
