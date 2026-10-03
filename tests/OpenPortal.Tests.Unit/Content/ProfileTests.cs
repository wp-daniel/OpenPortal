using OpenPortal.Content.Domain;
using OpenPortal.Content.Domain.Profiles;

namespace OpenPortal.Tests.Unit.Content;

public sealed class ProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static Profile NewProfile(string displayName = "Ada Lovelace") =>
        new(Guid.NewGuid(), displayName, Now);

    [Fact]
    public void Constructor_trims_the_display_name()
    {
        var profile = NewProfile("  Ada Lovelace  ");

        profile.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_display_name(string displayName)
    {
        Should.Throw<ArgumentException>(() => NewProfile(displayName));
    }

    [Fact]
    public void Constructor_rejects_an_empty_id()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new Profile(Guid.Empty, "Ada Lovelace", Now));
    }

    [Fact]
    public void Update_applies_every_field_and_stamps_the_change()
    {
        var profile = NewProfile();
        profile.SocialLinks.ShouldBeEmpty();

        var result = profile.Update(
            "Grace Hopper",
            "Compiler pioneer",
            "Wrote the first compiler.",
            "Arlington",
            "grace@example.com",
            "https://example.com/avatar.png",
            Now.AddMinutes(5));

        result.IsSuccess.ShouldBeTrue();
        profile.DisplayName.ShouldBe("Grace Hopper");
        profile.Headline.ShouldBe("Compiler pioneer");
        profile.Summary.ShouldBe("Wrote the first compiler.");
        profile.Location.ShouldBe("Arlington");
        profile.Email.ShouldBe("grace@example.com");
        profile.AvatarUrl.ShouldBe("https://example.com/avatar.png");
        profile.UpdatedAtUtc.ShouldBe(Now.AddMinutes(5));
    }

