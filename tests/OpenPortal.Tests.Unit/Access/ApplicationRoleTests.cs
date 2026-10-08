using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Grants;

namespace OpenPortal.Tests.Unit.Access;

public sealed class ApplicationRoleTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static PortalApplication Application() =>
        PortalApplication.CreateManual(
            Guid.NewGuid(),
            "crm",
            new ApplicationDetails("CRM", null, "https://crm.example.com", ["https://crm.example.com/signin-oidc"], []),
            Now).Value;

    private static ApplicationRoleDetails Role(string key, string? name = null) => new(key, name, null);

    [Theory]
    [InlineData("sales", "sales")]
    [InlineData("  Sales ", "sales")]
    [InlineData("crm:admin", "crm:admin")]
    [InlineData("billing.read_only-2", "billing.read_only-2")]
    public void A_role_key_is_trimmed_and_lower_cased(string key, string expected)
    {
        ApplicationRole.ValidateKey(key).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("-sales")]
    [InlineData("sales team")]
    [InlineData("ventes/équipe")]
    [InlineData(".hidden")]
    public void A_role_key_with_other_characters_is_rejected(string key)
    {
        ApplicationRole.ValidateKey(key).Error.ShouldBe(AccessErrors.RoleKeyInvalid);
    }

    [Fact]
    public void A_role_key_is_required_and_bounded()
    {
        ApplicationRole.ValidateKey("  ").Error.ShouldBe(AccessErrors.RoleKeyRequired);
        ApplicationRole.ValidateKey(new string('a', ApplicationRole.KeyMaxLength + 1)).Error.ShouldBe(AccessErrors.RoleKeyInvalid);
    }

    [Fact]
    public void A_role_without_a_name_is_named_after_its_key()
    {
        ApplicationRole.Validate(Role("sales")).Value.DisplayName.ShouldBe("sales");
    }

    [Fact]
    public void Setting_roles_renames_adds_and_removes_by_key_and_reports_the_removed_keys()
    {
        var application = Application();
        application.SetRoles([Role("sales", "Sales"), Role("billing", "Billing")], Now).IsSuccess.ShouldBeTrue();

        var removed = application.SetRoles([Role("sales", "Sales team"), Role("support")], Now.AddMinutes(1));

        removed.Value.ShouldBe(["billing"]);
        application.Roles.Select(role => (role.Key, role.DisplayName)).ShouldBe([("sales", "Sales team"), ("support", "support")], ignoreOrder: true);
        application.UpdatedAtUtc.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void Setting_the_same_roles_again_is_not_a_change()
    {
        var application = Application();
        application.SetRoles([Role("sales", "Sales")], Now);

        application.SetRoles([Role("sales", "Sales")], Now.AddDays(1)).Value.ShouldBeEmpty();
        application.UpdatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Duplicate_keys_and_too_many_roles_are_rejected_without_changing_anything()
    {
        var application = Application();
        application.SetRoles([Role("sales")], Now);

        application.SetRoles([Role("sales"), Role("SALES")], Now).Error.ShouldBe(AccessErrors.RoleKeyDuplicate);
        application.SetRoles(Enumerable.Range(0, PortalApplication.MaxRoles + 1).Select(index => Role($"role-{index}")), Now)
            .Error.ShouldBe(AccessErrors.TooManyRoles);

        application.Roles.Select(role => role.Key).ShouldBe(["sales"]);
    }

    [Fact]
    public void An_approved_application_only_gains_the_roles_it_announces()
    {
        var application = PortalApplication.CreateAnnounced(
            Guid.NewGuid(),
            "crm",
            new ApplicationDetails("CRM", null, "https://crm.example.com", ["https://crm.example.com/signin-oidc"], []),
            "1.0",
            Now).Value;

        application.RecordAnnouncedRoles([Role("viewer", "Viewer"), Role("editor")]).IsSuccess.ShouldBeTrue();
        application.RecordAnnouncedRoles([Role("viewer", "Viewer")]).IsSuccess.ShouldBeTrue();
        application.Roles.Select(role => role.Key).ShouldBe(["viewer"], "a pending application takes the list whole");

        application.Approve(Now).IsSuccess.ShouldBeTrue();
        application.SetRoles([Role("viewer", "Reader")], Now);

        application.RecordAnnouncedRoles([Role("viewer", "Viewer"), Role("auditor")]).IsSuccess.ShouldBeTrue();

        application.Roles.Select(role => (role.Key, role.DisplayName)).ShouldBe([("viewer", "Reader"), ("auditor", "auditor")], ignoreOrder: true);
    }

    [Fact]
    public void The_group_claim_setting_records_a_change_only_when_it_changes()
    {
        var application = Application();

        application.SetGroupClaims(GroupClaimMode.None, Now.AddDays(1));
        application.UpdatedAtUtc.ShouldBeNull();

        application.SetGroupClaims(GroupClaimMode.Granted, Now.AddDays(2));
        application.GroupClaims.ShouldBe(GroupClaimMode.Granted);
        application.UpdatedAtUtc.ShouldBe(Now.AddDays(2));
    }

    [Fact]
    public void A_grant_keeps_its_roles_sorted_and_unique_and_reports_whether_they_changed()
    {
        var grant = new ApplicationUserGrant(Guid.NewGuid(), Guid.NewGuid(), Now);

        grant.SetRoles(["sales", "billing", "sales"]).ShouldBeTrue();
        grant.Roles.ShouldBe(["billing", "sales"]);
        grant.SetRoles(["sales", "billing"]).ShouldBeFalse();
        grant.SetRoles([]).ShouldBeTrue();
        grant.Roles.ShouldBeEmpty();
    }
}
