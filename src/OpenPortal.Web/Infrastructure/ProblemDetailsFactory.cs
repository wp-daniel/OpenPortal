using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Localization;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// Translates a <see cref="Result"/> into an HTTP response.
/// <para>
/// This is the single place where a domain failure becomes a status code. Centralising it guarantees that
/// the same error always produces the same response shape, and that a new <see cref="ErrorType"/> cannot be
/// added without deciding what the client should see.
/// </para>
/// </summary>
/// <summary>
/// Bridges the same payload into MVC controllers, which return <see cref="ActionResult"/> rather than
/// <see cref="IResult"/>. Both paths share <see cref="Create"/>, so a controller response and a minimal
/// API response to the same error are byte-identical.
/// </summary>
public static class ProblemResults
{
    /// <summary>
    /// RFC 9457 gives the error payload its own media type so a client can recognise it without reading the
    /// status code first. The type is set on the result rather than left to formatter negotiation, because
    /// negotiation would otherwise answer <c>application/json</c> whenever the client asked for that.
    /// </summary>
    public const string ProblemJson = "application/problem+json";

    public static ActionResult ToActionResult(this ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        return new ObjectResult(problem)
        {
            StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError,
            ContentTypes = { ProblemJson },
        };
    }

    /// <summary>Converts a failed result into an <see cref="ActionResult"/>.</summary>
    public static ActionResult FromResult(HttpContext context, Result result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        return ProblemDetailsFactory.Create(context, result.Error).ToActionResult();
    }

    /// <summary>Converts a failed <see cref="Result{TValue}"/> into an <see cref="ActionResult"/>.</summary>
    public static ActionResult FromResult<TValue>(HttpContext context, Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        return ProblemDetailsFactory.Create(context, result.Error).ToActionResult();
    }
}

public static class ProblemDetailsFactory
{
    /// <summary>Maps the error's type to the matching status code.</summary>
    public static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };

    /// <summary>
    /// Converts a failed result into a ProblemDetails payload carrying the error's stable code.
    /// <para>
    /// <c>errorCode</c> is the extension clients branch on. <c>detail</c> repeats the human-readable
    /// description, which is acceptable here because every description in the system is written for the
    /// account holder and none of them discloses whether another account exists.
    /// </para>
    /// </summary>
    public static ProblemDetails Create(HttpContext context, Error error, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        var statusCode = ToStatusCode(error.Type);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title ?? context.Localize("problem.title." + error.Type, DefaultTitle(error.Type)),
            // The description is the English fallback; a translation keyed by the stable error code wins.
            Detail = context.Localize("error." + error.Code, error.Description),
            Type = $"https://tools.ietf.org/html/rfc9110#section-15.{statusCode / 100}.{statusCode % 100}",
            Instance = context.Request.Path,
        };

        problem.Extensions["errorCode"] = error.Code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return problem;
    }

    /// <summary>
    /// Converts a failed result into an <see cref="IResult"/>. Validation failures are returned as 400 with
    /// a ProblemDetails body so that every error path in the API looks the same to a client.
    /// </summary>
    public static IResult ToResult(HttpContext context, Result result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return Results.Problem(problemDetails: Create(context, result.Error));
    }

    /// <summary>Converts a failed <see cref="Result{TValue}"/> into an <see cref="IResult"/>.</summary>
    public static IResult ToResult<TValue>(HttpContext context, Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return Results.Problem(problemDetails: Create(context, result.Error));
    }

    private static string DefaultTitle(ErrorType type) => type switch
    {
        ErrorType.Validation => "The request is not valid.",
        ErrorType.Unauthorized => "Authentication is required.",
        ErrorType.Forbidden => "Access is denied.",
        ErrorType.NotFound => "The requested resource was not found.",
        ErrorType.Conflict => "The request conflicts with the current state.",
        _ => "The request could not be completed.",
    };
}