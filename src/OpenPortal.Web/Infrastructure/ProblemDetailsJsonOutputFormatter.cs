using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// Writes <see cref="ProblemDetails"/> as <c>application/problem+json</c>, ahead of the default JSON
/// formatter.
/// <para>
/// RFC 9457 gives the error payload a media type of its own precisely so a client can recognise it without
/// inspecting the status code. Declaring the type on the <see cref="ObjectResult"/> is not enough: when the
/// request carries no <c>Accept</c> header, MVC picks the selected formatter's <em>default</em> content type
/// and quietly answers <c>application/json</c> instead. Pinning it in a formatter that is consulted first
/// makes the type independent of what the caller happened to send, so every error body has one shape.
/// </para>
/// <para>
/// Serialisation reuses MVC's own JSON options, so a change to naming policy or converters applies here too.
/// </para>
/// </summary>
public sealed class ProblemDetailsJsonOutputFormatter : TextOutputFormatter
{
    public const string ProblemJson = "application/problem+json";

    private readonly JsonSerializerOptions _options;

    public ProblemDetailsJsonOutputFormatter(IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonOptions);

        _options = jsonOptions.Value.JsonSerializerOptions;

        SupportedMediaTypes.Add(ProblemJson);
        SupportedEncodings.Add(Encoding.UTF8);
    }

    protected override bool CanWriteType(Type? type) =>
        typeof(ProblemDetails).IsAssignableFrom(type);

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selectedEncoding);

        // The encoding is honoured rather than assumed: a client that negotiated one has a right to it.
        var stream = new MemoryStream();
        await JsonSerializer
            .SerializeAsync(stream, context.Object, context.Object!.GetType(), _options)
            .ConfigureAwait(false);

        var body = stream.ToArray();
        context.HttpContext.Response.ContentLength = body.Length;

        await context.HttpContext.Response.Body.WriteAsync(body).ConfigureAwait(false);
    }
}