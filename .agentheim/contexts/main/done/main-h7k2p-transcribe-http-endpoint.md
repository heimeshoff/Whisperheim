---
id: main-h7k2p
title: STT API — POST /transcribe HttpListener server + queue integration
status: done
type: feature
context: main
created: 2026-06-19
completed: 2026-06-19
depends_on: []
blocks: [main-q4m8t]
tags: [api, transcription, http, claude]
related_adrs: [0001]
related_research: [whisperheim-stt-api-exposure-2026-06-19]
prior_art: []
---

## Why
Claude (Claude Code) needs to send an audio file to WhisperHeim and get the
transcript back — the inverse of Utterheim's local *speak* endpoint. Today the
STT engine is reachable only through the WPF UI (Conversations tab / drag-and-drop).
This task exposes it as a local API so other first-party tooling can drive
transcription headlessly. Transport + shape are fixed by ADR-0001.

## What
A BCL-only HTTP/1.1 server built on `System.Net.HttpListener`, bound loopback-only
to `http://127.0.0.1:7777/`, running inside the tray process. It funnels requests
through the existing single-engine `TranscriptionQueueService` (it does **not**
reimplement decode or ASR). Synchronous shape: the handler enqueues the audio,
blocks until that queue item completes, and returns the transcript as JSON.

Endpoints (v1, per ADR-0001):
- `POST /transcribe` — raw audio bytes in the body → `200` JSON
  `{text, audioDurationSeconds, transcriptionDurationSeconds, realTimeFactor, chunkCount}`.
- `GET /health` — `200` `{status:"ok", busy:<bool>, queueDepth:<int>}`.

Integration approach (from the architect's notes):
- Write the request body to a temp file (preserve extension via `?filename=`/`X-Filename`),
  then `var item = queue.EnqueueFile(tempPath)` — reuses `FileTranscriptionService`'s
  NAudio/ffmpeg → 16 kHz mono decode verbatim. Do **not** use `TryAcquire`/`Release`
  (that rejects-when-busy and bypasses FIFO ordering).
- Bridge completion to a `Task` by subscribing to `ItemCompleted`/`ItemFailed`
  filtered by `item.Id`. Prefer adding a small `WaitForItemAsync(Guid id)` helper
  on `TranscriptionQueueService` rather than per-handler event juggling.
- `TranscriptionQueueItem` currently exposes only `ResultText`, not the full
  `FileTranscriptionResult`. Extend the item to carry the `FileTranscriptionResult`
  (additive field) so the response metadata (duration/RTF/chunkCount) can be returned.
- Construct + start the server in `App.xaml.cs StartupCore` (next to where
  `TranscriptionQueueService` is built); run the accept loop on a background thread
  like the existing `FfmpegDetector.DetectAsync()` kick-off. Stop/dispose it in the
  app shutdown path (`OnExit`).
- Bind the literal `127.0.0.1` prefix (avoids the http.sys URL-ACL/netsh wrinkle
  that non-`localhost` prefixes hit).

## Acceptance criteria
- [ ] Server binds `http://127.0.0.1:<port>/` (default `7777`), overridable via
      `WHISPERHEIM_TRANSCRIBE_PORT`; a port-in-use / bind failure logs a clear error
      (`Trace.TraceError`) and does **not** crash app startup (tray app still runs).
- [ ] `POST /transcribe` with an OGG/MP3/M4A/WAV body returns `200` with
      `{text, audioDurationSeconds, transcriptionDurationSeconds, realTimeFactor, chunkCount}`
      whose values match the engine's `FileTranscriptionResult`.
- [ ] The request funnels through `TranscriptionQueueService.EnqueueFile`; a second
      concurrent request **queues behind** the first (is not rejected) and both
      return correct transcripts in FIFO order.
- [ ] Empty/no-speech audio returns `200` with `"text": ""` (not the UI's
      `"(No speech detected)"` sentinel).
- [ ] Engine/decode failure returns `500` (or `415`/`400` for unsupported/corrupt
      audio) with `{"error":"<message>"}`; empty body returns `400`.
- [ ] Temp files are cleaned up after every request (success or failure).
- [ ] `GET /health` returns `{status:"ok", busy:<bool>, queueDepth:<int>}`.
- [ ] Server stops and disposes cleanly on app exit — no leaked `HttpListener` or
      background thread.

## Notes
- Decision + full wire contract + rejected alternatives: ADR-0001
  (`.agentheim/knowledge/decisions/0001-transcribe-endpoint-loopback-http.md`).
- Option space + trade-offs + Utterheim house pattern: research report
  `whisperheim-stt-api-exposure-2026-06-19` (§4 hosting, §7 Utterheim, §6 security).
- Sibling reference implementation (different transport — Kestrel — but same posture):
  `...utterheim/src/Utterheim/Services/Http/SpeakServer.cs`.
- Blocks `main-q4m8t` (the `whisperheim-transcribe` CLI wrapper), which needs this
  live endpoint to test against.
- Out of scope (deferred by ADR-0001): auth, OpenAI `/v1/audio/transcriptions`
  compatibility, async-accept+poll, live streaming.

## Outcome
Implemented the loopback STT API exactly per ADR-0001. The engine is exposed over a
BCL-only `HttpListener` bound to `http://127.0.0.1:7777/` (override via
`WHISPERHEIM_TRANSCRIBE_PORT`), serving `POST /transcribe` (audio body → full transcript
JSON) and `GET /health`. Requests funnel through the existing single-engine
`TranscriptionQueueService.EnqueueFile` (queue-and-block FIFO; never rejects when busy).

Design split for testability:
- `TranscribeRequestHandler` — transport-agnostic core: routing, temp-file lifecycle,
  error mapping (400 empty body / 415 unsupported / 500 engine failure / 404 / 405),
  and response shaping. Fully unit-tested against a fake `ITranscribeEngine`.
- `TranscribeServer` — thin `HttpListener` adapter: background accept loop, bind-failure
  is logged via `Trace.TraceError` and non-fatal, clean shutdown via `IDisposable`.
- `QueueTranscribeEngine` — adapts `TranscriptionQueueService` onto `ITranscribeEngine`,
  keeping HTTP concerns out of the queue.

Queue changes (additive): `TranscriptionQueueItem.Result` carries the full
`FileTranscriptionResult` (raw text, not the UI "(No speech detected)" sentinel), and
`TranscriptionQueueService.WaitForItemAsync(Guid)` bridges completion to a `Task`.

Wired into `App.xaml.cs StartupCore` (constructed next to the queue, started immediately)
and disposed in `OnAppExit`.

Key files:
- `src/WhisperHeim/Services/Http/ITranscribeEngine.cs`
- `src/WhisperHeim/Services/Http/TranscribeHttp.cs`
- `src/WhisperHeim/Services/Http/TranscribeRequestHandler.cs`
- `src/WhisperHeim/Services/Http/TranscribeServer.cs`
- `src/WhisperHeim/Services/Http/QueueTranscribeEngine.cs`
- `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs` (Result field + WaitForItemAsync)
- `src/WhisperHeim/App.xaml.cs` (composition + shutdown)
- `tests/WhisperHeim.Tests/TranscribeRequestHandlerTests.cs` (12 tests)
- `tests/WhisperHeim.Tests/TranscribeServerTests.cs` (13 tests, real HttpListener over loopback)

All 123 tests pass. No ADR written (ADR-0001 already fixes the decisions; the
testability split is routine implementation). Decisions + wire contract: ADR-0001.
