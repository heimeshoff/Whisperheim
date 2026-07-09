using System.IO;
using System.Text.Json.Serialization;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// A complete, structured transcript of a recorded call with speaker attribution
/// and timestamps for each segment.
/// </summary>
public sealed class CallTranscript
{
    /// <summary>Unique identifier for this transcript.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>User-editable display name for this transcript.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>UTC timestamp when the call recording started.</summary>
    [JsonPropertyName("recordingStartedUtc")]
    public required DateTimeOffset RecordingStartedUtc { get; init; }

    /// <summary>UTC timestamp when the call recording ended.</summary>
    [JsonPropertyName("recordingEndedUtc")]
    public required DateTimeOffset RecordingEndedUtc { get; init; }

    /// <summary>Total duration of the recorded call.</summary>
    [JsonPropertyName("duration")]
    public TimeSpan Duration => RecordingEndedUtc - RecordingStartedUtc;

    /// <summary>Ordered list of transcript segments with speaker labels and timestamps.</summary>
    [JsonPropertyName("segments")]
    public required IReadOnlyList<TranscriptSegment> Segments { get; init; }

    /// <summary>
    /// Global speaker name mappings: original label to custom display name.
    /// For example, {"Other": "Alice", "Speaker 2": "Bob"}.
    /// </summary>
    [JsonPropertyName("speakerNameMap")]
    public Dictionary<string, string> SpeakerNameMap { get; set; } = new();

    /// <summary>
    /// List of remote speaker names associated with this recording session.
    /// Used for re-transcription and as dropdown options in the transcript viewer.
    /// </summary>
    [JsonPropertyName("remoteSpeakerNames")]
    public List<string> RemoteSpeakerNames { get; set; } = new();

    /// <summary>
    /// Relative or absolute path to the preserved audio file (WAV) for playback.
    /// Stored relative to the transcript JSON file for portability.
    /// </summary>
    [JsonPropertyName("audioFilePath")]
    public string? AudioFilePath { get; set; }

    /// <summary>
    /// Path to the stored transcript JSON file, or null if not yet persisted.
    /// </summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    /// <summary>
    /// Absolute path to the Markdown file this transcript was last auto-exported
    /// to (see ADR-0008 / task main-m6x4v), or null if it has never been
    /// auto-exported. Used by <c>WhisperHeim.Services.Export.TranscriptAutoExportService</c> to
    /// decide whether re-transcribing the same, unrenamed recording session
    /// should overwrite this file in place rather than pick a disambiguated
    /// name. Not touched by manual "MD" exports (SaveFileDialog), and not
    /// updated on rename alone — a rename without re-transcription leaves this
    /// pointing at the (now stale) previously exported file.
    /// </summary>
    [JsonPropertyName("exportedMarkdownPath")]
    public string? ExportedMarkdownPath { get; set; }

    /// <summary>
    /// Resolves the absolute path to the audio file, interpreting AudioFilePath
    /// relative to the transcript JSON file's directory when necessary.
    /// Returns null if no audio file is configured or the file doesn't exist.
    /// </summary>
    [JsonIgnore]
    public string? ResolvedAudioFilePath
    {
        get
        {
            if (string.IsNullOrEmpty(AudioFilePath))
                return null;

            // If it's already absolute and exists, use it directly
            if (Path.IsPathRooted(AudioFilePath))
                return File.Exists(AudioFilePath) ? AudioFilePath : null;

            // Resolve relative to the transcript JSON file's directory
            if (!string.IsNullOrEmpty(FilePath))
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (dir is not null)
                {
                    var resolved = Path.GetFullPath(Path.Combine(dir, AudioFilePath));
                    return File.Exists(resolved) ? resolved : null;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Resolves mic.wav and system.wav paths in the transcript's session directory.
    /// Returns (micPath, systemPath) where either may be null if the file doesn't exist.
    /// </summary>
    [JsonIgnore]
    public (string? MicPath, string? SystemPath) ResolvedSourceAudioPaths
    {
        get
        {
            var dir = !string.IsNullOrEmpty(FilePath) ? Path.GetDirectoryName(FilePath) : null;
            if (dir is null)
                return (null, null);

            var mic = Path.Combine(dir, "mic.wav");
            var sys = Path.Combine(dir, "system.wav");
            return (
                File.Exists(mic) ? mic : null,
                File.Exists(sys) ? sys : null
            );
        }
    }

    /// <summary>
    /// Returns the display name for a segment, considering per-segment overrides first,
    /// then global speaker name mappings, and finally the original speaker label.
    /// </summary>
    public string GetDisplaySpeaker(TranscriptSegment segment)
    {
        // Per-segment override takes priority
        if (!string.IsNullOrEmpty(segment.SpeakerOverride))
            return segment.SpeakerOverride;

        // Global rename mapping
        if (SpeakerNameMap.TryGetValue(segment.Speaker, out var mapped))
            return mapped;

        return segment.Speaker;
    }

    /// <summary>
    /// Globally renames all segments with the given original speaker label.
    /// Clears any per-segment overrides that match the new name.
    /// </summary>
    public void RenameSpeakerGlobally(string originalSpeaker, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || originalSpeaker == newName)
            return;

        SpeakerNameMap[originalSpeaker] = newName;

        // Clear per-segment overrides on segments with this original speaker
        // that match the new global name (they're now redundant)
        foreach (var segment in Segments.Where(s => s.Speaker == originalSpeaker))
        {
            if (segment.SpeakerOverride == newName)
                segment.SpeakerOverride = null;
        }
    }
}

/// <summary>
/// A single segment within a call transcript, representing one speaker's
/// continuous utterance with timing and attribution.
/// </summary>
public sealed class TranscriptSegment
{
    /// <summary>Original speaker label assigned during transcription (e.g., "You", "Speaker 1").</summary>
    [JsonPropertyName("speaker")]
    public required string Speaker { get; init; }

    /// <summary>
    /// Optional per-segment speaker name override. When set, takes priority
    /// over the global speaker name map.
    /// </summary>
    [JsonPropertyName("speakerOverride")]
    public string? SpeakerOverride { get; set; }

    /// <summary>Start time of this segment relative to the recording start.</summary>
    [JsonPropertyName("startTime")]
    public required TimeSpan StartTime { get; init; }

    /// <summary>End time of this segment relative to the recording start.</summary>
    [JsonPropertyName("endTime")]
    public required TimeSpan EndTime { get; init; }

    /// <summary>Transcribed text for this segment.</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>Whether this segment came from the microphone (local user) or loopback (remote).</summary>
    [JsonPropertyName("isLocalSpeaker")]
    public required bool IsLocalSpeaker { get; init; }
}
