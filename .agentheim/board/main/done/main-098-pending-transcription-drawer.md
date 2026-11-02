---
id: main-098
title: Pending Transcription Drawer with Playback
status: done
type: feature
context: main
created: 2026-04-07
completed: 2026-04-07
commit:
depends_on: [main-095, main-097]
blocks: []
tags: [m2]
related_adrs: []
related_research: []
prior_art: []
milestone: M2
size: Medium
---
# Pending Transcription Drawer with Playback

## Description

Clicking a pending (not-yet-transcribed) recording opens an empty drawer. It should open the same drawer UI as active recordings, with full editing capabilities and audio playback with scrubbing.

## Problem

- `OpenPendingTranscribingDrawer()` opens the drawer but the content is empty/non-functional
- No way to edit the name or add speakers before transcription
- No way to listen to the recording to remember what it was about
- Clicking a pending item currently auto-enqueues it for transcription (wrong behavior -- see task 099)

## Requirements

### 1. Pending drawer shows full editing UI

When clicking a pending transcription item, the drawer should show:
- Editable transcript name field
- Speaker name list with add/remove (same UX as recording drawer)
- All fields pre-populated if the user already entered data during recording

### 2. Audio playback with scrubbing

- Play/pause button for the recorded audio
- Seek bar / scrubber so the user can jump to any point in the recording
- Current position and total duration display
- Uses existing `TranscriptAudioPlayer` service

### 3. Explicit "Queue Transcription" button

- A prominent button at the bottom of the pending drawer: "Transcribe" or "Queue Transcription"
- Clicking it enqueues the session for transcription (with the edited name and speakers)
- Clicking a pending item in the list should ONLY open the drawer, NOT auto-enqueue

### 4. Drawer state management

- Pending drawer is visually distinct from active recording (no recording timer, no red indicator)
- After clicking "Queue Transcription", the drawer can close or transition to show queued status

## Key Files

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` -- `OpenPendingTranscribingDrawer()` (~line 353), `PendingRow_Click()` (~line 782)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` -- drawer XAML layout
- `src/WhisperHeim/Services/Audio/TranscriptAudioPlayer.cs` -- existing playback service (supports seek via `CurrentPosition` property)

## Acceptance Criteria

- [ ] Clicking a pending item opens the drawer with name field, speaker list, and playback controls
- [ ] Name and speakers are pre-populated from recording session data
- [ ] Audio playback works with play/pause, seek bar, and position display
- [ ] "Queue Transcription" button enqueues the session with current name/speakers
- [ ] Clicking a pending item does NOT auto-start transcription
- [ ] Drawer works correctly for multiple pending items (open one, close, open another)

## Work Log

### 2026-04-07 - Implementation Complete

**Changes made:**

1. **Pending drawer shows full editing UI** - Rewrote `OpenPendingTranscribingDrawer()` to display editable transcript name, speaker name list (add/remove), and status info. Name and speakers are pre-populated from `transcript_name.json` if it exists in the session directory.

2. **Audio playback with seek bar** - Added a `Slider` control (seek bar/scrubber) to the playback panel in XAML. Wired up `SeekBar_PreviewMouseDown/Up` and `SeekBar_ValueChanged` handlers for drag-to-seek. The seek bar updates automatically during playback via `UpdateSeekBar()` called from `OnAudioPositionChanged`. Audio files (mic.wav/system.wav or imported audio) are loaded for inline playback in the pending drawer.

3. **Explicit "Queue Transcription" button** - Added a prominent styled button at the bottom of the drawer (Grid.Row="5"). `QueueTranscription_Click()` saves metadata, then enqueues via `TranscriptionRequested` (for call recordings) or `_queueService.EnqueueFileImport()` (for imported files), applying edited name and speakers.

4. **No auto-enqueue on click** - Simplified `PendingRow_Click()` to only open the drawer without enqueuing. The old auto-enqueue logic was moved into `QueueTranscription_Click()`.

5. **Metadata persistence** - Added `transcript_name.json` file in session directories to persist edited name and speaker names between drawer opens. `LoadPendingSessions()` reads saved names to show them in the list.

6. **State management** - Added `_isPendingDrawerOpen`, `_pendingDrawerItem`, `_pendingDrawerSpeakerNames`, `_pendingDrawerTitle`, `_isSeekBarDragging` fields. Updated `CloseDrawer()`, `OpenActiveRecordingDrawer()`, `OpenTranscriptDrawer()`, `SaveTranscriptNameAsync()`, `TranscriptNameBox_KeyDown()`, `AddSpeaker_Click()`, `RemoveSpeaker_Click()`, `SpeakerNameList_KeyDown()`, `SpeakerName_LostFocus()` to handle pending drawer state.

**Acceptance Criteria:**
- [x] Clicking a pending item opens the drawer with name field, speaker list, and playback controls
- [x] Name and speakers are pre-populated from recording session data
- [x] Audio playback works with play/pause, seek bar, and position display
- [x] "Queue Transcription" button enqueues the session with current name/speakers
- [x] Clicking a pending item does NOT auto-start transcription
- [x] Drawer works correctly for multiple pending items (open one, close, open another)

**Files changed:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` - Added seek slider to playback panel, added Queue Transcription button panel
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` - Rewrote pending drawer logic, added seek bar handlers, queue button handler, metadata persistence

**Build:** 0 errors, pre-existing warnings only
**Tests:** 32/32 passed
