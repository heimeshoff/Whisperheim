using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Headless service that auto-enqueues call-recording sessions for transcription
/// as soon as they stop. Owned by App so the path runs even when no window has
/// been constructed (the start-minimized → tray-only flow).
///
/// <para>
/// Historically, <c>TranscriptsPage</c> itself subscribed to
/// <see cref="ICallRecordingService.RecordingStopped"/> and called
/// <c>TranscriptionQueueService.Enqueue</c>. That coupled auto-transcription to
/// the page lifetime, which forced MainWindow (and its tray icon) to be
/// eagerly constructed at startup. Extracting it here lets MainWindow be
/// constructed lazily on first open while still guaranteeing every recording
/// gets queued.
/// </para>
///
/// <para>
/// When the page is open it still applies title and speaker-name edits in its
/// own <c>OnRecordingStopped</c> handler — but those mutations happen on the
/// <see cref="CallRecordingSession"/> instance before the event is raised, so
/// they're visible to this service via <see cref="CallRecordingSession.Title"/>
/// and <see cref="CallRecordingSession.RemoteSpeakerNames"/>.
/// </para>
///
/// <para>
/// It also owns <see cref="RequeuePendingSessions"/>: the transcription queue
/// is in-memory only, so a recording whose transcription was still running when
/// the app exited would otherwise sit "pending" forever. App calls it once
/// after startup to pick that work back up.
/// </para>
/// </summary>
public sealed class AutoTranscriptionService : IDisposable
{
    private readonly ICallRecordingService _recordingService;
    private readonly TranscriptionQueueService _queueService;
    private readonly TranscriptStorageService _storageService;
    private bool _disposed;

