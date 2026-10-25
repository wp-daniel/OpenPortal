using Microsoft.AspNetCore.Identity;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityAccountService : IAccountService
{
    /// <summary>How recent a sign-in must be to change the address of an account that has no password.</summary>
    private static readonly TimeSpan RecentSignIn = TimeSpan.FromMinutes(15);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserLookup _userLookup;
    private readonly IClock _clock;
    private readonly ILanguageCatalog _languages;
    private readonly IAuditTrail _audit;
    private readonly IAccountMessaging _messaging;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserSessions _sessions;
    private readonly ICurrentUser _currentUser;

    public IdentityAccountService(
        UserManager<ApplicationUser> userManager,
        UserLookup userLookup,
        IClock clock,
        ILanguageCatalog languages,
        IAuditTrail audit,
        IAccountMessaging messaging,
        SignInManager<ApplicationUser> signInManager,
        UserSessions sessions,
        ICurrentUser currentUser)
    {
        _sessions = sessions;
        _currentUser = currentUser;
        _signInManager = signInManager;
        _messaging = messaging;
        _audit = audit;
        _languages = languages;
        _userManager = userManager;
        _userLookup = userLookup;
        _clock = clock;
    }

    public async Task<Result<AccountProfileDto>> GetProfileAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

        return lookup.IsFailure
            ? Result<AccountProfileDto>.Failure(lookup.Error)
            : await ToProfileAsync(lookup.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AccountProfileDto>> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<AccountProfileDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;
        var rename = user.UpdateDetails(request.ToDetails(), _clock.UtcNow);

        if (rename.IsFailure)
        {
            return Result<AccountProfileDto>.Failure(rename.Error);
        }

        var persisted = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!persisted.Succeeded)
        {
            return Result<AccountProfileDto>.Failure(persisted.ToError(UserErrors.SaveFailed));
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.ProfileUpdated), cancellationToken).ConfigureAwait(false);

        return await ToProfileAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AccountProfileDto>> UpdateLanguageAsync(
        UpdateLanguageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<AccountProfileDto>.Failure(lookup.Error);
        }

        var requested = TextRules.Normalise(request.Language);
        if (requested is not null && !_languages.IsSupported(requested))
        {
            return Result<AccountProfileDto>.Failure(UserErrors.UnsupportedLanguage);
        }

        var user = lookup.Value;
        var applied = user.SetLanguage(requested is null ? null : _languages.Canonicalize(requested), _clock.UtcNow);
        if (applied.IsFailure)
        {
            return Result<AccountProfileDto>.Failure(applied.Error);
        }

        var persisted = await _userManager.UpdateAsync(user).ConfigureAwait(false);

        return persisted.Succeeded
            ? await ToProfileAsync(user, cancellationToken).ConfigureAwait(false)
            : Result<AccountProfileDto>.Failure(persisted.ToError(UserErrors.SaveFailed));
    }

    public async Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        // ChangePasswordAsync verifies the current password and rotates the security stamp, which signs
        // the account out everywhere else.
        var changed = await _userManager
            .ChangePasswordAsync(lookup.Value, request.CurrentPassword, request.NewPassword)
            .ConfigureAwait(false);

        var result = changed.Succeeded
            ? Result.Success()
            : Result.Failure(changed.ToError(UserErrors.CurrentPasswordIncorrect));

        // A failure is recorded too: repeated wrong current passwords from a live session can mean someone
        // else is at the keyboard.
        await _audit.RecordAsync(
                result.IsSuccess
                    ? AuditEvent.Succeeded(IdentityAuditActions.PasswordChanged)
                    : AuditEvent.Failed(
                        IdentityAuditActions.PasswordChanged,
                        details: new Dictionary<string, string?> { ["reason"] = result.Error.Code }),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            // The new stamp ends every other session; this one is re-issued so its owner stays signed in. The others are
            // ended now too, rather than at their next stamp check.
            await _sessions.EndAllAsync(lookup.Value.Id, _currentUser.SessionId, SessionEndReason.Revoked, cancellationToken).ConfigureAwait(false);
            await _signInManager.RefreshSignInAsync(lookup.Value).ConfigureAwait(false);

            await PasswordChangeNotice.SendAsync(_messaging, lookup.Value, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result> SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var user = lookup.Value;
        if (await _userManager.HasPasswordAsync(user).ConfigureAwait(false))
        {
            return Result.Failure(UserErrors.PasswordAlreadySet);
        }

        // Validated against the password policy like any other password; adding one rotates the security stamp.
        var added = await _userManager.AddPasswordAsync(user, request.NewPassword).ConfigureAwait(false);
        if (!added.Succeeded)
        {
            return Result.Failure(added.ToError(UserErrors.SaveFailed));
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.PasswordSet), cancellationToken).ConfigureAwait(false);
        await _signInManager.RefreshSignInAsync(user).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<AccountProfileDto>> RequestEmailChangeAsync(ChangeEmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<AccountProfileDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;
        var email = request.NewEmail.Trim();

        // Without delivery the new address cannot be proven; an administrator can still change it.
        if (!await _messaging.CanSendEmailAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result<AccountProfileDto>.Failure(UserErrors.EmailChangeUnavailable);
        }

        if (string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AccountProfileDto>.Failure(UserErrors.EmailUnchanged);
        }

        // Whoever found an open session must not be able to move the account to an address of theirs (and then reset
        // its password there): the password, or for an account without one a sign-in of the last minutes, proves the owner.
        if (await _userManager.HasPasswordAsync(user).ConfigureAwait(false))
        {
            var check = string.IsNullOrEmpty(request.CurrentPassword)
                ? SignInResult.Failed
                : await _signInManager.CheckPasswordSignInAsync(user, request.CurrentPassword, lockoutOnFailure: true).ConfigureAwait(false);

            if (!check.Succeeded)
            {
                await _audit.RecordAsync(
                        AuditEvent.Failed(
                            IdentityAuditActions.EmailChangeRequested,
                            details: new Dictionary<string, string?> { ["reason"] = "wrong_password" }),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Result<AccountProfileDto>.Failure(check.IsLockedOut ? UserErrors.AccountLockedOut : UserErrors.CurrentPasswordIncorrect);
            }
        }
        else if (_currentUser.AuthenticatedAtUtc is not { } authenticatedAt || _clock.UtcNow - authenticatedAt > RecentSignIn)
        {
            return Result<AccountProfileDto>.Failure(UserErrors.RecentSignInRequired);
        }

        user.RequestEmailChange(email, _clock.UtcNow);
        var persisted = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!persisted.Succeeded)
        {
            return Result<AccountProfileDto>.Failure(persisted.ToError(UserErrors.SaveFailed));
        }

        // The answer does not say whether the address belongs to someone: its owner is told instead, and the change
        // simply never completes.
        var owner = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (owner is not null && owner.Id != user.Id)
        {
            await _messaging.SendAsync(
                    new AccountMessage(
                        AccountMessageKinds.EmailChangeConflict,
                        owner.Email!,
                        owner.DisplayName,
                        owner.Language,
                        "/account?tab=security",
                        new Dictionary<string, string?> { ["name"] = owner.FirstName, ["email"] = owner.Email }),
                    cancellationToken)
                .ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Failed(
                        IdentityAuditActions.EmailChangeRequested,
                        details: new Dictionary<string, string?> { ["email"] = email, ["reason"] = "address_in_use" }),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // The token is bound to this address and to the security stamp: a newer request (another address) or a
            // password change makes the link useless.
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, email).ConfigureAwait(false);

            await _messaging.SendAsync(
                    new AccountMessage(
                        AccountMessageKinds.EmailChange,
                        email,
                        user.DisplayName,
                        user.Language,
                        $"/confirm-email-change?user={user.Id}&token={Uri.EscapeDataString(token)}",
                        new Dictionary<string, string?> { ["name"] = user.FirstName, ["email"] = email }),
                    cancellationToken)
                .ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        IdentityAuditActions.EmailChangeRequested,
                        details: new Dictionary<string, string?> { ["email"] = email }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await ToProfileAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AccountProfileDto>> CancelEmailChangeAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<AccountProfileDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;
        if (user.CancelEmailChange())
        {
            var persisted = await _userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!persisted.Succeeded)
            {
                return Result<AccountProfileDto>.Failure(persisted.ToError(UserErrors.SaveFailed));
            }
        }

        return await ToProfileAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<EmailChangeConfirmedDto>> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        if (user?.PendingEmail is not { } email)
        {
            return Result<EmailChangeConfirmedDto>.Failure(UserErrors.EmailChangeInvalid);
        }

        var valid = await _userManager
            .VerifyUserTokenAsync(
                user,
                _userManager.Options.Tokens.ChangeEmailTokenProvider,
                UserManager<ApplicationUser>.GetChangeEmailTokenPurpose(email),
                request.Token)
            .ConfigureAwait(false);

        if (!valid)
        {
            return Result<EmailChangeConfirmedDto>.Failure(UserErrors.EmailChangeInvalid);
        }

        // Someone may have taken the address since the link was sent.
        var owner = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (owner is not null && owner.Id != user.Id)
        {
            return Result<EmailChangeConfirmedDto>.Failure(UserErrors.DuplicateEmail);
        }

        var previous = user.Email!;
        user.ChangeEmail(email, _clock.UtcNow);

        // The address is also the user name. Rotating the stamp saves both (validated as unique) and ends the sessions
        // that knew the account by its old address.
        await _userManager.UpdateNormalizedEmailAsync(user).ConfigureAwait(false);
        await _userManager.UpdateNormalizedUserNameAsync(user).ConfigureAwait(false);
        var saved = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!saved.Succeeded)
        {
            return Result<EmailChangeConfirmedDto>.Failure(saved.ToError(UserErrors.SaveFailed));
        }

        // Opened in the account's own session: that one goes on, the others end now.
        var signedIn = _currentUser.UserId == user.Id && _currentUser.ImpersonatorId is null;
        await _sessions.EndAllAsync(user.Id, signedIn ? _currentUser.SessionId : null, SessionEndReason.Revoked, cancellationToken).ConfigureAwait(false);
        if (signedIn)
        {
            await _signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
        }

        await _audit.RecordAsync(
                new AuditEvent
                {
                    Action = IdentityAuditActions.EmailChanged,

                    // The link may be opened signed out, so the account is named explicitly, as actor and target.
                    Actor = user.ToAuditSubject(),
                    Target = user.ToAuditSubject(),
                    Details = new Dictionary<string, string?> { ["from"] = previous, ["to"] = email },
                },
                cancellationToken)
            .ConfigureAwait(false);

        // The previous address hears of it, so a change its owner did not make stands out.
        await _messaging.SendAsync(
                new AccountMessage(
                    AccountMessageKinds.EmailChanged,
                    previous,
                    user.DisplayName,
                    user.Language,
                    "/account?tab=security",
                    new Dictionary<string, string?> { ["name"] = user.FirstName, ["email"] = email }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<EmailChangeConfirmedDto>.Success(new EmailChangeConfirmedDto(email, signedIn));
    }

    private async Task<Result<AccountProfileDto>> ToProfileAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

        return Result<AccountProfileDto>.Success(new AccountProfileDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            DisplayName: user.DisplayName,
            EmailConfirmed: user.EmailConfirmed,
            CreatedAtUtc: user.CreatedAtUtc,
            UpdatedAtUtc: user.UpdatedAtUtc,
            Roles: roles.Order(StringComparer.Ordinal).ToArray(),
            Language: user.Language,
            FirstName: user.FirstName,
            LastName: user.LastName,
            PhoneNumber: user.PhoneNumber,
            JobTitle: user.JobTitle,
            Company: user.Company,
            Department: user.Department,
            AddressLine: user.AddressLine,
            City: user.City,
            PostalCode: user.PostalCode,
            Country: user.Country,
            AvatarUpdatedAtUtc: user.AvatarUpdatedAtUtc,
            PendingEmail: user.PendingEmail));
    }
}