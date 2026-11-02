---
id: main-076
title: Active Recording Card + Auto-Transcribe on Stop
status: done
type: feature
context: main
created: 2026-03-25
completed: 2026-03-25
commit:
depends_on: [main-075, main-073]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# Active Recording Card + Auto-Transcribe on Stop

## Objective
Show the active recording session as a card in the Transcripts page, allow editing metadata (title, speaker count, speaker names) during recording, and auto-enqueue for transcription when recording stops.

## Details

### Active Recording Card
- When a recording is in progress, show it as a distinct card at the top of the Transcripts list (above pending and completed transcripts)
- Visual distinction from pending/completed items (e.g., pulsing recording indicator, red accent, live duration counter)
- Clicking the active recording card opens a detail view (in the drawer) where the user can:
  - Set/edit the session title
  - Set the number of remote speakers
  - Enter names for each remote speaker
- Clicking the card does NOT start transcription (unlike pending items)
- The tray icon click during recording should open the main window normally, allowing navigation to the Transcripts page to see and edit the active recording

### Auto-Transcribe on Recording Stop
- When the user stops a recording (via tray icon or hotkey), the recording session is automatically enqueued into the transcription queue (task 075)
- The queue item carries the metadata (title, speaker names, speaker count) that was entered during recording
- If no speaker names were provided, the queue item still processes with auto-detect fallback
- No manual "Transcribe" click needed -- stopping the recording is the trigger

### Pending Recordings Cleanup
- Existing "pending" recordings (recorded but not yet transcribed) should also be enqueueable via the queue
- The manual transcription trigger on pending items should enqueue into the queue rather than launching a modal dialog

## Acceptance Criteria
- [x] Active recording session visible as a card in Transcripts page
- [x] Card shows live duration, recording indicator, and session metadata
- [x] Clicking active card opens drawer with editable title, speaker count, and speaker names
- [x] Clicking active card does NOT trigger transcription
- [x] Stopping a recording auto-enqueues it for transcription with pre-filled metadata
- [x] Pending recordings can still be manually enqueued
- [x] Tray icon click during recording opens the main window normally

## Notes
- Task 073 already added speaker name list and manual transcription trigger -- build on that work
- The speaker name count implicitly sets `NumClusters` for diarization (task 077)
- Research file: `.workflow/research/transcription-engine-overhaul.md`

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-25 — Implementation Complete

**What was done:**
1. Added active recording card to TranscriptsPage XAML — red-accented card at top of list with pulsing recording dot, live duration counter, and subtitle showing metadata status.
2. Added active recording drawer — clicking the card opens a side drawer with editable session title, remote speaker count (increment/decrement), and speaker name text fields. Does NOT trigger transcription.
3. Implemented auto-enqueue on recording stop — when recording stops, the session is automatically enqueued into TranscriptionQueueService with any pre-filled metadata (title, speaker names).
4. Passed ICallRecordingService to TranscriptsPage constructor and subscribed to RecordingStarted/RecordingStopped events.
5. Updated MainWindow to pass recording service to TranscriptsPage and eagerly create the page so it receives events even before navigation.
6. Pending recordings remain manually enqueueable via existing PendingRow_Click and ReTranscribe_Click handlers (unchanged).
7. Tray icon click continues to open the main window normally (no changes needed).

**All acceptance criteria met.** Build succeeds with 0 errors.

**Files changed:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — Active recording card + active recording drawer content
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — Recording service integration, active card/drawer logic, auto-enqueue on stop
- `src/WhisperHeim/MainWindow.xaml.cs` — Pass recording service to TranscriptsPage, eager page creation
