namespace OpenPortal.Access.Application.Notifications;

/// <summary>
/// The notifications the Access module sends. Each needs <c>notification.&lt;type&gt;.title</c> and <c>.body</c>
/// labels in the resource files (a test checks it); the parameters each one fills are listed beside it.
/// </summary>
public static class AccessNotificationTypes
{
    /// <summary>To the reviewers: someone asks for an application (<c>user</c>, <c>application</c>, <c>reason</c>).</summary>
    public const string RequestSubmitted = "access.request_submitted";

    /// <summary>To the requester (<c>application</c>, <c>note</c>).</summary>
    public const string RequestApproved = "access.request_approved";

    /// <summary>To the requester (<c>application</c>, <c>note</c>).</summary>
    public const string RequestRejected = "access.request_rejected";

    /// <summary>To the user: an application was granted to them personally (<c>application</c>).</summary>
    public const string ApplicationGranted = "access.application_granted";

    /// <summary>To the user: they were added to a group (<c>group</c>).</summary>
    public const string GroupJoined = "access.group_joined";

    /// <summary>To the application reviewers: an application announced itself and waits (<c>application</c>, <c>clientId</c>).</summary>
    public const string ApplicationAnnounced = "application.announced";

    /// <summary>To the user: they were added to an organization (<c>organization</c>, <c>role</c>).</summary>
    public const string OrganizationJoined = "organization.joined";
}

/// <summary>The kinds of work Access notices are addressed to; the host maps each to the people who may do it.</summary>
public static class AccessNotificationRoles
{
    /// <summary>Whoever may approve or reject access requests.</summary>
    public const string RequestReviewers = "access.request_reviewers";

    /// <summary>Whoever may approve applications.</summary>
    public const string ApplicationReviewers = "access.application_reviewers";
}
