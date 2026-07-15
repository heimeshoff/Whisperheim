---
id: main-c3x7q
title: HighQualityRecorderService clamps -1 to device 0 instead of honoring WAVE_MAPPER
status: backlog
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
Task main-v7k2d fixed the dictation hotkey path so that a negative device index
("no saved device" / "saved device removed") passes through to NAudio's
`WaveInEvent.DeviceNumber` as `WAVE_MAPPER` (the true Windows-preferred capture
device) instead of being forced to device 0. See ADR-0009 for the full
rationale.

## What
`HighQualityRecorderService.StartRecording` (`src/WhisperHeim/Services/Audio/HighQualityRecorderService.cs:75-76`),
used by `CallRecordingService` for voice-message / call recording, has the
identical clamp:

```csharp
if (deviceIndex < 0)
    deviceIndex = 0;
```

This was deliberately left untouched by main-v7k2d — no acceptance criterion
there covered voice-message recording, and the bug report was specifically
about dictation. This task is to decide, for the recording path specifically:
whether the same "no saved device" fallback exists here (check how
`deviceIndex` is supplied to `StartRecording` from `CallRecordingService`/
`CallRecordingHotkeyService`), and if so, apply the same fix and verify it
doesn't regress recording behavior.

## Acceptance criteria
- [ ] To be defined during refinement — determine whether a saved-device
      fallback path actually reaches `StartRecording` with a negative index
      in practice (vs. always being called with a concrete resolved index),
      before deciding whether this is worth fixing.

## Notes
Captured during main-v7k2d's execution (see ADR-0009-honor-system-default-capture-device).
Not fixed inline to keep that task's diff scoped to the dictation hotkey bug it
was filed for.
