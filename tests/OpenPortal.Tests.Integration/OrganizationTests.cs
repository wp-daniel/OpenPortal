using System.Net;
using System.Text.Json;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.Web.Authorization;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Organizations (tenants): who belongs to which, and how an application that works per organization learns which
/// one the user signs in for, at the sign-in and again at every refresh.
/// </summary>
public sealed class OrganizationTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private const string Scope = "openid profile email offline_access organization";

    [Fact]
    public async Task Organizations_have_unique_immutable_short_names_and_members_with_roles()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var slug = NewSlug();

        var organization = await CreateOrganizationAsync(admin, slug, "Acme");
        organization.GetProperty("slug").GetString().ShouldBe(slug);

        using (var duplicate = await admin.PostAsync("/api/admin/organizations", new { name = "Other", slug = slug.ToUpperInvariant() }))
        {
            await Scenario.ExpectErrorAsync(duplicate, HttpStatusCode.Conflict, "access.organization_slug_in_use");
        }

        using (var invalid = await admin.PostAsync("/api/admin/organizations", new { name = "Bad", slug = "-no good" }))
        {
            await Scenario.ExpectErrorAsync(invalid, HttpStatusCode.BadRequest, "access.organization_slug_invalid");
        }

        var id = organization.GetProperty("id").GetGuid();
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("member"), Scenario.Password);

        using (var added = await admin.PutAsync($"/api/admin/organizations/{id}/members/{user.Id}", new { role = "admin" }))
        {
            var member = (await Scenario.JsonAsync(added, HttpStatusCode.OK)).GetProperty("members").EnumerateArray().Single();
            member.GetProperty("id").GetGuid().ShouldBe(user.Id);
            member.GetProperty("role").GetString().ShouldBe("admin");
        }

        using (var badRole = await admin.PutAsync($"/api/admin/organizations/{id}/members/{user.Id}", new { role = "owner" }))
        {
            await Scenario.ExpectErrorAsync(badRole, HttpStatusCode.BadRequest, "access.organization_role_invalid");
        }

        using (var renamed = await admin.PutAsync($"/api/admin/organizations/{id}", new { name = "Acme Corp" }))
        {
            var detail = await Scenario.JsonAsync(renamed, HttpStatusCode.OK);
            detail.GetProperty("name").GetString().ShouldBe("Acme Corp");
            detail.GetProperty("slug").GetString().ShouldBe(slug);
        }

        using var client = await Scenario.SignedInAsync(factory, user);
        var mine = (await Scenario.GetJsonAsync(client, "/api/account/organizations")).EnumerateArray().Single();
        mine.GetProperty("slug").GetString().ShouldBe(slug);
        mine.GetProperty("role").GetString().ShouldBe("admin");
        (await Scenario.NotificationTypesAsync(client)).ShouldContain(AccessNotificationTypes.OrganizationJoined);

        (await Scenario.GetJsonAsync(admin, $"/api/admin/organizations/users/{user.Id}"))
            .EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(id);
    }

    [Fact]
    public async Task Managing_organizations_needs_the_organizations_page()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("plain"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, user);

        using (var refused = await client.PostAsync("/api/admin/organizations", new { name = "Nope", slug = NewSlug() }))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        await Scenario.GrantPageAsync(admin, PortalPages.Organizations, await Scenario.CreateGroupAsync(admin, user.Id));

        using var allowed = await client.PostAsync("/api/admin/organizations", new { name = "Yes", slug = NewSlug() });
        allowed.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_member_of_one_organization_signs_in_for_it_and_loses_the_session_when_removed()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin, new { organizations = "required" });
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("tenant"), Scenario.Password);
        await GrantAsync(admin, applicationId, user.Id);

        var slug = NewSlug();
        var organizationId = (await CreateOrganizationAsync(admin, slug, "Tenant One")).GetProperty("id").GetGuid();
        await SetMemberAsync(admin, organizationId, user.Id, "member");

        var tokens = await Scenario.SignInToApplicationAsync(factory, user, clientId, secret, Scope);
        var claims = Scenario.ReadJwtPayload(tokens.GetProperty("id_token").GetString()!);
        claims.GetProperty("org_id").GetString().ShouldBe(organizationId.ToString());
        claims.GetProperty("org_slug").GetString().ShouldBe(slug);
        claims.GetProperty("org_name").GetString().ShouldBe("Tenant One");
        claims.GetProperty("org_role").GetString().ShouldBe("member");

        // A new role reaches the application at the next refresh.
        await SetMemberAsync(admin, organizationId, user.Id, "admin");
        using (var refreshed = await Scenario.RefreshAsync(factory, clientId, secret, tokens.GetProperty("refresh_token").GetString()!))
        {
            var again = await Scenario.JsonAsync(refreshed, HttpStatusCode.OK);
            Scenario.ReadJwtPayload(again.GetProperty("id_token").GetString()!).GetProperty("org_role").GetString().ShouldBe("admin");
            tokens = again;
        }

        using (var removed = await admin.DeleteAsync($"/api/admin/organizations/{organizationId}/members/{user.Id}"))
        {
            removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var refused = await Scenario.RefreshAsync(factory, clientId, secret, tokens.GetProperty("refresh_token").GetString()!);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("invalid_grant");
    }

    [Fact]
    public async Task An_application_that_needs_an_organization_turns_away_users_without_one()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, _) = await Scenario.CreateApplicationAsync(admin, new { organizations = "required" });
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("loner"), Scenario.Password);
        await GrantAsync(admin, applicationId, user.Id);

        var (location, _) = await Scenario.AuthorizeAsync(factory, user, clientId, Scope);

        location.OriginalString.ShouldStartWith("/access-denied?");
        location.OriginalString.ShouldContain("reason=organization");
    }

    [Fact]
    public async Task A_member_of_several_organizations_chooses_one_and_cannot_pick_someone_elses()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin, new { organizations = "optional" });
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("consultant"), Scenario.Password);
        await GrantAsync(admin, applicationId, user.Id);

        var first = NewSlug();
        var second = NewSlug();
        var foreign = NewSlug();
        await SetMemberAsync(admin, (await CreateOrganizationAsync(admin, first, "First")).GetProperty("id").GetGuid(), user.Id, "member");
        await SetMemberAsync(admin, (await CreateOrganizationAsync(admin, second, "Second")).GetProperty("id").GetGuid(), user.Id, "member");
        await CreateOrganizationAsync(admin, foreign, "Foreign");

        // Without a choice the portal asks, then comes back to the same request.
        var (choose, _) = await Scenario.AuthorizeAsync(factory, user, clientId, Scope);
        choose.OriginalString.ShouldStartWith("/select-organization?");
        var returnUrl = System.Web.HttpUtility.ParseQueryString(choose.OriginalString.Split('?', 2)[1])["returnUrl"]!;
        returnUrl.ShouldStartWith("/connect/authorize?");
        returnUrl.ShouldNotContain("organization=");

        var tokens = await Scenario.SignInToApplicationAsync(factory, user, clientId, secret, Scope, organization: second);
        Scenario.ReadJwtPayload(tokens.GetProperty("id_token").GetString()!).GetProperty("org_slug").GetString().ShouldBe(second);

        var (denied, _) = await Scenario.AuthorizeAsync(factory, user, clientId, Scope, organization: foreign);
        denied.OriginalString.ShouldStartWith("/access-denied?");
    }

    [Fact]
    public async Task An_application_that_does_not_use_organizations_hears_nothing_about_them()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("plain"), Scenario.Password);
        await GrantAsync(admin, applicationId, user.Id);
        await SetMemberAsync(admin, (await CreateOrganizationAsync(admin, NewSlug(), "Ignored")).GetProperty("id").GetGuid(), user.Id, "member");

        var tokens = await Scenario.SignInToApplicationAsync(factory, user, clientId, secret, Scope);

        Scenario.ReadJwtPayload(tokens.GetProperty("id_token").GetString()!).TryGetProperty("org_id", out _).ShouldBeFalse();
    }

    private static string NewSlug() => $"org-{Guid.NewGuid():N}"[..16];

    private static async Task<JsonElement> CreateOrganizationAsync(ApiClient admin, string slug, string name)
    {
        using var created = await admin.PostAsync("/api/admin/organizations", new { name, slug });

        return await Scenario.JsonAsync(created, HttpStatusCode.Created);
    }

    private static async Task SetMemberAsync(ApiClient admin, Guid organizationId, Guid userId, string role)
    {
        using var response = await admin.PutAsync($"/api/admin/organizations/{organizationId}/members/{userId}", new { role });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task GrantAsync(ApiClient admin, Guid applicationId, Guid userId)
    {
        using var response = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{userId}", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }
}
