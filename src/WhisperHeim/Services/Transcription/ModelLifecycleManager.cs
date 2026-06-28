using System.Diagnostics;

namespace WhisperHeim.Services.Transcription;

/// <summary>
/// Residency state of the speech-recognition model.
/// </summary>
public enum ModelResidencyState
{
    /// <summary>No recognizer in memory; a load must run before transcription.</summary>
    Unloaded,

    /// <summary>A load is in flight; callers await the shared load task.</summary>
    Loading,

    /// <summary>The recognizer is resident and ready to decode.</summary>
    Loaded,
}

/// <summary>
/// Lazy-load + keep-warm + idle-unload state machine for the ~640 MB Parakeet
/// recognizer (task infrastructure-d2v7n, ADR-0005).
///
/// <para>
/// The model only has to be resident <em>by the time a held dictation is
/// released</em> (push-to-talk transcribes the whole buffer in one shot on
/// release), so this manager:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Lazy-loads on key-DOWN</b> via <see cref="BeginLoad"/> (fire-and-forget),
///     so the fixed ~4 s reload overlaps the time the user spends speaking.
///   </description></item>
///   <item><description>
///     <b>Awaits the in-flight load on release</b> via <see cref="EnsureLoadedAsync"/>
///     — repeat presses during loading share a single load task; a second load is
///     never started.
///   </description></item>
///   <item><description>
///     <b>Keeps warm</b> through active dictation; <see cref="EnterDictation"/> /
///     <see cref="ExitDictation"/> mark in-flight work so an unload can never fire
///     mid-dictation, and any activity re-arms the idle clock.
///   </description></item>
///   <item><description>
///     <b>Idle-unloads</b> after <c>idleThreshold</c> (5 min default) with no
///     activity, freeing the ~680 MB of committed memory the k9m3p spike measured
///     (ADR-0005). The unload action pairs <c>Dispose()</c> with a GC + working-set
///     trim (ADR-0004); it is injected so this class stays testable without the
///     real recognizer.
///   </description></item>
/// </list>
///
/// <para>
/// The heavyweight load/unload work is injected as delegates so the lifecycle can
/// be unit-tested deterministically (injectable clock, no ~640 MB model). It reuses
/// the <see cref="IdleWorkingSetTrimmer"/> <c>NotifyActivity</c> + poll shape
/// (ADR-0004); the unload fuse is deliberately longer than the 3-min trim fuse
/// because a ~4 s session rebuild costs far more than a cold-page re-fault.
/// </para>
/// </summary>
public sealed class ModelLifecycleManager : IDisposable
{
    private readonly Func<CancellationToken, Task> _loadAsync;
    private readonly Action _unload;
    private readonly TimeSpan _idleThreshold;
    private readonly Func<DateTime> _utcNow;
    private readonly object _gate = new();

    private ModelResidencyState _state = ModelResidencyState.Unloaded;
    private Task? _loadTask;
    private int _busyCount;
    private DateTime _lastActivityUtc;
    private Timer? _timer;
    private bool _disposed;

    /// <param name="loadAsync">
    /// Loads the recognizer (e.g. <c>TranscriptionService.EnsureLoadedAsync</c>).
    /// Must be idempotent — it may be invoked again after an idle-unload.
    /// </param>
    /// <param name="unload">
    /// Frees the recognizer and reclaims memory (Dispose + GC + working-set trim).
    /// </param>
    /// <param name="idleThreshold">
    /// How long with no activity before the model is unloaded (5 min default).
    /// </param>
    /// <param name="utcNow">Clock source; defaults to <see cref="DateTime.UtcNow"/>.</param>
    public ModelLifecycleManager(
        Func<CancellationToken, Task> loadAsync,
        Action unload,
        TimeSpan idleThreshold,
        Func<DateTime>? utcNow = null)
    {
        _loadAsync = loadAsync ?? throw new ArgumentNullException(nameof(loadAsync));
        _unload = unload ?? throw new ArgumentNullException(nameof(unload));
        _idleThreshold = idleThreshold;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _lastActivityUtc = _utcNow();
    }

