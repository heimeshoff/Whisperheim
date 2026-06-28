using System;
using System.Threading;
using System.Threading.Tasks;
using WhisperHeim.Services.Transcription;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Drives the lazy-load + keep-warm + idle-unload state machine
/// (task infrastructure-d2v7n, ADR-0005) without the real ~640 MB recognizer:
/// load/unload are injected delegates and time is an injected clock. Covers the
/// state transitions (Unloaded → Loading → Loaded → idle → Unloaded) and the
/// concurrency edges ADR-0005 calls out: release-before-loaded (await), repeat
/// presses during loading (single shared load), load failure (graceful + retry),
/// cancellation, and never-unload-while-busy.
/// </summary>
public class ModelLifecycleManagerTests
{
    private sealed class FakeClock
    {
        private DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);
        public DateTime Now() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private static readonly TimeSpan Idle5Min = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task BeginLoad_TransitionsToLoaded_AndLoadsExactlyOnce()
    {
        int loads = 0;
        using var mgr = new ModelLifecycleManager(
            loadAsync: _ => { Interlocked.Increment(ref loads); return Task.CompletedTask; },
            unload: () => { },
            idleThreshold: Idle5Min);

        mgr.BeginLoad();
        await mgr.EnsureLoadedAsync();

        Assert.Equal(ModelResidencyState.Loaded, mgr.State);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task EnsureLoadedAsync_WhenAlreadyLoaded_DoesNotReload()
    {
        int loads = 0;
        using var mgr = new ModelLifecycleManager(
            _ => { Interlocked.Increment(ref loads); return Task.CompletedTask; },
            () => { }, Idle5Min);

        await mgr.EnsureLoadedAsync();
        await mgr.EnsureLoadedAsync();

        Assert.Equal(1, loads);
        Assert.Equal(ModelResidencyState.Loaded, mgr.State);
    }

    [Fact]
    public async Task RepeatPressesDuringLoading_ShareASingleLoadTask()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int loads = 0;
        using var mgr = new ModelLifecycleManager(
            async _ => { Interlocked.Increment(ref loads); await gate.Task; },
            () => { }, Idle5Min);

        mgr.BeginLoad();                    // key-down #1
        var release1 = mgr.EnsureLoadedAsync(); // release awaits in-flight load
        var release2 = mgr.EnsureLoadedAsync(); // a second press also awaits the same load

        Assert.Equal(ModelResidencyState.Loading, mgr.State);
        Assert.Equal(1, loads);             // single shared load, not three

        gate.SetResult();
        await release1;
        await release2;

        Assert.Equal(ModelResidencyState.Loaded, mgr.State);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task EnsureLoadedAsync_AwaitsInFlightLoad_StartedByKeyDown()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var mgr = new ModelLifecycleManager(
            async _ => await gate.Task, () => { }, Idle5Min);

        mgr.BeginLoad();
        var release = mgr.EnsureLoadedAsync();
        Assert.False(release.IsCompleted);  // release blocks until the load finishes

        gate.SetResult();
        await release;
        Assert.Equal(ModelResidencyState.Loaded, mgr.State);
    }

