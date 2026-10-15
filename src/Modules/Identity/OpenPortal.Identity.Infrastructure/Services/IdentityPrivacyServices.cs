using System.Globalization;
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

/// <summary>Reads the privacy settings row, falling back to the defaults while none has been saved.</summary>
internal sealed class PrivacySettingsStore
{
    private readonly IdentityDbContext _dbContext;

    public PrivacySettingsStore(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PrivacySettings> ReadAsync(CancellationToken cancellationToken) =>
        await _dbContext.PrivacySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == PrivacySettings.SingletonId, cancellationToken)
            .ConfigureAwait(false)
        ?? PrivacySettings.Defaults();
}

/// <inheritdoc />
internal sealed class IdentityPrivacySettingsService : IPrivacySettingsService
{
    private readonly IdentityDbContext _dbContext;
    private readonly PrivacySettingsStore _store;
    private readonly AdministrationGuard _guard;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public IdentityPrivacySettingsService(
        IdentityDbContext dbContext,
        PrivacySettingsStore store,
        AdministrationGuard guard,
        IClock clock,
        IAuditTrail audit)
    {
        _dbContext = dbContext;
        _store = store;
        _guard = guard;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<PrivacySettingsDto>> GetAsync(CancellationToken cancellationToken) =>
        _guard.CallerIsAdministrator
            ? Result<PrivacySettingsDto>.Success(ToDto(await _store.ReadAsync(cancellationToken).ConfigureAwait(false)))
            : Result<PrivacySettingsDto>.Failure(UserErrors.Forbidden);

    public async Task<Result<PrivacySettingsDto>> UpdateAsync(UpdatePrivacySettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_guard.CallerIsAdministrator)
        {
            return Result<PrivacySettingsDto>.Failure(UserErrors.Forbidden);
        }

        var settings = await _dbContext.PrivacySettings
            .FirstOrDefaultAsync(candidate => candidate.Id == PrivacySettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        var isNew = settings is null;
        settings ??= PrivacySettings.Defaults();

        var changed = settings.Update(request.SelfDeletionEnabled, request.DeletionGraceDays, _clock.UtcNow);
        if (changed.IsFailure)
        {
            return Result<PrivacySettingsDto>.Failure(changed.Error);
        }

        if (changed.Value)
        {
            if (isNew)
            {
                _dbContext.PrivacySettings.Add(settings);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        IdentityAuditActions.PrivacySettingsUpdated,
                        details: new Dictionary<string, string?>
                        {
                            ["selfDeletion"] = settings.SelfDeletionEnabled ? "on" : "off",
                            ["graceDays"] = settings.DeletionGraceDays.ToString(CultureInfo.InvariantCulture),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<PrivacySettingsDto>.Success(ToDto(settings));
    }

    private static PrivacySettingsDto ToDto(PrivacySettings settings) =>
        new(settings.SelfDeletionEnabled, settings.DeletionGraceDays, settings.UpdatedAtUtc);
}

/// <inheritdoc />
internal sealed class IdentityAccountPrivacyService : IAccountPrivacyService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserLookup _userLookup;
    private readonly PrivacySettingsStore _settings;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;
    private readonly IAccountMessaging _messaging;
    private readonly PersonalAccessTokenStore _tokens;

    public IdentityAccountPrivacyService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        UserLookup userLookup,
        PrivacySettingsStore settings,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier,
        IAccountMessaging messaging,
        PersonalAccessTokenStore tokens)
    {
        _tokens = tokens;
        _userManager = userManager;
        _signInManager = signInManager;
        _userLookup = userLookup;
        _settings = settings;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
        _messaging = messaging;
    }

    public async Task<Result> ConfirmPasswordAsync(string password, string action, CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

        return lookup.IsFailure
            ? Result.Failure(lookup.Error)
            : await ConfirmPasswordAsync(lookup.Value, password, action, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AccountDeletionDto>> RequestDeletionAsync(
        PasswordConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.SelfDeletionEnabled)
        {
            return Result<AccountDeletionDto>.Failure(UserErrors.SelfDeletionDisabled);
        }

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<AccountDeletionDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;

        var confirmed = await ConfirmPasswordAsync(user, request.Password, IdentityAuditActions.DeletionRequested, cancellationToken).ConfigureAwait(false);
        if (confirmed.IsFailure)
        {
            return Result<AccountDeletionDto>.Failure(confirmed.Error);
        }

        // The portal must keep someone able to administer it.
        if (await _userManager.IsInRoleAsync(user, Roles.Administrator).ConfigureAwait(false)
            && (await _userManager.GetUsersInRoleAsync(Roles.Administrator).ConfigureAwait(false))
                .Count(candidate => candidate.Id != user.Id && candidate.DeletionScheduledAtUtc is null) == 0)
        {
            return Result<AccountDeletionDto>.Failure(UserErrors.LastAdministrator);
        }

        user.RequestDeletion(_clock.UtcNow, TimeSpan.FromDays(settings.DeletionGraceDays));

        // A new stamp ends every other portal session at its next validation; the caller is signed out by the host.
        var saved = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (saved.Succeeded)
        {
            saved = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        }

        if (!saved.Succeeded)
        {
            return Result<AccountDeletionDto>.Failure(saved.ToError(UserErrors.SaveFailed));
        }

        // Scripts stop at once too; the host ends the application sessions.
        await _tokens.RevokeAllAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var scheduled = user.DeletionScheduledAtUtc!.Value;

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.DeletionRequested,
                    user.ToAuditSubject(),
                    new Dictionary<string, string?> { ["scheduledFor"] = scheduled.ToString("O", CultureInfo.InvariantCulture) }),
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = IdentityNotificationTypes.DeletionRequested,
                    Audience = NoticeAudience.Holders(IdentityNotificationRoles.UserReviewers),
                    Parameters = new Dictionary<string, string?>
                    {
                        ["user"] = user.DisplayName,
                        ["email"] = user.Email,
                        ["date"] = scheduled.ToString("O", CultureInfo.InvariantCulture),
                    },
                    Link = "/admin/users?status=deletion",
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _messaging.SendAsync(
                new AccountMessage(
                    AccountMessageKinds.DeletionScheduled,
                    user.Email!,
                    user.DisplayName,
                    user.Language,
                    "/sign-in",
                    new Dictionary<string, string?>
                    {
                        ["name"] = user.FirstName,
                        ["expires"] = scheduled.ToString("O", CultureInfo.InvariantCulture),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<AccountDeletionDto>.Success(new AccountDeletionDto(user.DeletionRequestedAtUtc!.Value, scheduled));
    }

    public async Task<Result> CancelDeletionAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var user = lookup.Value;
        if (!user.CancelDeletion())
        {
            return Result.Failure(UserErrors.DeletionNotScheduled);
        }

        var saved = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!saved.Succeeded)
        {
            return Result.Failure(saved.ToError(UserErrors.SaveFailed));
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.DeletionCancelled, user.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> ConfirmPasswordAsync(
        ApplicationUser user,
        string password,
        string action,
        CancellationToken cancellationToken)
    {
        // Counts towards lockout like a sign-in, so it cannot be used to guess the password.
        var checkedPassword = await _signInManager
            .CheckPasswordSignInAsync(user, password ?? string.Empty, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (checkedPassword.Succeeded)
        {
            return Result.Success();
        }

        await _audit.RecordAsync(
                AuditEvent.Failed(action, details: new Dictionary<string, string?> { ["reason"] = UserErrors.PasswordIncorrect.Code }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Failure(UserErrors.PasswordIncorrect);
    }
}
