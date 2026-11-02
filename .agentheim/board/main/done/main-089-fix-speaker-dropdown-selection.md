---
id: main-089
title: Fix speaker dropdown selection not applying
status: done
type: bug
context: main
created: 2026-03-26
completed: 2026-03-26
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Small
---
# Fix speaker dropdown selection not applying

## Objective

Fix the speaker name ComboBox in the transcript editor so that selecting a registered speaker name from the dropdown actually applies the change instead of reverting to the original "Speaker 1" / "Speaker 2" label.

## Details

### Bug Description

When editing a recorded conversation's transcript:
1. User clicks a speaker label (e.g., "Speaker 1") to edit it
2. ComboBox appears with dropdown showing registered speaker names
3. User clicks a name from the dropdown (e.g., "Alice")
4. Dropdown closes, but the label reverts to "Speaker 1"

This happens 100% of the time for both normal click and Shift+click.

### Root Cause Analysis

Likely a race condition between `LostFocus` and `SelectionChanged` events on the editable ComboBox (possibly exacerbated by Wpf.Ui styling):

1. When the user clicks a dropdown item, the dropdown closes
2. `LostFocus` fires on the ComboBox **before** `SelectionChanged` processes
3. `LostFocus` calls `CommitSpeakerEditAsync` with `EditingSpeakerName` still at the old value ("Speaker 1")
4. Since `newName == currentDisplay`, the edit is cancelled via `CancelEditSpeaker()` (line 1332-1335)
5. `SelectionChanged` fires next but `IsEditingSpeaker` is already `false` → early return (line 1270-1271)

### Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `TranscriptsPage.xaml` | 790-803 | ComboBox definition (IsEditable, Text binding, event handlers) |
| `TranscriptsPage.xaml.cs` | 1265-1283 | `SpeakerComboBox_SelectionChanged` handler |
| `TranscriptsPage.xaml.cs` | 1299-1305 | `SpeakerEditBox_LostFocus` handler |
| `TranscriptsPage.xaml.cs` | 1324-1400 | `CommitSpeakerEditAsync` (core commit logic) |
| `TranscriptsPage.xaml.cs` | 2042-2057 | `BeginEditSpeaker` / `CommitEditSpeaker` / `CancelEditSpeaker` |

### Fix Approach

Options (pick simplest that works):
- **A)** In `LostFocus`, add a short dispatcher delay to let `SelectionChanged` fire first
- **B)** Track a `_selectionJustChanged` flag in `SelectionChanged` and skip `LostFocus` commit when set
- **C)** Remove `LostFocus` commit and handle all commit paths explicitly (SelectionChanged, Enter key, click-away detection)

Add trace logging to confirm the event ordering before applying the fix.

## Acceptance Criteria

- [x] Selecting a speaker name from the dropdown applies the change immediately
- [x] The change persists (saved to disk via `UpdateAsync`)
- [x] Both normal click (global rename) and Shift+click (per-segment) work
- [x] Manual typing + Enter still works
- [x] Pressing Escape still cancels the edit

## Work Log

### 2026-03-26

**Fix applied:** Combined approaches A and B from the task description.

**Root cause confirmed:** When clicking a dropdown item, `LostFocus` fires before `SelectionChanged`. The `LostFocus` handler called `CommitSpeakerEditAsync` with the old `EditingSpeakerName` (still "Speaker 1"), which matched `currentDisplay`, triggering `CancelEditSpeaker()`. By the time `SelectionChanged` fired, `IsEditingSpeaker` was already `false`, so it early-returned.

**Changes to `TranscriptsPage.xaml.cs`:**
1. Added `_speakerSelectionCommitted` flag field.
2. In `SpeakerComboBox_SelectionChanged`: set `_speakerSelectionCommitted = true` before committing.
3. In `SpeakerEditBox_LostFocus`: deferred the commit via `Dispatcher.BeginInvoke` at `Background` priority so `SelectionChanged` processes first. If the flag is set, LostFocus skips the commit (already handled). Added trace logging for the skip path.

**Verification:** No existing behavior changed for Enter key, Escape, or click-away (LostFocus commit still fires when no dropdown selection was made). Build succeeds with 0 errors.
