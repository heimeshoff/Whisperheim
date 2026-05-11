using System.IO;
using WhisperHeim.Services.Recording;

namespace WhisperHeim.Tests;

/// <summary>
/// Verifies the atomic-write semantics of <see cref="RecordingFileStager"/>:
/// staged sessions move into the final directory, failures leave the files
/// in staging for recovery, and the startup sweep correctly handles orphans.
///
/// These tests operate against real temp directories on disk because the
/// helper's whole purpose is to interact with the filesystem.
/// </summary>
public class RecordingFileStagerTests : IDisposable
{
    private readonly string _testRoot;

    public RecordingFileStagerTests()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "WhisperHeimTests",
            "stager_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
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
            // best-effort cleanup
        }
    }

    private string StagingRoot => Path.Combine(_testRoot, "staging");
    private string FinalRoot => Path.Combine(_testRoot, "final");

    [Fact]
    public void MoveStagedSession_HappyPath_MovesFilesIntoFinalDir()
    {
        // Arrange: a staged session with two non-zero WAV files.
        var sessionId = "20260511_120000_abcdef";
        var stagingDir = Path.Combine(StagingRoot, sessionId);
        Directory.CreateDirectory(stagingDir);
        File.WriteAllBytes(Path.Combine(stagingDir, "mic.wav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(stagingDir, "system.wav"), new byte[] { 4, 5, 6 });

        var finalDir = Path.Combine(FinalRoot, "20260511_120000");

        // Act
        var result = RecordingFileStager.MoveStagedSession(stagingDir, finalDir);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(finalDir, result.ResultingDirectory);
        Assert.False(Directory.Exists(stagingDir), "Staging dir should be gone after move.");
        Assert.True(File.Exists(Path.Combine(finalDir, "mic.wav")));
        Assert.True(File.Exists(Path.Combine(finalDir, "system.wav")));
    }

    [Fact]
    public void MoveStagedSession_StagingMissing_ReturnsSuccessWithFinalDir()
    {
        // A staging dir that doesn't exist is a no-op (e.g. both streams failed
        // to start). Callers still expect a usable ResultingDirectory.
        var stagingDir = Path.Combine(StagingRoot, "ghost");
        var finalDir = Path.Combine(FinalRoot, "ghost-final");

        var result = RecordingFileStager.MoveStagedSession(stagingDir, finalDir);

        Assert.True(result.Success);
        Assert.Equal(finalDir, result.ResultingDirectory);
    }

    [Fact]
    public void MoveStagedSession_FinalAlreadyExists_AppendsCollisionSuffix()
    {
        // Pre-create the final dir so the move would otherwise collide.
        var sessionId = "20260511_130000_xyz";
        var stagingDir = Path.Combine(StagingRoot, sessionId);
        Directory.CreateDirectory(stagingDir);
        File.WriteAllBytes(Path.Combine(stagingDir, "mic.wav"), new byte[] { 1 });

        var finalDir = Path.Combine(FinalRoot, "20260511_130000");
        Directory.CreateDirectory(finalDir);
        File.WriteAllBytes(Path.Combine(finalDir, "existing.wav"), new byte[] { 99 });

        var result = RecordingFileStager.MoveStagedSession(stagingDir, finalDir);

        Assert.True(result.Success);
        Assert.NotEqual(finalDir, result.ResultingDirectory);
        Assert.StartsWith(finalDir + "_", result.ResultingDirectory);
        Assert.True(File.Exists(Path.Combine(result.ResultingDirectory, "mic.wav")));
        // Pre-existing dir should be untouched.
        Assert.True(File.Exists(Path.Combine(finalDir, "existing.wav")));
    }

    [Fact]
    public void SweepOrphans_NonEmptyOrphan_RecoveredWithPrefix()
    {
        var orphanName = "20260510_110000_oldsession";
        var orphan = Path.Combine(StagingRoot, orphanName);
        Directory.CreateDirectory(orphan);
        File.WriteAllBytes(Path.Combine(orphan, "mic.wav"), new byte[] { 7, 7, 7 });

        Directory.CreateDirectory(FinalRoot);

        var recovered = RecordingFileStager.SweepOrphans(StagingRoot, FinalRoot);

        Assert.Equal(1, recovered);
        var recoveredDir = Path.Combine(FinalRoot, RecordingFileStager.RecoveredPrefix + orphanName);
        Assert.True(Directory.Exists(recoveredDir));
        Assert.True(File.Exists(Path.Combine(recoveredDir, "mic.wav")));
        Assert.False(Directory.Exists(orphan));
    }

    [Fact]
    public void SweepOrphans_EmptyOrphanDir_Deleted()
    {
        var orphan = Path.Combine(StagingRoot, "empty_session");
        Directory.CreateDirectory(orphan);
        // No files at all → empty.

        Directory.CreateDirectory(FinalRoot);

        var recovered = RecordingFileStager.SweepOrphans(StagingRoot, FinalRoot);

        Assert.Equal(0, recovered);
        Assert.False(Directory.Exists(orphan), "Empty orphan dir should be deleted.");
    }

    [Fact]
    public void SweepOrphans_ZeroByteFilesOnly_TreatedAsEmpty()
    {
        // A session that never wrote any samples leaves zero-byte placeholder
        // WAV headers around. Treat as empty and clean up.
        var orphan = Path.Combine(StagingRoot, "zero_session");
        Directory.CreateDirectory(orphan);
        File.WriteAllBytes(Path.Combine(orphan, "mic.wav"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(orphan, "system.wav"), Array.Empty<byte>());

        Directory.CreateDirectory(FinalRoot);

        var recovered = RecordingFileStager.SweepOrphans(StagingRoot, FinalRoot);

        Assert.Equal(0, recovered);
        Assert.False(Directory.Exists(orphan));
    }

    [Fact]
    public void SweepOrphans_AlreadyRecoveredPrefix_DoesNotDoublePrefix()
    {
        // A previous startup sweep moved an orphan but then we crashed before
        // it was finalized, leaving "recovered_xyz" back in staging. Don't
        // turn it into "recovered_recovered_xyz".
        var orphanName = RecordingFileStager.RecoveredPrefix + "20260510_110000";
        var orphan = Path.Combine(StagingRoot, orphanName);
        Directory.CreateDirectory(orphan);
        File.WriteAllBytes(Path.Combine(orphan, "mic.wav"), new byte[] { 1 });

        Directory.CreateDirectory(FinalRoot);

        RecordingFileStager.SweepOrphans(StagingRoot, FinalRoot);

        var recoveredDir = Path.Combine(FinalRoot, orphanName);
        Assert.True(Directory.Exists(recoveredDir));
        Assert.False(Directory.Exists(
            Path.Combine(FinalRoot, RecordingFileStager.RecoveredPrefix + orphanName)));
    }

    [Fact]
    public void SweepOrphans_StagingMissing_ReturnsZero()
    {
        // Fresh install: staging root doesn't exist yet.
        var recovered = RecordingFileStager.SweepOrphans(
            Path.Combine(_testRoot, "does-not-exist"),
            FinalRoot);

        Assert.Equal(0, recovered);
    }
}
