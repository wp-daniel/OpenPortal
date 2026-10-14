using System.Net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Web.Resources;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The notification center: what the modules announce reaches the people concerned, each sees only their own, and
/// whoever caused a notice is not told about it.
/// </summary>
public sealed class NotificationTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task Joining_a_group_notifies_the_member_who_reads_and_deletes_it()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var member = await factory.CreateUserAsync(Scenario.UniqueEmail("member"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(factory, member);

        (await Scenario.GetJsonAsync(client, "/api/notifications/count")).GetProperty("unreadCount").GetInt32().ShouldBe(0);

        await Scenario.CreateGroupAsync(admin, member.Id);

        var count = await Scenario.GetJsonAsync(client, "/api/notifications/count");
        count.GetProperty("unreadCount").GetInt32().ShouldBe(1);
        count.GetProperty("latestAtUtc").ValueKind.ShouldNotBe(System.Text.Json.JsonValueKind.Null);

        var page = await Scenario.GetJsonAsync(client, "/api/notifications");
        var notification = page.GetProperty("items").EnumerateArray().Single();
        notification.GetProperty("type").GetString().ShouldBe(AccessNotificationTypes.GroupJoined);
        notification.GetProperty("parameters").GetProperty("group").GetString().ShouldStartWith("Group ");
        notification.GetProperty("link").GetString().ShouldBe("/");
        page.GetProperty("unreadCount").GetInt32().ShouldBe(1);

        var id = notification.GetProperty("id").GetGuid();

        using (var read = await client.PutAsync($"/api/notifications/{id}/read", new { }))
        {
            (await Scenario.JsonAsync(read, HttpStatusCode.OK)).GetProperty("readAtUtc").ValueKind.ShouldNotBe(System.Text.Json.JsonValueKind.Null);
        }

        (await Scenario.GetJsonAsync(client, "/api/notifications?unreadOnly=true")).GetProperty("totalCount").GetInt32().ShouldBe(0);

        using (var unread = await client.DeleteAsync($"/api/notifications/{id}/read"))
        {
            unread.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var all = await client.PostAsync("/api/notifications/read-all"))
        {
            all.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var clear = await client.DeleteAsync("/api/notifications/read"))
        {
            clear.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await Scenario.GetJsonAsync(client, "/api/notifications")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Nobody_reads_or_changes_someone_elses_notifications()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var member = await factory.CreateUserAsync(Scenario.UniqueEmail("owner"), Scenario.Password);
        var other = await factory.CreateUserAsync(Scenario.UniqueEmail("other"), Scenario.Password);
        await Scenario.CreateGroupAsync(admin, member.Id);

        using var owner = await Scenario.SignedInAsync(factory, member);
        var id = (await Scenario.GetJsonAsync(owner, "/api/notifications"))
            .GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();

        using var stranger = await Scenario.SignedInAsync(factory, other);
        (await Scenario.GetJsonAsync(stranger, "/api/notifications")).GetProperty("totalCount").GetInt32().ShouldBe(0);

        using (var read = await stranger.PutAsync($"/api/notifications/{id}/read", new { }))
        {
            await Scenario.ExpectErrorAsync(read, HttpStatusCode.NotFound, "notifications.not_found");
        }

        using (var delete = await stranger.DeleteAsync($"/api/notifications/{id}"))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var anonymous = await ApiClient.CreateAsync(factory);
        using var count = await anonymous.GetAsync("/api/notifications/count");
        count.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Whoever_causes_a_notice_is_not_told_about_it()
    {
        var (admin, adminUser) = await Scenario.AdminAsync(factory);

        // An administrator putting themselves in a group gets nothing for it.
        await Scenario.CreateGroupAsync(admin, adminUser.Id);

        (await Scenario.NotificationTypesAsync(admin)).ShouldNotContain(AccessNotificationTypes.GroupJoined);
    }

    [Fact]
    public async Task Resetting_a_password_tells_the_owner_and_deleting_the_account_forgets_its_notifications()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var user = await factory.CreateUserAsync(Scenario.UniqueEmail("reset"), Scenario.Password);

        using (var reset = await admin.PostAsync($"/api/admin/users/{user.Id}/reset-password", new { newPassword = "Another-Horse-9" }))
        {
            reset.StatusCode.ShouldBe(HttpStatusCode.NoContent, await reset.Content.ReadAsStringAsync());
        }

        using (var client = await Scenario.SignedInAsync(factory, user with { Password = "Another-Horse-9" }))
        {
            (await Scenario.NotificationTypesAsync(client)).ShouldContain(IdentityNotificationTypes.PasswordReset);
        }

        using (var deleted = await admin.DeleteAsync($"/api/admin/users/{user.Id}"))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenPortal.Notifications.Infrastructure.Persistence.NotificationsDbContext>();
        db.Notifications.Count(notification => notification.RecipientId == user.Id).ShouldBe(0);
    }

    [Fact]
    public void Every_notification_type_has_a_title_and_a_body_in_the_resources()
    {
        using var scope = factory.Services.CreateScope();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<Messages>>();

        var types = new[] { typeof(AccessNotificationTypes), typeof(IdentityNotificationTypes) }
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        types.ShouldNotBeEmpty();
        types.SelectMany(type => new[] { $"notification.{type}.title", $"notification.{type}.body" })
            .Where(key => localizer[key].ResourceNotFound)
            .ShouldBeEmpty();
    }
}
