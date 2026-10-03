using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Options;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// Inserts <see cref="ProblemDetailsJsonOutputFormatter"/> at the head of MVC's formatter list.
/// <para>
/// Registration goes through <see cref="IConfigureOptions{TOptions}"/> rather than
/// <c>AddMvcOptions</c> because the formatter needs MVC's JSON options, and those come from the container:
/// by the time <c>AddMvcOptions</c> runs there is nothing to resolve them from.
/// </para>
/// </summary>
public sealed class ConfigureProblemDetailsFormatter(
    IOptions<JsonOptions> jsonOptions) : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jsonOptions);

        if (options.OutputFormatters.Any(formatter => formatter is ProblemDetailsJsonOutputFormatter))
        {
            return;
        }

        options.OutputFormatters.Insert(0, new ProblemDetailsJsonOutputFormatter(jsonOptions));
    }
}