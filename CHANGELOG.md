# Changelog

All notable changes to Whisperheim are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.3] - 2026-06-28

RAM-optimization release: the idle footprint of the Parakeet recognizer is now
much smaller, and the first dictation after a cold start behaves correctly.

### Added
- Lazy-load, keep-warm, and idle-unload of the Parakeet recognizer — the model
  loads on first use and unloads after a period of inactivity to reclaim memory.
- "Warming-up" overlay state shown when an utterance arrives before the model
  has finished loading, so a fast first press is no longer silently dropped.
- One-shot LOH-compacting GC after startup, and working-set trimming after model
  load and on idle.

### Changed
- Switched from Server GC to Workstation GC (concurrent) to lower idle memory.
- Reduced Parakeet ASR intra-op threads from 4 to 2.

### Fixed
- First dictation overlay rendered at the wrong position; the overlay is now
  pre-warmed so its first show lands bottom-center.

## [0.1.2] - 2026-06-19

Turns Whisperheim into something other tools can call: a local HTTP
transcription API plus a Claude Code plugin.

### Added
- Local STT API: `POST /transcribe` served by an in-process HttpListener, wired
  into the existing transcription queue (ADR-0001).
- `whisperheim-transcribe` CLI wrapper and Claude Code plugin (`/transcribe`
  command) over the new endpoint.
- Status footer showing the STT API endpoint and health.
- FFmpeg fallback that transcodes any unsupported audio format (e.g. `.opus`)
  before transcription.

### Changed
- Renamed the brand display name from "WhisperHeim" to "Whisperheim".

## [0.1.1] - 2026-05-12

### Fixed
- `FirstRunSetupWindow` binding crash on a clean install (Task 108).

## [0.1.0] - 2026-05-12

First public release. Local-only audio Swiss army knife for Windows 11 — no
cloud, no subscription, no internet at runtime.

### Added
- **Dictation** — hold-to-talk global hotkey (`Ctrl + Win`) that streams speech
  straight into the focused text field via Win32 `SendInput`, with Silero VAD
  for speech-boundary detection and a pill-shaped waveform overlay at the cursor.
- **Call & file transcription** — dual microphone + WASAPI loopback capture with
  speaker diarization (out-of-process, crash-safe), a transcription queue, a
  unified recordings/files page, drag-and-drop file transcription, and a
  transcript viewer with export, search, column sorting, and per-segment speaker
  editing.
- **Streams** — transcription of video/audio links.
- **Templates** — voice-triggered and hotkey-triggered text expansion
  (`Ctrl + Win + Alt`), with grouping and built-in system templates.
- **Transcript analysis** via a local Ollama LLM (opt-in).
- **Speech-to-text** powered by Parakeet TDT via sherpa-onnx, with a model
  manager that auto-downloads Parakeet on first run; Silero VAD and Pyannote
  segmentation models ship bundled in the installer.
- **Deterministic clean-text pipeline** (filler-word removal).
- **Settings & UX** — Fluent window with Mica backdrop, light/dark themes, tray
  icon with context menu, Windows startup auto-launch, remembered window
  size/position, start-minimized, configurable data/config path for cloud sync,
  and hot-reload of settings from disk for multi-machine use.
- **Packaging & release** — Velopack installer, tag-triggered GitHub Actions
  release workflow, bundled VAD/segmentation models, FFmpeg detection with a
  first-use install prompt, first-run model-download dialog, uninstall data
  preservation, and a deferred code-signing hook.

### Removed
- Text-to-Speech (Kyutai Pocket TTS) — built during development and then removed
  before release (Task 103).

[0.1.3]: https://github.com/heimeshoff/WhisperHeim/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/heimeshoff/WhisperHeim/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/heimeshoff/WhisperHeim/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/heimeshoff/WhisperHeim/releases/tag/v0.1.0
