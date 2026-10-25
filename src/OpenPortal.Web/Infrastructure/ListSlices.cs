using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// Answers a list endpoint read in slices (<c>?search=&amp;skip=&amp;take=</c>): the body stays the array it always was,
/// and <see cref="TotalCountHeader"/> says how many entries match in all, so a screen can page through them.
/// </summary>
internal static class ListSlices
{
    public const string TotalCountHeader = "X-Total-Count";

    public static ActionResult<IReadOnlyList<T>> Respond<T>(ControllerBase controller, Result<SliceDto<T>> result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure)
        {
            return ProblemResults.FromResult(controller.HttpContext, result);
        }

        controller.Response.Headers[TotalCountHeader] = result.Value.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return controller.Ok(result.Value.Items);
    }
}
