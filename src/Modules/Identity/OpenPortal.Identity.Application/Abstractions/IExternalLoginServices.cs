using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>The external sign-in providers (Google, Microsoft) as administrators configure them. Administrators only.</summary>
public interface IExternalLoginSettingsService
{
    Task<Result<IReadOnlyList<ExternalProviderSettingsDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<ExternalProviderSettingsDto>> UpdateAsync(
        string provider,
        UpdateExternalProviderRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Signing in with an external account and connecting external accounts to portal accounts. The host's provider
/// handler leaves the external identity in Identity's external cookie (with <see cref="ExternalLoginClaimTypes"/>);
/// these operations read it from there.
/// </summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Signs in with the external identity: the connected account, else the account with the same verified address
    /// (which gets connected), else a new account when the provider creates them. The value is one of
    /// <see cref="ExternalSignInOutcomes"/>.
    /// </summary>
    Task<Result<string>> SignInAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Connects the external identity to the signed-in account, which must be <paramref name="expectedUserId"/> (the
    /// account that started the connection).
    /// </summary>
    Task<Result> LinkAsync(Guid expectedUserId, CancellationToken cancellationToken);

    Task<Result<AccountExternalLoginsDto>> ListOwnAsync(CancellationToken cancellationToken);

    /// <summary>Disconnects a provider. Refused when it is the only way left for the account to sign in.</summary>
    Task<Result> RemoveOwnAsync(string provider, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ExternalLoginDto>>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>An administrator disconnects a provider from an account (a compromised external account).</summary>
    Task<Result> RemoveForUserAsync(Guid userId, string provider, CancellationToken cancellationToken);
}

/// <summary>A provider that is on, with what the host's handler needs to talk to it.</summary>
/// <param name="ClientSecret">In clear: for the handler only, never for a response.</param>
public sealed record ExternalProviderConfiguration(
    string Key,
    string ClientId,
    string ClientSecret,
    string? Tenant,
    bool SingleTenant);

/// <summary>
/// The providers that are on, kept in memory for the host's authentication handlers (whose options are synchronous).
/// Refreshed from the database at most every few seconds, and at once on the instance that saved a change.
/// </summary>
public interface IExternalLoginProviders
{
    /// <summary>The providers that are on and configured.</summary>
    IReadOnlyList<ExternalProviderConfiguration> Current { get; }

    /// <summary>Reads the settings again unless they were read moments ago; raises <see cref="Changed"/> when they differ.</summary>
    Task RefreshAsync(CancellationToken cancellationToken);

    /// <summary>Raised after <see cref="Current"/> changed, so the host can drop what it built from the old values.</summary>
    event EventHandler? Changed;

    ExternalProviderConfiguration? Find(string provider) =>
        Current.FirstOrDefault(candidate => string.Equals(candidate.Key, provider, StringComparison.Ordinal));
}

/// <summary>The provider list for the session payload (only what a sign-in page shows).</summary>
public static class ExternalProviderInfo
{
    public static IReadOnlyList<ExternalProviderInfoDto> From(IExternalLoginProviders providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        return providers.Current
            .OrderBy(provider => ExternalLoginProvidersOrder(provider.Key))
            .Select(provider => new ExternalProviderInfoDto(provider.Key, ExternalLoginProviders.DisplayName(provider.Key)))
            .ToArray();
    }

    private static int ExternalLoginProvidersOrder(string key)
    {
        var index = ExternalLoginProviders.All.ToList().IndexOf(key);
        return index < 0 ? int.MaxValue : index;
    }
}
