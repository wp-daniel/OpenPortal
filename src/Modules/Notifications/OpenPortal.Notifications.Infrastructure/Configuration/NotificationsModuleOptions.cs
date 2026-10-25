namespace OpenPortal.Notifications.Infrastructure.Configuration;

/// <summary>Settings for the Notifications module, bound from the <c>Notifications</c> configuration section.</summary>
public sealed class NotificationsModuleOptions
{
    public const string SectionName = "Notifications";

    /// <summary>How long a notification is kept once read. Swept once a day; 0 keeps them.</summary>
    public int ReadRetentionDays { get; set; } = 30;

    /// <summary>How long a notification is kept at all, read or not; 0 keeps them.</summary>
    public int RetentionDays { get; set; } = 180;

    /// <summary>
    /// The most recipients one notice reaches. A role that names more people than this (a portal where everybody
    /// reviews requests) notifies the first ones only, so one event cannot write an unbounded number of rows.
    /// </summary>
    public int MaxRecipientsPerNotice { get; set; } = 500;

    /// <summary>How often someone who chose the digest receives it.</summary>
    public int DigestIntervalHours { get; set; } = 24;
}
