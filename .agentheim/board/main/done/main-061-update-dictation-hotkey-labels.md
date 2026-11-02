---
id: main-061
title: Update Dictation Page Hotkey Labels
status: done
type: feature
context: main
created: 2026-03-22
completed: 2026-03-22
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Small
---
# Update Dictation Page Hotkey Labels

## Description
Update the hotkey labels on the dictation page to match the actual registered shortcuts:
- "Start/Stop" → "Dictation" (Ctrl+Win)
- "Read Aloud" shortcut: Shift+Win+A → Ctrl+Win+^
- "Call Recording" (Ctrl+Win+R) — unchanged

## Acceptance Criteria
- [x] Dictation page shows "Dictation" instead of "Start/Stop"
- [x] Read Aloud shows Ctrl+Win+^ instead of Shift+Win+A
- [x] Call Recording remains Ctrl+Win+R
- [x] Visual appearance unchanged (just label/key text updates)

## Files Modified
- `src/WhisperHeim/Views/Pages/DictationPage.xaml` — hotkey card section

## Work Log
- **2026-03-22:** Verified that all hotkey labels in `DictationPage.xaml` already match the acceptance criteria: label is "Dictation" (not "Start/Stop"), Read Aloud shows Ctrl+Win+^, Call Recording shows Ctrl+Win+R. No old "Start/Stop" or "Shift+Win+A" references exist anywhere in the codebase. All acceptance criteria met — no code changes required.
- **Files changed:** 0 source files (task file updated only)
