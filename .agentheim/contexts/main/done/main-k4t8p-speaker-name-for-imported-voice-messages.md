---
id: main-k4t8p
title: Speaker name for imported voice messages
status: done
type: feature
context: main
created: 2026-07-09
completed: 2026-07-09
depends_on: []
blocks: []
tags: [import, speaker, transcription, ui]
related_adrs: []
related_research: []
prior_art: [main-037, main-073]
---

## Why
Importing a WhatsApp voice message and transcribing it always yields a transcript
attributed to the literal label `"Speaker"`. There is no point in the flow where the
user can say who is actually talking, and the existing SPEAKER NAMES panel — which
solves exactly this for recordings — does nothing for imports: `SaveFileImportTranscript`
leaves `RemoteSpeakerNames` empty, so the panel renders no rows, and a name typed into it
references no segment and renames nothing. The user's report: *"the speaker name is not
taken into account."*

Downstream this leaks into the Markdown auto-export (main-m6x4v): every imported voice
message exports as `### Speaker`, so a folder of exported voice notes has no attribution
at all.

## What
Prompt for the speaker's name at import time, and thread that name through the transcript
so it reaches the segments, the SPEAKER NAMES panel, and the Markdown export.

Scope is the imported-file path only — `QueueItemType.File` items that carry a `SessionDir`.
The ephemeral STT API path (`EnqueueFile` without a session dir, used by `POST /transcribe`
and `whisperheim-transcribe`) has no UI and no `transcript.json`, and stays untouched.

Decided with the builder:
- **One prompt per selected file** (the import picker is `Multiselect = true`), shown in
  selection order with the file name visible so the user knows which file they are naming.
- **Field starts empty, and is skippable.** Empty or dismissed → the file imports with
  today's `"Speaker"` label. The prompt never aborts an import.

## Acceptance criteria
- [ ] Importing an audio file via "Import audio files" prompts for a speaker name before the file is enqueued; the field is empty by default.
- [ ] A multi-file selection prompts once per file, in selection order, and each prompt shows the file name being named.
- [ ] Leaving the field empty, or dismissing the prompt (Esc / Cancel), imports that file with the current `"Speaker"` label — the import proceeds either way.
- [ ] The entered name is written to `TranscriptSegment.Speaker` **and** to `CallTranscript.RemoteSpeakerNames` in the session's `transcript.json`.
- [ ] Opening an imported transcript in `TranscriptsPage` shows the name as a row in the SPEAKER NAMES panel, and renaming it there propagates to the segments via `RenameSpeakerGlobally`.
- [ ] Markdown export (manual "MD" button and `TranscriptAutoExportService`) of an imported voice message emits `### <name>` instead of `### Speaker`.
- [ ] `POST /transcribe` and `whisperheim-transcribe` are unchanged: no prompt, no `transcript.json`, no speaker label.
- [ ] Unit tests cover: entered name → segment label + `RemoteSpeakerNames`; empty/dismissed name → `"Speaker"`; Markdown heading reflects the resolved display speaker.

## Notes

**Where it breaks today**
- `TranscriptionQueueService.SaveFileImportTranscript` (`src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs:777`) hardcodes `Speaker = "Speaker"` (`:789`) and never sets `RemoteSpeakerNames` (`:797`).
- `TranscriptsPage.ImportAudioFile` (`src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs:643`) derives the title from the filename stem (`:677`) and enqueues via `EnqueueFileImport` (`:681`) — it asks the user for nothing.
- `ShowSpeakerNamesPanel` (`TranscriptsPage.xaml.cs:2136`) binds rows from `RemoteSpeakerNames`, which is empty for imports → no rows to rename.
- `SaveSpeakerNames` (`:2208`) only propagates a rename when a row has a `PreviousName` and the `SpeakerNameMap` has a matching cluster. Adding a fresh name to an empty list therefore touches no segment. Seeding `RemoteSpeakerNames` at import fixes the panel for free.

**Not the pending drawer.** The SPEAKER NAMES panel is also reachable from the pending
drawer, but that drawer is bound to `PendingRecordingItem` and `QueueTranscription_Click`
builds `mic.wav` / `system.wav` paths (`TranscriptsPage.xaml.cs:1815`) — imports never
reach it. The prompt belongs in the import handler, not there.

**Prompt semantics consequence.** Because dismissing the name prompt still imports the
file, there is no per-file way to back out of a multi-file import once the picker is
confirmed. That is the accepted trade-off (the picker itself is the abort point); revisit
only if it bites in practice.

**Reference for the label seam.** Recordings resolve their labels in
`CallTranscriptionPipeline.GetSpeakerLabel` (`:955`) from `localSpeakerName`
(`AppSettings.General.DefaultSpeakerName`, defaulting to `"You"`) and `remoteSpeakerNames`.
Imports need the far simpler single-name equivalent — do not reuse `DefaultSpeakerName`
here, it means "me", and an imported voice message is by definition someone else
(`IsLocalSpeaker = false` is already correct).

**Prior art.** `main-037` (Speaker Name Editing) built `RenameSpeakerGlobally` +
`SpeakerNameMap` + per-segment `SpeakerOverride`; `main-073` (Speaker Name List, Count
Hint, and Manual Transcription) built the SPEAKER NAMES panel itself. Both are
recording-only. This task extends their reach to imports rather than adding a parallel
mechanism. `main-m6x4v` (Markdown auto-export) is the downstream consumer whose output
changes; `TranscriptMarkdownFormatter.Format` already calls `GetDisplaySpeaker`, so it
needs no change once the segment carries the right label.

## Outcome
- `TranscriptsPage.BrowseFiles_Click` now shows an `InputDialog` ("Speaker Name",
  `Who is speaking in "<file>"?`, empty default) once per selected file, in selection
  order, before calling `ImportAudioFile(filePath, speakerName)`. Confirmed-but-empty
  or dismissed (Esc/Cancel) both pass `null` through; the import always proceeds.
- `ImportAudioFile` threads the optional `speakerName` into
  `TranscriptionQueueService.EnqueueFileImport(title, destPath, sessionDir, speakerName)`.
- `TranscriptionQueueItem` gained a `SpeakerName` property (set via the `sessionDir`
  constructor overload) so the name survives the queue hop and `Retry()` re-enqueue.
- Extracted a new pure, unit-testable static class `FileImportTranscriptBuilder`
  (`Services/Transcription/FileImportTranscriptBuilder.cs`) out of
  `TranscriptionQueueService.SaveFileImportTranscript`. It resolves the entered name
  (trim; empty/whitespace/null -> `"Speaker"`, the pre-existing default) and writes it
  to both `TranscriptSegment.Speaker` and `CallTranscript.RemoteSpeakerNames` (a
  single-item list), which seeds the SPEAKER NAMES panel for imports "for free" per the
  notes above — no changes needed to the panel or `RenameSpeakerGlobally` itself.
  `TranscriptMarkdownFormatter.Format` needed no change either, confirmed by a
  formatter-level test.
- STT API (`POST /transcribe`) and `whisperheim-transcribe` are untouched: both call
  `EnqueueFile` (no session dir), so `ProcessFileItem` never reaches
  `SaveFileImportTranscript`.

**Files changed:**
- `src/WhisperHeim/Services/Transcription/FileImportTranscriptBuilder.cs` (new)
- `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs`
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`
- `tests/WhisperHeim.Tests/FileImportTranscriptBuilderTests.cs` (new, 7 test cases)
- `.agentheim/contexts/main/README.md`
