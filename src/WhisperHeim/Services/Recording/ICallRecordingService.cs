namespace WhisperHeim.Services.Recording;

/// <summary>
/// Orchestrates simultaneous microphone and system audio capture for call recording.
/// Provides a unified start/stop interface and produces separate WAV files per stream.
/// </summary>
public interface ICallRecordingService : IDisposable
{
    /// <summary>
    /// Raised when recording has started and both streams are active.
    /// </summary>
    event EventHandler<CallRecordingSession>? RecordingStarted;

    /// <summary>
    /// Raised when recording has stopped (user-initiated or error).
    /// The session contains the final file paths and timing info.
    /// </summary>
    event EventHandler<CallRecordingStoppedEventArgs>? RecordingStopped;

    /// <summary>
    /// Raised every second while recording, carrying the current duration.
    /// Useful for updating tray tooltip or menu display.
    /// </summary>
    event EventHandler<TimeSpan>? DurationUpdated;

    /// <summary>
    /// Raised when one of the two capture streams fails but the other continues.
    /// </summary>
    event EventHandler<StreamFailedEventArgs>? StreamFailed;

    /// <summary>
    /// Whether a call recording is currently in progress.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// The current (or most recent) recording session, or null if none.
    /// </summary>
    CallRecordingSession? CurrentSession { get; }

    /// <summary>
    /// Starts recording both microphone and system audio simultaneously.
    /// The microphone device is resolved internally from the saved
    /// <c>Dictation.AudioDevice</c> setting (task main-c3x7q; see
    /// ADR-0009-honor-system-default-capture-device) -- falling back to the
    /// system default when nothing is saved or the saved device is gone --
    /// so callers never need to pass or look up a device index themselves.
    /// </summary>
    void StartRecording();

    /// <summary>
    /// Stops the current recording and finalizes the WAV files.
    /// </summary>
    void StopRecording();

    /// <summary>
    /// Toggles recording on/off. Convenience method for hotkey binding.
    /// See <see cref="StartRecording"/> for microphone device resolution.
    /// </summary>
    void ToggleRecording();
}

/// <summary>
/// Event args for when call recording has stopped.
/// </summary>
public sealed class CallRecordingStoppedEventArgs : EventArgs
{
    public CallRecordingStoppedEventArgs(CallRecordingSession session, Exception? exception = null)
    {
        Session = session;
        Exception = exception;
    }

    public CallRecordingSession Session { get; }
    public Exception? Exception { get; }
}

/// <summary>
/// Identifies which audio stream in a dual-capture session.
/// </summary>
public enum AudioStreamKind
{
    Microphone,
    System,
}

/// <summary>
/// Event args for when one stream in a dual-capture session fails.
/// </summary>
public sealed class StreamFailedEventArgs : EventArgs
{
    public StreamFailedEventArgs(AudioStreamKind stream, Exception? exception = null)
    {
        Stream = stream;
        Exception = exception;
    }

    public AudioStreamKind Stream { get; }
    public Exception? Exception { get; }
}
