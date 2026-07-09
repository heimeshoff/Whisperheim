using WhisperHeim.Services.Export;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Tests;

/// <summary>
/// Covers <see cref="FileImportTranscriptBuilder"/> — the speaker-name resolution for
/// imported audio files (task main-k4t8p). An entered name at import time should flow
/// into both the segment label and <c>RemoteSpeakerNames</c> (so the SPEAKER NAMES
/// panel shows a row and Markdown export attributes the transcript correctly); an
/// empty/dismissed prompt should fall back to the literal "Speaker" label unchanged
/// from before this task.
/// </summary>
public class FileImportTranscriptBuilderTests
{
    private static readonly DateTimeOffset EnqueuedAt = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_WithEnteredName_SetsSegmentSpeakerAndRemoteSpeakerNames()
    {
        var transcript = FileImportTranscriptBuilder.Build(
            title: "voice-note",
            audioFileName: "voice-note.ogg",
            enqueuedAt: EnqueuedAt,
            audioDuration: TimeSpan.FromSeconds(12),
            text: "Hey, it's me.",
            enteredSpeakerName: "Mom");

        var segment = Assert.Single(transcript.Segments);
        Assert.Equal("Mom", segment.Speaker);
        Assert.False(segment.IsLocalSpeaker);
        Assert.Equal(new List<string> { "Mom" }, transcript.RemoteSpeakerNames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WithEmptyOrDismissedName_FallsBackToSpeakerLabel(string? enteredName)
    {
        var transcript = FileImportTranscriptBuilder.Build(
            title: "voice-note",
            audioFileName: "voice-note.ogg",
            enqueuedAt: EnqueuedAt,
            audioDuration: TimeSpan.FromSeconds(3),
            text: "Some words.",
            enteredSpeakerName: enteredName);

        var segment = Assert.Single(transcript.Segments);
        Assert.Equal("Speaker", segment.Speaker);
        Assert.Equal(new List<string> { "Speaker" }, transcript.RemoteSpeakerNames);
    }

    [Fact]
    public void Build_TrimsWhitespaceAroundEnteredName()
    {
        var transcript = FileImportTranscriptBuilder.Build(
            title: "voice-note",
            audioFileName: "voice-note.ogg",
            enqueuedAt: EnqueuedAt,
            audioDuration: TimeSpan.FromSeconds(3),
            text: "Some words.",
            enteredSpeakerName: "  Dad  ");

        var segment = Assert.Single(transcript.Segments);
        Assert.Equal("Dad", segment.Speaker);
    }

    [Fact]
    public void Build_MarkdownExport_UsesResolvedSpeakerAsHeading()
    {
        var transcript = FileImportTranscriptBuilder.Build(
            title: "voice-note",
            audioFileName: "voice-note.ogg",
            enqueuedAt: EnqueuedAt,
            audioDuration: TimeSpan.FromSeconds(3),
            text: "Hi from the voice note.",
            enteredSpeakerName: "Grandma");

        var markdown = TranscriptMarkdownFormatter.Format(transcript);

        Assert.Contains("### Grandma", markdown);
        Assert.DoesNotContain("### Speaker", markdown);
    }

    [Fact]
    public void Build_WithNoSpeechDetected_ProducesNoSegmentsButStillSeedsRemoteSpeakerNames()
    {
        var transcript = FileImportTranscriptBuilder.Build(
            title: "silent-note",
            audioFileName: "silent-note.ogg",
            enqueuedAt: EnqueuedAt,
            audioDuration: TimeSpan.FromSeconds(1),
            text: "",
            enteredSpeakerName: "Uncle Bob");

        Assert.Empty(transcript.Segments);
        Assert.Equal(new List<string> { "Uncle Bob" }, transcript.RemoteSpeakerNames);
    }
}
