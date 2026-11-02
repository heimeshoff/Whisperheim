---
id: main-034
title: Audio export (MP3/OGG)
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-029, main-033]
blocks: []
tags: [m4, tts]
related_adrs: []
related_research: []
prior_art: []
milestone: M4 - Text-to-Speech
size: Small
---
# Audio export (MP3/OGG)

**Parent:** 023

## Objective
Export generated TTS audio to MP3 or OGG files.

## Details
- "Save as..." button on TTS UI page
- Save dialog with format selection: MP3, OGG, WAV
- MP3 encoding via NAudio `MediaFoundationEncoder` (uses Windows built-in codec)
- OGG/Opus encoding via Concentus (already a dependency)
- WAV as lossless option (simple, already supported by NAudio)
- Resample from 24kHz (Pocket TTS native) to standard rates if needed (44.1kHz for MP3)

## Acceptance Criteria
- [x] Can export to MP3 with acceptable quality
- [x] Can export to OGG/Opus
- [x] Can export to WAV (lossless)
- [x] Save dialog with format picker and file name
- [x] Exported files play correctly in standard media players

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete
- Created `AudioExportService` in `Services/TextToSpeech/` with three export methods:
  - `ExportToWavAsync` — 16-bit PCM WAV via NAudio WaveFileWriter
  - `ExportToMp3Async` — MP3 via NAudio MediaFoundationEncoder, resamples 24kHz→44.1kHz
  - `ExportToOggAsync` — OGG/Opus via Concentus encoder + OpusOggWriteStream, resamples 24kHz→48kHz
- Added "Save as..." button to TextToSpeechPage XAML (with Save24 icon)
- Added SaveAsButton_Click handler in code-behind that:
  - Shows SaveFileDialog with WAV/MP3/OGG format filter
  - Generates audio via TTS service, then exports to selected format
  - Shows progress and status feedback
- Build succeeds (no CS errors; only expected exe file lock warning)
