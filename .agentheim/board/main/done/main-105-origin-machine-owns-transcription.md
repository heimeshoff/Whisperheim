---
id: main-105
title: Origin-Machine Owns Transcription (Multi-Machine Coordination)
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
# Origin-Machine Owns Transcription (Multi-Machine Coordination)

## Objective

When two WhisperHeim instances share the same `DataPath` over Google Drive (or any cloud-sync provider), prevent both machines from auto-transcribing the same recording. Decide ownership by recording origin: the machine that recorded a session is the one that transcribes it. Other machines see the audio + transcript appear via sync and never race for the work. Provide a manual "transcribe here anyway" action for the case where the origin machine is offline.

## Background

Auto-transcription today is triggered in-process: `CallRecordingService.RecordingStopped` fires on the recording machine, `TranscriptsPage.OnRecordingStopped` (`src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs:195`) enqueues the session into `_queueService`. This is fine for the recording machine.

The cross-machine race is in `TranscriptStorageService.ListPendingSessions` (`src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs:189`): it returns every session directory under `RecordingsPath` that has audio but no `transcript.json`. When machine A is recording (or has recorded but not yet finished transcribing), the session dir syncs to machine B via Drive — and machine B's pending-sessions UI lists it. If the user (or any auto-trigger added later) clicks transcribe on B, both machines run the same heavy job in parallel and race to write `transcript.json` (Drive will then create a `(Conflict)` copy).

Google Drive is eventually-consistent. Lock-file / claim-by-rename schemes over Drive are unreliable — both machines can "succeed" locally and Drive resolves with conflict copies. The robust pragmatic answer is to use **origin** as the coordination key: a machine identifier stamped on the recording at creation time, in machine-local config, never racing for anything across the wire.

## Design

### 1. Stable machine identifier

Add `MachineId` to `BootstrapConfig` (machine-local, see task 102 §1 for the pattern). On first run, populate it with a short stable string. Two options, pick one:

- **`Environment.MachineName`** — already unique per Windows machine for this user's two-machine setup; human-readable in filenames; can in theory change if the user renames the PC.
- **`Guid.NewGuid().ToString("N").Substring(0, 8)`** — random, stable across renames, but opaque.

Recommendation: `Environment.MachineName`, sanitised (`[^A-Za-z0-9-]` → `-`), with a `Guid` fallback if the sanitised name is empty or longer than 32 chars. The user runs two specific PCs; readable filenames are a real ergonomic win and the rename risk is negligible.

Expose `DataPathService.MachineId` (or `SettingsService.MachineId`) as a single property the rest of the code reads.

### 2. Stamp recordings with their origin

In `CallRecordingService.StartRecording` (`src/WhisperHeim/Services/Recording/CallRecordingService.cs:84-85`), change the session directory name from:

```
{yyyyMMdd_HHmmss}
```

to:

```
{yyyyMMdd_HHmmss}_{machineId}
```

Keep the existing `_n` collision suffix logic. This means the directory name itself carries the origin — no extra metadata file needed for the gating logic, and the directory is human-scannable in Drive (`20260511_142345_desktop`, `20260511_153012_laptop`).

Also write a small `session.json` inside the directory at recording-start time:

```json
{
  "sessionId": "20260511_142345_a1b2c3d4...",
  "machineId": "desktop",
  "startedAt": "2026-05-11T14:23:45.000Z",
  "schemaVersion": 1
}
```

This gives a stable structured field that survives future renames of the directory and is what the gating code reads (filename is a fallback for older recordings).

### 3. Apply the same to HighQualityRecorderService

The Streams feature uses `HighQualityRecorderService` and `StreamStorageService` — apply the same `{timestamp}_{machineId}` directory naming and `session.json` write so Streams recordings are coordinated the same way.

### 4. Gating in ListPendingSessions

Change `TranscriptStorageService.ListPendingSessions` to take a parameter (or read `DataPathService.MachineId` directly) and filter to only sessions whose `session.json.machineId` matches this machine.

For sessions without `session.json` (older recordings created before this task lands): fall back to parsing the trailing `_{machineId}` from the directory name. For ancient recordings with neither: treat as "owned by this machine" so existing single-machine users see no change.

The `TranscriptionQueueService.HasExceededRetryLimit` filter stays.

### 5. UI: distinguish "mine" / "other machine's" pending recordings

The pending-recordings UI (Transcripts page, pending drawer added in task 098) currently shows whatever `ListPendingSessions` returns. After §4, that list is just this machine's. Add a second list section (or a tab / toggle) showing **other machines' pending recordings** — read separately via `ListPendingSessionsFromOtherMachines()`.

For each "other machine" entry, show:

- Title / timestamp
- Originating machine name
- Action: **"Transcribe here"** — bypasses the gate and enqueues into this machine's `TranscriptionQueueService`

When the user clicks "Transcribe here":

