using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// Last-resort handler for exceptions that escaped a controller.
/// <para>
/// Expected failures never reach here: they are returned as <see cref="Result"/> values and mapped to a
/// status code by <see cref="ProblemDetailsFactory"/>. What arrives is therefore always a defect, and the
/// response deliberately says nothing about it.
/// </para>
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, error) = Translate(exception);

        // The trace id is the only handle a client gets, and it is what correlates this line with the
        // detail recorded on the server.
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path} (trace {TraceId}).",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Request {Method} {Path} was rejected with {StatusCode} (trace {TraceId}).",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                httpContext.TraceIdentifier);
        }

        // Never try to write to a response that has already begun: doing so throws and replaces a useful
        // failure with a confusing one.
        if (httpContext.Response.HasStarted)
        {
            return true;
        }

        var problem = ProblemDetailsFactory.Create(httpContext, error);

        if (_environment.IsDevelopment() && statusCode >= StatusCodes.Status500InternalServerError)
        {
            problem.Extensions["exception"] = exception.ToString();
        }

        httpContext.Response.StatusCode = statusCode;

        // The content type is passed explicitly because WriteAsJsonAsync otherwise writes plain
        // application/json, and clients are entitled to branch on application/problem+json per RFC 9457.
        await httpContext.Response
            .WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private const int ClientClosedRequest = 499;

    /// <summary>
    /// Maps the few exception types whose meaning the API contract depends on. Anything unrecognised stays a
    /// 500: guessing a 400 for a genuine defect would teach clients to retry silently.
    /// </summary>
    private static (int StatusCode, Error Error) Translate(Exception exception) => exception switch
    {
        OperationCanceledException => (
            ClientClosedRequest,
            Error.Failure("request.cancelled", "The request was cancelled.")),

        // A missing or stale antiforgery token is the client's problem, not a server fault. Left unmapped
        // it would report 500, which tells the caller to retry a request that can never succeed and hides
        // the fact that the fix is to re-run the token handshake.
        AntiforgeryValidationException => (
            StatusCodes.Status400BadRequest,
            Error.Validation(
                "antiforgery.invalid_token",
                "The antiforgery token is missing, invalid or was issued for a different user.")),

        // A duplicate key or unique-index violation is a conflict the caller can act on, not a server fault.
        DbUpdateException => (
            StatusCodes.Status409Conflict,
            Error.Conflict(
                "persistence.conflict",
                "The change conflicts with data that already exists.")),

        _ => (
            StatusCodes.Status500InternalServerError,
            Error.Failure("server.unexpected_error", "An unexpected error occurred.")),
    };
}