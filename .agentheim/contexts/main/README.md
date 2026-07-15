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
- **Imported voice message speaker name** (main-k4t8p) — importing an audio file (file picker, `Multiselect`) prompts once per file, in selection order, for the name of the (single, non-local) speaker. Empty/dismissed falls back to the literal label `"Speaker"`, same as before this task. The resolved name is written to the segment's `Speaker` and seeded into `CallTranscript.RemoteSpeakerNames`, so it shows as a row in the SPEAKER NAMES panel (renaming there reuses the same `RenameSpeakerGlobally` machinery main-037 built for recordings) and flows into the Markdown heading (`### <name>`) via the existing `GetDisplaySpeaker` resolution. Scoped to imports that carry a session directory (`transcript.json`) — the ephemeral STT API path (`POST /transcribe`, `whisperheim-transcribe`) has no UI and stays untouched. Pure resolution logic lives in `FileImportTranscriptBuilder`.
- **Template** — named text snippet inserted at cursor via a separate hotkey + voice trigger (e.g. "greeting").
- **Pill / Overlay** — the on-screen indicator that shows mic state and a live waveform while dictation is active.
- **Origin machine** — the host that captured a recording; owns transcription so multi-device sync doesn't double-process.
- **STT API** — a loopback-only HTTP endpoint (`POST /transcribe`, default `127.0.0.1:7777`) that exposes the shared transcription engine to first-party local tooling (e.g. Claude). Synchronous: audio file in → full transcript JSON out, funnelled through the same transcription queue as the UI. No auth, batch-only in v1 (ADR-0001).
- **whisperheim-transcribe** — the thin CLI wrapper (`WhisperHeim.Cli` project, `whisperheim-transcribe.exe`) over `POST /transcribe`. `whisperheim-transcribe <file>` POSTs the file's raw bytes and prints only the transcript `text` to stdout. Honors `WHISPERHEIM_ENDPOINT`; exit codes 0 success / 1 usage-or-file-error / 2 HTTP error / 3 endpoint unreachable. Ships alongside the tray exe (mirrors Utterheim's `utterheim-speak`).
- **Auto-export (Markdown)** (main-m6x4v, ADR-0008) — two independent, machine-local Settings folders (recorded conversations, imported voice messages). On transcription completion, `TranscriptAutoExportService` (a standalone `TranscriptionQueueService.ItemCompleted` subscriber, wired in `App.xaml.cs`) writes `<recording name>.md` into the matching folder via the shared `TranscriptMarkdownFormatter.Format` (also used by the manual "MD" export button). Overwrite identity is the Recording-session directory, tracked on `CallTranscript.ExportedMarkdownPath` — re-transcribing the same, unrenamed session overwrites its file in place; two sessions sharing a title disambiguate as `name.md` / `name (2).md`, decided by scanning sibling transcripts' own `ExportedMarkdownPath` claims, never by inspecting what's already on disk. Missing/unwritable folders are a silent skip (logged warning) — a successful transcription is never retroactively marked failed by an export-time problem. `ExportFileName.Sanitize` derives the Windows-safe base filename.

## Aggregates
- **Recording session** — protects per-session folder integrity (mic.wav, system.wav, transcript.json kept together; deletion removes the whole folder).
- **Transcription queue** — protects single-engine-busy invariant (ASR engine is a single resource; queue serializes work).
- **Template library** — protects template name uniqueness + system-template grouping.

## Key events
- `DictationStarted` / `DictationStopped`
- `RecordingStarted` / `RecordingStopped` / `RecordingTranscribed`
- `TranscriptionQueued` / `TranscriptionCompleted` / `TranscriptionFailed`
- `TemplateTriggered`
- `TemplateNoMatch` — template-mode dictation matched no template. Now an invitation to create one (main-t9w2k): a centered modal pre-fills the (possibly misheard, editable) trigger term + a replacement body and persists via `AddTemplate`. **Save-only** — creating does not type the body into the focused app.
- `ModelDownloadCompleted` (first-run UX)

## Key commands
- `StartDictation` / `StopDictation`
- `StartRecording` / `StopRecording`
- `TranscribeAudioFile` (drag-and-drop, or via the STT API's `POST /transcribe`). Format policy (main-r7n2k): OGG/MP3/M4A/WAV decode natively; **any other extension is attempted via FFmpeg** rather than rejected. The decoder is the authority — the file-picker's "supported formats" list is only a display hint. A missing FFmpeg for a non-native format surfaces a distinct "requires FFmpeg" error (HTTP **501**, never a generic 500); a present-but-undecodable file is "corrupt or not audio" (HTTP **415**). The decode path never blocks on the FFmpeg install modal (main-110).
- `RenderTemplate`
- `DownloadModel`

## Relationships with other contexts
- **Depends on:** `infrastructure/` for runtime (Velopack bootstrap, FFmpeg detection, release pipeline, settings/data path resolution).

## Open questions
- M3 Telegram bot integration (task `main-022`) still in backlog — stretch goal.
- M4 TTS feature was removed (task `main-103`); future re-introduction would need its own BC discussion.

## Notes
This BC's task numbering is flat (no `main-` prefix in the original `.workflow/`); the migration to `.agentheim/` adopted the `main-NNN` convention for filenames and frontmatter ids while preserving the existing numeric sequence.

**Microphone device selection (main-v7k2d, main-c3x7q, ADR-0009):** the saved
microphone name in `Dictation.AudioDevice` is honored by every live capture
path — it is the single setting shared across dictation and recording, not a
per-feature device choice. `DictationOrchestrator.OnHotkeyPressed`
(hold-to-talk) resolves it via
`AudioDeviceResolver.ResolveDeviceIndex(_audioCapture, savedDeviceName)` on
every hotkey press (no caching, so a settings change takes effect on the very
next press) and passes the result to `AudioCaptureService.StartCapture`.
`DictationPipeline` (VAD path) does the same. `CallRecordingService.StartRecording`
(driven by both the `TranscriptsPage` record button and the call-recording
hotkey) resolves it the same way via the internal
`ResolveMicDeviceIndex(captureService, savedDeviceName)` seam, fresh on every
recording start — `StartRecording()`/`ToggleRecording()` no longer take a
`micDeviceIndex` parameter; resolution is centralized inside the service
instead of duplicated in each caller. `-1` from the resolver means "use the
real system default" (NAudio `WAVE_MAPPER`) and is passed straight through to
`WaveInEvent.DeviceNumber` — it must **not** be clamped to device `0`, which
is a different (and possibly wrong) device.
`HighQualityRecorderService`/`IHighQualityRecorderService` still has that
clamp, but that service is unused dead code (nothing calls its
`StartRecording`) — deleting it is an out-of-scope tidy item noted on
main-c3x7q, not a live bug.
