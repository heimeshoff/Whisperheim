using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Export;

namespace WhisperHeim.Tests;

/// <summary>
/// Covers <see cref="TranscriptMarkdownFormatter.Format"/> — the single
/// implementation shared by the manual "MD" export button and
/// <see cref="TranscriptAutoExportService"/> (task main-m6x4v). Locks down
/// the exact output shape (header, duration, speaker-grouped sections,
/// timestamped lines) so future edits to either call site can't silently
/// diverge the two exports.
/// </summary>
public class TranscriptMarkdownFormatterTests
{
    private static CallTranscript MakeTranscript(IReadOnlyList<TranscriptSegment> segments) => new()
    {
        Id = "t1",
        Name = "Test Call",
        RecordingStartedUtc = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero),
        RecordingEndedUtc = new DateTimeOffset(2026, 7, 9, 12, 5, 30, TimeSpan.Zero),
        Segments = segments,
    };

    [Fact]
    public void Format_IncludesHeaderDateAndDuration()
    {
        var transcript = MakeTranscript(Array.Empty<TranscriptSegment>());

        var markdown = TranscriptMarkdownFormatter.Format(transcript);

        Assert.Contains("# Call Transcript", markdown);
        Assert.Contains($"**Date:** {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}", markdown);
        Assert.Contains("**Duration:** 00:05:30", markdown);
    }

    [Fact]
    public void Format_GroupsConsecutiveSegmentsBySpeakerUnderOneHeading()
    {
        var segments = new List<TranscriptSegment>
        {
            new() { Speaker = "You", StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(2), Text = "Hello", IsLocalSpeaker = true },
            new() { Speaker = "You", StartTime = TimeSpan.FromSeconds(2), EndTime = TimeSpan.FromSeconds(4), Text = "there", IsLocalSpeaker = true },
            new() { Speaker = "Other", StartTime = TimeSpan.FromSeconds(4), EndTime = TimeSpan.FromSeconds(6), Text = "Hi!", IsLocalSpeaker = false },
        };
        var transcript = MakeTranscript(segments);

        var markdown = TranscriptMarkdownFormatter.Format(transcript);

        // Exactly one heading per speaker run, not per segment.
        Assert.Equal(1, CountOccurrences(markdown, "### You"));
        Assert.Equal(1, CountOccurrences(markdown, "### Other"));
        Assert.Contains("*[00:00:00]* Hello", markdown);
        Assert.Contains("*[00:00:02]* there", markdown);
        Assert.Contains("*[00:00:04]* Hi!", markdown);

        // Speaker order preserved: "You" heading appears before "Other".
        Assert.True(markdown.IndexOf("### You", StringComparison.Ordinal) <
                    markdown.IndexOf("### Other", StringComparison.Ordinal));
    }

    [Fact]
    public void Format_UsesDisplaySpeakerNameOverride()
    {
        var segments = new List<TranscriptSegment>
        {
            new() { Speaker = "Speaker 1", StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(1), Text = "Hi", IsLocalSpeaker = false },
        };
        var transcript = MakeTranscript(segments);
        transcript.SpeakerNameMap["Speaker 1"] = "Alice";

        var markdown = TranscriptMarkdownFormatter.Format(transcript);

        Assert.Contains("### Alice", markdown);
        Assert.DoesNotContain("### Speaker 1", markdown);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
