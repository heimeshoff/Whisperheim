namespace WhisperHeim.Views;

/// <summary>
/// Represents the visual state of the dictation overlay microphone indicator.
/// </summary>
public enum OverlayMicState
{
    /// <summary>
    /// Microphone is connected and listening, but no speech is detected.
    /// Overlay: green, static.
    /// </summary>
    Idle,

    /// <summary>
    /// VAD has detected speech. Overlay: green with RMS-driven ring scaling.
    /// </summary>
    Speaking,

    /// <summary>
    /// No microphone found or no audio input available.
    /// Overlay: grey, static.
    /// </summary>
    NoMic,

    /// <summary>
    /// Transcribe-on-release is awaiting an in-flight model load — the held utterance
    /// outran the lazy load (task infrastructure-q4t8m, ADR-0005/0006). Overlay: all
    /// bars breathe up/down in sync in amber on a ~1 s cycle, ignoring RMS — reads as
    /// "working", not frozen. Distinct from Speaking (orange, RMS-driven) and Idle (grey).
    /// </summary>
    WarmingUp,

    /// <summary>
    /// A pipeline or system error has occurred.
    /// Overlay: red, static.
    /// </summary>
    Error,

    /// <summary>
    /// A recording above the hold-to-talk minimum decoded to nothing typeable —
    /// either an empty raw transcript or one the clean-text pipeline reduced to
    /// empty (task main-rc541, <c>DictationOrchestrator.NothingRecognized</c>).
    /// Overlay: neutral grey, static flat bars, "Nothing recognized" label. The
    /// pill re-shows (or defers its hide) for ~1.5 s then hides on its own —
    /// distinct from Error (red), which always takes precedence if it arrives
    /// while this hold is showing.
    /// </summary>
    NothingRecognized
}
