using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Tests.Unit.Text;

public sealed class TextRulesTests
{
    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("hello", "hello")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("\t\n", null)]
    public void Normalise_trims_and_collapses_blank_input(string? input, string? expected)
    {
        TextRules.Normalise(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?q=1#frag")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    public void IsHttpUrl_accepts_absolute_web_urls(string value)
    {
        TextRules.IsHttpUrl(value).ShouldBeTrue();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path")]
    [InlineData("example.com")]
    [InlineData("")]
    public void IsHttpUrl_rejects_anything_else(string value)
    {
        // The scheme matters: a profile avatar or social link rendered into an href is an injection
        // vector for javascript: and data: URLs, so only http and https are accepted.
        TextRules.IsHttpUrl(value).ShouldBeFalse();
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last@sub.example.co.uk")]
    [InlineData("user+tag@example.com")]
    public void IsPlausibleEmail_accepts_realistic_addresses(string value)
    {
        TextRules.IsPlausibleEmail(value).ShouldBeTrue();
    }

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@@example.com")]
    [InlineData("user@localhost")]
    [InlineData(".user@example.com")]
    [InlineData("user.@example.com")]
    [InlineData("us..er@example.com")]
    [InlineData("user@-example.com")]
    [InlineData("user@example.")]
    [InlineData("user name@example.com")]
    [InlineData("")]
    public void IsPlausibleEmail_rejects_malformed_addresses(string value)
    {
        TextRules.IsPlausibleEmail(value).ShouldBeFalse();
    }

    [Theory]
    [InlineData("My-Project_2026", "my-project_2026")]
    [InlineData("  spaced  ", "spaced")]
    [InlineData("UPPER", "upper")]
    public void TryNormaliseSlug_lowercases_a_valid_slug(string input, string expected)
    {
        TextRules.TryNormaliseSlug(input, out var slug).ShouldBeTrue();
        slug.ShouldBe(expected);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has/slash")]
    [InlineData("has?query")]
    [InlineData("accented-à")]
    [InlineData("emoji-🚀")]
    [InlineData("")]
    [InlineData(null)]
    public void TryNormaliseSlug_rejects_anything_outside_the_allowed_alphabet(string? input)
    {
        // Non-ASCII letters are refused even though Uri escaping could handle them: a slug appears in a
        // visible URL, and an escaped slug is worse for sharing and for search engines than a plain one.
        TextRules.TryNormaliseSlug(input, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryNormaliseSlug_reports_blank_input_as_invalid()
    {
        TextRules.TryNormaliseSlug("   ", out var slug).ShouldBeFalse();
        slug.ShouldBe(string.Empty);
    }
}