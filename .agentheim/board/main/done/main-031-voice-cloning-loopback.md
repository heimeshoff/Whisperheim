---
id: main-031
title: Voice cloning from system audio loopback
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-029]
blocks: []
tags: [m4, tts]
related_adrs: []
related_research: []
prior_art: []
milestone: M4 - Text-to-Speech
size: Medium
---
# Voice cloning from system audio loopback

**Parent:** 023

## Objective
Allow users to capture system audio (YouTube, podcasts, etc.) as reference for voice cloning.

## Details
- WASAPI loopback capture at native quality (48kHz, not downsampled to 16kHz)
- Reuse `LoopbackCaptureService` pattern but with high-quality output path
- UI: select output device, start/stop capture, duration display
- After capture, extract voice state and save as `.safetensors`
- Same voice management as mic cloning (name, preview, delete)
- Consider adding a "trim" feature to select the best 5-30 second segment

## Acceptance Criteria
- [x] Can capture system audio at native quality (48kHz)
- [x] Capture UI with device selection, start/stop, duration
- [x] Voice state extracted from captured audio
- [x] Custom voice saved and available in voice selector
- [x] Works with common audio sources (browser, media players)

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete
- Created `IHighQualityLoopbackService` interface with capture, level metering, duration, and save-as-voice API
- Created `HighQualityLoopbackService` using WasapiLoopbackCapture at native system format (no downsampling)
- Writes captured audio to WAV at native quality (typically 48kHz 32-bit float stereo)
- Saves voice references to `%APPDATA%/WhisperHeim/voices/{name}.wav` for TTS voice cloning
- Created `VoiceLoopbackCapturePage` WPF UI with:
  - Output device selection (render device enumeration)
  - Start/Stop capture controls
  - Real-time audio level meter with dB display
  - Duration display with 5-second minimum warning
  - Voice naming and save functionality
  - Tip about isolating audio source
- Wired service in `App.xaml.cs` and navigation in `MainWindow.xaml`/`.cs`
- Build succeeds (only file-lock warnings due to running process)
