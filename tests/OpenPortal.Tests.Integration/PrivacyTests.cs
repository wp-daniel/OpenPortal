using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Web.Privacy;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The account holder's privacy rights: a copy of their data, and the deletion of their account after a grace
/// period they can cancel in.
/// </summary>
public sealed class PrivacyTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task The_export_holds_every_section_and_no_secret()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("owner"), Scenario.Password);
        await Scenario.CreateGroupAsync(admin, user.Id);
        using var client = await Scenario.SignedInAsync(factory, user);

        using (var wrong = await client.PostAsync("/api/account/data-export", new { password = "not-the-password" }))
        {
            await Scenario.ExpectErrorAsync(wrong, HttpStatusCode.BadRequest, "identity.password_incorrect");
        }

        using var export = await client.PostAsync("/api/account/data-export", new { password = Scenario.Password });
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.ShouldBe("application/zip");
        export.Content.Headers.ContentDisposition!.FileName!.ShouldContain("openportal-my-data-");

        await using var stream = await export.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream);
        zip.Entries.Select(entry => entry.Name).ShouldContain("data.json");
        zip.Entries.Select(entry => entry.Name).ShouldContain("README.txt");

        using var reader = new StreamReader(zip.GetEntry("data.json")!.Open());
        var text = await reader.ReadToEndAsync();
        var data = JsonDocument.Parse(text).RootElement;
        var sections = data.GetProperty("sections");

        sections.GetProperty("account").GetProperty("profile").GetProperty("email").GetString().ShouldBe(user.Email);
        sections.GetProperty("access").GetProperty("groups").GetArrayLength().ShouldBe(1);
        sections.GetProperty("notifications").GetArrayLength().ShouldBeGreaterThan(0);
        sections.GetProperty("activity").EnumerateArray()
            .Select(entry => entry.GetProperty("action").GetString())
            .ShouldContain("auth.sign_in");

        text.ShouldNotContain("PasswordHash", Case.Insensitive);
        text.ShouldNotContain("SecurityStamp", Case.Insensitive);
        text.ShouldNotContain("AuthenticatorKey", Case.Insensitive);
    }

    [Fact]
    public async Task A_deletion_request_signs_out_waits_and_can_be_cancelled()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("leaver"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, user);

        using (var requested = await client.PostAsync("/api/account/deletion", new { password = Scenario.Password }))
        {
            var deletion = await Scenario.JsonAsync(requested, HttpStatusCode.OK);
            deletion.GetProperty("scheduledAtUtc").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(29));
        }

        (await Scenario.GetJsonAsync(client, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();

        // Signing in again is allowed, and says when the account goes.
        using var again = await Scenario.SignedInAsync(factory, user);
        var session = await Scenario.GetJsonAsync(again, "/api/auth/session");
        session.GetProperty("user").GetProperty("deletionScheduledAtUtc").ValueKind.ShouldBe(JsonValueKind.String);

        var pending = await Scenario.GetJsonAsync(admin, "/api/admin/users?status=deletion&pageSize=100");
        pending.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("email").GetString()).ShouldContain(user.Email);
        (await Scenario.NotificationTypesAsync(admin)).ShouldContain(IdentityNotificationTypes.DeletionRequested);

        using (var cancelled = await again.DeleteAsync("/api/account/deletion"))
        {
            cancelled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await Scenario.GetJsonAsync(again, "/api/auth/session"))
            .GetProperty("user").GetProperty("deletionScheduledAtUtc").ValueKind.ShouldBe(JsonValueKind.Null);

        using var twice = await again.DeleteAsync("/api/account/deletion");
        await Scenario.ExpectErrorAsync(twice, HttpStatusCode.Conflict, "identity.deletion_not_scheduled");
    }

    [Fact]
    public async Task The_sweep_deletes_accounts_whose_grace_period_is_over()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        using (var settings = await admin.PutAsync("/api/admin/settings/privacy", new { selfDeletionEnabled = true, deletionGraceDays = 0 }))
        {
            settings.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        try
        {
            var user = await factory.CreateUserAsync(Scenario.UniqueEmail("gone"), Scenario.Password);
            await Scenario.CreateGroupAsync(admin, user.Id);

            using (var client = await Scenario.SignedInAsync(factory, user))
            using (var requested = await client.PostAsync("/api/account/deletion", new { password = Scenario.Password }))
            {
                requested.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            var deleted = await factory.Services.GetRequiredService<AccountDeletionService>().SweepAsync(CancellationToken.None);
            deleted.ShouldBeGreaterThanOrEqualTo(1);

            using (var signIn = await (await Scenario.ClientAsync(factory)).SignInAsync(user.Email, user.Password))
            {
                signIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }

            var audit = await Scenario.GetJsonAsync(admin, $"/api/admin/audit?subject={user.Id}&pageSize=50");
            var entry = audit.GetProperty("items").EnumerateArray().First(item => item.GetProperty("action").GetString() == "user.deleted");
            entry.GetProperty("actor").GetProperty("type").GetString().ShouldBe("system");
        }
        finally
        {
            using var restore = await admin.PutAsync("/api/admin/settings/privacy", new { selfDeletionEnabled = true, deletionGraceDays = 30 });
        }
    }

    [Fact]
    public async Task Deletion_can_be_switched_off_and_never_removes_the_last_administrator()
    {
        var (admin, adminUser) = await Scenario.AdminAsync(factory);

        using (var off = await admin.PutAsync("/api/admin/settings/privacy", new { selfDeletionEnabled = false, deletionGraceDays = 30 }))
        {
            (await Scenario.JsonAsync(off, HttpStatusCode.OK)).GetProperty("selfDeletionEnabled").GetBoolean().ShouldBeFalse();
        }

        try
        {
            var user = await factory.CreateUserAsync(Scenario.UniqueEmail("stayer"), Scenario.Password);
            using var client = await Scenario.SignedInAsync(factory, user);
            (await Scenario.GetJsonAsync(client, "/api/auth/session")).GetProperty("features").GetProperty("selfDeletion").GetBoolean().ShouldBeFalse();

            using var refused = await client.PostAsync("/api/account/deletion", new { password = Scenario.Password });
            await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Forbidden, "identity.self_deletion_disabled");
        }
        finally
        {
            using var on = await admin.PutAsync("/api/admin/settings/privacy", new { selfDeletionEnabled = true, deletionGraceDays = 30 });
        }

        using var invalid = await admin.PutAsync("/api/admin/settings/privacy", new { selfDeletionEnabled = true, deletionGraceDays = 400 });
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Privacy_settings_are_for_administrators_only()
    {
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("curious"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, user);

        using var refused = await client.GetAsync("/api/admin/settings/privacy");
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
