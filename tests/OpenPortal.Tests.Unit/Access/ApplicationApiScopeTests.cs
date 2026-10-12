using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;

namespace OpenPortal.Tests.Unit.Access;

public sealed class ApplicationApiScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static PortalApplication Application() =>
        PortalApplication.CreateManual(
            Guid.NewGuid(),
            "crm",
            new ApplicationDetails("CRM", null, "https://crm.example.com", ["https://crm.example.com/signin-oidc"], []),
            Now).Value;

    [Fact]
    public void A_new_application_has_no_api_scopes()
    {
        Application().ApiScopes.ShouldBeEmpty();
    }

    [Fact]
    public void Scopes_are_normalised_deduplicated_and_kept_in_catalog_order()
    {
        var application = Application();

        application.SetApiScopes([" PORTAL.ACCESS.MANAGE ", "portal.users.read", "portal.users.read", ""], Now.AddDays(1)).IsSuccess.ShouldBeTrue();

        application.ApiScopes.ShouldBe([ApplicationApiScopes.UsersRead, ApplicationApiScopes.AccessManage]);
        application.UpdatedAtUtc.ShouldBe(Now.AddDays(1));
        application.HasApiScope(ApplicationApiScopes.UsersRead).ShouldBeTrue();
        application.HasApiScope(ApplicationApiScopes.UsersCreate).ShouldBeFalse();
    }

    [Fact]
    public void An_unknown_scope_is_rejected_and_nothing_changes()
    {
        var application = Application();

        application.SetApiScopes([ApplicationApiScopes.UsersRead, "portal.everything"], Now).Error.ShouldBe(AccessErrors.ApiScopeInvalid);

        application.ApiScopes.ShouldBeEmpty();
    }

    [Fact]
    public void Creating_users_requires_managing_access()
    {
        var application = Application();

        application.SetApiScopes([ApplicationApiScopes.UsersCreate], Now).Error.ShouldBe(AccessErrors.ApiScopeRequiresAccessManage);
        application.SetApiScopes([ApplicationApiScopes.UsersCreate, ApplicationApiScopes.AccessManage], Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Setting_the_same_scopes_again_is_not_a_change()
    {
        var application = Application();
        application.SetApiScopes([ApplicationApiScopes.UsersRead], Now.AddDays(1));

        application.SetApiScopes([ApplicationApiScopes.UsersRead], Now.AddDays(2)).IsSuccess.ShouldBeTrue();

        application.UpdatedAtUtc.ShouldBe(Now.AddDays(1));
    }
}
