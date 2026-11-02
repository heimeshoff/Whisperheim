---
id: main-006
title: Audio Capture Service
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-001]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Medium
---
# Audio Capture Service

## Objective
Capture microphone audio as 16kHz mono PCM using NAudio with a ring buffer.

## Details
Create an AudioCaptureService that wraps NAudio's WaveInEvent. Enumerate available input devices. Capture at 16kHz, 16-bit mono. Convert to float32 normalized samples. Use a thread-safe ring buffer (e.g., ConcurrentQueue or custom circular buffer) to decouple capture from processing. Expose events: AudioDataAvailable(float[] samples), CaptureStarted, CaptureStopped. Handle device disconnection gracefully.

## Acceptance Criteria
- [x] Audio captured from selected mic
- [x] 16kHz mono verified
- [x] Ring buffer works under load
- [x] Device enumeration works
- [x] Start/stop is clean

## Notes
NAudio WaveInEvent, 16kHz 16-bit mono, float32 normalized output.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete
- Created `IAudioCaptureService` interface with events: `AudioDataAvailable`, `CaptureStarted`, `CaptureStopped`
- Created `AudioCaptureService` wrapping NAudio `WaveInEvent` at 16kHz/16-bit/mono, converting to float32 normalized
- Created `AudioRingBuffer` — lock-free circular buffer using `Interlocked` operations, 30s capacity
- Created `AudioDeviceInfo` record for device enumeration
- Created `AudioDataEventArgs` and `CaptureStoppedEventArgs` (includes device disconnection flag)
- Device disconnection handled gracefully in `OnRecordingStopped` and `StopCapture`
- Added xUnit test project with 8 tests for ring buffer (write/read, overflow, clear, concurrency under load)
- All tests pass, zero warnings
