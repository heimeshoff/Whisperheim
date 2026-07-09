using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Persists call transcripts as JSON files in per-session folders under recordings/.
/// Each session gets its own folder: recordings/YYYYMMDD_HHmmss/ containing
/// transcript.json and any associated WAV files.
/// </summary>
public sealed class TranscriptStorageService : ITranscriptStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly DataPathService _dataPathService;

    public TranscriptStorageService(DataPathService dataPathService)
    {
        _dataPathService = dataPathService;
    }

    /// <summary>
    /// Exposes the underlying <see cref="DataPathService"/> so UI callers can
    /// read the machine id and other path-derived values without having to
    /// inject the service into every view.
    /// </summary>
    public DataPathService DataPathService => _dataPathService;

    /// <inheritdoc />
    public string TranscriptsDirectory => _dataPathService.RecordingsPath;

    /// <summary>
    /// Creates a new session directory for recording and returns its path.
    /// Format: recordings/YYYYMMDD_HHmmss/
    /// </summary>
    public string CreateSessionDirectory(DateTimeOffset startTimestamp)
    {
        var sessionName = startTimestamp.LocalDateTime.ToString("yyyyMMdd_HHmmss");
        var sessionDir = Path.Combine(_dataPathService.RecordingsPath, sessionName);

        // Avoid collision by appending a suffix
        if (Directory.Exists(sessionDir))
        {
            var suffix = 1;
            string candidate;
            do
            {
                candidate = $"{sessionDir}_{suffix}";
                suffix++;
            } while (Directory.Exists(candidate));
            sessionDir = candidate;
        }

        Directory.CreateDirectory(sessionDir);
        return sessionDir;
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        CallTranscript transcript,
        string? sessionDir = null,
        CancellationToken cancellationToken = default)
    {
        // Determine the session directory. Since task 105, session directories
        // include a `_{machineId}` suffix (and optionally a `_{n}` collision
        // counter), so we can't reconstruct the exact name purely from the
        // start timestamp. Prefer:
        //   0. The explicit sessionDir the caller passed (the pipeline knows the
        //      exact directory it read the WAVs from — name-based inference fails
        //      for dirs like `recovered_*` whose names don't start with the
        //      timestamp, misrouting the transcript into a brand-new folder).
        //   1. An existing directory whose WAVs the in-memory transcript was
        //      built from (look up via `AudioFilePath` if it was set absolutely).
        //   2. Otherwise, the first existing directory whose name starts with
        //      the timestamp (handles both `_{machineId}` and legacy un-suffixed
        //      dirs).
        //   3. Otherwise, create a new `{timestamp}_{machineId}` dir for this machine.
        var sessionName = transcript.RecordingStartedUtc.LocalDateTime.ToString("yyyyMMdd_HHmmss");
        var recordingsRoot = _dataPathService.RecordingsPath;
        Directory.CreateDirectory(recordingsRoot);

        // (0) Explicit session dir from the caller wins.
        if (sessionDir is not null && !Directory.Exists(sessionDir))
            sessionDir = null;

        // (1) AudioFilePath might be absolute and point inside the real session dir.
        if (sessionDir is null &&
            !string.IsNullOrEmpty(transcript.AudioFilePath) && Path.IsPathRooted(transcript.AudioFilePath))
        {
            var parent = Path.GetDirectoryName(transcript.AudioFilePath);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                sessionDir = parent;
        }

        // (2) Scan for an existing dir whose name starts with the timestamp prefix.
        if (sessionDir is null)
        {
            foreach (var dir in Directory.GetDirectories(recordingsRoot, $"{sessionName}*"))
            {
                sessionDir = dir;
                break;
            }
        }

        // (3) Brand-new directory — happen for re-transcribed file imports.
        if (sessionDir is null)
        {
            sessionDir = Path.Combine(
                recordingsRoot, $"{sessionName}_{_dataPathService.MachineId}");
            Directory.CreateDirectory(sessionDir);
        }

        var filePath = Path.Combine(sessionDir, "transcript.json");

        // Avoid overwriting: append a suffix if the file already exists in a different session
        if (File.Exists(filePath))
        {
            var baseDirName = Path.GetFileName(sessionDir);
            var suffix = 1;
            string candidateDir;
            do
            {
                candidateDir = Path.Combine(
                    recordingsRoot, $"{baseDirName}_{suffix}");
                suffix++;
            } while (Directory.Exists(candidateDir));

            sessionDir = candidateDir;
            Directory.CreateDirectory(sessionDir);
            filePath = Path.Combine(sessionDir, "transcript.json");
        }

        var json = JsonSerializer.Serialize(transcript, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        transcript.FilePath = filePath;

        Trace.TraceInformation(
            "[TranscriptStorageService] Saved transcript to {0} ({1} segments)",
            filePath, transcript.Segments.Count);

        return filePath;
    }

    /// <inheritdoc />
    public async Task<CallTranscript?> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var transcript = JsonSerializer.Deserialize<CallTranscript>(json, JsonOptions);

        if (transcript is not null)
        {
            transcript.FilePath = filePath;

            // Backward compatibility: generate a default name for old transcripts without one
            if (string.IsNullOrEmpty(transcript.Name))
            {
                transcript.Name = $"Call {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm}";
            }
        }

        return transcript;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        CallTranscript transcript,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(transcript.FilePath))
            throw new InvalidOperationException("Cannot update a transcript without a file path.");

        var json = JsonSerializer.Serialize(transcript, JsonOptions);
        await File.WriteAllTextAsync(transcript.FilePath, json, cancellationToken);

        Trace.TraceInformation(
            "[TranscriptStorageService] Updated transcript at {0}", transcript.FilePath);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListTranscriptFiles()
    {
        var recordingsDir = _dataPathService.RecordingsPath;
        if (!Directory.Exists(recordingsDir))
            return Array.Empty<string>();

        // Look for transcript.json inside each session subdirectory
        var files = new List<string>();
        foreach (var sessionDir in Directory.GetDirectories(recordingsDir))
        {
            var transcriptFile = Path.Combine(sessionDir, "transcript.json");
            if (File.Exists(transcriptFile))
            {
                files.Add(transcriptFile);
            }
            else if (!Directory.EnumerateFileSystemEntries(sessionDir).Any())
            {
                // Clean up empty session folders (e.g. left behind after deletion on synced drives)
                try
                {
                    Directory.Delete(sessionDir);
                    Trace.TraceInformation(
                        "[TranscriptStorageService] Cleaned up empty session directory: {0}", sessionDir);
                }
                catch (IOException) { }
            }
        }

        // Also support old-style flat transcript files during migration
        files.AddRange(Directory.GetFiles(recordingsDir, "transcript_*.json"));

        return files.OrderByDescending(f => f).ToArray();
    }

    /// <summary>
    /// Audio file extensions recognized as valid session content (WAV + import formats).
    /// </summary>
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".ogg", ".mp3", ".m4a"
    };

    /// <summary>
    /// Returns session directories owned by this machine that contain audio files
    /// but no <c>transcript.json</c>. Ownership is determined by the origin
    /// machineId (see <see cref="Recording.SessionMetadataStore.ResolveOriginMachineId"/>).
    /// Un-stamped legacy sessions are treated as owned-by-this-machine for
    /// backward compatibility.
    /// </summary>
    public IReadOnlyList<string> ListPendingSessions()
    {
        return EnumeratePendingSessions(ownerFilter: SessionOwnership.MineOrLegacy)
            .OrderByDescending(d => d)
            .ToArray();
    }

    /// <summary>
    /// Returns pending session directories whose origin machineId is a different
    /// machine than this one (i.e. recorded elsewhere, synced in via the shared
    /// data folder). Drives the "Other machines" section of the Transcripts page
    /// pending UI, and powers the manual "Transcribe here" takeover action.
    /// </summary>
    public IReadOnlyList<string> ListPendingSessionsFromOtherMachines()
    {
        return EnumeratePendingSessions(ownerFilter: SessionOwnership.OtherMachine)
            .OrderByDescending(d => d)
            .ToArray();
    }

    private enum SessionOwnership
    {
        MineOrLegacy,
        OtherMachine,
    }

    private IEnumerable<string> EnumeratePendingSessions(SessionOwnership ownerFilter)
    {
        var recordingsDir = _dataPathService.RecordingsPath;
        if (!Directory.Exists(recordingsDir))
            yield break;

        var myMachineId = _dataPathService.MachineId;

        foreach (var sessionDir in Directory.GetDirectories(recordingsDir))
        {
            var hasTranscript = File.Exists(Path.Combine(sessionDir, "transcript.json"));
            if (hasTranscript)
                continue;

            var hasAudio = Directory.GetFiles(sessionDir)
                .Any(f => AudioExtensions.Contains(Path.GetExtension(f)));
            if (!hasAudio)
                continue;

            // Skip sessions that have exhausted all retry attempts (only applies
            // to this machine's queue; other-machine listings show every pending
            // candidate so the user can still take over after a failure).
            if (ownerFilter == SessionOwnership.MineOrLegacy &&
                Transcription.TranscriptionQueueService.HasExceededRetryLimit(sessionDir))
            {
                continue;
            }

            var origin = Recording.SessionMetadataStore.ResolveOriginMachineId(sessionDir);
            bool isMineOrLegacy =
                origin is null ||
                string.Equals(origin, myMachineId, StringComparison.OrdinalIgnoreCase);

            switch (ownerFilter)
            {
                case SessionOwnership.MineOrLegacy when isMineOrLegacy:
                    yield return sessionDir;
                    break;
                case SessionOwnership.OtherMachine when !isMineOrLegacy:
                    yield return sessionDir;
                    break;
            }
        }
    }

    /// <summary>
    /// Deletes an entire recording session directory (transcript + WAV files).
    /// </summary>
    public void DeleteSession(string transcriptFilePath)
    {
        var sessionDir = Path.GetDirectoryName(transcriptFilePath);
        if (sessionDir is null)
            return;

        // Only delete the entire directory if it's a per-session folder
        // (i.e., it's a direct child of the recordings directory)
        var parentDir = Path.GetDirectoryName(sessionDir);
        if (parentDir is not null &&
            string.Equals(Path.GetFullPath(parentDir),
                Path.GetFullPath(_dataPathService.RecordingsPath),
                StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(sessionDir, recursive: true);
            // On synced drives the folder may linger; remove the now-empty shell
            if (Directory.Exists(sessionDir) &&
                !Directory.EnumerateFileSystemEntries(sessionDir).Any())
            {
                Directory.Delete(sessionDir);
            }
            Trace.TraceInformation(
                "[TranscriptStorageService] Deleted session directory: {0}", sessionDir);
        }
        else
        {
            // Fallback: just delete the transcript file
            if (File.Exists(transcriptFilePath))
                File.Delete(transcriptFilePath);
        }
    }
}
