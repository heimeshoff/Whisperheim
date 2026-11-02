---
id: main-v7k2d
title: Hotkey dictation ignores the selected microphone (always captures WaveIn device 0)
status: done
type: bug
context: main
created: 2026-07-15
completed: 2026-07-15
depends_on: []
blocks: []
tags: [dictation, audio, microphone, device-selection, regression]
related_adrs: [0009-honor-system-default-capture-device]
related_research: []
prior_art: []
---

## Why
Hold-to-talk dictation silently records the wrong microphone. The user selected
"Microphone (Elgato Wave:3)" in settings and verified it works in other apps, but
dictation produces empty transcriptions. The overlay bubble appears and flickers
`NoMic ↔ Speaking` every ~50 ms (the VAD reacting to a near-silent noise floor),
and `[TranscriptionService]` returns `""` (or the classic silence-hallucination
`"Thank you."`). Logs show **every** capture — all 8,315 in the current log —
starting on `device 0`, regardless of the saved device. It only appeared to work
before because whatever mic happened to sit at WaveIn index 0 was live; once the
real mic moved to a non-zero index, dictation went silent.

## What
`DictationOrchestrator.OnHotkeyPressed` calls `_audioCapture.StartCapture()` with
**no device index** (`src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs:145`).
`StartCapture`'s default is `-1`, which `AudioCaptureService.StartCapture` then
forces to physical WaveIn `device 0` (`AudioCaptureService.cs:65-66`). The saved
device name in `_settingsService.Current.Dictation.AudioDevice` is never resolved
or passed through — so the user's microphone choice is ignored on the hotkey path.

Note the contrast: `DictationPipeline.cs:112` **does** pass a resolved
`deviceIndex`. The VAD-free hotkey orchestrator is the one path that drops it.

The fix: in `OnHotkeyPressed`, resolve the saved device name to an index via
`AudioDeviceResolver.ResolveDeviceIndex(_audioCapture, _settingsService?.Current.Dictation.AudioDevice)`
and pass it to `StartCapture(deviceIndex)`. When the saved device is absent/unresolvable,
`-1` (system default) is the correct fallback — but note the separate latent issue below.

## Acceptance criteria
- [x] Holding the dictation hotkey captures from the microphone selected on the
      Dictation settings page, not always WaveIn device 0.
- [x] With "Microphone (Elgato Wave:3)" selected (at a non-zero WaveIn index),
      dictation produces correct transcribed text instead of `""`.
- [x] The resolved device index is logged at capture start (so
      `[AudioCaptureService] Starting capture on device N` reflects the real choice).
- [x] Changing the selected device in settings takes effect on the next hotkey
      press without an app restart.
- [x] When no device is saved or the saved device is gone, capture falls back to
      the system default and dictation still works.

## Notes
- Root cause found by reading `%APPDATA%\WhisperHeim\whisperheim.log` (2026-07-15):
  `[DictationOrchestrator] Hotkey pressed`, then `[AudioCaptureService] Starting
  capture on device 0 (of 3 available)` on every attempt, then empty transcriptions.
- **Latent secondary issue worth deciding on while here:** `AudioCaptureService.StartCapture`
  maps `-1` → `0` (`deviceIndex = 0`) with the comment "NAudio maps to device 0".
  For NAudio `WaveInEvent`, `DeviceNumber = -1` is `WAVE_MAPPER` (the actual Windows
  default/preferred capture device); forcing it to `0` overrides that and opens the
  first enumerated device instead. Consider passing `-1` through to `WaveInEvent`
  so the true system default is honored on the fallback path. (Verify NAudio
  `WaveInEvent` accepts `DeviceNumber = -1` before relying on it.)
- Consider whether `CallRecordingService` / other WaveIn call sites share the same
  `-1 → 0` fallback assumption.

## Outcome
Fixed both halves of the bug:
1. `DictationOrchestrator.OnHotkeyPressed` now calls the new internal
   `StartCaptureForDevice(savedDeviceName)`, which resolves the saved device
   name via `AudioDeviceResolver.ResolveDeviceIndex(_audioCapture, savedDeviceName)`
   and passes the resulting index to `_audioCapture.StartCapture(deviceIndex)`,
   logging the resolution. Resolved fresh on every hotkey press (no caching), so
   a settings change takes effect on the very next press. Covered by 4 new xUnit
   tests in `tests/WhisperHeim.Tests/DictationOrchestratorDeviceSelectionTests.cs`
   driving the extracted seam directly (the real hotkey event can only be raised
   from `GlobalHotkeyService` itself).
2. Decided and fixed the latent secondary issue: `AudioCaptureService.StartCapture`
   was clamping any negative `deviceIndex` to `0` ("NAudio maps to device 0" —
   a wrong assumption). Removed the clamp so `-1` passes straight through to
   `WaveInEvent.DeviceNumber`, which NAudio treats as `WAVE_MAPPER` (the real
   Windows-preferred capture device) — otherwise the resolver's own "system
   default" fallback (AC5) would have silently opened device 0 instead. See
   ADR-0009-honor-system-default-capture-device for the full rationale. Left
   `HighQualityRecorderService`'s identical clamp untouched (different feature,
   not reported broken) and filed backlog item `main-c3x7q` to evaluate it
   separately.

Full suite (242 tests) passes after the fix. Files touched:
`src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs`,
`src/WhisperHeim/Services/Audio/AudioCaptureService.cs`,
`tests/WhisperHeim.Tests/DictationOrchestratorDeviceSelectionTests.cs` (new),
`.agentheim/contexts/main/README.md`,
`.agentheim/knowledge/decisions/0009-honor-system-default-capture-device.md` (new),
`.agentheim/contexts/main/backlog/main-c3x7q-highqualityrecorder-honor-system-default-device.md` (new).
