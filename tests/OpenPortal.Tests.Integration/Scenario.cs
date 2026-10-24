using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenPortal.Web.Email;

namespace OpenPortal.Tests.Integration;

/// <summary>The steps the newer suites share: accounts, sign-in, JSON, the OpenID Connect code flow.</summary>
internal static class Scenario
{
    public const string Password = "Correct-Horse-9";
    public const string RedirectUri = "https://crm.example.com/signin-oidc";

    public static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.com";

    public static async Task<ApiClient> ClientAsync(WebApplicationFactory<Program> host, bool allowRedirects = true)
    {
        var client = ApiClient.For(host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowRedirects }));
        await client.RefreshAntiforgeryTokenAsync();

        return client;
    }

    public static async Task<ApiClient> SignedInAsync(WebApplicationFactory<Program> host, TestUser user)
    {
        var client = await ClientAsync(host);
        using var response = await client.SignInAsync(user.Email, user.Password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return client;
    }

    public static async Task<(ApiClient Client, TestUser User)> AdminAsync(OpenPortalFactory factory, WebApplicationFactory<Program>? host = null)
    {
        var admin = await factory.CreateUserAsync(UniqueEmail("admin"), Password, "Administrator");

        return (await SignedInAsync(host ?? factory, admin), admin);
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);

        return string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<JsonElement> GetJsonAsync(ApiClient client, string path)
    {
        using var response = await client.GetAsync(path);

        return await JsonAsync(response, HttpStatusCode.OK);
    }

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString();

    public static async Task ExpectErrorAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        JsonDocument.Parse(text).RootElement.GetProperty("errorCode").GetString().ShouldBe(errorCode);
    }

    public static async Task<Guid> CreateGroupAsync(ApiClient admin, params Guid[] memberIds)
    {
        using var created = await admin.PostAsync("/api/admin/groups", new { name = $"Group {Guid.NewGuid():N}"[..24] });
        var groupId = (await JsonAsync(created, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        foreach (var memberId in memberIds)
        {
            using var added = await admin.PutAsync($"/api/admin/groups/{groupId}/members/{memberId}", new { });
            added.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        return groupId;
    }

    public static async Task GrantPageAsync(ApiClient admin, string page, Guid groupId)
    {
        using var response = await admin.PutAsync($"/api/admin/page-permissions/{page}/groups/{groupId}", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Registers an application and returns its id, client id and secret.</summary>
    public static async Task<(Guid Id, string ClientId, string Secret)> CreateApplicationAsync(ApiClient admin, object? extra = null)
    {
        var clientId = $"app-{Guid.NewGuid():N}"[..20];
        var body = new Dictionary<string, object?>
        {
            ["clientId"] = clientId,
            ["displayName"] = $"App {clientId}",
            ["baseUrl"] = "https://crm.example.com",
            ["redirectUris"] = new[] { RedirectUri },
        };

        if (extra is not null)
        {
            foreach (var property in JsonSerializer.SerializeToElement(extra, JsonSerializerOptions.Web).EnumerateObject())
            {
                body[property.Name] = property.Value;
            }
        }

        using var created = await admin.PostAsync("/api/admin/applications", body);
        var json = await JsonAsync(created, HttpStatusCode.Created);

        return (json.GetProperty("application").GetProperty("id").GetGuid(), clientId, json.GetProperty("clientSecret").GetString()!);
    }

    /// <summary>Starts the code flow for a signed-in browser; returns the authorization endpoint's redirect.</summary>
    public static async Task<(Uri Location, string Verifier)> AuthorizeAsync(
        WebApplicationFactory<Program> host,
        TestUser user,
        string clientId,
        string scope,
        string? organization = null)
    {
        var browser = await ClientAsync(host, allowRedirects: false);
        using (var signIn = await browser.SignInAsync(user.Email, user.Password))
        {
            signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        using var authorized = await browser.GetAsync(
            "/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + "&response_type=code"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + $"&scope={Uri.EscapeDataString(scope)}"
            + "&state=s"
            + $"&code_challenge={challenge}"
            + "&code_challenge_method=S256"
            + (organization is null ? string.Empty : $"&organization={Uri.EscapeDataString(organization)}"));

        authorized.StatusCode.ShouldBe(HttpStatusCode.Found, await authorized.Content.ReadAsStringAsync());

        return (authorized.Headers.Location!, verifier);
    }

    /// <summary>Signs the user in to the application through the code flow and returns the token response.</summary>
    public static async Task<JsonElement> SignInToApplicationAsync(
        WebApplicationFactory<Program> host,
        TestUser user,
        string clientId,
        string secret,
        string scope,
        string? organization = null)
    {
        var (location, verifier) = await AuthorizeAsync(host, user, clientId, scope, organization);

        var code = System.Web.HttpUtility.ParseQueryString(location.Query)["code"];
        code.ShouldNotBeNullOrWhiteSpace(location.ToString());

        using var response = await TokenAsync(host, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["code_verifier"] = verifier,
        });

        return await JsonAsync(response, HttpStatusCode.OK);
    }

    public static async Task<HttpResponseMessage> TokenAsync(WebApplicationFactory<Program> host, Dictionary<string, string> form)
    {
        using var backChannel = host.CreateClient();

        return await backChannel.PostAsync("/connect/token", new FormUrlEncodedContent(form));
    }

    public static Task<HttpResponseMessage> RefreshAsync(WebApplicationFactory<Program> host, string clientId, string secret, string refreshToken) =>
        TokenAsync(host, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        });

    public static JsonElement ReadJwtPayload(string jwt) =>
        JsonDocument.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[1])).RootElement.Clone();

    /// <summary>The notification types the user has, newest first.</summary>
    public static async Task<IReadOnlyList<string?>> NotificationTypesAsync(ApiClient client) =>
        (await GetJsonAsync(client, "/api/notifications?pageSize=50"))
            .GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .ToList();
}

/// <summary>Keeps every email the host sends instead of delivering it.</summary>
internal sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _messages = [];

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public IReadOnlyList<EmailMessage> Messages
    {
        get
        {
            lock (_messages)
            {
                return _messages.ToList();
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        lock (_messages)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <summary>The portal link in the last email to <paramref name="to"/> (its path and query).</summary>
    public string LastLinkTo(string to)
    {
        var message = Messages.Last(candidate => string.Equals(candidate.To, to, StringComparison.OrdinalIgnoreCase));
        var match = System.Text.RegularExpressions.Regex.Match(message.TextBody, @"https?://\S+");
        match.Success.ShouldBeTrue(message.TextBody);

        return new Uri(match.Value).PathAndQuery;
    }
}
