using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Personal access tokens on the portal's own API: switched on by an administrator, created by their owner, scoped,
/// expiring and revocable, and never a key to the account's sensitive settings.
/// </summary>
public sealed class PersonalAccessTokenTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task A_read_token_reads_and_a_write_token_writes_without_antiforgery()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        await EnableAsync(admin, true);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("scripter"), Scenario.Password);
        using var owner = await Scenario.SignedInAsync(factory, user);

        (await Scenario.GetJsonAsync(owner, "/api/auth/session")).GetProperty("features").GetProperty("personalAccessTokens").GetBoolean().ShouldBeTrue();

        var read = await CreateAsync(owner, "read only", ["read"]);
        var write = await CreateAsync(owner, "automation", ["write"]);
        read.GetProperty("value").GetString()!.ShouldStartWith("opp_");
        write.GetProperty("token").GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()).ShouldBe(["read", "write"]);

        using var reader = Bearer(read.GetProperty("value").GetString()!);
        var profile = await reader.GetFromJsonAsync<JsonElement>("/api/account/profile");
        profile.GetProperty("email").GetString().ShouldBe(user.Email);

        using (var refused = await reader.PutAsJsonAsync("/api/account/profile", new { firstName = "No", lastName = "Way" }))
        {
            await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Forbidden, "auth.token_scope_insufficient");
        }

        using var writer = Bearer(write.GetProperty("value").GetString()!);
        using (var changed = await writer.PutAsJsonAsync("/api/account/profile", new { firstName = "Script", lastName = "Changed" }))
        {
            changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
        }

        // Never the sensitive settings, whatever the scope.
        using (var tokens = await writer.PostAsJsonAsync("/api/account/tokens", new { name = "more", scopes = new[] { "write" }, lifetimeDays = 30 }))
        {
            await Scenario.ExpectErrorAsync(tokens, HttpStatusCode.Forbidden, "auth.interactive_session_required");
        }

        using (var password = await writer.PostAsJsonAsync("/api/account/change-password", new { currentPassword = Scenario.Password, newPassword = "Another-Password-2026!" }))
        {
            await Scenario.ExpectErrorAsync(password, HttpStatusCode.Forbidden, "auth.interactive_session_required");
        }

        var listed = await Scenario.GetJsonAsync(owner, "/api/account/tokens");
        listed.EnumerateArray().Select(token => token.GetProperty("lastUsedAtUtc").ValueKind).ShouldAllBe(kind => kind == JsonValueKind.String);
    }

    [Fact]
    public async Task Revoked_tokens_bad_tokens_and_disabled_tokens_are_refused()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        await EnableAsync(admin, true);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("revoker"), Scenario.Password);
        using var owner = await Scenario.SignedInAsync(factory, user);

        var created = await CreateAsync(owner, "temporary", ["read"]);
        var value = created.GetProperty("value").GetString()!;
        using var client = Bearer(value);

        (await client.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Bearer("opp_not-a-real-token").GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await EnableAsync(admin, false);
        (await client.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await EnableAsync(admin, true);

        using (var revoke = await owner.DeleteAsync($"/api/account/tokens/{created.GetProperty("token").GetProperty("id").GetGuid()}"))
        {
            revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await client.GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_administrator_sees_and_revokes_a_user_s_tokens_and_signing_out_revokes_them_all()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        await EnableAsync(admin, true);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("audited"), Scenario.Password);
        using var owner = await Scenario.SignedInAsync(factory, user);

        var first = await CreateAsync(owner, "first", ["read"]);
        var second = await CreateAsync(owner, "second", ["read"]);

        var tokens = await Scenario.GetJsonAsync(admin, $"/api/admin/users/{user.Id}/tokens");
        tokens.GetArrayLength().ShouldBe(2);
        tokens.GetRawText().ShouldNotContain(first.GetProperty("value").GetString()!);

        using (var revoke = await admin.DeleteAsync($"/api/admin/users/{user.Id}/tokens/{first.GetProperty("token").GetProperty("id").GetGuid()}"))
        {
            revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await Bearer(first.GetProperty("value").GetString()!).GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using (var bulk = await admin.PostAsync("/api/admin/users/bulk", new { action = "sign_out", userIds = new[] { user.Id } }))
        {
            bulk.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await Bearer(second.GetProperty("value").GetString()!).GetAsync("/api/account/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tokens_cannot_be_created_while_switched_off()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        await EnableAsync(admin, false);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("early"), Scenario.Password);
        using var owner = await Scenario.SignedInAsync(factory, user);

        using var refused = await owner.PostAsync("/api/account/tokens", new { name = "x", scopes = new[] { "read" }, lifetimeDays = 30 });
        await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Forbidden, "tokens.disabled");
    }

    private HttpClient Bearer(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task EnableAsync(ApiClient admin, bool enabled)
    {
        using var response = await admin.PutAsync("/api/admin/settings/security", new
        {
            twoFactorEnabled = false,
            twoFactorIssuer = "OpenPortal",
            personalAccessTokensEnabled = enabled,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> CreateAsync(ApiClient owner, string name, string[] scopes)
    {
        using var response = await owner.PostAsync("/api/account/tokens", new { name, scopes, lifetimeDays = 30 });

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }
}
