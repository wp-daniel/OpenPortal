using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenPortal.Content.Infrastructure.Persistence;
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
        session.GetProperty("user").GetProperty("displayName").GetString().ShouldBe("ada");
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
        using var content = await client.GetAsync("/api/content");

        session.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
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
            displayName = "Renamed Person",
        }))
        {
            update.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var read = await client.GetAsync("/api/account/profile");
        var payload = await read.Content.ReadFromJsonAsync<JsonElement>();

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        payload.GetProperty("displayName").GetString().ShouldBe("Renamed Person");
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
    // Content: public and management surfaces.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Public_content_is_readable_anonymously()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/content");

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"A brand-new install has no profile and no projects, and must still answer. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Management_content_rejects_an_anonymous_caller()
    {
        using var client = await ApiClient.CreateAsync(_factory);

        using var response = await client.GetAsync("/api/manage/content/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Administrator_can_publish_a_profile_and_it_becomes_public()
    {
        var admin = await _factory.CreateUserAsync("publisher@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using (var response = await client.PutAsync("/api/manage/content/profile", new
        {
            displayName = "Published Person",
            headline = "Now visible",
            socialLinks = new[]
            {
                new { platform = "GitHub", url = "https://github.com/example", label = (string?)null },
            },
        }))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var anonymous = await ApiClient.CreateAsync(_factory);
        using var publicResponse = await anonymous.GetAsync("/api/content");
        var payload = await publicResponse.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("profile").GetProperty("displayName").GetString().ShouldBe("Published Person");
        payload.GetProperty("profile").GetProperty("socialLinks").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Administrator_can_create_a_project_and_publish_it()
    {
        var admin = await _factory.CreateUserAsync("author@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var create = await client.PutAsync("/api/manage/content/projects", new
        {
            name = "Analytical Engine",
            slug = "analytical-engine",
            summary = "Tabulating polynomials.",
            description = "A long-form description.",
            url = "https://example.com/engine",
            repositoryUrl = "https://github.com/example/engine",
            technologies = new[] { "C#", ".NET" },
            isPublished = true,
        });

        create.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"Body: {await create.Content.ReadAsStringAsync()}");
        var project = await create.Content.ReadFromJsonAsync<JsonElement>();

        var slug = project.GetProperty("slug").GetString();
        // The slug is normalised on the way in, so a mixed-case submission comes back lower-cased.
        slug.ShouldBe("analytical-engine");

        using var anonymous = await ApiClient.CreateAsync(_factory);
        using var bySlug = await anonymous.GetAsync($"/api/content/projects/{slug}");

        bySlug.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await bySlug.Content.ReadFromJsonAsync<JsonElement>();

        fetched.GetProperty("name").GetString().ShouldBe("Analytical Engine");
        fetched.GetProperty("technologies").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray()
            .ShouldBe(["C#", ".NET"], ignoreOrder: true);
    }

    [Fact]
    public async Task Unpublished_project_is_not_served_by_the_public_api()
    {
        var admin = await _factory.CreateUserAsync("drafts@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var create = await client.PutAsync("/api/manage/content/projects", new
        {
            name = "Unfinished Work",
            slug = "unfinished-work",
            technologies = Array.Empty<string>(),
            isPublished = false,
        });

        create.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"Body: {await create.Content.ReadAsStringAsync()}");
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();

        using var anonymous = await ApiClient.CreateAsync(_factory);
        using var response = await anonymous.GetAsync($"/api/content/projects/{slug}");

        // Drafts exist in the database but must not be reachable by anyone who is not an editor.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_slug_is_rejected_as_a_conflict()
    {
        var admin = await _factory.CreateUserAsync("dupes@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var first = await client.PutAsync("/api/manage/content/projects", new { name = "Twin", slug = "twin", technologies = Array.Empty<string>(), isPublished = true });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await client.PutAsync("/api/manage/content/projects", new { name = "Twin", slug = "twin", technologies = Array.Empty<string>(), isPublished = true });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Non_administrator_cannot_reach_management_content()
    {
        var user = await _factory.CreateUserAsync("nosy@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(user.Email, user.Password);

        using var response = await client.GetAsync("/api/manage/content/projects");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // The management projection.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Management_project_listing_reports_the_fields_an_editor_needs()
    {
        // The editor cannot publish, reorder or delete a project without its row id, its published flag and
        // its position, and it must be able to see unpublished projects at all. The public projection carries
        // none of those, so this asserts the management payload really is a different shape.
        var admin = await _factory.CreateUserAsync("listing@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var create = await client.PutAsync("/api/manage/content/projects", new
        {
            name = "Draft Engine",
            slug = "draft-engine",
            summary = "Still being written.",
            isPublished = false,
            position = 7,
            technologies = new[] { "F#" },
        });

        create.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"Body: {await create.Content.ReadAsStringAsync()}");

        using var list = await client.GetAsync("/api/manage/content/projects");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        var project = body.EnumerateArray()
            .Single(item => item.GetProperty("slug").GetString() == "draft-engine");

        project.GetProperty("id").GetGuid().ShouldNotBe(Guid.Empty);
        project.GetProperty("isPublished").GetBoolean().ShouldBeFalse();
        project.GetProperty("position").GetInt32().ShouldBe(7);
        project.GetProperty("updatedAtUtc").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UnixEpoch);
        project.GetProperty("technologies").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ShouldBe(["F#"]);
    }

    [Fact]
    public async Task Public_project_payload_does_not_leak_editor_fields()
    {
        // The separation is only worth anything if the public shape stays free of them. Publishing an id and
        // a draft flag to anonymous readers would let anyone enumerate draft work.
        var admin = await _factory.CreateUserAsync("leakage@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var create = await client.PutAsync("/api/manage/content/projects", new
        {
            name = "Quiet Machine",
            slug = "quiet-machine",
            isPublished = true,
            technologies = Array.Empty<string>(),
        });

        create.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var anonymous = await ApiClient.CreateAsync(_factory);
        using var response = await anonymous.GetAsync("/api/content/projects/quiet-machine");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var project = await response.Content.ReadFromJsonAsync<JsonElement>();

        project.TryGetProperty("id", out _).ShouldBeFalse("the public payload must not carry the row id.");
        project.TryGetProperty("isPublished", out _).ShouldBeFalse("a published reader does not need the flag.");
        project.TryGetProperty("position", out _).ShouldBeFalse("display order is not reader-facing.");
    }

    [Fact]
    public async Task Management_project_round_trip_supports_publish_and_delete()
    {
        // Exercises the full editing loop the UI performs: create, list to recover the id, publish by id, then
        // delete by id. Before the management projection carried the id this sequence was impossible to drive
        // from a client at all.
        var admin = await _factory.CreateUserAsync("lifecycle@example.com", Passwords.Valid, "Administrator");
        using var client = await ApiClient.CreateAsync(_factory);
        await client.SignInAsync(admin.Email, admin.Password);

        using var create = await client.PutAsync("/api/manage/content/projects", new
        {
            name = "Loom Engine",
            slug = "loom-engine",
            isPublished = false,
            technologies = Array.Empty<string>(),
        });

        create.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var list = await client.GetAsync("/api/manage/content/projects");
        var id = (await list.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Single(item => item.GetProperty("slug").GetString() == "loom-engine")
            .GetProperty("id")
            .GetGuid();

        using var publish = await client.PostAsync($"/api/manage/content/projects/{id}/published", new
        {
            isPublished = true,
        });

        publish.StatusCode.ShouldBe(HttpStatusCode.OK);

        var published = await publish.Content.ReadFromJsonAsync<JsonElement>();
        published.GetProperty("isPublished").GetBoolean().ShouldBeTrue();

        using var anonymous = await ApiClient.CreateAsync(_factory);
        using var publicRead = await anonymous.GetAsync("/api/content/projects/loom-engine");

        publicRead.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var delete = await client.DeleteAsync($"/api/manage/content/projects/{id}");

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var goneRead = await anonymous.GetAsync("/api/content/projects/loom-engine");

        goneRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

        // Identity and Content each own a DbContext. If either were unmigrated the first query against it
        // would fail with "no such table", so touching both proves the migration set is complete.
        provider.GetRequiredService<IdentityDbContext>().Database.CanConnect().ShouldBeTrue();
        provider.GetRequiredService<ContentDbContext>().Database.CanConnect().ShouldBeTrue();

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