using System.Diagnostics;
using System.Runtime;

namespace WhisperHeim.Services.Startup;

/// <summary>
/// Performs a single, deliberate Large Object Heap-compacting garbage
/// collection once the app has finished booting (task infrastructure-g3n5t).
///
/// <para>
/// Loading the Parakeet model and initializing WPF allocates large transient
/// buffers (model file reads, decode/setup scratch, XAML/resource init). Large
/// allocations land on the LOH, which is <em>not</em> compacted by default, so
/// once startup settles the managed heap can stay larger than the live set. A
/// one-shot blocking gen-2 collection with
/// <see cref="GCLargeObjectHeapCompactionMode.CompactOnce"/> returns that slack
/// to the OS without affecting steady-state behaviour.
/// </para>
///
/// <para>
/// This is intentionally a <em>one-shot</em> operation. A recurring forced GC
/// would hurt rather than help. It runs off the UI thread after a short
/// post-startup delay (see <see cref="ScheduleAsync"/>) so it never competes
/// with first-frame rendering or the user's first Ctrl+Win dictation.
/// </para>
///
/// <para>
/// The scheduled background task is the shared "post-startup housekeeping" seam
/// for the RAM-optimization task set: the working-set trim
/// (infrastructure-w7k9p) is expected to run immediately after this compaction
/// ("compact, then trim"). Keep that ordering when the trim lands.
/// </para>
/// </summary>
public sealed class StartupMemoryCompactor
{
    private int _hasRun;

    /// <summary>
    /// True if the most recent <see cref="Compact"/> call executed on a
    /// thread-pool (background) thread rather than the UI/caller thread.
    /// Exposed primarily so the responsiveness guarantee ("off the UI thread")
    /// is observable.
    /// </summary>
    public bool LastRunOnThreadPoolThread { get; private set; }

    /// <summary>
    /// Performs the one-shot LOH-compacting blocking gen-2 collection.
    /// Idempotent: only the first call does work; later calls are no-ops.
    /// </summary>
    /// <returns>
    /// <c>true</c> if this call performed the compaction; <c>false</c> if it
    /// had already run.
    /// </returns>
    public bool Compact()
    {
        if (Interlocked.Exchange(ref _hasRun, 1) == 1)
            return false;

        LastRunOnThreadPoolThread = Thread.CurrentThread.IsThreadPoolThread;

        // CompactOnce reverts to Default automatically after the next blocking
        // gen-2 collection, so this stays a single compacting pass.
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        return true;
    }

    /// <summary>
    /// Schedules <see cref="Compact"/> to run on a thread-pool thread after the
    /// given delay. Fire-and-forget from the caller's perspective; the returned
    /// task completes when the (best-effort) compaction has finished and never
    /// faults — failures are logged and swallowed so housekeeping can never
    /// destabilize the running app.
    /// </summary>
    /// <param name="delay">Post-startup delay before the housekeeping runs.</param>
    /// <param name="postCompactionStep">
    /// Optional follow-on step run immediately after the compaction completes, on
    /// the same delayed background task — the working-set trim
    /// (infrastructure-w7k9p) wires its trim here to realize "compact, then trim".
    /// Runs even if the compaction was a no-op (already run). Exceptions it throws
    /// are logged and swallowed like the rest of this hook.
    /// </param>
    /// <param name="cancellationToken">Cancels the delay (e.g. on shutdown).</param>
    public Task ScheduleAsync(
        TimeSpan delay,
        Action? postCompactionStep = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                Compact();

                // infrastructure-w7k9p: the post-load working-set trim runs here
                // ("compact, then trim"), after the LOH compaction so the trim
                // does not immediately re-fault compactable garbage back in.
                postCompactionStep?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // App shutting down before the delay elapsed — nothing to do.
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "[StartupMemoryCompactor] Post-startup compaction failed: {0}",
                    ex.Message);
            }
        }, cancellationToken);
    }
}
