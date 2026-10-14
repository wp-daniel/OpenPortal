using OpenPortal.Identity.Domain.Invitations;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Tests.Unit.Identity;

public sealed class RegistrationSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registration_is_off_until_an_administrator_opens_it()
    {
        var settings = RegistrationSettings.Defaults();

        settings.IsOpen.ShouldBeFalse();
        settings.RequireEmailConfirmation.ShouldBeTrue();
        settings.InvitationLifetimeDays.ShouldBe(RegistrationSettings.DefaultInvitationDays);
    }

    [Fact]
    public void Domains_are_normalised_de_duplicated_and_matched_exactly()
    {
        var settings = RegistrationSettings.Defaults();

        settings.Update(RegistrationMode.Open, [" Example.com ", "@example.com", "", "corp.example.org"], false, 7, Now).Value.ShouldBeTrue();

        settings.AllowedDomains.ShouldBe(["corp.example.org", "example.com"]);
        settings.AllowsEmail("ada@EXAMPLE.com").ShouldBeTrue();
        settings.AllowsEmail("ada@mail.example.com").ShouldBeFalse();
        settings.AllowsEmail("ada@elsewhere.org").ShouldBeFalse();
    }

    [Fact]
    public void Invalid_values_are_refused_and_an_unchanged_save_reports_nothing()
    {
        var settings = RegistrationSettings.Defaults();

        settings.Update(RegistrationMode.Open, ["not a domain"], false, 7, Now).Error.ShouldBe(UserErrors.RegistrationDomainInvalid);
        settings.Update(RegistrationMode.Open, [], false, 0, Now).Error.ShouldBe(UserErrors.InvitationLifetimeInvalid);
        settings.Update((RegistrationMode)42, [], false, 7, Now).Error.ShouldBe(UserErrors.RegistrationModeInvalid);

        settings.Update(RegistrationMode.Disabled, [], true, 7, Now).Value.ShouldBeFalse();
    }
}

public sealed class UserInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static UserInvitation Invitation() => UserInvitation.Create(
        Guid.NewGuid(),
        " ada@example.com ",
        "Ada",
        null,
        "Welcome",
        "HASH",
        Guid.NewGuid(),
        [Guid.Empty, Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("11111111-1111-1111-1111-111111111111")],
        null,
        Now,
        TimeSpan.FromDays(7)).Value;

    [Fact]
    public void An_invitation_is_pending_until_it_expires()
    {
        var invitation = Invitation();

        invitation.Email.ShouldBe("ada@example.com");
        invitation.NormalizedEmail.ShouldBe("ADA@EXAMPLE.COM");
        invitation.GroupIds.Count.ShouldBe(1);
        invitation.StatusAt(Now.AddDays(6)).ShouldBe(InvitationStatus.Pending);
        invitation.StatusAt(Now.AddDays(7)).ShouldBe(InvitationStatus.Expired);
    }

    [Fact]
    public void Accepting_works_once_and_not_after_expiry()
    {
        var expired = Invitation();
        expired.Accept(Guid.NewGuid(), Now.AddDays(8)).Error.ShouldBe(UserErrors.InvitationExpired);

        var invitation = Invitation();
        var userId = Guid.NewGuid();
        invitation.Accept(userId, Now.AddDays(1)).IsSuccess.ShouldBeTrue();

        invitation.AcceptedUserId.ShouldBe(userId);
        invitation.StatusAt(Now.AddDays(30)).ShouldBe(InvitationStatus.Accepted);
        invitation.Accept(Guid.NewGuid(), Now.AddDays(1)).Error.ShouldBe(UserErrors.InvitationInvalid);
        invitation.Revoke(Now.AddDays(1)).Error.ShouldBe(UserErrors.InvitationClosed);
    }

    [Fact]
    public void Renewing_issues_a_new_token_and_restarts_the_period_even_after_expiry()
    {
        var invitation = Invitation();

        invitation.Renew("OTHER", Now.AddDays(10), TimeSpan.FromDays(7)).IsSuccess.ShouldBeTrue();

        invitation.TokenHash.ShouldBe("OTHER");
        invitation.StatusAt(Now.AddDays(16)).ShouldBe(InvitationStatus.Pending);

        invitation.Revoke(Now.AddDays(11)).IsSuccess.ShouldBeTrue();
        invitation.Renew("AGAIN", Now.AddDays(12), TimeSpan.FromDays(7)).Error.ShouldBe(UserErrors.InvitationClosed);
    }
}

public sealed class AccountApprovalTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_registered_account_waits_for_approval_once()
    {
        var user = ApplicationUser.Create(
            Guid.NewGuid(),
            "ada@example.com",
            new UserDetails("Ada", "Lovelace", null, null, null, null, null, null, null, null),
            Now,
            AccountOrigin.Registered).Value;

        user.Origin.ShouldBe(AccountOrigin.Registered);
        user.PendingApproval.ShouldBeFalse();

        user.RequireApproval();
        user.Approve(Now).IsSuccess.ShouldBeTrue();
        user.PendingApproval.ShouldBeFalse();
        user.Approve(Now).Error.ShouldBe(UserErrors.RegistrationNotPending);
    }
}