    [Fact]
    public async Task LoadFailure_ResetsToUnloaded_SurfacesToCaller_AndAllowsRetry()
    {
        bool fail = true;
        int loads = 0;
        using var mgr = new ModelLifecycleManager(
            _ =>
            {
                Interlocked.Increment(ref loads);
                if (Volatile.Read(ref fail))
                    throw new InvalidOperationException("missing model");
                return Task.CompletedTask;
            },
            () => { }, Idle5Min);

        await Assert.ThrowsAsync<InvalidOperationException>(() => mgr.EnsureLoadedAsync());
        Assert.Equal(ModelResidencyState.Unloaded, mgr.State);

        // A later attempt retries cleanly (e.g. after the model finishes downloading).
        Volatile.Write(ref fail, false);
        await mgr.EnsureLoadedAsync();
        Assert.Equal(ModelResidencyState.Loaded, mgr.State);
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task BeginLoad_DoesNotThrow_WhenLoadFails()
    {
        using var mgr = new ModelLifecycleManager(
            _ => throw new InvalidOperationException("missing model"),
            () => { }, Idle5Min);

        mgr.BeginLoad(); // fire-and-forget must swallow the failure
        await Task.Delay(50);

        Assert.Equal(ModelResidencyState.Unloaded, mgr.State);
    }

    [Fact]
    public async Task EnsureLoadedAsync_CancelledWait_DoesNotCancelSharedLoad()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int loads = 0;
        using var mgr = new ModelLifecycleManager(
            async _ => { Interlocked.Increment(ref loads); await gate.Task; },
            () => { }, Idle5Min);

        using var cts = new CancellationTokenSource();
        var waiting = mgr.EnsureLoadedAsync(cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        // The shared load is untouched: still loading, still a single load.
        Assert.Equal(ModelResidencyState.Loading, mgr.State);
        Assert.Equal(1, loads);

        gate.SetResult();
        await mgr.EnsureLoadedAsync();
        Assert.Equal(ModelResidencyState.Loaded, mgr.State);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task PollOnce_UnloadsAfterIdle_WhenLoadedAndNotBusy()
    {
        var clock = new FakeClock();
        int unloads = 0;
        using var mgr = new ModelLifecycleManager(
            _ => Task.CompletedTask, () => Interlocked.Increment(ref unloads),
            Idle5Min, clock.Now);

        await mgr.EnsureLoadedAsync();
        clock.Advance(TimeSpan.FromMinutes(6));

        Assert.True(mgr.PollOnce());
        Assert.Equal(1, unloads);
        Assert.Equal(ModelResidencyState.Unloaded, mgr.State);

        // Continued idle does not unload again.
        Assert.False(mgr.PollOnce());
        Assert.Equal(1, unloads);
    }

    [Fact]
    public async Task PollOnce_DoesNotUnload_BeforeIdleThreshold()
    {
        var clock = new FakeClock();
        int unloads = 0;
        using var mgr = new ModelLifecycleManager(
            _ => Task.CompletedTask, () => Interlocked.Increment(ref unloads),
            Idle5Min, clock.Now);

        await mgr.EnsureLoadedAsync();
        clock.Advance(TimeSpan.FromMinutes(4));

        Assert.False(mgr.PollOnce());
        Assert.Equal(0, unloads);
    }

    [Fact]
    public async Task PollOnce_DoesNotUnload_WhileDictationInFlight()
    {
        var clock = new FakeClock();
        int unloads = 0;
        using var mgr = new ModelLifecycleManager(
            _ => Task.CompletedTask, () => Interlocked.Increment(ref unloads),
            Idle5Min, clock.Now);

        await mgr.EnsureLoadedAsync();
        mgr.EnterDictation();
        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.False(mgr.PollOnce());      // never unload mid-dictation
        Assert.Equal(0, unloads);

        mgr.ExitDictation();               // re-arms the idle clock
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(mgr.PollOnce());
        Assert.Equal(1, unloads);
    }

    [Fact]
    public void PollOnce_DoesNotUnload_WhenNeverLoaded()
    {
        var clock = new FakeClock();
        int unloads = 0;
        using var mgr = new ModelLifecycleManager(
            _ => Task.CompletedTask, () => Interlocked.Increment(ref unloads),
            Idle5Min, clock.Now);

        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.False(mgr.PollOnce());
        Assert.Equal(0, unloads);
    }

    [Fact]
    public async Task NotifyActivity_PreventsUnload_ByResettingIdleClock()
    {
        var clock = new FakeClock();
        int unloads = 0;
        using var mgr = new ModelLifecycleManager(
            _ => Task.CompletedTask, () => Interlocked.Increment(ref unloads),
            Idle5Min, clock.Now);

        await mgr.EnsureLoadedAsync();
        clock.Advance(TimeSpan.FromMinutes(4));
        mgr.NotifyActivity();
        clock.Advance(TimeSpan.FromMinutes(4)); // 4 min since activity < 5

        Assert.False(mgr.PollOnce());
        Assert.Equal(0, unloads);

        clock.Advance(TimeSpan.FromMinutes(2)); // now 6 min since activity
        Assert.True(mgr.PollOnce());
        Assert.Equal(1, unloads);
    }

    [Fact]
    public async Task AfterIdleUnload_NextLoadReloads()
    {
        var clock = new FakeClock();
        int loads = 0;
        using var mgr = new ModelLifecycleManager(
            _ => { Interlocked.Increment(ref loads); return Task.CompletedTask; },
            () => { }, Idle5Min, clock.Now);

        await mgr.EnsureLoadedAsync();
        Assert.Equal(1, loads);

        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(mgr.PollOnce());
        Assert.Equal(ModelResidencyState.Unloaded, mgr.State);

        await mgr.EnsureLoadedAsync();
        Assert.Equal(2, loads);
        Assert.Equal(ModelResidencyState.Loaded, mgr.State);
    }

    [Fact]
    public async Task Start_FiresUnload_OnceIdleViaTimer()
    {
        var fired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var mgr = new ModelLifecycleManager(
            _ => Task.CompletedTask, () => fired.TrySetResult(true),
            idleThreshold: TimeSpan.FromMilliseconds(1));

        await mgr.EnsureLoadedAsync();
        mgr.Start(pollInterval: TimeSpan.FromMilliseconds(20));

        var completed = await Task.WhenAny(fired.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(fired.Task, completed);
    }

    [Fact]
    public void Constructor_RejectsNullDelegates()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ModelLifecycleManager(null!, () => { }, Idle5Min));
        Assert.Throws<ArgumentNullException>(
            () => new ModelLifecycleManager(_ => Task.CompletedTask, null!, Idle5Min));
    }
}
