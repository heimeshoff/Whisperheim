---
id: main-042
title: TTS Voice Pre-Caching on Startup
status: done
type: feature
context: main
created: 
completed: 2026-03-22
commit:
depends_on: [main-041]
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: —
size: Small
---
# TTS Voice Pre-Caching on Startup

## Description

Pre-cache the default TTS voice on startup so the first read-aloud hotkey press is instant. Currently the Mimi encoder runs on first use (~1-3s delay). By warming up the model and default voice in a background thread after the UI is up, this cost is hidden from the user.

## Acceptance Criteria

- [x] Enable `VoiceEmbeddingCacheCapacity` (e.g. 10) in `TextToSpeechService.LoadModel()` so sherpa-onnx caches encoder output across calls
- [x] After UI is loaded, a background thread loads the TTS model and runs a short dummy generation with the default voice to populate the embedding cache
- [x] The default voice's WAV samples are cached in memory (avoid disk I/O on each generation call)
- [x] First read-aloud hotkey press uses the warm cache — no encoder delay
- [x] Startup UI remains responsive (warm-up is fully async/background)
- [x] If no default voice is configured, skip warm-up gracefully

## Implementation Notes

- Add `VoiceEmbeddingCacheCapacity = 10` to `OfflineTtsConfig` in `LoadModel()`
- Cache loaded WAV samples in a `Dictionary<string, (float[] Samples, int SampleRate)>` keyed by voice ID, populated during warm-up and reused in `CreateGenerationConfig()`
- Trigger warm-up from `App.xaml.cs` or `MainWindow` after UI is shown, on `Task.Run`
- Dummy generation can be a single short phrase (e.g. "ready") — just enough to force the encoder to run and cache the embedding
- Read `TtsSettings.DefaultVoiceId` to know which voice to warm up

## Work Log

**2026-03-22:** Implemented TTS voice pre-caching on startup.

### Changes Made

1. **TextToSpeechService.cs** — Added `VoiceEmbeddingCacheCapacity = 10` to model config; added `_wavSampleCache` dictionary for in-memory WAV sample caching; added `GetOrLoadWavSamples()` helper that caches loaded samples and is used by `CreateGenerationConfig()`; added `WarmUpAsync()` method that loads the model, pre-caches WAV samples, and runs a dummy generation ("ready") to populate the sherpa-onnx voice embedding cache.

2. **ITextToSpeechService.cs** — Added `WarmUpAsync(string? defaultVoiceId)` to the interface.

3. **App.xaml.cs** — After UI startup, fires `Task.Run` to call `WarmUpAsync` with the configured `DefaultVoiceId`. Failure is logged but non-fatal.

### Acceptance Criteria Status
All 6 criteria met. Build succeeds with 0 warnings, 0 errors.

### Files Changed
- `src/WhisperHeim/Services/TextToSpeech/TextToSpeechService.cs`
- `src/WhisperHeim/Services/TextToSpeech/ITextToSpeechService.cs`
- `src/WhisperHeim/App.xaml.cs`
