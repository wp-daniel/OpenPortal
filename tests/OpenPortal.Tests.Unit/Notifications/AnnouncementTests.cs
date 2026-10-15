using OpenPortal.Notifications.Domain;

namespace OpenPortal.Tests.Unit.Notifications;

public sealed class AnnouncementTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static AnnouncementContent Content(
        DateTimeOffset? starts = null,
        DateTimeOffset? ends = null,
        params (string Language, string Title)[] texts) =>
        new(
            AnnouncementSeverity.Warning,
            AnnouncementAudience.Everyone,
            starts,
            ends,
            Dismissible: true,
            texts.Select(text => new AnnouncementTextContent(text.Language, text.Title, null)).ToList());

    [Fact]
    public void An_announcement_shows_inside_its_window_only()
    {
        var announcement = Announcement.Create(Guid.NewGuid(), Content(Now, Now.AddHours(2), ("en", "Maintenance")), Now).Value;

        announcement.IsActiveAt(Now.AddMinutes(-1)).ShouldBeFalse();
        announcement.IsActiveAt(Now).ShouldBeTrue();
        announcement.IsActiveAt(Now.AddHours(2)).ShouldBeFalse();
    }

    [Fact]
    public void Invalid_content_is_refused_with_its_own_code()
    {
        Announcement.Create(Guid.NewGuid(), Content(texts: []), Now).Error.ShouldBe(AnnouncementErrors.TextRequired);
        Announcement.Create(Guid.NewGuid(), Content(Now, Now, ("en", "x")), Now).Error.ShouldBe(AnnouncementErrors.WindowInvalid);
        Announcement.Create(Guid.NewGuid(), Content(null, null, ("en", " ")), Now).Error.ShouldBe(AnnouncementErrors.TitleRequired);
        Announcement.Create(Guid.NewGuid(), Content(null, null, ("en", "a"), ("en", "b")), Now).Error.ShouldBe(AnnouncementErrors.LanguageInvalid);
        Announcement.Create(Guid.NewGuid(), Content(null, null, ("en", new string('x', 121))), Now).Error.ShouldBe(AnnouncementErrors.TextTooLong);
    }

    [Fact]
    public void Editing_reports_a_change_only_when_there_is_one()
    {
        var announcement = Announcement.Create(Guid.NewGuid(), Content(null, null, ("en", "Hello")), Now).Value;

        announcement.Update(Content(null, null, ("en", "Hello")), Now).Value.ShouldBeFalse();
        announcement.UpdatedAtUtc.ShouldBeNull();

        announcement.Update(Content(null, null, ("en", "Hello"), ("it", "Ciao")), Now).Value.ShouldBeTrue();
        announcement.UpdatedAtUtc.ShouldBe(Now);
        announcement.TextFor("it", "en").Title.ShouldBe("Ciao");
        announcement.TextFor("de", "en").Title.ShouldBe("Hello");
    }
}
