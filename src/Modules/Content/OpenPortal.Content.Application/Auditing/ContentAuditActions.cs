namespace OpenPortal.Content.Application.Auditing;

/// <summary>
/// The audit actions the Content module records. Each needs an <c>audit.action.&lt;code&gt;</c> label in the
/// resource files (a test checks it).
/// </summary>
public static class ContentAuditActions
{
    public const string ProfileUpdated = "content.profile_updated";

    public const string ProjectCreated = "content.project_created";

    public const string ProjectUpdated = "content.project_updated";

    public const string ProjectPublished = "content.project_published";

    public const string ProjectUnpublished = "content.project_unpublished";

    public const string ProjectDeleted = "content.project_deleted";
}
