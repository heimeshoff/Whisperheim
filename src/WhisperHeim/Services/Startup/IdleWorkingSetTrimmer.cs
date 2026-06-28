using System.Diagnostics;

namespace WhisperHeim.Services.Startup;

/// <summary>
/// Fires a working-set trim once the app has been idle — no dictation or
/// transcription activity — for a configured threshold (task infrastructure-w7k9p).
///
/// <para>
/// Activity callers signal via <see cref="NotifyActivity"/> (wired to the
/// dictation start/stop signal). A low-frequency poll timer checks the elapsed
/// idle time; when the threshold is crossed it runs the injected trim action
/// <em>exactly once</em> per idle period. Continued idle does not re-trim (there
/// is nothing newly touched to release); the next <see cref="NotifyActivity"/>
/// re-arms it. This keeps the cost to a single page-release per idle stretch.
/// </para>
///
/// <para>
/// The trim policy is decoupled from wall-clock time via an injectable clock so
/// it can be driven deterministically in tests; <see cref="PollOnce"/> exposes a
/// single poll tick without depending on the real timer.
/// </para>
/// </summary>
public sealed class IdleWorkingSetTrimmer : IDisposable
{
    private readonly TimeSpan _idleThreshold;
    private readonly Action _trim;
    private readonly Func<DateTime> _utcNow;
    private readonly object _gate = new();

    private DateTime _lastActivityUtc;
    private bool _trimmedSinceLastActivity;
    private Timer? _timer;
    private bool _disposed;

    /// <param name="idleThreshold">
    /// How long with no activity before a trim fires (e.g. 3 minutes).
    /// </param>
    /// <param name="trim">The working-set trim action to invoke when idle.</param>
    /// <param name="utcNow">
    /// Clock source; defaults to <see cref="DateTime.UtcNow"/>. Injectable for tests.
    /// </param>
    public IdleWorkingSetTrimmer(TimeSpan idleThreshold, Action trim, Func<DateTime>? utcNow = null)
    {
        _idleThreshold = idleThreshold;
        _trim = trim ?? throw new ArgumentNullException(nameof(trim));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _lastActivityUtc = _utcNow();
    }

    /// <summary>
    /// Begins polling for idle on a background timer. The first poll fires after
    /// <paramref name="pollInterval"/> and repeats at that cadence. No-op if the
    /// timer is already running or the instance is disposed.
    /// </summary>
    public void Start(TimeSpan pollInterval)
    {
        lock (_gate)
        {
            if (_disposed || _timer is not null)
                return;

            _timer = new Timer(_ => SafePoll(), null, pollInterval, pollInterval);
        }
    }

    /// <summary>
    /// Records that dictation/transcription activity just happened, resetting the
    /// idle clock and re-arming a future trim.
    /// </summary>
    public void NotifyActivity()
    {
        lock (_gate)
        {
            _lastActivityUtc = _utcNow();
            _trimmedSinceLastActivity = false;
        }
    }

    /// <summary>
    /// Whether a trim is due now: the idle threshold has elapsed since the last
    /// activity and no trim has run since. Calling this and acting on a
    /// <c>true</c> result consumes the opportunity for this idle period.
    /// </summary>
    public bool ShouldTrim()
    {
        lock (_gate)
        {
            if (_trimmedSinceLastActivity)
                return false;
            if (_utcNow() - _lastActivityUtc < _idleThreshold)
                return false;

            _trimmedSinceLastActivity = true;
            return true;
        }
    }

    /// <summary>
    /// Runs one poll tick: if a trim is due, invokes the trim action and returns
    /// <c>true</c>; otherwise returns <c>false</c>. The trim action is invoked
    /// outside the lock.
    /// </summary>
    public bool PollOnce()
    {
        if (!ShouldTrim())
            return false;

        _trim();
        return true;
    }

    private void SafePoll()
    {
        try
        {
            PollOnce();
        }
        catch (Exception ex)
        {
            // The injected trim is already failure-isolated, but guard the poll
            // loop so a stray exception can never tear down the timer thread.
            Trace.TraceWarning("[IdleWorkingSetTrimmer] Idle poll failed: {0}", ex.Message);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
