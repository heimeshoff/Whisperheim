---
id: main-062
title: TTS Page Layout Cleanup
status: done
type: feature
context: main
created: 
completed: 
commit:
depends_on: []
blocks: []
tags: [ui-polish]
related_adrs: []
related_research: []
prior_art: []
milestone: UI Polish
size: Small
---
# TTS Page Layout Cleanup

## Description

Clean up the Text to Speech page layout with two changes:

1. **Remove the "INPUT WORKSPACE" label** that sits above the text input area. The text input itself stays.
2. **Move the voice/speaker selector** (ComboBox with Person icon) from its current position in the card header row to **below the play/stop buttons, left-aligned** with them.

## Acceptance Criteria

- [x] The "INPUT WORKSPACE" label is removed from the TTS card header
- [x] The voice selector ComboBox (with Person icon) is positioned below the play/stop controls row, left-aligned with the play button
- [x] The voice selector retains its current style and functionality
- [x] Text input area remains fully functional
- [x] Play/stop/save controls are unaffected

## Files to Modify

- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml` — layout changes only

## Notes

- Straightforward XAML-only change, no code-behind or ViewModel modifications expected

## Work Log

- Removed the "INPUT WORKSPACE" label and its containing DockPanel header row from the TTS card
- Moved the voice selector (Person icon + ComboBox) from the removed header row to a new StackPanel below the controls row (play/stop/save buttons), left-aligned
- Build verified: 0 errors, 0 warnings
- All acceptance criteria met

**Files changed:**
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml`
