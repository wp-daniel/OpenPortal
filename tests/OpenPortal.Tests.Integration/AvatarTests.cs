using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenPortal.Tests.Integration;

/// <summary>Profile pictures: upload, serve, replace and remove, and who may do each.</summary>
public sealed class AvatarTests : IClassFixture<OpenPortalFactory>
{
    /// <summary>A real 1x1 PNG: the server identifies images by signature, so a stub header is enough.</summary>
    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    private readonly OpenPortalFactory _factory;

    public AvatarTests(OpenPortalFactory factory) => _factory = factory;

    private static MultipartFormDataContent Upload(byte[] content, string declaredType = "image/png")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(declaredType);

        return new MultipartFormDataContent { { file, "file", "avatar.png" } };
    }

    private async Task<(ApiClient Client, Guid UserId)> SignInAsync(string email, string role = "User")
    {
        var user = await _factory.CreateUserAsync(email, Passwords.Valid, role);
        var client = await ApiClient.CreateAsync(_factory);
        using var signIn = await client.SignInAsync(user.Email, user.Password);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (client, user.Id);
    }

    [Fact]
    public async Task Own_avatar_round_trips_and_is_announced_in_the_session()
    {
        var (client, userId) = await SignInAsync("avatar-self@example.com");
        using var _ = client;

        using var upload = await client.Http.PutAsync("/api/account/avatar", Upload(Png));
        upload.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var image = await client.GetAsync($"/api/users/{userId}/avatar");
        image.StatusCode.ShouldBe(HttpStatusCode.OK);
        image.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        image.Headers.TryGetValues("X-Content-Type-Options", out var sniff).ShouldBeTrue();
        sniff.ShouldContain("nosniff");
        (await image.Content.ReadAsByteArrayAsync()).ShouldBe(Png);

        using var sessionResponse = await client.GetAsync("/api/auth/session");
        var session = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        session.GetProperty("user").GetProperty("avatarUpdatedAtUtc").ValueKind.ShouldBe(JsonValueKind.String);

        // The ETag lets a revalidation end without resending the image.
        using var conditional = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{userId}/avatar");
        conditional.Headers.IfNoneMatch.Add(image.Headers.ETag!);
        using var notModified = await client.Http.SendAsync(conditional);
        notModified.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Removing_the_avatar_makes_it_404_and_clears_the_stamp()
    {
        var (client, userId) = await SignInAsync("avatar-remove@example.com");
        using var _ = client;

        using var upload = await client.Http.PutAsync("/api/account/avatar", Upload(Png));
        using var removed = await client.DeleteAsync("/api/account/avatar");
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var image = await client.GetAsync($"/api/users/{userId}/avatar");
        image.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        image.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await image.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().ShouldBe("identity.avatar_not_found");

        using var profileResponse = await client.GetAsync("/api/account/profile");
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>();
        profile.GetProperty("avatarUpdatedAtUtc").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Bytes_that_are_not_an_image_are_rejected_whatever_type_they_claim()
    {
        var (client, _) = await SignInAsync("avatar-fake@example.com");
        using var __ = client;

        using var response = await client.Http.PutAsync(
            "/api/account/avatar",
            Upload("<script>alert(1)</script>"u8.ToArray(), declaredType: "image/png"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().ShouldBe("identity.avatar_unsupported_type");
    }

    [Fact]
    public async Task An_oversized_image_is_a_400_problem()
    {
        var (client, _) = await SignInAsync("avatar-big@example.com");
        using var __ = client;

        var oversized = new byte[(256 * 1024) + 1];
        Png.CopyTo(oversized, 0);

        using var response = await client.Http.PutAsync("/api/account/avatar", Upload(oversized));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().ShouldBe("identity.avatar_too_large");
    }

    [Fact]
    public async Task A_missing_file_is_a_400_problem()
    {
        var (client, _) = await SignInAsync("avatar-none@example.com");
        using var __ = client;

        using var form = new MultipartFormDataContent { { new StringContent("x"), "other" } };
        using var response = await client.Http.PutAsync("/api/account/avatar", form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().ShouldBe("identity.avatar_required");
    }

    [Fact]
    public async Task Anonymous_callers_can_neither_read_nor_upload()
    {
        var target = await _factory.CreateUserAsync("avatar-anon-target@example.com", Passwords.Valid, "User");
        using var client = await ApiClient.CreateAsync(_factory);

        using var read = await client.GetAsync($"/api/users/{target.Id}/avatar");
        read.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var upload = await client.Http.PutAsync("/api/account/avatar", Upload(Png));
        upload.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_administrators_change_someone_elses_avatar()
    {
        var target = await _factory.CreateUserAsync("avatar-target@example.com", Passwords.Valid, "User");

        var (user, _) = await SignInAsync("avatar-peer@example.com");
        using (user)
        {
            using var denied = await user.Http.PutAsync($"/api/admin/users/{target.Id}/avatar", Upload(Png));
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        var (admin, _) = await SignInAsync("avatar-admin@example.com", "Administrator");
        using (admin)
        {
            using var allowed = await admin.Http.PutAsync($"/api/admin/users/{target.Id}/avatar", Upload(Png));
            allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // Any signed-in user can see it; that is how it shows up next to names across the portal.
            using var image = await admin.GetAsync($"/api/users/{target.Id}/avatar");
            image.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var removed = await admin.DeleteAsync($"/api/admin/users/{target.Id}/avatar");
            removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            using var missing = await admin.Http.PutAsync($"/api/admin/users/{Guid.NewGuid()}/avatar", Upload(Png));
            missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }
}
