using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>Reads the settings row, falling back to the defaults while none has been saved.</summary>
internal sealed class SecuritySettingsStore
{
    private readonly IdentityDbContext _dbContext;

    public SecuritySettingsStore(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SecuritySettings> ReadAsync(CancellationToken cancellationToken) =>
        await _dbContext.SecuritySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == SecuritySettings.SingletonId, cancellationToken)
            .ConfigureAwait(false)
        ?? SecuritySettings.Defaults();

    /// <summary>Whether the portal currently offers (and asks for) a second factor.</summary>
    public async Task<bool> IsTwoFactorAvailableAsync(CancellationToken cancellationToken) =>
        (await ReadAsync(cancellationToken).ConfigureAwait(false)).TwoFactorEnabled;
}

/// <inheritdoc />
internal sealed class IdentitySecuritySettingsService : ISecuritySettingsService
{
    private readonly IdentityDbContext _dbContext;
    private readonly SecuritySettingsStore _store;
    private readonly AdministrationGuard _guard;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly UserLookup _userLookup;
    private readonly IdentityTwoFactorPolicy _policy;
    private readonly UserSessionTicketStore _sessions;

    public IdentitySecuritySettingsService(
        IdentityDbContext dbContext,
        SecuritySettingsStore store,
        AdministrationGuard guard,
        IClock clock,
        IAuditTrail audit,
        UserLookup userLookup,
        IdentityTwoFactorPolicy policy,
        UserSessionTicketStore sessions)
    {
        _sessions = sessions;
        _userLookup = userLookup;
        _policy = policy;
        _dbContext = dbContext;
        _store = store;
        _guard = guard;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<SecuritySettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        // Sign-in security is never delegated through page grants: whoever could change it could weaken it.
        if (!_guard.CallerIsAdministrator)
        {
            return Result<SecuritySettingsDto>.Failure(UserErrors.Forbidden);
        }

        return Result<SecuritySettingsDto>.Success(ToDto(await _store.ReadAsync(cancellationToken).ConfigureAwait(false)));
    }

    public async Task<Result<SecuritySettingsDto>> UpdateAsync(
        UpdateSecuritySettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!_guard.CallerIsAdministrator)
        {
            return Result<SecuritySettingsDto>.Failure(UserErrors.Forbidden);
        }

        ArgumentNullException.ThrowIfNull(request);

        var settings = await _dbContext.SecuritySettings
            .FirstOrDefaultAsync(candidate => candidate.Id == SecuritySettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        var isNew = settings is null;
        settings ??= SecuritySettings.Defaults();

        var changed = settings.Update(
            request.TwoFactorEnabled,
            request.TwoFactorIssuer,
            _clock.UtcNow,
            request.PersonalAccessTokensEnabled,
            request.PasskeysEnabled,
            TwoFactorRequirements.FromContract(request.TwoFactorRequirement),
            request.IdleTimeoutMinutes);
        if (changed.IsFailure)
        {
            return Result<SecuritySettingsDto>.Failure(changed.Error);
        }

        // An administrator who demands a second factor they do not have would lock themselves out of the portal
        // (and of this setting) the moment they save.
        if (changed.Value
            && settings.TwoFactorRequirement != TwoFactorRequirement.None
            && await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false) is { IsSuccess: true } caller
            && await _policy.WouldLackFactorAsync(caller.Value, settings).ConfigureAwait(false))
        {
            return Result<SecuritySettingsDto>.Failure(UserErrors.TwoFactorRequirementSelf);
        }

        if (changed.Value)
        {
            if (isNew)
            {
                _dbContext.SecuritySettings.Add(settings);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _sessions.ForgetIdleSetting();

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        IdentityAuditActions.SecuritySettingsUpdated,
                        details: new Dictionary<string, string?>
                        {
                            ["twoFactor"] = settings.TwoFactorEnabled ? "on" : "off",
                            ["issuer"] = settings.TwoFactorIssuer,
                            ["personalAccessTokens"] = settings.PersonalAccessTokensEnabled ? "on" : "off",
                            ["passkeys"] = settings.PasskeysEnabled ? "on" : "off",
                            ["requirement"] = TwoFactorRequirements.ToContract(settings.TwoFactorRequirement),
                            ["idleTimeoutMinutes"] = settings.IdleTimeoutMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<SecuritySettingsDto>.Success(ToDto(settings));
    }

    private static SecuritySettingsDto ToDto(SecuritySettings settings) =>
        new(
            settings.TwoFactorEnabled,
            settings.TwoFactorIssuer,
            settings.PersonalAccessTokensEnabled,
            settings.UpdatedAtUtc,
            settings.PasskeysEnabled,
            TwoFactorRequirements.ToContract(settings.TwoFactorRequirement),
            settings.IdleTimeoutMinutes);
}
