---
id: main-d8m3p
title: Delete unused HighQualityRecorderService / IHighQualityRecorderService
status: backlog
type: chore
context: main
created: 2026-07-15
completed:
depends_on: []
blocks: []
tags: [audio, dead-code, tidy]
related_adrs: [0009-honor-system-default-capture-device]
related_research: []
prior_art: [main-c3x7q]
---

## Why
Investigated while refining/executing main-c3x7q ("recording ignores the saved
microphone"): `HighQualityRecorderService` is constructed in `App.xaml.cs`
(`:384`) and injected into `MainWindow`, but nothing ever calls its
`StartRecording`. Its `deviceIndex < 0 -> 0` clamp (the same bug ADR-0009
fixed in `AudioCaptureService`) is therefore dead code, not a live bug — it
was left unfixed on purpose during main-c3x7q so that task's verification
stayed scoped to the actual live recording path
(`CallRecordingService` -> `AudioCaptureService`).

## What
Confirm `HighQualityRecorderService` / `IHighQualityRecorderService` genuinely
have no live callers (re-check at pickup time — `App.xaml.cs`/`MainWindow.xaml.cs`
wiring may have changed), then delete both files and their DI wiring. If a live
caller has appeared in the meantime, this task should instead apply the same
WAVE_MAPPER pass-through fix ADR-0009 describes rather than deleting the type.

## Acceptance criteria
- [ ] To be defined during refinement — confirm dead-code status is still
      accurate, then either delete the service + interface + DI wiring, or
      (if it's gained a caller) apply the ADR-0009 WAVE_MAPPER fix instead.

## Notes
Captured during main-c3x7q. Not required for that task's own acceptance
criteria — see its Notes section ("Out of scope") and
`ADR-0009-honor-system-default-capture-device`.
