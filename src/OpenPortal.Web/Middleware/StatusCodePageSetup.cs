using OpenPortal.Web.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OpenPortal.Web.Middleware;

/// <summary>
/// Fills in the body of an empty error response so that one host can serve both a JSON client and a browser.
/// <para>
/// Two audiences share this application. An unmatched <c>/api/...</c> route must produce a ProblemDetails
/// body that a client can parse; an unmatched page route must produce the static shell, so a deep link still
/// loads the application instead of a bare 404. A single default behaviour cannot be right for both.
/// </para>
/// <para>
/// This also gives the 401 and 403 produced by the authentication and authorization middlewares a body,
/// which they otherwise leave empty.
/// </para>
/// </summary>
public static class StatusCodePageSetup
{
    public static IApplicationBuilder UseOpenPortalStatusCodePages(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseStatusCodePages(HandleAsync);
    }

    private static async Task HandleAsync(StatusCodeContext statusCodeContext)
    {
        ArgumentNullException.ThrowIfNull(statusCodeContext);

        var context = statusCodeContext.HttpContext;

        // A body may already have been written; appending to it would corrupt the response.
        if (context.Response.HasStarted)
        {
            return;
        }

        if (IsApiRequest(context))
        {
            await WriteProblemDetailsAsync(context).ConfigureAwait(false);

            return;
        }

        var errorPage = Path.Combine(
            context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath ?? string.Empty,
            "error.html");

        if (!File.Exists(errorPage))
        {
            // Nothing to serve: leave the response as it is rather than failing inside the error handler.
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(errorPage).ConfigureAwait(false);
    }

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    private static Task WriteProblemDetailsAsync(HttpContext context)
    {
        var status = context.Response.StatusCode;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = context.Localize("error." + ErrorCodeFor(status), TitleFor(status)),
            Instance = context.Request.Path,
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["errorCode"] = ErrorCodeFor(status);

        // The content type is passed explicitly because WriteAsJsonAsync otherwise writes plain
        // application/json, and clients are entitled to branch on application/problem+json per RFC 9457.
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "The request is not valid.",
        StatusCodes.Status401Unauthorized => "Authentication is required.",
        StatusCodes.Status403Forbidden => "Access is denied.",
        StatusCodes.Status404NotFound => "The requested resource was not found.",
        StatusCodes.Status409Conflict => "The request conflicts with the current state.",
        StatusCodes.Status413PayloadTooLarge => "The request payload is too large.",
        StatusCodes.Status415UnsupportedMediaType => "The request media type is not supported.",
        StatusCodes.Status429TooManyRequests => "Too many requests.",
        _ => status >= StatusCodes.Status500InternalServerError
            ? "The server could not complete the request."
            : "The request could not be completed.",
    };

    /// <summary>
    /// Stable codes so a client can branch on the failure without matching on status codes alone.
    /// </summary>
    private static string ErrorCodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "http.bad_request",
        StatusCodes.Status401Unauthorized => "identity.not_authenticated",
        StatusCodes.Status403Forbidden => "identity.forbidden",
        StatusCodes.Status404NotFound => "http.route_not_found",
        StatusCodes.Status409Conflict => "http.conflict",
        StatusCodes.Status413PayloadTooLarge => "http.payload_too_large",
        StatusCodes.Status415UnsupportedMediaType => "http.unsupported_media_type",
        StatusCodes.Status429TooManyRequests => "http.too_many_requests",
        _ => "server.error",
    };
}