1. Write `transcribing.lock` in the session dir containing `{machineId, startedAt}`. Best-effort: this is a hint to the user, not a distributed lock — Drive eventual consistency means we can't guarantee atomicity.
2. Enqueue + run.
3. On success, the `transcript.json` write wins. If the origin machine comes back online and also tries to transcribe, it sees the lock file (after sync settles, seconds to minutes) and skips; if Drive produces a conflict copy of `transcript.json` anyway, the user can resolve manually — this is a rare edge case.
4. On finish (success or failure), delete `transcribing.lock`.

Auto-transcription remains origin-gated; the "transcribe here" button is the only way to take over.

### 6. Settings hot-reload interaction

Task 102's hot-reload watches `settings.json`. This task adds `session.json` per recording — not a config file, doesn't need hot-reload. The pending-sessions UI already refreshes on a timer / page focus, which is sufficient.

`MachineId` lives in `bootstrap.json` (machine-local). It must never be synced and must never be cleared by the migration path. Verify the existing `DataPathService.MigrateIfNeeded` does not touch `MachineId`.

### 7. Sanity checks

- Make `MachineId` immutable after first generation. Changing it would orphan all recordings on this machine. If detection logic later wants to handle rename, add an explicit `previousMachineIds: []` array in bootstrap rather than mutating the current id.
- Log the resolved `MachineId` once at app start via `Trace.TraceInformation` so users can verify it from the trace log.
- Surface `MachineId` somewhere in the About / General page so the user can confirm which machine is which without trawling logs.

## Acceptance Criteria

- [ ] `BootstrapConfig` has a `MachineId` field, populated on first run from sanitised `Environment.MachineName` (fallback to short Guid), never synced
- [ ] `DataPathService` (or `SettingsService`) exposes `MachineId` as a read-only property
- [ ] `CallRecordingService.StartRecording` names the session directory `{yyyyMMdd_HHmmss}_{machineId}` and writes a `session.json` at start with `sessionId`, `machineId`, `startedAt`, `schemaVersion`
- [ ] `HighQualityRecorderService` (Streams) does the same
- [ ] `TranscriptStorageService.ListPendingSessions` filters to only sessions owned by `MachineId`, using `session.json` first, falling back to directory-name suffix, finally treating un-stamped sessions as owned-by-this-machine for backward compatibility
- [ ] A new `ListPendingSessionsFromOtherMachines()` returns the complementary set
- [ ] Transcripts page shows other-machine pending recordings in a separate section with a "Transcribe here" action
- [ ] "Transcribe here" writes a `transcribing.lock`, enqueues locally, removes the lock when done
- [ ] `MachineId` is logged at startup and surfaced in the General or About page
- [ ] Manual test: record on machine A with a Drive-synced `DataPath`. Open WhisperHeim on machine B. Confirm A's session appears under "Other machine" pending and is **not** auto-transcribed by B
- [ ] Manual test: take A offline; on B, click "Transcribe here" for A's session; verify `transcript.json` is produced and syncs back
- [ ] Manual test: bring A back online with B mid-transcription; verify A does **not** start a second transcription job (sees lock OR sees finished transcript)
- [ ] Manual test: rename a machine and confirm `MachineId` does not change

## Notes

- Pairs with task 104. Together: 104 stops Drive from choking on the active WAV; 105 stops both machines from racing on the resulting recording. Land them in either order — they're independent.
- Resist the temptation to add a richer distributed-lock or leasing system. Two machines, one shared folder, eventually-consistent storage — origin ownership + manual takeover is the right complexity ceiling. If it ever needs to scale further (3+ machines, automatic failover), revisit then.
- The `transcribing.lock` is intentionally **advisory**. Don't write code that crashes or hard-fails on lock contention; treat the lock as a UI hint. The actual correctness guarantee is that auto-transcription is origin-gated; manual takeover is rare and the user is aware.
- Consider an opt-out: if a user actually wants both machines to attempt transcription (e.g. for benchmarking or one machine is a dedicated transcriber), an `OllamaTranscriptionOwnership: "origin" | "any"` bootstrap field could relax the gate later. Not in scope now.

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-11 11:28 -- Work Completed

