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

    /// <summary>The OpenIddict application property carrying the back-channel logout URI, read by the host.</summary>
    public const string BackChannelLogoutProperty = "backchannel_logout_uri";

    /// <summary>
    /// Creates the client, or replaces its secret, and returns the new secret in clear; null for a public client,
    /// which is created without one.
    /// </summary>
    public async Task<string?> IssueSecretAsync(PortalApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (application.IsPublic)
        {
            await CreatePublicAsync(application, cancellationToken).ConfigureAwait(false);
            return null;
        }

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

    private async Task CreatePublicAsync(PortalApplication application, CancellationToken cancellationToken)
    {
        if (await _applications.FindByClientIdAsync(application.ClientId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = application.ClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
        };

        Describe(descriptor, application);
        await _applications.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);
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

        var wanted = PermissionsFor(application);
        var held = await _applications.GetPermissionsAsync(client, cancellationToken).ConfigureAwait(false);

        // Both ways: an API scope switched off must leave the client too.
        if (wanted.SetEquals(held))
        {
            return false;
        }

        await SyncAsync(application, cancellationToken).ConfigureAwait(false);
        return true;
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

    /// <summary>Revokes every authorization and token issued to the user, in every application.</summary>
    public async Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var subject = userId.ToString();

        await _authorizations.RevokeAsync(subject, client: null, status: null, type: null, cancellationToken).ConfigureAwait(false);
        await _tokens.RevokeAsync(subject, client: null, status: null, type: null, cancellationToken).ConfigureAwait(false);
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

        // Authorization code with PKCE plus refresh tokens is the only flow offered to users: it is the right one
        // for a server-rendered or BFF application, and every other user flow is a way around the access check.
        // Client credentials is added only for an application given API scopes, and carries no user.
        descriptor.Permissions.Clear();
        descriptor.Permissions.UnionWith(PermissionsFor(application));

        descriptor.Requirements.Clear();
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        // Read by the portal when a user signs out, to tell the application (OpenID Connect Back-Channel Logout).
        descriptor.Properties.Remove(BackChannelLogoutProperty);
        if (application.BackChannelLogoutUri is { } logoutUri)
        {
            descriptor.Properties[BackChannelLogoutProperty] = System.Text.Json.JsonSerializer.SerializeToElement(logoutUri);
        }
    }

    /// <summary>The OpenIddict permissions the application's client should hold today.</summary>
    private static HashSet<string> PermissionsFor(PortalApplication application)
    {
        var permissions = new HashSet<string>(UserFlowPermissions, StringComparer.Ordinal);

        // Only a client that can authenticate may ask what a token means (introspection).
        if (!application.IsPublic)
        {
            permissions.Add(Permissions.Endpoints.Introspection);
        }

        if (application.ApiScopes.Count > 0)
        {
            permissions.Add(Permissions.GrantTypes.ClientCredentials);
            permissions.UnionWith(application.ApiScopes.Select(scope => Permissions.Prefixes.Scope + scope));
        }

        return permissions;
    }

    /// <summary>
    /// Authorization code with PKCE plus refresh tokens, and the profile, email, roles, groups and organization
    /// scopes. Which groups an application actually receives, and whether it hears about organizations, is decided
    /// by its own settings, not by the scope.
    /// </summary>
    private static readonly string[] UserFlowPermissions =
    [
        Permissions.Endpoints.Authorization,
        Permissions.Endpoints.Token,
        Permissions.Endpoints.EndSession,
        Permissions.Endpoints.Revocation,
        Permissions.GrantTypes.AuthorizationCode,
        Permissions.GrantTypes.RefreshToken,
        Permissions.ResponseTypes.Code,
        Permissions.Scopes.Email,
        Permissions.Scopes.Profile,
        Permissions.Scopes.Roles,
        Permissions.Prefixes.Scope + PortalScopes.Groups,
        Permissions.Prefixes.Scope + PortalScopes.Organization,
    ];

    private static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
}
