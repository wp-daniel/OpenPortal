using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityExternalLoginService : IExternalLoginService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IdentityDbContext _dbContext;
    private readonly IExternalLoginProviders _providers;
    private readonly UserLookup _userLookup;
    private readonly AdministrationGuard _guard;
    private readonly IdentityTwoFactorPolicy _policy;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    public IdentityExternalLoginService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IdentityDbContext dbContext,
        IExternalLoginProviders providers,
        UserLookup userLookup,
        AdministrationGuard guard,
        IdentityTwoFactorPolicy policy,
        IAuditTrail audit,
        INotifier notifier,
        IClock clock)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _dbContext = dbContext;
        _providers = providers;
        _userLookup = userLookup;
        _guard = guard;
        _policy = policy;
        _audit = audit;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<Result<string>> SignInAsync(CancellationToken cancellationToken)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync().ConfigureAwait(false);
        if (info is null)
        {
            await RecordSignInAsync(Anonymous(null), SignInFailureReasons.ExternalFailed, null, cancellationToken).ConfigureAwait(false);
            return Result<string>.Failure(UserErrors.ExternalSignInFailed);
        }

        var outcome = await SignInWithAsync(info, cancellationToken).ConfigureAwait(false);

        // Whatever happened, the external identity has served its purpose; only a pending second step still needs it.
        if (outcome.IsFailure || outcome.Value != ExternalSignInOutcomes.TwoFactorRequired)
        {
            await _signInManager.Context.SignOutAsync(IdentityConstants.ExternalScheme).ConfigureAwait(false);
        }

        return outcome;
    }

    private async Task<Result<string>> SignInWithAsync(ExternalLoginInfo info, CancellationToken cancellationToken)
    {
        var provider = info.LoginProvider;
        var method = SignInMethods.ExternalPrefix + provider;
        var email = Normalise(info.Principal.FindFirstValue(ExternalLoginClaimTypes.Email));
        var verified = string.Equals(info.Principal.FindFirstValue(ExternalLoginClaimTypes.EmailVerified), "true", StringComparison.Ordinal);

        if (_providers.Find(provider) is null)
        {
            await RecordSignInAsync(Anonymous(email), SignInFailureReasons.ExternalFailed, method, cancellationToken).ConfigureAwait(false);
            return Result<string>.Failure(UserErrors.ExternalProviderDisabled);
        }

        var user = await _userManager.FindByLoginAsync(provider, info.ProviderKey).ConfigureAwait(false);

        if (user is null)
        {
            if (email is null)
            {
                await RecordSignInAsync(Anonymous(null), SignInFailureReasons.ExternalNotLinked, method, cancellationToken).ConfigureAwait(false);
                return Result<string>.Failure(UserErrors.ExternalEmailMissing);
            }

            user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);

            if (user is not null)
            {
                // An address the provider does not vouch for proves nothing: anyone can put any address on an account
                // of their own at some providers. Such an account is connected by its owner, signed in, instead.
                if (!verified)
                {
                    await RecordSignInAsync(user.ToAuditSubject(), SignInFailureReasons.ExternalNotLinked, method, cancellationToken).ConfigureAwait(false);
                    return Result<string>.Failure(UserErrors.ExternalEmailUnverified);
                }

                var linked = await AddLoginAsync(user, info, email, "verified_email", cancellationToken).ConfigureAwait(false);
                if (linked.IsFailure)
                {
                    return Result<string>.Failure(linked.Error);
                }

                // The provider vouched for the address, which is what confirming it by email would have shown.
                if (!user.EmailConfirmed)
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false);
                    await _userManager.ConfirmEmailAsync(user, token).ConfigureAwait(false);
                }
            }
            else
            {
                var created = await CreateAccountAsync(info, email, verified, cancellationToken).ConfigureAwait(false);
                if (created.IsFailure)
                {
                    return Result<string>.Failure(created.Error);
                }

                user = created.Value;

                if (user.PendingApproval)
                {
                    return Result<string>.Success(ExternalSignInOutcomes.ApprovalPending);
                }
            }
        }

        // The same rule as a password sign-in: an account that must use its passkey cannot skip it this way either.
        if (await _policy.RequiresPasskeySignInAsync(user, cancellationToken).ConfigureAwait(false))
        {
            await RecordSignInAsync(user.ToAuditSubject(), SignInFailureReasons.PasskeyRequired, method, cancellationToken).ConfigureAwait(false);
            return Result<string>.Failure(UserErrors.PasskeyRequired);
        }

        var remember = info.AuthenticationProperties?.Items.TryGetValue(ExternalLoginProperties.RememberMe, out var value) == true
            && string.Equals(value, "true", StringComparison.Ordinal);

        // Approval, confirmation and lockout are checked as for a password; an account with an authenticator app is
        // asked for its code next (the provider's own second factor is not visible to the portal).
        var signIn = await _signInManager
            .ExternalLoginSignInAsync(provider, info.ProviderKey, isPersistent: remember, bypassTwoFactor: false)
            .ConfigureAwait(false);

        if (signIn.RequiresTwoFactor)
        {
            return Result<string>.Success(ExternalSignInOutcomes.TwoFactorRequired);
        }

        if (!signIn.Succeeded)
        {
            var (reason, error) = signIn.IsLockedOut
                ? (SignInFailureReasons.LockedOut, UserErrors.AccountLockedOut)
                : user.PendingApproval
                    ? (SignInFailureReasons.PendingApproval, UserErrors.AccountPendingApproval)
                    : (SignInFailureReasons.NotAllowed, UserErrors.AccountNotAllowed);

            await RecordSignInAsync(user.ToAuditSubject(), reason, method, cancellationToken).ConfigureAwait(false);

            return Result<string>.Failure(error);
        }

        user.RecordSignIn(_clock.UtcNow);
        await _userManager.UpdateAsync(user).ConfigureAwait(false);
        await _userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        await RecordSignInAsync(user.ToAuditSubject(), null, method, cancellationToken).ConfigureAwait(false);

        return Result<string>.Success(ExternalSignInOutcomes.SignedIn);
    }

    private async Task<Result<ApplicationUser>> CreateAccountAsync(
        ExternalLoginInfo info,
        string email,
        bool verified,
        CancellationToken cancellationToken)
    {
        var provider = info.LoginProvider;
        var settings = await _dbContext.ExternalLoginProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Key == provider, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null || settings.SignUp == ExternalSignUpMode.Disabled)
        {
            await RecordRegistrationRefusalAsync(email, provider, "sign_up_closed", cancellationToken).ConfigureAwait(false);
            return Result<ApplicationUser>.Failure(UserErrors.ExternalSignUpClosed);
        }

        // A new account takes the address as its sign-in identifier, so it must be one the person controls.
        if (!verified)
        {
            await RecordRegistrationRefusalAsync(email, provider, "email_unverified", cancellationToken).ConfigureAwait(false);
            return Result<ApplicationUser>.Failure(UserErrors.ExternalEmailNotVerified);
        }

        if (!settings.AllowsEmail(email))
        {
            await RecordRegistrationRefusalAsync(email, provider, "domain_not_allowed", cancellationToken).ConfigureAwait(false);
            return Result<ApplicationUser>.Failure(UserErrors.ExternalDomainNotAllowed);
        }

        var (first, last) = NamesOf(info.Principal, email);
        var account = ApplicationUser.Create(
            Guid.NewGuid(),
            email,
            new UserDetails(first, last, null, null, null, null, null, null, null, null),
            _clock.UtcNow,
            AccountOrigin.External);

        if (account.IsFailure)
        {
            return Result<ApplicationUser>.Failure(account.Error);
        }

        var user = account.Value;
        user.EmailConfirmed = true;

        var needsApproval = settings.SignUp == ExternalSignUpMode.Approval;
        if (needsApproval)
        {
            user.RequireApproval();
        }

        // No password: the account signs in through the provider until its owner sets one.
        var created = await _userManager.CreateAsync(user).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return Result<ApplicationUser>.Failure(created.ToError(UserErrors.SaveFailed));
        }

        var role = await _userManager.AddToRoleAsync(user, Roles.User).ConfigureAwait(false);
        var login = role.Succeeded
            ? await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, info.ProviderKey, email)).ConfigureAwait(false)
            : role;

        if (!login.Succeeded)
        {
            await _userManager.DeleteAsync(user).ConfigureAwait(false);
            return Result<ApplicationUser>.Failure(login.ToError(UserErrors.RoleAssignmentFailed));
        }

        await _audit.RecordAsync(
                new AuditEvent
                {
                    Action = IdentityAuditActions.Registered,
                    Actor = user.ToAuditSubject(),
                    Target = user.ToAuditSubject(),
                    Details = new Dictionary<string, string?>
                    {
                        ["provider"] = provider,
                        ["approval"] = needsApproval ? "required" : "none",
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = needsApproval ? IdentityNotificationTypes.RegistrationPending : IdentityNotificationTypes.UserRegistered,
                    Audience = NoticeAudience.Holders(IdentityNotificationRoles.UserReviewers),
                    Parameters = new Dictionary<string, string?> { ["user"] = user.DisplayName, ["email"] = user.Email },
                    Link = needsApproval ? "/admin/users?status=pending" : "/admin/users",
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Result<ApplicationUser>.Success(user);
    }

    public async Task<Result> LinkAsync(Guid expectedUserId, CancellationToken cancellationToken)
    {
        try
        {
            var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
            if (lookup.IsFailure || lookup.Value.Id != expectedUserId)
            {
                return Result.Failure(UserErrors.ExternalSignInFailed);
            }

            // The expected id was written into the challenge by the account that started it, so a connection started
            // by someone else (a forged link) cannot attach their external account to this one.
            var info = await _signInManager.GetExternalLoginInfoAsync(expectedUserId.ToString()).ConfigureAwait(false);
            if (info is null)
            {
                return Result.Failure(UserErrors.ExternalSignInFailed);
            }

            if (_providers.Find(info.LoginProvider) is null)
            {
                return Result.Failure(UserErrors.ExternalProviderDisabled);
            }

            var user = lookup.Value;
            var owner = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey).ConfigureAwait(false);
            if (owner is not null)
            {
                return owner.Id == user.Id ? Result.Success() : Result.Failure(UserErrors.ExternalLoginInUse);
            }

            var email = Normalise(info.Principal.FindFirstValue(ExternalLoginClaimTypes.Email));
            var added = await AddLoginAsync(user, info, email, "owner", cancellationToken).ConfigureAwait(false);
            if (added.IsSuccess)
            {
                // Adding a login rotates the security stamp; this session is the one that asked for it.
                await _signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
            }

            return added;
        }
        finally
        {
            await _signInManager.Context.SignOutAsync(IdentityConstants.ExternalScheme).ConfigureAwait(false);
        }
    }

    public async Task<Result<AccountExternalLoginsDto>> ListOwnAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<AccountExternalLoginsDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;
        var logins = ToDtos(await _userManager.GetLoginsAsync(user).ConfigureAwait(false));
        var available = ExternalProviderInfo.From(_providers)
            .Where(provider => logins.All(login => login.Provider != provider.Key))
            .ToArray();

        return Result<AccountExternalLoginsDto>.Success(new AccountExternalLoginsDto(
            await _userManager.HasPasswordAsync(user).ConfigureAwait(false),
            logins,
            available));
    }

    public async Task<Result> RemoveOwnAsync(string provider, CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var user = lookup.Value;
        var logins = await _userManager.GetLoginsAsync(user).ConfigureAwait(false);
        var removing = logins.Where(login => login.LoginProvider == provider).ToList();
        if (removing.Count == 0)
        {
            return Result.Failure(UserErrors.ExternalLoginNotFound);
        }

        var otherWays = logins.Count > removing.Count
            || await _userManager.HasPasswordAsync(user).ConfigureAwait(false)
            || (await _userManager.GetPasskeysAsync(user).ConfigureAwait(false)).Count > 0;
        if (!otherWays)
        {
            return Result.Failure(UserErrors.ExternalLoginLastMethod);
        }

        var removed = await RemoveLoginsAsync(user, removing).ConfigureAwait(false);
        if (removed.IsFailure)
        {
            return removed;
        }

        await _signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
        await _audit.RecordAsync(
                AuditEvent.Succeeded(IdentityAuditActions.ExternalLoginRemoved, details: new Dictionary<string, string?> { ["provider"] = provider }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ExternalLoginDto>>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<ExternalLoginDto>>.Failure(guard.Error);
        }

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<IReadOnlyList<ExternalLoginDto>>.Failure(lookup.Error);
        }

        return Result<IReadOnlyList<ExternalLoginDto>>.Success(ToDtos(await _userManager.GetLoginsAsync(lookup.Value).ConfigureAwait(false)));
    }

    public async Task<Result> RemoveForUserAsync(Guid userId, string provider, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var user = lookup.Value;
        var changeable = await _guard.EnsureMayChangeAsync(user).ConfigureAwait(false);
        if (changeable.IsFailure)
        {
            return changeable;
        }

        var removing = (await _userManager.GetLoginsAsync(user).ConfigureAwait(false))
            .Where(login => login.LoginProvider == provider)
            .ToList();
        if (removing.Count == 0)
        {
            return Result.Failure(UserErrors.ExternalLoginNotFound);
        }

        // Unlike the owner, an administrator may take away the last way in (a compromised external account): the
        // owner then gets back in with a password reset. Removing a login also ends the sessions it opened.
        var removed = await RemoveLoginsAsync(user, removing).ConfigureAwait(false);
        if (removed.IsFailure)
        {
            return removed;
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.UserExternalLoginRemoved,
                    user.ToAuditSubject(),
                    new Dictionary<string, string?> { ["provider"] = provider }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> AddLoginAsync(
        ApplicationUser user,
        ExternalLoginInfo info,
        string? accountName,
        string how,
        CancellationToken cancellationToken)
    {
        var added = await _userManager
            .AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, accountName))
            .ConfigureAwait(false);

        if (!added.Succeeded)
        {
            return Result.Failure(added.ToError(UserErrors.SaveFailed));
        }

        await _audit.RecordAsync(
                new AuditEvent
                {
                    Action = IdentityAuditActions.ExternalLoginAdded,
                    Actor = user.ToAuditSubject(),
                    Target = user.ToAuditSubject(),
                    Details = new Dictionary<string, string?>
                    {
                        ["provider"] = info.LoginProvider,
                        ["account"] = accountName,
                        ["via"] = how,
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = IdentityNotificationTypes.ExternalLoginAdded,
                    Audience = NoticeAudience.Users(user.Id),
                    Parameters = new Dictionary<string, string?>
                    {
                        ["provider"] = ExternalLoginProviders.DisplayName(info.LoginProvider),
                        ["account"] = accountName,
                    },
                    Link = "/account?tab=security",
                    IncludeCaller = true,
                    Security = true,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> RemoveLoginsAsync(ApplicationUser user, IEnumerable<UserLoginInfo> logins)
    {
        foreach (var login in logins)
        {
            var removed = await _userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey).ConfigureAwait(false);
            if (!removed.Succeeded)
            {
                return Result.Failure(removed.ToError(UserErrors.SaveFailed));
            }
        }

        return Result.Success();
    }

    private static IReadOnlyList<ExternalLoginDto> ToDtos(IEnumerable<UserLoginInfo> logins) =>
        logins
            .Where(login => ExternalLoginProviders.IsKnown(login.LoginProvider))
            .OrderBy(login => login.LoginProvider, StringComparer.Ordinal)
            .Select(login => new ExternalLoginDto(
                login.LoginProvider,
                ExternalLoginProviders.DisplayName(login.LoginProvider),
                login.ProviderDisplayName))
            .ToArray();

    /// <summary>
    /// First and last name for a new account: the provider's own when it shares them, else split from the full name,
    /// else from the address (<c>ada.lovelace@…</c>). Both are required, so a single word fills both.
    /// </summary>
    internal static (string First, string Last) NamesOf(ClaimsPrincipal principal, string email)
    {
        var given = Clip(principal.FindFirstValue(ExternalLoginClaimTypes.GivenName));
        var family = Clip(principal.FindFirstValue(ExternalLoginClaimTypes.FamilyName));
        if (given is not null && family is not null)
        {
            return (given, family);
        }

        var full = principal.FindFirstValue(ExternalLoginClaimTypes.Name)?.Trim();
        var words = string.IsNullOrEmpty(full) || full.Contains('@', StringComparison.Ordinal)
            ? email[..Math.Max(0, email.IndexOf('@', StringComparison.Ordinal))]
                .Split(['.', '_', '-', '+'], StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..])
                .ToArray()
            : full.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var first = given ?? Clip(words.FirstOrDefault()) ?? "-";
        var last = family ?? Clip(words.Length > 1 ? string.Join(' ', words[1..]) : words.FirstOrDefault()) ?? first;

        return (first, last);
    }

    private static string? Clip(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > ApplicationUser.NameMaxLength ? trimmed[..ApplicationUser.NameMaxLength] : trimmed;
    }

    private static string? Normalise(string? email)
    {
        var trimmed = email?.Trim();
        return string.IsNullOrEmpty(trimmed) || !trimmed.Contains('@', StringComparison.Ordinal) ? null : trimmed;
    }

    private static AuditSubject Anonymous(string? email) => new(AuditSubjectTypes.User, string.Empty, email);

    private Task RecordSignInAsync(AuditSubject actor, string? failureReason, string? method, CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, string?>();
        if (failureReason is not null)
        {
            details["reason"] = failureReason;
        }

        if (method is not null)
        {
            details["method"] = method;
        }

        IdentityMetrics.SignIns.Add(
            1,
            new KeyValuePair<string, object?>("outcome", failureReason is null ? "success" : "failure"),
            new KeyValuePair<string, object?>("method", method ?? "external"),
            new KeyValuePair<string, object?>("reason", failureReason ?? "none"));

        return _audit.RecordAsync(
            new AuditEvent
            {
                Action = IdentityAuditActions.SignIn,
                Outcome = failureReason is null ? AuditOutcome.Success : AuditOutcome.Failure,
                Actor = actor,
                Details = details.Count == 0 ? null : details,
            },
            cancellationToken);
    }

    private Task RecordRegistrationRefusalAsync(string email, string provider, string reason, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            new AuditEvent
            {
                Action = IdentityAuditActions.Registered,
                Outcome = AuditOutcome.Failure,
                Actor = Anonymous(email),
                Details = new Dictionary<string, string?> { ["reason"] = reason, ["provider"] = provider },
            },
            cancellationToken);
}
