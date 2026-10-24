using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Web.Webhooks;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Webhooks: an application hears about its users (granted, revoked, changed) the moment it happens, in signed posts
/// it can verify with its secret (Standard Webhooks).
/// </summary>
public sealed class WebhookTests : IClassFixture<OpenPortalFactory>
{
    private const string HookUrl = "https://crm.example.com/hooks/openportal";

    private readonly OpenPortalFactory _factory;
    private readonly HookCatcher _hooks = new();
    private readonly WebApplicationFactory<Program> _host;

    public WebhookTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient(WebhookHandler.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _hooks)));
    }

    [Fact]
    public async Task Granting_a_user_posts_a_signed_access_granted_event()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var (applicationId, clientId, secret) = await ApplicationWithWebhookAsync(admin, ["access.granted", "access.revoked"]);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("hooked"), Scenario.Password);

        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { }))
        {
            granted.IsSuccessStatusCode.ShouldBeTrue(await granted.Content.ReadAsStringAsync());
        }

        var hook = _hooks.Received.Last(received => received.Body.Contains(user.Id.ToString(), StringComparison.Ordinal));
        hook.Headers["webhook-signature"].ShouldBe("v1," + WebhookHandler.Sign(secret, hook.Headers["webhook-id"], hook.Headers["webhook-timestamp"], hook.Body));

        var body = JsonDocument.Parse(hook.Body).RootElement;
        body.GetProperty("type").GetString().ShouldBe("access.granted");
        body.GetProperty("application").GetString().ShouldBe(clientId);
        body.GetProperty("data").GetProperty("subject").GetProperty("id").GetString().ShouldBe(user.Id.ToString());

        using (var revoked = await admin.DeleteAsync($"/api/admin/access/applications/{applicationId}/users/{user.Id}"))
        {
            revoked.IsSuccessStatusCode.ShouldBeTrue(await revoked.Content.ReadAsStringAsync());
        }

        _hooks.Received.ShouldContain(received => received.Body.Contains("\"access.revoked\"", StringComparison.Ordinal)
            && received.Body.Contains(user.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Only_subscribed_events_about_the_applications_users_are_posted()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var (applicationId, _, _) = await ApplicationWithWebhookAsync(admin, ["user.updated"]);

        var member = await _factory.CreateUserAsync(Scenario.UniqueEmail("member"), Scenario.Password);
        var stranger = await _factory.CreateUserAsync(Scenario.UniqueEmail("stranger"), Scenario.Password);

        using (var granted = await admin.PutAsync($"/api/admin/access/applications/{applicationId}/users/{member.Id}", new { }))
        {
            granted.IsSuccessStatusCode.ShouldBeTrue();
        }

        // Not subscribed to access.granted: nothing for the grant itself.
        _hooks.Received.ShouldNotContain(received => received.Body.Contains(member.Id.ToString(), StringComparison.Ordinal));

        await UpdateOwnProfileAsync(member, "Renamed");
        await UpdateOwnProfileAsync(stranger, "Other");

        _hooks.Received.ShouldContain(received => received.Body.Contains("\"user.updated\"", StringComparison.Ordinal)
            && received.Body.Contains(member.Id.ToString(), StringComparison.Ordinal));

        // Someone who cannot open the application is none of its business.
        _hooks.Received.ShouldNotContain(received => received.Body.Contains(stranger.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_administrator_can_send_a_test_event()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var (applicationId, clientId, _) = await ApplicationWithWebhookAsync(admin, []);

        using var tested = await admin.PostAsync($"/api/admin/applications/{applicationId}/webhook/test");

        tested.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _hooks.Received.ShouldContain(received => received.Body.Contains("\"webhook.test\"", StringComparison.Ordinal)
            && received.Body.Contains(clientId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_webhook_url_must_be_https()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);

        using var created = await admin.PostAsync("/api/admin/applications", new
        {
            clientId = $"hook-{Guid.NewGuid():N}"[..20],
            displayName = "Bad hook",
            baseUrl = "https://crm.example.com",
            redirectUris = new[] { Scenario.RedirectUri },
            webhookUrl = "http://crm.example.com/hooks",
        });

        await Scenario.ExpectErrorAsync(created, HttpStatusCode.BadRequest, "access.webhook_url_invalid");
    }

    private async Task UpdateOwnProfileAsync(TestUser user, string firstName)
    {
        using var client = await Scenario.SignedInAsync(_host, user);
        using var updated = await client.PutAsync("/api/account/profile", new { firstName, lastName = "Test" });
        updated.IsSuccessStatusCode.ShouldBeTrue(await updated.Content.ReadAsStringAsync());
    }

    private static async Task<(Guid Id, string ClientId, string Secret)> ApplicationWithWebhookAsync(ApiClient admin, string[] events)
    {
        var (applicationId, clientId, _) = await Scenario.CreateApplicationAsync(admin, new { webhookUrl = HookUrl, webhookEvents = events });

        using var generated = await admin.PostAsync($"/api/admin/applications/{applicationId}/webhook/secret");
        var secret = (await Scenario.JsonAsync(generated, HttpStatusCode.OK)).GetProperty("secret").GetString()!;
        secret.ShouldStartWith("whsec_");

        return (applicationId, clientId, secret);
    }

    /// <summary>What the portal posted to applications, with its headers; answers 200.</summary>
    private sealed class HookCatcher : HttpMessageHandler
    {
        private readonly List<(Dictionary<string, string> Headers, string Body)> _received = [];

        public IReadOnlyList<(Dictionary<string, string> Headers, string Body)> Received
        {
            get
            {
                lock (_received)
                {
                    return _received.ToList();
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(',', header.Value), StringComparer.OrdinalIgnoreCase);
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            lock (_received)
            {
                _received.Add((headers, body));
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
