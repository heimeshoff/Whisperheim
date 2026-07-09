using System.Diagnostics;
using System.IO;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.Export;

/// <summary>
/// Subscriber to <see cref="TranscriptionQueueService.ItemCompleted"/> that
/// auto-exports a completed transcript as Markdown into a configured
/// machine-local folder (task main-m6x4v, ADR-0008). Independent of the
/// existing subscribers (transcribing-lock cleanup, drawer refresh) — this
/// one owns no invariant, so it lives outside the queue aggregate and is
/// wired up externally (see <c>App.xaml.cs</c>).
///
/// <para>
/// The queue item never carries a built <see cref="CallTranscript"/> for
/// Recording-type items (the pipeline's result is discarded by
/// <c>TranscriptionQueueService.ProcessRecordingItem</c>), so this service
/// reloads <c>transcript.json</c> from the session directory instead — which
/// is on disk by the time <c>ItemCompleted</c> fires on both the recording
/// and file-import paths.
/// </para>
///
/// <para>
/// Overwrite-vs-disambiguate identity is keyed to the Recording-session
/// directory, not <see cref="CallTranscript.Id"/> (which is regenerated on
/// every re-transcription) — see ADR-0008. Collisions between same-titled
/// sibling sessions are resolved by scanning sibling transcripts' own
/// persisted <see cref="CallTranscript.ExportedMarkdownPath"/> claims, never
/// by inspecting whether a file happens to already exist on disk at the
/// candidate path (that file could just be this same session's own prior
/// export, safe to overwrite).
/// </para>
/// </summary>
public sealed class TranscriptAutoExportService
{
    private readonly ITranscriptStorageService _transcriptStorage;
    private readonly DataPathService _dataPathService;

    public TranscriptAutoExportService(
        ITranscriptStorageService transcriptStorage,
        DataPathService dataPathService)
    {
        _transcriptStorage = transcriptStorage;
        _dataPathService = dataPathService;
    }

    /// <summary>
    /// Event-handler-shaped entry point for wiring onto
    /// <see cref="TranscriptionQueueService.ItemCompleted"/> (fire-and-forget:
    /// export failures are swallowed inside <see cref="ExportIfConfiguredAsync"/>
    /// and must never affect the already-Completed queue item).
    /// </summary>
    public async void OnItemCompleted(object? sender, TranscriptionQueueItem item)
        => await ExportIfConfiguredAsync(item);

