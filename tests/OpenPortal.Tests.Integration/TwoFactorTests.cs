using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Two-factor authentication with an authenticator app: the portal setting that turns it on, enrolment, the
/// second sign-in step, recovery codes, and an administrator's reset.
/// </summary>
public sealed class TwoFactorTests : IClassFixture<OpenPortalFactory>
{
    private const string SettingsPath = "/api/admin/settings/security";

    private readonly OpenPortalFactory _factory;

    public TwoFactorTests(OpenPortalFactory factory) => _factory = factory;

    [Fact]
    public async Task Setup_is_refused_while_the_portal_setting_is_off()
    {
        await SetPortalTwoFactorAsync(enabled: false);
        var (client, _) = await SignInAsync("tfa-off@example.com");

        var status = await ReadJsonAsync(await client.GetAsync("/api/account/two-factor"));
        status.GetProperty("available").GetBoolean().ShouldBeFalse();
        status.GetProperty("enabled").GetBoolean().ShouldBeFalse();

        using var setup = await client.PostAsync("/api/account/two-factor/setup");
        setup.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(setup)).ShouldBe("identity.two_factor_unavailable");
    }

    [Fact]
    public async Task Only_administrators_read_or_change_the_security_settings()
    {
        var (user, _) = await SignInAsync("tfa-not-admin@example.com");

        using var read = await user.GetAsync(SettingsPath);
        read.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var write = await user.PutAsync(SettingsPath, new { twoFactorEnabled = true, twoFactorIssuer = "Mine" });
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var (admin, _) = await SignInAsync("tfa-settings-admin@example.com", "Administrator");

        using var invalid = await admin.PutAsync(SettingsPath, new { twoFactorEnabled = true, twoFactorIssuer = "Acme: CRM" });
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(invalid)).ShouldBe("identity.two_factor_issuer_invalid");

        using var saved = await admin.PutAsync(SettingsPath, new { twoFactorEnabled = true, twoFactorIssuer = "Acme CRM" });
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);

        var settings = await ReadJsonAsync(await admin.GetAsync(SettingsPath));
        settings.GetProperty("twoFactorEnabled").GetBoolean().ShouldBeTrue();
        settings.GetProperty("twoFactorIssuer").GetString().ShouldBe("Acme CRM");
    }

    [Fact]
    public async Task An_enrolled_account_signs_in_with_its_code_or_a_recovery_code()
    {
        await SetPortalTwoFactorAsync(enabled: true);
        var (client, user) = await SignInAsync("tfa-flow@example.com");

        var setup = await ReadJsonAsync(await client.PostAsync("/api/account/two-factor/setup"));
        var key = setup.GetProperty("sharedKey").GetString()!;
        setup.GetProperty("authenticatorUri").GetString()!.ShouldStartWith("otpauth://totp/");

        using (var wrong = await client.PostAsync("/api/account/two-factor/enable", new { code = "000000" }))
        {
            wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ErrorCodeAsync(wrong)).ShouldBe("identity.two_factor_setup_code_invalid");
        }

        var enabled = await ReadJsonAsync(await client.PostAsync("/api/account/two-factor/enable", new { code = Totp(key) }));
        var codes = enabled.GetProperty("codes").EnumerateArray().Select(code => code.GetString()!).ToList();
        codes.Count.ShouldBe(10);

        // Enabling rotated the security stamp; the session that did it stays signed in.
        var status = await ReadJsonAsync(await client.GetAsync("/api/account/two-factor"));
        status.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        status.GetProperty("recoveryCodesLeft").GetInt32().ShouldBe(10);

        // The password alone is not enough any more.
        using var fresh = await ApiClient.CreateAsync(_factory);
        using (var password = await fresh.SignInAsync(user.Email, user.Password))
        {
            password.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await ErrorCodeAsync(password)).ShouldBe("identity.two_factor_required");
        }

        (await IsSignedInAsync(fresh)).ShouldBeFalse();

        using (var badCode = await fresh.PostAsync("/api/auth/login/two-factor", new { code = "000000" }))
        {
            badCode.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await ErrorCodeAsync(badCode)).ShouldBe("identity.two_factor_code_invalid");
        }

        using (var goodCode = await fresh.PostAsync("/api/auth/login/two-factor", new { code = Totp(key) }))
        {
            goodCode.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await ReadJsonAsync(goodCode)).GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
        }

        await fresh.RefreshAntiforgeryTokenAsync();
        await fresh.SignOutAsync();

        // A recovery code works once, typed in lower case too.
        using (var password = await fresh.SignInAsync(user.Email, user.Password))
        {
            password.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var recovery = await fresh.PostAsync(
            "/api/auth/login/two-factor",
            new { code = codes[0].ToLowerInvariant(), useRecoveryCode = true }))
        {
            recovery.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await fresh.RefreshAntiforgeryTokenAsync();
        await fresh.SignOutAsync();

        using (var password = await fresh.SignInAsync(user.Email, user.Password))
        {
            password.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var reused = await fresh.PostAsync("/api/auth/login/two-factor", new { code = codes[0], useRecoveryCode = true }))
        {
            reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task The_second_step_without_a_pending_sign_in_is_refused()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.PostAsync("/api/auth/login/two-factor", new { code = "123456" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(response)).ShouldBe("identity.two_factor_session_expired");
    }

    [Fact]
    public async Task Turning_the_portal_setting_off_stops_asking_for_codes_but_keeps_the_enrolment()
    {
        await SetPortalTwoFactorAsync(enabled: true);
        var (client, user) = await SignInAsync("tfa-suspended@example.com");
        await EnrolAsync(client);

        await SetPortalTwoFactorAsync(enabled: false);

        using var fresh = await ApiClient.CreateAsync(_factory);
        using (var password = await fresh.SignInAsync(user.Email, user.Password))
        {
            password.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var status = await ReadJsonAsync(await fresh.GetAsync("/api/account/two-factor"));
        status.GetProperty("available").GetBoolean().ShouldBeFalse();
        status.GetProperty("enabled").GetBoolean().ShouldBeTrue();

        await SetPortalTwoFactorAsync(enabled: true);

        using var again = await ApiClient.CreateAsync(_factory);
        using var asked = await again.SignInAsync(user.Email, user.Password);
        asked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(asked)).ShouldBe("identity.two_factor_required");
    }

    [Fact]
    public async Task Turning_it_off_needs_the_password()
    {
        await SetPortalTwoFactorAsync(enabled: true);
        var (client, user) = await SignInAsync("tfa-disable@example.com");
        await EnrolAsync(client);

        using (var wrong = await client.PostAsync("/api/account/two-factor/disable", new { password = "not-the-password" }))
        {
            wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ErrorCodeAsync(wrong)).ShouldBe("identity.current_password_incorrect");
        }

        using (var right = await client.PostAsync("/api/account/two-factor/disable", new { password = user.Password }))
        {
            right.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Still signed in after the stamp rotation, and the password is enough again.
        (await ReadJsonAsync(await client.GetAsync("/api/account/two-factor"))).GetProperty("enabled").GetBoolean().ShouldBeFalse();

        using var fresh = await ApiClient.CreateAsync(_factory);
        using var password = await fresh.SignInAsync(user.Email, user.Password);
        password.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_administrator_resets_an_account_that_lost_its_phone()
    {
        await SetPortalTwoFactorAsync(enabled: true);
        var (client, user) = await SignInAsync("tfa-lost-phone@example.com");
        await EnrolAsync(client);

        var (admin, _) = await SignInAsync("tfa-reset-admin@example.com", "Administrator");

        var listed = await ReadJsonAsync(await admin.GetAsync($"/api/admin/users?search={Uri.EscapeDataString(user.Email)}"));
        listed.GetProperty("items")[0].GetProperty("twoFactorEnabled").GetBoolean().ShouldBeTrue();

        using (var reset = await admin.DeleteAsync($"/api/admin/users/{user.Id}/two-factor"))
        {
            reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var after = await ReadJsonAsync(await admin.GetAsync($"/api/admin/users?search={Uri.EscapeDataString(user.Email)}"));
        after.GetProperty("items")[0].GetProperty("twoFactorEnabled").GetBoolean().ShouldBeFalse();

        using var fresh = await ApiClient.CreateAsync(_factory);
        using var password = await fresh.SignInAsync(user.Email, user.Password);
        password.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------

    private async Task<(ApiClient Client, TestUser User)> SignInAsync(string email, string role = "User")
    {
        var user = await _factory.CreateUserAsync(email, Passwords.Valid, role);
        var client = await ApiClient.CreateAsync(_factory);
        using var signIn = await client.SignInAsync(user.Email, user.Password);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (client, user);
    }

    private async Task SetPortalTwoFactorAsync(bool enabled)
    {
        var (admin, _) = await SignInAsync($"tfa-toggle-{Guid.NewGuid():N}@example.com", "Administrator");

        using (admin)
        {
            using var response = await admin.PutAsync(SettingsPath, new { twoFactorEnabled = enabled, twoFactorIssuer = "OpenPortal" });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    /// <summary>Enrols the signed-in account and returns its shared key.</summary>
    private static async Task<string> EnrolAsync(ApiClient client)
    {
        var setup = await ReadJsonAsync(await client.PostAsync("/api/account/two-factor/setup"));
        var key = setup.GetProperty("sharedKey").GetString()!;

        using var enabled = await client.PostAsync("/api/account/two-factor/enable", new { code = Totp(key) });
        enabled.StatusCode.ShouldBe(HttpStatusCode.OK);

        return key;
    }

    private static async Task<bool> IsSignedInAsync(ApiClient client) =>
        (await ReadJsonAsync(await client.GetAsync("/api/auth/session"))).GetProperty("isAuthenticated").GetBoolean();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());

            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        return problem.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    /// <summary>The current RFC 6238 code for a key as the setup endpoint shows it (base32 in groups of four).</summary>
    private static string Totp(string displayedKey)
    {
        var secret = Base32Decode(displayedKey.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant());
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);

        var hash = HMACSHA1.HashData(secret, message);
        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7FFFFFFF) % 1_000_000;

        return binary.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        int buffer = 0, bits = 0;

        foreach (var character in input.TrimEnd('='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character, StringComparison.Ordinal);
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        return [.. output];
    }
}
