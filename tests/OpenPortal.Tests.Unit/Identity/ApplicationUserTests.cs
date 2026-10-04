using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Tests.Unit.Identity;

public sealed class ApplicationUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static ApplicationUser NewUser(string email = "ada@example.com", string displayName = "Ada Lovelace") =>
        new(Guid.NewGuid(), email, displayName, Now);

    [Fact]
    public void Constructor_sets_the_user_name_to_the_email_address()
    {
        var user = NewUser("  Ada@Example.com  ");

        // Identity's user validator requires a non-empty user name even for email-only sign-in. Deriving
        // it from the email keeps one identifier per account instead of a second one nobody signs in with.
        user.Email.ShouldBe("Ada@Example.com");
        user.UserName.ShouldBe("Ada@Example.com");
        user.DisplayName.ShouldBe("Ada Lovelace");
        user.CreatedAtUtc.ShouldBe(Now);
        user.UpdatedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Constructor_rejects_a_blank_email()
    {
        Should.Throw<ArgumentException>(() => NewUser("   "));
    }

    [Fact]
    public void Constructor_rejects_a_null_email()
    {
        Should.Throw<ArgumentNullException>(() => NewUser(null!));
    }

    [Fact]
    public void Constructor_rejects_a_blank_display_name()
    {
        Should.Throw<ArgumentException>(() => NewUser("ada@example.com", "   "));
    }

    [Fact]
    public void Constructor_rejects_a_null_display_name()
    {
        Should.Throw<ArgumentNullException>(() => NewUser("ada@example.com", null!));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   ")]
    public void UpdateDisplayName_rejects_a_name_below_the_minimum(string displayName)
    {
        var user = NewUser();
        user.UpdateDisplayName("Grace Hopper", Now);

        var result = user.UpdateDisplayName(displayName, Now.AddHours(1));

        result.IsFailure.ShouldBeTrue();
        user.DisplayName.ShouldBe("Grace Hopper");
        user.UpdatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void UpdateDisplayName_rejects_an_over_long_name()
    {
        var user = NewUser();

        var result = user.UpdateDisplayName(
            new string('x', ApplicationUser.DisplayNameMaxLength + 1),
            Now);

        result.Error.ShouldBe(UserErrors.DisplayNameTooLong);
    }

    [Fact]
    public void UpdateDisplayName_trims_and_stamps_only_on_an_actual_change()
    {
        var user = NewUser();
        user.UpdateDisplayName("Grace Hopper", Now);

        user.UpdateDisplayName("Grace Hopper", Now).IsSuccess.ShouldBeTrue();
        // Same value submitted again: not a change, so the timestamp must not move. Otherwise every form
        // save would claim the account had been modified.
        user.UpdatedAtUtc.ShouldBe(Now);

        user.UpdateDisplayName("  Grace Hopper  ", Now.AddDays(1)).IsSuccess.ShouldBeTrue();
        user.DisplayName.ShouldBe("Grace Hopper");
        user.UpdatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void A_new_user_has_no_language_preference()
    {
        NewUser().Language.ShouldBeNull();
    }

    [Theory]
    [InlineData("it")]
    [InlineData("pt-BR")]
    [InlineData("  fr  ")]
    public void SetLanguage_stores_a_well_formed_code(string code)
    {
        var user = NewUser();

        user.SetLanguage(code, Now.AddDays(1)).IsSuccess.ShouldBeTrue();

        user.Language.ShouldBe(code.Trim());
        user.UpdatedAtUtc.ShouldBe(Now.AddDays(1));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("it_IT")]
    [InlineData("-it")]
    [InlineData("it-")]
    [InlineData("abcdefghijkl")]
    [InlineData("i t")]
    public void SetLanguage_rejects_a_malformed_code(string code)
    {
        var user = NewUser();

        var result = user.SetLanguage(code, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.UnsupportedLanguage);
        user.Language.ShouldBeNull();
    }

    [Fact]
    public void SetLanguage_with_null_clears_the_preference()
    {
        var user = NewUser();
        user.SetLanguage("it", Now);

        user.SetLanguage(null, Now.AddDays(1)).IsSuccess.ShouldBeTrue();

        user.Language.ShouldBeNull();
    }

    [Fact]
    public void SetLanguage_does_not_touch_the_timestamp_when_nothing_changes()
    {
        var user = NewUser();
        user.SetLanguage("it", Now);

        user.SetLanguage("it", Now.AddDays(1));

        user.UpdatedAtUtc.ShouldBe(Now);
    }

    private static UserDetails Details(
        string? first = "Grace",
        string? last = "Hopper",
        string? phone = "+1 202 555 0143") =>
        new(first, last, phone, " Rear Admiral ", "", null, "1 Navy Way", "Arlington", "22202", "USA");

    [Fact]
    public void UpdateDetails_stores_the_details_trims_them_and_derives_the_display_name()
    {
        var user = NewUser();

        var result = user.UpdateDetails(Details(), Now.AddHours(1));

        result.IsSuccess.ShouldBeTrue();
        user.FirstName.ShouldBe("Grace");
        user.LastName.ShouldBe("Hopper");
        user.DisplayName.ShouldBe("Grace Hopper");
        user.PhoneNumber.ShouldBe("+1 202 555 0143");
        user.JobTitle.ShouldBe("Rear Admiral");
        user.Company.ShouldBeNull();
        user.UpdatedAtUtc.ShouldBe(Now.AddHours(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void UpdateDetails_requires_a_first_name(string? first)
    {
        var user = NewUser();

        var result = user.UpdateDetails(Details(first: first), Now);

        result.Error.ShouldBe(UserErrors.FirstNameRequired);
        user.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public void UpdateDetails_requires_a_last_name()
    {
        NewUser().UpdateDetails(Details(last: ""), Now).Error.ShouldBe(UserErrors.LastNameRequired);
    }

    [Theory]
    [InlineData("call me")]
    [InlineData("123")]
    [InlineData("+39 333 abc 4567")]
    public void UpdateDetails_rejects_a_malformed_phone_number(string phone)
    {
        NewUser().UpdateDetails(Details(phone: phone), Now).Error.ShouldBe(UserErrors.InvalidPhoneNumber);
    }

    [Fact]
    public void UpdateDetails_rejects_over_long_names()
    {
        var tooLong = new string('x', ApplicationUser.NameMaxLength + 1);

        NewUser().UpdateDetails(Details(first: tooLong), Now).Error.ShouldBe(UserErrors.NameTooLong);
    }

    [Fact]
    public void UpdateDetails_clears_the_phone_number_when_blank()
    {
        var user = NewUser();
        user.UpdateDetails(Details(), Now);

        user.UpdateDetails(Details(phone: "  "), Now).IsSuccess.ShouldBeTrue();

        user.PhoneNumber.ShouldBeNull();
    }
}