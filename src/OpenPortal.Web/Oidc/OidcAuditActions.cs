namespace OpenPortal.Web.Oidc;

/// <summary>
/// The audit actions the OpenID Connect endpoints record. Each needs an <c>audit.action.&lt;code&gt;</c> label in
/// the resource files (a test checks it).
/// </summary>
public static class OidcAuditActions
{
    /// <summary>
    /// A user signed in to an application through the portal (a code was issued), or was refused because they
    /// have no access to it.
    /// </summary>
    public const string SignIn = "oidc.sign_in";

    /// <summary>An application's token request was refused because the user or their access is gone.</summary>
    public const string TokenRefused = "oidc.token_refused";
}
