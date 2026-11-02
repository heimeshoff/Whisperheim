---
id: main-059
title: Rework Read-Aloud Hotkey to Navigate to TTS Page
status: done
type: refactor
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
milestone: --
size: Medium
---
# Rework Read-Aloud Hotkey to Navigate to TTS Page

## Objective
Change the read-aloud hotkey to Ctrl+Win+Ä and rework its behavior: instead of speaking inline with an overlay, it should capture the selected text, bring WhisperHeim to the foreground, navigate to the Text to Speech page, and paste the text into the input workspace.

## Details

### 1. Change hotkey
- Update default from `Shift+Win+Ä` (MOD_SHIFT | MOD_WIN + 0xDE) to `Ctrl+Win+Ä` (MOD_CONTROL | MOD_WIN + 0xDE)
- File: `src/WhisperHeim/Services/SelectedText/ReadAloudHotkeyService.cs`

### 2. New hotkey flow
When the hotkey is pressed:
1. Capture selected text via `SelectedTextService.CaptureSelectedTextAsync()`
2. Bring the WhisperHeim window to the foreground (restore if minimized)
3. Navigate to the Text to Speech page via `MainWindow.NavigateTo("TextToSpeech")`
4. Set the captured text into the TTS input workspace (`SpeechTextInput`), replacing any existing text
5. Stop — user controls playback manually from the TTS page

### 3. Remove read-aloud overlay
- Delete overlay window and related UI (ReadAloudOverlay)
- Delete `ReadAloudOverlayState.cs`
- Remove overlay initialization, event handlers, and teardown from `MainWindow.xaml.cs`
- Remove `ReadAloudStarted`, `ReadAloudPlaying`, `ReadAloudCompleted`, `ReadAloudCancelled` events and their handlers

### 4. Clean up ReadAloudHotkeyService
- Remove inline TTS speak logic (`_textToSpeechService.SpeakAsync()` call)
- Remove voice resolution, playback device resolution, model loading from hotkey handler
- Service now only needs: `SelectedTextService`, and a way to signal MainWindow (event or callback) to show + navigate + paste

### Key files
- `src/WhisperHeim/Services/SelectedText/ReadAloudHotkeyService.cs` — hotkey registration and handler
- `src/WhisperHeim/MainWindow.xaml.cs` — overlay logic removal, window restore, navigation
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml.cs` — method to set input text programmatically
- `src/WhisperHeim/Views/ReadAloudOverlayState.cs` — delete
- Overlay window files — delete

## Acceptance Criteria
- [x] Hotkey is Ctrl+Win+Ä (replaces Shift+Win+Ä)
- [x] Pressing hotkey captures selected text from any app
- [x] WhisperHeim window is brought to foreground / restored if minimized
- [x] App navigates to Text to Speech page
- [x] Captured text appears in the TTS input workspace, replacing any existing text
- [x] Read-aloud overlay is fully removed (no overlay window, no overlay state)
- [x] User controls playback manually from the TTS page
- [x] No leftover dead code from the old inline read-aloud flow

## Work Log
<!-- Appended by /work during execution -->
### 2026-03-22 — Implementation complete
- Changed default hotkey from `Shift+Win+Ä` to `Ctrl+Win+Ä` in `ReadAloudHotkeyService`
- Rewrote `ReadAloudHotkeyService`: removed all inline TTS logic (SpeakAsync, voice resolution, playback device, model loading, cancellation token tracking, VoiceId/Speed properties, overlay lifecycle events). Now only captures text and raises `TextCaptured` event.
- Removed `ITextToSpeechService` dependency from constructor (updated App.xaml.cs instantiation)
- Added `ReadAloudTextCapturedEventArgs` event args class
- Added `SetInputText(string)` public method to `TextToSpeechPage` for programmatic text injection
- Updated `MainWindow.xaml.cs`: replaced overlay initialization and 4 overlay event handlers with single `OnReadAloudTextCaptured` handler that brings window to foreground, navigates to TTS page, selects nav item, and sets input text
- Removed overlay teardown from `OnClosing`
- Deleted 3 overlay files: `ReadAloudOverlayState.cs`, `ReadAloudOverlayWindow.xaml`, `ReadAloudOverlayWindow.xaml.cs`
- Updated `AppSettings.cs` comment to reflect new default hotkey
- Build succeeds with 0 warnings, 0 errors
