using System.Net;
using System.Text.Json;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The richer access model: groups inside groups, access with an end date, access reviews (recertification) and
/// organizations whose own administrators manage their members.
/// </summary>
public sealed class AccessModelTests : IClassFixture<OpenPortalFactory>
{
    private readonly OpenPortalFactory _factory;

    public AccessModelTests(OpenPortalFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Members_of_a_subgroup_get_what_the_parent_group_is_given()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("nested"), Scenario.Password);
        var (applicationId, clientId, _) = await Scenario.CreateApplicationAsync(admin);

        var parent = await Scenario.CreateGroupAsync(admin);
        var child = await Scenario.CreateGroupAsync(admin, user.Id);

        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/groups/{parent}", new { }))
        {
            granted.IsSuccessStatusCode.ShouldBeTrue();
        }

        (await LaunchpadAsync(user)).ShouldNotContain(clientId);

        using (var nested = await admin.PutAsync($"/api/admin/groups/{parent}/subgroups/{child}", new { }))
        {
            var detail = await Scenario.JsonAsync(nested, HttpStatusCode.OK);
            detail.GetProperty("subgroups").EnumerateArray().Select(group => group.GetProperty("id").GetGuid()).ShouldContain(child);
        }

        (await LaunchpadAsync(user)).ShouldContain(clientId);

        // A cycle is refused.
        using (var cycle = await admin.PutAsync($"/api/admin/groups/{child}/subgroups/{parent}", new { }))
        {
            await Scenario.ExpectErrorAsync(cycle, HttpStatusCode.BadRequest, "access.group_nesting_cycle");
        }

        using (var removed = await admin.DeleteAsync($"/api/admin/groups/{parent}/subgroups/{child}"))
        {
            removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await LaunchpadAsync(user)).ShouldNotContain(clientId);
    }

    [Fact]
    public async Task Temporary_access_ends_by_itself()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("temporary"), Scenario.Password);
        var (applicationId, clientId, _) = await Scenario.CreateApplicationAsync(admin);

        using (var past = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) }))
        {
            await Scenario.ExpectErrorAsync(past, HttpStatusCode.BadRequest, "access.expiry_in_past");
        }

        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(2) }))
        {
            granted.IsSuccessStatusCode.ShouldBeTrue(await granted.Content.ReadAsStringAsync());
        }

        (await LaunchpadAsync(user)).ShouldContain(clientId);

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        // Expired: it grants nothing any more, even before the sweep removes it.
        (await LaunchpadAsync(user)).ShouldNotContain(clientId);
    }

    [Fact]
    public async Task A_completed_review_removes_what_reviewers_revoked()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);
        var kept = await _factory.CreateUserAsync(Scenario.UniqueEmail("kept"), Scenario.Password);
        var dropped = await _factory.CreateUserAsync(Scenario.UniqueEmail("dropped"), Scenario.Password);
        var (applicationId, clientId, _) = await Scenario.CreateApplicationAsync(admin);

        foreach (var user in new[] { kept, dropped })
        {
            using var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { });
            granted.IsSuccessStatusCode.ShouldBeTrue();
        }

        JsonElement review;
        using (var started = await admin.PostAsync("/api/admin/access-reviews", new { applicationId, name = "Q4 review" }))
        {
            review = await Scenario.JsonAsync(started, HttpStatusCode.OK);
        }

        var reviewId = review.GetProperty("review").GetProperty("id").GetGuid();
        var items = review.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(2);

        foreach (var item in items)
        {
            var decision = item.GetProperty("subjectId").GetGuid() == dropped.Id ? "revoke" : "keep";
            using var decided = await admin.PutAsync($"/api/admin/access-reviews/{reviewId}/items/{item.GetProperty("id").GetGuid()}", new { decision });
            decided.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var completed = await admin.PostAsync($"/api/admin/access-reviews/{reviewId}/complete"))
        {
            (await Scenario.JsonAsync(completed, HttpStatusCode.OK)).GetProperty("review").GetProperty("status").GetString().ShouldBe("completed");
        }

        (await LaunchpadAsync(kept)).ShouldContain(clientId);
        (await LaunchpadAsync(dropped)).ShouldNotContain(clientId);

        using var again = await admin.PostAsync($"/api/admin/access-reviews/{reviewId}/complete");
        await Scenario.ExpectErrorAsync(again, HttpStatusCode.Conflict, "access.review_not_open");
    }

    [Fact]
    public async Task An_organization_administrator_manages_its_members_but_not_other_organizations()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);
        var owner = await _factory.CreateUserAsync(Scenario.UniqueEmail("owner"), Scenario.Password);
        var newcomer = await _factory.CreateUserAsync(Scenario.UniqueEmail("newcomer"), Scenario.Password);

        var mine = await CreateOrganizationAsync(admin);
        var other = await CreateOrganizationAsync(admin);

        using (var made = await admin.PutAsync($"/api/admin/organizations/{mine}/members/{owner.Id}", new { role = "admin" }))
        {
            made.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var client = await Scenario.SignedInAsync(_factory, owner);

        using (var added = await client.PostAsync($"/api/account/organizations/{mine}/members", new { email = newcomer.Email, role = "member" }))
        {
            var detail = await Scenario.JsonAsync(added, HttpStatusCode.OK);
            detail.GetRawText().ShouldContain(newcomer.Id.ToString());
        }

        // The only administrator cannot step down or leave.
        using (var demoted = await client.PutAsync($"/api/account/organizations/{mine}/members/{owner.Id}", new { role = "member" }))
        {
            await Scenario.ExpectErrorAsync(demoted, HttpStatusCode.Conflict, "access.organization_last_admin");
        }

        using (var foreign = await client.GetAsync($"/api/account/organizations/{other}"))
        {
            await Scenario.ExpectErrorAsync(foreign, HttpStatusCode.Forbidden, "access.organization_manage_forbidden");
        }

        // A plain member does not manage it either.
        using var member = await Scenario.SignedInAsync(_factory, newcomer);
        using var refused = await member.GetAsync($"/api/account/organizations/{mine}");
        await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Forbidden, "access.organization_manage_forbidden");
    }

    private async Task<Guid> CreateOrganizationAsync(ApiClient admin)
    {
        var slug = $"org-{Guid.NewGuid():N}"[..16];
        using var created = await admin.PostAsync("/api/admin/organizations", new { name = slug, slug });
        var text = await created.Content.ReadAsStringAsync();
        created.IsSuccessStatusCode.ShouldBeTrue(text);

        return JsonDocument.Parse(text).RootElement.GetProperty("id").GetGuid();
    }

    private async Task<IReadOnlyList<string?>> LaunchpadAsync(TestUser user)
    {
        using var client = await Scenario.SignedInAsync(_factory, user);
        var launchpad = await Scenario.GetJsonAsync(client, "/api/account/applications");

        return launchpad.EnumerateArray().Select(item => item.GetProperty("clientId").GetString()).ToList();
    }
}
