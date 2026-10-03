using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Content.Infrastructure.DependencyInjection;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Infrastructure.Configuration;
using OpenPortal.Identity.Infrastructure.DependencyInjection;
using OpenPortal.Web;
using OpenPortal.Web.Authentication;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Localization;
using OpenPortal.Web.Middleware;
using OpenPortal.Web.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database provider selection.
//
// The provider and connection string are read and validated here, because this is the only place allowed
// to know which engine is in use. Both module contexts are registered through the selector, so nothing
// downstream depends on the choice.
// ---------------------------------------------------------------------------
builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        $"Database:{nameof(DatabaseOptions.ConnectionString)} must be configured.")
    .ValidateOnStart();

var database = new DatabaseOptions
{
    Provider = Enum.TryParse<DatabaseProvider>(
        builder.Configuration[$"{DatabaseOptions.SectionName}:Provider"],
        ignoreCase: true,
        out var parsed)
        ? parsed
        : DatabaseProvider.Sqlite,
    ConnectionString = builder.Configuration.GetConnectionString("OpenPortal")
        ?? builder.Configuration[$"{DatabaseOptions.SectionName}:ConnectionString"]
        ?? string.Empty,
    MigrateOnStartup = builder.Configuration.GetValue(
        $"{DatabaseOptions.SectionName}:MigrateOnStartup",
        builder.Environment.IsDevelopment()),
};

if (string.IsNullOrWhiteSpace(database.ConnectionString))
{
    throw new InvalidOperationException(
        "No database connection string is configured. Set ConnectionStrings:OpenPortal or "
        + $"{DatabaseOptions.SectionName}:ConnectionString.");
}

builder.Services.Configure<DatabaseOptions>(options =>
{
    options.Provider = database.Provider;
    options.ConnectionString = database.ConnectionString;
    options.MigrateOnStartup = database.MigrateOnStartup;
});

builder.Services.AddOptions<BootstrapAdminOptions>()
    .Bind(builder.Configuration.GetSection(BootstrapAdminOptions.SectionName));

// Languages, request culture and the resource-based message catalog. Registered before the modules
// because the Identity module validates a saved language against ILanguageCatalog.
builder.Services.AddOpenPortalLocalization(builder.Configuration);

// ---------------------------------------------------------------------------
// Modules.
//
// Each module registers its own services. The host supplies only what belongs to the host: the
// database provider, the current-user adapter, and the answer to "may this caller edit content".
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<IContentEditAuthorization, RoleContentEditAuthorization>();

builder.Services.AddIdentityModule(
    builder.Configuration,
    options => DatabaseProviderSelector.Configure(options, database.Provider, database.ConnectionString));

builder.Services.AddContentModule(options =>
    DatabaseProviderSelector.Configure(options, database.Provider, database.ConnectionString));

// ---------------------------------------------------------------------------
// Web stack.
//
// There is no Razor and no view engine: this host serves a JSON API plus the compiled Vite output in
// wwwroot. In development SpaProxy forwards to the Vite dev server, so the browser talks to one origin
// and the session cookie stays first-party.
// ---------------------------------------------------------------------------
// Pins application/problem+json for every error body. Registered as an options configurator rather than
// inside AddControllers because the formatter needs MVC's JSON options from the container.
builder.Services.AddSingleton<IConfigureOptions<MvcOptions>, ConfigureProblemDetailsFormatter>();

builder.Services
    .AddControllers(options => options.Filters.Add<ValidateAntiforgeryTokenFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        // An [ApiController] action rejects a malformed or incomplete body before it ever runs, and it does
        // so through this factory. Left at the default it would emit no errorCode extension, which is the
        // field clients branch on.
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = context.HttpContext.Localize("error.http.validation_failed", "The request is not valid."),
                Instance = context.HttpContext.Request.Path,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            };

            // DataAnnotations messages on the request contracts are resource keys (validation.*), because
            // those contracts live in modules that must not depend on localization. Resolve them here;
            // anything that is not a known key (a binder's own message) is passed through untouched.
            foreach (var field in problem.Errors.Keys.ToArray())
            {
                problem.Errors[field] = problem.Errors[field]
                    .Select(message => message.StartsWith("validation.", StringComparison.Ordinal)
                        ? context.HttpContext.Localize(message, message)
                        : message)
                    .ToArray();
            }

            problem.Extensions["errorCode"] = "http.validation_failed";
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddOpenPortalAuthorization();

