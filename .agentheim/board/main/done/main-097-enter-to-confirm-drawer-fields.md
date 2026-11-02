---
id: main-097
title: Enter-to-Confirm in Drawer Text Fields
status: done
type: feature
context: main
created: 2026-04-07
completed: 2026-04-07
commit:
depends_on: [main-095]
blocks: []
tags: [m2]
related_adrs: []
related_research: []
prior_art: []
milestone: M2
size: Small
---
# Enter-to-Confirm in Drawer Text Fields

## Description

When editing text fields in the recording/transcription drawer (transcript name and speaker names), pressing Enter should commit the value and dismiss the cursor. Currently, pressing Enter does nothing -- the name only updates in the list when the drawer is closed.

## Requirements

### 1. Transcript name field -- Enter to confirm

- Pressing Enter in the transcript name TextBox dismisses focus (cursor disappears)
- The updated name is immediately reflected in the corresponding list entry (the red recording card or pending item)
- No drawer close/reopen required

### 2. Speaker name fields -- Enter to confirm

- Pressing Enter in a speaker name TextBox dismisses focus (cursor disappears)
- The name is committed to the speaker list
- Clicking the name again re-enters edit mode (focus + cursor appears)

### 3. Consistent behavior across drawer states

- This Enter-to-confirm behavior applies in all drawer contexts: active recording, pending transcription, and completed transcript

## Key Files

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` -- drawer XAML with TextBox definitions (~lines 492-915)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` -- drawer logic, name/speaker handling

## Acceptance Criteria

- [ ] Pressing Enter in the transcript name field removes focus and updates the list entry name immediately
- [ ] Pressing Enter in a speaker name field removes focus and commits the name
- [ ] Clicking a committed speaker name re-enters edit mode
- [ ] Works in recording drawer, pending drawer, and completed transcript drawer
- [ ] Pressing Escape also dismisses focus without committing (if feasible)

## Work Log

### 2026-04-07

**Changes made to `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`:**

1. **TranscriptNameBox_KeyDown**: Added `Keyboard.ClearFocus()` after saving on Enter, so focus is dismissed and the cursor disappears. Also updates the active recording card title immediately when in recording mode. Added Escape key handling to revert the name and dismiss focus.

2. **SaveTranscriptNameAsync**: Added handling for the active recording case (when `_selectedTranscript` is null but `_isActiveRecordingDrawerOpen` is true), so pressing Enter during an active recording saves the title to `_activeRecordingTitle`.

3. **SpeakerNameList_KeyDown**: Added Escape key handling to dismiss focus without committing the speaker name.

**Acceptance Criteria Status:**
- [x] Pressing Enter in the transcript name field removes focus and updates the list entry name immediately
- [x] Pressing Enter in a speaker name field removes focus and commits the name (was already partially working, now fully consistent)
- [x] Clicking a committed speaker name re-enters edit mode (existing behavior, unchanged)
- [x] Works in recording drawer, pending drawer, and completed transcript drawer
- [x] Pressing Escape also dismisses focus without committing

**Files changed:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`
