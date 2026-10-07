using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenPortal.Web.Authorization;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Deleting an account removes it and everything the Access module held about it, and follows the same rules
/// as editing: nobody deletes themselves, only an administrator deletes an administrator.
/// </summary>
public sealed class UserDeletionTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private const string Password = "Correct-Horse-9";

    [Fact]
    public async Task Deleting_a_user_removes_the_account_and_its_group_memberships()
    {
        using var admin = await AdminAsync();
        var target = await factory.CreateUserAsync($"leaving-{Guid.NewGuid():N}@example.com", Password);
        var groupId = await CreateGroupAsync(admin, target.Id);

        using (var deleted = await admin.DeleteAsync($"/api/admin/users/{target.Id}"))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
        }

        using (var read = await admin.GetAsync($"/api/admin/users/{target.Id}"))
        {
            read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using (var group = await admin.GetAsync($"/api/admin/groups/{groupId}"))
        {
            (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("members").GetArrayLength().ShouldBe(0);
        }

        using var again = await admin.DeleteAsync($"/api/admin/users/{target.Id}");
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Nobody_can_delete_their_own_account()
    {
        var admin = await factory.CreateUserAsync($"self-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        using var client = await SignedInAsync(admin);

        using var response = await client.DeleteAsync($"/api/admin/users/{admin.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).ShouldBe("identity.cannot_delete_self");
    }

    [Fact]
    public async Task A_delegated_users_page_deletes_ordinary_accounts_but_not_administrators()
    {
        using var admin = await AdminAsync();
        var member = await factory.CreateUserAsync($"hr-{Guid.NewGuid():N}@example.com", Password);
        var otherAdmin = await factory.CreateUserAsync($"boss-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        var plain = await factory.CreateUserAsync($"plain-{Guid.NewGuid():N}@example.com", Password);

        var groupId = await CreateGroupAsync(admin, member.Id);
        using (var grant = await admin.PutAsync($"/api/admin/page-permissions/{PortalPages.Users}/groups/{groupId}", new { }))
        {
            grant.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var client = await SignedInAsync(member);

        using (var deleteAdmin = await client.DeleteAsync($"/api/admin/users/{otherAdmin.Id}"))
        {
            deleteAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await ErrorCodeAsync(deleteAdmin)).ShouldBe("identity.administrator_required");
        }

        using var deletePlain = await client.DeleteAsync($"/api/admin/users/{plain.Id}");
        deletePlain.StatusCode.ShouldBe(HttpStatusCode.NoContent, await deletePlain.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_administrator_flag_is_set_and_cleared_by_an_administrator()
    {
        using var admin = await AdminAsync();
        var target = await factory.CreateUserAsync($"flag-{Guid.NewGuid():N}@example.com", Password);

        async Task<bool> UpdateAsync(bool isAdministrator)
        {
            using var response = await admin.PutAsync(
                $"/api/admin/users/{target.Id}",
                new { firstName = "Flag", lastName = "Target", isAdministrator });
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isAdministrator").GetBoolean();
        }

        (await UpdateAsync(true)).ShouldBeTrue();
        (await UpdateAsync(false)).ShouldBeFalse();
    }

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

    private static async Task<Guid> CreateGroupAsync(ApiClient admin, Guid memberId)
    {
        using var created = await admin.PostAsync("/api/admin/groups", new { name = $"Group {Guid.NewGuid():N}" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var groupId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var added = await admin.PutAsync($"/api/admin/groups/{groupId}/members/{memberId}", new { });
        added.StatusCode.ShouldBe(HttpStatusCode.OK);

        return groupId;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString();
}
