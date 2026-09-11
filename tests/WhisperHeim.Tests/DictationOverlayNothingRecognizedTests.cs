using WhisperHeim.Views;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Drives task main-rc541's <see cref="NothingRecognizedOverlayCoordinator"/> seam (mirrors the
/// <c>DictationOverlayWarmUpTests</c> / <c>DictationOverlayPositionTests</c> convention of pulling
/// the pure sequencing out of the WPF window for unit testing): the transition into "Nothing
/// recognized", the ~1.5 s auto-revert, and Error preemption. The actual pill visuals/log line and
/// the real <see cref="System.Windows.Threading.DispatcherTimer"/> wiring are WPF/integration
/// concerns exercised manually per the project's lack of WPF UI-test infrastructure — see this
/// task's Outcome for the [human-eye] pass.
/// </summary>
public class DictationOverlayNothingRecognizedTests
{
    private sealed class FakeScheduler
    {
        public int ShowCalls;
        public int HideCalls;
        public int CancelCalls;
        public TimeSpan? LastDelay;
        private Action? _pendingCallback;

        public NothingRecognizedOverlayCoordinator CreateCoordinator() => new(
            showNothingRecognized: () => ShowCalls++,
            hide: () => HideCalls++,
            scheduleRevert: (delay, callback) =>
            {
                LastDelay = delay;
                _pendingCallback = callback;
            },
            cancelScheduledRevert: () =>
            {
                CancelCalls++;
                _pendingCallback = null;
            });

        /// <summary>Simulates the ~1.5s timer elapsing by invoking whatever callback is currently armed.</summary>
        public void FireScheduledRevert() => _pendingCallback?.Invoke();
    }

    [Fact]
    public void Show_ShowsNothingRecognizedAndArmsRevertAtHoldDuration()
    {
        var fake = new FakeScheduler();
        var coordinator = fake.CreateCoordinator();

        coordinator.Show();

        Assert.Equal(1, fake.ShowCalls);
        Assert.Equal(0, fake.HideCalls);
        Assert.Equal(NothingRecognizedOverlayCoordinator.HoldDuration, fake.LastDelay);
    }

    [Fact]
    public void ScheduledRevert_AfterHoldDuration_Hides()
    {
        var fake = new FakeScheduler();
        var coordinator = fake.CreateCoordinator();

        coordinator.Show();
        fake.FireScheduledRevert();

        Assert.Equal(1, fake.HideCalls);
    }

    [Fact]
    public void Cancel_BeforeRevertFires_PreemptsTheHold_HideNeverCalled()
    {
        var fake = new FakeScheduler();
        var coordinator = fake.CreateCoordinator();

        coordinator.Show();
        coordinator.Cancel(); // e.g. a PipelineError arrived meanwhile

        fake.FireScheduledRevert(); // a stale timer firing late must still no-op
        Assert.Equal(0, fake.HideCalls);
    }

    [Fact]
    public void Cancel_WithNoPendingHold_IsANoOp()
    {
        var fake = new FakeScheduler();
        var coordinator = fake.CreateCoordinator();

        coordinator.Cancel();

        Assert.Equal(0, fake.HideCalls);
    }

    [Fact]
    public void SecondShow_BeforeFirstRevertFires_SupersedesRatherThanStacking()
    {
        var fake = new FakeScheduler();
        var coordinator = fake.CreateCoordinator();

        coordinator.Show();
        coordinator.Show();

        // Every Show() cancels whatever was previously scheduled before arming its own --
        // the second call's cancel is what supersedes the first hold's revert.
        Assert.Equal(2, fake.CancelCalls);
        Assert.Equal(2, fake.ShowCalls);

        fake.FireScheduledRevert();
        Assert.Equal(1, fake.HideCalls);
    }
}
