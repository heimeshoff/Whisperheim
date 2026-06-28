using System.Diagnostics;
using System.Windows;
using WhisperHeim.Models;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Templates;
using WhisperHeim.Services.TextProcessing;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.Orchestration;

/// <summary>
/// Hold-to-talk dictation orchestrator.
///
/// Key down: start recording + show overlay.
/// While holding: accumulate audio samples.
/// Key up: stop recording, transcribe full audio, type the final result.
///
/// Template mode: if Alt is held during recording (Ctrl+Win+Alt), the transcribed
/// text is fuzzy-matched against templates. If a match is found, the template's
/// expanded text is typed instead. If no match, the raw transcription is typed.
///
/// No VAD needed -- the user controls speech boundaries by holding/releasing the hotkey.
/// </summary>
public sealed class DictationOrchestrator : IDisposable
{
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly IAudioCaptureService _audioCapture;
    private readonly ITranscriptionService _transcription;
    private readonly IInputSimulator _inputSimulator;
    private readonly ITemplateService? _templateService;
    private readonly SettingsService? _settingsService;
    private readonly Action<bool> _onDictationStateChanged;
    private readonly ModelLifecycleManager? _modelLifecycle;

    private readonly object _lock = new();
    private readonly List<float> _recordedSamples = new();
    private bool _isRecording;
    private bool _isTemplateMode;
    private bool _disposed;
    private string? _lastNormalDictation;

    private const int MinSamples = 8000; // 0.5s at 16kHz
    private const int SampleRate = 16000;

    /// <summary>
    /// Raised on a background thread with the RMS amplitude of each audio chunk.
    /// Value is in [0.0, 1.0] range.
    /// </summary>
    public event Action<double>? AudioAmplitudeChanged;

    /// <summary>
    /// Raised on a background thread when a pipeline error occurs.
    /// </summary>
    public event Action<Exception>? PipelineError;

    /// <summary>
    /// Raised when template mode is active but no template matched the spoken text.
    /// The string parameter contains the transcribed text that failed to match.
    /// </summary>
    public event Action<string>? TemplateNoMatch;

    /// <summary>
    /// Raised around the transcribe-on-release model-load wait (task infrastructure-q4t8m,
    /// ADR-0005/0006). <c>true</c> fires at release when the recognizer is not yet
    /// resident, so the held utterance outran the key-down load; <c>false</c> fires the
    /// moment <see cref="ModelLifecycleManager.EnsureLoadedAsync"/> returns and decode
    /// begins. The overlay uses this to show — and keep alive — the "warming up" state.
    /// Raised on a background thread; subscribers must marshal to the UI thread.
    /// </summary>
    public event Action<bool>? WarmingUpChanged;

    public DictationOrchestrator(
        GlobalHotkeyService hotkeyService,
        IAudioCaptureService audioCapture,
        ITranscriptionService transcription,
        IInputSimulator inputSimulator,
        Action<bool> onDictationStateChanged,
        ITemplateService? templateService = null,
        SettingsService? settingsService = null,
        ModelLifecycleManager? modelLifecycle = null)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _transcription = transcription ?? throw new ArgumentNullException(nameof(transcription));
        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
        _onDictationStateChanged = onDictationStateChanged ?? throw new ArgumentNullException(nameof(onDictationStateChanged));
        _templateService = templateService;
        _settingsService = settingsService;
        _modelLifecycle = modelLifecycle;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.HotkeyReleased += OnHotkeyReleased;

