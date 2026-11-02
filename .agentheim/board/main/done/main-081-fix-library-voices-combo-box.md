---
id: main-081
title: Fix Library Voices Not Showing in TTS Combo Box
status: done
type: bug
context: main
created: 
completed: 2026-03-25
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: Bug Fix
size: Small
---
# Fix Library Voices Not Showing in TTS Combo Box

## Description

Custom cloned voices appear in the "Library Voices" sidebar list but not in the voice selection combo box on the TTS page. Root cause: path mismatch when a custom data path is configured.

`TextToSpeechPage.xaml.cs` has a hardcoded `CustomVoicesDir` pointing to `%APPDATA%\WhisperHeim\voices`. When a custom data path is configured via `DataPathService`, the TTS service's `CustomVoicesDir` gets updated to `{DataPath}/voices` via `Initialize()`, but the page's field does not.

**Result:**
- Voice cloning saves to `%APPDATA%\WhisperHeim\voices` (page's hardcoded path)
- Sidebar list reads from `%APPDATA%\WhisperHeim\voices` (same hardcoded path -- works)
- Combo box reads from `{DataPath}/voices` via `_ttsService.GetAvailableVoices()` (different path -- doesn't find them)

**Secondary bug:** `LibraryVoice_Click` compares `item.VoiceId == voiceName` but the combo box stores IDs as `"custom:{name}"` while the card's `Tag` is just `name`, so clicking a library voice card to select it in the combo box also fails.

## Acceptance Criteria

- [ ] Custom cloned voices appear in the combo box when a custom data path is configured
- [ ] Custom cloned voices appear in the combo box when using the default data path
- [ ] Clicking a library voice card correctly selects the matching voice in the combo box
- [ ] Newly cloned voices immediately appear in both the sidebar and the combo box

## Technical Notes

**Fix approach:**
1. Replace the hardcoded `CustomVoicesDir` in `TextToSpeechPage.xaml.cs` with the path from `DataPathService` (inject or resolve via the service container)
2. Fix `LibraryVoice_Click` to compare against `$"custom:{voiceName}"` instead of bare `voiceName`

**Files to modify:**
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml.cs` (lines 25-29: hardcoded path, line 729: ID mismatch)

## Dependencies

- Task 063 (Configurable data path) -- done

## Work Log

### 2026-03-25
**Fixed both bugs:**

1. **Path mismatch (primary bug):** Replaced the hardcoded `CustomVoicesDir` (static field pointing to `%APPDATA%\WhisperHeim\voices`) with an instance field `_customVoicesDir` initialized from `DataPathService.VoicesPath`. Threaded `DataPathService` through `App.xaml.cs` -> `MainWindow` -> `TextToSpeechPage` constructor. Now the sidebar list and the combo box both read from the same `DataPathService`-resolved path.

2. **Voice ID mismatch (secondary bug):** Fixed `LibraryVoice_Click` to compare `item.VoiceId == $"custom:{voiceName}"` instead of `item.VoiceId == voiceName`, matching the `"custom:{name}"` format used by `TextToSpeechService.GetAvailableVoices()`.

Build verified: 0 errors.
