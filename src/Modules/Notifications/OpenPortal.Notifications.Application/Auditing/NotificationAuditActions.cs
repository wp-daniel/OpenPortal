namespace OpenPortal.Notifications.Application.Auditing;

/// <summary>The audit action codes the Notifications module records. Each needs <c>audit.action.&lt;code&gt;</c> in the resources.</summary>
public static class NotificationAuditActions
{
    public const string AnnouncementCreated = "announcement.created";
    public const string AnnouncementUpdated = "announcement.updated";
    public const string AnnouncementDeleted = "announcement.deleted";
    public const string MailSettingsUpdated = "mail.settings_updated";
    public const string MailTestSent = "mail.test_sent";
    public const string OutboxRetried = "outbox.retried";
    public const string OutboxDeleted = "outbox.deleted";
}
