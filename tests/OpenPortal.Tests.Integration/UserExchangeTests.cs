using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Tests.Integration;

/// <summary>The user list as a spreadsheet and back, and actions on many accounts at once.</summary>
public sealed class UserExchangeTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task The_export_is_csv_defused_and_hides_the_administrator_flag_from_delegates()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var formula = await factory.CreateUserAsync(Scenario.UniqueEmail("formula"), Scenario.Password);
        using (var update = await admin.PutAsync($"/api/admin/users/{formula.Id}", new { firstName = "Ada", lastName = "Formula", company = "=HYPERLINK(\"x\")", isAdministrator = false }))
        {
            update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        }

        using (var export = await admin.GetAsync($"/api/admin/users/export?search={Uri.EscapeDataString(formula.Email)}"))
        {
            export.StatusCode.ShouldBe(HttpStatusCode.OK);
            export.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
            var rows = Csv.Parse(await export.Content.ReadAsStringAsync());
            rows[0].ShouldContain("isAdministrator");
            rows.Count.ShouldBe(2);
            rows[1][0].ShouldBe(formula.Email);
            rows[1][rows[0].ToList().IndexOf("company")].ShouldBe("'=HYPERLINK(\"x\")");
        }

        var manager = await factory.CreateUserAsync(Scenario.UniqueEmail("manager"), Scenario.Password);
        var group = await Scenario.CreateGroupAsync(admin, manager.Id);
        await Scenario.GrantPageAsync(admin, "users", group);
        using var delegated = await Scenario.SignedInAsync(factory, manager);

        using var limited = await delegated.GetAsync("/api/admin/users/export");
        limited.StatusCode.ShouldBe(HttpStatusCode.OK);
        Csv.Parse(await limited.Content.ReadAsStringAsync())[0].ShouldNotContain("isAdministrator");

        var audit = await Scenario.GetJsonAsync(admin, "/api/admin/audit?category=user&pageSize=50");
        audit.GetProperty("items").EnumerateArray().Select(entry => entry.GetProperty("action").GetString()).ShouldContain("user.exported");
    }

    [Fact]
    public async Task The_import_invites_new_addresses_updates_known_ones_and_reports_each_row()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var existing = await factory.CreateUserAsync(Scenario.UniqueEmail("known"), Scenario.Password);
        var groupId = await Scenario.CreateGroupAsync(admin);
        var groupName = (await Scenario.GetJsonAsync(admin, $"/api/admin/groups/{groupId}")).GetProperty("name").GetString();
        var fresh = Scenario.UniqueEmail("fresh");

        var file = $"email;firstName;lastName;company;groups\n{fresh};Nuova;Persona;;{groupName}\n{existing.Email};;;Acme;\nnot-an-email;;;;\n{fresh};;;;\nghost@example.com;;;;No such group\n";

        // A dry run writes nothing.
        var preview = await ImportAsync(admin, file, dryRun: true, updateExisting: true);
        preview.GetProperty("dryRun").GetBoolean().ShouldBeTrue();
        Outcomes(preview).ShouldBe(["invited", "updated", "error", "error", "error"]);
        (await Scenario.GetJsonAsync(admin, $"/api/admin/invitations?search={Uri.EscapeDataString(fresh)}"))
            .GetProperty("items").GetArrayLength().ShouldBe(0);

        var result = await ImportAsync(admin, file, dryRun: false, updateExisting: true);
        Outcomes(result).ShouldBe(["invited", "updated", "error", "error", "error"]);
        result.GetProperty("rows")[2].GetProperty("errorCode").GetString().ShouldBe("users.import_email_invalid");
        result.GetProperty("rows")[3].GetProperty("errorCode").GetString().ShouldBe("users.import_duplicate");
        result.GetProperty("rows")[4].GetProperty("errorCode").GetString().ShouldBe("users.import_group_unknown");

        (await Scenario.GetJsonAsync(admin, $"/api/admin/invitations?search={Uri.EscapeDataString(fresh)}"))
            .GetProperty("items").GetArrayLength().ShouldBe(1);

        var updated = await Scenario.GetJsonAsync(admin, $"/api/admin/users/{existing.Id}");
        updated.GetProperty("company").GetString().ShouldBe("Acme");
        updated.GetProperty("firstName").GetString().ShouldNotBeNullOrEmpty();

        // Without updateExisting, known accounts are left alone.
        var again = await ImportAsync(admin, $"email,company\n{existing.Email},Other\n", dryRun: false, updateExisting: false);
        Outcomes(again).ShouldBe(["skipped"]);
    }

    [Fact]
    public async Task A_file_without_an_email_column_is_refused()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);

        using var content = Form("name\nsomeone\n");
        using var response = await admin.PostFormDataAsync("/api/admin/users/import", content);
        await Scenario.ExpectErrorAsync(response, HttpStatusCode.BadRequest, "users.import_no_email_column");
    }

    [Fact]
    public async Task Bulk_actions_report_each_account_and_respect_the_usual_checks()
    {
        var (admin, adminUser) = await Scenario.AdminAsync(factory);
        var first = await factory.CreateUserAsync(Scenario.UniqueEmail("bulk1"), Scenario.Password);
        var second = await factory.CreateUserAsync(Scenario.UniqueEmail("bulk2"), Scenario.Password);
        var groupId = await Scenario.CreateGroupAsync(admin);

        var added = await BulkAsync(admin, new { action = "add_to_group", userIds = new[] { first.Id, second.Id }, groupId });
        added.EnumerateArray().All(result => result.GetProperty("succeeded").GetBoolean()).ShouldBeTrue();
        (await Scenario.GetJsonAsync(admin, $"/api/admin/groups/{groupId}")).GetProperty("members").GetArrayLength().ShouldBe(2);

        using var session = await Scenario.SignedInAsync(factory, first);
        var signedOut = await BulkAsync(admin, new { action = "sign_out", userIds = new[] { first.Id, adminUser.Id } });
        signedOut[0].GetProperty("succeeded").GetBoolean().ShouldBeTrue();
        signedOut[1].GetProperty("succeeded").GetBoolean().ShouldBeFalse();

        var deleted = await BulkAsync(admin, new { action = "delete", userIds = new[] { second.Id } });
        deleted[0].GetProperty("succeeded").GetBoolean().ShouldBeTrue();
        using (var gone = await admin.GetAsync($"/api/admin/users/{second.Id}"))
        {
            gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var missingGroup = await admin.PostAsync("/api/admin/users/bulk", new { action = "add_to_group", userIds = new[] { first.Id } });
        await Scenario.ExpectErrorAsync(missingGroup, HttpStatusCode.BadRequest, "users.bulk_group_required");
    }

    private static List<string?> Outcomes(JsonElement result) =>
        result.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("outcome").GetString()).ToList();

    private static MultipartFormDataContent Form(string csv)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        return new MultipartFormDataContent { { file, "file", "users.csv" } };
    }

    private static async Task<JsonElement> ImportAsync(ApiClient admin, string csv, bool dryRun, bool updateExisting)
    {
        using var content = Form(csv);
        using var response = await admin.PostFormDataAsync(
            $"/api/admin/users/import?dryRun={dryRun.ToString().ToLowerInvariant()}&updateExisting={updateExisting.ToString().ToLowerInvariant()}",
            content);

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }

    private static async Task<JsonElement> BulkAsync(ApiClient admin, object body)
    {
        using var response = await admin.PostAsync("/api/admin/users/bulk", body);

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }
}
