---
id: main-009
title: Streaming Dictation Pipeline
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-007, main-008]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Large
---
# Streaming Dictation Pipeline

## Objective
Wire VAD and ASR into a streaming pipeline that produces live text from continuous speech.

## Details
Create a DictationPipeline that orchestrates: AudioCapture -> VAD -> ASR -> Text output. When VAD detects speech start, begin accumulating. On speech end, send to ASR. For live partial results: also send accumulated audio every ~1-2 seconds during ongoing speech (tumbling window approach). Diff current transcription against previous to determine new text. Handle rapid speech/pause cycles. Pipeline should be startable/stoppable. Expose events: PartialResult(string text), FinalResult(string text). Thread-safe throughout.

## Acceptance Criteria
- [x] Continuous speech produces streaming text updates
- [x] Pauses finalize segments
- [x] Latency under 2s
- [x] Handles rapid start/stop
- [x] No audio glitches or dropped segments

## Notes
Tumbling window approach for partial results every ~1-2 seconds. Diff-based new text detection.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete

**Files created:**
- `src/WhisperHeim/Services/Dictation/IDictationPipeline.cs` — Interface with PartialResult, FinalResult, Error events; Start/Stop lifecycle
- `src/WhisperHeim/Services/Dictation/DictationPipeline.cs` — Full implementation wiring AudioCapture → VAD → ASR → Text
- `src/WhisperHeim/Services/Dictation/DictationPipelineSettings.cs` — Configuration (partial interval, min audio duration, sample rate)
- `tests/WhisperHeim.Tests/DictationPipelineTests.cs` — 11 unit tests with fakes for all three dependencies

**Architecture:**
- AudioCapture raises AudioDataAvailable → fed to VAD's ProcessAudio
- VAD fires SpeechStarted → pipeline starts accumulating raw audio + starts tumbling window timer (1.5s default)
- Timer tick → snapshot accumulated audio → TranscribeAsync → diff against previous partial → emit PartialResult with new text only
- VAD fires SpeechEnded → stop timer, TranscribeAsync on VAD's speech audio (includes pre-speech padding) → emit FinalResult
- Sequence counter invalidates stale partial results when speech ends or new segment starts
- Thread-safe: all mutable state guarded by lock, ASR calls are async fire-and-forget
- Handles device disconnection, rapid start/stop cycles, and graceful Stop() with pending speech finalization
- ComputeNewText uses prefix-match diff with fallback to longest-common-prefix for ASR corrections
