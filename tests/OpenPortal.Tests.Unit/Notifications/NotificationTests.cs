using OpenPortal.Notifications.Domain;

namespace OpenPortal.Tests.Unit.Notifications;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("/admin/access-requests", "/admin/access-requests")]
    [InlineData(" /account?tab=security ", "/account?tab=security")]
    [InlineData("https://evil.example.com", null)]
    [InlineData("//evil.example.com/path", null)]
    [InlineData("/\\evil.example.com", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("", null)]
    public void Only_local_paths_are_kept_as_links(string link, string? expected) =>
        Notification.NormaliseLink(link).ShouldBe(expected);

    [Theory]
    [InlineData("access.request_submitted", true)]
    [InlineData("Access.Request", false)]
    [InlineData("access..request", false)]
    [InlineData("access request", false)]
    public void The_type_is_dotted_lower_case(string type, bool valid) =>
        Notification.Create(Guid.NewGuid(), Guid.NewGuid(), type, null, null, Now).IsSuccess.ShouldBe(valid);

    [Fact]
    public void Reading_and_unreading_report_whether_anything_changed()
    {
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "access.group_joined", "{}", "/", Now).Value;

        notification.IsRead.ShouldBeFalse();
        notification.MarkRead(Now).ShouldBeTrue();
        notification.MarkRead(Now).ShouldBeFalse();
        notification.MarkUnread().ShouldBeTrue();
        notification.MarkUnread().ShouldBeFalse();
    }

    [Fact]
    public void A_notice_without_a_recipient_or_with_oversized_values_is_handled()
    {
        Notification.Create(Guid.NewGuid(), Guid.Empty, "access.group_joined", null, null, Now)
            .Error.ShouldBe(NotificationErrors.RecipientRequired);

        Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "access.group_joined", new string('x', Notification.ParametersMaxLength + 1), null, Now)
            .Value.Parameters.ShouldBeNull();
    }
}
