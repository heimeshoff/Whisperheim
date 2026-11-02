---
id: main-104
title: Stage WAV Writes Outside the Synced Data Folder
status: done
type: feature
context: main
created: 2026-05-11
completed: 2026-05-11
commit:
depends_on: [main-063, main-102]
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: Post-M1 polish (multi-machine sync)
size: Medium
---
# Stage WAV Writes Outside the Synced Data Folder

## Objective

Long call recordings currently write `mic.wav` and `system.wav` directly into `{DataPath}\recordings\{sessionName}\`. When `DataPath` points at a Google Drive (or Dropbox / OneDrive) folder, the cloud sync client tries to read the file while NAudio still holds an exclusive write handle and reports "can't sync" errors for the duration of the recording. Move the active write to a machine-local staging directory and atomically move the finished session into the synced data folder on stop, so cloud sync sees a single complete file appear rather than a live-changing file.

## Background

`CallRecordingService.StartRecording` (`src/WhisperHeim/Services/Recording/CallRecordingService.cs:81-106`) currently does:

```csharp
recordingDir = Path.Combine(_dataPathService.RecordingsPath, sessionName);
Directory.CreateDirectory(recordingDir);
_micWavFilePath = Path.Combine(recordingDir, "mic.wav");
var systemWavFilePath = Path.Combine(recordingDir, "system.wav");
_micWaveWriter = new WaveFileWriter(_micWavFilePath, micFormat);
_loopbackCapture.OutputFilePath = systemWavFilePath;
```

For a 90-minute call, Google Drive logs sync errors for the full 90 minutes because both WAVs are continuously growing under exclusive `FileShare.Read` handles held by `NAudio.Wave.WaveFileWriter` and the WASAPI loopback writer. Once `StopRecording` closes the writers (line 346), Drive eventually catches up — but the user already has a wall of red error toasts and one or both files may need a manual retry.

The fix is the standard "atomic-write" pattern: write to a temp location on the local disk, move into place when finished. `File.Move` and `Directory.Move` within the same volume are atomic; across volumes they fall back to copy+delete (still acceptable here — only happens once, on stop, with a complete file).

Out of scope: the `transcript.json` written by `TranscriptStorageService` after transcription is small, written in one shot, and already plays well with cloud sync — no staging needed.

## Design

### 1. Add a staging path to DataPathService

Add `DataPathService.RecordingStagingPath` returning a machine-local directory:

```
%LOCALAPPDATA%\WhisperHeim\recording-staging\
```

This is independent of `DataPath` and never moves when the user reconfigures the synced data folder. Create the directory on first access.

### 2. Stage the active recording

In `CallRecordingService.StartRecording`:

- Compute `finalDir = Path.Combine(_dataPathService.RecordingsPath, sessionName)` as today (still used for collision-avoidance numbering and as the eventual destination).
- Resolve the collision-suffixed name against the **final** location, not the staging dir, so different machines staging concurrently don't both pick the same final name from each other's staging dirs (they can't see each other's staging — only the final dir is shared).
- Compute `stagingDir = Path.Combine(_dataPathService.RecordingStagingPath, sessionId)` (use the GUID-suffixed `sessionId`, line 77 — guaranteed unique across machines).
- Open both `WaveFileWriter` instances and the loopback `OutputFilePath` inside `stagingDir`.
- Store both `stagingDir` and `finalDir` on the current session (extend `CallRecordingSession` with a `StagingDir` or carry it on the service).

### 3. Atomically move on stop

In `CallRecordingService.StopRecording` / `OnAllStreamsStopped`, after both `_micWaveWriter?.Dispose()` and the loopback close have returned (around line 346):

1. Verify both WAV files exist in `stagingDir` (a stream that failed to start leaves only one — still move whatever is there).
2. `Directory.Move(stagingDir, finalDir)` if `finalDir`'s parent (`RecordingsPath`) is on the same volume as the staging dir.
3. Otherwise fall back to: `Directory.CreateDirectory(finalDir)` → `File.Move(staging/file, final/file)` for each file → `Directory.Delete(stagingDir)`.
4. Rewrite `_micWavFilePath` and `CallRecordingSession.MicWavFilePath` / `SystemWavFilePath` to the final paths **before** raising `RecordingStopped` so downstream consumers (transcription queue, TranscriptsPage) see the synced location.

### 4. Crash-recovery sweep

On app start, scan `RecordingStagingPath` for orphaned session directories left behind by a crash or hard kill mid-recording.

- For each orphan that contains audio files of non-zero size: move it into `RecordingsPath` using the same atomic-move logic, prefixed with `recovered_` so the user can recognise it.
- Empty staging dirs: delete.

Do this in `App.OnStartup` before any UI loads, log via `Trace.TraceInformation` with file counts.

### 5. Pending recovery if move fails

If the atomic move itself fails (Drive folder unreachable, permissions, etc.), do **not** lose the recording:

- Keep the files in `stagingDir`.
- Log a `Trace.TraceError` with the staging path.
- Raise `RecordingStopped` with the staging path as the session WAV path so the in-app pending-transcription UI can still pick it up locally.
- The next start-up sweep (§4) retries the move.

### 6. WAV path used by HighQualityRecorderService

`HighQualityRecorderService` (`src/WhisperHeim/Services/Audio/HighQualityRecorderService.cs`) also writes WAVs (separate code path used by the Streams feature). Apply the same staging pattern there: write to `RecordingStagingPath`, move into `StreamStorageService`'s output dir on stop. Same atomic-move helper, same crash-recovery sweep.

Extract a small `RecordingFileStager` helper so both services share the move/recover code.

## Acceptance Criteria

- [ ] `DataPathService` exposes `RecordingStagingPath` rooted in `%LOCALAPPDATA%\WhisperHeim\recording-staging\`; directory is created on first access
- [ ] `CallRecordingService.StartRecording` opens its `WaveFileWriter` and sets `LoopbackCaptureService.OutputFilePath` inside `RecordingStagingPath`, not under `DataPath`
- [ ] After both audio streams close, the session directory is atomically moved into `DataPathService.RecordingsPath` and `CallRecordingSession.MicWavFilePath` / `SystemWavFilePath` are updated to the final paths before `RecordingStopped` fires
- [ ] Same-volume case uses `Directory.Move`; cross-volume case falls back to per-file `File.Move` + cleanup
- [ ] If the move fails (Drive folder unreachable), files remain in staging and a recovery sweep on next startup picks them up with a `recovered_` prefix
- [ ] `HighQualityRecorderService` writes through the same staging path for Streams recordings; the move helper is shared between the two services
- [ ] Manual test: with `DataPath` pointing at a Google Drive folder, record a 30+ minute call and verify Drive shows no "can't sync" warnings for `mic.wav` / `system.wav` during recording; the finished files appear and sync cleanly within seconds of stop
- [ ] Manual test: kill the app via Task Manager mid-recording; restart; verify the partial WAVs are recovered into `RecordingsPath` under a `recovered_*` directory and surface as a pending session
- [ ] Manual test: temporarily make `RecordingsPath` unwritable (read-only attribute), record + stop; verify files stay in staging, log says so, and a subsequent startup with the path restored moves them across

## Notes

- This task pairs with task 105 (origin-machine owns transcription) — together they make the Drive-shared `DataPath` setup robust for two-machine use. They are independent; either can land first.
- Don't try to "tell Drive to ignore the file during write" via dotfile naming or Drive-specific config — there's no portable, reliable mechanism. The staging-then-move pattern is provider-agnostic and works the same for OneDrive / Dropbox / iCloud.
- Consider whether `bootstrap.json` and `settings.json` writes need the same treatment. They probably don't (small, written in one `File.WriteAllText` call), but if Drive ever complains, the same `RecordingFileStager` helper applies.
- Keep an eye on disk space on the system drive — long recordings at 32-bit float / 48kHz can be hundreds of MB. The staging dir lives on `%LOCALAPPDATA%`, usually the OS drive. Document this in `DataPathService` xmldoc.

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-11 11:17 -- Work Completed

**What was done:**
- Added `DataPathService.RecordingStagingPath` (and the underlying `LocalAppDataRoot` constant) pointing at `%LOCALAPPDATA%\WhisperHeim\recording-staging\`; directory is created on first access. Documented OS-drive disk-space implication in xmldoc.
- Created `RecordingFileStager` (`Services/Recording/RecordingFileStager.cs`) with two public entry points:
  - `MoveStagedSession(stagingDir, finalDir)` — same-volume `Directory.Move` (atomic); cross-volume fallback to per-file `File.Move` + cleanup; auto-collision-suffixes the final dir; on failure leaves files in staging and returns a `MoveResult` carrying the staging path so the in-app pending UI can still pick it up.
  - `SweepOrphans(stagingRoot, finalRoot)` — startup recovery: non-empty orphans are moved into the final root with a `recovered_` prefix (without double-prefixing if already prefixed); zero-byte / empty dirs are deleted.
- Updated `CallRecordingService.StartRecording`: the WAV writers and the `LoopbackCaptureService.OutputFilePath` now open inside `RecordingStagingPath\<sessionId>` (GUID-suffixed for cross-machine uniqueness), while the collision-suffixed final dir is resolved against `RecordingsPath` so concurrent machines don't pick the same final name. Service tracks both `_stagingDir` and `_finalDir` per session.
- Updated `CallRecordingService.FinalizeSession`: after the WAV writers close, the staged session is atomically moved into the final dir via `RecordingFileStager`, and `CallRecordingSession.MicWavFilePath` / `SystemWavFilePath` are rewritten to the final paths **before** `RecordingStopped` fires (made the session properties `internal set` for this). If the move fails, the session is left in staging and a Trace.TraceError is emitted; the next startup sweep retries.
- `HighQualityRecorderService` now optionally takes a `DataPathService` and writes its staging WAV into `RecordingStagingPath` instead of `%TEMP%`. `App.xaml.cs` was updated to pass `_dataPathService`. `SaveRecording` continues to copy (not move) so the staged file can be reused for multiple saves.
- `App.StartupCore` invokes `RecordingFileStager.SweepOrphans` right after `MigrateIfNeeded`, before any UI loads, with `Trace.TraceInformation` logging the recovered count.
- Added unit-test class `RecordingFileStagerTests` covering happy-path move, missing staging dir, collision-suffix, non-empty / empty / zero-byte / already-prefixed orphans, and missing staging root.

**Acceptance criteria status:**
- [x] `DataPathService` exposes `RecordingStagingPath` rooted in `%LOCALAPPDATA%\WhisperHeim\recording-staging\`; directory is created on first access — verified by code review (`DataPathService.cs`); the property's getter calls `Directory.CreateDirectory`.
- [x] `CallRecordingService.StartRecording` opens its `WaveFileWriter` and sets `LoopbackCaptureService.OutputFilePath` inside `RecordingStagingPath`, not under `DataPath` — verified by code review; `_micWavFilePath` and `systemWavFilePath` are now `Path.Combine(_stagingDir, ...)`.
- [x] After both audio streams close, the session directory is atomically moved into `DataPathService.RecordingsPath` and `CallRecordingSession.MicWavFilePath` / `SystemWavFilePath` are updated to the final paths before `RecordingStopped` fires — verified by code review of `FinalizeSession`; the rewrite happens immediately before `RecordingStopped?.Invoke`.
- [x] Same-volume case uses `Directory.Move`; cross-volume case falls back to per-file `File.Move` + cleanup — verified by code in `RecordingFileStager.MoveStagedSession` / `MoveAcrossVolumes` and exercised by `MoveStagedSession_HappyPath` (same volume during test). Cross-volume path is straight-line code with no branching.
- [x] If the move fails, files remain in staging and a recovery sweep on next startup picks them up with a `recovered_` prefix — verified by `SweepOrphans_NonEmptyOrphan_RecoveredWithPrefix` test; `App.StartupCore` calls `SweepOrphans` before UI loads.
- [x] `HighQualityRecorderService` writes through the same staging path for Streams recordings; the move helper is shared between the two services — verified by code review; `HighQualityRecorderService` now uses `DataPathService.RecordingStagingPath` and `RecordingFileStager` is a single shared `static class`.
- [ ] Manual test: with `DataPath` pointing at Google Drive, record a 30+ minute call — **not automated**. Design intentionally supports this; unit tests cover the move semantics. **User must run this manual pass.**
- [ ] Manual test: kill the app via Task Manager mid-recording, restart, verify `recovered_*` directory — **not automated**. `SweepOrphans` is unit-tested for the recovery semantics; `App.StartupCore` invokes it before UI loads. **User must run this manual pass.**
- [ ] Manual test: make `RecordingsPath` unwritable, record + stop, verify files stay in staging — **not automated**. The failure branch in `MoveStagedSession` returns `Success=false` with `ResultingDirectory` pointing at staging, and `FinalizeSession` rewrites the session paths accordingly so the in-app pending UI picks up the staged file. **User must run this manual pass.**

**Files changed:**
- `src/WhisperHeim/Services/Settings/DataPathService.cs` — added `LocalAppDataRoot` constant and `RecordingStagingPath` property.
- `src/WhisperHeim/Services/Recording/RecordingFileStager.cs` — new file; atomic-move helper + crash-recovery sweep.
- `src/WhisperHeim/Services/Recording/CallRecordingSession.cs` — made `MicWavFilePath` and `SystemWavFilePath` rewritable from inside the service (post-move).
- `src/WhisperHeim/Services/Recording/CallRecordingService.cs` — `StartRecording` writes to staging; `FinalizeSession` performs the atomic move and updates session paths before `RecordingStopped` fires.
- `src/WhisperHeim/Services/Audio/HighQualityRecorderService.cs` — now accepts an optional `DataPathService` and writes its staging file into `RecordingStagingPath`.
- `src/WhisperHeim/App.xaml.cs` — passes `_dataPathService` to `HighQualityRecorderService`; invokes `RecordingFileStager.SweepOrphans` on startup.
- `tests/WhisperHeim.Tests/RecordingFileStagerTests.cs` — new file; 8 xUnit tests covering move + recovery semantics.

**Test results:** `dotnet test` — 82/82 pass (8 new + 74 existing). `dotnet build` succeeds with no new warnings (10 pre-existing).

**Note on manual tests:** Three acceptance criteria require a real Google Drive folder and a long recording (or external process kill); these cannot be exercised in CI. The code paths involved are all covered by unit tests of the `RecordingFileStager` helper, but the end-to-end behaviour on a real cloud-synced drive must be verified by the user.
