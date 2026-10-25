using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Web.Email;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Changing the address an account signs in with: by its owner through a link sent to the new address (nothing changes
/// until it is opened), or by an administrator for an owner whose old mailbox is gone.
/// </summary>
public sealed class EmailChangeTests : IClassFixture<OpenPortalFactory>
{
    private readonly OpenPortalFactory _factory;
    private readonly CapturingEmailSender _mail = new();
    private readonly WebApplicationFactory<Program> _host;

    public EmailChangeTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmailSender>(_mail)));
    }

    [Fact]
    public async Task The_link_sent_to_the_new_address_makes_it_the_sign_in_address()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("mover"), Scenario.Password);
        var newEmail = Scenario.UniqueEmail("moved");
        using var browser = await Scenario.SignedInAsync(_host, user);
        using var elsewhere = await Scenario.SignedInAsync(_host, user);

        using (var requested = await browser.PostAsync("/api/account/email", new { newEmail, currentPassword = user.Password }))
        {
            var profile = await Scenario.JsonAsync(requested, HttpStatusCode.OK);
            profile.GetProperty("pendingEmail").GetString().ShouldBe(newEmail);
            profile.GetProperty("email").GetString().ShouldBe(user.Email, "nothing changes before the link is opened");
        }

        var link = _mail.LastLinkTo(newEmail);
        link.ShouldStartWith("/confirm-email-change?");
        var query = HttpUtility.ParseQueryString(new Uri("https://portal" + link).Query);
        var body = new { userId = query["user"], token = query["token"] };

        using (var confirmed = await browser.PostAsync("/api/auth/email-change/confirm", body))
        {
            var result = await Scenario.JsonAsync(confirmed, HttpStatusCode.OK);
            result.GetProperty("email").GetString().ShouldBe(newEmail);
            result.GetProperty("signedIn").GetBoolean().ShouldBeTrue();
        }

        // The session that opened the link goes on under the new address; the other one has ended.
        await browser.RefreshAntiforgeryTokenAsync();
        var session = await Scenario.GetJsonAsync(browser, "/api/auth/session");
        session.GetProperty("user").GetProperty("email").GetString().ShouldBe(newEmail);
        session.GetProperty("user").GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
        (await Scenario.GetJsonAsync(elsewhere, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();

        // The previous address is told.
        _mail.Messages.Last(message => message.To == user.Email).TextBody.ShouldContain(newEmail);

        // The new address signs in, the old one no longer does.
        using var fresh = await Scenario.ClientAsync(_host);
        using (var old = await fresh.SignInAsync(user.Email, user.Password))
        {
            old.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var signedIn = await fresh.SignInAsync(newEmail, user.Password))
        {
            signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // The link worked once.
        using var again = await browser.PostAsync("/api/auth/email-change/confirm", body);
        await Scenario.ExpectErrorAsync(again, HttpStatusCode.BadRequest, "identity.email_change_invalid");
    }

    [Fact]
    public async Task A_wrong_password_asks_for_nothing()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("guessed"), Scenario.Password);
        var newEmail = Scenario.UniqueEmail("attacker");
        using var browser = await Scenario.SignedInAsync(_host, user);

        using var refused = await browser.PostAsync("/api/account/email", new { newEmail, currentPassword = "not-the-password" });

        await Scenario.ExpectErrorAsync(refused, HttpStatusCode.BadRequest, "identity.current_password_incorrect");
        _mail.Messages.ShouldNotContain(message => message.To == newEmail);
        (await Scenario.GetJsonAsync(browser, "/api/account/profile")).GetProperty("pendingEmail").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task An_address_another_account_uses_gets_the_same_answer_and_its_owner_is_told()
    {
        var owner = await _factory.CreateUserAsync(Scenario.UniqueEmail("owner"), Scenario.Password);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("prober"), Scenario.Password);
        using var browser = await Scenario.SignedInAsync(_host, user);

        using var requested = await browser.PostAsync("/api/account/email", new { newEmail = owner.Email, currentPassword = user.Password });

        // Same answer as for a free address, so the endpoint cannot be used to find accounts...
        (await Scenario.JsonAsync(requested, HttpStatusCode.OK)).GetProperty("pendingEmail").GetString().ShouldBe(owner.Email);

        // ...but no link goes out: the owner hears that someone tried, and nothing they receive changes anything.
        var told = _mail.Messages.Last(message => message.To == owner.Email);
        told.TextBody.ShouldNotContain("/confirm-email-change");
    }

    [Fact]
    public async Task A_newer_request_or_a_cancellation_makes_the_link_useless()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("indecisive"), Scenario.Password);
        var first = Scenario.UniqueEmail("first");
        var second = Scenario.UniqueEmail("second");
        using var browser = await Scenario.SignedInAsync(_host, user);

        (await browser.PostAsync("/api/account/email", new { newEmail = first, currentPassword = user.Password })).Dispose();
        var firstLink = HttpUtility.ParseQueryString(new Uri("https://portal" + _mail.LastLinkTo(first)).Query);

        (await browser.PostAsync("/api/account/email", new { newEmail = second, currentPassword = user.Password })).Dispose();
        var secondLink = HttpUtility.ParseQueryString(new Uri("https://portal" + _mail.LastLinkTo(second)).Query);

        using (var stale = await browser.PostAsync("/api/auth/email-change/confirm", new { userId = firstLink["user"], token = firstLink["token"] }))
        {
            await Scenario.ExpectErrorAsync(stale, HttpStatusCode.BadRequest, "identity.email_change_invalid");
        }

        using (var cancelled = await browser.DeleteAsync("/api/account/email"))
        {
            (await Scenario.JsonAsync(cancelled, HttpStatusCode.OK)).GetProperty("pendingEmail").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        }

        using var afterCancel = await browser.PostAsync("/api/auth/email-change/confirm", new { userId = secondLink["user"], token = secondLink["token"] });
        await Scenario.ExpectErrorAsync(afterCancel, HttpStatusCode.BadRequest, "identity.email_change_invalid");
    }

    [Fact]
    public async Task Without_email_delivery_the_owner_cannot_change_the_address()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("offline"), Scenario.Password);
        using var browser = await Scenario.SignedInAsync(_factory, user);

        using var refused = await browser.PostAsync("/api/account/email", new { newEmail = Scenario.UniqueEmail("nowhere"), currentPassword = user.Password });

        await Scenario.ExpectErrorAsync(refused, HttpStatusCode.Conflict, "identity.email_change_unavailable");
    }

    [Fact]
    public async Task An_administrator_changes_the_address_and_both_mailboxes_are_told()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("lost-mailbox"), Scenario.Password);
        var newEmail = Scenario.UniqueEmail("new-mailbox");
        using var userBrowser = await Scenario.SignedInAsync(_host, user);

        using (var changed = await admin.PutAsync($"/api/admin/users/{user.Id}/email", new { email = newEmail }))
        {
            var summary = await Scenario.JsonAsync(changed, HttpStatusCode.OK);
            summary.GetProperty("email").GetString().ShouldBe(newEmail);
        }

        _mail.Messages.ShouldContain(message => message.To == user.Email);
        _mail.Messages.ShouldContain(message => message.To == newEmail);

        // The account's sessions ended; it signs in with the new address and finds the notice.
        (await Scenario.GetJsonAsync(userBrowser, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();
        using var signedIn = await Scenario.SignedInAsync(_host, user with { Email = newEmail });
        (await Scenario.NotificationTypesAsync(signedIn)).ShouldContain("account.email_changed");

        // An address that belongs to someone else is refused.
        var other = await _factory.CreateUserAsync(Scenario.UniqueEmail("taken"), Scenario.Password);
        using var duplicate = await admin.PutAsync($"/api/admin/users/{user.Id}/email", new { email = other.Email });
        await Scenario.ExpectErrorAsync(duplicate, HttpStatusCode.Conflict, "identity.duplicate_email");
    }

    [Fact]
    public async Task Only_holders_of_the_users_page_change_someone_elses_address()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("target"), Scenario.Password);
        var other = await _factory.CreateUserAsync(Scenario.UniqueEmail("nosy"), Scenario.Password);
        using var browser = await Scenario.SignedInAsync(_host, other);

        using var refused = await browser.PutAsync($"/api/admin/users/{user.Id}/email", new { email = Scenario.UniqueEmail("hijack") });

        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
