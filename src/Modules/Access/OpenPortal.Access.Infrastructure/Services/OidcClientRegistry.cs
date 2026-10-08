using System.Buffers.Text;
using System.Security.Cryptography;
using OpenIddict.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain.Applications;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// Keeps OpenIddict's client registrations in step with <see cref="PortalApplication"/>.
/// <para>
/// A client exists only once an application has been approved, because a confidential client cannot be
/// created without a secret, and the secret is issued at approval. Disabling an application keeps the client
/// (so re-enabling does not force a new secret onto every deployment) and relies on the access check in the
/// sign-in endpoints plus token revocation to shut it out.
/// </para>
/// </summary>
internal sealed class OidcClientRegistry
{
    private readonly IOpenIddictApplicationManager _applications;
    private readonly IOpenIddictAuthorizationManager _authorizations;
    private readonly IOpenIddictTokenManager _tokens;

    public OidcClientRegistry(
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictTokenManager tokens)
    {
        _applications = applications;
        _authorizations = authorizations;
        _tokens = tokens;
    }

    /// <summary>Creates the client, or replaces its secret, and returns the new secret in clear.</summary>
    public async Task<string> IssueSecretAsync(PortalApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        var secret = NewSecret();
        var client = await _applications.FindByClientIdAsync(application.ClientId, cancellationToken).ConfigureAwait(false);

        if (client is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = application.ClientId,
                ClientSecret = secret,
                ClientType = ClientTypes.Confidential,
                ConsentType = ConsentTypes.Implicit,
            };

            Describe(descriptor, application);
            await _applications.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _applications.UpdateAsync(client, secret, cancellationToken).ConfigureAwait(false);
        }

        return secret;
    }

    /// <summary>Copies the editable details onto an existing client. Does nothing before approval.</summary>
    public async Task SyncAsync(PortalApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        var client = await _applications.FindByClientIdAsync(application.ClientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return;
        }

        // Populate from the stored client first so the hashed secret is carried over unchanged.
        var descriptor = new OpenIddictApplicationDescriptor();
        await _applications.PopulateAsync(descriptor, client, cancellationToken).ConfigureAwait(false);

        Describe(descriptor, application);
        await _applications.UpdateAsync(client, descriptor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Brings an existing client's permissions up to date when they lag behind what <see cref="Describe"/>
    /// grants today, for example the scopes added in a later version. Returns true when the client changed.
    /// </summary>
    public async Task<bool> UpgradePermissionsAsync(PortalApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        var client = await _applications.FindByClientIdAsync(application.ClientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return false;
        }

        foreach (var permission in CurrentPermissions)
        {
            if (!await _applications.HasPermissionAsync(client, permission, cancellationToken).ConfigureAwait(false))
            {
                await SyncAsync(application, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    /// <summary>Deletes the client together with its authorizations and tokens.</summary>
    public async Task DeleteAsync(string clientId, CancellationToken cancellationToken)
    {
        var client = await _applications.FindByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is not null)
        {
            await _applications.DeleteAsync(client, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Revokes the authorizations and tokens issued to the client, for the given users or, when
    /// <paramref name="userIds"/> is null, for everyone. A revoked refresh token cannot be redeemed, so the
    /// application loses the session at the next refresh at the latest.
    /// </summary>
    public async Task RevokeAsync(string clientId, IEnumerable<Guid>? userIds, CancellationToken cancellationToken)
    {
        var client = await _applications.FindByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return;
        }

        var clientKey = await _applications.GetIdAsync(client, cancellationToken).ConfigureAwait(false);

        var subjects = userIds?.Select(id => (string?)id.ToString()) ?? [null];

        foreach (var subject in subjects)
        {
            await _authorizations.RevokeAsync(subject, clientKey, status: null, type: null, cancellationToken).ConfigureAwait(false);
            await _tokens.RevokeAsync(subject, clientKey, status: null, type: null, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Describe(OpenIddictApplicationDescriptor descriptor, PortalApplication application)
    {
        descriptor.DisplayName = application.DisplayName;

        descriptor.RedirectUris.Clear();
        foreach (var uri in application.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        descriptor.PostLogoutRedirectUris.Clear();
        foreach (var uri in application.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        // Authorization code with PKCE plus refresh tokens is the only flow offered: it is the right one for a
        // server-rendered or BFF application, and every other flow is a way around the access check.
        descriptor.Permissions.Clear();
        descriptor.Permissions.UnionWith(CurrentPermissions);

        descriptor.Requirements.Clear();
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
    }

    /// <summary>
    /// Authorization code with PKCE plus refresh tokens, and the profile, email, roles and groups scopes. Which
    /// groups an application actually receives is decided by its own setting, not by the scope.
    /// </summary>
    private static readonly string[] CurrentPermissions =
    [
        Permissions.Endpoints.Authorization,
        Permissions.Endpoints.Token,
        Permissions.Endpoints.EndSession,
        Permissions.GrantTypes.AuthorizationCode,
        Permissions.GrantTypes.RefreshToken,
        Permissions.ResponseTypes.Code,
        Permissions.Scopes.Email,
        Permissions.Scopes.Profile,
        Permissions.Scopes.Roles,
        Permissions.Prefixes.Scope + PortalScopes.Groups,
    ];

    private static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
}
