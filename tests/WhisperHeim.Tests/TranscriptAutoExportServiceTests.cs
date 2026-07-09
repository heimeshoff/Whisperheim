using System.IO;
using WhisperHeim.Models;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Export;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Tests;

/// <summary>
/// Covers <see cref="TranscriptAutoExportService"/> (task main-m6x4v, ADR-0008):
/// per-kind folder configuration, the Recording-session-directory overwrite
/// identity, sibling-title disambiguation, and the silent-skip failure modes
/// that must never retroactively affect an already-Completed queue item.
///
/// Uses a real <see cref="TranscriptStorageService"/> pointed at an isolated
/// temp data path (no real %APPDATA% touched — <see cref="DataPathService.Load"/>
/// / <see cref="DataPathService.Save"/> are never called, so nothing hits disk
/// outside <see cref="_testRoot"/>).
/// </summary>
public class TranscriptAutoExportServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly DataPathService _dataPathService;
    private readonly TranscriptStorageService _storage;
    private readonly TranscriptAutoExportService _sut;

    public TranscriptAutoExportServiceTests()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(), "WhisperHeimTests", "autoexport_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        _dataPathService = new DataPathService();
        _dataPathService.Bootstrap.DataPath = Path.Combine(_testRoot, "data");
        _dataPathService.Bootstrap.MachineId = "testmachine";

        _storage = new TranscriptStorageService(_dataPathService);
        _sut = new TranscriptAutoExportService(_storage, _dataPathService);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    // ── helpers ──────────────────────────────────────────────────────

    private string NewExportFolder(string name)
    {
        var dir = Path.Combine(_testRoot, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private async Task<(string SessionDir, CallTranscript Transcript)> CreateSessionAsync(
        string name, string sessionDirName, string segmentText = "Hello world")
    {
        var sessionDir = Path.Combine(_dataPathService.RecordingsPath, sessionDirName);
        Directory.CreateDirectory(sessionDir);

        var transcript = new CallTranscript
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            RecordingStartedUtc = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero),
            RecordingEndedUtc = new DateTimeOffset(2026, 7, 9, 12, 1, 0, TimeSpan.Zero),
            Segments = new List<TranscriptSegment>
            {
                new()
                {
                    Speaker = "You", StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(1),
                    Text = segmentText, IsLocalSpeaker = true,
                },
            },
        };

        await _storage.SaveAsync(transcript, sessionDir);
        return (sessionDir, transcript);
    }

    private static TranscriptionQueueItem RecordingItem(string title, string sessionDir) =>
        new(title, new CallRecordingSession(
            Path.Combine(sessionDir, "mic.wav"),
            Path.Combine(sessionDir, "system.wav"),
            new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero)));

    private static TranscriptionQueueItem FileImportItem(string title, string sessionDir) =>
        new(title, Path.Combine(sessionDir, "voice.mp3"), sessionDir);

    /// <summary>
    /// Simulates <c>ReTranscribe_Click</c> deleting the old transcript.json
    /// before the pipeline rebuilds a brand-new <see cref="CallTranscript"/>
    /// (fresh Id, ExportedMarkdownPath reset) into the same session directory.
    /// </summary>
    private async Task<CallTranscript> ReTranscribeAsync(string sessionDir, string name, string newSegmentText)
    {
        var existingJson = Path.Combine(sessionDir, "transcript.json");
        if (File.Exists(existingJson))
            File.Delete(existingJson);

        var rebuilt = new CallTranscript
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            RecordingStartedUtc = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero),
            RecordingEndedUtc = new DateTimeOffset(2026, 7, 9, 12, 1, 0, TimeSpan.Zero),
            Segments = new List<TranscriptSegment>
            {
                new()
                {
                    Speaker = "You", StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(1),
                    Text = newSegmentText, IsLocalSpeaker = true,
                },
            },
        };

        await _storage.SaveAsync(rebuilt, sessionDir);
        return rebuilt;
    }

    // ── AC: completed recording writes <name>.md ────────────────────

    [Fact]
    public async Task ExportIfConfiguredAsync_WritesMarkdown_ForCompletedRecording()
    {
        var exportFolder = NewExportFolder("recordings-export");
        _dataPathService.Bootstrap.RecordingsExportFolder = exportFolder;

        var (sessionDir, _) = await CreateSessionAsync("Test Recording", "20260709_120000_testmachine");
        var item = RecordingItem("Test Recording", sessionDir);

        await _sut.ExportIfConfiguredAsync(item);

        var expectedPath = Path.Combine(exportFolder, "Test Recording.md");
        Assert.True(File.Exists(expectedPath));
        Assert.Contains("Hello world", await File.ReadAllTextAsync(expectedPath));

        var reloaded = await _storage.LoadAsync(Path.Combine(sessionDir, "transcript.json"));
        Assert.Equal(expectedPath, reloaded!.ExportedMarkdownPath);
    }

    // ── AC: completed file import writes <name>.md ──────────────────

    [Fact]
    public async Task ExportIfConfiguredAsync_WritesMarkdown_ForCompletedFileImport()
    {
        var exportFolder = NewExportFolder("imports-export");
        _dataPathService.Bootstrap.ImportsExportFolder = exportFolder;

        var (sessionDir, _) = await CreateSessionAsync("Voice Memo", "20260709_130000_testmachine");
        var item = FileImportItem("Voice Memo", sessionDir);

        await _sut.ExportIfConfiguredAsync(item);

        var expectedPath = Path.Combine(exportFolder, "Voice Memo.md");
        Assert.True(File.Exists(expectedPath));
    }

    // ── AC: unset folder -> no export for that kind; other kind unaffected ──

    [Fact]
    public async Task ExportIfConfiguredAsync_SkipsRecording_WhenRecordingsFolderNotConfigured()
    {
        // Imports folder configured, recordings folder left null.
        _dataPathService.Bootstrap.ImportsExportFolder = NewExportFolder("imports-export-2");

        var (sessionDir, _) = await CreateSessionAsync("Unconfigured Kind", "20260709_140000_testmachine");
        var item = RecordingItem("Unconfigured Kind", sessionDir);

        await _sut.ExportIfConfiguredAsync(item);

        Assert.Empty(Directory.GetFiles(_dataPathService.Bootstrap.ImportsExportFolder!, "*.md"));
    }

    [Fact]
    public async Task ExportIfConfiguredAsync_ExportsImport_WhileRecordingsFolderIsUnset_ProvingKindsAreIndependent()
    {
        var importsFolder = NewExportFolder("imports-export-3");
        _dataPathService.Bootstrap.ImportsExportFolder = importsFolder;
        _dataPathService.Bootstrap.RecordingsExportFolder = null;

        var (sessionDir, _) = await CreateSessionAsync("Independent Import", "20260709_150000_testmachine");
        var item = FileImportItem("Independent Import", sessionDir);

        await _sut.ExportIfConfiguredAsync(item);

        Assert.True(File.Exists(Path.Combine(importsFolder, "Independent Import.md")));
    }

    // ── AC: missing/unwritable folder -> silent skip, no exception ──

    [Fact]
    public async Task ExportIfConfiguredAsync_SkipsSilently_WhenExportFolderMissing()
    {
        var missingFolder = Path.Combine(_testRoot, "does-not-exist");
        _dataPathService.Bootstrap.RecordingsExportFolder = missingFolder;

        var (sessionDir, _) = await CreateSessionAsync("Orphan Folder", "20260709_160000_testmachine");
        var item = RecordingItem("Orphan Folder", sessionDir);

        // Must not throw.
        await _sut.ExportIfConfiguredAsync(item);

        Assert.False(Directory.Exists(missingFolder));

        var reloaded = await _storage.LoadAsync(Path.Combine(sessionDir, "transcript.json"));
        Assert.Null(reloaded!.ExportedMarkdownPath);
    }

    [Fact]
    public async Task ExportIfConfiguredAsync_SkipsSilently_WhenTranscriptJsonMissing()
    {
        // SessionDir with no transcript.json at all (e.g. a race, or a
        // corrupt/never-finished write) — must not throw.
        var exportFolder = NewExportFolder("recordings-export-4");
        _dataPathService.Bootstrap.RecordingsExportFolder = exportFolder;

        var sessionDir = Path.Combine(_dataPathService.RecordingsPath, "20260709_170000_testmachine");
        Directory.CreateDirectory(sessionDir);
        var item = RecordingItem("No Transcript Yet", sessionDir);

        await _sut.ExportIfConfiguredAsync(item);

        Assert.Empty(Directory.GetFiles(exportFolder, "*.md"));
    }

    // ── AC: completions with no SessionDir produce no export ────────

    [Fact]
    public async Task ExportIfConfiguredAsync_SkipsSilently_WhenItemHasNoSessionDir()
    {
        // Mirrors STT API / EnqueueFile: a File item with no SessionDir.
        var recordingsFolder = NewExportFolder("recordings-export-5");
        var importsFolder = NewExportFolder("imports-export-5");
        _dataPathService.Bootstrap.RecordingsExportFolder = recordingsFolder;
        _dataPathService.Bootstrap.ImportsExportFolder = importsFolder;

        var item = new TranscriptionQueueItem("Ephemeral", Path.Combine(_testRoot, "somefile.wav"));

        await _sut.ExportIfConfiguredAsync(item);

        Assert.Empty(Directory.GetFiles(recordingsFolder, "*.md"));
        Assert.Empty(Directory.GetFiles(importsFolder, "*.md"));
    }

    // ── AC: re-transcribing the same recording overwrites the same .md ──

    [Fact]
    public async Task ExportIfConfiguredAsync_OverwritesInPlace_OnReTranscriptionOfSameSession()
    {
        var exportFolder = NewExportFolder("recordings-export-6");
        _dataPathService.Bootstrap.RecordingsExportFolder = exportFolder;

        var (sessionDir, _) = await CreateSessionAsync("Repeat Call", "20260709_180000_testmachine", "first pass");
        var item = RecordingItem("Repeat Call", sessionDir);
        await _sut.ExportIfConfiguredAsync(item);

        var expectedPath = Path.Combine(exportFolder, "Repeat Call.md");
        Assert.Contains("first pass", await File.ReadAllTextAsync(expectedPath));

        // Re-transcribe: transcript.json is deleted and rebuilt fresh (same
        // session dir, same title, ExportedMarkdownPath reset to null) —
        // exactly what ReTranscribe_Click does.
        await ReTranscribeAsync(sessionDir, "Repeat Call", "second pass");
        await _sut.ExportIfConfiguredAsync(item);

        Assert.True(File.Exists(expectedPath));
        Assert.Contains("second pass", await File.ReadAllTextAsync(expectedPath));
        Assert.DoesNotContain("first pass", await File.ReadAllTextAsync(expectedPath));

        // Only the one file — no "Repeat Call (2).md" was created.
        Assert.Single(Directory.GetFiles(exportFolder, "*.md"));
    }

    // ── AC: same-titled sibling sessions disambiguate; never cross-clobber ──

    [Fact]
    public async Task ExportIfConfiguredAsync_DisambiguatesSameTitledSiblingSessions()
    {
        var exportFolder = NewExportFolder("recordings-export-7");
        _dataPathService.Bootstrap.RecordingsExportFolder = exportFolder;

        var (sessionDirA, _) = await CreateSessionAsync("Shared Title", "20260709_190000_testmachine", "session A content");
        var itemA = RecordingItem("Shared Title", sessionDirA);
        await _sut.ExportIfConfiguredAsync(itemA);

        var (sessionDirB, _) = await CreateSessionAsync("Shared Title", "20260709_191000_testmachine", "session B content");
        var itemB = RecordingItem("Shared Title", sessionDirB);
        await _sut.ExportIfConfiguredAsync(itemB);

        var pathA = Path.Combine(exportFolder, "Shared Title.md");
        var pathB = Path.Combine(exportFolder, "Shared Title (2).md");

        Assert.True(File.Exists(pathA));
        Assert.True(File.Exists(pathB));
        Assert.Contains("session A content", await File.ReadAllTextAsync(pathA));
        Assert.Contains("session B content", await File.ReadAllTextAsync(pathB));

        // Re-transcribing session A must never touch session B's file, and
        // must resolve back to its own "Shared Title.md" (not a new suffix),
        // even though its own ExportedMarkdownPath was reset by the rebuild.
        await ReTranscribeAsync(sessionDirA, "Shared Title", "session A content v2");
        await _sut.ExportIfConfiguredAsync(itemA);

        Assert.Contains("session A content v2", await File.ReadAllTextAsync(pathA));
        Assert.Contains("session B content", await File.ReadAllTextAsync(pathB));
        Assert.Equal(2, Directory.GetFiles(exportFolder, "*.md").Length);
    }

    // ── Robustness: a corrupt transcript.json must not throw ────────

    [Fact]
    public async Task ExportIfConfiguredAsync_SkipsSilently_WhenTranscriptJsonIsCorrupt()
    {
        var exportFolder = NewExportFolder("recordings-export-8");
        _dataPathService.Bootstrap.RecordingsExportFolder = exportFolder;

        var sessionDir = Path.Combine(_dataPathService.RecordingsPath, "20260709_200000_testmachine");
        Directory.CreateDirectory(sessionDir);
        await File.WriteAllTextAsync(Path.Combine(sessionDir, "transcript.json"), "{ not valid json");

        var item = RecordingItem("Corrupt", sessionDir);

        // Must not throw — a transcription that already succeeded must never
        // be retroactively affected by an export-time problem.
        await _sut.ExportIfConfiguredAsync(item);

        Assert.Empty(Directory.GetFiles(exportFolder, "*.md"));
    }
}
