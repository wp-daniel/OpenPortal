namespace OpenPortal.SharedKernel.Notifications;

/// <summary>
/// Tells people in the portal that something happened that concerns them: an access request waiting for a
/// decision, a decision on their own request, an invitation accepted.
/// <para>
/// A port in the shared kernel, like <see cref="Auditing.IAuditTrail"/>: every module notifies at the point where
/// it knows what happened, without depending on the module that stores and shows the notifications. The host
/// supplies the implementation and decides who an <see cref="NoticeAudience"/> names.
/// </para>
/// <para>
/// Implementations never throw (other than for cancellation): the change being announced has already been
/// committed, and failing the request afterwards would invite a retry of something that succeeded. Call it
/// <em>after</em> the change is committed, never inside a transaction. The caller never notifies themselves:
/// the person whose action caused a notice is left out of its recipients.
/// </para>
/// </summary>
public interface INotifier
{
    Task NotifyAsync(Notice notice, CancellationToken cancellationToken);
}

/// <summary>One notification, for one audience.</summary>
public sealed record Notice
{
    /// <summary>
    /// Dotted, stable type such as <c>access.request_submitted</c>. Declared as a constant by the module that sends
    /// it; the client renders it as <c>notification.&lt;type&gt;.title</c> and <c>.body</c>, filled with
    /// <see cref="Parameters"/>, so the stored notice is independent of the reader's language.
    /// </summary>
    public required string Type { get; init; }

    public required NoticeAudience Audience { get; init; }

    /// <summary>The values the texts mention (an application name, a person). Never secrets.</summary>
    public IReadOnlyDictionary<string, string?>? Parameters { get; init; }

    /// <summary>
    /// Where the notice leads in the portal, as a local path such as <c>/admin/access-requests</c>. Anything that
    /// is not a local path is dropped.
    /// </summary>
    public string? Link { get; init; }

    /// <summary>
    /// Keeps the caller among the recipients. Normally whoever causes a notice is not told about it; this is for
    /// the rare notice the caller did not decide, such as the memberships an invitation gives the person
    /// accepting it.
    /// </summary>
    public bool IncludeCaller { get; init; }

    /// <summary>
    /// About the safety of the recipient's account (a new sign-in, a reset password, a support session): emailed at once
    /// to everyone who has not turned email off, whatever else they chose to receive.
    /// </summary>
    public bool Security { get; init; }
}

/// <summary>
/// Who receives a notice: some users by id, and/or everyone who may handle a kind of work.
/// </summary>
/// <param name="UserIds">Accounts named directly (the requester of an access request).</param>
/// <param name="Roles">
/// Kinds of work, declared by the sending module (for example <c>access.request_reviewers</c>); the host turns
/// each into the people allowed to do it, administrators included.
/// </param>
public sealed record NoticeAudience(IReadOnlyCollection<Guid> UserIds, IReadOnlyCollection<string> Roles)
{
    public static NoticeAudience Users(params Guid[] userIds) => new(userIds, []);

    public static NoticeAudience Holders(params string[] roles) => new([], roles);
}
