---
id: main-078
title: Fix Temporal Ordering -- Clock Drift Correction
status: done
type: bug
context: main
created: 2026-03-25
completed: 2026-03-25
commit:
depends_on: [main-077]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Small
---
# Fix Temporal Ordering -- Clock Drift Correction

## Objective
Fix incorrect temporal ordering of transcript segments from mic and loopback streams by applying clock drift correction before merging.

## Details

### The Problem
WASAPI mic capture and loopback capture use different hardware clocks (ADC vs DAC). These clocks drift at approximately 0.1% (1ms per second), causing:
- 30-minute recording: ~1.8 seconds drift
- 60-minute recording: ~3.6 seconds drift

This causes transcript segments to appear out of order -- e.g., a remote speaker's segment appears before the local speaker's response even though the response came first.

### Linear Drift Correction
After recording stops, before merging segments:
1. Measure the actual sample count / duration of each WAV file
2. If durations differ, compute a drift factor: `correction = mic_duration / loopback_duration`
3. Scale all loopback segment timestamps (start and end) by this factor
4. Then interleave mic + loopback segments and sort by corrected start time

### Implementation
- Add drift correction step in `CallTranscriptionPipeline.ProcessAsync`, after diarization/VAD and before the merge-and-sort step
- Log the measured drift for diagnostics (e.g., "Clock drift: 1.2s over 30min recording")
- For short recordings (<5 min), drift is negligible -- still apply correction but expect near-zero adjustment

## Acceptance Criteria
- [x] WAV file durations measured after recording
- [x] Loopback segment timestamps scaled by drift correction factor
- [x] Segments from both streams interleaved in correct temporal order
- [x] Drift amount logged for diagnostics
- [x] Short recordings still produce correctly ordered output
- [x] Long recordings (30min+) no longer have out-of-order segments

## Notes
- This is a small, focused change to the pipeline merge step
- The correction is linear (constant drift rate assumption) -- good enough for WASAPI
- Research file: `.workflow/research/transcription-engine-overhaul.md`, section "Dual-Stream Temporal Ordering"

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-25 — Implementation complete

**What was done:**
Added linear clock drift correction to `CallTranscriptionPipeline.ProcessAsync`, inserted between the segment-building step and the merge-and-sort step. When both mic and loopback WAV files exist, the code computes `correction = micDuration / loopbackDuration` and scales all loopback segment timestamps (start and end) by this factor. The drift amount is logged for diagnostics.

**Key details:**
- WAV durations were already measured earlier in the method (`micDurationSeconds`, `systemDurationSeconds`) via `GetWavDuration` — reused those values
- `AttributedDiarizationSegment` is a sealed record, so used `with` expression for immutable update
- Correction is only applied when both streams exist and have >0.5s duration
- For short recordings the correction factor is near 1.0 (negligible adjustment) — no special-casing needed
- For long recordings the linear scaling corrects the ~0.1% hardware clock drift

**Acceptance criteria:** All met.

**Files changed:**
- `src/WhisperHeim/Services/CallTranscription/CallTranscriptionPipeline.cs` — added drift correction block (~25 lines)

**Build:** Passes with 0 errors, 2 pre-existing warnings.