    /// <summary>
    /// Exports <paramref name="item"/>'s transcript as Markdown into the
    /// configured folder for its <see cref="QueueItemType"/>, if configured.
    /// Never throws — every failure mode (no session dir, folder not
    /// configured, transcript.json missing, folder missing/unwritable) is a
    /// silent skip with a logged warning, so a transcription that already
    /// succeeded is never retroactively affected by an export-time problem.
    /// </summary>
    public async Task ExportIfConfiguredAsync(
        TranscriptionQueueItem item, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionDir = ResolveSessionDir(item);
            if (sessionDir is null)
            {
                // STT API responses and EnqueueFile (no SessionDir) persist
                // nothing to reload from — nothing to export.
                return;
            }

            var folder = item.ItemType == QueueItemType.Recording
                ? _dataPathService.Bootstrap.RecordingsExportFolder
                : _dataPathService.Bootstrap.ImportsExportFolder;

            if (string.IsNullOrWhiteSpace(folder))
                return; // That kind's auto-export is not configured.

            var transcriptPath = Path.Combine(sessionDir, "transcript.json");
            var transcript = await _transcriptStorage.LoadAsync(transcriptPath, cancellationToken);
            if (transcript is null)
            {
                Trace.TraceWarning(
                    "[TranscriptAutoExport] No transcript.json at {0}; skipping auto-export for '{1}'.",
                    transcriptPath, item.Title);
                return;
            }

            if (!IsFolderUsable(folder))
            {
                Trace.TraceWarning(
                    "[TranscriptAutoExport] Export folder missing or unwritable, skipping: {0}", folder);
                return;
            }

            var claimedBySiblings = await LoadSiblingClaimsAsync(transcript, cancellationToken);
            var exportPath = ResolveExportPath(transcript, folder, claimedBySiblings);

            var markdown = TranscriptMarkdownFormatter.Format(transcript);
            File.WriteAllText(exportPath, markdown);

            transcript.ExportedMarkdownPath = exportPath;
            await _transcriptStorage.UpdateAsync(transcript, cancellationToken);

            Trace.TraceInformation(
                "[TranscriptAutoExport] Exported '{0}' to {1}", item.Title, exportPath);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[TranscriptAutoExport] Auto-export failed for '{0}': {1}", item.Title, ex.Message);
        }
    }

    /// <summary>
    /// Resolves the session directory that owns <paramref name="item"/>'s
    /// persisted <c>transcript.json</c>, mirroring the pattern used elsewhere
    /// on the same event (<c>TranscriptsPage.OnQueueItemCompleted</c>,
    /// <c>TranscriptionQueueService.GetSessionDir</c>). Returns null for
    /// completions with no on-disk session (STT API / <c>EnqueueFile</c>).
    /// </summary>
    internal static string? ResolveSessionDir(TranscriptionQueueItem item)
    {
        if (item.SessionDir is not null)
            return item.SessionDir;

        if (item.ItemType == QueueItemType.Recording && item.Session is not null)
            return Path.GetDirectoryName(item.Session.MicWavFilePath);

        return null;
    }

    /// <summary>
    /// True when <paramref name="folder"/> exists and a probe file can be
    /// written to and deleted from it. Deliberately does <em>not</em> create
    /// the folder when missing — an export folder that has disappeared (an
    /// unmounted drive, a folder the user deleted) is treated as "not
    /// available right now", the same conservative silent-skip stance ADR-0008
    /// documents, not an invitation to recreate it.
    /// </summary>
    private static bool IsFolderUsable(string folder)
    {
        if (!Directory.Exists(folder))
            return false;

        try
        {
            var probe = Path.Combine(folder, $".whisperheim_export_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads every other transcript's <see cref="CallTranscript.ExportedMarkdownPath"/>
    /// (excluding <paramref name="current"/>'s own transcript.json) into a
    /// set of claimed export paths, so <see cref="ResolveExportPath"/> can
    /// tell a genuine sibling-title collision apart from this same session's
    /// own previously-exported file.
    /// </summary>
    private async Task<HashSet<string>> LoadSiblingClaimsAsync(
        CallTranscript current, CancellationToken cancellationToken)
    {
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<string> files;
        try
        {
            files = _transcriptStorage.ListTranscriptFiles();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[TranscriptAutoExport] Failed to list sibling transcripts for collision check: {0}",
                ex.Message);
            return claimed;
        }

        foreach (var file in files)
        {
            if (current.FilePath is not null && IsSameFile(file, current.FilePath))
                continue; // This session's own transcript.json.

            CallTranscript? other;
            try
            {
                other = await _transcriptStorage.LoadAsync(file, cancellationToken);
            }
            catch
            {
                continue; // Best-effort — a corrupt sibling shouldn't block export.
            }

            if (!string.IsNullOrEmpty(other?.ExportedMarkdownPath))
                claimed.Add(other!.ExportedMarkdownPath!);
        }

        return claimed;
    }

    private static bool IsSameFile(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Computes the Markdown path to write <paramref name="transcript"/> to
    /// inside <paramref name="folder"/> — see ADR-0008.
    /// </summary>
    internal static string ResolveExportPath(
        CallTranscript transcript, string folder, HashSet<string> claimedBySiblings)
    {
        var sanitizedName = ExportFileName.Sanitize(transcript.Name, transcript.RecordingStartedUtc);
        var desired = Path.Combine(folder, sanitizedName + ".md");

        // Re-transcription of the same, unrenamed recording: overwrite in place.
        if (string.Equals(transcript.ExportedMarkdownPath, desired, StringComparison.OrdinalIgnoreCase))
            return desired;

        if (!claimedBySiblings.Contains(desired))
            return desired;

        var suffix = 2;
        string candidate;
        do
        {
            candidate = Path.Combine(folder, $"{sanitizedName} ({suffix}).md");
            suffix++;
        } while (claimedBySiblings.Contains(candidate));

        return candidate;
    }
}
