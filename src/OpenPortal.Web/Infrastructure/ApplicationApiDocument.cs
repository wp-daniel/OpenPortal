using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Controllers;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// The OpenAPI document for the applications that sign in through the portal (<c>/openapi/apps.json</c>, shown at
/// <c>/scalar</c>): how users sign in to them (OpenID Connect), how their backend gets a token of its own and what
/// it can do with it (the application API), and the announcement endpoint. The portal's own administration API
/// is deliberately left out.
/// <para>
/// The OpenID Connect endpoints are written by hand here, because they are protocol endpoints that OpenIddict
/// drives and ASP.NET Core's API explorer cannot describe (form bodies, redirects, protocol errors).
/// </para>
/// </summary>
public sealed class ApplicationApiDocument : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    public const string DocumentName = "apps";

    /// <summary>The routes the document covers, besides the hand-written protocol endpoints.</summary>
    public const string RoutePrefix = "api/apps/";

    public const string OAuth2Scheme = "oauth2";

    public const string ProvisioningKeyScheme = "provisioningKey";

    private const string SignInTag = "Sign-in (OpenID Connect)";
    private const string ApplicationApiTag = "Application API";
    private const string AnnouncementTag = "Announcement";

    /// <summary>Only this document's routes; the portal's own API stays in the <c>v1</c> document.</summary>
    public static bool Includes(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription description) =>
        description.RelativePath?.StartsWith(RoutePrefix, StringComparison.OrdinalIgnoreCase) == true;

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        document.Info = new OpenApiInfo
        {
            Title = "OpenPortal for applications",
            Version = "v1",
            Description =
                "What an application needs to work with OpenPortal.\n\n"
                + "**Signing users in.** Send the browser to `/connect/authorize` (authorization code + PKCE), exchange the code at "
                + "`/connect/token`, refresh there too. The portal checks the user's access to the application on every token request.\n\n"
                + "**The application's own token.** Once an administrator enables API scopes for the application (Applications → "
                + "the application → API), its backend calls `/connect/token` with `grant_type=client_credentials`, its client id and "
                + "secret, and uses the access token as `Authorization: Bearer …` on `/api/apps/me/*`. No user and no portal screen "
                + "is involved. Scopes: `portal.users.read`, `portal.access.manage`, `portal.users.create`.\n\n"
                + "**Announcing.** `POST /api/apps/announce` with the provisioning key registers the application for approval.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[OAuth2Scheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Client credentials for the application API; authorization code with PKCE for users.",
            Flows = new OpenApiOAuthFlows
            {
                ClientCredentials = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri("/connect/token", UriKind.Relative),
                    Scopes = new Dictionary<string, string>
                    {
                        [ApplicationApiScopes.UsersRead] = "Read the users who can open the application.",
                        [ApplicationApiScopes.AccessManage] = "Grant the application to users, set their roles, withdraw it.",
                        [ApplicationApiScopes.UsersCreate] = "Create ordinary portal accounts (needs portal.access.manage too).",
                    },
                },
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri("/connect/authorize", UriKind.Relative),
                    TokenUrl = new Uri("/connect/token", UriKind.Relative),
                    RefreshUrl = new Uri("/connect/token", UriKind.Relative),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"] = "Sign the user in (an identity token).",
                        ["profile"] = "The user's name.",
                        ["email"] = "The user's email address.",
                        ["offline_access"] = "A refresh token.",
                        [PortalScopes.Roles] = "The user's roles in the application (role claims).",
                        [PortalScopes.Groups] = "The user's groups, as the application's setting allows (groups claim).",
                    },
                },
            },
        };

        document.Components.SecuritySchemes[ProvisioningKeyScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApplicationAnnouncementsController.ProvisioningKeyHeader,
            Description = "The portal's provisioning key (Access:ProvisioningKey), shared with the applications that announce themselves.",
        };

        // Replaced, not added to: the generator also lists one tag per controller, which the operations no longer use.
        document.Tags = new HashSet<OpenApiTag>
        {
            new() { Name = SignInTag, Description = "OpenID Connect endpoints: user sign-in and the application's own token." },
            new() { Name = ApplicationApiTag, Description = "Called by the application's backend with a client credentials token." },
            new() { Name = AnnouncementTag, Description = "Registers a running application for an administrator's approval." },
        };

        document.Paths ??= new OpenApiPaths();
        document.Paths["/.well-known/openid-configuration"] = Path(HttpMethod.Get, Discovery(document));
        document.Paths["/connect/authorize"] = Path(HttpMethod.Get, Authorize(document));
        document.Paths["/connect/token"] = Path(HttpMethod.Post, Token(document));
        document.Paths["/connect/userinfo"] = Path(HttpMethod.Get, UserInfo(document));
        document.Paths["/connect/endsession"] = Path(HttpMethod.Get, EndSession(document));

        return Task.CompletedTask;
    }

    /// <summary>Names the security each generated operation needs: the scope of its attribute, or the provisioning key.</summary>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (context.DocumentName != DocumentName)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<RequireApplicationScopeAttribute>().LastOrDefault() is { } required)
        {
            operation.Tags = new HashSet<OpenApiTagReference> { new(ApplicationApiTag, context.Document) };
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(OAuth2Scheme, context.Document)] =
                        required.Scope is null ? [] : [required.Scope],
                },
            ];
        }
        else if (context.Description.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller)
                 && controller == "ApplicationAnnouncements")
        {
            operation.Tags = new HashSet<OpenApiTagReference> { new(AnnouncementTag, context.Document) };
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(ProvisioningKeyScheme, context.Document)] = [],
                },
            ];

            // The key is described by the security scheme; as a parameter too it would be asked for twice.
            operation.Parameters = operation.Parameters?
                .Where(parameter => parameter.Name != ApplicationAnnouncementsController.ProvisioningKeyHeader)
                .ToList();
        }

        return Task.CompletedTask;
    }

    private static OpenApiPathItem Path(HttpMethod method, OpenApiOperation operation) => new()
    {
        Operations = new Dictionary<HttpMethod, OpenApiOperation> { [method] = operation },
    };

    private static OpenApiOperation Discovery(OpenApiDocument document) => new()
    {
        Tags = new HashSet<OpenApiTagReference> { new(SignInTag, document) },
        Summary = "Discovery document",
        Description = "Endpoints, signing keys (`jwks_uri`), grant types and scopes. Point your OpenID Connect library's authority at the portal's base URL and it reads this itself.",
        Responses = new OpenApiResponses
        {
            ["200"] = Json("The provider's metadata.", new OpenApiSchema { Type = JsonSchemaType.Object }),
        },
    };

    private static OpenApiOperation Authorize(OpenApiDocument document) => new()
    {
        Tags = new HashSet<OpenApiTagReference> { new(SignInTag, document) },
        Summary = "Sign a user in (browser redirect)",
        Description =
            "Send the user's browser here. The portal signs the user in if needed (password, then the authenticator code when two-factor "
            + "is on), checks that the user may open the application, and redirects to `redirect_uri` with `code` and `state`. A user "
            + "without access sees a portal page instead. Only the authorization code flow with PKCE (`S256`) is accepted. "
            + "The application API scopes are never granted here.",
        Parameters =
        [
            Query("client_id", "The application's client id.", required: true),
            Query("redirect_uri", "One of the application's registered redirect URIs.", required: true),
            Query("response_type", "Always `code`.", required: true, values: ["code"]),
            Query("scope", "Space-separated: `openid` plus any of `profile`, `email`, `offline_access`, `roles`, `groups`.", required: true),
            Query("code_challenge", "Base64url SHA-256 of the code verifier.", required: true),
            Query("code_challenge_method", "Always `S256`.", required: true, values: ["S256"]),
            Query("state", "Opaque value returned unchanged; protects against CSRF.", required: false),
            Query("nonce", "Bound into the identity token.", required: false),
            Query("prompt", "`login` forces a new sign-in, `none` fails with `login_required` instead of showing the sign-in page.", required: false),
        ],
        Responses = new OpenApiResponses
        {
            ["302"] = new OpenApiResponse { Description = "To `redirect_uri` with `code` and `state`, to the portal's sign-in page, or to its access-denied page." },
        },
    };

    private static OpenApiOperation Token(OpenApiDocument document) => new()
    {
        Tags = new HashSet<OpenApiTagReference> { new(SignInTag, document) },
        Summary = "Get a token",
        Description =
            "Three grants, each authenticated with the application's `client_id` and `client_secret` (form fields or HTTP Basic):\n\n"
            + "- `authorization_code`: `code`, `redirect_uri`, `code_verifier` → access, identity and (with `offline_access`) refresh token for the user.\n"
            + "- `refresh_token`: `refresh_token` → new tokens with the user's current roles and groups. Refused once the user's access is withdrawn.\n"
            + "- `client_credentials`: optional `scope` (application API scopes, all enabled ones when absent) → an access token for the "
            + "application itself, for `/api/apps/me/*`. Needs API scopes enabled for the application.\n\n"
            + "Access tokens last minutes (`expires_in`), so revoking access takes effect quickly.",
        RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/x-www-form-urlencoded"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Required = new HashSet<string> { "grant_type" },
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["grant_type"] = Text("The grant.", ["authorization_code", "refresh_token", "client_credentials"]),
                            ["client_id"] = Text("The application's client id."),
                            ["client_secret"] = Text("The application's client secret."),
                            ["code"] = Text("authorization_code: the code from the redirect."),
                            ["redirect_uri"] = Text("authorization_code: the redirect URI used on /connect/authorize."),
                            ["code_verifier"] = Text("authorization_code: the PKCE code verifier."),
                            ["refresh_token"] = Text("refresh_token: the refresh token."),
                            ["scope"] = Text("client_credentials: space-separated application API scopes."),
                        },
                    },
                },
            },
        },
        Responses = new OpenApiResponses
        {
            ["200"] = Json("The tokens.", new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["access_token"] = Text("Bearer token."),
                    ["token_type"] = Text("Always `Bearer`."),
                    ["expires_in"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Description = "Seconds until the access token expires." },
                    ["scope"] = Text("The granted scopes."),
                    ["id_token"] = Text("User grants with `openid`: the identity token."),
                    ["refresh_token"] = Text("User grants with `offline_access`: the refresh token."),
                },
            }),
            ["400"] = Json("A protocol error: `invalid_grant` (code, refresh token or access no longer valid), `invalid_scope`, `unauthorized_client`, `invalid_client`.", ProtocolError()),
        },
    };

    private static OpenApiOperation UserInfo(OpenApiDocument document) => new()
    {
        Tags = new HashSet<OpenApiTagReference> { new(SignInTag, document) },
        Summary = "The signed-in user's claims",
        Description = "With a user's access token: `sub`, plus `email`, `name`/`preferred_username`, `role` and `groups` for the scopes it was issued with.",
        Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(OAuth2Scheme, document)] = ["openid"] },
        ],
        Responses = new OpenApiResponses
        {
            ["200"] = Json("The user's claims.", new OpenApiSchema { Type = JsonSchemaType.Object }),
            ["401"] = new OpenApiResponse { Description = "Missing, expired or revoked token, or the account can no longer sign in." },
        },
    };

    private static OpenApiOperation EndSession(OpenApiDocument document) => new()
    {
        Tags = new HashSet<OpenApiTagReference> { new(SignInTag, document) },
        Summary = "Sign the user out (browser redirect)",
        Description = "Ends the portal session, then returns to `post_logout_redirect_uri` if it is registered for the application, otherwise to the portal's sign-in page.",
        Parameters =
        [
            Query("id_token_hint", "The identity token the application received.", required: false),
            Query("post_logout_redirect_uri", "One of the application's registered post-logout redirect URIs.", required: false),
            Query("state", "Returned unchanged.", required: false),
        ],
        Responses = new OpenApiResponses
        {
            ["302"] = new OpenApiResponse { Description = "To the post-logout redirect URI or the portal's sign-in page." },
        },
    };

    private static OpenApiParameter Query(string name, string description, bool required, string[]? values = null) => new()
    {
        Name = name,
        In = ParameterLocation.Query,
        Required = required,
        Description = description,
        Schema = Text(null, values),
    };

    private static OpenApiSchema Text(string? description, string[]? values = null) => new()
    {
        Type = JsonSchemaType.String,
        Description = description,
        Enum = values?.Select(value => (JsonNode)JsonValue.Create(value)!).ToList(),
    };

    private static OpenApiSchema ProtocolError() => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["error"] = Text("The OAuth error code."),
            ["error_description"] = Text("A human-readable explanation."),
        },
    };

    private static OpenApiResponse Json(string description, OpenApiSchema schema) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = schema } },
    };
}
