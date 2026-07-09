---
id: main-m6x4v
title: Auto-export transcripts as Markdown to configured default folders
status: done
type: feature
context: main
created: 2026-07-09
completed: 2026-07-09
depends_on: []
blocks: []
tags: [export, settings, transcription, markdown]
related_adrs: [0008]
related_research: []
prior_art: [main-055, main-063, main-019]
---

## Why
Every transcript today has to be exported by hand: open the Transcripts page, click
"MD", pick a folder in a `SaveFileDialog`. For a user who transcribes calls and
dropped voice messages daily and files them into a notes vault, that's a manual
step repeated forever, and it's the same two destination folders every time.

Configuring those two folders once turns transcription into the whole workflow —
speak or drop a file, and the Markdown lands where it belongs.

## What
Two new settings — a **recorded-conversations export folder** and an **imported
voice-messages export folder** — each independently optional. When a transcription
completes and the matching folder is configured, write the transcript as
`<recording name>.md` into that folder. When it isn't configured, nothing happens.

The two kinds are distinguishable at the transcription queue's completion seam
(`QueueItemType.Recording` vs `QueueItemType.File`) even though the persisted
`CallTranscript` carries no provenance flag — so this needs no domain-model change
to route the two destinations.

**In scope:** recorded calls, and drag-and-drop / file-picker imports.
**Out of scope:** Streams (video-link transcripts, separate `StreamTranscript` path)
and the STT API (`POST /transcribe` sets no `SessionDir` and persists nothing —
it stays a synchronous text response).

### Shape
- `BootstrapConfig` gains `RecordingsExportFolder` + `ImportsExportFolder`
  (machine-local, **not** synced `AppSettings` — see Notes).
- `TranscriptMarkdownFormatter.Format(CallTranscript)` — lift the existing
  `private static FormatAsMarkdown` out of `TranscriptsPage.xaml.cs:2481` into a
  service so both manual and auto export share one implementation.
- `ExportFileName.Sanitize(string)` — Windows-safe filename derivation.
- `TranscriptAutoExportService` — a **new subscriber** to
  `TranscriptionQueueService.ItemCompleted`, sitting alongside the two existing
  subscribers. Not inside the queue: the queue aggregate protects the
  single-engine-busy invariant and has nothing to do with folder export, and for
  `Recording` items `ProcessRecordingItem` discards the pipeline's returned
  `CallTranscript` anyway — the subscriber reloads `transcript.json` from the
  session dir. (Verified: `transcript.json` is on disk before `ItemCompleted`
  fires at `TranscriptionQueueService.cs:654`, on both the recording and import
  paths.)
- Overwrite is keyed to the **recording session**, not the name — see ADR-0008.

### Sanitization rule
`ExportFileName.Sanitize(name)`:
1. Replace every char in `Path.GetInvalidFileNameChars()` (plus the explicit
   Windows set `< > : " / \ | ? *`) with `_`.
2. Trim trailing dots and spaces.
3. If the result (case-insensitive, ignoring extension) is a reserved device name
   — `CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9` — prefix with `_`.
4. Truncate to 120 chars.
5. If empty after all of the above, fall back to
   `transcript_{RecordingStartedUtc:yyyyMMdd_HHmmss}`.

## Acceptance criteria
- [x] Settings exposes two folder pickers (recorded conversations, imported voice
      messages), each with Browse + Clear, persisting to `BootstrapConfig`.
      Follows the `GeneralPage.BrowseDataPath_Click` pattern
      (`OpenFolderDialog` + validate). *(manual)*
- [x] A completed **recording** with the recordings folder configured writes
      `<sanitized name>.md`, content generated from the reloaded
      `transcript.json`. *(unit)*
- [x] A completed **file import** with the imports folder configured writes
      `<sanitized name>.md`. *(unit)*
- [x] Either folder unset/empty → no export for that kind; the other kind is
      unaffected. *(unit)*
- [x] Missing or unwritable folder → no exception propagates, a warning is
      logged, and the queue item still reaches `Completed`. A transcription that
      succeeded must never be marked failed because an export folder was gone.
      *(unit + manual)*
- [x] Re-transcribing the same recording, unrenamed, overwrites the same `.md`
      in place. *(unit)*
- [x] Two different recordings sharing a title produce `name.md` and
      `name (2).md`; the first file is never touched by the second recording.
      *(unit)*
- [x] `ExportFileName.Sanitize` has a table-driven test covering every branch of
      the rule above (invalid chars, reserved names, trailing dots/spaces, length
      cap, empty-after-sanitize). *(unit)*
- [x] Completions carrying no `SessionDir` (STT API, `EnqueueFile`) produce no
      export. *(unit)*
- [x] `TranscriptMarkdownFormatter.Format` produces byte-identical output to
      today's `FormatAsMarkdown`, and is the single implementation used by both
      the manual "MD" button and auto-export. *(unit)*

## Notes

