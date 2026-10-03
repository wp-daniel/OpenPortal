namespace OpenPortal.Identity.Application.Contracts;

/// <summary>A slice of a larger result set, produced by list endpoints.</summary>
/// <param name="Items">The rows for the requested page.</param>
/// <param name="Page">One-based page number that produced <paramref name="Items"/>.</param>
/// <param name="PageSize">Maximum number of rows per page.</param>
/// <param name="TotalCount">Total number of rows matching the query, across all pages.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}