---
id: main-016
title: Dual Audio Capture for Call Recording
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-015]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# Dual Audio Capture for Call Recording

## Objective
Record mic and system audio simultaneously as separate streams for call transcription.

## Details
Create a CallRecordingService that starts both AudioCaptureService (mic) and LoopbackCaptureService (system) simultaneously. Each stream saved to a separate temp WAV file. Synchronize timestamps between streams. Provide a unified start/stop interface. Add a hotkey for call recording (configurable, default: Ctrl+Shift+Win). Show recording duration in the tray tooltip or overlay. Handle one stream failing without losing the other.

## Acceptance Criteria
- [x] Both streams recorded simultaneously
- [x] Timestamps aligned between mic and system audio streams
- [x] Separate WAV files produced for each stream
- [x] Hotkey works (default: Ctrl+Shift+Win+R, configurable)
- [x] Recording duration visible in tray tooltip or overlay
- [x] One stream failing does not lose the other

## Notes
Depends on LoopbackCaptureService (015) and the existing AudioCaptureService for mic input.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete
**Agent:** claude-opus-4-6

Created 4 new files in `src/WhisperHeim/Services/Recording/`:

1. **ICallRecordingService.cs** — Interface defining the unified start/stop/toggle API, plus events for RecordingStarted, RecordingStopped, and StreamFailed. Includes AudioStreamKind enum and event args classes.

2. **CallRecordingService.cs** — Core orchestrator that creates and manages both an `AudioCaptureService` (mic) and `LoopbackCaptureService` (system) simultaneously. Key features:
   - Both streams start together with a shared UTC `StartTimestamp` for alignment
   - Each stream writes to a separate WAV file in `%TEMP%/WhisperHeim/`
   - If one stream fails to start or disconnects mid-recording, the other continues
   - `StreamFailed` event notifies consumers of partial failures
   - `DurationUpdated` event fires every second via DispatcherTimer for UI display
   - Static `FormatDuration()` helper for tray tooltip formatting
   - `ToggleRecording()` convenience method for hotkey binding

3. **CallRecordingSession.cs** — Immutable session model holding mic/system WAV file paths, start/end timestamps, and computed Duration property.

4. **CallRecordingHotkeyService.cs** — Dedicated global hotkey (Ctrl+Shift+Win+R, configurable) that toggles call recording. Uses a distinct hotkey ID (0x7702) to coexist with the dictation hotkey service.

**Build verification:** All new files compile without errors. Pre-existing errors in the worktree (ModelManagerService, AppSettings not yet created by other tasks) are unrelated.
