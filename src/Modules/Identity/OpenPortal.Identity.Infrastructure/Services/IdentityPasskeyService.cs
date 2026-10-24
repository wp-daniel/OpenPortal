using System.Buffers.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityTwoFactorPolicy : ITwoFactorPolicy
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SecuritySettingsStore _settings;

    public IdentityTwoFactorPolicy(UserManager<ApplicationUser> userManager, SecuritySettingsStore settings)
    {
        _userManager = userManager;
        _settings = settings;
    }

    public async Task<bool> IsSetupRequiredAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (settings.TwoFactorRequirement == TwoFactorRequirement.None)
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        var factors = await FactorsAsync(user, settings).ConfigureAwait(false);

        return factors.Required && !factors.HasAny;
    }

    /// <summary>
    /// True when the account must sign in with a passkey: the requirement covers it and passkeys are its only
    /// second factor, so a password alone would bypass the requirement.
    /// </summary>
    public async Task<bool> RequiresPasskeySignInAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (settings.TwoFactorRequirement == TwoFactorRequirement.None)
        {
            return false;
        }

        var factors = await FactorsAsync(user, settings).ConfigureAwait(false);

        return factors.Required && !factors.Authenticator && factors.Passkeys > 0;
    }

    /// <summary>Whether these settings would cover the account while it has no second factor they accept.</summary>
    public async Task<bool> WouldLackFactorAsync(ApplicationUser user, SecuritySettings settings)
    {
        var factors = await FactorsAsync(user, settings).ConfigureAwait(false);

        return factors.Required && !factors.HasAny;
    }

    /// <summary>Whether removing one factor would leave a covered account without any.</summary>
    public async Task<bool> IsLastFactorAsync(ApplicationUser user, bool removingAuthenticator, int removingPasskeys, CancellationToken cancellationToken)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var factors = await FactorsAsync(user, settings).ConfigureAwait(false);

        if (!factors.Required)
        {
            return false;
        }

        var authenticator = factors.Authenticator && !removingAuthenticator;
        var passkeys = factors.Passkeys - removingPasskeys > 0;

        return !authenticator && !passkeys;
    }

    private async Task<Factors> FactorsAsync(ApplicationUser user, SecuritySettings settings)
    {
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var authenticator = settings.TwoFactorEnabled && await _userManager.GetTwoFactorEnabledAsync(user).ConfigureAwait(false);
        var passkeys = settings.PasskeysEnabled ? (await _userManager.GetPasskeysAsync(user).ConfigureAwait(false)).Count : 0;

        return new Factors(settings.RequiresSecondFactor(roles), authenticator, passkeys);
    }

    private readonly record struct Factors(bool Required, bool Authenticator, int Passkeys)
    {
        public bool HasAny => Authenticator || Passkeys > 0;
    }
}

/// <inheritdoc />
internal sealed partial class IdentityPasskeyService : IPasskeyService
{
    private const int NameMaxLength = 64;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserLookup _userLookup;
    private readonly AdministrationGuard _guard;
    private readonly SecuritySettingsStore _settings;
    private readonly IdentityTwoFactorPolicy _policy;
    private readonly IAuditTrail _audit;
    private readonly ILogger<IdentityPasskeyService> _logger;

