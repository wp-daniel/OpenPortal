using System.ComponentModel.DataAnnotations;

namespace OpenPortal.Identity.Application.Contracts;

/// <summary>Credentials submitted to sign in.</summary>
public sealed class LoginRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.password.required")]
    [StringLength(256, ErrorMessage = "validation.password.max")]
    public string Password { get; init; } = string.Empty;

    /// <summary>When true the session cookie is persistent and survives closing the browser.</summary>
    public bool RememberMe { get; init; }
}