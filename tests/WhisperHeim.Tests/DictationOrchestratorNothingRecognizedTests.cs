using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Orchestration;
using WhisperHeim.Services.Transcription;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Drives task main-rc541's <see cref="DictationOrchestrator.NothingRecognized"/> signal via
/// <see cref="DictationOrchestrator.TranscribeFinalAsync"/> directly (mirrors
/// <see cref="DictationOrchestratorEmptyResultTests"/>'s seam): asserts it fires exactly once
/// for an empty raw transcript, exactly once when the clean pipeline reduces a non-empty
/// transcript to empty, not at all for recordings below <c>MinSamples</c> (never reaching
/// <see cref="DictationOrchestrator.TranscribeFinalAsync"/> at all), and that template mode
/// with an empty transcript raises it instead of <see cref="DictationOrchestrator.TemplateNoMatch"/>.
/// </summary>
/// <remarks>
/// Shares an xUnit collection with <see cref="DictationOrchestratorEmptyResultTests"/> — see
/// that class's remarks for why: both exercise <see cref="DictationOrchestrator.TranscribeFinalAsync"/>'s
/// empty-result path, which logs via the process-wide <see cref="System.Diagnostics.Trace"/>.
/// </remarks>
[Collection("DictationOrchestratorEmptyResultTrace")]
public class DictationOrchestratorNothingRecognizedTests
{
    private static float[] MakeSamples(int count, float value = 0.05f)
    {
        var samples = new float[count];
        for (int i = 0; i < count; i++) samples[i] = value;
        return samples;
    }

    private static DictationOrchestrator CreateOrchestrator(
        ITranscriptionService transcription, FakeInputSimulator? input = null) =>
        new(
            new GlobalHotkeyService(),
            new FakeAudioCapture(),
            transcription,
            input ?? new FakeInputSimulator(),
            _ => { });

    [Fact]
    public async Task EmptyRawTranscript_RaisesNothingRecognizedExactlyOnce_NothingTyped()
    {
        var samples = MakeSamples(48000);
        var transcription = new FakeTranscription(
            new TranscriptionResult("", TimeSpan.FromSeconds(3.0), TimeSpan.FromMilliseconds(120), 0.04));
        var input = new FakeInputSimulator();
        var orchestrator = CreateOrchestrator(transcription, input);

        var raisedCount = 0;
        TimeSpan? lastDuration = null;
        orchestrator.NothingRecognized += duration =>
        {
            raisedCount++;
            lastDuration = duration;
        };

        await orchestrator.TranscribeFinalAsync(samples, templateMode: false, warming: false);

        Assert.Equal(1, raisedCount);
        Assert.Equal(TimeSpan.FromSeconds(3.0), lastDuration);
        // Nothing typed means the normal-dictation branch (the only place that both
        // types and sets _lastNormalDictation, on adjacent statements) was never
        // reached -- so _lastNormalDictation is implicitly unchanged too.
        Assert.Equal(0, input.TypeTextCallCount);
    }

    [Fact]
    public async Task NonEmptyRawTranscript_CleanPipelineReducesToEmpty_RaisesNothingRecognizedOnce()
    {
        // FillerRemovalService.Clean strips filler-only utterances down to nothing.
        var samples = MakeSamples(16000);
        var transcription = new FakeTranscription(
            new TranscriptionResult("um uh", TimeSpan.FromSeconds(1.0), TimeSpan.FromMilliseconds(40), 0.04));
        var orchestrator = CreateOrchestrator(transcription);

        var raisedCount = 0;
        orchestrator.NothingRecognized += _ => raisedCount++;

        await orchestrator.TranscribeFinalAsync(samples, templateMode: false, warming: false);

        Assert.Equal(1, raisedCount);
    }

    [Fact]
    public void ExceedsMinSamples_AtOrBelowThreshold_IsFalse()
    {
        // 8000 samples = 0.5s at 16kHz, the documented MinSamples threshold: at or
        // below it, StopRecording's gate skips TranscribeFinalAsync entirely, so
        // NothingRecognized (which only fires from inside it) can never be raised.
        Assert.False(DictationOrchestrator.ExceedsMinSamples(8000));
        Assert.False(DictationOrchestrator.ExceedsMinSamples(0));
    }

    [Fact]
    public void ExceedsMinSamples_AboveThreshold_IsTrue()
    {
        Assert.True(DictationOrchestrator.ExceedsMinSamples(8001));
    }

    [Fact]
    public async Task TemplateMode_EmptyTranscript_RaisesNothingRecognized_NotTemplateNoMatch()
    {
        var samples = MakeSamples(16000);
        var transcription = new FakeTranscription(
            new TranscriptionResult("", TimeSpan.FromSeconds(1.0), TimeSpan.FromMilliseconds(40), 0.04));
        var orchestrator = CreateOrchestrator(transcription);

        var nothingRecognizedRaised = false;
        var templateNoMatchRaised = false;
        orchestrator.NothingRecognized += _ => nothingRecognizedRaised = true;
        orchestrator.TemplateNoMatch += _ => templateNoMatchRaised = true;

        await orchestrator.TranscribeFinalAsync(samples, templateMode: true, warming: false);

        Assert.True(nothingRecognizedRaised);
        Assert.False(templateNoMatchRaised);
    }

    [Fact]
    public async Task NonEmptyTranscript_Success_DoesNotRaiseNothingRecognized()
    {
        var samples = MakeSamples(16000, value: 0.2f);
        var transcription = new FakeTranscription(
            new TranscriptionResult("hello world", TimeSpan.FromSeconds(1.0), TimeSpan.FromMilliseconds(40), 0.04));
        var orchestrator = CreateOrchestrator(transcription);

        var raised = false;
        orchestrator.NothingRecognized += _ => raised = true;

        await orchestrator.TranscribeFinalAsync(samples, templateMode: false, warming: false);

        Assert.False(raised);
    }

    #region Fakes

    private sealed class FakeAudioCapture : IAudioCaptureService
    {
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
        public event EventHandler? CaptureStarted;
        public event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;

        public bool IsCapturing { get; private set; }

        public IReadOnlyList<AudioDeviceInfo> GetAvailableDevices() => Array.Empty<AudioDeviceInfo>();

        public void StartCapture(int deviceIndex = -1)
        {
            IsCapturing = true;
            CaptureStarted?.Invoke(this, EventArgs.Empty);
        }

        public void StopCapture() => IsCapturing = false;

        public void Dispose()
        {
            _ = AudioDataAvailable;
            _ = CaptureStopped;
        }
    }

    private sealed class FakeInputSimulator : IInputSimulator
    {
        public int KeystrokeDelayMs { get; set; }
        public int TypeTextCallCount { get; private set; }

        public Task TypeTextAsync(string text, CancellationToken cancellationToken = default)
        {
            TypeTextCallCount++;
            return Task.CompletedTask;
        }

        public Task SendBackspacesAsync(int count, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeTranscription : ITranscriptionService
    {
        private readonly TranscriptionResult _result;

        public FakeTranscription(TranscriptionResult result)
        {
            _result = result;
        }

        public bool IsLoaded { get; set; } = true;

        public void LoadModel() => IsLoaded = true;

        public Task<TranscriptionResult> TranscribeAsync(
            float[] samples, int sampleRate = 16000,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);

        public void Dispose()
        {
        }
    }

    #endregion
}
