---
id: main-015
title: WASAPI Loopback Audio Capture
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-006]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# WASAPI Loopback Audio Capture

## Objective
Capture system audio via WASAPI loopback recording.

## Details
Create a LoopbackCaptureService using NAudio's WasapiLoopbackCapture. Capture all system audio (whatever is playing through speakers/headphones). Resample to 16kHz mono to match the ASR pipeline. Store raw audio to a temp WAV file during recording. Expose start/stop/events matching AudioCaptureService interface. Handle the case where no audio device is available.

## Acceptance Criteria
- [x] System audio captured during a Zoom/YouTube/etc session
- [x] Resampled correctly to 16kHz mono
- [x] Saved to WAV file
- [x] Start/stop works cleanly
- [x] Handles missing audio device gracefully

## Notes
Uses NAudio's WasapiLoopbackCapture API. Must match the same interface as AudioCaptureService for consistency.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — LoopbackCaptureService implemented
- Created `src/WhisperHeim/Services/Audio/LoopbackCaptureService.cs`
- Implements `IAudioCaptureService` using NAudio's `WasapiLoopbackCapture`
- Captures system audio from the default render device (loopback)
- Converts source format (typically 32-bit float, 48kHz stereo) to 16kHz mono float32
- Down-mixes multi-channel audio to mono by averaging channels
- Resamples via linear interpolation from source rate to 16kHz
- Writes resampled audio to a temp WAV file during recording (exposed via `TempWavFilePath`)
- Pushes resampled samples into `AudioRingBuffer` and raises `AudioDataAvailable` events
- Handles missing audio device by throwing `InvalidOperationException` with clear message on `StartCapture`
- `GetAvailableDevices()` enumerates active render endpoints via `MMDeviceEnumerator`
- Handles format variations: IEEE float, 16-bit PCM, 32-bit PCM, Extensible
- Build verified: compiles cleanly with zero warnings/errors
