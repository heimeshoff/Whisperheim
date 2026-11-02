---
id: main-018
title: Call Transcription Pipeline
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-016, main-017, main-008]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Large
---
# Call Transcription Pipeline

## Objective
Produce a timestamped, speaker-attributed transcript from a recorded call.

## Details
After call recording stops, run the transcription pipeline: 1) Diarize the combined audio to identify speaker segments, 2) Transcribe each segment with Parakeet TDT, 3) Assemble into a structured transcript with timestamps and speaker labels. Use the dual-stream hint (mic = "You", loopback = "Other") to improve speaker labeling. Show progress during processing. Store transcripts in %APPDATA%/WhisperHeim/transcripts/ with date-based naming. Support cancellation.

## Acceptance Criteria
- [x] Complete transcript produced with speaker labels and timestamps
- [x] Progress shown during processing
- [x] Transcripts stored persistently in %APPDATA%/WhisperHeim/transcripts/
- [x] Date-based naming for transcript files
- [x] Cancelable processing

## Notes
Combines diarization (017), dual capture (016), and Parakeet transcription (008) into a unified pipeline.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete

Created 6 new files in `src/WhisperHeim/Services/CallTranscription/`:

1. **CallTranscript.cs** — Domain models: `CallTranscript` (JSON-serializable with id, timestamps, segments) and `TranscriptSegment` (speaker label, start/end time, text, isLocalSpeaker flag).

2. **TranscriptionPipelineProgress.cs** — Progress model with `PipelineStage` enum (LoadingAudio, Diarizing, Transcribing, Assembling, Saving, Completed) and stage/overall percentages.

3. **ICallTranscriptionPipeline.cs** — Interface with `ProcessAsync(CallRecordingSession, IProgress, CancellationToken)`.

4. **ITranscriptStorageService.cs** — Storage interface: Save, Load, ListTranscriptFiles.

5. **TranscriptStorageService.cs** — Persists transcripts as JSON in `%APPDATA%/WhisperHeim/transcripts/` with `transcript_YYYYMMDD_HHmmss.json` naming. Handles duplicate file names.

6. **CallTranscriptionPipeline.cs** — Full pipeline implementation:
   - Loads WAV files via NAudio with resampling to 16kHz mono
   - Uses `DiarizeDualStreamAsync` for mic="You" / loopback="Other" speaker attribution
   - Falls back to single-stream diarization when one stream is empty
   - Transcribes each segment using the source-specific audio (mic or loopback) for better quality
   - Merges consecutive same-speaker segments within 2s gap
   - Weighted progress reporting across all stages (5/30/55/5/5)
   - Full CancellationToken support throughout

Build: 0 errors, 0 warnings.
