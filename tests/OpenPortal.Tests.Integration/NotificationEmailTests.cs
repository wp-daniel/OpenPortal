using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.Web.Email;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Notifications by email: security notices reach everyone who did not turn email off, the rest only those who asked
/// for every notification, and the daily digest gathers what is still unread.
/// </summary>
public sealed class NotificationEmailTests : IClassFixture<OpenPortalFactory>, IAsyncDisposable
{
    private const string PasswordResetTitle = "Password changed by an administrator";
    private const string JoinedTitle = "Added to an organization";

    private readonly OpenPortalFactory _factory;
    private readonly CapturingEmailSender _mail = new();
    private readonly WebApplicationFactory<Program> _host;

    public NotificationEmailTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmailSender>(_mail)));
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task By_default_only_security_notices_are_emailed()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("defaults"), Scenario.Password);

        using (var preferences = await Scenario.SignedInAsync(_host, user))
        {
            var current = await Scenario.GetJsonAsync(preferences, "/api/notifications/preferences");
            current.GetProperty("email").GetString().ShouldBe("security");
            current.GetProperty("emailAvailable").GetBoolean().ShouldBeTrue();
        }

        await JoinOrganizationAsync(admin, user.Id);
        _mail.Messages.ShouldNotContain(message => message.To == user.Email && message.Subject == JoinedTitle);

        await ResetPasswordAsync(admin, user.Id);
        var reset = _mail.Messages.Last(message => message.To == user.Email && message.Subject == PasswordResetTitle);
        reset.TextBody.ShouldContain("/account?tab=security");
    }

    [Fact]
    public async Task Instant_emails_every_notice_and_off_emails_none()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var eager = await _factory.CreateUserAsync(Scenario.UniqueEmail("eager"), Scenario.Password);
        var quiet = await _factory.CreateUserAsync(Scenario.UniqueEmail("quiet"), Scenario.Password);

        await ChooseAsync(eager, "instant");
        await ChooseAsync(quiet, "off");

        await JoinOrganizationAsync(admin, eager.Id);
        await JoinOrganizationAsync(admin, quiet.Id);
        await ResetPasswordAsync(admin, quiet.Id);

        _mail.Messages.ShouldContain(message => message.To == eager.Email && message.Subject == JoinedTitle);
        _mail.Messages.ShouldNotContain(message => message.To == quiet.Email && (message.Subject == JoinedTitle || message.Subject == PasswordResetTitle));

        using var client = await Scenario.SignedInAsync(_host, eager);
        using var invalid = await client.PutAsync("/api/notifications/preferences", new { email = "hourly" });
        await Scenario.ExpectErrorAsync(invalid, HttpStatusCode.BadRequest, "notifications.email_delivery_invalid");
    }

    [Fact]
    public async Task The_daily_digest_gathers_the_unread_notifications_once()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("digest"), Scenario.Password);
        await ChooseAsync(user, "daily");

        await JoinOrganizationAsync(admin, user.Id);
        await JoinOrganizationAsync(admin, user.Id);
        _mail.Messages.ShouldNotContain(message => message.To == user.Email && message.Subject == JoinedTitle);

        // A day goes by.
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var preference = await db.NotificationPreferences.SingleAsync(row => row.UserId == user.Id);
            db.Entry(preference).Property(row => row.LastDigestAtUtc).CurrentValue = DateTimeOffset.UtcNow.AddHours(-25);
            await db.SaveChangesAsync();
        }

        (await SendDigestsAsync()).ShouldBeGreaterThanOrEqualTo(1);

        string.Join(" | ", _mail.Messages.Where(message => message.To == user.Email).Select(message => message.Subject)).ShouldContain("notifications waiting");
        var digest = _mail.Messages.Single(message => message.To == user.Email && message.Subject.Contains("notification", StringComparison.OrdinalIgnoreCase));
        digest.TextBody.ShouldContain(JoinedTitle);

        // Nothing is due again until the next day.
        await SendDigestsAsync();
        _mail.Messages.Count(message => message.To == user.Email && message.Subject == digest.Subject).ShouldBe(1);
    }

    /// <summary>Runs the digest sweep now (the service is internal to the module, so it is reached by reflection).</summary>
    private Task<int> SendDigestsAsync()
    {
        var service = _host.Services.GetServices<IHostedService>().Single(candidate => candidate.GetType().Name == "NotificationDigestService");

        return (Task<int>)service.GetType().GetMethod("SendDueAsync")!.Invoke(service, [CancellationToken.None])!;
    }

    private async Task ChooseAsync(TestUser user, string email)
    {
        using var client = await Scenario.SignedInAsync(_host, user);
        using var saved = await client.PutAsync("/api/notifications/preferences", new { email });
        (await Scenario.JsonAsync(saved, HttpStatusCode.OK)).GetProperty("email").GetString().ShouldBe(email);
    }

    private static async Task JoinOrganizationAsync(ApiClient admin, Guid userId)
    {
        using var created = await admin.PostAsync("/api/admin/organizations", new { name = "Org", slug = $"org-{Guid.NewGuid():N}"[..16] });
        var id = (await Scenario.JsonAsync(created, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        using var added = await admin.PutAsync($"/api/admin/organizations/{id}/members/{userId}", new { role = "member" });
        added.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task ResetPasswordAsync(ApiClient admin, Guid userId)
    {
        using var reset = await admin.PostAsync($"/api/admin/users/{userId}/reset-password", new { newPassword = "Brand-New-Horse-7" });
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent, await reset.Content.ReadAsStringAsync());
    }
}