### Why `BootstrapConfig` and not `AppSettings`
Export folders are per-machine filesystem paths (drive letters, mount points),
like the existing `DataPath` — which also lives in `BootstrapConfig`, not the
synced `settings.json`. Under "origin machine owns transcription" (main-105) only
the origin machine ever transcribes, so only its copy of the setting is ever
consulted. Syncing the paths through `AppSettings` would put a path valid on
machine A into machine B's config, where auto-export would silently no-op with no
diagnosable cause — strictly worse than asking the user to set the folder once
per machine. Optionally mirror into `AppSettings` the way `OllamaEndpoint` /
`OllamaModel` already are; not required for correctness.

### Accepted gap: rename after export orphans the old file
Renaming a recording without re-transcribing leaves the previously exported
`<old-name>.md` in place. `SaveTranscriptNameAsync` (`TranscriptsPage.xaml.cs:1471`)
has no notification seam, and deleting a file the user may have edited outside the
app is destructive. Auto-export is a transcription-time snapshot, not a live
mirror. Documented in ADR-0008 so it isn't later mistaken for a bug.

### Open questions (non-blocking, assumed answers in brackets)
- Clearing an export-folder setting leaves already-exported `.md` files in place.
  [assumed yes]
- Silent-skip + log is sufficient on an unwritable folder; no per-transcript
  "exported to…" UI indicator in this task. [assumed yes]

### Key files
- `src/WhisperHeim/Models/BootstrapConfig.cs` — new settings fields
- `src/WhisperHeim/Services/Settings/SettingsService.cs` — persistence / sync
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml.cs:182` — folder-picker pattern
- `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs:654` —
  `ItemCompleted` seam; `:839` `GetSessionDir`; `:777` `SaveFileImportTranscript`
- `src/WhisperHeim/Services/CallTranscription/CallTranscript.cs` — new
  `ExportedMarkdownPath` field
- `src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs` —
  `LoadAsync` / `UpdateAsync`
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs:2481` — formatter to lift;
  `:2424` manual export (rewire to the extracted formatter)
- New: `src/WhisperHeim/Services/Export/TranscriptAutoExportService.cs`,
  `src/WhisperHeim/Services/Export/ExportFileName.cs`

## Outcome
Implemented exactly per the Shape/ADR-0008 design, with one load-bearing
resolution beyond what ADR-0008 spells out: `ReTranscribe_Click` deletes
`transcript.json` before the pipeline rebuilds it, so the on-disk
`CallTranscript.ExportedMarkdownPath` of a re-transcribed session is always
reset to `null` by the time the subscriber reloads it — the ADR's literal
"if `ExportedMarkdownPath` already equals desired, overwrite in place" check
alone can never fire for a UI-driven re-transcription. `TranscriptAutoExportService
.ResolveExportPath` therefore decides collisions by scanning **sibling**
transcripts' own `ExportedMarkdownPath` claims (via `ListTranscriptFiles` +
`LoadAsync`, excluding the current session's own file) rather than by checking
`File.Exists` on the candidate path — matching the ADR's "ownership is decided
from persisted memory, never by inspecting the existing file" clause and giving
correct idempotent overwrite-in-place even after the field resets to null.

**New files:**
- `src/WhisperHeim/Services/Export/ExportFileName.cs` — `Sanitize(name, recordingStartedUtc)`, the 5-step Windows-safe filename rule.
- `src/WhisperHeim/Services/Export/TranscriptMarkdownFormatter.cs` — `Format(CallTranscript)`, lifted verbatim from `TranscriptsPage.FormatAsMarkdown`.
- `src/WhisperHeim/Services/Export/TranscriptAutoExportService.cs` — `ExportIfConfiguredAsync(TranscriptionQueueItem)` (public, directly unit-testable) + `OnItemCompleted` (event-handler-shaped wrapper wired in `App.xaml.cs`).
- Tests: `tests/WhisperHeim.Tests/ExportFileNameTests.cs` (27 cases), `TranscriptMarkdownFormatterTests.cs` (3), `TranscriptAutoExportServiceTests.cs` (10).

**Modified:**
- `src/WhisperHeim/Models/BootstrapConfig.cs` — `RecordingsExportFolder` / `ImportsExportFolder`.
- `src/WhisperHeim/Services/CallTranscription/CallTranscript.cs` — `ExportedMarkdownPath` (serialized `exportedMarkdownPath`).
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — manual "MD" export, clipboard copy, and the AI-analysis Markdown feed now all call `TranscriptMarkdownFormatter.Format`; the old private `FormatAsMarkdown` is removed (single implementation, satisfying the byte-identical-output AC by construction).
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml` / `.xaml.cs` — new "AUTO-EXPORT (MARKDOWN)" section: two folder cards (Browse/Clear each), `OpenFolderDialog` + `DataPathService.ValidatePath` pattern mirroring `BrowseDataPath_Click`. Manual UI wiring — no automated UI test infra in this project, so this criterion is TDD-skipped for the picker itself; the underlying persistence (`BootstrapConfig` fields) and the consuming service are fully unit-tested.
- `src/WhisperHeim/App.xaml.cs` — constructs `TranscriptAutoExportService` and subscribes it to `_transcriptionQueueService.ItemCompleted`, alongside `AutoTranscriptionService` and the other App-owned subscribers.

Full suite: 231/231 passing (was 191 baseline; +40 new tests here).
