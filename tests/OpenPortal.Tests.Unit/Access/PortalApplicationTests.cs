using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;

namespace OpenPortal.Tests.Unit.Access;

public sealed class PortalApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static ApplicationDetails Details(params string[] redirectUris) => new(
        "  CRM  ",
        null,
        "https://crm.example.com",
        redirectUris.Length == 0 ? ["https://crm.example.com/signin-oidc"] : redirectUris,
        []);

    [Fact]
    public void A_manual_application_is_active_and_normalised()
    {
        var created = PortalApplication.CreateManual(Guid.NewGuid(), "  My-CRM ", Details(), Now);

        created.IsSuccess.ShouldBeTrue();
        created.Value.ClientId.ShouldBe("my-crm");
        created.Value.DisplayName.ShouldBe("CRM");
        created.Value.Status.ShouldBe(ApplicationStatus.Active);
        created.Value.Source.ShouldBe(ApplicationSource.Manual);
    }

    [Theory]
    [InlineData("http://crm.example.com/signin-oidc")]
    [InlineData("https://crm.example.com/signin-oidc#fragment")]
    [InlineData("/signin-oidc")]
    [InlineData("ftp://crm.example.com/signin-oidc")]
    public void An_unsafe_redirect_uri_is_rejected(string uri)
    {
        var created = PortalApplication.CreateManual(Guid.NewGuid(), "crm", Details(uri), Now);

        created.Error.ShouldBe(AccessErrors.RedirectUriInvalid);
    }

    [Theory]
    [InlineData("http://localhost:5001/signin-oidc")]
    [InlineData("http://127.0.0.1:5001/signin-oidc")]
    public void Plain_http_is_allowed_for_loopback_only(string uri)
    {
        PortalApplication.CreateManual(Guid.NewGuid(), "crm", Details(uri), Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void At_least_one_redirect_uri_is_required()
    {
        var created = PortalApplication.CreateManual(
            Guid.NewGuid(),
            "crm",
            new ApplicationDetails("CRM", null, "https://crm.example.com", [" "], []),
            Now);

        created.Error.ShouldBe(AccessErrors.RedirectUriRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("has space")]
    [InlineData("dots.not.allowed")]
    public void An_invalid_client_id_is_rejected(string? clientId)
    {
        PortalApplication.CreateManual(Guid.NewGuid(), clientId, Details(), Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Approval_moves_a_pending_application_to_active_once()
    {
        var application = PortalApplication.CreateAnnounced(Guid.NewGuid(), "crm", Details(), "1.0", Now).Value;
        application.Status.ShouldBe(ApplicationStatus.Pending);
        application.LastSeenAtUtc.ShouldBe(Now);

        application.Approve(Now).IsSuccess.ShouldBeTrue();
        application.Status.ShouldBe(ApplicationStatus.Active);

        application.Approve(Now).Error.ShouldBe(AccessErrors.ApplicationNotPending);
    }

    [Fact]
    public void Disable_and_enable_only_from_the_matching_state()
    {
        var application = PortalApplication.CreateManual(Guid.NewGuid(), "crm", Details(), Now).Value;

        application.Enable(Now).Error.ShouldBe(AccessErrors.ApplicationNotDisabled);
        application.Disable(Now).IsSuccess.ShouldBeTrue();
        application.Disable(Now).Error.ShouldBe(AccessErrors.ApplicationNotActive);
        application.Enable(Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_pending_application_takes_announced_redirect_uris_as_they_are()
    {
        var application = PortalApplication.CreateAnnounced(Guid.NewGuid(), "crm", Details(), "1.0", Now).Value;

        application.RecordAnnouncement(Details("https://crm.example.com/new-callback"), "1.1", Now.AddMinutes(5)).IsSuccess.ShouldBeTrue();

        application.RedirectUris.ShouldBe(["https://crm.example.com/new-callback"]);
        application.Version.ShouldBe("1.1");
        application.LastSeenAtUtc.ShouldBe(Now.AddMinutes(5));
        application.HasManifestChanges.ShouldBeFalse();
    }

    [Fact]
    public void An_approved_application_only_proposes_new_redirect_uris_until_an_administrator_applies_them()
    {
        var application = PortalApplication.CreateAnnounced(Guid.NewGuid(), "crm", Details(), "1.0", Now).Value;
        application.Approve(Now);

        application.RecordAnnouncement(Details("https://evil.example.com/steal"), "1.1", Now).IsSuccess.ShouldBeTrue();

        application.RedirectUris.ShouldBe(["https://crm.example.com/signin-oidc"]);
        application.AnnouncedRedirectUris.ShouldBe(["https://evil.example.com/steal"]);
        application.HasManifestChanges.ShouldBeTrue();

        application.ApplyAnnouncedManifest(Now).IsSuccess.ShouldBeTrue();
        application.RedirectUris.ShouldBe(["https://evil.example.com/steal"]);
        application.HasManifestChanges.ShouldBeFalse();
        application.ApplyAnnouncedManifest(Now).Error.ShouldBe(AccessErrors.NoManifestChanges);
    }
}
