using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Resources;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Portal pages granted to groups: a member of a group holding a page may use that page's endpoints, nobody
/// else may (administrators excepted), and a page never becomes a way to climb to administrator.
/// </summary>
public sealed class PagePermissionTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private const string Password = "Correct-Horse-9";

    [Fact]
    public async Task A_group_page_opens_its_endpoints_to_members_and_closing_it_takes_effect_at_once()
    {
        using var admin = await AdminAsync();
        var member = await factory.CreateUserAsync($"member-{Guid.NewGuid():N}@example.com", Password);
        using var client = await SignedInAsync(member);

        using (var before = await client.GetAsync("/api/admin/users"))
        {
            before.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        (await SessionPagesAsync(client)).ShouldBeEmpty();

        var groupId = await CreateGroupAsync(admin, member.Id);
        await GrantAsync(admin, PortalPages.Users, groupId);

        using (var allowed = await client.GetAsync("/api/admin/users"))
        {
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
        }

        (await SessionPagesAsync(client)).ShouldBe([PortalPages.Users]);

        // Another page stays closed.
        using (var groups = await client.GetAsync($"/api/admin/groups/{groupId}"))
        {
            groups.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var revoke = await admin.DeleteAsync($"/api/admin/page-permissions/{PortalPages.Users}/groups/{groupId}"))
        {
            revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var after = await client.GetAsync("/api/admin/users");
        after.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_page_shared_by_several_screens_opens_only_the_reads_they_need()
    {
        using var admin = await AdminAsync();
        var member = await factory.CreateUserAsync($"groups-{Guid.NewGuid():N}@example.com", Password);
        using var client = await SignedInAsync(member);

        var groupId = await CreateGroupAsync(admin, member.Id);
        await GrantAsync(admin, PortalPages.Groups, groupId);

        // The groups page picks users, so it may list them, but not edit them.
        using (var list = await client.GetAsync("/api/admin/users"))
        {
            list.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var read = await client.GetAsync($"/api/admin/users/{member.Id}");
        read.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_delegated_users_page_cannot_be_used_to_reach_administrator()
    {
        using var admin = await AdminAsync();
        var member = await factory.CreateUserAsync($"hr-{Guid.NewGuid():N}@example.com", Password);
        var otherAdmin = await factory.CreateUserAsync($"boss-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        var plain = await factory.CreateUserAsync($"plain-{Guid.NewGuid():N}@example.com", Password);
        using var client = await SignedInAsync(member);

        await GrantAsync(admin, PortalPages.Users, await CreateGroupAsync(admin, member.Id));

        using (var create = await client.PostAsync("/api/admin/users", new
        {
            email = $"new-admin-{Guid.NewGuid():N}@example.com",
            password = Password,
            firstName = "New",
            lastName = "Admin",
            isAdministrator = true,
        }))
        {
            create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await ErrorCodeAsync(create)).ShouldBe("identity.administrator_required");
        }

        using (var promote = await client.PutAsync($"/api/admin/users/{plain.Id}", new
        {
            firstName = "Plain",
            lastName = "User",
            isAdministrator = true,
        }))
        {
            promote.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await ErrorCodeAsync(promote)).ShouldBe("identity.administrator_required");
        }

        using (var editAdmin = await client.PutAsync($"/api/admin/users/{otherAdmin.Id}", new
        {
            firstName = "Boss",
            lastName = "Renamed",
            isAdministrator = true,
        }))
        {
            editAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var reset = await client.PostAsync(
                   $"/api/admin/users/{otherAdmin.Id}/reset-password",
                   new { newPassword = "Another-Horse-9" }))
        {
            reset.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // An ordinary account is still theirs to edit.
        using var edit = await client.PutAsync($"/api/admin/users/{plain.Id}", new
        {
            firstName = "Plain",
            lastName = "Renamed",
            isAdministrator = false,
        });
        edit.StatusCode.ShouldBe(HttpStatusCode.OK, await edit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Page_permissions_are_administrator_only_even_for_holders_of_every_page()
    {
        using var admin = await AdminAsync();
        var member = await factory.CreateUserAsync($"all-{Guid.NewGuid():N}@example.com", Password);
        using var client = await SignedInAsync(member);

        var groupId = await CreateGroupAsync(admin, member.Id);
        foreach (var page in PortalPages.All)
        {
            await GrantAsync(admin, page.Key, groupId);
        }

        using (var read = await client.GetAsync("/api/admin/page-permissions"))
        {
            read.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var grant = await client.PutAsync($"/api/admin/page-permissions/{PortalPages.Users}/groups/{groupId}", new { });
        grant.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_matrix_lists_the_catalog_and_rejects_unknown_pages_and_groups()
    {
        using var admin = await AdminAsync();
        var groupId = await CreateGroupAsync(admin);
        await GrantAsync(admin, PortalPages.ContentProjects, groupId);

        using (var response = await admin.GetAsync("/api/admin/page-permissions"))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var matrix = await response.Content.ReadFromJsonAsync<JsonElement>();

            matrix.GetProperty("pages").EnumerateArray().Select(page => page.GetProperty("key").GetString())
                .ShouldBe(PortalPages.All.Select(page => page.Key));
            matrix.GetProperty("grants").EnumerateArray()
                .ShouldContain(grant => grant.GetProperty("groupId").GetGuid() == groupId
                    && grant.GetProperty("pageKey").GetString() == PortalPages.ContentProjects);
        }

        using (var group = await admin.GetAsync($"/api/admin/groups/{groupId}"))
        {
            (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pages").EnumerateArray()
                .Select(page => page.GetString()).ShouldBe([PortalPages.ContentProjects]);
        }

        using (var unknownPage = await admin.PutAsync($"/api/admin/page-permissions/no-such-page/groups/{groupId}", new { }))
        {
            unknownPage.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await ErrorCodeAsync(unknownPage)).ShouldBe("access.page_not_found");
        }

        using var unknownGroup = await admin.PutAsync($"/api/admin/page-permissions/{PortalPages.Users}/groups/{Guid.NewGuid()}", new { });
        unknownGroup.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_group_pages_can_be_replaced_as_a_whole_and_an_unknown_key_changes_nothing()
    {
        using var admin = await AdminAsync();
        var groupId = await CreateGroupAsync(admin);
        await GrantAsync(admin, PortalPages.Users, groupId);

        using (var set = await admin.PutAsync(
                   $"/api/admin/page-permissions/groups/{groupId}",
                   new { pages = new[] { PortalPages.ContentProfile, PortalPages.ContentProjects } }))
        {
            set.StatusCode.ShouldBe(HttpStatusCode.NoContent, await set.Content.ReadAsStringAsync());
        }

        (await GroupPagesAsync(admin, groupId)).ShouldBe([PortalPages.ContentProfile, PortalPages.ContentProjects]);

        using (var unknown = await admin.PutAsync(
                   $"/api/admin/page-permissions/groups/{groupId}",
                   new { pages = new[] { PortalPages.Users, "no-such-page" } }))
        {
            unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        (await GroupPagesAsync(admin, groupId)).ShouldBe([PortalPages.ContentProfile, PortalPages.ContentProjects]);

        using (var clear = await admin.PutAsync($"/api/admin/page-permissions/groups/{groupId}", new { pages = Array.Empty<string>() }))
        {
            clear.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await GroupPagesAsync(admin, groupId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_a_group_removes_its_pages()
    {
        using var admin = await AdminAsync();
        var member = await factory.CreateUserAsync($"gone-{Guid.NewGuid():N}@example.com", Password);
        using var client = await SignedInAsync(member);

        var groupId = await CreateGroupAsync(admin, member.Id);
        await GrantAsync(admin, PortalPages.Applications, groupId);

        using (var allowed = await client.GetAsync("/api/admin/applications"))
        {
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var delete = await admin.DeleteAsync($"/api/admin/groups/{groupId}"))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var after = await client.GetAsync("/api/admin/applications");
        after.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_administrator_session_lists_every_page()
    {
        using var admin = await AdminAsync();

        (await SessionPagesAsync(admin)).ShouldBe(PortalPages.All.Select(page => page.Key), ignoreOrder: true);
    }

    // -----------------------------------------------------------------------
    // The catalog stays in step with the endpoints and the translations.
    // -----------------------------------------------------------------------

    [Fact]
    public void Every_administrative_endpoint_belongs_to_a_page_or_is_administrator_only()
    {
        var unguarded = AdministrativeActions()
            .Where(action => PagesOf(action.Method).Count == 0 && !IsAdministratorOnly(action.Method))
            .Select(action => action.Route)
            .ToList();

        unguarded.ShouldBeEmpty("Give these endpoints [RequirePortalPage(...)] or the AdministratorOnly policy.");
    }

    [Fact]
    public void Every_page_used_by_an_endpoint_is_in_the_catalog_and_every_catalog_page_is_used()
    {
        var catalog = PortalPages.All.Select(page => page.Key).ToHashSet(StringComparer.Ordinal);
        var used = AdministrativeActions().SelectMany(action => PagesOf(action.Method)).ToHashSet(StringComparer.Ordinal);

        used.Except(catalog).ShouldBeEmpty("These pages are required by an endpoint but missing from PortalPages.All.");
        catalog.Except(used).ShouldBeEmpty("These pages are in PortalPages.All but no endpoint requires them.");
    }

    [Fact]
    public void Every_catalog_page_has_its_label_and_area_translated()
    {
        using var scope = factory.Services.CreateScope();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<Messages>>();

        var missing = PortalPages.All
            .SelectMany(page => new[] { page.LabelKey, page.AreaKey })
            .Where(key => localizer[key].ResourceNotFound)
            .ToList();

        missing.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------

    private sealed record ControllerAction(MethodInfo Method, string Route);

    private static IEnumerable<ControllerAction> AdministrativeActions()
    {
        var controllers = typeof(PortalPages).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

        foreach (var controller in controllers)
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;

            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var http in method.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var template = http.Template ?? string.Empty;
                    var route = template.StartsWith("api/", StringComparison.Ordinal) || prefix.Length == 0
                        ? template
                        : template.Length == 0 ? prefix : $"{prefix}/{template}";

                    if (route.StartsWith("api/admin", StringComparison.Ordinal)
                        || route.StartsWith("api/manage", StringComparison.Ordinal))
                    {
                        yield return new ControllerAction(method, $"{string.Join(',', http.HttpMethods)} {route}");
                    }
                }
            }
        }
    }

    private static IReadOnlyList<string> PagesOf(MethodInfo method) =>
        method.GetCustomAttributes<RequirePortalPageAttribute>()
            .Concat(method.DeclaringType!.GetCustomAttributes<RequirePortalPageAttribute>())
            .SelectMany(attribute => attribute.Pages)
            .ToList();

    private static bool IsAdministratorOnly(MethodInfo method) =>
        method.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(method.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>())
            .Any(attribute => attribute.Policy == Policies.AdministratorOnly);

    private async Task<ApiClient> AdminAsync()
    {
        var admin = await factory.CreateUserAsync($"admin-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        return await SignedInAsync(admin);
    }

    private async Task<ApiClient> SignedInAsync(TestUser user)
    {
        var client = await ApiClient.CreateAsync(factory);
        using var signedIn = await client.SignInAsync(user.Email, user.Password);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return client;
    }

    private static async Task<Guid> CreateGroupAsync(ApiClient admin, params Guid[] memberIds)
    {
        using var created = await admin.PostAsync("/api/admin/groups", new { name = $"Group {Guid.NewGuid():N}" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var groupId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        foreach (var memberId in memberIds)
        {
            using var added = await admin.PutAsync($"/api/admin/groups/{groupId}/members/{memberId}", new { });
            added.StatusCode.ShouldBe(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        }

        return groupId;
    }

    private static async Task GrantAsync(ApiClient admin, string pageKey, Guid groupId)
    {
        using var granted = await admin.PutAsync($"/api/admin/page-permissions/{pageKey}/groups/{groupId}", new { });
        granted.StatusCode.ShouldBe(HttpStatusCode.NoContent, await granted.Content.ReadAsStringAsync());
    }

    private static async Task<IReadOnlyList<string?>> GroupPagesAsync(ApiClient admin, Guid groupId)
    {
        using var response = await admin.GetAsync($"/api/admin/groups/{groupId}");
        var group = await response.Content.ReadFromJsonAsync<JsonElement>();

        return group.GetProperty("pages").EnumerateArray().Select(page => page.GetString()).ToList();
    }

    private static async Task<IReadOnlyList<string?>> SessionPagesAsync(ApiClient client)
    {
        using var response = await client.GetAsync("/api/auth/session");
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();

        return session.GetProperty("user").GetProperty("pages").EnumerateArray().Select(page => page.GetString()).ToList();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString();
}
