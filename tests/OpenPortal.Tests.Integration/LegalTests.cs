using System.Net;
using System.Text.Json;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The privacy notice and the terms of use: drafts, publishing, the acceptance each account owes the version in
/// force, and the places that insist on it (registration, the applications' sign-in).
/// </summary>
public sealed class LegalTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task Publishing_a_version_asks_every_account_to_accept_it_once()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var member = await factory.CreateUserAsync(Scenario.UniqueEmail("member"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, member);

        var published = await PublishAsync(admin, "terms", "Terms of use");
        var id = published.GetProperty("id").GetGuid();

        var pending = PendingIds(await Scenario.GetJsonAsync(client, "/api/auth/session"));
        pending.ShouldContain(id);

        using var anonymous = await Scenario.ClientAsync(factory);
        var current = await Scenario.GetJsonAsync(anonymous, "/api/legal/terms");
        current.GetProperty("id").GetGuid().ShouldBe(id);
        current.GetProperty("body").GetString()!.ShouldContain("# Terms of use");
        (await Scenario.GetJsonAsync(anonymous, "/api/auth/session")).GetProperty("legal").GetProperty("terms").GetBoolean().ShouldBeTrue();

        using (var accept = await client.PostAsync("/api/account/legal/accept", new { documentIds = pending }))
        {
            (await Scenario.JsonAsync(accept, HttpStatusCode.OK)).GetArrayLength().ShouldBe(0);
        }

        PendingIds(await Scenario.GetJsonAsync(client, "/api/auth/session")).ShouldBeEmpty();

        // A new version is owed again; the old id is no longer accepted.
        var next = await PublishAsync(admin, "terms", "Terms of use, v2");
        var owed = PendingIds(await Scenario.GetJsonAsync(client, "/api/auth/session"));
        owed.ShouldContain(next.GetProperty("id").GetGuid());
        owed.ShouldNotContain(id);

        using (var stale = await client.PostAsync("/api/account/legal/accept", new { documentIds = new[] { id } }))
        {
            await Scenario.ExpectErrorAsync(stale, HttpStatusCode.BadRequest, "legal.acceptance_required");
        }

        var list = await Scenario.GetJsonAsync(admin, "/api/admin/legal");
        var first = list.EnumerateArray().Single(document => document.GetProperty("id").GetGuid() == id);
        first.GetProperty("status").GetString().ShouldBe("superseded");
        first.GetProperty("acceptanceCount").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        var audit = await Scenario.GetJsonAsync(admin, $"/api/admin/audit?subject={next.GetProperty("id").GetGuid()}");
        audit.GetProperty("items").EnumerateArray().Select(entry => entry.GetProperty("action").GetString()).ShouldContain("legal.document_published");
    }

    [Fact]
    public async Task Only_drafts_change_and_there_is_one_draft_per_kind()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);

        var draft = await DraftAsync(admin, "privacy", "Privacy");
        var id = draft.GetProperty("id").GetGuid();
        draft.GetProperty("status").GetString().ShouldBe("draft");

        using (var second = await admin.PostAsync("/api/admin/legal/privacy", Body("Another")))
        {
            await Scenario.ExpectErrorAsync(second, HttpStatusCode.Conflict, "legal.draft_exists");
        }

        using (var edited = await admin.PutAsync($"/api/admin/legal/{id}", Body("Privacy, edited")))
        {
            (await Scenario.JsonAsync(edited, HttpStatusCode.OK)).GetProperty("texts")[0].GetProperty("title").GetString().ShouldBe("Privacy, edited");
        }

        using (var publish = await admin.PostAsync($"/api/admin/legal/{id}/publish"))
        {
            publish.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var frozen = await admin.PutAsync($"/api/admin/legal/{id}", Body("Too late")))
        {
            await Scenario.ExpectErrorAsync(frozen, HttpStatusCode.Conflict, "legal.not_draft");
        }

        using (var kept = await admin.DeleteAsync($"/api/admin/legal/{id}"))
        {
            await Scenario.ExpectErrorAsync(kept, HttpStatusCode.Conflict, "legal.not_draft");
        }

        var next = await DraftAsync(admin, "privacy", "Privacy v2");
        next.GetProperty("version").GetInt32().ShouldBe(draft.GetProperty("version").GetInt32() + 1);

        using (var removed = await admin.DeleteAsync($"/api/admin/legal/{next.GetProperty("id").GetGuid()}"))
        {
            removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var empty = await admin.PostAsync("/api/admin/legal/privacy", new { requiresAcceptance = true, texts = Array.Empty<object>() }))
        {
            await Scenario.ExpectErrorAsync(empty, HttpStatusCode.BadRequest, "legal.text_required");
        }
    }

    [Fact]
    public async Task Registration_needs_the_versions_in_force()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        using (var open = await admin.PutAsync("/api/admin/settings/registration", new
        {
            mode = "open",
            allowedDomains = Array.Empty<string>(),
            requireEmailConfirmation = false,
            invitationLifetimeDays = 7,
        }))
        {
            open.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await PublishAsync(admin, "terms", "Terms");
        await PublishAsync(admin, "privacy", "Privacy");

        using var anonymous = await Scenario.ClientAsync(factory);
        var terms = (await Scenario.GetJsonAsync(anonymous, "/api/legal/terms")).GetProperty("id").GetGuid();
        var privacy = (await Scenario.GetJsonAsync(anonymous, "/api/legal/privacy")).GetProperty("id").GetGuid();

        var email = Scenario.UniqueEmail("newcomer");
        using (var refused = await anonymous.PostAsync("/api/auth/register", new { email, password = Scenario.Password, firstName = "New", lastName = "Person", acceptedLegalDocumentIds = new[] { terms } }))
        {
            await Scenario.ExpectErrorAsync(refused, HttpStatusCode.BadRequest, "legal.acceptance_required");
        }

        using (var registered = await anonymous.PostAsync("/api/auth/register", new { email, password = Scenario.Password, firstName = "New", lastName = "Person", acceptedLegalDocumentIds = new[] { terms, privacy } }))
        {
            (await Scenario.JsonAsync(registered, HttpStatusCode.OK)).GetProperty("outcome").GetString().ShouldBe("signed_in");
        }

        PendingIds(await Scenario.GetJsonAsync(anonymous, "/api/auth/session")).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_application_sign_in_waits_for_the_acceptance()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("signer"), Scenario.Password);
        var app = await Scenario.CreateApplicationAsync(admin);
        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{app.Id}/users/{user.Id}", new { }))
        {
            granted.StatusCode.ShouldBe(HttpStatusCode.NoContent, await granted.Content.ReadAsStringAsync());
        }

        await PublishAsync(admin, "terms", "Terms for apps");

        var (location, _) = await Scenario.AuthorizeAsync(factory, user, app.ClientId, "openid");
        location.OriginalString.ShouldStartWith("/legal/accept?returnUrl=%2Fconnect%2Fauthorize");
    }

    [Fact]
    public async Task Editing_needs_the_legal_page()
    {
        var editor = await factory.CreateUserAsync(Scenario.UniqueEmail("editor"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, editor);

        using var refused = await client.GetAsync("/api/admin/legal");
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static object Body(string title) => new
    {
        requiresAcceptance = true,
        texts = new[] { new { language = "en", title, body = $"# {title}\n\nWe keep your data safe." } },
    };

    private static async Task<JsonElement> DraftAsync(ApiClient admin, string kind, string title)
    {
        using var response = await admin.PostAsync($"/api/admin/legal/{kind}", Body(title));

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }

    private static async Task<JsonElement> PublishAsync(ApiClient admin, string kind, string title)
    {
        var draft = await DraftAsync(admin, kind, title);
        using var response = await admin.PostAsync($"/api/admin/legal/{draft.GetProperty("id").GetGuid()}/publish");

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }

    private static List<Guid> PendingIds(JsonElement session) =>
        session.GetProperty("user").GetProperty("pendingLegalDocuments").EnumerateArray().Select(document => document.GetProperty("id").GetGuid()).ToList();
}