[Fact]
    public void Update_is_atomic_and_leaves_the_profile_untouched_when_a_field_is_invalid()
    {
        var profile = NewProfile();
        profile.Update(
            "Grace Hopper",
            "Original headline",
            null,
            null,
            null,
            null,
            Now);

        // Every field here is valid except the avatar URL, and it is the last field checked. Nothing may
        // change: a partially applied edit is a silent data-corruption bug, so the earlier valid fields must
        // not have been written before validation failed.
        var result = profile.Update(
            "Changed Name",
            "Changed headline",
            "Changed summary",
            "Changed location",
            "grace@example.com",
            "javascript:alert(1)",
            Now.AddHours(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ContentErrors.UrlInvalid);
        profile.DisplayName.ShouldBe("Grace Hopper");
        profile.Headline.ShouldBe("Original headline");
        profile.Location.ShouldBeNull();
        profile.Email.ShouldBeNull();
        profile.AvatarUrl.ShouldBeNull();
        profile.UpdatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Update_rejects_an_invalid_email_and_never_looks_at_later_fields()
    {
        var profile = NewProfile();

        var result = profile.Update(
            "Grace Hopper",
            null,
            null,
            null,
            "grace@@example.com",
            "also-not-a-url",
            Now);

        // Email is validated before the avatar URL, so this asserts the documented order rather than
        // whichever field happens to fail last.
        result.Error.ShouldBe(ContentErrors.EmailInvalid);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   ")]
    public void Update_rejects_a_display_name_below_the_minimum(string displayName)
    {
        var profile = NewProfile();

        var result = profile.Update(displayName, null, null, null, null, null, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(
            string.IsNullOrWhiteSpace(displayName)
                ? ContentErrors.DisplayNameRequired.Code
                : ContentErrors.DisplayNameTooShort.Code);
    }

    [Fact]
    public void Update_rejects_a_display_name_above_the_maximum()
    {
        var profile = NewProfile();

        var result = profile.Update(new string('x', Profile.DisplayNameMaxLength + 1), null, null, null, null, null, Now);

        result.Error.ShouldBe(ContentErrors.DisplayNameTooLong);
    }

    [Fact]
    public void Update_rejects_an_over_long_summary()
    {
        var profile = NewProfile();

        var result = profile.Update(
            "Ada Lovelace",
            null,
            new string('x', Profile.SummaryMaxLength + 1),
            null,
            null,
            null,
            Now);

        result.Error.ShouldBe(ContentErrors.SummaryTooLong);
    }

    [Fact]
    public void Update_treats_blank_optional_fields_as_absent()
    {
        var profile = NewProfile();

        profile.Update("Ada Lovelace", "   ", string.Empty, null, "  ", "\t", Now);

        profile.Headline.ShouldBeNull();
        profile.Summary.ShouldBeNull();
        profile.Email.ShouldBeNull();
        profile.AvatarUrl.ShouldBeNull();
    }

    [Fact]
    public void ReplaceSocialLinks_assigns_contiguous_positions_in_order()
    {
        var profile = NewProfile();

        var result = profile.ReplaceSocialLinks(
            [
                new SocialLinkDraft("GitHub", "https://github.com/ada", null),
                new SocialLinkDraft("Mastodon", "https://mastodon.social/@ada", "my instance"),
                new SocialLinkDraft("Website", "https://ada.example.com", null),
            ],
            Now);

        result.IsSuccess.ShouldBeTrue();
        profile.SocialLinks.Count.ShouldBe(3);
        profile.SocialLinks.Select(link => link.Position).ShouldBe([0, 1, 2]);
        profile.SocialLinks.Select(link => link.Platform).ShouldBe(["GitHub", "Mastodon", "Website"]);
        profile.SocialLinks[1].Label.ShouldBe("my instance");
        profile.SocialLinks.All(link => link.ProfileId == profile.Id).ShouldBeTrue();
    }

    [Fact]
    public void ReplaceSocialLinks_clears_the_previous_set()
    {
        var profile = NewProfile();
        profile.ReplaceSocialLinks([new SocialLinkDraft("GitHub", "https://github.com/ada", null)], Now);

        profile.ReplaceSocialLinks([new SocialLinkDraft("GitLab", "https://gitlab.com/ada", null)], Now);

        profile.SocialLinks.Count.ShouldBe(1);
        profile.SocialLinks[0].Platform.ShouldBe("GitLab");
        profile.SocialLinks[0].Position.ShouldBe(0);
    }

    [Fact]
    public void ReplaceSocialLinks_rejects_a_non_web_url_without_applying_anything()
    {
        var profile = NewProfile();
        profile.ReplaceSocialLinks([new SocialLinkDraft("GitHub", "https://github.com/ada", null)], Now);

        var result = profile.ReplaceSocialLinks(
            [
                new SocialLinkDraft("GitLab", "https://gitlab.com/ada", null),
                new SocialLinkDraft("Evil", "javascript:alert(1)", null),
            ],
            Now);

        result.Error.ShouldBe(ContentErrors.UrlInvalid);
        // The valid first draft must not be applied, or a rejected edit would leave the profile changed.
        profile.SocialLinks.Count.ShouldBe(1);
        profile.SocialLinks[0].Platform.ShouldBe("GitHub");
    }

    [Fact]
    public void ReplaceSocialLinks_rejects_a_blank_platform()
    {
        var profile = NewProfile();

        var result = profile.ReplaceSocialLinks([new SocialLinkDraft("  ", "https://example.com", null)], Now);

        result.Error.ShouldBe(ContentErrors.SocialLinkPlatformRequired);
    }

    [Fact]
    public void ReplaceSocialLinks_accepts_an_empty_set()
    {
        var profile = NewProfile();
        profile.ReplaceSocialLinks([new SocialLinkDraft("GitHub", "https://github.com/ada", null)], Now);

        var result = profile.ReplaceSocialLinks([], Now);

        result.IsSuccess.ShouldBeTrue();
        profile.SocialLinks.ShouldBeEmpty();
    }

    [Fact]
    public void AddSocialLink_continues_the_position_sequence()
    {
        var profile = NewProfile();
        profile.AddSocialLink(new SocialLinkDraft("GitHub", "https://github.com/ada", null)).IsSuccess.ShouldBeTrue();

        var result = profile.AddSocialLink(new SocialLinkDraft("GitLab", "https://gitlab.com/ada", null));

        result.IsSuccess.ShouldBeTrue();
        profile.SocialLinks.Select(link => link.Position).ShouldBe([0, 1]);
    }

    [Fact]
    public void ValidateSocialLinks_validates_without_an_instance()
    {
        var ok = Profile.ValidateSocialLinks(
            [new SocialLinkDraft("GitHub", "https://github.com/ada", null)]);

        var bad = Profile.ValidateSocialLinks(
            [new SocialLinkDraft("GitHub", "not-a-url", null)]);

        ok.IsSuccess.ShouldBeTrue();
        bad.Error.ShouldBe(ContentErrors.UrlInvalid);
    }
}