    /// <summary>Current residency state (observable for tests / diagnostics).</summary>
    public ModelResidencyState State
    {
        get { lock (_gate) return _state; }
    }

    /// <summary>Whether a dictation is currently in flight (an unload is suppressed).</summary>
    public bool IsBusy
    {
        get { lock (_gate) return _busyCount > 0; }
    }

    /// <summary>Resets the idle clock so a pending idle-unload is deferred.</summary>
    public void NotifyActivity()
    {
        lock (_gate) _lastActivityUtc = _utcNow();
    }

    /// <summary>
    /// Proactively starts loading the model (call on hotkey key-DOWN). Fire-and-forget:
    /// idempotent, re-arms the idle clock, and never throws — a load failure is logged
    /// and surfaced to the next <see cref="EnsureLoadedAsync"/> caller instead.
    /// </summary>
    public void BeginLoad()
    {
        NotifyActivity();
        var loadTask = StartLoadIfNeeded();
        _ = ObserveAsync(loadTask);
    }

    /// <summary>
    /// Ensures the model is resident, awaiting an in-flight load rather than starting
    /// a second one. Each caller can cancel its own wait via <paramref name="ct"/>
    /// without cancelling the shared load. A load failure surfaces here so the caller
    /// can degrade gracefully (e.g. trigger the model-download fallback).
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        NotifyActivity();
        var loadTask = StartLoadIfNeeded();
        if (loadTask.IsCompletedSuccessfully)
            return;

        await loadTask.WaitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Marks a dictation as started: increments in-flight and re-arms the idle clock.</summary>
    public void EnterDictation()
    {
        lock (_gate)
        {
            _busyCount++;
            _lastActivityUtc = _utcNow();
        }
    }

    /// <summary>Marks a dictation as finished: decrements in-flight and re-arms the idle clock.</summary>
    public void ExitDictation()
    {
        lock (_gate)
        {
            if (_busyCount > 0)
                _busyCount--;
            _lastActivityUtc = _utcNow();
        }
    }

    /// <summary>
    /// Runs one idle poll: if the model is loaded, no dictation is in flight, and the
    /// idle threshold has elapsed, unloads the model and returns <c>true</c>. The
    /// unload action runs outside the lock.
    /// </summary>
    public bool PollOnce()
    {
        lock (_gate)
        {
            if (_disposed)
                return false;
            if (_state != ModelResidencyState.Loaded)
                return false;
            if (_busyCount > 0)
                return false;
            if (_utcNow() - _lastActivityUtc < _idleThreshold)
                return false;

            _state = ModelResidencyState.Unloaded;
            _loadTask = null;
        }

        _unload();
        return true;
    }

    /// <summary>
    /// Begins polling for idle on a background timer at <paramref name="pollInterval"/>.
    /// No-op if already started or disposed.
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

    private Task StartLoadIfNeeded()
    {
        lock (_gate)
        {
            if (_disposed)
                return Task.CompletedTask;
            if (_state == ModelResidencyState.Loaded)
                return Task.CompletedTask;
            if (_state == ModelResidencyState.Loading && _loadTask is not null)
                return _loadTask;

            _state = ModelResidencyState.Loading;
            _loadTask = RunLoadAsync();
            return _loadTask;
        }
    }

    private async Task RunLoadAsync()
    {
        try
        {
            await _loadAsync(CancellationToken.None).ConfigureAwait(false);
            lock (_gate)
            {
                // Honor an intervening unload/dispose: only promote to Loaded if we
                // are still the in-flight load.
                if (_state == ModelResidencyState.Loading)
                    _state = ModelResidencyState.Loaded;
            }
        }
        catch
        {
            lock (_gate)
            {
                if (_state == ModelResidencyState.Loading)
                {
                    _state = ModelResidencyState.Unloaded;
                    _loadTask = null;
                }
            }
            throw;
        }
    }

    private static async Task ObserveAsync(Task loadTask)
    {
        try
        {
            await loadTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[ModelLifecycleManager] Proactive load failed: {0}", ex.Message);
        }
    }

    private void SafePoll()
    {
        try
        {
            PollOnce();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[ModelLifecycleManager] Idle poll failed: {0}", ex.Message);
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
