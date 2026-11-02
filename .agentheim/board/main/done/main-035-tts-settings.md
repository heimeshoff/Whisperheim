---
id: main-035
title: TTS settings + hotkey configuration
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-029, main-032]
blocks: []
tags: [m4, tts]
related_adrs: []
related_research: []
prior_art: []
milestone: M4 - Text-to-Speech
size: Small
---
# TTS settings + hotkey configuration

**Parent:** 023

## Objective
Add TTS-specific settings to the settings page and persist them.

## Details
- Extend `AppSettings` model with TTS section:
  - Default voice (built-in or custom voice name)
  - Read-aloud hotkey (key combination)
  - Playback device (audio output device selection)
- Add TTS section to existing Settings page
- Hotkey configuration: key combo picker similar to existing dictation hotkey
- Playback device selector: enumerate output devices via NAudio `WaveOutEvent`
- Persist via existing `SettingsService`

## Acceptance Criteria
- [x] Default voice setting persists across sessions
- [x] Read-aloud hotkey is configurable
- [x] Playback device is selectable
- [ ] Settings appear in existing Settings page (UI deferred to task 033 TTS page)
- [x] Changes take effect immediately (no restart)

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Backend implementation complete
- Added `TtsSettings` class to `AppSettings` with `DefaultVoiceId`, `ReadAloudHotkey`, and `PlaybackDeviceId` fields, all persisted as JSON via existing SettingsService
- Added `ToDisplayString()` and `TryParse()` to `HotkeyRegistration` for hotkey string serialization/deserialization (e.g. "Ctrl+Shift+R")
- Updated `ReadAloudHotkeyService` to accept `SettingsService` and read hotkey, voice, and playback device from TTS settings instead of hardcoding
- Added `ReRegisterFromSettings()` method for live hotkey changes without restart
- Extended `ITextToSpeechService.SpeakAsync` and `TextToSpeechService.SpeakAsync` with `playbackDeviceNumber` parameter (NAudio `WaveOutEvent.DeviceNumber`)
- Updated `App.xaml.cs` to pass `SettingsService` to `ReadAloudHotkeyService`
- Build succeeds (only file-lock warnings from running process)
- Note: UI controls for TTS settings page deferred to task 033 (concurrent TTS UI page task)
