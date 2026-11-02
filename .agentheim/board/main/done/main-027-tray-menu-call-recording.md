---
id: main-027
title: Tray context menu for start/stop call recording
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-026]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# Tray context menu for start/stop call recording

## Objective
User can start/stop call recording from the tray context menu or via Ctrl+Win+R hotkey, with visual feedback during recording.

## Details

### Context menu
- Add a "Start Call Recording" menu item to the tray `ContextMenu` in `MainWindow.xaml`, between "Settings" and the separator
- Use a microphone/record icon (e.g., `Record24` or `Mic24` SymbolIcon)
- While recording, change the menu item text to "Stop Call Recording (MM:SS)" with live duration
- Wire the `Click` handler to toggle recording via `CallRecordingService.ToggleRecording()`

### Hotkey
- Register `CallRecordingHotkeyService` in `MainWindow.SetupHotkeysAndOrchestration()`
- Use **Ctrl+Win+R** (not the default Ctrl+Shift+Win+R) — create a custom `HotkeyRegistration` with `ModifierKeys.Control | ModifierKeys.Win` and VirtualKey `0x52`
- Hotkey toggles recording same as menu click

### Recording state feedback
- Subscribe to `CallRecordingService.RecordingStarted`, `RecordingStopped`, `DurationUpdated`, `StreamFailed`
- While recording: update tray icon (use a distinct color, e.g., orange or pulsing), update tooltip to "WhisperHeim - Recording call (MM:SS)"
- On `DurationUpdated`: refresh the context menu item text with current duration
- On `StreamFailed`: show a brief notification or log warning (don't stop the other stream)
- On stop: restore idle tray icon and tooltip, reset menu item text

## Acceptance Criteria
- [x] Tray context menu shows "Start Call Recording" item with icon
- [x] Clicking the menu item starts dual-stream recording (mic + loopback)
- [x] During recording, menu item shows "Stop Call Recording (MM:SS)" with live duration
- [x] Clicking again stops recording
- [x] Ctrl+Win+R hotkey toggles recording
- [x] Tray icon/tooltip changes during recording
- [x] Tray icon/tooltip resets when recording stops

## Notes
- Ensure dictation hotkey (Ctrl+Win) and call recording hotkey (Ctrl+Win+R) don't conflict
- Use `Dispatcher.Invoke` for UI updates from service event callbacks
- The `RecordingStopped` event will be consumed by task 028 to trigger transcription

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21
- Added "Start Call Recording" menu item with Record24 icon to tray context menu in MainWindow.xaml
- Added TrayCallRecording_Click handler that calls ToggleRecording()
- Registered Ctrl+Win+R hotkey in SetupHotkeysAndOrchestration() (custom HotkeyRegistration overriding default Ctrl+Shift+Win+R)
- Subscribed to RecordingStarted, RecordingStopped, DurationUpdated, StreamFailed events
- OnCallRecordingStarted: sets orange tray icon, updates tooltip and menu text
- OnCallRecordingDurationUpdated: updates menu text and tooltip with live MM:SS duration
- OnCallRecordingStopped: restores idle icon, tooltip, and menu text
- OnCallRecordingStreamFailed: logs warning via Trace
- Added orange _callRecordingIcon for distinct visual feedback during call recording
- Updated OnDictationStateChanged to restore call recording icon/tooltip if dictation ends while call recording is active
- Added DurationUpdated event to ICallRecordingService interface (was only on concrete class)
- Added event unsubscription in OnClosing cleanup
- Build succeeds, all 21 tests pass
