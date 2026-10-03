using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// Describes the cookie authentication scheme in the generated OpenAPI document.
/// <para>
/// Without this the document would declare no security scheme at all, and anyone generating a client from
/// it would see every endpoint as anonymously callable.
/// </para>
/// </summary>
public sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "cookieAuth";

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

        return Task.CompletedTask;
    }
}