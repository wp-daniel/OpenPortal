using OpenPortal.Notifications.Domain;

namespace OpenPortal.Tests.Unit.Notifications;

public sealed class DeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static OutboxMessage Message() => OutboxMessage.Create(Guid.NewGuid(), "email", "{}", "ada@example.com: Hello", Now).Value;

    [Fact]
    public void A_new_message_is_due_at_once()
    {
        var message = Message();

        message.Status.ShouldBe(OutboxMessageStatus.Pending);
        message.IsDueAt(Now).ShouldBeTrue();
    }

    [Fact]
    public void A_failed_attempt_waits_longer_each_time_then_gives_up()
    {
        var message = Message();

        message.MarkAttemptFailed("421", Now, maxAttempts: 3, permanent: false);
        message.Status.ShouldBe(OutboxMessageStatus.Pending);
        message.AvailableAtUtc.ShouldBe(Now.AddSeconds(30));
        message.IsDueAt(Now.AddSeconds(29)).ShouldBeFalse();

        message.MarkAttemptFailed("421", Now, maxAttempts: 3, permanent: false);
        message.AvailableAtUtc.ShouldBe(Now.AddMinutes(2));

        message.MarkAttemptFailed("421", Now, maxAttempts: 3, permanent: false);
        message.Status.ShouldBe(OutboxMessageStatus.Failed);
        message.Attempts.ShouldBe(3);
        message.LastError.ShouldBe("421");
        message.IsDueAt(Now.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void A_permanent_failure_is_not_retried()
    {
        var message = Message();

        message.MarkAttemptFailed("no handler", Now, maxAttempts: 10, permanent: true);

        message.Status.ShouldBe(OutboxMessageStatus.Failed);
    }

    [Fact]
    public void Retry_puts_a_failed_message_back_but_not_a_delivered_one()
    {
        var failed = Message();
        failed.MarkAttemptFailed("x", Now, maxAttempts: 1, permanent: false);

        failed.Retry(Now.AddHours(1)).IsSuccess.ShouldBeTrue();
        failed.Status.ShouldBe(OutboxMessageStatus.Pending);
        failed.IsDueAt(Now.AddHours(1)).ShouldBeTrue();

        var delivered = Message();
        delivered.MarkDelivered(Now);
        delivered.Retry(Now).Error.ShouldBe(OutboxErrors.AlreadyDelivered);
    }

    [Fact]
    public void A_very_long_error_is_cut()
    {
        var message = Message();

        message.MarkAttemptFailed(new string('x', 5000), Now, maxAttempts: 10, permanent: false);

        message.LastError!.Length.ShouldBe(OutboxMessage.ErrorMaxLength);
    }

    [Theory]
    [InlineData("smtp.example.com", 587, "portal@example.com", true)]
    [InlineData("10.0.0.5", 25, "portal@example.com", true)]
    [InlineData("smtp://smtp.example.com", 587, "portal@example.com", false)]
    [InlineData("smtp example.com", 587, "portal@example.com", false)]
    [InlineData("smtp.example.com", 0, "portal@example.com", false)]
    [InlineData("smtp.example.com", 587, "not an address", false)]
    [InlineData("smtp.example.com", 587, "Portal <portal@example.com>", false)]
    [InlineData("", 0, null, true)]
    public void Mail_settings_need_a_plain_host_a_port_and_a_sender(string host, int port, string? from, bool valid) =>
        MailSettings.Validate(host, port, from, null, null).IsSuccess.ShouldBe(valid);

    [Fact]
    public void The_password_is_kept_unless_replaced_or_cleared()
    {
        var settings = MailSettings.Defaults();

        settings.Update("smtp.example.com", 587, MailSecurity.StartTls, "portal", "protected-1", false, "portal@example.com", null, Now)
            .Value.ShouldBeTrue();
        settings.ProtectedPassword.ShouldBe("protected-1");
        settings.IsConfigured.ShouldBeTrue();

        // Nothing new: nothing changes.
        settings.Update("smtp.example.com", 587, MailSecurity.StartTls, "portal", null, false, "portal@example.com", null, Now)
            .Value.ShouldBeFalse();
        settings.ProtectedPassword.ShouldBe("protected-1");

        settings.Update("smtp.example.com", 587, MailSecurity.StartTls, "portal", null, true, "portal@example.com", null, Now)
            .Value.ShouldBeTrue();
        settings.ProtectedPassword.ShouldBeNull();
    }

    [Fact]
    public void Without_a_host_the_settings_are_not_in_use()
    {
        var settings = MailSettings.Defaults();

        settings.Update(" ", 587, MailSecurity.Auto, null, null, false, null, null, Now).IsSuccess.ShouldBeTrue();

        settings.IsConfigured.ShouldBeFalse();
    }
}
