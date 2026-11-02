---
id: main-d8m3p
title: Delete unused HighQualityRecorderService / IHighQualityRecorderService
status: done
type: chore
context: main
created: 2026-07-15
completed: 2026-07-15
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
Delete `HighQualityRecorderService` / `IHighQualityRecorderService` and their DI
wiring. Refinement (2026-07-15) confirmed the dead-code status against current
source: the service is constructed in `App.xaml.cs` and injected into
`MainWindow`, stored in a field, and **never invoked** — no call to
`StartRecording`/`StopRecording`/`SaveRecording` exists anywhere. Its
`deviceIndex < 0 → 0` clamp (the same bug ADR-0009 fixed in
`AudioCaptureService`) is therefore dead code, not a live bug.

**Fallback (re-check at pickup):** if a live caller has appeared since this
refinement, do NOT delete — instead apply the ADR-0009 WAVE_MAPPER pass-through
fix (`if (deviceIndex < 0) deviceIndex = 0;` → pass `-1` straight to
`WaveInEvent.DeviceNumber`) and leave the type in place. The confirmation step
below is the gate that decides which branch runs.

### Deletion map (verified 2026-07-15)
- **Delete files:**
  - `src/WhisperHeim/Services/Audio/HighQualityRecorderService.cs`
  - `src/WhisperHeim/Services/Audio/IHighQualityRecorderService.cs`
    (also removes `RecordingStoppedEventArgs` defined there — confirmed used
    nowhere else; every other call site uses the unrelated
    `CallRecordingStoppedEventArgs`, which stays)
- **`App.xaml.cs`:** remove the `_highQualityRecorderService` field declaration
  (~`:70`), its construction `new HighQualityRecorderService(_dataPathService)`
  (~`:384`), and the `_highQualityRecorderService!` argument passed into the
  `new MainWindow(...)` call (~`:695`).
- **`MainWindow.xaml.cs`:** remove the `_highQualityRecorderService` field
  (~`:58`), the `IHighQualityRecorderService` constructor parameter (~`:100`),
  and its assignment (~`:121`).

### Do NOT touch (live siblings)
- `HighQualityLoopbackService` / `IHighQualityLoopbackService` — genuinely live:
  static `HighQualityLoopbackService.Initialize` runs at `App.xaml.cs:253` and
  the instance is passed into `MainWindow` at `:694`. Leave entirely alone.
- `CallRecordingStoppedEventArgs` (`Services/Recording/`) — the live recording
  path's event args, unrelated to the deleted `RecordingStoppedEventArgs`.

## Acceptance criteria
- [x] Re-confirm at pickup that `HighQualityRecorderService.StartRecording` /
      `.StopRecording` / `.SaveRecording` have no live callers (grep the `src/`
      tree). If a caller now exists, switch to the ADR-0009 fallback fix above
      and skip the remaining deletion criteria.
- [x] `HighQualityRecorderService.cs` and `IHighQualityRecorderService.cs` are
      deleted.
- [x] All DI wiring for the service is removed from `App.xaml.cs` (field,
      construction, `MainWindow` argument) and `MainWindow.xaml.cs` (field,
      constructor parameter, assignment).
- [x] `HighQualityLoopbackService` / `IHighQualityLoopbackService` and
      `CallRecordingStoppedEventArgs` are untouched.
- [x] The solution builds with no errors or new warnings, and the existing test
      suite passes.

## Outcome
Re-confirmed at pickup (grep of `src/`) that `HighQualityRecorderService` had
zero callers of `StartRecording`/`StopRecording`/`SaveRecording` — the fallback
branch was not needed. Deleted `Services/Audio/HighQualityRecorderService.cs`
and `Services/Audio/IHighQualityRecorderService.cs` (the latter also removed
`RecordingStoppedEventArgs`, confirmed unused elsewhere; `CallRecordingStoppedEventArgs`
in `Services/Recording/ICallRecordingService.cs` is unrelated and untouched).
Removed the DI wiring: field, construction, and constructor-argument in
`App.xaml.cs` (`_highQualityRecorderService`); field, constructor parameter,
and assignment in `MainWindow.xaml.cs`. `HighQualityLoopbackService` /
`IHighQualityLoopbackService` left entirely alone. Dropped the now-stale
"unused dead code ... out-of-scope tidy item" sentence from the BC README's
microphone-device-selection note. Solution builds clean (same 12 pre-existing
warnings, no new ones) and the full test suite passes (260/260: 246 in
WhisperHeim.Tests, 14 in WhisperHeim.Cli.Tests).

## Notes
Captured during main-c3x7q, refined 2026-07-15. Not required for main-c3x7q's own
acceptance criteria — see its Notes section ("Out of scope") and
`ADR-0009-honor-system-default-capture-device`. The BC README (Microphone device
selection note) already flags this service as "unused dead code … deleting it is
an out-of-scope tidy item" — a worker completing this task should also drop that
trailing sentence from the README so it no longer references a deleted type.
