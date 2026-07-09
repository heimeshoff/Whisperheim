using WhisperHeim.Services.CallTranscription;

namespace WhisperHeim.Services.Transcription;

/// <summary>
/// Builds the <see cref="CallTranscript"/> for an imported audio file (a single,
/// non-local speaker). Pulled out of <see cref="TranscriptionQueueService"/> as a pure
/// function so the speaker-name resolution (entered name vs. the "Speaker" fallback,
/// task main-k4t8p) is unit-testable without the WPF Dispatcher plumbing the rest of
/// the queue service depends on.
/// </summary>
internal static class FileImportTranscriptBuilder
{
    /// <summary>Fallback label used when no speaker name was entered at import time.</summary>
    public const string DefaultSpeakerLabel = "Speaker";

    /// <summary>
    /// Resolves the name entered in the import-time prompt to a usable label, falling
    /// back to <see cref="DefaultSpeakerLabel"/> when nothing (or only whitespace) was
    /// entered, or the prompt was dismissed.
    /// </summary>
    public static string ResolveSpeakerName(string? enteredSpeakerName) =>
        string.IsNullOrWhiteSpace(enteredSpeakerName) ? DefaultSpeakerLabel : enteredSpeakerName.Trim();

    /// <summary>
    /// Builds a single-speaker <see cref="CallTranscript"/> for an imported file,
    /// carrying the resolved speaker name into both the segment label and
    /// <see cref="CallTranscript.RemoteSpeakerNames"/> so the SPEAKER NAMES panel and
    /// Markdown export pick it up (task main-k4t8p).
    /// </summary>
    public static CallTranscript Build(
        string title,
        string audioFileName,
        DateTimeOffset enqueuedAt,
        TimeSpan audioDuration,
        string? text,
        string? enteredSpeakerName)
    {
        var speakerLabel = ResolveSpeakerName(enteredSpeakerName);

        var segments = new List<TranscriptSegment>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            segments.Add(new TranscriptSegment
            {
                Speaker = speakerLabel,
                StartTime = TimeSpan.Zero,
                EndTime = audioDuration,
                Text = text,
                IsLocalSpeaker = false,
            });
        }

        return new CallTranscript
        {
            Id = Guid.NewGuid().ToString(),
            Name = title,
            RecordingStartedUtc = enqueuedAt,
            RecordingEndedUtc = enqueuedAt + audioDuration,
            Segments = segments,
            AudioFilePath = audioFileName,
            RemoteSpeakerNames = new List<string> { speakerLabel },
        };
    }
}
