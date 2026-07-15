using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Recording;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Drives the recording-path device-selection fix (task main-c3x7q): before this fix,
/// <c>CallRecordingService.StartRecording</c> never resolved the saved microphone
/// (<c>Dictation.AudioDevice</c>) -- both the <c>TranscriptsPage</c> record button and
/// the call-recording hotkey always passed <c>-1</c> straight to
/// <see cref="AudioCaptureService"/>, so recording captured from the system default
/// mic even when the user had selected a different one for dictation.
///
/// These tests exercise the extracted <see cref="CallRecordingService.ResolveMicDeviceIndex"/>
/// seam directly (mirrors <c>DictationOrchestratorDeviceSelectionTests</c>' approach to
/// <c>DictationOrchestrator.StartCaptureForDevice</c>) since driving the wiring through a
/// full <see cref="CallRecordingService.StartRecording"/> call would require live NAudio
/// mic and WASAPI loopback devices, which CI hosts may not have (see
/// ADR-0009-honor-system-default-capture-device's note that WaveInEvent needs real
/// capture hardware to exercise directly).
/// </summary>
public class CallRecordingServiceDeviceSelectionTests
{
    [Fact]
    public void ResolveMicDeviceIndex_SavedDeviceMatches_ResolvesToItsIndex()
    {
        var audio = new FakeAudioCapture(
            new AudioDeviceInfo(0, "Built-in Microphone", 1),
            new AudioDeviceInfo(1, "USB Headset", 1),
            new AudioDeviceInfo(2, "Microphone (Elgato Wave:3)", 1));

        var resolved = CallRecordingService.ResolveMicDeviceIndex(audio, "Microphone (Elgato Wave:3)");

        Assert.Equal(2, resolved);
    }

    [Fact]
    public void ResolveMicDeviceIndex_NoSavedDevice_FallsBackToDefault()
    {
        var audio = new FakeAudioCapture(
            new AudioDeviceInfo(0, "Built-in Microphone", 1),
            new AudioDeviceInfo(1, "Microphone (Elgato Wave:3)", 1));

        var resolved = CallRecordingService.ResolveMicDeviceIndex(audio, null);

        Assert.Equal(-1, resolved);
    }

    [Fact]
    public void ResolveMicDeviceIndex_SavedDeviceNoLongerPresent_FallsBackToDefault()
    {
        var audio = new FakeAudioCapture(
            new AudioDeviceInfo(0, "Built-in Microphone", 1));

        var resolved = CallRecordingService.ResolveMicDeviceIndex(audio, "Microphone (Elgato Wave:3) (unplugged)");

        Assert.Equal(-1, resolved);
    }

    [Fact]
    public void ResolveMicDeviceIndex_ResolvesFreshEachCall_NoCaching()
    {
        // A device change in settings must take effect on the very next recording --
        // i.e. resolution must not be cached (StartRecording calls this fresh every time).
        var audio = new FakeAudioCapture(
            new AudioDeviceInfo(0, "Built-in Microphone", 1),
            new AudioDeviceInfo(1, "Microphone (Elgato Wave:3)", 1));

        var first = CallRecordingService.ResolveMicDeviceIndex(audio, "Built-in Microphone");
        var second = CallRecordingService.ResolveMicDeviceIndex(audio, "Microphone (Elgato Wave:3)");

        Assert.Equal(0, first);
        Assert.Equal(1, second);
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

    #endregion
}