    public IdentityPasskeyService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        UserLookup userLookup,
        AdministrationGuard guard,
        SecuritySettingsStore settings,
        IdentityTwoFactorPolicy policy,
        IAuditTrail audit,
        ILogger<IdentityPasskeyService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userLookup = userLookup;
        _guard = guard;
        _settings = settings;
        _policy = policy;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<PasskeyDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<IReadOnlyList<PasskeyDto>>.Failure(lookup.Error);
        }

        var passkeys = await _userManager.GetPasskeysAsync(lookup.Value).ConfigureAwait(false);

        return Result<IReadOnlyList<PasskeyDto>>.Success(passkeys
            .OrderBy(passkey => passkey.CreatedAt)
            .Select(ToDto)
            .ToList());
    }

    public async Task<Result<string>> CreationOptionsAsync(CancellationToken cancellationToken)
    {
        if (!(await _settings.ReadAsync(cancellationToken).ConfigureAwait(false)).PasskeysEnabled)
        {
            return Result<string>.Failure(UserErrors.PasskeysDisabled);
        }

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<string>.Failure(lookup.Error);
        }

        var user = lookup.Value;

        // The id is the account's own id, so the authenticator stores one passkey per account and replaces it
        // when the same device registers again.
        var options = await _signInManager
            .MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
            {
                Id = user.Id.ToString(),
                Name = user.Email!,
                DisplayName = user.DisplayName,
            })
            .ConfigureAwait(false);

        return Result<string>.Success(options);
    }

    public async Task<Result<PasskeyDto>> RegisterAsync(RegisterPasskeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!(await _settings.ReadAsync(cancellationToken).ConfigureAwait(false)).PasskeysEnabled)
        {
            return Result<PasskeyDto>.Failure(UserErrors.PasskeysDisabled);
        }

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<PasskeyDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;
        var name = NormaliseName(request.Name);
        if (name is { Length: > NameMaxLength })
        {
            return Result<PasskeyDto>.Failure(UserErrors.PasskeyNameInvalid);
        }

        var attestation = await _signInManager.PerformPasskeyAttestationAsync(request.Credential).ConfigureAwait(false);

        // The options were issued for this account; a credential created for another one is not accepted.
        if (!attestation.Succeeded || !string.Equals(attestation.UserEntity.Id, user.Id.ToString(), StringComparison.Ordinal))
        {
            LogAttestationFailed(attestation.Failure?.Message ?? "user mismatch");
            return Result<PasskeyDto>.Failure(UserErrors.PasskeyInvalid);
        }

        var passkey = attestation.Passkey;
        passkey.Name = name ?? await DefaultNameAsync(user).ConfigureAwait(false);

        var added = await _userManager.AddOrUpdatePasskeyAsync(user, passkey).ConfigureAwait(false);
        if (!added.Succeeded)
        {
            return Result<PasskeyDto>.Failure(added.ToError(UserErrors.SaveFailed));
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(IdentityAuditActions.PasskeyAdded, details: new Dictionary<string, string?> { ["name"] = passkey.Name }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<PasskeyDto>.Success(ToDto(passkey));
    }

    public async Task<Result<PasskeyDto>> RenameAsync(string passkeyId, RenamePasskeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = NormaliseName(request.Name);
        if (name is null or { Length: > NameMaxLength })
        {
            return Result<PasskeyDto>.Failure(UserErrors.PasskeyNameInvalid);
        }

        var found = await FindOwnAsync(passkeyId, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<PasskeyDto>.Failure(found.Error);
        }

        var (user, passkey) = found.Value;
        passkey.Name = name;

        var saved = await _userManager.AddOrUpdatePasskeyAsync(user, passkey).ConfigureAwait(false);
        if (!saved.Succeeded)
        {
            return Result<PasskeyDto>.Failure(saved.ToError(UserErrors.SaveFailed));
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(IdentityAuditActions.PasskeyRenamed, details: new Dictionary<string, string?> { ["name"] = name }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<PasskeyDto>.Success(ToDto(passkey));
    }

    public async Task<Result> DeleteAsync(string passkeyId, CancellationToken cancellationToken)
    {
        var found = await FindOwnAsync(passkeyId, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result.Failure(found.Error);
        }

        var (user, passkey) = found.Value;

        if (await _policy.IsLastFactorAsync(user, removingAuthenticator: false, removingPasskeys: 1, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(UserErrors.TwoFactorLastFactor);
        }

        var removed = await _userManager.RemovePasskeyAsync(user, passkey.CredentialId).ConfigureAwait(false);
        if (!removed.Succeeded)
        {
            return Result.Failure(removed.ToError(UserErrors.SaveFailed));
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(IdentityAuditActions.PasskeyRemoved, details: new Dictionary<string, string?> { ["name"] = passkey.Name }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> ResetForUserAsync(Guid userId, CancellationToken cancellationToken)
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

        var changeable = await _guard.EnsureMayChangeAsync(lookup.Value).ConfigureAwait(false);
        if (changeable.IsFailure)
        {
            return changeable;
        }

        var user = lookup.Value;
        var passkeys = await _userManager.GetPasskeysAsync(user).ConfigureAwait(false);

        foreach (var passkey in passkeys)
        {
            await _userManager.RemovePasskeyAsync(user, passkey.CredentialId).ConfigureAwait(false);
        }

        // Sessions opened with one of those passkeys end too.
        await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.PasskeysReset,
                    user.ToAuditSubject(),
                    new Dictionary<string, string?> { ["count"] = passkeys.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public static string EncodeId(byte[] credentialId) => Base64Url.EncodeToString(credentialId);

    private async Task<Result<(ApplicationUser User, UserPasskeyInfo Passkey)>> FindOwnAsync(string passkeyId, CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<(ApplicationUser, UserPasskeyInfo)>.Failure(lookup.Error);
        }

        byte[] credentialId;
        try
        {
            credentialId = Base64Url.DecodeFromChars(passkeyId);
        }
        catch (FormatException)
        {
            return Result<(ApplicationUser, UserPasskeyInfo)>.Failure(UserErrors.PasskeyNotFound);
        }

        var passkey = await _userManager.GetPasskeyAsync(lookup.Value, credentialId).ConfigureAwait(false);

        return passkey is null
            ? Result<(ApplicationUser, UserPasskeyInfo)>.Failure(UserErrors.PasskeyNotFound)
            : Result<(ApplicationUser, UserPasskeyInfo)>.Success((lookup.Value, passkey));
    }

    /// <summary>"Passkey", "Passkey 2"…: something to tell them apart until the owner names them.</summary>
    private async Task<string> DefaultNameAsync(ApplicationUser user)
    {
        var count = (await _userManager.GetPasskeysAsync(user).ConfigureAwait(false)).Count;

        return count == 0 ? "Passkey" : $"Passkey {count + 1}";
    }

    private static string? NormaliseName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static PasskeyDto ToDto(UserPasskeyInfo passkey) =>
        new(EncodeId(passkey.CredentialId), passkey.Name ?? "Passkey", passkey.CreatedAt, passkey.IsBackedUp);

    [LoggerMessage(Level = LogLevel.Information, Message = "A passkey registration was refused: {Reason}")]
    private partial void LogAttestationFailed(string reason);
}
