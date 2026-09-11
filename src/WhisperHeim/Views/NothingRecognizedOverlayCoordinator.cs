namespace WhisperHeim.Views;

/// <summary>
/// Coordinates the "Nothing recognized" overlay hold (task main-rc541) outside the WPF window
/// itself so the show/auto-revert/Error-preemption state machine is unit-testable without a live
/// <see cref="System.Windows.Threading.Dispatcher"/> — mirroring the seam convention used for
/// <see cref="DictationOverlayWindow.ComputeBottomCenter"/> (pure geometry) and
/// <c>DictationOrchestrator.ShouldWarmUpOnRelease</c> (pure decision): the visual state and the
/// real ~1.5 s timer are wired in <c>App.xaml.cs</c> and verified manually, this class holds only
/// the sequencing.
/// <para>
/// Unlike the "warming up" hold (known synchronously before the overlay hide is even queued, so it
/// can defer that hide), whether a dictation will decode to nothing is only known after decode
/// completes — often after the overlay has already faded out. <see cref="Show"/> therefore
/// re-shows the pill rather than deferring a hide, holds it for <see cref="HoldDuration"/>, then
/// reverts. A <see cref="Cancel"/> (a <c>PipelineError</c> arriving, or a fresh dictation starting)
/// preempts a pending hold — Error always takes precedence, and a fresh recording must not be
/// hidden out from under the user by a stale revert timer.
/// </para>
/// </summary>
public sealed class NothingRecognizedOverlayCoordinator
{
    /// <summary>How long the "Nothing recognized" pill stays up before auto-reverting.</summary>
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(1500);

    private readonly Action _showNothingRecognized;
    private readonly Action _hide;
    private readonly Action<TimeSpan, Action> _scheduleRevert;
    private readonly Action _cancelScheduledRevert;

    private bool _pending;

    /// <param name="showNothingRecognized">
    /// Re-shows the overlay (if needed) and sets its visual state to
    /// <see cref="OverlayMicState.NothingRecognized"/>.
    /// </param>
    /// <param name="hide">Hides the overlay, as the normal successful-dictation path does.</param>
    /// <param name="scheduleRevert">
    /// Arms a one-shot callback to fire after the given delay. Called with
    /// <see cref="HoldDuration"/> and an internal revert callback; tests supply a fake that
    /// captures the callback instead of waiting on a real clock.
    /// </param>
    /// <param name="cancelScheduledRevert">Cancels any armed callback from <paramref name="scheduleRevert"/>.</param>
    public NothingRecognizedOverlayCoordinator(
        Action showNothingRecognized,
        Action hide,
        Action<TimeSpan, Action> scheduleRevert,
        Action cancelScheduledRevert)
    {
        _showNothingRecognized = showNothingRecognized ?? throw new ArgumentNullException(nameof(showNothingRecognized));
        _hide = hide ?? throw new ArgumentNullException(nameof(hide));
        _scheduleRevert = scheduleRevert ?? throw new ArgumentNullException(nameof(scheduleRevert));
        _cancelScheduledRevert = cancelScheduledRevert ?? throw new ArgumentNullException(nameof(cancelScheduledRevert));
    }

    /// <summary>
    /// Shows the "Nothing recognized" state and (re-)arms the auto-revert. Safe to call while a
    /// previous hold is still pending — it is superseded rather than stacked.
    /// </summary>
    public void Show()
    {
        _cancelScheduledRevert();
        _pending = true;
        _showNothingRecognized();
        _scheduleRevert(HoldDuration, Revert);
    }

    /// <summary>
    /// Preempts a pending hold without hiding the overlay itself — the caller (a
    /// <c>PipelineError</c> handler setting the Error state, or a fresh dictation starting) owns
    /// whatever happens to the overlay next.
    /// </summary>
    public void Cancel()
    {
        if (!_pending) return;
        _pending = false;
        _cancelScheduledRevert();
    }

    private void Revert()
    {
        // A stale timer firing after Cancel() (or after a second Show() re-armed a fresh one)
        // must no-op -- Cancel() already stops the scheduled callback, but this guard also covers
        // fakes in tests that don't model real cancellation.
        if (!_pending) return;
        _pending = false;
        _hide();
    }
}
