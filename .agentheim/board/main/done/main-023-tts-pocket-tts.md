---
id: main-023
title: Kyutai Pocket TTS Integration
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: []
blocks: []
tags: [m4, tts]
related_adrs: []
related_research: []
prior_art: []
milestone: M4 - Text-to-Speech
size: Large (broken into subtasks 029–035)
---
# Kyutai Pocket TTS Integration

## Objective
Integrate Kyutai Pocket TTS for local text-to-speech with voice cloning, a "read selected text" global hotkey, and audio file export.

## Overview

Pocket TTS is a 100M-parameter, CPU-only, open-source TTS model by Kyutai (MIT code, CC-BY-4.0 weights). It supports voice cloning from as little as 5 seconds of reference audio. The sherpa-onnx NuGet package (already a dependency) includes C# bindings for Pocket TTS, enabling native .NET integration without Python.

## Feature Set

### 1. TTS Engine (Task 029)
- Integrate Pocket TTS via sherpa-onnx C# bindings (or ONNX model directly)
- Model download via existing ModelManagerService
- `ITextToSpeechService` interface: `GenerateAudioAsync(text, voiceState)` + `GenerateAudioStreamAsync(text, voiceState)`
- Audio playback via NAudio `WaveOutEvent`
- Support 8 built-in voices: alba, marius, javert, jean, fantine, cosette, eponine, azelma

### 2. Voice Cloning (Tasks 030–031)
- **From recording (Task 030):** Record mic audio at 44.1/48kHz (high quality, separate from 16kHz Whisper path) as voice reference
- **From system audio (Task 031):** Capture loopback audio at native quality for cloning from any playing source (YouTube, podcast, etc.)
- Extract voice state from reference audio, save as `.safetensors` for instant reloading
- Voice management: name, preview, delete custom voices
- Kyutai recommends clean audio — warn user about background noise

### 3. Read Selected Text (Task 032)
- Global hotkey (user-configurable) triggers "read aloud"
- Cascading text capture:
  1. Try UI Automation `TextPattern.GetSelection()` (clean, no side effects)
  2. Fall back to simulated Ctrl+C via `SendInput` + clipboard read/restore
- Pass captured text to TTS engine with currently selected voice
- Works in browsers, editors, any Windows application

### 4. TTS UI Page (Task 033)
- Text input field for paste-and-read
- Voice selector (built-in + custom voices)
- Play/Stop controls
- Voice preview button
- Status indicator during generation

### 5. Audio Export (Task 034)
- Export generated speech to MP3 or OGG file
- Save dialog with format selection
- MP3 encoding via NAudio's `MediaFoundationEncoder` or LAME
- OGG encoding via Concentus (already a dependency)

### 6. Settings & Hotkey Config (Task 035)
- TTS settings: default voice, read-aloud hotkey, playback device
- Persist in existing AppSettings

## Technical Approach

- **Model:** Kyutai Pocket TTS (ONNX variant from HuggingFace)
- **Integration:** sherpa-onnx C# bindings (`org.k2fsa.sherpa.onnx` NuGet, already referenced)
- **Audio output:** NAudio `WaveOutEvent` for playback, 24kHz sample rate (Pocket TTS native)
- **Voice cloning:** High-quality capture at 44.1/48kHz, separate from Whisper's 16kHz path
- **Text capture:** UI Automation + SendInput Ctrl+C fallback chain
- **Export:** NAudio for WAV/MP3, Concentus for OGG

## Acceptance Criteria
- [ ] Pocket TTS model auto-downloads on first use
- [ ] Can generate and play speech from text with any built-in voice
- [ ] Can clone a voice from mic recording (≥5 seconds)
- [ ] Can clone a voice from system audio loopback
- [ ] Custom voices persist as .safetensors and appear in voice selector
- [ ] Global hotkey reads selected text aloud from any application
- [ ] Text field allows paste-and-read with Play/Stop
- [ ] Generated speech can be exported as MP3 or OGG
- [ ] All settings (voice, hotkey, output device) are configurable and persisted

## Subtasks
- **029** — Pocket TTS engine service + model download + built-in voice playback
- **030** — Voice cloning from microphone recording (high-quality capture)
- **031** — Voice cloning from system audio loopback
- **032** — Read selected text via global hotkey (UI Automation + Ctrl+C fallback)
- **033** — TTS UI page (text input, voice selector, play/stop)
- **034** — Audio export (MP3/OGG)
- **035** — TTS settings + hotkey configuration

## Research Notes
- Pocket TTS: https://github.com/kyutai-labs/pocket-tts (MIT license)
- ONNX variant: https://huggingface.co/KevinAHM/pocket-tts-onnx
- sherpa-onnx C# bindings: https://github.com/k2-fsa/sherpa-onnx
- Voice cloning needs clean reference audio; warn about noise
- CPU-only, ~200ms first-chunk latency, RTF ~0.17 (6x real-time)
- English only

## Work Log
<!-- Appended by /work during execution -->
