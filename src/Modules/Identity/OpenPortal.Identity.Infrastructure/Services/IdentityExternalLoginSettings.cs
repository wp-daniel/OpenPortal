using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

/// <summary>
/// Encrypts the providers' client secrets with the portal's Data Protection keys: the portal sends them to the
/// provider on every code exchange, so it must be able to read them back (unlike passwords, which it only verifies).
/// </summary>
internal sealed class ExternalSecretProtector
{
    private const string Purpose = "OpenPortal.Identity.ExternalLoginSecret.v1";

    private readonly IDataProtector _protector;

    public ExternalSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string secret) => _protector.Protect(secret);

    /// <summary>The secret in clear, or null when there is none or the keys that protected it are gone.</summary>
    public string? TryUnprotect(string? protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

/// <inheritdoc />
internal sealed partial class ExternalLoginDirectory : IExternalLoginProviders
{
    /// <summary>How long a read is trusted before another instance's change is looked for.</summary>
    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ExternalSecretProtector _protector;
    private readonly IClock _clock;
    private readonly ILogger<ExternalLoginDirectory> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Snapshot _snapshot = new([], string.Empty);
    private long _readAtUtcTicks = DateTimeOffset.MinValue.UtcTicks;

    public ExternalLoginDirectory(
        IServiceScopeFactory scopes,
        ExternalSecretProtector protector,
        IClock clock,
        ILogger<ExternalLoginDirectory> logger)
    {
        _scopes = scopes;
        _protector = protector;
        _clock = clock;
        _logger = logger;
    }

    public IReadOnlyList<ExternalProviderConfiguration> Current => Volatile.Read(ref _snapshot).Providers;

    public event EventHandler? Changed;

    public Task RefreshAsync(CancellationToken cancellationToken) =>
        _clock.UtcNow.UtcTicks - Interlocked.Read(ref _readAtUtcTicks) < Freshness.Ticks ? Task.CompletedTask : ReloadAsync(cancellationToken);

    /// <summary>Reads the settings now (after a change saved on this instance).</summary>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<ExternalLoginProvider> rows;
            await using (var scope = _scopes.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                rows = await db.ExternalLoginProviders.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            }

            var providers = new List<ExternalProviderConfiguration>();
            foreach (var row in rows.Where(row => row.Enabled && ExternalLoginProviders.IsKnown(row.Key)))
            {
                var secret = _protector.TryUnprotect(row.ProtectedClientSecret);
                if (secret is null || string.IsNullOrEmpty(row.ClientId))
                {
                    // Keys rotated away or lost: the provider cannot work until an administrator enters the secret again.
                    LogSecretUnreadable(row.Key);
                    continue;
                }

                providers.Add(new ExternalProviderConfiguration(row.Key, row.ClientId, secret, row.Tenant, row.IsSingleTenant));
            }

            var signature = string.Join(
                '\n',
                rows.OrderBy(row => row.Key, StringComparer.Ordinal)
                    .Select(row => $"{row.Key}|{row.Enabled}|{row.ClientId}|{row.ProtectedClientSecret}|{row.Tenant}"));

            Interlocked.Exchange(ref _readAtUtcTicks, _clock.UtcNow.UtcTicks);

            var previous = Volatile.Read(ref _snapshot);
            if (string.Equals(previous.Signature, signature, StringComparison.Ordinal))
            {
                return;
            }

            Volatile.Write(ref _snapshot, new Snapshot(providers, signature));
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A database that is not reachable (or not migrated yet) keeps the providers as they were.
            LogReloadFailed(exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record Snapshot(IReadOnlyList<ExternalProviderConfiguration> Providers, string Signature);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The client secret of sign-in provider {Provider} cannot be read; enter it again in the settings.")]
    private partial void LogSecretUnreadable(string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The external sign-in providers could not be read.")]
    private partial void LogReloadFailed(Exception exception);
}

/// <inheritdoc />
internal sealed class IdentityExternalLoginSettingsService : IExternalLoginSettingsService
{
    private readonly IdentityDbContext _dbContext;
    private readonly AdministrationGuard _guard;
    private readonly ExternalSecretProtector _protector;
    private readonly ExternalLoginDirectory _directory;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public IdentityExternalLoginSettingsService(
        IdentityDbContext dbContext,
        AdministrationGuard guard,
        ExternalSecretProtector protector,
        ExternalLoginDirectory directory,
        IClock clock,
        IAuditTrail audit)
    {
        _dbContext = dbContext;
        _guard = guard;
        _protector = protector;
        _directory = directory;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ExternalProviderSettingsDto>>> ListAsync(CancellationToken cancellationToken)
    {
        // Who can sign in through what is sign-in security, never delegated.
        if (!_guard.CallerIsAdministrator)
        {
            return Result<IReadOnlyList<ExternalProviderSettingsDto>>.Failure(UserErrors.Forbidden);
        }

        var rows = await _dbContext.ExternalLoginProviders.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<ExternalProviderSettingsDto>>.Success(ExternalLoginProviders.All
            .Select(key => ToDto(rows.FirstOrDefault(row => row.Key == key) ?? ExternalLoginProvider.Unconfigured(key)))
            .ToArray());
    }

    public async Task<Result<ExternalProviderSettingsDto>> UpdateAsync(
        string provider,
        UpdateExternalProviderRequest request,
        CancellationToken cancellationToken)
    {
        if (!_guard.CallerIsAdministrator)
        {
            return Result<ExternalProviderSettingsDto>.Failure(UserErrors.Forbidden);
        }

        ArgumentNullException.ThrowIfNull(request);

        if (!ExternalLoginProviders.IsKnown(provider))
        {
            return Result<ExternalProviderSettingsDto>.Failure(UserErrors.ExternalProviderNotFound);
        }

        var row = await _dbContext.ExternalLoginProviders
            .FirstOrDefaultAsync(candidate => candidate.Key == provider, cancellationToken)
            .ConfigureAwait(false);

        var isNew = row is null;
        row ??= ExternalLoginProvider.Unconfigured(provider);

        var secret = string.IsNullOrWhiteSpace(request.ClientSecret) ? null : _protector.Protect(request.ClientSecret.Trim());
        var changed = row.Update(
            request.Enabled,
            request.ClientId,
            secret,
            request.Tenant,
            ExternalSignUpModes.FromContract(request.SignUp),
            request.AllowedDomains,
            _clock.UtcNow);

        if (changed.IsFailure)
        {
            return Result<ExternalProviderSettingsDto>.Failure(changed.Error);
        }

        if (changed.Value)
        {
            if (isNew)
            {
                _dbContext.ExternalLoginProviders.Add(row);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _directory.ReloadAsync(cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        IdentityAuditActions.ExternalProviderUpdated,
                        details: new Dictionary<string, string?>
                        {
                            ["provider"] = provider,
                            ["enabled"] = row.Enabled ? "on" : "off",
                            ["clientId"] = row.ClientId,
                            ["secret"] = secret is null ? "kept" : "replaced",
                            ["tenant"] = row.Tenant,
                            ["signUp"] = ExternalSignUpModes.ToContract(row.SignUp),
                            ["domains"] = string.Join(", ", row.AllowedDomains),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<ExternalProviderSettingsDto>.Success(ToDto(row));
    }

    private static ExternalProviderSettingsDto ToDto(ExternalLoginProvider row) =>
        new(
            row.Key,
            ExternalLoginProviders.DisplayName(row.Key),
            row.Enabled,
            row.ClientId,
            row.HasClientSecret,
            row.Tenant ?? (row.Key == ExternalLoginProviders.Microsoft ? ExternalLoginProvider.MicrosoftOrganizations : null),
            ExternalSignUpModes.ToContract(row.SignUp),
            row.AllowedDomains,
            ExternalLoginProviders.CallbackPath(row.Key),
            row.UpdatedAtUtc);
}
