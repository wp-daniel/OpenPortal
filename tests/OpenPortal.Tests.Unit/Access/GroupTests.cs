using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Groups;

namespace OpenPortal.Tests.Unit.Access;

public sealed class GroupTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_group_is_created_with_a_trimmed_name_and_an_upper_cased_key()
    {
        var group = Group.Create(Guid.NewGuid(), "  Sales  ", "  ", Now).Value;

        group.Name.ShouldBe("Sales");
        group.NormalisedName.ShouldBe("SALES");
        group.Description.ShouldBeNull();
    }

    [Fact]
    public void A_blank_or_over_long_name_is_rejected()
    {
        Group.Create(Guid.NewGuid(), " ", null, Now).Error.ShouldBe(AccessErrors.GroupNameRequired);
        Group.Create(Guid.NewGuid(), new string('x', Group.NameMaxLength + 1), null, Now).Error.ShouldBe(AccessErrors.GroupNameTooLong);
    }

    [Fact]
    public void Adding_a_member_twice_changes_nothing()
    {
        var group = Group.Create(Guid.NewGuid(), "Sales", null, Now).Value;
        var userId = Guid.NewGuid();

        group.AddMember(userId, Now).ShouldBeTrue();
        group.AddMember(userId, Now).ShouldBeFalse();

        group.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void Removing_reports_whether_the_user_was_a_member()
    {
        var group = Group.Create(Guid.NewGuid(), "Sales", null, Now).Value;
        var userId = Guid.NewGuid();
        group.AddMember(userId, Now);

        group.RemoveMember(Guid.NewGuid()).ShouldBeFalse();
        group.RemoveMember(userId).ShouldBeTrue();
        group.Members.ShouldBeEmpty();
    }
}
