using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Grants;

namespace OpenPortal.Tests.Unit.Access;

public sealed class PageGroupGrantTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("users")]
    [InlineData("content.projects")]
    [InlineData("crm.deals-2026")]
    public void A_well_formed_key_is_accepted(string key)
    {
        var grant = PageGroupGrant.Create(key, Guid.NewGuid(), Now).Value;

        grant.PageKey.ShouldBe(key);
        grant.GrantedAtUtc.ShouldBe(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Users")]
    [InlineData("content..projects")]
    [InlineData(".users")]
    [InlineData("users/edit")]
    [InlineData("user s")]
    public void A_malformed_key_is_a_validation_error(string? key)
    {
        PageGroupGrant.Create(key, Guid.NewGuid(), Now).Error.ShouldBe(AccessErrors.PageKeyInvalid);
    }

    [Fact]
    public void An_over_long_key_is_rejected()
    {
        PageGroupGrant.ValidatePageKey(new string('a', PageGroupGrant.PageKeyMaxLength + 1)).Error
            .ShouldBe(AccessErrors.PageKeyInvalid);
    }
}