        Trace.TraceInformation("[DictationOrchestrator] Started. Hold hotkey to dictate. Hold Alt additionally for template mode.");
    }

    public void Stop()
    {
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.HotkeyReleased -= OnHotkeyReleased;

        if (_isRecording)
            StopRecording(transcribe: false);

        Trace.TraceInformation("[DictationOrchestrator] Stopped.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_isRecording) return; // already recording
            _isRecording = true;
            _isTemplateMode = false;
            _recordedSamples.Clear();
        }

        Trace.TraceInformation("[DictationOrchestrator] Hotkey pressed -- starting recording.");

        // Lazy-load the recognizer on key-DOWN (ADR-0005): kick off the ~4 s
        // session build in the background so it overlaps the time the user spends
        // speaking. Transcribe-on-release awaits this same load rather than starting
        // a second one. Fire-and-forget and idempotent.
        _modelLifecycle?.BeginLoad();

        _audioCapture.AudioDataAvailable += OnAudioData;

        try
        {
            _audioCapture.StartCapture();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to start capture: {0}", ex.Message);
            _audioCapture.AudioDataAvailable -= OnAudioData;
            lock (_lock) _isRecording = false;
            PipelineError?.Invoke(ex);
            return;
        }

        NotifyStateChanged(true);
    }

    private void OnHotkeyReleased(object? sender, EventArgs e)
    {
        bool wasRecording;
        lock (_lock)
        {
            wasRecording = _isRecording;
            if (!wasRecording) return;
        }

        Trace.TraceInformation("[DictationOrchestrator] Hotkey released -- stopping and transcribing.");
        StopRecording(transcribe: true);
    }

    private void StopRecording(bool transcribe)
    {
        bool templateMode;
        lock (_lock)
        {
            if (!_isRecording) return;
            _isRecording = false;
            templateMode = _isTemplateMode;
        }

        // Stop capture
        _audioCapture.AudioDataAvailable -= OnAudioData;
        try { _audioCapture.StopCapture(); }
        catch (Exception ex) { Trace.TraceWarning("[DictationOrchestrator] Error stopping capture: {0}", ex.Message); }

        float[] samples = Array.Empty<float>();
        if (transcribe)
        {
            lock (_lock)
            {
                samples = _recordedSamples.ToArray();
                _recordedSamples.Clear();
            }
        }

        bool willTranscribe = transcribe && samples.Length > MinSamples;

        // Decide the warming-up state *before* the hide is dispatched: if the held
        // utterance outran the key-down load, transcribe-on-release must await the
        // remaining load. We raise WarmingUpChanged(true) ahead of NotifyStateChanged
        // so the overlay's deferred-hide flag is set before the fade-out is queued —
        // otherwise the hide would win the race and the warming state would no-op.
        bool warming = willTranscribe && ShouldWarmUpOnRelease(_modelLifecycle?.State);
        if (warming)
            WarmingUpChanged?.Invoke(true);

        NotifyStateChanged(false);

        if (willTranscribe)
        {
            _ = TranscribeFinalAsync(samples, templateMode, warming);
        }
        else if (transcribe)
        {
            Trace.TraceInformation("[DictationOrchestrator] Recording too short ({0} samples), skipping.", samples.Length);
        }
    }

    /// <summary>
    /// Decides whether the "warming up" overlay state should be shown when a held
    /// dictation is released (task infrastructure-q4t8m, ADR-0005/0006): <c>true</c>
    /// exactly when the recognizer is not yet <see cref="ModelResidencyState.Loaded"/>,
    /// so transcribe-on-release must await the in-flight load. Returns <c>false</c>
    /// when no lifecycle manager is wired (eager-load builds) or the model is already
    /// resident — the common case, which must never flash the warming state.
    /// </summary>
    internal static bool ShouldWarmUpOnRelease(ModelResidencyState? state)
        => state is { } s && s != ModelResidencyState.Loaded;

    private void OnAudioData(object? sender, AudioDataEventArgs e)
    {
        try
        {
            lock (_lock)
            {
                if (_isRecording)
                {
                    _recordedSamples.AddRange(e.Samples);

                    // Continuously check if Alt is held during recording.
                    // Once detected, template mode stays on for the rest of this session.
                    if (!_isTemplateMode && _templateService is not null && IsKeyDown(NativeMethods.VK_MENU))
                    {
                        _isTemplateMode = true;
                        Trace.TraceInformation("[DictationOrchestrator] Alt detected during recording -- template mode.");
                    }
                }
            }

            // Calculate RMS amplitude and notify listeners
            var rms = CalculateRms(e.Samples);
            AudioAmplitudeChanged?.Invoke(rms);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] OnAudioData error: {0}", ex);
        }
    }

    /// <summary>
    /// Calculates the Root Mean Square (RMS) amplitude of audio samples.
    /// Returns a value in [0.0, 1.0] for normalized float samples.
    /// </summary>
    private static double CalculateRms(float[] samples)
    {
        if (samples.Length == 0) return 0.0;

        double sumSquares = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sumSquares += samples[i] * (double)samples[i];
        }

        return Math.Sqrt(sumSquares / samples.Length);
    }

    private async Task TranscribeFinalAsync(float[] samples, bool templateMode, bool warming)
    {
        _modelLifecycle?.EnterDictation();
        try
        {
            // Await the key-DOWN load (if still in flight) before transcribing — the
            // model must be resident by release. Never starts a second load.
            if (_modelLifecycle is not null)
                await _modelLifecycle.EnsureLoadedAsync();

            // Load complete — the warming window is over. Clear it here (and only on
            // success) so the overlay fades out before decode, exactly as the normal
            // release path does. On a load failure we fall through to the catch, which
            // raises PipelineError → the overlay shows Error instead (Error precedence).
            if (warming)
                WarmingUpChanged?.Invoke(false);

            var result = await _transcription.TranscribeAsync(samples, SampleRate);
            var rawText = result.Text.Trim();
            if (string.IsNullOrEmpty(rawText)) return;

            Trace.TraceInformation(
                "[DictationOrchestrator] Final: \"{0}\" (audio={1:F2}s, transcribe={2:F0}ms, RTF={3:F3}, template={4})",
                rawText, result.AudioDuration.TotalSeconds,
                result.TranscriptionDuration.TotalMilliseconds, result.RealTimeFactor, templateMode);

            if (templateMode && _templateService is not null)
            {
                // Template matching runs on raw text — templates match spoken phrases as-is.
                var match = _templateService.MatchAndExpand(rawText);
                if (match is not null)
                {
                    if (match.IsSystemTemplate)
                    {
                        Trace.TraceInformation(
                            "[DictationOrchestrator] System template matched: \"{0}\" (score={1:F2}, action={2}).",
                            match.TemplateName, match.MatchScore, match.SystemActionId);
                        await HandleSystemAction(match.SystemActionId);
                        return;
                    }

                    Trace.TraceInformation(
                        "[DictationOrchestrator] Template matched: \"{0}\" (score={1:F2}), typing expanded text.",
                        match.TemplateName, match.MatchScore);
                    await TypeTextSafe(match.ExpandedText);
                    return;
                }

                Trace.TraceInformation(
                    "[DictationOrchestrator] No template match for \"{0}\".", rawText);
                TemplateNoMatch?.Invoke(rawText);
                return;
            }

            // Run the deterministic clean-text pipeline before typing, unless
            // the user has opted into Raw mode.
            var textToType = ApplyCleanPipeline(rawText);
            if (string.IsNullOrEmpty(textToType))
            {
                Trace.TraceInformation(
                    "[DictationOrchestrator] Clean pipeline produced empty result, nothing to type.");
                return;
            }

            // Store last normal dictation (cleaned) for the Repeat command.
            _lastNormalDictation = textToType;
            await TypeTextSafe(textToType);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Final transcription error: {0}", ex.Message);
            PipelineError?.Invoke(ex);
        }
        finally
        {
            // Balance EnterDictation: clears the in-flight flag and re-arms the idle
            // clock so the model can be unloaded once the user truly goes idle.
            _modelLifecycle?.ExitDictation();
        }
    }

    /// <summary>
    /// Runs the deterministic clean-text pipeline (filler removal + whitespace
    /// normalization) on <paramref name="rawText"/> unless the user has set
    /// dictation to Raw mode. Returns the text that should be typed.
    /// </summary>
    private string ApplyCleanPipeline(string rawText)
    {
        var settings = _settingsService?.Current.Dictation;
        var mode = settings?.TextMode ?? DictationTextMode.Clean;

        if (mode == DictationTextMode.Raw)
        {
            return rawText;
        }

        var sw = Stopwatch.StartNew();
        var cleaned = FillerRemovalService.Clean(rawText, settings?.Language);
        sw.Stop();

        Trace.TraceInformation(
            "[DictationOrchestrator] Clean pipeline: \"{0}\" -> \"{1}\" ({2:F2}ms)",
            rawText, cleaned, sw.Elapsed.TotalMilliseconds);

        return cleaned;
    }

    private async Task HandleSystemAction(string? actionId)
    {
        switch (actionId)
        {
            case SystemTemplateDefinitions.RepeatActionId:
                if (string.IsNullOrEmpty(_lastNormalDictation))
                {
                    Trace.TraceInformation("[DictationOrchestrator] Repeat: no previous dictation to repeat.");
                    return;
                }

                Trace.TraceInformation(
                    "[DictationOrchestrator] Repeat: re-typing last dictation ({0} chars).",
                    _lastNormalDictation.Length);
                await TypeTextSafe(_lastNormalDictation);
                break;

            default:
                Trace.TraceWarning("[DictationOrchestrator] Unknown system action: {0}", actionId);
                break;
        }
    }

    private async Task TypeTextSafe(string text)
    {
        try
        {
            await _inputSimulator.TypeTextAsync(text);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to type text: {0}", ex.Message);
        }
    }

    private void NotifyStateChanged(bool isActive)
    {
        try
        {
            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                try { _onDictationStateChanged(isActive); }
                catch (Exception ex) { Trace.TraceError("[DictationOrchestrator] State callback error: {0}", ex.Message); }
            });
        }
        catch (Exception ex)
        {
            Trace.TraceError("[DictationOrchestrator] Failed to dispatch state change: {0}", ex.Message);
        }
    }

    private static bool IsKeyDown(int vk) =>
        (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
}
