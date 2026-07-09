---
id: main-m6x4v
title: Auto-export transcripts as Markdown to configured default folders
status: todo
type: feature
context: main
created: 2026-07-09
completed:
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
- [ ] Settings exposes two folder pickers (recorded conversations, imported voice
      messages), each with Browse + Clear, persisting to `BootstrapConfig`.
      Follows the `GeneralPage.BrowseDataPath_Click` pattern
      (`OpenFolderDialog` + validate). *(manual)*
- [ ] A completed **recording** with the recordings folder configured writes
      `<sanitized name>.md`, content generated from the reloaded
      `transcript.json`. *(unit)*
- [ ] A completed **file import** with the imports folder configured writes
      `<sanitized name>.md`. *(unit)*
- [ ] Either folder unset/empty → no export for that kind; the other kind is
      unaffected. *(unit)*
- [ ] Missing or unwritable folder → no exception propagates, a warning is
      logged, and the queue item still reaches `Completed`. A transcription that
      succeeded must never be marked failed because an export folder was gone.
      *(unit + manual)*
- [ ] Re-transcribing the same recording, unrenamed, overwrites the same `.md`
      in place. *(unit)*
- [ ] Two different recordings sharing a title produce `name.md` and
      `name (2).md`; the first file is never touched by the second recording.
      *(unit)*
- [ ] `ExportFileName.Sanitize` has a table-driven test covering every branch of
      the rule above (invalid chars, reserved names, trailing dots/spaces, length
      cap, empty-after-sanitize). *(unit)*
- [ ] Completions carrying no `SessionDir` (STT API, `EnqueueFile`) produce no
      export. *(unit)*
- [ ] `TranscriptMarkdownFormatter.Format` produces byte-identical output to
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
