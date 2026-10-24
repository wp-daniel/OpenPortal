using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Web.Email;

namespace OpenPortal.Tests.Integration;

/// <summary>"Forgot your password?": a link by email, used once, that signs the account out everywhere.</summary>
public sealed class PasswordRecoveryTests : IClassFixture<OpenPortalFactory>
{
    private const string NewPassword = "Brand-New-Secret-42";

    private readonly OpenPortalFactory _factory;
    private readonly CapturingEmailSender _mail = new();
    private readonly WebApplicationFactory<Program> _host;

    public PasswordRecoveryTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmailSender>(_mail)));
    }

    [Fact]
    public async Task The_sign_in_page_offers_recovery_when_email_works()
    {
        using var anonymous = await Scenario.ClientAsync(_host);

        var session = await Scenario.GetJsonAsync(anonymous, "/api/auth/session");

        session.GetProperty("registration").GetProperty("passwordReset").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task A_reset_link_sets_a_new_password_once_and_ends_the_other_sessions()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("forgetful"), Scenario.Password);
        using var elsewhere = await Scenario.SignedInAsync(_host, user);

        using var anonymous = await Scenario.ClientAsync(_host);
        using (var requested = await anonymous.PostAsync("/api/auth/password/forgot", new { email = user.Email }))
        {
            requested.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var link = _mail.LastLinkTo(user.Email);
        link.ShouldStartWith("/reset-password?");
        var query = HttpUtility.ParseQueryString(new Uri("https://portal" + link).Query);
        var body = new { userId = query["user"], token = query["token"], newPassword = NewPassword };

        using (var reset = await anonymous.PostAsync("/api/auth/password/reset", body))
        {
            reset.StatusCode.ShouldBe(HttpStatusCode.NoContent, await reset.Content.ReadAsStringAsync());
        }

        // The owner is told by email.
        _mail.Messages.Last(message => message.To == user.Email).Subject.ShouldContain("changed");

        // The session that was open elsewhere has ended.
        (await Scenario.GetJsonAsync(elsewhere, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeFalse();

        // The new password works, the old one does not.
        using var fresh = await Scenario.ClientAsync(_host);
        using (var old = await fresh.SignInAsync(user.Email, Scenario.Password))
        {
            old.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var signedIn = await fresh.SignInAsync(user.Email, NewPassword))
        {
            signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // The link worked once.
        using var again = await anonymous.PostAsync("/api/auth/password/reset", body with { newPassword = "Another-Secret-77" });
        await Scenario.ExpectErrorAsync(again, HttpStatusCode.BadRequest, "identity.password_reset_invalid");
    }

    [Fact]
    public async Task Asking_for_an_unknown_address_looks_the_same_and_sends_nothing()
    {
        using var anonymous = await Scenario.ClientAsync(_host);
        var unknown = Scenario.UniqueEmail("nobody");

        using var requested = await anonymous.PostAsync("/api/auth/password/forgot", new { email = unknown });

        requested.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _mail.Messages.ShouldNotContain(message => message.To == unknown);
    }

    [Fact]
    public async Task A_forged_token_is_refused()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("target"), Scenario.Password);
        using var anonymous = await Scenario.ClientAsync(_host);

        using var reset = await anonymous.PostAsync(
            "/api/auth/password/reset",
            new { userId = user.Id, token = "CfDJ8-not-a-real-token", newPassword = NewPassword });

        await Scenario.ExpectErrorAsync(reset, HttpStatusCode.BadRequest, "identity.password_reset_invalid");

        using var signedIn = await anonymous.SignInAsync(user.Email, Scenario.Password);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_weak_new_password_is_refused_and_the_link_still_works()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("weak"), Scenario.Password);
        using var anonymous = await Scenario.ClientAsync(_host);
        using (await anonymous.PostAsync("/api/auth/password/forgot", new { email = user.Email }))
        {
        }

        var query = HttpUtility.ParseQueryString(new Uri("https://portal" + _mail.LastLinkTo(user.Email)).Query);

        using (var weak = await anonymous.PostAsync("/api/auth/password/reset", new { userId = query["user"], token = query["token"], newPassword = "short" }))
        {
            weak.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using var strong = await anonymous.PostAsync("/api/auth/password/reset", new { userId = query["user"], token = query["token"], newPassword = NewPassword });
        strong.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Changing_the_password_from_the_account_page_sends_a_notice()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("changer"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(_host, user);

        using var changed = await client.PostAsync(
            "/api/account/change-password",
            new { currentPassword = Scenario.Password, newPassword = NewPassword });
        changed.IsSuccessStatusCode.ShouldBeTrue(await changed.Content.ReadAsStringAsync());

        _mail.Messages.ShouldContain(message => message.To == user.Email && message.Subject.Contains("changed", StringComparison.Ordinal));

        // The session that changed it stays signed in; the stamp ends only the others.
        (await Scenario.GetJsonAsync(client, "/api/auth/session")).GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
    }
}
