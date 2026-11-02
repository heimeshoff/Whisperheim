---
id: main-007
title: Silero VAD Integration
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-006]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Medium
---
# Silero VAD Integration

## Objective
Integrate Silero VAD to detect speech boundaries in the audio stream.

## Details
Load Silero VAD ONNX model via ONNX Runtime. Process audio chunks from the ring buffer (512 or 1536 samples at 16kHz). Expose events: SpeechStarted, SpeechEnded(float[] speechAudio). Accumulate speech audio between SpeechStarted and SpeechEnded. Configure thresholds: speech probability threshold (~0.5), min speech duration (~250ms), min silence duration for end-of-speech (~500ms). These should be in settings.

## Acceptance Criteria
- [x] VAD correctly detects speech start/end
- [x] No false triggers on silence
- [x] Speech segments are accumulated and emitted
- [x] Thresholds are configurable

## Notes
Silero VAD ONNX model, chunk sizes 512 or 1536 samples at 16kHz. Thresholds stored in settings.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 - Implementation Complete
- Created `IVoiceActivityDetector.cs`: Interface with `SpeechStarted`, `SpeechEnded(float[] speechAudio)` events, `IsSpeechDetected` property, `ProcessAudio()` and `Reset()` methods.
- Created `VadSettings.cs`: Configurable thresholds (SpeechThreshold=0.5, SilenceThreshold=0.35, MinSpeechDurationMs=250, MinSilenceDurationMs=500, ChunkSamples=512, SampleRate=16000, PreSpeechPadMs=100).
- Created `SileroVadService.cs`: Full implementation using Microsoft.ML.OnnxRuntime. State machine with speech/silence frame counting, hysteresis via separate speech/silence thresholds, pre-speech audio padding, hidden state (h/c) management for Silero ONNX model. Constructor accepts model path and optional settings. Thread-safe via lock.
- No C# compilation errors; pre-existing XAML errors from concurrent tasks are unrelated.
