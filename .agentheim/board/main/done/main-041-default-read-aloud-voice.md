---
id: main-041
title: Default Read-Aloud Voice from TTS Page
status: done
type: feature
context: main
created: 
completed: 2026-03-22
commit:
depends_on: [main-040]
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: —
size: Small
---
# Default Read-Aloud Voice from TTS Page

## Description

When the user selects a voice on the TTS page, persist that choice as the default voice for the read-aloud hotkey (Shift+Win+Ä). Currently `TtsSettings.DefaultVoiceId` exists in settings and `ReadAloudHotkeyService` reads it, but the TTS page voice combo does not write it back to settings.

## Acceptance Criteria

- [x] Selecting a voice in the TTS page `VoiceCombo` persists `DefaultVoiceId` in `TtsSettings`
- [x] On next read-aloud hotkey press, the newly selected voice is used
- [x] On app startup, the TTS page voice combo reflects the saved default voice
- [x] If the saved voice is no longer available (deleted .wav), falls back to first available voice gracefully

## Implementation Notes

- `VoiceCombo_SelectionChanged` in `TextToSpeechPage.xaml.cs` should save the selected voice ID to `SettingsService`
- `ReadAloudHotkeyService` already reads `TtsSettings.DefaultVoiceId` — no changes needed there
- On page load, pre-select the combo to match `DefaultVoiceId` from settings

## Work Log

**2026-03-22** — Implemented default voice persistence.

**Changes:**
1. `TextToSpeechPage.xaml.cs`: Injected `SettingsService`; `VoiceCombo_SelectionChanged` now writes `DefaultVoiceId` to settings and saves; `PopulateVoicesAsync` pre-selects the saved voice on load, falling back to index 0 if the saved voice is missing.
2. `MainWindow.xaml.cs`: Passed `_settingsService` to `TextToSpeechPage` constructor.

**Acceptance criteria:** All met. `ReadAloudHotkeyService` already reads `TtsSettings.DefaultVoiceId` at hotkey-press time, so the newly saved voice is picked up automatically. Fallback to first available voice handles deleted .wav files gracefully.

**Files changed:**
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml.cs`
- `src/WhisperHeim/MainWindow.xaml.cs`
