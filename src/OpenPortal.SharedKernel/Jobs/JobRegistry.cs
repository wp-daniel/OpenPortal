using System.Collections.Concurrent;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.SharedKernel.Jobs;

/// <summary>
/// The portal's recurring background work (retention sweeps, expiry, digests): what exists, how often it runs, and how
/// its last run on this instance went. Each job registers itself and runs through <see cref="RunAsync"/>, which records
/// the outcome; an administrator can see the list and run a job now.
/// </summary>
public interface IJobRegistry
{
    /// <summary>Declares a job. Registering a name again replaces it (a scoped host rebuilt in a test).</summary>
    void Register(string name, TimeSpan interval, Func<CancellationToken, Task<int>> run);

    /// <summary>Runs the job now, records how it went and returns how many items it handled. Rethrows a failure.</summary>
    Task<int> RunAsync(string name, CancellationToken cancellationToken);

    IReadOnlyList<JobStatus> Snapshot();
}

/// <param name="Name">Dotted, stable name such as <c>audit.retention</c>.</param>
/// <param name="LastResult">How many items the last successful run handled (rows deleted, digests sent).</param>
/// <param name="LastError">The last failure's message; null after a success.</param>
public sealed record JobStatus(
    string Name,
    TimeSpan Interval,
    DateTimeOffset? LastStartedAtUtc,
    TimeSpan? LastDuration,
    bool? LastSucceeded,
    int? LastResult,
    string? LastError,
    int Runs,
    int Failures);

/// <summary>An in-memory <see cref="IJobRegistry"/>: per instance, and forgotten at restart, like the jobs' own timers.</summary>
public sealed class JobRegistry : IJobRegistry
{
    private readonly ConcurrentDictionary<string, Entry> _jobs = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    public JobRegistry(IClock clock)
    {
        _clock = clock;
    }

    public void Register(string name, TimeSpan interval, Func<CancellationToken, Task<int>> run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(run);

        _jobs.AddOrUpdate(
            name,
            _ => new Entry(interval, run),
            (_, existing) =>
            {
                existing.Interval = interval;
                existing.Run = run;
                return existing;
            });
    }

    public async Task<int> RunAsync(string name, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(name, out var entry))
        {
            throw new KeyNotFoundException($"There is no job named {name}.");
        }

        var started = _clock.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        lock (entry)
        {
            entry.LastStartedAtUtc = started;
        }

        try
        {
            var result = await entry.Run(cancellationToken).ConfigureAwait(false);
            lock (entry)
            {
                entry.Finish(stopwatch.Elapsed, true, result, null);
            }

            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            lock (entry)
            {
                entry.Finish(stopwatch.Elapsed, false, null, exception.Message);
            }

            throw;
        }
    }

    public IReadOnlyList<JobStatus> Snapshot() =>
        _jobs.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                lock (pair.Value)
                {
                    var entry = pair.Value;
                    return new JobStatus(
                        pair.Key,
                        entry.Interval,
                        entry.LastStartedAtUtc,
                        entry.LastDuration,
                        entry.LastSucceeded,
                        entry.LastResult,
                        entry.LastError,
                        entry.Runs,
                        entry.Failures);
                }
            })
            .ToList();

    private sealed class Entry(TimeSpan interval, Func<CancellationToken, Task<int>> run)
    {
        public TimeSpan Interval { get; set; } = interval;

        public Func<CancellationToken, Task<int>> Run { get; set; } = run;

        public DateTimeOffset? LastStartedAtUtc { get; set; }

        public TimeSpan? LastDuration { get; private set; }

        public bool? LastSucceeded { get; private set; }

        public int? LastResult { get; private set; }

        public string? LastError { get; private set; }

        public int Runs { get; private set; }

        public int Failures { get; private set; }

        public void Finish(TimeSpan duration, bool succeeded, int? result, string? error)
        {
            LastDuration = duration;
            LastSucceeded = succeeded;
            LastResult = succeeded ? result : LastResult;
            LastError = error;
            Runs++;
            Failures += succeeded ? 0 : 1;
        }
    }
}
