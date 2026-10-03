using System.ComponentModel.DataAnnotations;

namespace OpenPortal.Identity.Application.Contracts;

/// <summary>Credentials submitted to sign in.</summary>
public sealed class LoginRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required.")]
    [StringLength(256, ErrorMessage = "Password must not exceed 256 characters.")]
    public string Password { get; init; } = string.Empty;

    /// <summary>When true the session cookie is persistent and survives closing the browser.</summary>
    public bool RememberMe { get; init; }
}