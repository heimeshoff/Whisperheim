using NAudio.Wave;

namespace WhisperHeim.Services.Audio;

/// <summary>
/// Captures microphone audio at 16kHz 16-bit mono using NAudio WaveInEvent,
/// converts to float32 normalized samples, and buffers via a ring buffer.
/// </summary>
public sealed class AudioCaptureService : IAudioCaptureService
{
    /// <summary>16kHz sample rate as required by Whisper models.</summary>
    public const int SampleRate = 16000;

    /// <summary>16-bit PCM.</summary>
    public const int BitsPerSample = 16;

    /// <summary>Mono channel.</summary>
    public const int Channels = 1;

    /// <summary>Ring buffer holds 30 seconds of audio at 16kHz.</summary>
    private const int RingBufferSeconds = 30;

    private static readonly WaveFormat CaptureFormat = new(SampleRate, BitsPerSample, Channels);

    private readonly AudioRingBuffer _ringBuffer;
    private WaveInEvent? _waveIn;
    private bool _disposed;
    private volatile bool _isCapturing;

    public AudioCaptureService()
    {
        _ringBuffer = new AudioRingBuffer(SampleRate * RingBufferSeconds);
    }

    /// <inheritdoc />
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    /// <inheritdoc />
    public event EventHandler? CaptureStarted;

    /// <inheritdoc />
    public event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;

    /// <inheritdoc />
    public bool IsCapturing => _isCapturing;

    /// <summary>
    /// Provides direct access to the ring buffer for consumers that want to
    /// pull samples on their own schedule rather than relying on events.
    /// </summary>
    public AudioRingBuffer RingBuffer => _ringBuffer;

    /// <inheritdoc />
    public IReadOnlyList<AudioDeviceInfo> GetAvailableDevices() => AudioDeviceResolver.EnumerateInputDevices();

    /// <inheritdoc />
    public void StartCapture(int deviceIndex = -1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isCapturing)
            return;

        // deviceIndex < 0 (typically -1) is passed straight through to NAudio's
        // WaveInEvent.DeviceNumber, which treats a negative device number as
        // WAVE_MAPPER -- the actual Windows-preferred default capture device.
        // Earlier code forced this to device 0 ("NAudio maps to device 0"), which
        // was wrong: it silently opened the first enumerated device instead of the
        // real system default, defeating the fallback path in
        // AudioDeviceResolver.ResolveDeviceIndex (task main-v7k2d; see
        // ADR-0009-honor-system-default-capture-device).

        int deviceCount = WaveInEvent.DeviceCount;
        System.Diagnostics.Trace.TraceInformation(
            "[AudioCaptureService] Starting capture on device {0} (of {1} available)",
            deviceIndex, deviceCount);

        if (deviceCount == 0)
        {
            throw new InvalidOperationException("No audio input devices found.");
        }

        _ringBuffer.Clear();

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceIndex,
            WaveFormat = CaptureFormat,
            // Buffer 50ms chunks -> 800 samples at 16kHz
            BufferMilliseconds = 50,
            NumberOfBuffers = 3,
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        try
        {
            _waveIn.StartRecording();
            _isCapturing = true;
            System.Diagnostics.Trace.TraceInformation(
                "[AudioCaptureService] Recording started successfully.");
            CaptureStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                "[AudioCaptureService] StartRecording failed: {0}", ex);
            CleanupWaveIn();
            throw;
        }
    }

    /// <inheritdoc />
    public void StopCapture()
    {
        if (!_isCapturing)
            return;

        _isCapturing = false;

        try
        {
            _waveIn?.StopRecording();
        }
        catch (Exception)
        {
            // StopRecording may throw if device was already disconnected.
            // The RecordingStopped event handler will deal with cleanup.
            CleanupWaveIn();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopCapture();
        CleanupWaveIn();
    }

    /// <summary>
    /// Called on the NAudio capture thread when PCM data is available.
    /// Converts 16-bit PCM to float32 normalized and pushes into the ring buffer.
    /// </summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            int bytesRecorded = e.BytesRecorded;
            if (bytesRecorded == 0)
                return;

            // Each sample is 2 bytes (16-bit)
            int sampleCount = bytesRecorded / 2;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short pcm16 = BitConverter.ToInt16(e.Buffer, i * 2);
                samples[i] = pcm16 / 32768f;
            }

            // Push into ring buffer
            _ringBuffer.Write(samples);

            // Raise event for subscribers
            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(samples));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("[AudioCaptureService] OnDataAvailable error: {0}", ex);
        }
    }

    /// <summary>
    /// Called when NAudio stops recording (user stop or device disconnection).
    /// </summary>
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            _isCapturing = false;

            bool deviceDisconnected = e.Exception is not null;

            if (e.Exception is not null)
            {
                System.Diagnostics.Trace.TraceError(
                    "[AudioCaptureService] RecordingStopped with exception: {0}", e.Exception);
            }
            else
            {
                System.Diagnostics.Trace.TraceInformation(
                    "[AudioCaptureService] RecordingStopped normally.");
            }

            CleanupWaveIn();

            CaptureStopped?.Invoke(this, new CaptureStoppedEventArgs(
                wasDeviceDisconnected: deviceDisconnected,
                exception: e.Exception));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("[AudioCaptureService] OnRecordingStopped error: {0}", ex);
        }
    }

    private void CleanupWaveIn()
    {
        if (_waveIn is null)
            return;

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;
        _waveIn.Dispose();
        _waveIn = null;
    }
}
