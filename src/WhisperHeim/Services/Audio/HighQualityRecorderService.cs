using System.Diagnostics;
using System.IO;
using NAudio.Wave;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Services.Audio;

/// <summary>
/// Records microphone audio at high quality (44.1kHz 16-bit mono).
/// This is separate from <see cref="AudioCaptureService"/> which records at 16kHz for Whisper.
/// </summary>
/// <remarks>
/// Active capture writes go to the machine-local staging directory
/// (<see cref="DataPathService.RecordingStagingPath"/>) so cloud sync clients
/// never see a growing file. <see cref="SaveRecording"/> then copies the
/// finished WAV to the user-supplied destination. If no
/// <see cref="DataPathService"/> is supplied we fall back to <c>%TEMP%</c>
/// (test / standalone scenarios).
/// </remarks>
public sealed class HighQualityRecorderService : IHighQualityRecorderService
{
    /// <summary>44.1kHz sample rate for high-quality voice recording.</summary>
    public const int SampleRate = 44100;

    /// <summary>16-bit PCM.</summary>
    public const int BitsPerSample = 16;

    /// <summary>Mono channel.</summary>
    public const int Channels = 1;

    private static readonly WaveFormat RecordingFormat = new(SampleRate, BitsPerSample, Channels);

    private readonly DataPathService? _dataPathService;

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _tempFilePath;
    private string? _lastRecordingPath;
    private readonly Stopwatch _durationTimer = new();
    private System.Threading.Timer? _durationUpdateTimer;
    private bool _disposed;
    private volatile bool _isRecording;

    public HighQualityRecorderService(DataPathService? dataPathService = null)
    {
        _dataPathService = dataPathService;
    }

    /// <inheritdoc />
    public bool IsRecording => _isRecording;

    /// <inheritdoc />
    public TimeSpan Duration => _durationTimer.Elapsed;

    /// <inheritdoc />
    public event EventHandler<float>? LevelChanged;

    /// <inheritdoc />
    public event EventHandler<TimeSpan>? DurationChanged;

    /// <inheritdoc />
    public event EventHandler<RecordingStoppedEventArgs>? RecordingStopped;

    /// <inheritdoc />
    public IReadOnlyList<AudioDeviceInfo> GetAvailableDevices() => AudioDeviceResolver.EnumerateInputDevices();

    /// <inheritdoc />
    public void StartRecording(int deviceIndex = -1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRecording)
            return;

        if (deviceIndex < 0)
            deviceIndex = 0;

        int deviceCount = WaveInEvent.DeviceCount;
        Trace.TraceInformation(
            "[HighQualityRecorder] Starting recording on device {0} (of {1} available) at {2}Hz",
            deviceIndex, deviceCount, SampleRate);

        if (deviceCount == 0)
            throw new InvalidOperationException("No audio input devices found.");

        // Create staging file for recording. Prefer the shared
        // RecordingStagingPath so the file lives next to call recordings
        // (centralized for the crash-recovery sweep) and never sits inside
        // a cloud-synced DataPath. Falls back to %TEMP% when the service is
        // constructed without a DataPathService.
        var stagingRoot = _dataPathService?.RecordingStagingPath ?? Path.GetTempPath();
        Directory.CreateDirectory(stagingRoot);
        _tempFilePath = Path.Combine(stagingRoot, $"whisperheim_voice_{Guid.NewGuid():N}.wav");
        _writer = new WaveFileWriter(_tempFilePath, RecordingFormat);

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceIndex,
            WaveFormat = RecordingFormat,
            BufferMilliseconds = 50,
            NumberOfBuffers = 3,
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnWaveInStopped;

