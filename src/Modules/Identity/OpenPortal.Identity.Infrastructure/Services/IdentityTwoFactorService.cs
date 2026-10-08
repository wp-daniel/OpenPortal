using System.Text;
using Microsoft.AspNetCore.Identity;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityTwoFactorService : ITwoFactorService
{
    /// <summary>How many recovery codes are issued at a time.</summary>
    public const int RecoveryCodeCount = 10;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserLookup _userLookup;
    private readonly SecuritySettingsStore _settings;
    private readonly IAuditTrail _audit;

    public IdentityTwoFactorService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        UserLookup userLookup,
        SecuritySettingsStore settings,
        IAuditTrail audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userLookup = userLookup;
        _settings = settings;
        _audit = audit;
    }

    public async Task<Result<TwoFactorStatusDto>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<TwoFactorStatusDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;
        var available = await _settings.IsTwoFactorAvailableAsync(cancellationToken).ConfigureAwait(false);
        var codesLeft = user.TwoFactorEnabled
            ? await _userManager.CountRecoveryCodesAsync(user).ConfigureAwait(false)
            : 0;

        return Result<TwoFactorStatusDto>.Success(new TwoFactorStatusDto(available, user.TwoFactorEnabled, codesLeft));
    }

    public async Task<Result<TwoFactorSetupDto>> BeginSetupAsync(CancellationToken cancellationToken)
    {
        var lookup = await FindForEnrolmentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<TwoFactorSetupDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;

        // An unfinished earlier setup keeps its key, so a QR code already scanned keeps working.
        var key = await _userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (string.IsNullOrEmpty(key))
        {
            var reset = await _userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
            if (!reset.Succeeded)
            {
                return Result<TwoFactorSetupDto>.Failure(reset.ToError(UserErrors.SaveFailed));
            }

            // Creating the key rotates the security stamp; re-issue this session's cookie so it stays valid.
            await _signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
            key = await _userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false) ?? string.Empty;
        }

        var issuer = (await _settings.ReadAsync(cancellationToken).ConfigureAwait(false)).TwoFactorIssuer;

        return Result<TwoFactorSetupDto>.Success(new TwoFactorSetupDto(
            SharedKey: FormatKey(key),
            AuthenticatorUri: AuthenticatorUri(issuer, user.Email ?? user.Id.ToString(), key)));
    }

    public async Task<Result<RecoveryCodesDto>> EnableAsync(
        EnableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await FindForEnrolmentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<RecoveryCodesDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;

        var valid = await _userManager
            .VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, NormaliseCode(request.Code))
            .ConfigureAwait(false);

        if (!valid)
        {
            return Result<RecoveryCodesDto>.Failure(UserErrors.TwoFactorSetupCodeInvalid);
        }

        var enabled = await _userManager.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);
        if (!enabled.Succeeded)
        {
            return Result<RecoveryCodesDto>.Failure(enabled.ToError(UserErrors.SaveFailed));
        }

        var codes = await _userManager
            .GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount)
            .ConfigureAwait(false);

        // Enabling rotates the security stamp; this session was the one that proved the code, so it stays.
        await _signInManager.RefreshSignInAsync(user).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.TwoFactorEnabled), cancellationToken)
            .ConfigureAwait(false);

        return Result<RecoveryCodesDto>.Success(new RecoveryCodesDto(codes?.ToArray() ?? []));
    }

    public async Task<Result> DisableAsync(TwoFactorPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await FindEnrolledWithPasswordAsync(request, IdentityAuditActions.TwoFactorDisabled, cancellationToken)
            .ConfigureAwait(false);

        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var user = lookup.Value;

        var cleared = await TwoFactorEnrolment.ClearAsync(_userManager, user).ConfigureAwait(false);
        if (cleared.IsFailure)
        {
            return cleared;
        }

        // The stamp changed: other sessions and browsers that skipped the code end, this one carries on.
        await _signInManager.RefreshSignInAsync(user).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.TwoFactorDisabled), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(
        TwoFactorPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await FindEnrolledWithPasswordAsync(request, IdentityAuditActions.RecoveryCodesGenerated, cancellationToken)
            .ConfigureAwait(false);

        if (lookup.IsFailure)
        {
            return Result<RecoveryCodesDto>.Failure(lookup.Error);
        }

        var codes = await _userManager
            .GenerateNewTwoFactorRecoveryCodesAsync(lookup.Value, RecoveryCodeCount)
            .ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.RecoveryCodesGenerated), cancellationToken)
            .ConfigureAwait(false);

        return Result<RecoveryCodesDto>.Success(new RecoveryCodesDto(codes?.ToArray() ?? []));
    }

    /// <summary>The caller, provided the portal offers two-factor and the caller is not enrolled yet.</summary>
    private async Task<Result<ApplicationUser>> FindForEnrolmentAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return lookup;
        }

        if (!await _settings.IsTwoFactorAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result<ApplicationUser>.Failure(UserErrors.TwoFactorUnavailable);
        }

        return lookup.Value.TwoFactorEnabled
            ? Result<ApplicationUser>.Failure(UserErrors.TwoFactorAlreadyEnabled)
            : lookup;
    }

    /// <summary>
    /// The caller, provided they are enrolled and typed their password. A wrong password is recorded against
    /// <paramref name="action"/>, as for a password change: it can mean someone else is at the keyboard.
    /// </summary>
    private async Task<Result<ApplicationUser>> FindEnrolledWithPasswordAsync(
        TwoFactorPasswordRequest request,
        string action,
        CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return lookup;
        }

        var user = lookup.Value;

        if (!user.TwoFactorEnabled)
        {
            return Result<ApplicationUser>.Failure(UserErrors.TwoFactorNotEnabled);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false))
        {
            await _audit.RecordAsync(
                    AuditEvent.Failed(
                        action,
                        details: new Dictionary<string, string?> { ["reason"] = UserErrors.CurrentPasswordIncorrect.Code }),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result<ApplicationUser>.Failure(UserErrors.CurrentPasswordIncorrect);
        }

        return lookup;
    }

    /// <summary>Authenticator apps accept the code with spaces or a dash, as they display it.</summary>
    public static string NormaliseCode(string code) =>
        code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

    /// <summary>The key in lower-case groups of four, easier to type into an app than one long string.</summary>
    private static string FormatKey(string key)
    {
        var formatted = new StringBuilder();

        for (var index = 0; index < key.Length; index += 4)
        {
            if (index > 0)
            {
                formatted.Append(' ');
            }

            formatted.Append(key.AsSpan(index, Math.Min(4, key.Length - index)));
        }

        return formatted.ToString().ToLowerInvariant();
    }

    /// <summary>The otpauth URI every authenticator app understands (Key Uri Format).</summary>
    private static string AuthenticatorUri(string issuer, string account, string key)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);

        return $"otpauth://totp/{encodedIssuer}:{Uri.EscapeDataString(account)}?secret={key}&issuer={encodedIssuer}&digits=6";
    }
}

/// <summary>Removing an account's authenticator enrolment, shared by the owner and by administrators.</summary>
internal static class TwoFactorEnrolment
{
    /// <summary>
    /// Turns two-factor off and discards the key and the recovery codes, so turning it on again needs a fresh
    /// scan. The security stamp changes, which ends sessions and forgets browsers that skipped the code.
    /// </summary>
    public static async Task<Result> ClearAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        var disabled = await userManager.SetTwoFactorEnabledAsync(user, false).ConfigureAwait(false);
        if (!disabled.Succeeded)
        {
            return Result.Failure(disabled.ToError(UserErrors.SaveFailed));
        }

        var reset = await userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (!reset.Succeeded)
        {
            return Result.Failure(reset.ToError(UserErrors.SaveFailed));
        }

        // Generating zero codes replaces the stored list with an empty one.
        await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0).ConfigureAwait(false);

        return Result.Success();
    }
}
