---
id: main-017
title: Speaker Diarization with sherpa-onnx
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-005]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Large
---
# Speaker Diarization with sherpa-onnx

## Objective
Identify and separate speakers in an audio recording using sherpa-onnx diarization.

## Details
Use sherpa-onnx's speaker diarization API with pyannote segmentation 3.0 ONNX models. Download diarization models via the model manager (005). Accept a WAV file as input. Output a list of segments: {speakerId, startTime, endTime}. For call transcription, use the dual streams to help attribution: mic audio = user, loopback audio = other speakers. If single-stream, rely purely on diarization. Run as a background task (can take time for long recordings).

## Acceptance Criteria
- [x] Correctly separates 2+ speakers
- [x] Segments have accurate timestamps
- [x] Models download automatically via model manager
- [x] Handles long recordings (1hr+)
- [x] Runs as a background task without blocking UI

## Notes
Uses pyannote segmentation 3.0 ONNX models through sherpa-onnx. Dual-stream hint from call recording improves attribution accuracy.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete

**Added diarization model definitions to ModelManagerService:**
- `PyannoteSegmentation`: pyannote segmentation 3.0 int8 ONNX (~1.5 MB) from HuggingFace
- `SpeakerEmbedding`: 3D-Speaker ERes2Net base embedding extractor (~38 MB) from GitHub releases
- Convenience path properties: `PyannoteSegmentationModelPath`, `SpeakerEmbeddingModelPath`
- Models added to `KnownModels` list so they auto-download via existing model manager

**Created Services/Diarization/ with three files:**
- `DiarizationResult.cs` — Domain types: `DiarizationSegment`, `DiarizationResult`, `DiarizationProgress`, `SpeakerSource`, `AttributedDiarizationSegment`
- `ISpeakerDiarizationService.cs` — Interface with `DiarizeAsync` (single-stream) and `DiarizeDualStreamAsync` (mic+loopback attribution)
- `SpeakerDiarizationService.cs` — Implementation using sherpa-onnx `OfflineSpeakerDiarization` API

**Key design decisions:**
- Uses `OfflineSpeakerDiarization` with `ProcessWithCallback` for progress reporting and cancellation support
- Single-stream mode: pure acoustic diarization with auto-detect or fixed speaker count
- Dual-stream mode: diarizes mic and loopback independently, then merges with source attribution (mic=local user, loopback=remote speakers)
- All processing runs on `Task.Run` background threads; native diarizer access is lock-protected
- Clustering threshold 0.5 (sherpa-onnx default); `NumClusters=-1` for auto-detect
- Models: pyannote segmentation 3.0 int8 for speed, ERes2Net base for embedding quality
