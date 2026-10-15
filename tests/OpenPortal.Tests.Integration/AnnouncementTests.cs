using System.Net;
using System.Text.Json;
using OpenPortal.Notifications.Application.Auditing;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The banners shown across the portal: who sees them and when, in which language, and who may write them.
/// </summary>
public sealed class AnnouncementTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task Anonymous_visitors_see_only_announcements_meant_for_everyone()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var everyone = Unique("Maintenance tonight");
        var members = Unique("New feature");

        await CreateAsync(admin, new { severity = "maintenance", audience = "everyone", dismissible = false, texts = Texts(everyone) });
        await CreateAsync(admin, new { severity = "info", audience = "signed_in", dismissible = true, texts = Texts(members) });

        using var anonymous = await Scenario.ClientAsync(factory);
        var seen = await TitlesAsync(anonymous);
        seen.ShouldContain(everyone);
        seen.ShouldNotContain(members);

        var signedIn = await TitlesAsync(admin);
        signedIn.ShouldContain(everyone);
        signedIn.ShouldContain(members);

        // Maintenance comes first.
        var first = (await Scenario.GetJsonAsync(admin, "/api/announcements")).EnumerateArray().First();
        first.GetProperty("severity").GetString().ShouldBe("maintenance");
    }

    [Fact]
    public async Task Only_announcements_inside_their_window_show()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var future = Unique("Future");
        var past = Unique("Past");
        var now = DateTimeOffset.UtcNow;

        var scheduled = await CreateAsync(admin, new { severity = "info", audience = "everyone", startsAtUtc = now.AddDays(1), texts = Texts(future) });
        var ended = await CreateAsync(admin, new { severity = "info", audience = "everyone", startsAtUtc = now.AddDays(-2), endsAtUtc = now.AddDays(-1), texts = Texts(past) });

        scheduled.GetProperty("status").GetString().ShouldBe("scheduled");
        ended.GetProperty("status").GetString().ShouldBe("ended");

        var seen = await TitlesAsync(admin);
        seen.ShouldNotContain(future);
        seen.ShouldNotContain(past);
    }

    [Fact]
    public async Task The_reader_gets_their_language_and_falls_back_to_the_default()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var english = Unique("Hello");
        var italian = Unique("Ciao");

        await CreateAsync(admin, new
        {
            severity = "warning",
            audience = "everyone",
            texts = new[] { new { language = "en", title = english, body = "Body" }, new { language = "it", title = italian, body = "Testo" } },
        });

        using var anonymous = await Scenario.ClientAsync(factory);
        (await TitlesAsync(anonymous, "?lang=it")).ShouldContain(italian);
        (await TitlesAsync(anonymous, "?lang=fr")).ShouldContain(english);
    }

    [Fact]
    public async Task Editing_replaces_the_texts_and_deleting_removes_the_banner_with_both_audited()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var original = Unique("Original");
        var edited = Unique("Edited");

        var created = await CreateAsync(admin, new { severity = "info", audience = "everyone", texts = Texts(original) });
        var id = created.GetProperty("id").GetGuid();

        using (var update = await admin.PutAsync($"/api/admin/announcements/{id}", new
        {
            severity = "warning",
            audience = "everyone",
            texts = new[] { new { language = "en", title = edited, body = (string?)null }, new { language = "it", title = edited, body = (string?)null } },
        }))
        {
            var body = await Scenario.JsonAsync(update, HttpStatusCode.OK);
            body.GetProperty("texts").GetArrayLength().ShouldBe(2);
            body.GetProperty("severity").GetString().ShouldBe("warning");
        }

        var seen = await TitlesAsync(admin);
        seen.ShouldContain(edited);
        seen.ShouldNotContain(original);

        using (var delete = await admin.DeleteAsync($"/api/admin/announcements/{id}"))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await TitlesAsync(admin)).ShouldNotContain(edited);

        var audit = await Scenario.GetJsonAsync(admin, $"/api/admin/audit?subject={id}&pageSize=10");
        audit.GetProperty("items").EnumerateArray().Select(entry => entry.GetProperty("action").GetString())
            .ShouldBe([NotificationAuditActions.AnnouncementDeleted, NotificationAuditActions.AnnouncementUpdated, NotificationAuditActions.AnnouncementCreated]);
    }

    [Fact]
    public async Task Invalid_announcements_are_refused_with_their_own_code()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var now = DateTimeOffset.UtcNow;

        using (var noText = await admin.PostAsync("/api/admin/announcements", new { severity = "info", audience = "everyone", texts = Array.Empty<object>() }))
        {
            await Scenario.ExpectErrorAsync(noText, HttpStatusCode.BadRequest, "announcements.text_required");
        }

        using (var window = await admin.PostAsync("/api/admin/announcements", new { severity = "info", audience = "everyone", startsAtUtc = now, endsAtUtc = now.AddHours(-1), texts = Texts("x") }))
        {
            await Scenario.ExpectErrorAsync(window, HttpStatusCode.BadRequest, "announcements.window_invalid");
        }

        using (var kind = await admin.PostAsync("/api/admin/announcements", new { severity = "loud", audience = "everyone", texts = Texts("x") }))
        {
            await Scenario.ExpectErrorAsync(kind, HttpStatusCode.BadRequest, "announcements.invalid");
        }
    }

    [Fact]
    public async Task Managing_needs_the_announcements_page()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var editor = await factory.CreateUserAsync(Scenario.UniqueEmail("editor"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, editor);

        using (var refused = await client.GetAsync("/api/admin/announcements"))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        var group = await Scenario.CreateGroupAsync(admin, editor.Id);
        await Scenario.GrantPageAsync(admin, "announcements", group);

        using var allowed = await client.PostAsync("/api/admin/announcements", new { severity = "info", audience = "everyone", texts = Texts(Unique("Delegated")) });
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string Unique(string title) => $"{title} {Guid.NewGuid():N}";

    private static object[] Texts(string title) => [new { language = "en", title, body = "Details." }];

    private static async Task<JsonElement> CreateAsync(ApiClient admin, object body)
    {
        using var response = await admin.PostAsync("/api/admin/announcements", body);

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }

    private static async Task<IReadOnlyList<string?>> TitlesAsync(ApiClient client, string query = "") =>
        (await Scenario.GetJsonAsync(client, "/api/announcements" + query))
            .EnumerateArray()
            .Select(announcement => announcement.GetProperty("title").GetString())
            .ToList();
}