**What was done:**
- Added `MachineId` to `BootstrapConfig` and a lazy `DataPathService.MachineId` property that generates the id once (sanitised `Environment.MachineName`, fallback to 8-char Guid) and persists it. Logged at app start via `Trace.TraceInformation`.
- Created `SessionMetadata` / `SessionMetadataStore` (per-session `session.json` writer + reader, plus `ResolveOriginMachineId` with the three-tier fallback specified in the design: session.json → directory-name suffix → null for legacy).
- Created `TranscribingLock` advisory-lock helper (`transcribing.lock` write/remove/read) used by the manual takeover action; documented as advisory, never crash on contention.
- Updated `CallRecordingService.StartRecording` to use the post-104 staging shape: final dir is now `{yyyyMMdd_HHmmss}_{machineId}` (collision counter still appended); a `session.json` is written into the staging dir before WAVs open, so it moves atomically with them.
- Updated `TranscriptStorageService`: `ListPendingSessions` now filters to sessions owned by this machine (or legacy un-stamped dirs); added `ListPendingSessionsFromOtherMachines` returning the complementary set. `SaveAsync` now finds an existing session dir by timestamp prefix (so the transcript lands beside its WAVs even with the new `_{machineId}` suffix) and falls back to creating a fresh `{timestamp}_{machineId}` dir. Exposed the `DataPathService` so the UI can read `MachineId` without an extra injection.
- Added the "OTHER MACHINE" section to `TranscriptsPage.xaml` (mirrors the existing PENDING section visually but emits a "Transcribe here" button). Wired `LoadOtherMachinePendingSessions` and `TranscribeHere_Click` in the code-behind, plus an `OnQueueItemCompleted` hook to drop the advisory lock when the takeover finishes (success or failure). Updated `GroupToggle_Click` to route between the two pending toggles.
- Added a "Machine identifier" card to `GeneralPage.xaml(.cs)` so the user can see the resolved id without trawling the log.
- Added `SessionMetadataStoreTests` covering round-trip, missing file, session.json-wins-over-suffix, suffix fallback, collision suffix (`_n`), legacy un-stamped dirs, and the `recovered_` prefix from `RecordingFileStager.SweepOrphans`.

**Acceptance criteria status:**
- [x] `BootstrapConfig.MachineId` present, populated on first run from sanitised `Environment.MachineName` (fallback to 8-char Guid), persisted to machine-local `bootstrap.json`, never synced — `BootstrapConfig.cs:53`, `DataPathService.GenerateMachineId`.
- [x] `DataPathService.MachineId` exposed as a read-only property (computed-on-first-access, cached in bootstrap thereafter).
- [x] `CallRecordingService.StartRecording` names the final dir `{yyyyMMdd_HHmmss}_{machineId}` and writes `session.json` (sessionId, machineId, startedAt, schemaVersion) into the staging dir so it moves atomically.
- [x] `HighQualityRecorderService` — **NOT MODIFIED.** Voice clone samples are written as a single `whisperheim_voice_{guid}.wav` to staging and copied to a caller-supplied `voices/` destination; they do not produce session directories and are not scanned by `ListPendingSessions`, so the multi-machine race the task is gating doesn't apply. The brief lists this under "Streams", but Streams in this codebase is URL-based (`StreamTranscriptionService`), not a recording feature. Documented here so a follow-up can revisit if voice samples ever become a cross-machine concern.
- [x] `ListPendingSessions` filters to this-machine + legacy-unstamped — `TranscriptStorageService.EnumeratePendingSessions(SessionOwnership.MineOrLegacy)`.
- [x] `ListPendingSessionsFromOtherMachines` added — same enumerator with `SessionOwnership.OtherMachine`.
- [x] Transcripts page shows the other-machine section with a "Transcribe here" action — `OtherMachinePendingSection` in XAML + `LoadOtherMachinePendingSessions` / `TranscribeHere_Click` in code-behind.
- [x] "Transcribe here" writes the lock, enqueues, removes the lock when the queue item completes — `TranscribingLock.Write/Remove`, `OnQueueItemCompleted`.
- [x] `MachineId` logged at startup (`[App] Machine id: {0}`) and shown on General page ("Machine identifier" card).
- [ ] Manual cross-machine tests **cannot be automated** and must be run by the user:
  - Record on A with Drive-synced DataPath; on B confirm A's session appears under "Other machine" and is not auto-transcribed by B.
  - Take A offline; on B click "Transcribe here" for A's session; verify `transcript.json` is produced and syncs back.
  - Bring A back online mid-transcription on B; verify A doesn't start a second job (sees lock OR sees finished transcript).
  - Rename a machine and confirm the stored `MachineId` does not change (it's persisted to `bootstrap.json` and only generated when missing).

**Files changed:**
- `src/WhisperHeim/Models/BootstrapConfig.cs` — added `MachineId` field.
- `src/WhisperHeim/Services/Settings/DataPathService.cs` — `MachineId` property + `GenerateMachineId`.
- `src/WhisperHeim/Services/Recording/SessionMetadata.cs` — new file: per-session metadata model + reader/writer/resolver.
- `src/WhisperHeim/Services/Recording/TranscribingLock.cs` — new file: advisory-lock helper.
- `src/WhisperHeim/Services/Recording/CallRecordingService.cs` — directory naming + `session.json` write at start.
- `src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs` — gating in `ListPendingSessions`, new `ListPendingSessionsFromOtherMachines`, `SaveAsync` adapted to the new directory naming, exposed `DataPathService`.
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — added `OtherMachinePendingSection`.
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — `LoadOtherMachinePendingSessions`, `TranscribeHere_Click`, `OnQueueItemCompleted`, `OtherMachinePendingItem` view model, two-toggle `GroupToggle_Click`.
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml` — "Machine identifier" card.
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml.cs` — populate the new display.
- `src/WhisperHeim/App.xaml.cs` — startup trace of `MachineId`.
- `tests/WhisperHeim.Tests/SessionMetadataStoreTests.cs` — new tests (8 cases; all green; full suite 90/90 passing).
