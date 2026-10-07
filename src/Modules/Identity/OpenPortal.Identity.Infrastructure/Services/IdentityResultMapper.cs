using Microsoft.AspNetCore.Identity;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>
/// Translates ASP.NET Core Identity validation failures into stable, machine-readable
/// <see cref="Error"/> codes so that clients never have to parse Identity's internal error strings.
/// </summary>
internal static class IdentityResultMapper
{
    private const string DuplicateEmailCode = "DuplicateEmail";
    private const string DuplicateUserNameCode = "DuplicateUserName";
    private const string PasswordMismatchCode = "PasswordMismatch";

    private static readonly string[] PasswordComplexityCodes =
    [
        "PasswordTooShort",
        "PasswordRequiresUniqueChars",
        "PasswordRequiresDigit",
        "PasswordRequiresLower",
        "PasswordRequiresUpper",
        "PasswordRequiresNonAlphanumeric",
    ];

    /// <param name="result">The failed Identity result.</param>
    /// <param name="fallback">Error to use when Identity reports a failure without saying why.</param>
    public static Error ToError(this IdentityResult result, Error fallback)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fallback);

        if (result.Succeeded)
        {
            throw new ArgumentException("Only failed Identity results can be mapped to an error.", nameof(result));
        }

        var first = result.Errors.FirstOrDefault(error => !string.IsNullOrWhiteSpace(error.Code));
        if (first is null)
        {
            return fallback;
        }

        var codes = result.Errors.Select(error => error.Code).ToArray();

        if (codes.Any(code => PasswordComplexityCodes.Contains(code, StringComparer.Ordinal)))
        {
            return UserErrors.PasswordComplexity;
        }

        if (codes.Contains(DuplicateEmailCode, StringComparer.Ordinal)
            || codes.Contains(DuplicateUserNameCode, StringComparer.Ordinal))
        {
            return UserErrors.DuplicateEmail;
        }

        if (codes.Contains(PasswordMismatchCode, StringComparer.Ordinal))
        {
            return UserErrors.CurrentPasswordIncorrect;
        }

        // Identity validator descriptions are written for end users (for example "Passwords must have at
        // least one digit"), so the first one is safe to surface and far more useful than a bare code.
        return Error.Validation("identity." + first.Code.ToLowerInvariant(), first.Description);
    }
}