// Antiforgery for a cookie-authenticated API. The token cookie is readable by script on purpose: it is
// a per-session nonce, not a credential, and the client echoes it in a header so a cross-site form post
// cannot forge a state-changing request. The session cookie itself stays HttpOnly.
var requireSecureCookies = builder.Configuration.GetValue(
    $"{IdentityModuleOptions.SectionName}:Cookie:RequireSecure",
    !builder.Environment.IsDevelopment());

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = AntiforgeryDefaults.CookieName;
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = requireSecureCookies
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.HeaderName = AntiforgeryDefaults.HeaderName;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DatabaseInitializer>();

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<SecuritySchemeTransformer>());

var app = builder.Build();

// ---------------------------------------------------------------------------
// Startup work.
//
// Roles are always provisioned. Migrations run only when configured, because several instances
// migrating concurrently is a race; in production that belongs in a separate, ordered deployment step.
// ---------------------------------------------------------------------------
// The initializer depends on scoped services (both DbContexts, UserManager, RoleManager), so it runs
// inside its own scope rather than being resolved from the root provider.
//
// A bootstrap failure is deliberately allowed to propagate. Catching it here and exiting quietly would be
// friendlier for a human running `dotnet run`, but it hides the cause from every host that embeds this
// program - a test host would report only "the server has not been started" - and turning a specific,
// actionable error into a generic one is a bad trade. BootstrapAdminException therefore carries the
// diagnosis in its message instead.
await using (var startupScope = app.Services.CreateAsyncScope())
{
    await startupScope.ServiceProvider
        .GetRequiredService<DatabaseInitializer>()
        .InitialiseAsync(CancellationToken.None)
        .ConfigureAwait(false);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// In development, Microsoft.AspNetCore.SpaProxy forwards unmatched requests to the Vite dev server. It is
// wired automatically by the package's hosting startup, driven by the SpaRoot/SpaProxyLaunchCommand/
// SpaProxyServerUrl properties in the csproj, so there is nothing to call here. Routing through this origin
// rather than pointing the browser at Vite directly is what keeps the session cookie first-party: a
// two-origin dev setup would break every SameSite=Strict cookie request.

app.UseDefaultFiles();
app.UseStaticFiles();

// Before the exception handler, so even an error body is written in the caller's language.
app.UseRequestLocalization();

app.UseExceptionHandler();

// Gives an empty 401/403/404 a body, choosing ProblemDetails for /api and the static shell for a page.
app.UseOpenPortalStatusCodePages();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapOpenApi();

// Everything else is a client-side route: return the built SPA shell so a hard refresh or a deep link
// works.
//
// An unmatched /api path must NOT reach here. Answering it with index.html would turn a typo in a client
// call, or a route that was renamed, into a 200 carrying HTML: the caller would then try to parse an HTML
// page as JSON and report a confusing parse failure instead of a 404 it could act on.
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = ProblemResults.ProblemJson;

        await context.Response
            .WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = context.Localize("error.http.endpoint_not_found", "The requested API endpoint does not exist."),
                    Instance = context.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc9110#section-10.2.2",
                    Extensions = { ["errorCode"] = "http.endpoint_not_found" },
                },
                options: null,
                contentType: ProblemResults.ProblemJson)
            .ConfigureAwait(false);

        return;
    }

    var indexPath = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");

    if (!File.Exists(indexPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexPath).ConfigureAwait(false);
});

await app.RunAsync();

/// <summary>
/// Declared so <c>WebApplicationFactory&lt;Program&gt;</c> can boot this exact host in integration tests.
/// </summary>
public partial class Program;