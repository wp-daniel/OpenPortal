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

/// <summary>Reads the registration settings row, falling back to the defaults (registration off) while none is saved.</summary>
internal sealed class RegistrationSettingsStore
{
    private readonly IdentityDbContext _dbContext;

    public RegistrationSettingsStore(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RegistrationSettings> ReadAsync(CancellationToken cancellationToken) =>
        await _dbContext.RegistrationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == RegistrationSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false)
        ?? RegistrationSettings.Defaults();

    /// <summary>
    /// Whether <paramref name="user"/> may not sign in yet because its own address is unconfirmed: only accounts
    /// that registered themselves, and only while the portal asks for confirmation.
    /// </summary>
    public async Task<bool> AwaitsEmailConfirmationAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        user is { Origin: AccountOrigin.Registered, EmailConfirmed: false }
        && (await ReadAsync(cancellationToken).ConfigureAwait(false)).RequireEmailConfirmation;
}

/// <inheritdoc />
internal sealed class IdentityRegistrationSettingsService : IRegistrationSettingsService
{
    private readonly IdentityDbContext _dbContext;
    private readonly RegistrationSettingsStore _store;
    private readonly AdministrationGuard _guard;
    private readonly IAccountMessaging _messaging;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public IdentityRegistrationSettingsService(
        IdentityDbContext dbContext,
        RegistrationSettingsStore store,
        AdministrationGuard guard,
        IAccountMessaging messaging,
        IClock clock,
        IAuditTrail audit)
    {
        _dbContext = dbContext;
        _store = store;
        _guard = guard;
        _messaging = messaging;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<RegistrationSettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        // Who may get an account is never delegated through page grants.
        if (!_guard.CallerIsAdministrator)
        {
            return Result<RegistrationSettingsDto>.Failure(UserErrors.Forbidden);
        }

        return Result<RegistrationSettingsDto>.Success(ToDto(await _store.ReadAsync(cancellationToken).ConfigureAwait(false), await _messaging.CanSendEmailAsync(cancellationToken).ConfigureAwait(false)));
    }

    public async Task<Result<RegistrationSettingsDto>> UpdateAsync(
        UpdateRegistrationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_guard.CallerIsAdministrator)
        {
            return Result<RegistrationSettingsDto>.Failure(UserErrors.Forbidden);
        }

        if (RegistrationMapping.ParseMode(request.Mode) is not { } mode)
        {
            return Result<RegistrationSettingsDto>.Failure(UserErrors.RegistrationModeInvalid);
        }

        // Open registration that waits for a confirmation nobody can send would strand every new account.
        if (mode != RegistrationMode.Disabled && request.RequireEmailConfirmation && !await _messaging.CanSendEmailAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result<RegistrationSettingsDto>.Failure(UserErrors.EmailDeliveryUnavailable);
        }

        var settings = await _dbContext.RegistrationSettings
            .FirstOrDefaultAsync(candidate => candidate.Id == RegistrationSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        var isNew = settings is null;
        settings ??= RegistrationSettings.Defaults();

        var changed = settings.Update(
            mode,
            request.AllowedDomains,
            request.RequireEmailConfirmation,
            request.InvitationLifetimeDays,
            _clock.UtcNow);

        if (changed.IsFailure)
        {
            return Result<RegistrationSettingsDto>.Failure(changed.Error);
        }

        if (changed.Value)
        {
            if (isNew)
            {
                _dbContext.RegistrationSettings.Add(settings);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        IdentityAuditActions.RegistrationSettingsUpdated,
                        details: new Dictionary<string, string?>
                        {
                            ["mode"] = RegistrationMapping.ToContract(settings.Mode),
                            ["domains"] = settings.AllowedDomains.Count == 0 ? "*" : string.Join(", ", settings.AllowedDomains),
                            ["emailConfirmation"] = settings.RequireEmailConfirmation ? "on" : "off",
                            ["invitationDays"] = settings.InvitationLifetimeDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<RegistrationSettingsDto>.Success(ToDto(settings, await _messaging.CanSendEmailAsync(cancellationToken).ConfigureAwait(false)));
    }

    private static RegistrationSettingsDto ToDto(RegistrationSettings settings, bool canSendEmail) => new(
        RegistrationMapping.ToContract(settings.Mode),
        settings.AllowedDomains,
        settings.RequireEmailConfirmation,
        settings.InvitationLifetimeDays,
        canSendEmail,
        settings.UpdatedAtUtc);
}

/// <summary>Contract names for the registration enums.</summary>
internal static class RegistrationMapping
{
    public static string ToContract(RegistrationMode mode) => mode switch
    {
        RegistrationMode.Disabled => RegistrationModes.Disabled,
        RegistrationMode.Open => RegistrationModes.Open,
        RegistrationMode.Approval => RegistrationModes.Approval,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static RegistrationMode? ParseMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        RegistrationModes.Disabled => RegistrationMode.Disabled,
        RegistrationModes.Open => RegistrationMode.Open,
        RegistrationModes.Approval => RegistrationMode.Approval,
        _ => null,
    };

    public static string ToContract(AccountOrigin origin) => origin switch
    {
        AccountOrigin.Created => AccountOrigins.Created,
        AccountOrigin.Registered => AccountOrigins.Registered,
        AccountOrigin.Invited => AccountOrigins.Invited,
        AccountOrigin.Provisioned => AccountOrigins.Provisioned,
        AccountOrigin.External => AccountOrigins.External,
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };
}
