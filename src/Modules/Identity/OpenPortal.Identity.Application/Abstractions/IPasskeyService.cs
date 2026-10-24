using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// The signed-in account's passkeys (WebAuthn credentials: a fingerprint, face or PIN on a device, or a security
/// key). Scoped to <see cref="ICurrentUser"/>. The browser does the cryptography; the options and credentials pass
/// through as the JSON the WebAuthn API produces and consumes.
/// </summary>
public interface IPasskeyService
{
    Task<Result<IReadOnlyList<PasskeyDto>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>The <c>publicKey</c> options for <c>navigator.credentials.create()</c>, as JSON.</summary>
    Task<Result<string>> CreationOptionsAsync(CancellationToken cancellationToken);

    /// <summary>Verifies the new credential against the options issued last and adds it to the account.</summary>
    Task<Result<PasskeyDto>> RegisterAsync(RegisterPasskeyRequest request, CancellationToken cancellationToken);

    Task<Result<PasskeyDto>> RenameAsync(string passkeyId, RenamePasskeyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a passkey. Refused when it is the account's last second factor and the portal requires one.
    /// </summary>
    Task<Result> DeleteAsync(string passkeyId, CancellationToken cancellationToken);

    /// <summary>An administrator removes every passkey of an account (a lost device); its sessions end.</summary>
    Task<Result> ResetForUserAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Whether an account must still set up a second factor before using the portal.</summary>
public interface ITwoFactorPolicy
{
    /// <summary>
    /// True when the portal's requirement covers the account and it has neither an authenticator app nor a passkey
    /// (counting only the methods the portal has on).
    /// </summary>
    Task<bool> IsSetupRequiredAsync(Guid userId, CancellationToken cancellationToken);
}
