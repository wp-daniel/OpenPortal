using OpenPortal.Identity.Domain.Legal;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Identity.Domain.Tokens;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Tests.Unit.Identity;

public sealed class LegalDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static LegalDocumentContent Content(params (string Language, string Title, string Body)[] texts) =>
        new(true, texts.Select(text => new LegalDocumentTextContent(text.Language, text.Title, text.Body)).ToList());

    [Fact]
    public void A_draft_changes_until_published_and_never_after()
    {
        var draft = LegalDocument.CreateDraft(Guid.NewGuid(), LegalDocumentKind.Terms, 1, Content(("en", "Terms", "# Terms")), Now).Value;

        draft.UpdateDraft(Content(("en", "Terms", "# Terms v1")), Now).Value.ShouldBeTrue();
        draft.UpdateDraft(Content(("en", "Terms", "# Terms v1")), Now).Value.ShouldBeFalse();

        draft.Publish(Now).IsSuccess.ShouldBeTrue();
        draft.Status.ShouldBe(LegalDocumentStatus.Published);
        draft.UpdateDraft(Content(("en", "Terms", "changed")), Now).Error.ShouldBe(LegalErrors.NotDraft);
        draft.Publish(Now).Error.ShouldBe(LegalErrors.NotDraft);

        draft.Supersede();
        draft.Status.ShouldBe(LegalDocumentStatus.Superseded);
    }

    [Fact]
    public void Texts_are_validated_and_the_reader_language_falls_back()
    {
        LegalDocument.CreateDraft(Guid.NewGuid(), LegalDocumentKind.Privacy, 1, Content(), Now).Error.ShouldBe(LegalErrors.TextRequired);
        LegalDocument.CreateDraft(Guid.NewGuid(), LegalDocumentKind.Privacy, 1, Content(("en", "A", "x"), ("EN", "B", "y")), Now)
            .Error.ShouldBe(LegalErrors.LanguageInvalid);
        LegalDocument.CreateDraft(Guid.NewGuid(), LegalDocumentKind.Privacy, 1, Content(("en", "A", " ")), Now).Error.ShouldBe(LegalErrors.TextRequired);

        var document = LegalDocument.CreateDraft(Guid.NewGuid(), LegalDocumentKind.Privacy, 1, Content(("it", "Privacy IT", "x"), ("en", "Privacy EN", "y")), Now).Value;

        document.TextFor("it-CH", "en").Title.ShouldBe("Privacy IT");
        document.TextFor("fr", "en").Title.ShouldBe("Privacy EN");
        document.TextFor("fr", "de").Title.ShouldBe("Privacy IT");
    }
}

public sealed class AccountDeletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_deletion_is_scheduled_once_and_can_be_cancelled()
    {
        var user = ApplicationUser.Create(Guid.NewGuid(), "ada@example.com", new UserDetails("Ada", "Lovelace", null, null, null, null, null, null, null, null), Now).Value;

        user.CancelDeletion().ShouldBeFalse();

        user.RequestDeletion(Now, TimeSpan.FromDays(30));
        user.DeletionScheduledAtUtc.ShouldBe(Now.AddDays(30));

        user.RequestDeletion(Now.AddDays(5), TimeSpan.FromDays(30));
        user.DeletionScheduledAtUtc.ShouldBe(Now.AddDays(30));

        user.CancelDeletion().ShouldBeTrue();
        user.DeletionScheduledAtUtc.ShouldBeNull();
        user.DeletionRequestedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void The_grace_period_stays_within_a_year()
    {
        var settings = PrivacySettings.Defaults();

        settings.SelfDeletionEnabled.ShouldBeTrue();
        settings.DeletionGraceDays.ShouldBe(PrivacySettings.DefaultGraceDays);
        settings.Update(true, 400, Now).Error.ShouldBe(UserErrors.GraceDaysInvalid);
        settings.Update(true, -1, Now).Error.ShouldBe(UserErrors.GraceDaysInvalid);
        settings.Update(false, 0, Now).Value.ShouldBeTrue();
        settings.Update(false, 0, Now).Value.ShouldBeFalse();
    }
}

public sealed class PersonalAccessTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static PersonalAccessToken Create(string[] scopes, int days = 30) =>
        PersonalAccessToken.Create(Guid.NewGuid(), Guid.NewGuid(), "CLI", scopes, days, "opp_abcdefghijkl", new string('a', 64), Now).Value;

    [Fact]
    public void Write_implies_read_and_only_the_prefix_is_kept_in_clear()
    {
        var token = Create(["write"]);

        token.Scopes.ShouldBe(["read", "write"]);
        token.Allows(PersonalAccessTokenScopes.Read).ShouldBeTrue();
        token.DisplayPrefix.ShouldBe("opp_abcdefgh");
        Create(["read"]).Allows(PersonalAccessTokenScopes.Write).ShouldBeFalse();
    }

    [Fact]
    public void A_token_expires_can_be_revoked_and_records_its_use_sparingly()
    {
        var token = Create(["read"], days: 7);

        token.IsActiveAt(Now.AddDays(6)).ShouldBeTrue();
        token.IsActiveAt(Now.AddDays(7)).ShouldBeFalse();

        token.RecordUse(Now).ShouldBeTrue();
        token.RecordUse(Now.AddMinutes(1)).ShouldBeFalse();
        token.RecordUse(Now.AddMinutes(6)).ShouldBeTrue();

        token.Revoke(Now).ShouldBeTrue();
        token.Revoke(Now).ShouldBeFalse();
        token.IsActiveAt(Now).ShouldBeFalse();
    }

    [Fact]
    public void Invalid_tokens_are_refused_before_construction()
    {
        PersonalAccessToken.Create(Guid.NewGuid(), Guid.NewGuid(), " ", ["read"], 30, "opp_x", "h", Now).Error.ShouldBe(TokenErrors.NameInvalid);
        PersonalAccessToken.Create(Guid.NewGuid(), Guid.NewGuid(), "a", ["admin"], 30, "opp_x", "h", Now).Error.ShouldBe(TokenErrors.ScopesInvalid);
        PersonalAccessToken.Create(Guid.NewGuid(), Guid.NewGuid(), "a", [], 30, "opp_x", "h", Now).Error.ShouldBe(TokenErrors.ScopesInvalid);
        PersonalAccessToken.Create(Guid.NewGuid(), Guid.NewGuid(), "a", ["read"], 366, "opp_x", "h", Now).Error.ShouldBe(TokenErrors.LifetimeInvalid);
    }
}
