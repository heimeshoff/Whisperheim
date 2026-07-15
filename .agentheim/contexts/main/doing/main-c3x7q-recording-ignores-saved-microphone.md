---
id: main-c3x7q
title: Call/voice-message recording ignores the saved microphone (always records from system default)
status: doing
type: bug
context: main
created: 2026-07-15
completed:
depends_on: []
blocks: []
tags: [audio, microphone, device-selection, voice-messages, call-recording]
related_adrs: [0009-honor-system-default-capture-device]
related_research: []
prior_art: [main-v7k2d]
---

## Why
main-v7k2d (ADR-0009) fixed the **dictation** hotkey path to resolve the saved
microphone name (`Dictation.AudioDevice`) to a WaveIn device index via
`AudioDeviceResolver.ResolveDeviceIndex` on every hotkey press, and to pass a
negative "no saved device" result straight through to NAudio's `WAVE_MAPPER`
(the true Windows-preferred device) instead of clamping to device 0.

The **recording** path (call recording + voice-message capture) never got the
first half of that fix: it resolves nothing. Both entry points —
`TranscriptsPage`'s record button (`StartStopRecording_Click`) and the call
hotkey (`CallRecordingHotkeyService` → `CallRecordingService.ToggleRecording`) —
call `CallRecordingService.StartRecording()` with the default `micDeviceIndex =
-1`. So recording **always captures from the Windows default mic, silently
ignoring the microphone the user selected in settings.** A user who picks a
specific mic for dictation gets that mic for dictation but the system default
for recording — inconsistent and surprising.

## What
Have the recording path resolve the saved microphone the same way dictation
does, reusing the single existing `Dictation.AudioDevice` setting (this is a
consistency bug fix, **not** a new per-feature recording-device setting).

Investigation findings this task was re-scoped from (originally filed as
"HighQualityRecorderService clamps -1 to device 0"):
- `HighQualityRecorderService.StartRecording`'s `deviceIndex < 0 → 0` clamp is
  **dead code**: the service is constructed (`App.xaml.cs:384`) and injected into
  `MainWindow`, but its `StartRecording` is never called by any path. The clamp
  never runs. ADR-0009's aside that it's "used by `CallRecordingService`" is
  factually wrong — see the Notes.
- The live recording path routes through `CallRecordingService.StartRecording` →
  `new AudioCaptureService().StartCapture(micDeviceIndex)`, and
  `AudioCaptureService` was **already** fixed by main-v7k2d to pass a negative
  index through to `WAVE_MAPPER`. So the *fallback* half already works; only
  saved-device *resolution* is missing.

Preferred shape (worker may refine): centralize resolution in
`CallRecordingService.StartRecording` — resolve `Dictation.AudioDevice` →
index at start time (no caching, so a settings change takes effect on the next
recording, mirroring `DictationOrchestrator`), rather than duplicating the
lookup in each caller. `AudioDeviceResolver.ResolveDeviceIndex` already returns
`-1` when nothing is saved or the saved device is gone, and the downstream
`AudioCaptureService` already honors `-1` as `WAVE_MAPPER`.

## Acceptance criteria
- [ ] Starting a recording via **both** the `TranscriptsPage` record button and
      the call-recording hotkey resolves the saved mic name
      (`Dictation.AudioDevice`) to a device index via
      `AudioDeviceResolver.ResolveDeviceIndex` before mic capture starts —
      instead of always passing `-1`.
- [ ] Resolution happens at recording-start time with **no caching**: changing
      the selected microphone in settings takes effect on the very next
      recording (same no-cache semantics as `DictationOrchestrator.OnHotkeyPressed`).
- [ ] When no device is saved, or the saved device is no longer present,
      resolution yields `-1` and recording falls back to the true system default
      (`WAVE_MAPPER`) and still records successfully (no clamp to device 0).
- [ ] The resolved index flows `CallRecordingService.StartRecording` →
      `AudioCaptureService.StartCapture` unchanged — it is not re-clamped
      anywhere along the way.
- [ ] System-audio (loopback) capture is unaffected — the device index is
      correctly ignored for `LoopbackCaptureService`, and dual mic+loopback
      recording still produces both `mic.wav` and `system.wav`.
- [ ] A unit test covers resolution feeding recording start (saved device
      present → its index; saved absent/removed → `-1`), mirroring
      `DictationOrchestratorDeviceSelectionTests`.

## Notes
Re-scoped 2026-07-15 during refinement of the original capture
(`HighQualityRecorderService clamps -1 to device 0`). The original premise was
investigated and found not to manifest — the clamp lives in a service nothing
calls. The user's intent (confirmed during refinement) is that the saved
microphone **should** apply to recording, so the task now targets the real
user-facing gap: recording never resolving the saved device.

Out of scope (candidates for a separate tidy task, not required here):
- Deleting the unused `HighQualityRecorderService` / `IHighQualityRecorderService`.
- Applying a defensive `WAVE_MAPPER` parity fix to that dead service.

See ADR-0009-honor-system-default-capture-device for the dictation-side
rationale and the `WAVE_MAPPER` contract this reuses.
