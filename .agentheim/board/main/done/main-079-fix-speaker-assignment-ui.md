---
id: main-079
title: Fix Speaker Assignment UI -- ComboBox Bug + Per-Segment Reassignment
status: done
type: bug
context: main
created: 2026-03-25
completed: 2026-03-25
commit:
depends_on: [main-077]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# Fix Speaker Assignment UI -- ComboBox Bug + Per-Segment Reassignment

## Objective
Fix the broken speaker assignment combo boxes and enable flexible per-segment speaker reassignment in the transcript detail view.

## Details

### Bug Fix: ComboBox Closes Immediately
The speaker ComboBox in the transcript detail drawer closes immediately when clicked. Root cause: the ComboBox is nested inside a Border with a `MouseLeftButtonDown` handler (`Segment_MouseLeftButtonDown`) that fires audio playback. The click event bubbles from the ComboBox up to the Border, stealing the click.

**Fix:** Prevent the click event from bubbling when interacting with the ComboBox. Add a `PreviewMouseLeftButtonDown` handler on the ComboBox (or its container) that marks the event as handled when the ComboBox is in editing/dropdown mode.

### Per-Segment Speaker Reassignment
- Initial speaker assignment: names mapped to cluster IDs by order of first appearance (from task 077)
- Any individual transcript segment should be reassignable to a different speaker
- The ComboBox dropdown should list all available speaker names (from the session's speaker list + any auto-detected cluster IDs)
- Changing a segment's speaker should persist immediately (update the transcript JSON)
- Consider: "Apply to all segments with this speaker ID" option -- if the user reassigns one segment from "Speaker 1" to "Alice", offer to reassign all "Speaker 1" segments to "Alice"

### Speaker Name List
- The available speaker names come from: (a) names provided before/during recording (task 076), (b) auto-detected cluster IDs as fallback ("Speaker 1", "Speaker 2", etc.)
- User should be able to add new speaker names after transcription (e.g., they forgot to add someone before recording)
- Editing a speaker name in the header should propagate to all segments attributed to that speaker

## Acceptance Criteria
- [ ] Speaker ComboBox opens and stays open when clicked (bug fixed)
- [ ] Clicking a ComboBox does not trigger audio playback
- [ ] Any segment's speaker can be reassigned via the ComboBox dropdown
- [ ] Speaker reassignment persists to transcript JSON
- [ ] "Apply to all" option when reassigning a speaker (bulk update)
- [ ] New speaker names can be added after transcription
- [ ] Editing a speaker name in the header updates all segments with that speaker

## Notes
- Bug location: `TranscriptsPage.xaml` lines 493-520, handler at `TranscriptsPage.xaml.cs` line 594
- The `Segment_MouseLeftButtonDown` handler on the parent Border intercepts clicks meant for the ComboBox
- Research file: `.workflow/research/transcription-engine-overhaul.md`

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-25 - Implementation Complete

**Changes made:**

1. **Bug fix: ComboBox closes immediately** - Added `IsInsideInteractiveControl()` helper that walks the visual tree from the click source; if it finds a ComboBox, TextBox, or ToggleButton before reaching the segment Border, the `Segment_MouseLeftButtonDown` handler returns early without triggering audio playback. Also added `PreviewMouseLeftButtonDown` on the ComboBox itself that marks the event as handled and re-opens the dropdown via dispatcher to prevent event bubbling.

2. **Per-segment speaker reassignment via ComboBox** - Added `SelectionChanged` handler on the speaker ComboBox that commits the edit immediately when a name is selected from the dropdown. The existing LostFocus/KeyDown handlers continue to work for typed-in names.

3. **"Apply to all" prompt** - After a per-segment rename (Shift+Click), if other segments share the same original speaker name, a MessageBox prompt asks whether to apply the rename to all matching segments. If confirmed, uses `RenameSpeakerGlobally` to update all.

4. **Speaker name header editing propagates to segments** - Enhanced `SpeakerNameItem` with `PreviousName` tracking. When a speaker name is edited in the header panel and focus is lost, `SaveSpeakerNames` detects the rename, updates the `SpeakerNameMap` entries and per-segment overrides, and refreshes all segment display names.

5. **Add new speaker names after transcription** - Already supported via the existing "+ Add" button in the speaker names panel.

**Acceptance Criteria:**
- [x] Speaker ComboBox opens and stays open when clicked (bug fixed)
- [x] Clicking a ComboBox does not trigger audio playback
- [x] Any segment's speaker can be reassigned via the ComboBox dropdown
- [x] Speaker reassignment persists to transcript JSON
- [x] "Apply to all" option when reassigning a speaker (bulk update)
- [x] New speaker names can be added after transcription
- [x] Editing a speaker name in the header updates all segments with that speaker

**Files changed:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` - Added PreviewMouseLeftButtonDown and SelectionChanged handlers on speaker ComboBox
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` - Added IsInsideInteractiveControl, SpeakerComboBox_PreviewMouseLeftButtonDown, SpeakerComboBox_SelectionChanged; enhanced Segment_MouseLeftButtonDown, CommitSpeakerEditAsync, SaveSpeakerNames, SpeakerNameItem
