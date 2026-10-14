using System.Net;
using System.Text.Json;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.Web.Authorization;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// A user who cannot open an application asks for it; a reviewer approves (which grants it, with roles) or rejects
/// it, and both sides are told.
/// </summary>
public sealed class AccessRequestTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task Only_applications_that_accept_requests_can_be_asked_for()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, _, _) = await Scenario.CreateApplicationAsync(admin);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("asker"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, user);

        (await Scenario.GetJsonAsync(client, "/api/account/access-requests/catalog"))
            .EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldNotContain(applicationId);

        using var refused = await client.PostAsync("/api/account/access-requests", new { applicationId });
        await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Conflict, "access.requests_not_accepted");

        using var missing = await client.PostAsync("/api/account/access-requests", new { applicationId = Guid.NewGuid() });
        await Scenario.ExpectErrorAsync(missing, HttpStatusCode.NotFound, "access.application_not_found");
    }

    [Fact]
    public async Task An_approved_request_grants_the_application_with_roles_and_tells_the_requester()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, _) = await Scenario.CreateApplicationAsync(admin, new
        {
            allowAccessRequests = true,
            roles = new[] { new { key = "sales", displayName = "Sales" } },
        });

        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("asker"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, user);

        var catalog = await Scenario.GetJsonAsync(client, "/api/account/access-requests/catalog");
        var entry = catalog.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == applicationId);
        entry.GetProperty("clientId").GetString().ShouldBe(clientId);
        entry.GetProperty("latestRequest").ValueKind.ShouldBe(JsonValueKind.Null);

        using (var submitted = await client.PostAsync("/api/account/access-requests", new { applicationId, reason = "I sell things" }))
        {
            var request = await Scenario.JsonAsync(submitted, HttpStatusCode.Created);
            request.GetProperty("status").GetString().ShouldBe("pending");
            request.GetProperty("reason").GetString().ShouldBe("I sell things");
        }

        using (var twice = await client.PostAsync("/api/account/access-requests", new { applicationId }))
        {
            await Scenario.ExpectErrorAsync(twice, HttpStatusCode.Conflict, "access.request_already_pending");
        }

        // The reviewers are told; the requester is not told about their own request.
        (await Scenario.NotificationTypesAsync(admin)).ShouldContain(AccessNotificationTypes.RequestSubmitted);
        (await Scenario.NotificationTypesAsync(client)).ShouldNotContain(AccessNotificationTypes.RequestSubmitted);

        var pending = await Scenario.GetJsonAsync(admin, $"/api/admin/access-requests?status=pending&applicationId={applicationId}");
        pending.GetProperty("pendingCount").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        var item = pending.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(user.Id);
        item.GetProperty("hasAccess").GetBoolean().ShouldBeFalse();
        item.GetProperty("applicationRoles").EnumerateArray().Single().GetProperty("key").GetString().ShouldBe("sales");
        var requestId = item.GetProperty("id").GetGuid();

        using (var unknownRole = await admin.PostAsync($"/api/admin/access-requests/{requestId}/approve", new { roles = new[] { "root" } }))
        {
            await Scenario.ExpectErrorAsync(unknownRole, HttpStatusCode.BadRequest, "access.role_not_found");
        }

        using (var approved = await admin.PostAsync($"/api/admin/access-requests/{requestId}/approve", new { roles = new[] { "sales" }, note = "Welcome" }))
        {
            var decided = await Scenario.JsonAsync(approved, HttpStatusCode.OK);
            decided.GetProperty("status").GetString().ShouldBe("approved");
            decided.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["sales"]);
            decided.GetProperty("decisionNote").GetString().ShouldBe("Welcome");
        }

        using (var again = await admin.PostAsync($"/api/admin/access-requests/{requestId}/reject", new { }))
        {
            await Scenario.ExpectErrorAsync(again, HttpStatusCode.Conflict, "access.request_not_pending");
        }

        (await Scenario.GetJsonAsync(client, "/api/account/applications"))
            .EnumerateArray().Select(app => app.GetProperty("id").GetGuid()).ShouldContain(applicationId);

        // Approval is its own notice; the plain "granted" one is not sent on top of it.
        var types = await Scenario.NotificationTypesAsync(client);
        types.ShouldContain(AccessNotificationTypes.RequestApproved);
        types.ShouldNotContain(AccessNotificationTypes.ApplicationGranted);

        var access = await Scenario.GetJsonAsync(admin, $"/api/admin/access/users/{user.Id}");
        access.GetProperty("applications").EnumerateArray().Single().GetProperty("roles")
            .EnumerateArray().Select(role => role.GetString()).ShouldBe(["sales"]);

        // Once granted, the application leaves the catalog and cannot be asked for again.
        (await Scenario.GetJsonAsync(client, "/api/account/access-requests/catalog"))
            .EnumerateArray().Select(app => app.GetProperty("id").GetGuid()).ShouldNotContain(applicationId);

        using var granted = await client.PostAsync("/api/account/access-requests", new { applicationId });
        await Scenario.ExpectErrorAsync(granted, HttpStatusCode.Conflict, "access.already_has_access");
    }

    [Fact]
    public async Task A_rejected_request_explains_why_and_can_be_asked_again_while_a_withdrawn_one_closes()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, _, _) = await Scenario.CreateApplicationAsync(admin, new { allowAccessRequests = true });
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("asker"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, user);

        var first = await SubmitAsync(client, applicationId);

        using (var rejected = await admin.PostAsync($"/api/admin/access-requests/{first}/reject", new { note = "Not for your team" }))
        {
            (await Scenario.JsonAsync(rejected, HttpStatusCode.OK)).GetProperty("status").GetString().ShouldBe("rejected");
        }

        var notification = (await Scenario.GetJsonAsync(client, "/api/notifications")).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("type").GetString() == AccessNotificationTypes.RequestRejected);
        notification.GetProperty("parameters").GetProperty("note").GetString().ShouldBe("Not for your team");

        var latest = (await Scenario.GetJsonAsync(client, "/api/account/access-requests/catalog"))
            .EnumerateArray().Single(app => app.GetProperty("id").GetGuid() == applicationId)
            .GetProperty("latestRequest");
        latest.GetProperty("status").GetString().ShouldBe("rejected");
        latest.GetProperty("decisionNote").GetString().ShouldBe("Not for your team");

        var second = await SubmitAsync(client, applicationId);

        using (var cancelled = await client.DeleteAsync($"/api/account/access-requests/{second}"))
        {
            cancelled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var twice = await client.DeleteAsync($"/api/account/access-requests/{second}"))
        {
            await Scenario.ExpectErrorAsync(twice, HttpStatusCode.Conflict, "access.request_not_pending");
        }

        (await Scenario.GetJsonAsync(client, "/api/account/access-requests"))
            .EnumerateArray().Select(request => request.GetProperty("status").GetString())
            .ShouldBe(["cancelled", "rejected"]);

        // Someone else's request does not exist, as far as anybody else can tell.
        var stranger = await factory.CreateUserAsync(Scenario.UniqueEmail("stranger"), Scenario.Password);
        using var other = await Scenario.SignedInAsync(factory, stranger);
        var third = await SubmitAsync(client, applicationId);

        using var foreign = await other.DeleteAsync($"/api/account/access-requests/{third}");
        await Scenario.ExpectErrorAsync(foreign, HttpStatusCode.NotFound, "access.request_not_found");
    }

    [Fact]
    public async Task Reviewing_needs_the_access_requests_page()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, _, _) = await Scenario.CreateApplicationAsync(admin, new { allowAccessRequests = true });
        var asker = await factory.CreateUserAsync(Scenario.UniqueEmail("asker"), Scenario.Password);
        var reviewer = await factory.CreateUserAsync(Scenario.UniqueEmail("reviewer"), Scenario.Password);

        using var askerClient = await Scenario.SignedInAsync(factory, asker);
        var requestId = await SubmitAsync(askerClient, applicationId);

        using var reviewerClient = await Scenario.SignedInAsync(factory, reviewer);

        using (var before = await reviewerClient.GetAsync("/api/admin/access-requests"))
        {
            before.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        await Scenario.GrantPageAsync(admin, PortalPages.AccessRequests, await Scenario.CreateGroupAsync(admin, reviewer.Id));

        // Holders of the page are reviewers: later requests reach them too.
        var other = await factory.CreateUserAsync(Scenario.UniqueEmail("asker2"), Scenario.Password);
        using var otherClient = await Scenario.SignedInAsync(factory, other);
        await SubmitAsync(otherClient, applicationId);
        (await Scenario.NotificationTypesAsync(reviewerClient)).ShouldContain(AccessNotificationTypes.RequestSubmitted);

        using var approved = await reviewerClient.PostAsync($"/api/admin/access-requests/{requestId}/approve", new { });
        var decided = await Scenario.JsonAsync(approved, HttpStatusCode.OK);
        decided.GetProperty("decidedBy").GetProperty("id").GetGuid().ShouldBe(reviewer.Id);
    }

    private static async Task<Guid> SubmitAsync(ApiClient client, Guid applicationId)
    {
        using var submitted = await client.PostAsync("/api/account/access-requests", new { applicationId });

        return (await Scenario.JsonAsync(submitted, HttpStatusCode.Created)).GetProperty("id").GetGuid();
    }
}
