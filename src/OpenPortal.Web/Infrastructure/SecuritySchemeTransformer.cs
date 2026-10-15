using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// Describes the two ways to call the portal's API in the generated OpenAPI document: the browser's session cookie,
/// or a personal access token (<c>Authorization: Bearer opp_…</c>) for scripts and command-line tools.
/// <para>
/// Without this the document would declare no security scheme at all, and anyone generating a client from
/// it would see every endpoint as anonymously callable.
/// </para>
/// </summary>
public sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "cookieAuth";
    private const string TokenSchemeName = "personalAccessToken";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // Both collections are null until something writes to them.
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Security ??= [];

        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            // ApiKey in the Cookie header: that is literally how the browser transmits the session.
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "OpenPortal.Auth",
            Description = "Session cookie issued by POST /api/auth/login.",
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document, null)] = [],
        };

        document.Security.Add(requirement);

        document.Components.SecuritySchemes[TokenSchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "A personal access token (opp_…) created in Account > Tokens, while the portal allows them. "
                + "Scope read allows GET requests only; write allows everything else too. Never accepted for the "
                + "account's password, second factor, tokens, data download or deletion.",
        };

        // A separate requirement: either the cookie or the token.
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(TokenSchemeName, document, null)] = [],
        });

        return Task.CompletedTask;
    }
}