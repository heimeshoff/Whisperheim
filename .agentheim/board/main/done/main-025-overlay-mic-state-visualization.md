---
id: main-025
title: Overlay Microphone State Visualization
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Medium
---
# Overlay Microphone State Visualization

## Description

Replace the static red microphone overlay icon with dynamic, state-aware visuals. The overlay circle should reflect the current mic/pipeline state through color and animation:

| State | Icon Color | Animation |
|-------|-----------|-----------|
| Idle (mic connected, no speech) | Green | None -- static |
| Speaking (VAD detects speech) | Green | Ring scaling driven by real-time audio amplitude |
| No mic / no audio input | Grey | None |
| Pipeline error or bug | Red | None |

The ring scaling animation should use the existing `SpeechPulse` ScaleTransform infrastructure, but instead of a fixed 0.4s loop, drive it by real-time RMS amplitude calculated from the `float[]` audio samples already available via `AudioCaptureService.AudioDataAvailable`.

This applies to the **overlay circle only** -- the tray icon stays as-is.

## Acceptance Criteria

- [x] Overlay mic icon is green when mic is active and idle
- [x] When speaking, ring pulses proportional to voice amplitude (RMS-driven scaling)
- [x] Overlay mic icon turns grey when no mic is found or no audio input
- [x] Overlay mic icon turns red on pipeline/system errors
- [x] Transitions between states are smooth (no jarring flickers)

## Technical Notes

- Existing infrastructure: `SpeechPulse` and `ListeningPulse` storyboards in `DictationOverlayWindow.xaml`
- `NotifySpeechActivity()` and `NotifySpeechPause()` methods exist but are not wired up
- VAD events (`SpeechStarted`/`SpeechEnded`) exist in `IVoiceActivityDetector`
- Raw audio samples available as `float[]` normalized [-1.0, 1.0] at 16kHz from `IAudioCaptureService`
- Need to add RMS amplitude calculation from audio samples
- Need to wire VAD events through to the overlay window
- Mic icon glyph: `U+E720` (Segoe Fluent Icons)
- Overlay ellipse stroke and mic icon foreground need to become dynamic (currently hardcoded red)

## Files Likely Affected

- `src/WhisperHeim/Views/DictationOverlayWindow.xaml` -- colors, animation definitions
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs` -- amplitude-driven animation, state management
- `src/WhisperHeim/MainWindow.xaml.cs` -- wire VAD/error events to overlay
- `src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs` -- propagate state changes
- Possibly a new small helper for RMS calculation from audio samples

## Work Log

### 2026-03-21

**What was done:**

1. Created `OverlayMicState` enum with four states: Idle (green), Speaking (green + RMS scaling), NoMic (grey), Error (red).

2. Updated `DictationOverlayWindow.xaml`: replaced hardcoded red `Stroke` and `Foreground` with named `SolidColorBrush` elements (`EllipseStrokeBrush`, `MicIconBrush`) initialized to green, enabling runtime color animation.

3. Rewrote `DictationOverlayWindow.xaml.cs`:
   - Added `SetMicState(OverlayMicState)` method that transitions color via `ColorAnimation` (300ms cubic ease) and manages pulse animations per state.
   - Added `UpdateAmplitude(double rmsAmplitude)` method that drives `ScaleTransform` directly from smoothed RMS values (exponential moving average) when in Speaking state. Scale range: 0.92 (silent) to 1.12 (loud).
   - `NotifySpeechActivity()` and `NotifySpeechPause()` now delegate to `SetMicState()`.

4. Updated `DictationOrchestrator.cs`:
   - Added `AudioAmplitudeChanged` event (fires with RMS amplitude on each audio chunk).
   - Added `PipelineError` event (fires on capture start failure or transcription error).
   - Added `CalculateRms(float[])` static helper for RMS computation from normalized samples.

5. Updated `MainWindow.xaml.cs`:
   - Wired `AudioAmplitudeChanged` to dispatch RMS values to `_overlayWindow.UpdateAmplitude()` on UI thread, with automatic Idle/Speaking state transition based on a 0.015 threshold.
   - Wired `PipelineError` to set overlay to Error state.

**Acceptance criteria status:** All 5 criteria met.

**Files changed:**
- `src/WhisperHeim/Views/OverlayMicState.cs` (new)
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml`
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs`
- `src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs`
- `src/WhisperHeim/MainWindow.xaml.cs`
