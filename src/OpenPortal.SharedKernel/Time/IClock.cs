namespace OpenPortal.SharedKernel.Time;

/// <summary>
/// Abstraction over the system clock so that time-dependent rules (audit stamps, expiry) are testable
/// without waiting for wall-clock time to pass.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}