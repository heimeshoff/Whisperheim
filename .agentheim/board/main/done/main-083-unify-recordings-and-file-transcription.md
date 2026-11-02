---
id: main-083
title: Unify Recordings & File Transcription
status: done
type: refactor
context: main
created: 
completed: 2026-03-25
commit:
depends_on: [main-075, main-076, main-071]
blocks: []
tags: [m3, voice-files]
related_adrs: []
related_research: []
prior_art: []
milestone: M3 (Voice Message Transcription)
size: Large
---
# Unify Recordings & File Transcription

## Description

Merge the separate "Transcriptions" page into the Recordings page so that imported audio files become first-class recording entries. The Transcriptions nav item and TranscribeFilesPage go away entirely.

## Acceptance Criteria

### UI Changes — Recordings Page
- [x] Two buttons below the hotkey subline ("Press Ctrl+Win+R to start/stop"):
  - **Start Recording / Stop Recording** — toggles based on current recording state
  - **Browse** — opens a file picker for importing audio files (.ogg, .mp3, .m4a, .wav)
- [x] Remove the "Transcriptions" navigation item from the sidebar

### File Import Flow
- [x] On file selection, **move** the file into a new session directory under `recordings/` (format: `YYYYMMDD_HHmmss/`)
  - If move fails (cross-drive, network share), fall back to **copy** (do not delete the original)
- [x] Create the session directory and place the audio file inside it (as the single audio file, no mic/system split)
- [x] Create a `transcript.json` stub or mark it as pending so it appears in the recordings list immediately
- [x] Enqueue the file for transcription via the existing `TranscriptionQueueService`

### Transcription Behavior
- [x] Default: **no diarization** for imported files (single-speaker, flat transcription)
- [x] Use the existing `FileTranscriptionService` pipeline for the initial transcription
- [x] Produce a `CallTranscript`-compatible `transcript.json` with:
  - Title derived from the original filename
  - Duration from audio file metadata
  - Date from file import time
  - Single speaker segments (no speaker labels needed for single-speaker mode)
  - `AudioFilePath` pointing to the moved/copied audio file

### In-List Appearance
- [x] While transcribing: shown like a recording in progress — title in the row, duration and date visible, "Transcribing..." state indicator
- [x] Once complete: indistinguishable from a call recording entry — title, duration, date, and transcript shown in the drawer on click

### Drawer — Re-transcribe with Diarization
- [x] The existing re-transcribe button in the drawer should work for imported files
- [x] If the user has defined more than one speaker (via speaker name editing), re-transcription triggers the **call transcription pipeline with diarization**
- [x] Single-speaker re-transcription stays on the flat pipeline

### Cleanup
- [x] Remove `TranscribeFilesPage.xaml` and `TranscribeFilesPage.xaml.cs`
- [x] Remove `TranscribeFilesViewModel` and `TranscriptionItemViewModel`
- [x] Remove the "Transcriptions" case from MainWindow navigation
- [x] Remove or repurpose `FileTranscriptionService` (keep if still used by the queue for single-file transcription, remove if fully replaced)
- [x] Clean up any dead references in DI registration / MainWindow

## Technical Notes

- The `CallTranscriptionPipeline` expects dual streams (mic + system). For imported single-file diarization, the file should be fed as the system/loopback stream with no mic stream, so diarization treats all speakers as "remote."
- The `TranscriptionQueueService` already supports both call and file items — the key change is that file items now produce `CallTranscript` JSON instead of ephemeral text results.
- Move semantics: use `File.Move` first, catch `IOException`, fall back to `File.Copy`.

## Open Questions

- None — all clarified during capture.

## Work Log

### 2026-03-25

**All acceptance criteria met.** Build succeeds (0 errors), all 32 tests pass.

#### Changes Made

1. **TranscriptsPage.xaml** — Added "Start Recording / Stop Recording" and "Browse" buttons below the hotkey subline in the header area. Updated empty state text.

2. **TranscriptsPage.xaml.cs** — Added `IFileTranscriptionService` dependency. Implemented:
   - `StartStopRecording_Click` / `UpdateRecordingButtonState` for toggling recording
   - `BrowseFiles_Click` / `ImportAudioFile` for the file import flow (move with copy fallback, session dir creation, queue enqueue)
   - Updated `PendingRow_Click` to handle imported audio files (not just mic.wav)
   - Updated `ReTranscribe_Click` to support imported files: single-speaker re-transcription uses flat pipeline; multi-speaker uses diarization pipeline feeding the file as system stream
   - Added `FindImportedAudioFile` and `CountAudioFiles` helpers

3. **TranscriptionQueueService.cs** — Added `EnqueueFileImport(title, filePath, sessionDir)` method. Added `SessionDir` property to `TranscriptionQueueItem`. Updated `ProcessFileItem` to save a `CallTranscript`-compatible `transcript.json` with title, duration, date, single-speaker segment, and audio file path when processing an imported file. Updated `Retry` to preserve session dir.

4. **TranscriptStorageService.cs** — Updated `ListPendingSessions` to detect non-WAV audio files (.ogg, .mp3, .m4a) in addition to .wav.

5. **MainWindow.xaml** — Removed the "Transcriptions" nav item from the sidebar.

6. **MainWindow.xaml.cs** — Removed "Transcriptions" case from `NavigateTo`. Updated `GetOrCreateTranscriptsPage` to pass `IFileTranscriptionService`. Removed `NavLabelTranscriptions` reference from sidebar collapse logic.

7. **Deleted**: `TranscribeFilesPage.xaml`, `TranscribeFilesPage.xaml.cs` (contained `TranscribeFilesViewModel` and `TranscriptionItemViewModel`)

8. **Kept**: `FileTranscriptionService` — still used by `TranscriptionQueueService` for single-file transcription.

#### Files Changed
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml`
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`
- `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs`
- `src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs`
- `src/WhisperHeim/MainWindow.xaml`
- `src/WhisperHeim/MainWindow.xaml.cs`
- `src/WhisperHeim/Views/Pages/TranscribeFilesPage.xaml` (deleted)
- `src/WhisperHeim/Views/Pages/TranscribeFilesPage.xaml.cs` (deleted)
