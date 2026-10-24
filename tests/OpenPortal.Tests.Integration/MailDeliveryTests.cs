using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Web.Email;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The SMTP server saved from the portal (Settings → Email), the test email, and the delivery queue that retries
/// what the mail server refused. The tests of this class share one settings row, so each writes what it needs.
/// </summary>
public sealed class MailDeliveryTests : IClassFixture<OpenPortalFactory>
{
    private readonly OpenPortalFactory _factory;
    private readonly CapturingMailTransport _transport = new();
    private readonly WebApplicationFactory<Program> _host;

    public MailDeliveryTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IMailTransport>(_transport)));
    }

    [Fact]
    public async Task The_password_is_stored_but_never_returned_and_kept_when_left_empty()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);

        using (var saved = await admin.PutAsync("/api/admin/settings/email", Settings(password: "s3cret-smtp")))
        {
            var body = await Scenario.JsonAsync(saved, HttpStatusCode.OK);
            body.GetProperty("hasPassword").GetBoolean().ShouldBeTrue();
            body.GetProperty("source").GetString().ShouldBe("portal");
            body.TryGetProperty("password", out _).ShouldBeFalse();
            (await saved.Content.ReadAsStringAsync()).ShouldNotContain("s3cret-smtp");
        }

        // Saving again without a password keeps it; the test email then authenticates with the stored one.
        using (var kept = await admin.PutAsync("/api/admin/settings/email", Settings(fromName: "Renamed")))
        {
            (await Scenario.JsonAsync(kept, HttpStatusCode.OK)).GetProperty("hasPassword").GetBoolean().ShouldBeTrue();
        }

        using (var tested = await admin.PostAsync("/api/admin/settings/email/test", TestRequest("ops@example.com")))
        {
            (await Scenario.JsonAsync(tested, HttpStatusCode.OK)).GetProperty("delivered").GetBoolean().ShouldBeTrue();
        }

        var (settings, message) = _transport.Sent.Last();
        settings.Password.ShouldBe("s3cret-smtp");
        settings.Host.ShouldBe("smtp.example.com");
        message.To.ShouldBe("ops@example.com");

        using (var cleared = await admin.PutAsync("/api/admin/settings/email", Settings(clearPassword: true)))
        {
            (await Scenario.JsonAsync(cleared, HttpStatusCode.OK)).GetProperty("hasPassword").GetBoolean().ShouldBeFalse();
        }
    }

    [Fact]
    public async Task A_failing_test_email_reports_the_server_answer()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        _transport.FailWith = "535 5.7.8 Authentication credentials invalid";

        try
        {
            using var tested = await admin.PostAsync("/api/admin/settings/email/test", TestRequest("ops@example.com"));
            var body = await Scenario.JsonAsync(tested, HttpStatusCode.OK);
            body.GetProperty("delivered").GetBoolean().ShouldBeFalse();
            body.GetProperty("error").GetString()!.ShouldContain("535");
        }
        finally
        {
            _transport.FailWith = null;
        }
    }

    [Fact]
    public async Task Invalid_settings_are_refused()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);

        using var badFrom = await admin.PutAsync("/api/admin/settings/email", new
        {
            host = "smtp.example.com",
            port = 587,
            security = "auto",
            fromAddress = "not an address",
        });
        await Scenario.ExpectErrorAsync(badFrom, HttpStatusCode.BadRequest, "mail.from_invalid");

        using var badHost = await admin.PutAsync("/api/admin/settings/email", new
        {
            host = "smtp://smtp.example.com",
            port = 587,
            security = "auto",
            fromAddress = "portal@example.com",
        });
        await Scenario.ExpectErrorAsync(badHost, HttpStatusCode.BadRequest, "mail.host_invalid");

        using var badSecurity = await admin.PutAsync("/api/admin/settings/email", new
        {
            host = "smtp.example.com",
            port = 587,
            security = "quantum",
            fromAddress = "portal@example.com",
        });
        await Scenario.ExpectErrorAsync(badSecurity, HttpStatusCode.BadRequest, "mail.security_invalid");
    }

    [Fact]
    public async Task Only_administrators_see_the_email_settings_and_the_queue()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("plain"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(_host, user);

        using var settings = await client.GetAsync("/api/admin/settings/email");
        settings.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var queue = await client.GetAsync("/api/admin/outbox");
        queue.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_email_the_server_refuses_stays_queued_and_goes_out_on_retry()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        using (var saved = await admin.PutAsync("/api/admin/settings/email", Settings(password: "pw")))
        {
            saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var invited = Scenario.UniqueEmail("queued");
        _transport.FailWith = "421 Service not available";

        try
        {
            // An invitation is accepted although the mail server is down: the email waits in the queue.
            using var invitation = await admin.PostAsync("/api/admin/invitations", new { email = invited });
            invitation.IsSuccessStatusCode.ShouldBeTrue(await invitation.Content.ReadAsStringAsync());
        }
        finally
        {
            _transport.FailWith = null;
        }

        var queue = await Scenario.GetJsonAsync(admin, "/api/admin/outbox?status=pending");
        var waiting = queue.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("description").GetString()!.StartsWith(invited, StringComparison.Ordinal));
        waiting.GetProperty("attempts").GetInt32().ShouldBe(1);
        waiting.GetProperty("lastError").GetString()!.ShouldContain("421");
        queue.GetProperty("summary").GetProperty("pending").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        _transport.Sent.ShouldNotContain(sent => sent.Message.To == invited);

        // Retried by hand (the test host delivers inline instead of in the background): it goes out now.
        using (var retried = await admin.PostAsync($"/api/admin/outbox/{waiting.GetProperty("id").GetGuid()}/retry"))
        {
            var message = await Scenario.JsonAsync(retried, HttpStatusCode.OK);
            message.GetProperty("status").GetString().ShouldBe("delivered");
            message.GetProperty("attempts").GetInt32().ShouldBe(2);
        }

        _transport.Sent.ShouldContain(sent => sent.Message.To == invited);

        using var again = await admin.PostAsync($"/api/admin/outbox/{waiting.GetProperty("id").GetGuid()}/retry");
        await Scenario.ExpectErrorAsync(again, HttpStatusCode.Conflict, "outbox.already_delivered");
    }

    private static object Settings(string? password = null, bool clearPassword = false, string fromName = "OpenPortal") => new
    {
        host = "smtp.example.com",
        port = 587,
        security = "starttls",
        username = "portal",
        password,
        clearPassword,
        fromAddress = "portal@example.com",
        fromName,
    };

    private static object TestRequest(string to) => new
    {
        host = "smtp.example.com",
        port = 587,
        security = "starttls",
        username = "portal",
        fromAddress = "portal@example.com",
        to,
    };
}

/// <summary>Keeps what would have gone to the mail server, or fails like it while <see cref="FailWith"/> is set.</summary>
internal sealed class CapturingMailTransport : IMailTransport
{
    private readonly List<(MailTransportSettings Settings, EmailMessage Message)> _sent = [];

    public string? FailWith { get; set; }

    public IReadOnlyList<(MailTransportSettings Settings, EmailMessage Message)> Sent
    {
        get
        {
            lock (_sent)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(MailTransportSettings settings, EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailWith is { } error)
        {
            throw new InvalidOperationException(error);
        }

        lock (_sent)
        {
            _sent.Add((settings, message));
        }

        return Task.CompletedTask;
    }
}
