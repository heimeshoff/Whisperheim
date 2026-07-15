using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Orchestration;
using WhisperHeim.Services.Transcription;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Drives the hotkey-path device-selection fix (task main-v7k2d): before this fix,
/// <c>DictationOrchestrator.OnHotkeyPressed</c> called <c>StartCapture()</c> with no
/// device index, so it always opened WaveIn device 0 regardless of the microphone
/// saved in Dictation settings. These tests exercise the extracted
/// <see cref="DictationOrchestrator.StartCaptureForDevice"/> seam directly (mirrors
/// the existing <c>ShouldWarmUpOnRelease</c> test seam) since the real hotkey event
/// can only be raised from inside <see cref="GlobalHotkeyService"/> itself.
/// </summary>
public class DictationOrchestratorDeviceSelectionTests
{
    private static DictationOrchestrator CreateOrchestrator(FakeAudioCapture audio) =>
        new(
            new GlobalHotkeyService(),
            audio,
            new FakeTranscription(),
            new FakeInputSimulator(),
            _ => { });

    [Fact]
    public void StartCaptureForDevice_SavedDeviceMatches_ResolvesToItsIndex()
    {
        var audio = new FakeAudioCapture(
            new AudioDeviceInfo(0, "Built-in Microphone", 1),
            new AudioDeviceInfo(1, "USB Headset", 1),
            new AudioDeviceInfo(2, "Microphone (Elgato Wave:3)", 1));
        var orchestrator = CreateOrchestrator(audio);

        var resolved = orchestrator.StartCaptureForDevice("Microphone (Elgato Wave:3)");

        Assert.Equal(2, resolved);
        Assert.Equal(2, audio.LastStartDeviceIndex);
    }

    [Fact]
    public void StartCaptureForDevice_NoSavedDevice_FallsBackToDefault()
    {
        var audio = new FakeAudioCapture(
            new AudioDeviceInfo(0, "Built-in Microphone", 1),
            new AudioDeviceInfo(1, "Microphone (Elgato Wave:3)", 1));
        var orchestrator = CreateOrchestrator(audio);

        var resolved = orchestrator.StartCaptureForDevice(null);

        Assert.Equal(-1, resolved);
        Assert.Equal(-1, audio.LastStartDeviceIndex);
    }

    [Fact]
    public void StartCaptureForDevice_SavedDeviceNoLongerPresent_FallsBackToDefault()
    {
        var audio = new FakeAudioCapture(
            new AudioDeviceInfo(0, "Built-in Microphone", 1));
        var orchestrator = CreateOrchestrator(audio);

        var resolved = orchestrator.StartCaptureForDevice("Microphone (Elgato Wave:3) (unplugged)");

        Assert.Equal(-1, resolved);
        Assert.Equal(-1, audio.LastStartDeviceIndex);
    }

    [Fact]
    public void StartCaptureForDevice_ResolvesFreshEachCall_NoCaching()
    {
        // A device change in settings must take effect on the very next hotkey
        // press without an app restart -- i.e. resolution must not be cached.
        var audio = new FakeAudioCapture(
            new AudioDeviceInfo(0, "Built-in Microphone", 1),
            new AudioDeviceInfo(1, "Microphone (Elgato Wave:3)", 1));
        var orchestrator = CreateOrchestrator(audio);

        var first = orchestrator.StartCaptureForDevice("Built-in Microphone");
        var second = orchestrator.StartCaptureForDevice("Microphone (Elgato Wave:3)");

        Assert.Equal(0, first);
        Assert.Equal(1, second);
        Assert.Equal(1, audio.LastStartDeviceIndex);
    }

    #region Fakes

    private sealed class FakeAudioCapture : IAudioCaptureService
    {
        private readonly IReadOnlyList<AudioDeviceInfo> _devices;

        public FakeAudioCapture(params AudioDeviceInfo[] devices)
        {
            _devices = devices;
        }

        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
        public event EventHandler? CaptureStarted;
        public event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;

        public bool IsCapturing { get; private set; }

        public int? LastStartDeviceIndex { get; private set; }

        public IReadOnlyList<AudioDeviceInfo> GetAvailableDevices() => _devices;

        public void StartCapture(int deviceIndex = -1)
        {
            LastStartDeviceIndex = deviceIndex;
            IsCapturing = true;
            CaptureStarted?.Invoke(this, EventArgs.Empty);
        }

        public void StopCapture()
        {
            IsCapturing = false;
        }

        public void Dispose()
        {
            // Referenced to silence "event never used" warnings for unused test doubles.
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
        public bool IsLoaded { get; set; } = true;

        public void LoadModel() => IsLoaded = true;

        public Task<TranscriptionResult> TranscribeAsync(
            float[] samples, int sampleRate = 16000,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TranscriptionResult("", TimeSpan.Zero, TimeSpan.Zero, 0));

        public void Dispose() { }
    }

    #endregion
}
