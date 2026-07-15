---
id: 0009
title: Honor NAudio WAVE_MAPPER (-1) as the real system default capture device
scope: main
status: accepted
date: 2026-07-15
supersedes: []
superseded_by: []
related_tasks: [main-v7k2d]
related_research: []
---

# ADR 0009: Honor NAudio WAVE_MAPPER (-1) as the real system default capture device

## Context
Task main-v7k2d fixed the hotkey dictation path (`DictationOrchestrator.OnHotkeyPressed`)
to resolve the saved microphone name to a WaveIn device index via
`AudioDeviceResolver.ResolveDeviceIndex` before calling `StartCapture(deviceIndex)`.
That resolver deliberately returns `-1` ("system default") when no device is saved
or the saved device is no longer present (acceptance criterion: "capture falls back
to the system default and dictation still works").

`AudioCaptureService.StartCapture`, however, previously clamped any negative
`deviceIndex` to `0`:

```csharp
// Resolve default device (-1 means system default, which NAudio maps to device 0)
if (deviceIndex < 0)
    deviceIndex = 0;
```

This comment was wrong. NAudio's `WaveInEvent.DeviceNumber` is passed straight
through to the underlying `waveInOpen` call; a negative device number (canonically
`-1`) is the documented `WAVE_MAPPER` sentinel, which asks Windows to pick its
actual preferred/default capture device. Forcing `-1` to `0` instead silently
opened whatever device happens to enumerate first — not the same thing once a
mic other than the first-enumerated one is the real default, which is exactly the
class of bug this task was filed for. The clamp defeated the resolver's own
fallback path: `ResolveDeviceIndex` returns `-1` to mean "use the true system
default," but `StartCapture` was translating that into "use device 0" before it
ever reached NAudio.

## Decision
`AudioCaptureService.StartCapture` now passes a negative `deviceIndex` straight
through to `WaveInEvent.DeviceNumber` unchanged, letting Windows/NAudio resolve
`WAVE_MAPPER` itself. The `deviceCount == 0` guard is unaffected — that check is
about whether *any* capture device exists, independent of which one is opened.

`HighQualityRecorderService.StartRecording` (used for voice-message recording via
`CallRecordingService`, not by the dictation hotkey path) has the identical
`deviceIndex < 0 -> 0` clamp. It is **not** changed by this task: no acceptance
criterion here touches voice-message recording, and its saved-device semantics
were not reported as broken. Fixing it is tracked as a separate backlog item
(`main-c3x7q`) so it gets its own scoped
verification rather than riding along on this bug fix.

## Consequences
- The dictation hotkey path's "no saved device" and "saved device removed"
  fallbacks now open the actual Windows-preferred microphone, not just WaveIn
  device 0.
- `HighQualityRecorderService`'s equivalent fallback still opens device 0 until
  the tracked backlog item is picked up — same latent behavior as before, just
  not silently fixed as a side effect of an unrelated task.
- No test exercises the NAudio pass-through directly: `WaveInEvent` requires a
  real capture device, so this behavior can't be asserted without live
  audio hardware in CI. The change is a two-line deletion restoring the
  documented NAudio contract; risk is judged low and is offset by the existing
  `DictationOrchestratorDeviceSelectionTests` coverage of the resolution logic
  that feeds this call.
