using System.Text;
using WhisperHeim.Services.CallTranscription;

namespace WhisperHeim.Services.Export;

/// <summary>
/// Single Markdown formatting implementation for a <see cref="CallTranscript"/>,
/// shared by the manual "MD" export button (<c>TranscriptsPage</c>) and
/// <see cref="TranscriptAutoExportService"/> (task main-m6x4v, ADR-0008).
/// Lifted verbatim from the former private
/// <c>TranscriptsPage.FormatAsMarkdown</c> so both call sites produce
/// byte-identical output — there is exactly one implementation now.
/// </summary>
public static class TranscriptMarkdownFormatter
{
    public static string Format(CallTranscript transcript)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Call Transcript");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Duration:** {transcript.Duration:hh\\:mm\\:ss}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        string? currentSpeaker = null;
        foreach (var segment in transcript.Segments)
        {
            var speaker = transcript.GetDisplaySpeaker(segment);
            if (speaker != currentSpeaker)
            {
                if (currentSpeaker is not null)
                    sb.AppendLine();
                sb.AppendLine($"### {speaker}");
                currentSpeaker = speaker;
            }

            sb.AppendLine($"*[{segment.StartTime:hh\\:mm\\:ss}]* {segment.Text}");
        }

        return sb.ToString();
    }
}