    /// <summary>
    /// Audio extensions eligible for the file-import requeue path (mirrors
    /// the set <see cref="TranscriptStorageService"/> uses to detect pending
    /// sessions).
    /// </summary>
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".ogg", ".mp3", ".m4a"
    };

    public AutoTranscriptionService(
        ICallRecordingService recordingService,
        TranscriptionQueueService queueService,
        TranscriptStorageService storageService)
    {
        _recordingService = recordingService;
        _queueService = queueService;
        _storageService = storageService;
        _recordingService.RecordingStopped += OnRecordingStopped;
    }

    private void OnRecordingStopped(object? sender, CallRecordingStoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            Trace.TraceWarning(
                "[AutoTranscription] Recording stopped with error, skipping auto-enqueue: {0}",
                e.Exception.Message);
            return;
        }

        // Defer the enqueue to Background priority so any UI subscribers
        // (TranscriptsPage's RecordingStopped handler runs at Normal priority
        // and mutates session.Title / session.RemoteSpeakerNames from the
        // drawer's edit fields) finish first. When no page is open — the
        // start-minimized flow — this just runs immediately with defaults.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null)
        {
            dispatcher.BeginInvoke(DispatcherPriority.Background, () => EnqueueWithDefaults(e.Session));
        }
        else
        {
            EnqueueWithDefaults(e.Session);
        }
    }

    private void EnqueueWithDefaults(CallRecordingSession session)
    {
        var title = !string.IsNullOrWhiteSpace(session.Title)
            ? session.Title!
            : $"Call {session.StartTimestamp.LocalDateTime:yyyy-MM-dd HH:mm}";

        _queueService.Enqueue(title, session);
        Trace.TraceInformation("[AutoTranscription] Auto-enqueued recording: {0}", title);
    }

    // ── Startup requeue of interrupted / never-transcribed sessions ─────

    /// <summary>
    /// Re-enqueues every pending session owned by this machine (audio present,
    /// no transcript.json, retry limit not exhausted). The queue does not
    /// survive an app exit, so without this a transcription interrupted by
    /// shutdown leaves its recording pending until manually re-queued.
    /// Must be called on the dispatcher thread (it reads the queue's Items).
    /// </summary>
    public void RequeuePendingSessions()
    {
        IReadOnlyList<string> pendingDirs;
        try
        {
            pendingDirs = _storageService.ListPendingSessions();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[AutoTranscription] Pending-session scan failed: {0}", ex.Message);
            return;
        }

        foreach (var dir in pendingDirs)
        {
            try
            {
                RequeuePendingSession(dir);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "[AutoTranscription] Could not requeue pending session '{0}': {1}",
                    dir, ex.Message);
            }
        }
    }

    private void RequeuePendingSession(string sessionDir)
    {
        if (IsAlreadyQueued(sessionDir))
            return;

        var (savedName, speakers) = LoadPendingMetadata(sessionDir);
        var micPath = Path.Combine(sessionDir, "mic.wav");

        if (File.Exists(micPath))
        {
            // Call recording session — mirror the manual queue action on the
            // Transcripts page: timestamps from the dir name (fallback: file
            // times), title/speakers from transcript_name.json.
            var dirName = Path.GetFileName(sessionDir);
            DateTimeOffset startTimestamp;
            if (dirName.Length >= 15 &&
                DateTime.TryParseExact(
                    dirName[..15],
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date))
            {
                startTimestamp = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
            }
            else
            {
                startTimestamp = new DateTimeOffset(File.GetCreationTimeUtc(micPath), TimeSpan.Zero);
            }

            var systemPath = Path.Combine(sessionDir, "system.wav");
            var session = new CallRecordingSession(
                micPath,
                File.Exists(systemPath) ? systemPath : micPath,
                startTimestamp)
            {
                EndTimestamp = new DateTimeOffset(File.GetLastWriteTimeUtc(micPath), TimeSpan.Zero),
                RemoteSpeakerNames = speakers,
            };

            var title = !string.IsNullOrWhiteSpace(savedName)
                ? savedName!
                : $"Call {startTimestamp.LocalDateTime:yyyy-MM-dd HH:mm}";

            _queueService.Enqueue(title, session);
            Trace.TraceInformation(
                "[AutoTranscription] Requeued pending recording: {0} ({1})", title, sessionDir);
        }
        else
        {
            // Imported audio file that never finished transcribing.
            var audioFile = Directory.GetFiles(sessionDir)
                .FirstOrDefault(f => AudioExtensions.Contains(Path.GetExtension(f)));
            if (audioFile is null)
                return;

            var title = !string.IsNullOrWhiteSpace(savedName)
                ? savedName!
                : Path.GetFileNameWithoutExtension(audioFile);

            _queueService.EnqueueFileImport(title, audioFile, sessionDir);
            Trace.TraceInformation(
                "[AutoTranscription] Requeued pending file import: {0} ({1})", title, sessionDir);
        }
    }

    private bool IsAlreadyQueued(string sessionDir)
    {
        var fullDir = Path.GetFullPath(sessionDir);
        foreach (var item in _queueService.Items)
        {
            if (item.Stage is QueueItemStage.Completed or QueueItemStage.Failed)
                continue;

            var itemDir = item.SessionDir ??
                (item.Session is not null
                    ? Path.GetDirectoryName(item.Session.MicWavFilePath)
                    : null);
            if (itemDir is not null &&
                string.Equals(Path.GetFullPath(itemDir), fullDir, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Reads the user-edited title and speaker names from the session's
    /// <c>transcript_name.json</c> (written by the Transcripts page's pending
    /// drawer). Best-effort: returns defaults when absent or unreadable.
    /// </summary>
    private static (string? Name, List<string> Speakers) LoadPendingMetadata(string sessionDir)
    {
        var nameFile = Path.Combine(sessionDir, "transcript_name.json");
        if (!File.Exists(nameFile))
            return (null, new List<string>());

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(nameFile));
            string? name = null;
            if (doc.RootElement.TryGetProperty("name", out var nameElement))
                name = nameElement.GetString();

            var speakers = new List<string>();
            if (doc.RootElement.TryGetProperty("speakers", out var speakersElement))
            {
                foreach (var s in speakersElement.EnumerateArray())
                {
                    var val = s.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                        speakers.Add(val);
                }
            }

            return (name, speakers);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[AutoTranscription] Failed to read pending metadata from {0}: {1}",
                nameFile, ex.Message);
            return (null, new List<string>());
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _recordingService.RecordingStopped -= OnRecordingStopped;
    }
}
