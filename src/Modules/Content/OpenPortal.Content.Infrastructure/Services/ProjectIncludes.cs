using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using OpenPortal.Content.Domain.Projects;

namespace OpenPortal.Content.Infrastructure.Services;

/// <summary>
/// Query fragments shared by the Content services.
/// <para>
/// These live in one place because both the public and the management read paths must shape and order
/// projects identically: if they diverged, a project could appear in one list and not the other, or appear
/// in a different position depending on which endpoint the client called.
/// </para>
/// </summary>
internal static class ProjectIncludes
{
    /// <summary>
    /// Eager-loads the technology names. Required whenever a project is projected, because
    /// <c>Technology</c> is a shared row and is not reachable without the join.
    /// </summary>
    public static IIncludableQueryable<Project, Technology> WithTechnologies(this IQueryable<Project> query) =>
        query.Include(project => project.Technologies)
            .ThenInclude(link => link.Technology);

    /// <summary>
    /// Applies the canonical display order: explicitly positioned projects first, then the rest.
    /// <para>
    /// The position test is written out rather than relying on a plain <c>OrderBy</c> on the nullable
    /// column, because NULL ordering is provider-dependent: SQLite sorts NULLs lowest ascending while
    /// PostgreSQL sorts them highest. Without the explicit test, switching provider would silently reorder
    /// the public page.
    /// </para>
    /// </summary>
    public static IOrderedQueryable<Project> ApplyDisplayOrder(this IQueryable<Project> query) =>
        query
            .OrderBy(project => project.Position.HasValue ? 0 : 1)
            .ThenBy(project => project.Position)
            .ThenByDescending(project => project.StartedOn);
}