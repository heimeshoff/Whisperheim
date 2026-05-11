using System.Diagnostics;
using System.IO;

namespace WhisperHeim.Services.Recording;

/// <summary>
/// Atomic-write helper for WAV recordings. Active capture writes go to a
/// machine-local staging directory; once the file handles close, the entire
/// session directory is moved into the final (potentially cloud-synced)
/// destination as a single atomic operation. This prevents cloud sync clients
/// (Google Drive, OneDrive, Dropbox) from trying to upload a file that NAudio
/// still holds an exclusive write handle on.
/// </summary>
/// <remarks>
/// Same-volume case uses <see cref="Directory.Move"/> (atomic). Cross-volume
/// case falls back to per-file <see cref="File.Move"/> + cleanup. Either way,
/// the move only happens once, after the writer has closed, with a complete
/// file — so sync clients see a single completed file appear rather than a
/// live-changing file.
/// </remarks>
public static class RecordingFileStager
{
    /// <summary>
    /// Prefix used for session directories recovered from staging after a
    /// crash or hard-kill mid-recording.
    /// </summary>
    public const string RecoveredPrefix = "recovered_";

    /// <summary>
    /// Result of an atomic-move attempt.
    /// </summary>
    public sealed record MoveResult(bool Success, string ResultingDirectory, Exception? Error)
    {
        /// <summary>
        /// True if the staged session ended up at <see cref="ResultingDirectory"/>
        /// inside the final (cloud-synced) location.
        /// </summary>
        public bool MovedToFinal => Success;
    }

    /// <summary>
    /// Atomically moves a staged session directory into <paramref name="finalDir"/>.
    /// On failure (Drive unreachable, permissions, etc.) the files remain in
    /// <paramref name="stagingDir"/> so the next crash-recovery sweep can retry.
    /// </summary>
    /// <param name="stagingDir">Source — the machine-local staging directory.</param>
    /// <param name="finalDir">Destination — typically inside the synced data folder.</param>
    /// <returns>
    /// A <see cref="MoveResult"/> whose <see cref="MoveResult.ResultingDirectory"/>
    /// is <paramref name="finalDir"/> on success, or <paramref name="stagingDir"/>
    /// if the move failed and the recording is left in place for recovery.
    /// </returns>
    public static MoveResult MoveStagedSession(string stagingDir, string finalDir)
    {
        if (!Directory.Exists(stagingDir))
        {
            // Nothing to move — treat as success against the final dir.
            return new MoveResult(true, finalDir, null);
        }

        try
        {
            var finalParent = Path.GetDirectoryName(finalDir);
            if (!string.IsNullOrEmpty(finalParent))
                Directory.CreateDirectory(finalParent);

            if (Directory.Exists(finalDir))
            {
                // Pick a collision-free name. This can happen if a previous run
                // already created the final dir (e.g. recovery after partial move).
                finalDir = AppendCollisionSuffix(finalDir);
            }

            if (IsSameVolume(stagingDir, finalDir))
            {
                Directory.Move(stagingDir, finalDir);
            }
            else
            {
                MoveAcrossVolumes(stagingDir, finalDir);
            }

            Trace.TraceInformation(
                "[RecordingFileStager] Moved staged session: {0} -> {1}",
                stagingDir, finalDir);

            return new MoveResult(true, finalDir, null);
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[RecordingFileStager] Failed to move staged session {0} -> {1}: {2}. " +
                "Files remain in staging and will be retried on next startup.",
                stagingDir, finalDir, ex.Message);

            return new MoveResult(false, stagingDir, ex);
        }
    }

    /// <summary>
    /// On app start, scans <paramref name="stagingRoot"/> for orphaned session
    /// directories left behind by a crash or hard kill mid-recording. Each
    /// orphan that contains non-zero audio files is moved into
    /// <paramref name="finalRoot"/> with a <c>recovered_</c> prefix; empty
    /// dirs are deleted.
    /// </summary>
    /// <returns>The number of orphan sessions that were recovered.</returns>
    public static int SweepOrphans(string stagingRoot, string finalRoot)
    {
        if (!Directory.Exists(stagingRoot))
            return 0;

        int recovered = 0;
        int deleted = 0;

        string[] orphans;
        try
        {
            orphans = Directory.GetDirectories(stagingRoot);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[RecordingFileStager] Could not enumerate staging root {0}: {1}",
                stagingRoot, ex.Message);
            return 0;
        }

        foreach (var orphan in orphans)
        {
            try
            {
                if (!HasNonEmptyAudio(orphan))
                {
                    // Empty / zero-byte — clean up.
                    Directory.Delete(orphan, recursive: true);
                    deleted++;
                    continue;
                }

                var orphanName = Path.GetFileName(orphan);
                // If the orphan name already starts with the recovered prefix
                // (e.g. a previous sweep tried and failed to move), don't
                // double-prefix.
                var recoveredName = orphanName.StartsWith(RecoveredPrefix, StringComparison.Ordinal)
                    ? orphanName
                    : RecoveredPrefix + orphanName;

                var finalDir = Path.Combine(finalRoot, recoveredName);
                var result = MoveStagedSession(orphan, finalDir);
                if (result.Success)
                    recovered++;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "[RecordingFileStager] Failed to process orphan {0}: {1}",
                    orphan, ex.Message);
            }
        }

        if (recovered > 0 || deleted > 0)
        {
            Trace.TraceInformation(
                "[RecordingFileStager] Staging sweep complete. Recovered: {0}, deleted (empty): {1}.",
                recovered, deleted);
        }

        return recovered;
    }

    /// <summary>
    /// True if <paramref name="dir"/> contains at least one non-zero-byte file
    /// (recursively).
    /// </summary>
    private static bool HasNonEmptyAudio(string dir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                if (info.Length > 0)
                    return true;
            }
        }
        catch
        {
            // If we can't enumerate, assume there's data we shouldn't lose.
            return true;
        }

        return false;
    }

    /// <summary>
    /// True if two paths sit on the same volume / drive root. <see cref="Directory.Move"/>
    /// is only guaranteed atomic when this is true.
    /// </summary>
    private static bool IsSameVolume(string pathA, string pathB)
    {
        try
        {
            var rootA = Path.GetPathRoot(Path.GetFullPath(pathA));
            var rootB = Path.GetPathRoot(Path.GetFullPath(pathB));
            return string.Equals(rootA, rootB, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cross-volume fallback: create the destination dir, copy/move each file
    /// individually, then remove the now-empty staging dir.
    /// </summary>
    private static void MoveAcrossVolumes(string stagingDir, string finalDir)
    {
        Directory.CreateDirectory(finalDir);

        foreach (var file in Directory.EnumerateFiles(stagingDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(stagingDir, file);
            var destFile = Path.Combine(finalDir, relative);
            var destDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            // File.Move with overwrite handles the cross-volume copy+delete internally.
            File.Move(file, destFile, overwrite: false);
        }

        // After all files have moved, remove the staging dir (best-effort).
        try
        {
            Directory.Delete(stagingDir, recursive: true);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[RecordingFileStager] Cross-volume move succeeded but staging cleanup failed for {0}: {1}",
                stagingDir, ex.Message);
        }
    }

    /// <summary>
    /// Returns <paramref name="basePath"/> with a numeric suffix that doesn't
    /// exist on disk (e.g. <c>foo_1</c>, <c>foo_2</c>).
    /// </summary>
    private static string AppendCollisionSuffix(string basePath)
    {
        var suffix = 1;
        string candidate;
        do
        {
            candidate = $"{basePath}_{suffix}";
            suffix++;
        } while (Directory.Exists(candidate));
        return candidate;
    }
}
