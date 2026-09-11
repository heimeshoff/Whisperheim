using System.Diagnostics;
using System.IO;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Diagnostics;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Orchestration;
using WhisperHeim.Services.Transcription;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Drives task main-ma9j8's empty-dictation-result diagnostics via
/// <see cref="DictationOrchestrator.TranscribeFinalAsync"/> directly (mirrors
/// <see cref="DictationOrchestratorDeviceSelectionTests"/>'s test-seam approach
/// since the real hold-to-talk path can only be exercised via the low-level
/// keyboard hook): asserts the Warning/Information trace split around the 3s
/// threshold, the capped WAV dump, the disable switch, and that a dump I/O
/// failure never surfaces as <see cref="DictationOrchestrator.PipelineError"/>.
/// </summary>
/// <remarks>
/// Shares an xUnit collection with <see cref="DictationOrchestratorNothingRecognizedTests"/>
/// (task main-rc541) so the two classes never run in parallel: both exercise
/// <see cref="DictationOrchestrator.TranscribeFinalAsync"/>'s empty-result path, which logs
/// via the process-wide <see cref="Trace"/> — a <see cref="CapturingTraceListener"/> attached
/// here would otherwise also capture trace lines from a concurrently-running instance of the
/// other class.
/// </remarks>
[Collection("DictationOrchestratorEmptyResultTrace")]
public class DictationOrchestratorEmptyResultTests : IDisposable
{
    private readonly string _testRoot;

    public DictationOrchestratorEmptyResultTests()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "WhisperHeimTests",
            "orch_diag_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static float[] MakeSamples(int count, float value = 0.05f)
    {
        var samples = new float[count];
        for (int i = 0; i < count; i++) samples[i] = value;
        return samples;
    }

    private static DictationOrchestrator CreateOrchestrator(
        ITranscriptionService transcription,
        EmptyDictationDumpService? dumpService = null) =>
        new(
            new GlobalHotkeyService(),
            new FakeAudioCapture(),
            transcription,
            new FakeInputSimulator(),
            _ => { },
            emptyDictationDumpService: dumpService);

    [Fact]
    public async Task EmptyTranscript_AudioAtLeastThreeSeconds_LogsWarningAndDumps()
    {
        var samples = MakeSamples(48000); // 3.0s at 16kHz
        var transcription = new FakeTranscription(
            new TranscriptionResult("", TimeSpan.FromSeconds(3.0), TimeSpan.FromMilliseconds(120), 0.04));
        var dumpService = new EmptyDictationDumpService(_testRoot, () => DateTime.UtcNow);
        var orchestrator = CreateOrchestrator(transcription, dumpService);

        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            await orchestrator.TranscribeFinalAsync(samples, templateMode: false, warming: false);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        var entry = Assert.Single(listener.Entries, e => e.Message.Contains("Empty transcript"));
        Assert.Equal(TraceEventType.Warning, entry.Type);
        Assert.Contains("3.00s", entry.Message);
        Assert.Contains("48000 samples", entry.Message);
        Assert.Contains("rms=", entry.Message);
        Assert.Contains("peak=", entry.Message);
        Assert.Contains("decode=120", entry.Message);
        Assert.Contains("template=False", entry.Message);
        Assert.Contains("residency=", entry.Message);
        Assert.Contains("dump=", entry.Message);

        var dumps = Directory.GetFiles(_testRoot, "empty-dictation-*.wav");
        Assert.Single(dumps);
    }

    [Fact]
    public async Task EmptyTranscript_AudioUnderThreeSeconds_LogsInformationAndDoesNotDump()
    {
        var samples = MakeSamples(16000); // 1.0s at 16kHz
        var transcription = new FakeTranscription(
            new TranscriptionResult("", TimeSpan.FromSeconds(1.0), TimeSpan.FromMilliseconds(40), 0.04));
        var dumpService = new EmptyDictationDumpService(_testRoot, () => DateTime.UtcNow);
        var orchestrator = CreateOrchestrator(transcription, dumpService);

        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            await orchestrator.TranscribeFinalAsync(samples, templateMode: false, warming: false);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        var entry = Assert.Single(listener.Entries, e => e.Message.Contains("Empty transcript"));
        Assert.Equal(TraceEventType.Information, entry.Type);
        Assert.Contains("1.00s", entry.Message);
        Assert.Contains("dump disabled", entry.Message);

        Assert.False(Directory.Exists(_testRoot) && Directory.GetFiles(_testRoot).Length > 0);
    }