        try
        {
            _waveIn.StartRecording();
            _isRecording = true;
            _durationTimer.Restart();

            // Start a timer that fires every ~100ms to raise DurationChanged
            _durationUpdateTimer = new System.Threading.Timer(
                _ =>
                {
                    try { DurationChanged?.Invoke(this, _durationTimer.Elapsed); }
                    catch (Exception ex) { Trace.TraceError("[HighQualityRecorder] DurationChanged error: {0}", ex); }
                },
                null,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(100));

            Trace.TraceInformation("[HighQualityRecorder] Recording started.");
        }
        catch (Exception ex)
        {
            Trace.TraceError("[HighQualityRecorder] StartRecording failed: {0}", ex);
            CleanupRecording();
            throw;
        }
    }

    /// <inheritdoc />
    public string? StopRecording()
    {
        if (!_isRecording)
            return _lastRecordingPath;

        _isRecording = false;
        _durationTimer.Stop();
        _durationUpdateTimer?.Dispose();
        _durationUpdateTimer = null;

        try
        {
            _waveIn?.StopRecording();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[HighQualityRecorder] StopRecording error: {0}", ex);
        }

        // Finalize the WAV file
        try
        {
            _writer?.Dispose();
            _writer = null;
        }
        catch (Exception ex)
        {
            Trace.TraceError("[HighQualityRecorder] Error finalizing WAV: {0}", ex);
        }

        CleanupWaveIn();

        _lastRecordingPath = _tempFilePath;
        Trace.TraceInformation(
            "[HighQualityRecorder] Recording stopped. Duration: {0:F1}s, File: {1}",
            _durationTimer.Elapsed.TotalSeconds, _tempFilePath);

        return _lastRecordingPath;
    }

    /// <inheritdoc />
    public bool SaveRecording(string destinationPath)
    {
        if (_lastRecordingPath is null || !File.Exists(_lastRecordingPath))
        {
            Trace.TraceWarning("[HighQualityRecorder] No recording to save.");
            return false;
        }

        try
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(destinationPath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            // Copy from staging into the (potentially synced) destination.
            // We copy (not move) so the staged file can still be saved again
            // under a different name if the caller chooses to.
            File.Copy(_lastRecordingPath, destinationPath, overwrite: true);
            Trace.TraceInformation("[HighQualityRecorder] Recording saved to: {0}", destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("[HighQualityRecorder] Failed to save recording: {0}", ex);
            return false;
        }
    }

    /// <summary>
    /// Called on the NAudio capture thread when PCM data is available.
    /// Writes to the WAV file and calculates RMS level.
    /// </summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            if (e.BytesRecorded == 0)
                return;

            // Write raw PCM to WAV file
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);

            // Calculate RMS level for the level meter
            int sampleCount = e.BytesRecorded / 2; // 16-bit samples
            double sumSquares = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                short pcm16 = BitConverter.ToInt16(e.Buffer, i * 2);
                float sample = pcm16 / 32768f;
                sumSquares += sample * sample;
            }

            float rms = (float)Math.Sqrt(sumSquares / sampleCount);
            // Clamp to [0, 1]
            rms = Math.Min(1.0f, rms * 3.0f); // Amplify a bit for visual feedback

            LevelChanged?.Invoke(this, rms);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[HighQualityRecorder] OnDataAvailable error: {0}", ex);
        }
    }

    /// <summary>
    /// Called when NAudio stops recording.
    /// </summary>
    private void OnWaveInStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            _isRecording = false;

            if (e.Exception is not null)
            {
                Trace.TraceError("[HighQualityRecorder] Recording stopped with error: {0}", e.Exception);
            }

            RecordingStopped?.Invoke(this, new RecordingStoppedEventArgs(
                success: e.Exception is null,
                filePath: _tempFilePath,
                exception: e.Exception));
        }
        catch (Exception ex)
        {
            Trace.TraceError("[HighQualityRecorder] OnWaveInStopped error: {0}", ex);
        }
    }

    private void CleanupRecording()
    {
        _durationUpdateTimer?.Dispose();
        _durationUpdateTimer = null;
        _writer?.Dispose();
        _writer = null;
        CleanupWaveIn();
    }

    private void CleanupWaveIn()
    {
        if (_waveIn is null)
            return;

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnWaveInStopped;
        _waveIn.Dispose();
        _waveIn = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_isRecording)
            StopRecording();

        CleanupRecording();

        // Clean up temp file
        if (_lastRecordingPath is not null && File.Exists(_lastRecordingPath))
        {
            try { File.Delete(_lastRecordingPath); }
            catch { /* best effort */ }
        }
    }
}
