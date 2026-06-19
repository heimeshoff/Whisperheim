# main

## Purpose
The whole WhisperHeim app — the single bounded context for live dictation, call transcription, voice-message transcription, and (historically) text-to-speech. The project shipped as one unified tray app, so all domain work flows through this BC.

## Classification
core

This BC *is* the product. There are no supporting or generic domains carved out of it. Infra concerns (Velopack pipeline, GitHub release workflow, code signing, FFmpeg detection) live in `contexts/infrastructure/`; everything user-facing lives here.

## Actors
- **Power user on Windows 11** — dictates into any application via a global hotkey, records calls (Zoom / Teams / Meet) for after-the-fact transcription, drags voice messages onto the tray icon to transcribe.

## Ubiquitous language
- **Dictation** — live, streaming speech-to-text inserted at the cursor of the focused window via `SendInput`. Latency target <2s.
- **Recording / Call** — a captured session combining microphone audio (`mic.wav`) and system audio (`system.wav` via WASAPI loopback). Persists as a folder under `recordings/YYYYMMDD_HHMMSS/` with a `transcript.json`.
- **Transcript** — timestamped, speaker-attributed text produced from a recording. Speakers are `You` (mic) and `Remote` (loopback) under the VAD-per-stream model; diarization is reserved for single-stream cases.
- **Template** — named text snippet inserted at cursor via a separate hotkey + voice trigger (e.g. "greeting").
- **Pill / Overlay** — the on-screen indicator that shows mic state and a live waveform while dictation is active.
- **Origin machine** — the host that captured a recording; owns transcription so multi-device sync doesn't double-process.
- **STT API** — a loopback-only HTTP endpoint (`POST /transcribe`, default `127.0.0.1:7777`) that exposes the shared transcription engine to first-party local tooling (e.g. Claude). Synchronous: audio file in → full transcript JSON out, funnelled through the same transcription queue as the UI. No auth, batch-only in v1 (ADR-0001).
- **whisperheim-transcribe** — the thin CLI wrapper (`WhisperHeim.Cli` project, `whisperheim-transcribe.exe`) over `POST /transcribe`. `whisperheim-transcribe <file>` POSTs the file's raw bytes and prints only the transcript `text` to stdout. Honors `WHISPERHEIM_ENDPOINT`; exit codes 0 success / 1 usage-or-file-error / 2 HTTP error / 3 endpoint unreachable. Ships alongside the tray exe (mirrors Utterheim's `utterheim-speak`).

## Aggregates
- **Recording session** — protects per-session folder integrity (mic.wav, system.wav, transcript.json kept together; deletion removes the whole folder).
- **Transcription queue** — protects single-engine-busy invariant (ASR engine is a single resource; queue serializes work).
- **Template library** — protects template name uniqueness + system-template grouping.

## Key events
- `DictationStarted` / `DictationStopped`
- `RecordingStarted` / `RecordingStopped` / `RecordingTranscribed`
- `TranscriptionQueued` / `TranscriptionCompleted` / `TranscriptionFailed`
- `TemplateTriggered`
- `ModelDownloadCompleted` (first-run UX)

## Key commands
- `StartDictation` / `StopDictation`
- `StartRecording` / `StopRecording`
- `TranscribeAudioFile` (drag-and-drop, or via the STT API's `POST /transcribe`)
- `RenderTemplate`
- `DownloadModel`

## Relationships with other contexts
- **Depends on:** `infrastructure/` for runtime (Velopack bootstrap, FFmpeg detection, release pipeline, settings/data path resolution).

## Open questions
- M3 Telegram bot integration (task `main-022`) still in backlog — stretch goal.
- M4 TTS feature was removed (task `main-103`); future re-introduction would need its own BC discussion.

## Notes
This BC's task numbering is flat (no `main-` prefix in the original `.workflow/`); the migration to `.agentheim/` adopted the `main-NNN` convention for filenames and frontmatter ids while preserving the existing numeric sequence.
