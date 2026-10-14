using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Web.Email;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// People creating their own account, as the portal's settings allow: never by default, at once, after confirming
/// their address, or after an administrator's approval. The tests of this class share one settings row, so each
/// sets the mode it needs first.
/// </summary>
public sealed class RegistrationTests : IClassFixture<OpenPortalFactory>, IAsyncDisposable
{
    private readonly OpenPortalFactory _factory;
    private readonly CapturingEmailSender _mail = new();
    private readonly WebApplicationFactory<Program> _host;

    public RegistrationTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmailSender>(_mail)));
    }

    [Fact]
    public async Task Registration_is_closed_until_an_administrator_opens_it()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        await ConfigureAsync(admin, "disabled", confirm: false);

        using var anonymous = await Scenario.ClientAsync(_host);
        var session = await Scenario.GetJsonAsync(anonymous, "/api/auth/session");
        session.GetProperty("registration").GetProperty("enabled").GetBoolean().ShouldBeFalse();

        using var refused = await anonymous.PostAsync("/api/auth/register", Body(Scenario.UniqueEmail("early")));
        await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Forbidden, "identity.registration_closed");
    }

    [Fact]
    public async Task Open_registration_without_confirmation_signs_the_new_account_in()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        await ConfigureAsync(admin, "open", confirm: false);

        using var anonymous = await Scenario.ClientAsync(_host);
        (await Scenario.GetJsonAsync(anonymous, "/api/auth/session"))
            .GetProperty("registration").GetProperty("enabled").GetBoolean().ShouldBeTrue();

        var email = Scenario.UniqueEmail("newcomer");
        using (var registered = await anonymous.PostAsync("/api/auth/register", Body(email)))
        {
            (await Scenario.JsonAsync(registered, HttpStatusCode.OK)).GetProperty("outcome").GetString().ShouldBe("signed_in");
        }

        var session = await Scenario.GetJsonAsync(anonymous, "/api/auth/session");
        session.GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
        session.GetProperty("user").GetProperty("email").GetString().ShouldBe(email);

        // Without confirmation the address cannot be hidden: a second registration says it is taken.
        using var other = await Scenario.ClientAsync(_host);
        using var duplicate = await other.PostAsync("/api/auth/register", Body(email));
        await Scenario.ExpectErrorAsync(duplicate, HttpStatusCode.Conflict, "identity.duplicate_email");

        (await Scenario.NotificationTypesAsync(admin)).ShouldContain(IdentityNotificationTypes.UserRegistered);

        var users = await Scenario.GetJsonAsync(admin, $"/api/admin/users?search={Uri.EscapeDataString(email)}");
        users.GetProperty("items").EnumerateArray().Single().GetProperty("origin").GetString().ShouldBe("registered");
    }

    [Fact]
    public async Task An_account_waiting_for_approval_signs_in_once_approved()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        await ConfigureAsync(admin, "approval", confirm: false, domains: ["example.com"]);

        using var anonymous = await Scenario.ClientAsync(_host);

        using (var wrongDomain = await anonymous.PostAsync("/api/auth/register", Body($"someone-{Guid.NewGuid():N}@elsewhere.org")))
        {
            await Scenario.ExpectErrorAsync(wrongDomain, HttpStatusCode.BadRequest, "identity.registration_domain_not_allowed");
        }

        var email = Scenario.UniqueEmail("waiting");
        using (var registered = await anonymous.PostAsync("/api/auth/register", Body(email)))
        {
            (await Scenario.JsonAsync(registered, HttpStatusCode.OK)).GetProperty("outcome").GetString().ShouldBe("approval_pending");
        }

        // The reason is told only to whoever knows the password.
        using (var wrong = await anonymous.SignInAsync(email, "Wrong-Horse-99"))
        {
            await Scenario.ExpectErrorAsync(wrong, HttpStatusCode.Unauthorized, "identity.invalid_credentials");
        }

        using (var waiting = await anonymous.SignInAsync(email, Scenario.Password))
        {
            await Scenario.ExpectErrorAsync(waiting, HttpStatusCode.Unauthorized, "identity.account_pending_approval");
        }

        (await Scenario.NotificationTypesAsync(admin)).ShouldContain(IdentityNotificationTypes.RegistrationPending);

        var pending = await Scenario.GetJsonAsync(admin, $"/api/admin/users?status=pending&search={Uri.EscapeDataString(email)}");
        var user = pending.GetProperty("items").EnumerateArray().Single();
        user.GetProperty("pendingApproval").GetBoolean().ShouldBeTrue();
        var userId = user.GetProperty("id").GetGuid();

        using (var approved = await admin.PostAsync($"/api/admin/users/{userId}/approve"))
        {
            (await Scenario.JsonAsync(approved, HttpStatusCode.OK)).GetProperty("pendingApproval").GetBoolean().ShouldBeFalse();
        }

        using (var again = await admin.PostAsync($"/api/admin/users/{userId}/approve"))
        {
            await Scenario.ExpectErrorAsync(again, HttpStatusCode.Conflict, "identity.registration_not_pending");
        }

        _mail.Messages.ShouldContain(message => message.To == email);

        using var signedIn = await anonymous.SignInAsync(email, Scenario.Password);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Scenario.NotificationTypesAsync(anonymous)).ShouldContain(IdentityNotificationTypes.RegistrationApproved);
    }

    [Fact]
    public async Task A_confirmed_address_is_required_before_signing_in_and_the_endpoint_does_not_reveal_accounts()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        await ConfigureAsync(admin, "open", confirm: true);

        using var anonymous = await Scenario.ClientAsync(_host);
        var email = Scenario.UniqueEmail("confirm");

        using (var registered = await anonymous.PostAsync("/api/auth/register", Body(email)))
        {
            (await Scenario.JsonAsync(registered, HttpStatusCode.OK)).GetProperty("outcome").GetString().ShouldBe("confirmation_sent");
        }

        using (var unconfirmed = await anonymous.SignInAsync(email, Scenario.Password))
        {
            await Scenario.ExpectErrorAsync(unconfirmed, HttpStatusCode.Unauthorized, "identity.email_not_confirmed");
        }

        // The same address again gets the same answer and no account is created twice.
        var sentBefore = _mail.Messages.Count(message => message.To == email);
        using (var twice = await anonymous.PostAsync("/api/auth/register", Body(email)))
        {
            (await Scenario.JsonAsync(twice, HttpStatusCode.OK)).GetProperty("outcome").GetString().ShouldBe("confirmation_sent");
        }

        _mail.Messages.Count(message => message.To == email).ShouldBe(sentBefore);

        using (var resend = await anonymous.PostAsync("/api/auth/confirm-email/resend", new { email }))
        {
            resend.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var nobody = await anonymous.PostAsync("/api/auth/confirm-email/resend", new { email = Scenario.UniqueEmail("nobody") }))
        {
            nobody.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var link = _mail.LastLinkTo(email);
        link.ShouldStartWith("/confirm-email?");
        var query = System.Web.HttpUtility.ParseQueryString(link.Split('?', 2)[1]);

        using (var forged = await anonymous.PostAsync("/api/auth/confirm-email", new { userId = query["user"], token = "forged" }))
        {
            await Scenario.ExpectErrorAsync(forged, HttpStatusCode.BadRequest, "identity.email_confirmation_invalid");
        }

        using (var confirmed = await anonymous.PostAsync("/api/auth/confirm-email", new { userId = query["user"], token = query["token"] }))
        {
            (await Scenario.JsonAsync(confirmed, HttpStatusCode.OK)).GetProperty("outcome").GetString().ShouldBe("confirmed");
        }

        using var signedIn = await anonymous.SignInAsync(email, Scenario.Password);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Registration_settings_are_for_administrators_and_need_email_to_ask_for_confirmation()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory);

        // The plain host has no email delivery configured.
        using (var refused = await admin.PutAsync("/api/admin/settings/registration", Settings("open", confirm: true)))
        {
            await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Conflict, "identity.email_delivery_unavailable");
        }

        var settings = await Scenario.GetJsonAsync(admin, "/api/admin/settings/registration");
        settings.GetProperty("emailDeliveryAvailable").GetBoolean().ShouldBeFalse();

        using (var badDomain = await admin.PutAsync("/api/admin/settings/registration", Settings("open", confirm: false, domains: ["not a domain"])))
        {
            await Scenario.ExpectErrorAsync(badDomain, HttpStatusCode.BadRequest, "identity.registration_domain_invalid");
        }

        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("plain"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(_factory, user);
        using var forbidden = await client.GetAsync("/api/admin/settings/registration");
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    private static object Body(string email) => new { email, password = Scenario.Password, firstName = "New", lastName = "Person" };

    private static object Settings(string mode, bool confirm, string[]? domains = null) => new
    {
        mode,
        allowedDomains = domains ?? [],
        requireEmailConfirmation = confirm,
        invitationLifetimeDays = 7,
    };

    private static async Task ConfigureAsync(ApiClient admin, string mode, bool confirm, string[]? domains = null)
    {
        using var response = await admin.PutAsync("/api/admin/settings/registration", Settings(mode, confirm, domains));
        var settings = await Scenario.JsonAsync(response, HttpStatusCode.OK);
        settings.GetProperty("mode").GetString().ShouldBe(mode);
        settings.GetProperty("emailDeliveryAvailable").GetBoolean().ShouldBeTrue();
    }
}
