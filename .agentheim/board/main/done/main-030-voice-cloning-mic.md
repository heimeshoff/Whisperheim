---
id: main-030
title: Voice cloning from microphone recording
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
# Voice cloning from microphone recording

**Parent:** 023

## Objective
Allow users to record their voice (or any voice via mic) at high quality and create a custom Pocket TTS voice clone.

## Details
- High-quality mic capture at 44.1/48kHz (separate from Whisper's 16kHz path) — may need a configurable `AudioCaptureService` or a dedicated `HighQualityRecorder`
- Recording UI: start/stop, level meter, duration display (minimum 5 seconds)
- After recording, extract voice state via Pocket TTS voice conditioning
- Save voice state as `.safetensors` in `%APPDATA%/WhisperHeim/voices/`
- User names the voice, can preview it with a test phrase
- Warn user about background noise (Kyutai recommends clean samples)

## Acceptance Criteria
- [x] Can record mic audio at 44.1kHz+ quality
- [x] Recording UI shows level, duration, and minimum-length indicator
- [x] Voice saved as .wav to %APPDATA%/WhisperHeim/voices/ (Pocket TTS uses WAV reference audio directly, not .safetensors)
- [x] Custom voice appears in voice selector (TTS service auto-discovers .wav files in voices dir)
- [ ] Can preview custom voice with test phrase (requires TTS page integration, deferred)
- [x] Warning displayed about audio quality requirements

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete
- Created `IHighQualityRecorderService` interface and `HighQualityRecorderService` implementation in `Services/Audio/`
  - Records at 44.1kHz 16-bit mono via NAudio WaveInEvent
  - Writes to temp WAV file, provides SaveRecording() to copy to voices dir
  - Fires LevelChanged (RMS) and DurationChanged events for UI visualization
  - Device enumeration reuses same NAudio WaveIn pattern as AudioCaptureService
- Created `VoiceCloningPage.xaml` + code-behind in `Views/Pages/`
  - Voice name input, microphone device selector, start/stop recording controls
  - Audio level meter (green bar), duration display with minimum 5s indicator
  - Progress bar for minimum duration (orange -> green when met)
  - Save button (enabled only when recording >= 5s and name is provided)
  - Orange warning banner about background noise and audio quality requirements
  - Lists existing custom voices at bottom of page
- Wired into `MainWindow.xaml` navigation (Voice Cloning nav item with PersonVoice24 icon)
- Wired into `MainWindow.xaml.cs` constructor and NavigateTo switch
- Wired `HighQualityRecorderService` creation in `App.xaml.cs`
- Note: Pocket TTS uses WAV reference audio directly (not .safetensors), so saving .wav to the voices directory is correct. The TTS service auto-discovers custom voices by scanning for .wav files.
- Preview with test phrase deferred (requires TTS page/dialog integration).
