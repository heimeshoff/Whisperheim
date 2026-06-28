using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using WhisperHeim.Services.Startup;

namespace WhisperHeim.Tests;

/// <summary>
/// Verifies the one-shot LOH-compacting startup collection (task
/// infrastructure-g3n5t). The compactor performs a single blocking gen-2
/// garbage collection with <see cref="GCLargeObjectHeapCompactionMode.CompactOnce"/>
/// once the app has finished booting, off the UI thread, to return Large
/// Object Heap slack accumulated during model load + WPF init back to the OS.
/// </summary>
public class StartupMemoryCompactorTests
{
    [Fact]
    public void Compact_RunsBlockingGen2Compaction_RevertingLohModeToDefault()
    {
        // CompactOnce reverts to Default only after a blocking gen-2 collection
        // has actually run. Pre-seeding CompactOnce and observing it back at
        // Default proves the production code performed the compacting collection.
        var compactor = new StartupMemoryCompactor();
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

        bool ran = compactor.Compact();

        Assert.True(ran);
        Assert.Equal(
            GCLargeObjectHeapCompactionMode.Default,
            GCSettings.LargeObjectHeapCompactionMode);
    }

    [Fact]
    public void Compact_IsOneShot_SecondCallDoesNotRunAgain()
    {
        var compactor = new StartupMemoryCompactor();

        Assert.True(compactor.Compact());
        Assert.False(compactor.Compact());
    }

    [Fact]
    public async Task ScheduleAsync_RunsCompactionOffTheCallingThread_AfterDelay()
    {
        var compactor = new StartupMemoryCompactor();

        await compactor.ScheduleAsync(TimeSpan.FromMilliseconds(10));

        // The scheduled run already happened, so a direct call is now a no-op.
        Assert.False(compactor.Compact());
        // ...and it ran on a thread-pool thread, never the UI/caller thread.
        Assert.True(compactor.LastRunOnThreadPoolThread);
    }
}
