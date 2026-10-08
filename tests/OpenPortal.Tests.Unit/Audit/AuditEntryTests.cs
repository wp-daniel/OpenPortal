using OpenPortal.Audit.Domain;
using OpenPortal.SharedKernel.Auditing;

namespace OpenPortal.Tests.Unit.Audit;

public sealed class AuditEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_entry_keeps_the_event_as_given()
    {
        var entry = AuditEntry.Record(
            Guid.NewGuid(),
            Now,
            " user.created ",
            AuditOutcome.Success,
            new AuditSubject(AuditSubjectTypes.User, "actor-id", "admin@example.com"),
            new AuditSubject(AuditSubjectTypes.User, "target-id", "new@example.com"),
            """{"administrator":"false"}""",
            new AuditOrigin("10.0.0.1", "Mozilla/5.0", "trace-1"));

        entry.Action.ShouldBe("user.created");
        entry.ActorLabel.ShouldBe("admin@example.com");
        entry.TargetId.ShouldBe("target-id");
        entry.Details.ShouldBe("""{"administrator":"false"}""");
        entry.IpAddress.ShouldBe("10.0.0.1");
        entry.CorrelationId.ShouldBe("trace-1");
    }

    [Fact]
    public void Long_values_are_clipped_rather_than_refused()
    {
        // Labels and user agents come from callers; a security event must be recorded even when they are huge.
        var entry = AuditEntry.Record(
            Guid.NewGuid(),
            Now,
            "auth.sign_in",
            AuditOutcome.Failure,
            new AuditSubject(AuditSubjectTypes.User, string.Empty, new string('x', 5000)),
            target: null,
            details: "{\"reason\":\"" + new string('y', 5000) + "\"}",
            new AuditOrigin(null, new string('z', 5000), null));

        entry.ActorLabel!.Length.ShouldBe(AuditEntry.LabelMaxLength);
        entry.ActorId.ShouldBeNull("an empty id means there was no account to name");
        entry.UserAgent!.Length.ShouldBe(AuditEntry.UserAgentMaxLength);
        entry.Details.ShouldBe("""{"truncated":"true"}""", "clipped JSON would be unreadable");
        entry.TargetType.ShouldBeNull();
    }

    [Theory]
    [InlineData("user.created", "user")]
    [InlineData("auth.sign_in", "auth")]
    [InlineData("standalone", "standalone")]
    public void The_category_is_the_part_before_the_first_dot(string action, string category)
    {
        AuditEntry.CategoryOf(action).ShouldBe(category);
    }
}
