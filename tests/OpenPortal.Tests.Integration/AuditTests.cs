using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Content.Application.Auditing;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Oidc;
using OpenPortal.Web.Resources;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The audit log: what the modules record, who may read it, and the last sign-in it goes with. Every entry is
/// checked through the same endpoint the audit page uses, so the test proves the whole path.
/// </summary>
public sealed class AuditTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    private const string Password = "Correct-Horse-9";

    [Fact]
    public async Task Sign_ins_are_recorded_with_their_outcome_and_set_the_last_sign_in()
    {
        using var admin = await AdminAsync();
        var user = await factory.CreateUserAsync($"audited-{Guid.NewGuid():N}@example.com", Password);
        var unknown = $"nobody-{Guid.NewGuid():N}@example.com";

        using (var anonymous = await ApiClient.CreateAsync(factory))
        {
            using var wrong = await anonymous.SignInAsync(user.Email, "Wrong-Password-1");
            wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            using var missing = await anonymous.SignInAsync(unknown, Password);
            missing.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await LastSignInAsync(admin, user.Email)).ShouldBeNull();

        using (var client = await SignedInAsync(user))
        {
            await client.SignOutAsync();
        }

        (await LastSignInAsync(admin, user.Email)).ShouldNotBeNull();

        var entries = await EntriesAsync(admin, $"subject={user.Id}&category=auth");
        entries.Select(entry => (Action(entry), Outcome(entry))).ShouldBe(
        [
            ("auth.sign_out", "success"),
            ("auth.sign_in", "success"),
            ("auth.sign_in", "failure"),
        ]);

        var failure = entries.Last();
        failure.GetProperty("actor").GetProperty("label").GetString().ShouldBe(user.Email);
        failure.GetProperty("details").GetProperty("reason").GetString().ShouldBe("invalid_password");
        failure.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();

        // An unknown address has no account to name; the address typed is kept so guessing shows up.
        var guess = (await EntriesAsync(admin, $"search={Uri.EscapeDataString(unknown)}")).ShouldHaveSingleItem();
        Outcome(guess).ShouldBe("failure");
        guess.GetProperty("details").GetProperty("reason").GetString().ShouldBe("unknown_account");
    }

    [Fact]
    public async Task Administrative_changes_name_who_did_them_and_to_whom()
    {
        var administrator = await factory.CreateUserAsync($"admin-{Guid.NewGuid():N}@example.com", Password, "Administrator");
        using var admin = await SignedInAsync(administrator);
        var email = $"created-{Guid.NewGuid():N}@example.com";

        using var created = await admin.PostAsync("/api/admin/users", new
        {
            email,
            password = Password,
            firstName = "Grace",
            lastName = "Hopper",
            isAdministrator = false,
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using (var promoted = await admin.PutAsync($"/api/admin/users/{userId}", new { firstName = "Grace", lastName = "Hopper", isAdministrator = true }))
        {
            promoted.StatusCode.ShouldBe(HttpStatusCode.OK, await promoted.Content.ReadAsStringAsync());
        }

        using (var deleted = await admin.DeleteAsync($"/api/admin/users/{userId}"))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var entries = await EntriesAsync(admin, $"subject={userId}&category=user");
        entries.Select(Action).ShouldBe(["user.deleted", "user.administrator_granted", "user.updated", "user.created"]);

        foreach (var entry in entries)
        {
            entry.GetProperty("actor").GetProperty("id").GetString().ShouldBe(administrator.Id.ToString());
            entry.GetProperty("actor").GetProperty("label").GetString().ShouldBe(administrator.Email);

            // The label is a snapshot: the account is gone, its email stays readable.
            entry.GetProperty("target").GetProperty("label").GetString().ShouldBe(email);
        }

        entries.Last().GetProperty("details").GetProperty("administrator").GetString().ShouldBe("false");
    }

    [Fact]
    public async Task Group_and_grant_changes_are_recorded_once_and_idempotent_repeats_are_not()
    {
        using var admin = await AdminAsync();
        var member = await factory.CreateUserAsync($"member-{Guid.NewGuid():N}@example.com", Password);

        using var group = await admin.PostAsync("/api/admin/groups", new { name = $"Audited {Guid.NewGuid():N}" });
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var added = await admin.PutAsync($"/api/admin/groups/{groupId}/members/{member.Id}", new { });
            added.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var entries = await EntriesAsync(admin, $"subject={member.Id}&category=group");
        var added1 = entries.ShouldHaveSingleItem();
        Action(added1).ShouldBe("group.member_added");
        added1.GetProperty("details").GetProperty("groupId").GetString().ShouldBe(groupId.ToString());
    }

    [Fact]
    public async Task The_log_needs_the_audit_page_and_holders_of_it_can_read_it()
    {
        using var admin = await AdminAsync();
        var reader = await factory.CreateUserAsync($"auditor-{Guid.NewGuid():N}@example.com", Password);
        using var client = await SignedInAsync(reader);

        using (var refused = await client.GetAsync("/api/admin/audit"))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var refusedExport = await client.GetAsync("/api/admin/audit/export"))
        {
            refusedExport.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var group = await admin.PostAsync("/api/admin/groups", new { name = $"Auditors {Guid.NewGuid():N}" });
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await admin.PutAsync($"/api/admin/groups/{groupId}/members/{reader.Id}", new { })).Dispose();
        (await admin.PutAsync($"/api/admin/page-permissions/{PortalPages.Audit}/groups/{groupId}", new { })).Dispose();

        using var allowed = await client.GetAsync("/api/admin/audit?pageSize=5");
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
        var page = await allowed.Content.ReadFromJsonAsync<JsonElement>();
        page.GetProperty("pageSize").GetInt32().ShouldBe(5);
        page.GetProperty("totalCount").GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task The_export_is_csv_and_defuses_values_a_spreadsheet_would_run()
    {
        using var admin = await AdminAsync();

        // A failed sign-in with an address that looks like a formula ends up in the actor label.
        using (var anonymous = await ApiClient.CreateAsync(factory))
        {
            (await anonymous.SignInAsync("=HYPERLINK(\"x\")@example.com", Password)).Dispose();
        }

        using var export = await admin.GetAsync("/api/admin/audit/export?category=auth&outcome=failure");
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        export.Content.Headers.ContentDisposition!.FileName!.ShouldContain("openportal-audit-");

        var csv = await export.Content.ReadAsStringAsync();
        csv.ShouldStartWith("occurredAtUtc,action,outcome,");
        csv.ShouldContain("\"'=HYPERLINK(\"\"x\"\")@example.com\"");
        csv.ShouldNotContain(",=HYPERLINK");
    }

    [Fact]
    public async Task The_inactive_filter_lists_accounts_that_never_signed_in()
    {
        using var admin = await AdminAsync();
        var dormant = await factory.CreateUserAsync($"dormant-{Guid.NewGuid():N}@example.com", Password);
        var active = await factory.CreateUserAsync($"active-{Guid.NewGuid():N}@example.com", Password);
        (await SignedInAsync(active)).Dispose();

        using var response = await admin.GetAsync("/api/admin/users?status=inactive&pageSize=100");
        var emails = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("email").GetString())
            .ToList();

        emails.ShouldContain(dormant.Email);
        emails.ShouldNotContain(active.Email);
    }

    [Fact]
    public void Every_audit_action_has_a_label_in_the_resources()
    {
        using var scope = factory.Services.CreateScope();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<Messages>>();

        var actions = new[] { typeof(IdentityAuditActions), typeof(AccessAuditActions), typeof(ContentAuditActions), typeof(OidcAuditActions) }
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        actions.Count.ShouldBeGreaterThan(40);
        actions.Where(action => localizer[$"audit.action.{action}"].ResourceNotFound).ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------

    private static string Action(JsonElement entry) => entry.GetProperty("action").GetString()!;

    private static string Outcome(JsonElement entry) => entry.GetProperty("outcome").GetString()!;

    private static async Task<IReadOnlyList<JsonElement>> EntriesAsync(ApiClient admin, string query)
    {
        using var response = await admin.GetAsync($"/api/admin/audit?pageSize=100&{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
    }

    private static async Task<string?> LastSignInAsync(ApiClient admin, string email)
    {
        using var response = await admin.GetAsync($"/api/admin/users?search={Uri.EscapeDataString(email)}");
        var user = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().Single();

        return user.GetProperty("lastSignInAtUtc").ValueKind == JsonValueKind.Null ? null : user.GetProperty("lastSignInAtUtc").GetString();
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
}