    [Fact]
    public async Task EmptyTranscript_DumpDisabledViaEnvVar_WarningStatesDumpDisabled()
    {
        var samples = MakeSamples(48000);
        var transcription = new FakeTranscription(
            new TranscriptionResult("", TimeSpan.FromSeconds(3.0), TimeSpan.FromMilliseconds(120), 0.04));
        var dumpService = new EmptyDictationDumpService(_testRoot, () => DateTime.UtcNow, isDisabled: () => true);
        var orchestrator = CreateOrchestrator(transcription, dumpService);

        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            await orchestrator.TranscribeFinalAsync(samples, templateMode: false, warming: false);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        var entry = Assert.Single(listener.Entries, e => e.Message.Contains("Empty transcript"));
        Assert.Equal(TraceEventType.Warning, entry.Type);
        Assert.Contains("dump disabled", entry.Message);
        Assert.False(Directory.Exists(_testRoot) && Directory.GetFiles(_testRoot).Length > 0);
    }

    [Fact]
    public async Task EmptyTranscript_DumpIoFailure_IsLoggedAndDoesNotRaisePipelineErrorOrThrow()
    {
        // Occupy the target directory's path with a plain file so the dump
        // service's Directory.CreateDirectory throws.
        Directory.CreateDirectory(Path.GetDirectoryName(_testRoot)!);
        File.WriteAllText(_testRoot, "occupied");

        var samples = MakeSamples(48000);
        var transcription = new FakeTranscription(
            new TranscriptionResult("", TimeSpan.FromSeconds(3.0), TimeSpan.FromMilliseconds(120), 0.04));
        var dumpService = new EmptyDictationDumpService(_testRoot, () => DateTime.UtcNow);
        var orchestrator = CreateOrchestrator(transcription, dumpService);

        Exception? pipelineError = null;
        orchestrator.PipelineError += ex => pipelineError = ex;

        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            await orchestrator.TranscribeFinalAsync(samples, templateMode: false, warming: false);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            File.Delete(_testRoot);
        }

        Assert.Null(pipelineError);
        var entry = Assert.Single(listener.Entries, e => e.Message.Contains("Empty transcript"));
        Assert.Contains("dump failed", entry.Message);
    }

    [Fact]
    public async Task NonEmptyTranscript_FinalLogLine_IncludesRmsAndPeak()
    {
        var samples = MakeSamples(16000, value: 0.2f);
        var transcription = new FakeTranscription(
            new TranscriptionResult("hello world", TimeSpan.FromSeconds(1.0), TimeSpan.FromMilliseconds(40), 0.04));
        var orchestrator = CreateOrchestrator(transcription);

        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            await orchestrator.TranscribeFinalAsync(samples, templateMode: false, warming: false);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        var entry = Assert.Single(listener.Entries, e => e.Message.Contains("Final:"));
        Assert.Contains("rms=", entry.Message);
        Assert.Contains("peak=", entry.Message);
    }

    #region Fakes

    private sealed class CapturingTraceListener : TraceListener
    {
        public List<(TraceEventType Type, string Message)> Entries { get; } = new();

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }

        public override void TraceEvent(
            TraceEventCache? eventCache, string source, TraceEventType eventType, int id,
            string? format, params object?[]? args)
        {
            var message = format is null
                ? string.Empty
                : (args is null || args.Length == 0 ? format : string.Format(format, args));
            Entries.Add((eventType, message));
        }

        public override void TraceEvent(
            TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            Entries.Add((eventType, message ?? string.Empty));
        }
    }

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

        public Task TypeTextAsync(string text, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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
