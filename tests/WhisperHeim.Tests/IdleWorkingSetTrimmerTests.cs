using System;
using System.Threading;
using System.Threading.Tasks;
using WhisperHeim.Services.Startup;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Verifies the idle working-set trim policy (task infrastructure-w7k9p): after a
/// configured idle threshold with no dictation/transcription activity the trim
/// fires exactly once, is re-armed by activity, and never fires while activity is
/// recent. The trim action is injected so the policy is tested without the Win32
/// P/Invoke; a controllable clock makes it deterministic.
/// </summary>
public class IdleWorkingSetTrimmerTests
{
    private sealed class FakeClock
    {
        private DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);
        public DateTime Now() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public void ShouldTrim_IsFalse_BeforeIdleThresholdElapses()
    {
        var clock = new FakeClock();
        using var trimmer = new IdleWorkingSetTrimmer(
            TimeSpan.FromMinutes(3), trim: () => { }, utcNow: clock.Now);

        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.False(trimmer.ShouldTrim());
    }

    [Fact]
    public void ShouldTrim_IsTrueOnce_AfterIdleThreshold_ThenFalseUntilActivity()
    {
        var clock = new FakeClock();
        using var trimmer = new IdleWorkingSetTrimmer(
            TimeSpan.FromMinutes(3), trim: () => { }, utcNow: clock.Now);

        clock.Advance(TimeSpan.FromMinutes(3));

        // Fires exactly once for this idle period...
        Assert.True(trimmer.ShouldTrim());
        // ...and continued idle does not re-trim (nothing newly touched).
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.False(trimmer.ShouldTrim());
    }

    [Fact]
    public void NotifyActivity_ReArmsTheTrim_AndResetsTheIdleClock()
    {
        var clock = new FakeClock();
        using var trimmer = new IdleWorkingSetTrimmer(
            TimeSpan.FromMinutes(3), trim: () => { }, utcNow: clock.Now);

        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.True(trimmer.ShouldTrim());

        // New activity re-arms and resets the clock: not yet idle again.
        trimmer.NotifyActivity();
        Assert.False(trimmer.ShouldTrim());

        // Idle again from the activity moment → trims once more.
        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.True(trimmer.ShouldTrim());
    }

    [Fact]
    public void PollOnce_InvokesTrim_OnlyWhenIdleThresholdElapsed()
    {
        var clock = new FakeClock();
        int trims = 0;
        using var trimmer = new IdleWorkingSetTrimmer(
            TimeSpan.FromMinutes(3), trim: () => Interlocked.Increment(ref trims), utcNow: clock.Now);

        Assert.False(trimmer.PollOnce());
        Assert.Equal(0, trims);

        clock.Advance(TimeSpan.FromMinutes(3));

        Assert.True(trimmer.PollOnce());
        Assert.Equal(1, trims);

        // Continued idle: no second invocation.
        Assert.False(trimmer.PollOnce());
        Assert.Equal(1, trims);
    }

    [Fact]
    public async Task Start_FiresTrim_OnceIdleViaTimer()
    {
        // Tiny threshold + poll so the real timer path exercises end-to-end
        // without injecting a clock. The constructor stamps "last activity" at
        // construction, so by the first poll the (1 ms) threshold has elapsed.
        var fired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var trimmer = new IdleWorkingSetTrimmer(
            TimeSpan.FromMilliseconds(1), trim: () => fired.TrySetResult(true));

        trimmer.Start(pollInterval: TimeSpan.FromMilliseconds(20));

        var completed = await Task.WhenAny(fired.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(fired.Task, completed);
    }

    [Fact]
    public void Constructor_RejectsNullTrim()
    {
        Assert.Throws<ArgumentNullException>(
            () => new IdleWorkingSetTrimmer(TimeSpan.FromMinutes(3), trim: null!));
    }
}
