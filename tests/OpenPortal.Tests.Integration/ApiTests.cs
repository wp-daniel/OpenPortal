using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.Web;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// One host for every test in the class: booting is the expensive part, and the database is per-host so
/// tests within a class share it without sharing credentials.
/// </summary>
public sealed class ApiTests : IClassFixture<OpenPortalFactory>
{
    private readonly OpenPortalFactory _factory;

    public ApiTests(OpenPortalFactory factory) => _factory = factory;

    // -----------------------------------------------------------------------
    // Session and sign-in.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Session_is_anonymous_for_a_caller_with_no_cookie()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/auth/session");
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        session.GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
        session.GetProperty("user").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Session_publishes_the_password_policy_the_server_actually_enforces()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/auth/session");
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Read the same options the user store validates against, so this fails if the published copy drifts in
        // either direction: a stale constant here would have a client form rejecting passwords the server
        // accepts, which is the failure nobody can debug from a 400.
        using var scope = _factory.Services.CreateScope();
        var configured = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value
            .Password;

        var published = session.GetProperty("passwordPolicy");

        published.GetProperty("requiredLength").GetInt32().ShouldBe(configured.RequiredLength);
        published.GetProperty("requiredUniqueChars").GetInt32().ShouldBe(configured.RequiredUniqueChars);
        published.GetProperty("requireLowercase").GetBoolean().ShouldBe(configured.RequireLowercase);
        published.GetProperty("requireUppercase").GetBoolean().ShouldBe(configured.RequireUppercase);
        published.GetProperty("requireDigit").GetBoolean().ShouldBe(configured.RequireDigit);
        published.GetProperty("requireNonAlphanumeric").GetBoolean().ShouldBe(configured.RequireNonAlphanumeric);
    }

    [Fact]
    public async Task Password_policy_is_published_to_anonymous_callers_too()
    {
        // The sign-in and change-password forms need the policy before anyone is signed in, so an anonymous
        // session that omitted it would leave those screens validating against a guess.
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/auth/session");
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();

        session.GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
        session.GetProperty("passwordPolicy").ValueKind.ShouldBe(JsonValueKind.Object);
        session.GetProperty("passwordPolicy").GetProperty("requiredLength").GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Antiforgery_endpoint_reports_the_header_name_and_writes_a_readable_cookie()
    {
        // A client that has not yet run the handshake, so the response really does carry the cookie. A client
        // that already holds one would be served from it and set no cookie at all.
        using var http = _factory.CreateClient();

        using var response = await http.GetAsync("/api/auth/antiforgery");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("headerName").GetString().ShouldBe(AntiforgeryDefaults.HeaderName);
        payload.GetProperty("requestToken").GetString().ShouldNotBeNullOrWhiteSpace();

        var cookie = response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{AntiforgeryDefaults.CookieName}=", StringComparison.Ordinal));

        cookie.ShouldNotContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
    }

    [Fact]
    public async Task The_request_token_is_not_the_cookie_value_and_only_the_request_token_is_accepted()
    {
        // The regression this pins: the SPA once read the readable XSRF-TOKEN cookie and sent its value in the
        // header, which fails with "the cookie token and the request token were swapped". Integration tests
        // could not catch it, because the test helper performs the same handshake the browser was skipping.
        using var http = _factory.CreateClient();

        using var handshake = await http.GetAsync("/api/auth/antiforgery");
        var requestToken = (await handshake.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("requestToken")
            .GetString();

        var cookieValue = handshake.Headers
            .GetValues("Set-Cookie")
            .Select(value => value[(value.IndexOf('=') + 1)..value.IndexOf(';')])
            .Single(value => !string.IsNullOrWhiteSpace(value));

        cookieValue.ShouldNotBeNullOrWhiteSpace();

        // They are genuinely different values, so echoing the cookie cannot be a valid shortcut.
        cookieValue.ShouldNotBe(requestToken);

        using var client = ApiClient.For(http);

        // The request token works. Deliberately not ApiClient.CreateAsync: the handshake was already performed
        // above and its two values are what is under test, so the helper must not quietly fetch another.
        client.Http.DefaultRequestHeaders.Add(AntiforgeryDefaults.HeaderName, requestToken);

        using var accepted = await client.PostAsync(
            "/api/auth/login",
            new { email = "nobody@example.com", password = "irrelevant" });
        accepted.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The cookie value in the same header does not, and it is rejected as a token problem rather than as
        // bad credentials - which is the detail that makes this diagnosable instead of mysterious.
        client.Http.DefaultRequestHeaders.Remove(AntiforgeryDefaults.HeaderName);
        client.Http.DefaultRequestHeaders.Add(AntiforgeryDefaults.HeaderName, cookieValue);

        using var rejected = await client.PostAsync(
            "/api/auth/login",
            new { email = "nobody@example.com", password = "irrelevant" });

        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().ShouldBe("antiforgery.invalid_token");
    }

    [Fact]
    public async Task Sign_in_with_valid_credentials_returns_the_session()
    {
        var user = await _factory.CreateUserAsync("ada@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.PostAsync(
            "/api/auth/login",
            new { email = user.Email, password = user.Password });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();

        session.GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
        session.GetProperty("user").GetProperty("email").GetString().ShouldBe("ada@example.com");
        session.GetProperty("user").GetProperty("displayName").GetString().ShouldBe("ada Test");
        session.GetProperty("user").GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()).ShouldContain("User");

        // Carried here too, so a client that only ever reads the post-login response still has the real policy
        // for the change-password form instead of falling back to "unknown".
        session.GetProperty("passwordPolicy").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public async Task Sign_in_never_echoes_credential_material_back()
    {
        var user = await _factory.CreateUserAsync("grace@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.PostAsync(
            "/api/auth/login",
            new { email = user.Email, password = user.Password });

        var raw = await response.Content.ReadAsStringAsync();

        raw.ShouldNotContain(user.Password);
        raw.ShouldNotContain("passwordHash");
        raw.ShouldNotContain("securityStamp");
        raw.ShouldNotContain("concurrencyStamp");
    }

    [Fact]
    public async Task Sign_in_with_a_wrong_password_is_rejected_without_revealing_which_part_was_wrong()
    {
        await _factory.CreateUserAsync("known@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);

        using var wrongPassword = await client.PostAsync(
            "/api/auth/login",
            new { email = "known@example.com", password = "Wr0ng-Password!" });

        using var unknownAccount = await client.PostAsync(
            "/api/auth/login",
            new { email = "nobody@example.com", password = Passwords.Valid });

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownAccount.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The two failures must be indistinguishable, otherwise the endpoint becomes an account-existence
        // oracle that anyone can enumerate. The trace id is excluded because it is deliberately unique per
        // request: that is what makes it useful for correlating a log entry, and it is the one field allowed
        // to differ.
        (await wrongPassword.Content.ReadAsStringAsync()).WithoutTraceId()
            .ShouldBe((await unknownAccount.Content.ReadAsStringAsync()).WithoutTraceId());
    }

    [Fact]
    public async Task Sign_out_clears_the_session()
    {
        var user = await _factory.CreateUserAsync("logout@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);

        await client.SignInAsync(user.Email, user.Password);
        await client.SignOutAsync();

        // The client keeps its cookie container, so this is the real check: the response must not still
        // claim to be authenticated.
        using var session = await client.GetAsync("/api/auth/session");
        var payload = await session.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Antiforgery.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Unsafe_request_without_a_token_is_rejected()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.PostWithoutTokenAsync("/api/auth/login", new
        {
            email = "anyone@example.com",
            password = Passwords.Valid,
        });

        // Sign-in is itself an unsafe request that a third-party page could trigger to sign a victim into an
        // account the attacker controls. It must be protected like every other unsafe request.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unsafe_request_with_a_forged_token_is_rejected()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        client.Http.DefaultRequestHeaders.Remove(AntiforgeryDefaults.HeaderName);
        client.Http.DefaultRequestHeaders.Add(AntiforgeryDefaults.HeaderName, "not-a-real-token");

        using var response = await client.PostAsync("/api/auth/login", new
        {
            email = "anyone@example.com",
            password = Passwords.Valid,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Safe_requests_do_not_require_a_token()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        client.Http.DefaultRequestHeaders.Remove(AntiforgeryDefaults.HeaderName);

        using var session = await client.GetAsync("/api/auth/session");
        using var languages = await client.GetAsync("/api/i18n/languages");

        session.StatusCode.ShouldBe(HttpStatusCode.OK);
        languages.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // Authorisation.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Admin_endpoints_reject_an_anonymous_caller_with_401_not_403()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/admin/users");

        // 403 would tell the caller the endpoint exists and that only the role is missing.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Admin_endpoints_reject_a_signed_in_non_administrator_with_403()
    {
        var user = await _factory.CreateUserAsync("plain@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(user.Email, user.Password);

        using var response = await client.GetAsync("/api/admin/users");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_create_a_user_and_gets_201_with_its_location()
    {
        // Guards CreatedAtAction(nameof(GetAsync)): with MVC's default async-suffix trimming the route lookup
        // fails after the account was already created, and the client sees an error for a successful create.
        var admin = await _factory.CreateUserAsync("creator@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var response = await client.PostAsync(
            "/api/admin/users",
            new
            {
                email = "created@example.com",
                password = Passwords.Valid,
                firstName = "Grace",
                lastName = "Hopper",
                phoneNumber = "+1 202 555 0143",
                company = "Navy",
                jobTitle = "Rear Admiral",
                city = "Arlington",
                isAdministrator = false,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.Location.ShouldNotBeNull();

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("displayName").GetString().ShouldBe("Grace Hopper");
        created.GetProperty("phoneNumber").GetString().ShouldBe("+1 202 555 0143");
        created.GetProperty("company").GetString().ShouldBe("Navy");
    }

    [Fact]
    public async Task Creating_a_user_without_a_last_name_or_with_a_bad_phone_is_a_400()
    {
        var admin = await _factory.CreateUserAsync("strict@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var noLastName = await client.PostAsync(
            "/api/admin/users",
            new { email = "a@example.com", password = Passwords.Valid, firstName = "Ada" });
        using var badPhone = await client.PostAsync(
            "/api/admin/users",
            new
            {
                email = "b@example.com",
                password = Passwords.Valid,
                firstName = "Ada",
                lastName = "Lovelace",
                phoneNumber = "call me maybe",
                isAdministrator = false,
            });

        noLastName.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        badPhone.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        badPhone.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Admin_can_filter_users_by_administrator_flag_status_and_search()
    {
        var admin = await _factory.CreateUserAsync("filter-admin@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var created = await client.PostAsync(
            "/api/admin/users",
            new
            {
                email = "filter-target@example.com",
                password = Passwords.Valid,
                firstName = "Zed",
                lastName = "Zimmermann",
                phoneNumber = "+39 333 123 4567",
                company = "Acme Spa",
                isAdministrator = false,
            });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        async Task<string[]> EmailsAsync(string query)
        {
            using var response = await client.GetAsync("/api/admin/users" + query);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

            return payload.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("email").GetString()!)
                .ToArray();
        }

        (await EmailsAsync("?search=acme")).ShouldBe(["filter-target@example.com"]);
        (await EmailsAsync("?search=333%20123")).ShouldBe(["filter-target@example.com"]);
        (await EmailsAsync("?administrator=true")).ShouldContain("filter-admin@example.com");
        (await EmailsAsync("?administrator=true")).ShouldNotContain("filter-target@example.com");
        (await EmailsAsync("?administrator=false")).ShouldContain("filter-target@example.com");
        (await EmailsAsync("?administrator=false")).ShouldNotContain("filter-admin@example.com");
        (await EmailsAsync("?status=active")).ShouldContain("filter-target@example.com");
        (await EmailsAsync("?status=locked")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Admin_can_list_users_and_the_listing_hides_credential_fields()
    {
        var admin = await _factory.CreateUserAsync("boss@example.com", Passwords.Valid, "Administrator");
        await _factory.CreateUserAsync("member@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var response = await client.GetAsync("/api/admin/users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();

        raw.ShouldContain("member@example.com");
        raw.ShouldNotContain("passwordHash");
        raw.ShouldNotContain("securityStamp");
        raw.ShouldNotContain(Passwords.Valid);
    }

    // -----------------------------------------------------------------------
    // Account.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Account_profile_can_be_read_and_updated_by_its_owner()
    {
        var user = await _factory.CreateUserAsync("profile@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(user.Email, user.Password);

        using (var update = await client.PutAsync("/api/account/profile", new
        {
            firstName = "Renamed",
            lastName = "Person",
            phoneNumber = "+39 06 1234 5678",
            city = "Roma",
        }))
        {
            update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        }

        using var read = await client.GetAsync("/api/account/profile");
        var payload = await read.Content.ReadFromJsonAsync<JsonElement>();

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        payload.GetProperty("displayName").GetString().ShouldBe("Renamed Person");
        payload.GetProperty("firstName").GetString().ShouldBe("Renamed");
        payload.GetProperty("city").GetString().ShouldBe("Roma");
    }

    [Fact]
    public async Task Account_endpoints_reject_an_anonymous_caller()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/account/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Password_change_requires_the_current_password()
    {
        var user = await _factory.CreateUserAsync("rotate@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(user.Email, user.Password);

        using var response = await client.PostAsync("/api/account/change-password", new
        {
            currentPassword = "Wr0ng-Current!",
            newPassword = Passwords.Rotated,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The old password must still work, which is the part that actually matters: a rejected change that
        // nonetheless changed the password would be worse than the failure itself.
        using var retry = await client.PostAsync("/api/auth/login", new
        {
            email = user.Email,
            password = user.Password,
        });

        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Password_change_succeeds_and_invalidates_the_old_password()
    {
        var user = await _factory.CreateUserAsync("rotate2@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(user.Email, user.Password);

        using (var response = await client.PostAsync("/api/account/change-password", new
        {
            currentPassword = user.Password,
            newPassword = Passwords.Rotated,
        }))
        {
            // 204: the change has no representation the client needs back.
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var withOld = await client.PostAsync("/api/auth/login", new
        {
            email = user.Email,
            password = user.Password,
        });
        withOld.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var withNew = await client.PostAsync("/api/auth/login", new
        {
            email = user.Email,
            password = Passwords.Rotated,
        });
        withNew.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // Cross-cutting behaviour.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Unknown_api_route_answers_problem_details_not_html()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Unknown_api_route_does_not_fall_back_to_the_spa_shell()
    {
        // With a built client in wwwroot the catch-all route can serve index.html for anything unmatched.
        // Answering an /api typo with a 200 HTML page would make a client parse a document as JSON and report
        // a parse error instead of a 404, so the API prefix is excluded from the fallback explicitly.
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/does-not-exist");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldNotContain("<!DOCTYPE html");
        body.ShouldNotContain("<div id=\"root\">");
    }

    [Fact]
    public async Task Unknown_page_route_still_serves_the_spa_shell()
    {
        // The other half of the same rule: a client-side route must still resolve to index.html, otherwise a
        // deep link or a hard refresh would 404.
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/projects/some-unpublished-slug");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Only acceptable when the client has not been built into wwwroot, which is the case for a
            // test run with SkipClientBuild. Asserted here so the reason is visible rather than mysterious.
            response.Content.Headers.ContentType?.MediaType.ShouldNotBe("application/problem+json");

            return;
        }

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync()).ShouldContain("id=\"root\"");
    }

    [Fact]
    public async Task Malformed_json_is_reported_as_a_validation_problem()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.Http.PostAsync("/api/auth/login", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(
            "application/problem+json",
            $"Body was: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Both_module_databases_are_created_by_the_migrations()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        // Every module owns a DbContext. If one were unmigrated the first query against it would fail with
        // "no such table", so touching each proves the migration set is complete.
        provider.GetRequiredService<IdentityDbContext>().Database.CanConnect().ShouldBeTrue();
        provider.GetRequiredService<AccessDbContext>().Database.CanConnect().ShouldBeTrue();

        (await provider.GetRequiredService<IdentityDbContext>().Users.CountAsync())
            .ShouldBeGreaterThan(0);
    }
}

/// <summary>Passwords that satisfy the configured policy, kept in one place so rotation tests stay honest.</summary>
internal static class Passwords
{
    public const string Valid = "Str0ng-Passw0rd!";
    public const string Rotated = "An0ther-Passw0rd!";
}

/// <summary>Helpers for comparing two error bodies that legitimately differ in one field.</summary>
internal static class StringExtensions
{
    private static readonly System.Text.RegularExpressions.Regex TraceId =
        new("\"traceId\":\"[^\"]+\"", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(2));

    /// <summary>Replaces the per-request trace id with a placeholder so two bodies can be compared.</summary>
    public static string WithoutTraceId(this string body) => TraceId.Replace(body, "\"traceId\":\"<trace>\"");
}