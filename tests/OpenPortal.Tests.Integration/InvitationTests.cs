using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Email;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Invitations: an administrator invites an address, the link (emailed, and shown once) lets the person create
/// their account with the memberships promised; the link works once and can be withdrawn or reissued.
/// </summary>
public sealed class InvitationTests : IClassFixture<OpenPortalFactory>, IAsyncDisposable
{
    private readonly OpenPortalFactory _factory;
    private readonly CapturingEmailSender _mail = new();
    private readonly WebApplicationFactory<Program> _host;

    public InvitationTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmailSender>(_mail)));
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task An_accepted_invitation_creates_a_confirmed_account_with_its_memberships()
    {
        var (admin, adminUser) = await Scenario.AdminAsync(_factory, _host);
        var groupId = await Scenario.CreateGroupAsync(admin);

        using var organization = await admin.PostAsync("/api/admin/organizations", new { name = "Invited Org", slug = $"inv-{Guid.NewGuid():N}"[..16] });
        var organizationId = (await Scenario.JsonAsync(organization, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var email = Scenario.UniqueEmail("invitee");
        var issued = await InviteAsync(admin, new
        {
            email,
            firstName = "Grace",
            message = "Welcome aboard",
            groupIds = new[] { groupId },
            organizationIds = new[] { organizationId },
        });

        issued.GetProperty("emailSent").GetBoolean().ShouldBeTrue();
        issued.GetProperty("invitation").GetProperty("status").GetString().ShouldBe("pending");
        var token = TokenOf(issued.GetProperty("link").GetString()!);
        TokenOf(_mail.LastLinkTo(email)).ShouldBe(token);

        using (var duplicate = await admin.PostAsync("/api/admin/invitations", new { email }))
        {
            await Scenario.ExpectErrorAsync(duplicate, HttpStatusCode.Conflict, "identity.invitation_already_pending");
        }

        using var invitee = await Scenario.ClientAsync(_host);

        using (var preview = await invitee.PostAsync("/api/auth/invitations/preview", new { token }))
        {
            var body = await Scenario.JsonAsync(preview, HttpStatusCode.OK);
            body.GetProperty("email").GetString().ShouldBe(email);
            body.GetProperty("firstName").GetString().ShouldBe("Grace");
            body.GetProperty("message").GetString().ShouldBe("Welcome aboard");
        }

        using (var weak = await invitee.PostAsync("/api/auth/invitations/accept", new { token, password = "short", firstName = "Grace", lastName = "Hopper" }))
        {
            weak.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var accepted = await invitee.PostAsync("/api/auth/invitations/accept", new { token, password = Scenario.Password, firstName = "Grace", lastName = "Hopper" }))
        {
            accepted.StatusCode.ShouldBe(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        }

        await invitee.RefreshAntiforgeryTokenAsync();
        var session = await Scenario.GetJsonAsync(invitee, "/api/auth/session");
        session.GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
        var userId = session.GetProperty("user").GetProperty("id").GetGuid();
        session.GetProperty("user").GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();

        // The promised memberships are in place.
        (await Scenario.GetJsonAsync(admin, $"/api/admin/groups/{groupId}"))
            .GetProperty("members").EnumerateArray().Select(member => member.GetProperty("id").GetGuid()).ShouldContain(userId);
        (await Scenario.GetJsonAsync(invitee, "/api/account/organizations"))
            .EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(organizationId);
        (await Scenario.NotificationTypesAsync(invitee)).ShouldContain(OpenPortal.Access.Application.Notifications.AccessNotificationTypes.OrganizationJoined);

        (await Scenario.NotificationTypesAsync(admin)).ShouldContain(IdentityNotificationTypes.InvitationAccepted);

        var listed = await Scenario.GetJsonAsync(admin, $"/api/admin/invitations?search={Uri.EscapeDataString(email)}");
        var invitation = listed.GetProperty("items").EnumerateArray().Single();
        invitation.GetProperty("status").GetString().ShouldBe("accepted");
        invitation.GetProperty("acceptedUserId").GetGuid().ShouldBe(userId);
        invitation.GetProperty("invitedById").GetGuid().ShouldBe(adminUser.Id);

        // The link worked once.
        using var reused = await Scenario.ClientAsync(_host);
        using var again = await reused.PostAsync("/api/auth/invitations/preview", new { token });
        await Scenario.ExpectErrorAsync(again, HttpStatusCode.NotFound, "identity.invitation_invalid");
    }

    [Fact]
    public async Task Sending_again_replaces_the_link_and_withdrawing_closes_it()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var email = Scenario.UniqueEmail("later");

        var first = await InviteAsync(admin, new { email });
        var firstToken = TokenOf(first.GetProperty("link").GetString()!);
        var id = first.GetProperty("invitation").GetProperty("id").GetGuid();

        string secondToken;
        using (var resent = await admin.PostAsync($"/api/admin/invitations/{id}/resend"))
        {
            secondToken = TokenOf((await Scenario.JsonAsync(resent, HttpStatusCode.OK)).GetProperty("link").GetString()!);
        }

        secondToken.ShouldNotBe(firstToken);

        using var anonymous = await Scenario.ClientAsync(_host);

        using (var old = await anonymous.PostAsync("/api/auth/invitations/preview", new { token = firstToken }))
        {
            await Scenario.ExpectErrorAsync(old, HttpStatusCode.NotFound, "identity.invitation_invalid");
        }

        using (var current = await anonymous.PostAsync("/api/auth/invitations/preview", new { token = secondToken }))
        {
            current.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var revoked = await admin.DeleteAsync($"/api/admin/invitations/{id}"))
        {
            revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var closed = await anonymous.PostAsync("/api/auth/invitations/accept", new { token = secondToken, password = Scenario.Password, firstName = "A", lastName = "B" }))
        {
            await Scenario.ExpectErrorAsync(closed, HttpStatusCode.NotFound, "identity.invitation_invalid");
        }

        using var twice = await admin.DeleteAsync($"/api/admin/invitations/{id}");
        await Scenario.ExpectErrorAsync(twice, HttpStatusCode.Conflict, "identity.invitation_closed");
    }

    [Fact]
    public async Task An_existing_account_cannot_be_invited_and_groups_need_the_groups_page()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var existing = await _factory.CreateUserAsync(Scenario.UniqueEmail("exists"), Scenario.Password);

        using (var taken = await admin.PostAsync("/api/admin/invitations", new { email = existing.Email }))
        {
            await Scenario.ExpectErrorAsync(taken, HttpStatusCode.Conflict, "identity.duplicate_email");
        }

        // A holder of the users page alone may invite, but not into groups.
        var hr = await _factory.CreateUserAsync(Scenario.UniqueEmail("hr"), Scenario.Password);
        await Scenario.GrantPageAsync(admin, PortalPages.Users, await Scenario.CreateGroupAsync(admin, hr.Id));
        var groupId = await Scenario.CreateGroupAsync(admin);

        using var client = await Scenario.SignedInAsync(_host, hr);

        using (var withGroup = await client.PostAsync("/api/admin/invitations", new { email = Scenario.UniqueEmail("x"), groupIds = new[] { groupId } }))
        {
            await Scenario.ExpectErrorAsync(withGroup, HttpStatusCode.Forbidden, "access.administration_forbidden");
        }

        using var plain = await client.PostAsync("/api/admin/invitations", new { email = Scenario.UniqueEmail("y") });
        plain.StatusCode.ShouldBe(HttpStatusCode.Created, await plain.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> InviteAsync(ApiClient admin, object body)
    {
        using var response = await admin.PostAsync("/api/admin/invitations", body);

        return await Scenario.JsonAsync(response, HttpStatusCode.Created);
    }

    private static string TokenOf(string link) =>
        System.Web.HttpUtility.ParseQueryString(new Uri(new Uri("http://localhost"), link).Query)["token"]!;
}
