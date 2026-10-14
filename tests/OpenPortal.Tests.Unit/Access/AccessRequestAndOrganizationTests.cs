using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Organizations;
using OpenPortal.Access.Domain.Requests;

namespace OpenPortal.Tests.Unit.Access;

public sealed class AccessRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static AccessRequest Pending(string? reason = "I need it") =>
        AccessRequest.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason, Now).Value;

    [Fact]
    public void A_request_starts_pending_with_a_trimmed_reason()
    {
        var request = Pending("  For the Q3 budget  ");

        request.Status.ShouldBe(AccessRequestStatus.Pending);
        request.Reason.ShouldBe("For the Q3 budget");
        request.DecidedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void An_over_long_reason_or_note_is_rejected()
    {
        AccessRequest.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('x', AccessRequest.ReasonMaxLength + 1), Now)
            .Error.ShouldBe(AccessErrors.RequestReasonTooLong);

        Pending().Reject(Guid.NewGuid(), new string('x', AccessRequest.NoteMaxLength + 1), Now)
            .Error.ShouldBe(AccessErrors.RequestNoteTooLong);
    }

    [Fact]
    public void Approving_records_the_reviewer_the_roles_and_the_note()
    {
        var request = Pending();
        var reviewer = Guid.NewGuid();

        request.Approve(reviewer, ["sales", "admin", "sales"], " Welcome ", Now.AddHours(1)).IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(AccessRequestStatus.Approved);
        request.DecidedByUserId.ShouldBe(reviewer);
        request.GrantedRoles.ShouldBe(["admin", "sales"]);
        request.DecisionNote.ShouldBe("Welcome");
        request.DecidedAtUtc.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Only_a_pending_request_can_be_decided_or_withdrawn()
    {
        var rejected = Pending();
        rejected.Reject(Guid.NewGuid(), null, Now).IsSuccess.ShouldBeTrue();

        rejected.Approve(Guid.NewGuid(), [], null, Now).Error.ShouldBe(AccessErrors.RequestNotPending);
        rejected.Cancel(Now).Error.ShouldBe(AccessErrors.RequestNotPending);

        var cancelled = Pending();
        cancelled.Cancel(Now).IsSuccess.ShouldBeTrue();
        cancelled.Status.ShouldBe(AccessRequestStatus.Cancelled);
        cancelled.DecidedByUserId.ShouldBeNull();
    }
}

public sealed class OrganizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_slug_is_lower_cased_and_validated()
    {
        Organization.Create(Guid.NewGuid(), "Acme", " ACME-Italia ", null, Now).Value.Slug.ShouldBe("acme-italia");

        Organization.Create(Guid.NewGuid(), "Acme", "", null, Now).Error.ShouldBe(AccessErrors.OrganizationSlugRequired);
        Organization.Create(Guid.NewGuid(), "Acme", "-acme", null, Now).Error.ShouldBe(AccessErrors.OrganizationSlugInvalid);
        Organization.Create(Guid.NewGuid(), "Acme", "acme corp", null, Now).Error.ShouldBe(AccessErrors.OrganizationSlugInvalid);
        Organization.Create(Guid.NewGuid(), "Acme", new string('a', Organization.SlugMaxLength + 1), null, Now)
            .Error.ShouldBe(AccessErrors.OrganizationSlugInvalid);
    }

    [Fact]
    public void A_new_organization_is_not_reported_as_updated()
    {
        var organization = Organization.Create(Guid.NewGuid(), "  Acme  ", "acme", "  ", Now).Value;

        organization.Name.ShouldBe("Acme");
        organization.Description.ShouldBeNull();
        organization.UpdatedAtUtc.ShouldBeNull();
        Organization.Create(Guid.NewGuid(), " ", "acme", null, Now).Error.ShouldBe(AccessErrors.OrganizationNameRequired);
    }

    [Fact]
    public void Setting_a_member_adds_changes_the_role_or_does_nothing()
    {
        var organization = Organization.Create(Guid.NewGuid(), "Acme", "acme", null, Now).Value;
        var userId = Guid.NewGuid();

        organization.SetMember(userId, OrganizationRole.Member, Now).ShouldBe(MembershipChange.Added);
        organization.SetMember(userId, OrganizationRole.Member, Now).ShouldBe(MembershipChange.None);
        organization.SetMember(userId, OrganizationRole.Admin, Now).ShouldBe(MembershipChange.RoleChanged);

        organization.Members.Single().Role.ShouldBe(OrganizationRole.Admin);
        organization.RemoveMember(userId).ShouldBeTrue();
        organization.RemoveMember(userId).ShouldBeFalse();
    }
}

public sealed class ApplicationSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Access_requests_and_organizations_are_off_by_default_and_changes_are_stamped()
    {
        var application = PortalApplication.CreateManual(
            Guid.NewGuid(),
            "crm",
            new ApplicationDetails("CRM", null, "https://crm.example.com", ["https://crm.example.com/signin-oidc"], []),
            Now).Value;

        application.AllowAccessRequests.ShouldBeFalse();
        application.Organizations.ShouldBe(OrganizationMode.None);

        application.SetAccessRequests(true, Now.AddHours(1));
        application.SetOrganizationMode(OrganizationMode.Required, Now.AddHours(2));

        application.AllowAccessRequests.ShouldBeTrue();
        application.Organizations.ShouldBe(OrganizationMode.Required);
        application.UpdatedAtUtc.ShouldBe(Now.AddHours(2));
    }
}
