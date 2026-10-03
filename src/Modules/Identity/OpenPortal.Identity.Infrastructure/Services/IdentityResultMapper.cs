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
    private const string FallbackCode = "identity.invalid_operation";

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
    /// <param name="fallback">Error to use when the Identity codes carry no more specific meaning.</param>
    public static Error ToError(this IdentityResult result, Error fallback)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fallback);

        if (result.Succeeded)
        {
            throw new ArgumentException("Only failed Identity results can be mapped to an error.", nameof(result));
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

        // Identity validator descriptions are written for end users (for example "Passwords must have at
        // least one digit"), so the first one is safe to surface and far more useful than a bare code.
        var description = result.Errors.FirstOrDefault()?.Description ?? fallback.Description;
        var code = codes.Length > 0 && !string.IsNullOrWhiteSpace(codes[0])
            ? "identity." + codes[0].ToLowerInvariant()
            : FallbackCode;

        return Error.Validation(code, description);
    }
}