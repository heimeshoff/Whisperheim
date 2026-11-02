---
id: main-095
title: Unified Recording & Transcript Drawer
status: done
type: feature
context: main
created: 2026-04-01
completed: 2026-04-01
commit:
depends_on: [main-076]
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: --
size: Medium
---
# Unified Recording & Transcript Drawer

## Description

Unify the active-recording drawer and the transcript drawer into a single drawer that transitions live from "recording" state to "transcribed" state. The current active-recording drawer is visually present but does not accept mouse clicks or keyboard input — this is the primary bug to fix.

## Problem

- The active-recording drawer (opened by clicking the recording card) shows a timer but **does not accept any user input** — title and speaker name fields are non-interactive
- There are two separate drawer content sections (`ActiveRecordingDrawerContent` and `TranscriptDrawerContent`) with different layouts and behaviors
- The +/- speaker counter is unnecessary; speaker count should be inferred from the list of names entered

## Requirements

### 1. Fix input in active-recording drawer

The drawer must accept keyboard and mouse input so the user can type a session title and speaker names while recording is in progress. Diagnose why the current fields are non-interactive (likely a hit-test, focus, or overlay issue) and fix it.

### 2. Replace speaker counter with speaker name list

Remove the +/- increment/decrement buttons for speaker count. Instead, use the same speaker-name editing UX as the transcript drawer:
- An "Add speaker" button that appends a new empty name field
- A remove (x) button per speaker entry
- Speaker count is inferred from the number of names in the list

### 3. Unified drawer layout

Use a single drawer layout that adapts based on session state:

**While recording:**
- Header with close button and editable title field
- Recording duration (live timer)
- Speaker names panel (add/remove names)
- **Hidden:** Playback panel, transcript segments, analysis panel, action buttons (delete, export, copy, analyze), RE-TRANSCRIBE button

**After transcription completes:**
- Same drawer, same instance — no close/reopen needed
- Live-update to reveal: playback panel, transcript segments, analysis panel, action buttons
- RE-TRANSCRIBE button appears, using the speaker names already entered for diarization
- All export and delete functionality becomes available

### 4. Live state transition

When the recording stops and transcription completes:
- The drawer stays open (no close/reopen)
- The recording timer stops and is replaced by the completed duration
- Newly available sections animate or fade in
- The title and speaker names entered during recording carry over seamlessly into the completed transcript

### 5. Controls visibility by state

| Control | Recording | Transcribed |
|---|---|---|
| Title (editable) | Yes | Yes |
| Recording timer | Yes | No (show duration) |
| Speaker names (add/remove) | Yes | Yes |
| Playback panel | No | Yes (if audio exists) |
| RE-TRANSCRIBE button | No | Yes |
| Transcript segments | No | Yes |
| Analysis panel | No | Yes |
| DELETE button | No | Yes |
| COPY / MD / JSON buttons | No | Yes |
| ANALYZE button | No | Yes |
| Delete Audio button | No | Yes (if audio exists) |

## Acceptance Criteria

- [ ] Clicking the active recording card opens the drawer and I can immediately type a title and add speaker names
- [ ] Speaker names use add/remove list (no +/- counter)
- [ ] When transcription completes, the drawer live-updates to show playback, segments, and all action buttons without closing
- [ ] Speaker names entered during recording are used for diarization
- [ ] RE-TRANSCRIBE button appears only after transcription is complete and uses the current speaker list
- [ ] No delete, play, export, or analyze buttons visible while recording is in progress

## Key Files

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — Drawer XAML (lines 491-1076)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — Drawer code-behind
- `src/WhisperHeim/Services/Recording/CallRecordingSession.cs` — Recording session model

## Work Log

### 2026-04-01 — Implementation complete

**Root cause of non-interactive input:** The `ActiveRecordingDrawerContent` StackPanel was positioned at Grid.Row="0" with RowSpan="6", but other grid children defined later in the XAML (particularly `TranscriptScrollViewer` at Grid.Row="3") had higher z-order and intercepted mouse/keyboard input even when their content was empty.

**Changes made:**

1. **Removed `ActiveRecordingDrawerContent` StackPanel entirely** — eliminated the separate overlay panel that caused the input-blocking z-order issue.

2. **Unified the drawer** — the existing `TranscriptDrawerContent` header now serves both states. Added `RecordingIndicatorPanel` (recording dot + "RECORDING IN PROGRESS" label) and `DrawerRecordingDuration` timer inside the header, shown/hidden based on state.

3. **Replaced speaker counter with add/remove list** — removed the +/- increment/decrement buttons (`IncrementSpeakerCount_Click`, `DecrementSpeakerCount_Click`, `SyncActiveSpeakerNameSlots`, `_activeRecordingSpeakerCount`). The active recording drawer now uses the same `SpeakerNamesPanel` with "Add" and "x" remove buttons.

4. **Live state transition** — `OnRecordingStopped` no longer closes the drawer. Instead it hides recording-specific UI elements and shows a "transcription queued" message. `RefreshList` now calls `TryAutoOpenTranscriptInDrawer()` which auto-opens the completed transcript in the already-open drawer when transcription finishes.

5. **Controls visibility by state** — during recording: title editable, timer shown, speaker names panel shown, RE-TRANSCRIBE/playback/action/analysis panels hidden. After transcription: timer hidden, all transcript controls shown.

**Files changed:** `TranscriptsPage.xaml`, `TranscriptsPage.xaml.cs`
