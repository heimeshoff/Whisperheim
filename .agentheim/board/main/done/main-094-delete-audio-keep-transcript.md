---
id: main-094
title: Delete Audio Files While Keeping Transcript
status: done
type: feature
context: main
created: 2026-03-31
completed: 2026-03-31
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: --
size: Medium
---
# Delete Audio Files While Keeping Transcript

## Description

Add the ability to delete WAV audio files from a recording while preserving the full transcript, speaker names, timestamps, and all other metadata. This lets users reclaim disk space after transcription without losing any transcript data.

## Changes Required

### 1. UI: Show file size in PlaybackPanel

In `TranscriptsPage.xaml`, extend the `PlaybackPanel` grid (line ~651) to show the total size of WAV files in MB next to the playback duration (`PlaybackPositionText`). Format: `12.3 MB`.

Calculate total size by summing `mic.wav` + `system.wav` (or the single combined audio file referenced by `audioFilePath`).

### 2. UI: Add red "Delete Audio" button in PlaybackPanel

Add a red delete button next to the file size display, visually consistent with the existing delete button style (`#FFE74856`). This button lives **inside** the `PlaybackPanel`, not in the `ActionPanel` at the bottom.

### 3. Delete Audio logic

On click:
1. Show a confirmation dialog (similar to the existing `DrawerDeleteTranscript_Click` confirmation)
2. Stop audio playback if playing
3. Delete WAV files: `mic.wav`, `system.wav`, and any combined audio file referenced by `audioFilePath`
4. Clear `audioFilePath` in the transcript JSON (set to null/empty) and save via `TranscriptStorageService.UpdateAsync`
5. Hide the `PlaybackPanel` (same behavior as when no audio files exist)
6. The recording remains in the conversation list — only audio is removed

### 4. PlaybackPanel visibility

The existing `DisplayTranscript` method (line ~939 in code-behind) already hides `PlaybackPanel` when audio files don't exist. After deleting audio, trigger the same visibility logic so the panel disappears. On future loads, the panel should also remain hidden since the files are gone.

### 5. Disable guard on Delete Audio button

The delete-audio button must be **disabled** when no transcription has been made (i.e., the transcript has no segments or segments list is empty/null). This prevents users from accidentally deleting audio before it has been transcribed.

### 6. Bottom delete button unaffected

The existing delete button in `ActionPanel` (bottom-left) continues to work exactly as before — deleting the entire session directory including transcript. It has no new constraints.

## Key Files

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — PlaybackPanel UI (lines 651-700), ActionPanel (lines 913-1030)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — DisplayTranscript (line ~939), delete handler (line ~1049), playback controls
- `src/WhisperHeim/Services/CallTranscription/CallTranscript.cs` — Data model, `AudioFilePath`, `ResolvedAudioFilePath`, `ResolvedSourceAudioPaths`
- `src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs` — `UpdateAsync`, `DeleteSession`

## Acceptance Criteria

- [x] WAV file total size (MB) is displayed next to the playback duration in the drawer
- [x] Red delete-audio button appears next to the file size
- [x] Clicking delete-audio shows a confirmation dialog
- [x] On confirmation: WAV files are deleted, transcript JSON is updated, PlaybackPanel hides
- [x] Transcript, speaker names, timestamps, duration, segment count all preserved after audio deletion
- [x] Recording still appears in conversation list after audio deletion
- [x] Delete-audio button is disabled when no transcription segments exist
- [x] Bottom delete button (full session delete) is unaffected and has no new constraints
- [x] On reopening a recording where audio was previously deleted, PlaybackPanel is hidden

## Work Log

**2026-03-31** — Implemented delete-audio-keep-transcript feature.

### Changes Made

**TranscriptsPage.xaml**: Extended PlaybackPanel grid with two new columns — `AudioFileSizeText` (TextBlock showing total WAV size in MB) and `DeleteAudioButton` (red styled button matching existing delete style with disabled opacity trigger). Updated `OpenExternalButton` ColumnSpan to 6.

**TranscriptsPage.xaml.cs**: Added `DeleteAudio_Click` handler that shows confirmation dialog, stops playback, deletes mic.wav/system.wav/combined audio files, clears `AudioFilePath` in transcript JSON via `UpdateAsync`, and hides `PlaybackPanel`. Added `GetAudioFileTotalSize` helper method. Updated `DisplayTranscript` to show file size and enable/disable the delete-audio button based on whether segments exist.

### Acceptance Criteria Status
All 9 acceptance criteria met. Build succeeds with 0 errors.

### Files Changed
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml`
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`
