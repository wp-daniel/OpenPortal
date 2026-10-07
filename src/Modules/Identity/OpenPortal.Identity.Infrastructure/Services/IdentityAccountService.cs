using Microsoft.AspNetCore.Identity;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityAccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserLookup _userLookup;
    private readonly IClock _clock;
    private readonly ILanguageCatalog _languages;

    public IdentityAccountService(
        UserManager<ApplicationUser> userManager,
        UserLookup userLookup,
        IClock clock,
        ILanguageCatalog languages)
    {
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

        return persisted.Succeeded
            ? await ToProfileAsync(user, cancellationToken).ConfigureAwait(false)
            : Result<AccountProfileDto>.Failure(persisted.ToError(UserErrors.SaveFailed));
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

        return changed.Succeeded
            ? Result.Success()
            : Result.Failure(changed.ToError(UserErrors.CurrentPasswordIncorrect));
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
            AvatarUpdatedAtUtc: user.AvatarUpdatedAtUtc));
    }
}