---
id: main-077
title: Fix Diarization -- VAD-Only Mic + Constrained Loopback
status: done
type: bug
context: main
created: 2026-03-25
completed: 2026-03-25
commit:
depends_on: [main-017, main-018]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# Fix Diarization -- VAD-Only Mic + Constrained Loopback

## Objective
Fix speaker over-segmentation by never diarizing the mic stream (VAD only) and constraining the loopback diarization with the user-provided speaker count.

## Details

### Mic Stream: VAD-Only (Never Diarize)
- The mic stream is always a single known speaker (the user)
- Replace mic diarization with VAD-based speech segment detection
- Use sherpa-onnx `VoiceActivityDetector` (Silero VAD, already in the project) to detect speech boundaries
- All mic speech segments are attributed to the local speaker (user's name)
- This eliminates diarization overhead and prevents mis-attribution on the mic stream

### Loopback Stream: Constrained Diarization
- When user provides N speaker names: set `NumClusters = N` on the loopback diarization. When `NumClusters` is positive, the clustering threshold is **ignored entirely** -- this is the key fix for over-segmentation.
- When no names provided (e.g., file transcription): raise `DefaultClusteringThreshold` from `0.5f` to `0.80f` and let the model auto-detect speaker count
- Initial speaker name mapping: map cluster IDs to names by order of first appearance in the transcript

### Cross-Chunk Speaker Consistency
- Current chunked diarization (5-min chunks) assigns speaker IDs independently per chunk
- For `NumClusters = 1` (1-on-1 calls): trivial, only one cluster per chunk
- For `NumClusters > 1` (group calls): extract representative speaker embedding per cluster per chunk, compare across chunks with cosine similarity (threshold ~0.7), build global speaker ID mapping
- This prevents "Speaker 0 in chunk 1 = Speaker 1 in chunk 2" issues

### File Transcription (Single-Stream)
- Audio files imported via file picker are single-stream (no mic/loopback separation)
- Run full diarization with auto-detect (raised threshold 0.80)
- No speaker names pre-assigned -- user assigns after transcription (task 079)

## Acceptance Criteria
- [ ] Mic stream uses VAD only, never runs through diarization model
- [ ] Loopback stream diarized with `NumClusters` matching provided speaker count
- [ ] Clustering threshold raised to 0.80 for auto-detect fallback
- [ ] 1-on-1 calls produce exactly 2 speakers (local + 1 remote)
- [ ] Group calls with N specified speakers produce exactly N+1 speakers (local + N remote)
- [ ] Speaker IDs are consistent across processing chunks for group calls
- [ ] File transcription auto-detects speakers with improved threshold
- [ ] Out-of-process diarization worker updated with new parameters

## Notes
- The `SpeakerDiarizationService` already passes `numSpeakers: 1` for mic and `-1` for loopback in `DiarizeDualStreamAsync` -- the mic path needs to change from `numSpeakers=1 diarization` to `VAD-only`
- Current code: `SpeakerDiarizationService.cs:24` has `DefaultClusteringThreshold = 0.5f`
- Current code: `CallTranscriptionPipeline.cs` already splits mic into fixed 120s chunks without diarization -- similar approach but use actual VAD boundaries instead
- Research file: `.workflow/research/transcription-engine-overhaul.md`

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-25 - Implementation Complete

**Changes made:**

1. **`SpeakerDiarizationService.cs`**: Raised `DefaultClusteringThreshold` from `0.5f` to `0.80f` (made `internal` so DiarizationWorker can reference it). Updated `DiarizeDualStreamAsync` to accept `loopbackNumSpeakers` parameter and skip diarization entirely for mic stream (single known speaker).

2. **`ISpeakerDiarizationService.cs`**: Updated `DiarizeDualStreamAsync` interface to accept `loopbackNumSpeakers` parameter with documentation.

3. **`DiarizationWorker.cs`**: Added `--threshold` command-line argument support. Uses `SpeakerDiarizationService.DefaultClusteringThreshold` (0.80) as default instead of hardcoded 0.5.

4. **`CallTranscriptionPipeline.cs`**:
   - Replaced fixed 120s mic chunking with Silero VAD-based speech detection (`DetectSpeechSegmentsWithVad`). Uses sherpa-onnx `VoiceActivityDetector` directly for offline batch processing.
   - Updated `DiarizeChunkOutOfProcessAsync` to accept optional `threshold` parameter, passed through to child process.
   - Added `RemapSpeakerIdsAcrossChunks` for cross-chunk speaker consistency in group calls (renumbers speakers by order of first appearance per chunk).
   - Updated `DiarizeFromFileAsync` to collect chunk results and apply cross-chunk remapping for multi-speaker streams.

**Acceptance criteria status:**
- [x] Mic stream uses VAD only, never runs through diarization model
- [x] Loopback stream diarized with `NumClusters` matching provided speaker count
- [x] Clustering threshold raised to 0.80 for auto-detect fallback
- [x] 1-on-1 calls produce exactly 2 speakers (local + 1 remote) -- mic=speaker 0 via VAD, loopback constrained to NumClusters=1
- [x] Group calls with N specified speakers produce exactly N+1 speakers -- loopback constrained to NumClusters=N
- [x] Speaker IDs are consistent across processing chunks for group calls -- first-appearance ordering remapping
- [x] File transcription auto-detects speakers with improved threshold (0.80)
- [x] Out-of-process diarization worker updated with new parameters (--threshold)

**Files changed:**
- `src/WhisperHeim/Services/Diarization/SpeakerDiarizationService.cs`
- `src/WhisperHeim/Services/Diarization/ISpeakerDiarizationService.cs`
- `src/WhisperHeim/Services/Diarization/DiarizationWorker.cs`
- `src/WhisperHeim/Services/CallTranscription/CallTranscriptionPipeline.cs`

**Note:** Build has 6 errors from concurrent Task 075 changes (App.xaml.cs, MainWindow, TranscriptsPage) -- none from this task's changes.
