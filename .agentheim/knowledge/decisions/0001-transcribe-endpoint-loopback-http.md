---
id: 0001
title: Expose WhisperHeim's transcribe endpoint over loopback HTTP (HttpListener, synchronous)
scope: global
status: accepted
date: 2026-06-19
supersedes: []
superseded_by: []
related_tasks: [main-h7k2p, main-q4m8t]
related_research: [whisperheim-stt-api-exposure-2026-06-19]
---

# ADR 0001: Expose WhisperHeim's transcribe endpoint over loopback HTTP (HttpListener, synchronous)

## Context
WhisperHeim must expose its offline STT engine (Sherpa-ONNX / Parakeet, single
serialized `TranscriptionQueueService`) as a **local API** so Claude (Claude Code)
can send an audio file and get the transcript back — the inverse of the sibling
TTS app Utterheim's *speak* endpoint (Utterheim ADR-0003).

v1 scope is fixed and narrow:
- **Batch only.** Caller sends a whole audio file / PCM buffer, gets the full
  transcript back. No live streaming / partial results.
- **Consumer is local + first-party** (Claude, via a CLI wrapper). Not a browser,
  not cloud. Therefore: **no auth and no OpenAI-API compatibility in v1**;
  loopback-only.
- A `whisperheim-transcribe <file>` CLI wrapper ships alongside the tray exe,
  mirroring `utterheim-speak`.

The research report `whisperheim-stt-api-exposure-2026-06-19` (see §7) surveyed
the option space and resolved the Utterheim house pattern: Kestrel Minimal API on
`127.0.0.1:7223`, loopback-only, no auth, hosted as an `IHostedService` on the
.NET Generic Host, fronted by a CLI wrapper. Three transports are live for
WhisperHeim: **named pipe** (BCL-only, no open port), **HttpListener loopback**
(BCL-only, curl-able), and **Kestrel/ASP.NET Core** (house-consistent, but pulls
in the ASP.NET Core runtime ~11 MB *and* requires adopting the Generic Host).

Two facts shape the decision:
1. **WhisperHeim is not on the Generic Host.** `Program.cs` is a custom Velopack
   `[STAThread] Main`; the project is `Microsoft.NET.Sdk` (not `.Sdk.Web`);
   services are wired by hand in `App.xaml.cs` (`StartupCore`), not via DI/host.
   Adopting Kestrel means adopting `Host.CreateDefaultBuilder` + the ASP.NET Core
   runtime — a structural change disproportionate to one endpoint.
2. **The single shared engine is the real constraint, not the transport.** Every
   surface must serialize through `TranscriptionQueueService`, which processes one
   item at a time. The workload is request→response ("file in → text out"), the
   opposite of Utterheim's fire-and-forget `POST /speak` (202 + poll).

## Decision
Expose the engine as an **HTTP/1.1 server built on `System.Net.HttpListener`**,
bound **loopback-only** to `http://127.0.0.1:7777/` (default; overridable — see
below), running inside the WhisperHeim tray process. BCL-only: no ASP.NET Core
runtime, no Generic Host.

The API shape is **synchronous block-and-return-text**. The handler enqueues the
audio onto `TranscriptionQueueService` and holds the HTTP connection open until
that item reaches a terminal stage, then returns the transcript in the response
body. This is the simplest possible contract for the CLI wrapper and for Claude:
one call in, one transcript out, no polling state machine.

### v1 wire contract

**`POST /transcribe`**
- **Request body:** raw audio bytes (the file's own bytes — OGG/MP3/M4A/WAV/etc.,
  whatever `IFileTranscriptionService` already decodes via NAudio/ffmpeg). The
  server writes the body to a temp file and feeds that path to the queue, reusing
  the existing decode→16 kHz-mono path. `Content-Type` is advisory and may be
  ignored in v1 (decode is sniffed/by-extension as today); an optional
  `?filename=foo.ogg` query hint or `X-Filename` header lets the server preserve
  the extension for the decoder.
- **Response (200 OK), `application/json`:**
  ```json
  {
    "text": "the full transcript",
    "audioDurationSeconds": 42.13,
    "transcriptionDurationSeconds": 7.05,
    "realTimeFactor": 0.17,
    "chunkCount": 3
  }
  ```
  Fields map 1:1 onto `FileTranscriptionResult`. Empty/no-speech audio returns
  `200` with `"text": ""` (the queue's `"(No speech detected)"` sentinel is a UI
  concern; the API returns the empty string so callers can branch cleanly).
- **`GET /health`** → `200 OK` `{"status":"ok","busy":<bool>,"queueDepth":<int>}`.
  Lets the CLI wrapper probe liveness and report "is WhisperHeim running?".

**Busy / error behavior (the load-bearing part):** because the engine is a single
FIFO queue, the API **queues-and-blocks — it does NOT reject when busy.** A
request that arrives while another transcription is in flight is enqueued behind
it and the connection stays open until its turn completes. This is correct for a
local single-user caller: Claude issues one transcription, waits, gets text; if it
issues two, the second simply waits. Concurrency is bounded only by HttpListener's
own accept loop. Error mapping:
- Engine failure / decode failure (`ItemFailed`) → **`500 Internal Server Error`**
  with `{"error":"<message>"}` (the queue item's `ErrorMessage`).
- Unsupported / corrupt audio that fails decode → **`415 Unsupported Media Type`**
  or `400` with the decoder's message.
- Empty body → **`400 Bad Request`** `{"error":"audio body is required"}`.
- Cancellation (client disconnects) → best-effort: the in-flight queue item is
  left to finish (cancellation of an already-running engine item is out of scope
  for v1; a disconnected client just abandons the result).

**Binding & auth:** bind **`127.0.0.1` only** (loopback; no `0.0.0.0`, no LAN
exposure, no Windows Firewall prompt). **No token/auth in v1**, matching
Utterheim's "single-user, localhost-only, by design" baseline (ADR-0003). The
DNS-rebinding/CORS risk from the research §6 does not apply: the consumer is a CLI,
not a browser; v1 sets no CORS headers and serves only the two endpoints above.

**Config override:** default port `7777` (distinct from Utterheim's `7223` so the
two apps coexist), overridable via env var `WHISPERHEIM_TRANSCRIBE_PORT` and/or an
`appsettings`/settings entry (mirror Utterheim's `UTTERHEIM_` scheme). The CLI
wrapper honors `WHISPERHEIM_ENDPOINT` (default `http://127.0.0.1:7777`). Port
collision surfaces a clear, logged error and the user overrides; the tray app
still starts even if the API cannot bind (non-fatal).

## Consequences
### Positive
- **No new runtime weight, no host migration.** BCL-only; nothing added to the
  Velopack payload; `App.xaml.cs` keeps its hand-wired composition root. The
  server starts on one background thread, like the existing `--diarize-worker`
  and `FfmpegDetector` patterns.
- **House-shaped and inspectable.** A base URL + JSON body that responds to curl,
  honoring a `WHISPERHEIM_`-prefixed override — conceptually identical to
  Utterheim from a caller's view, so the mental model transfers.
- **Synchronous shape is the minimum viable contract.** No request-id/poll state
  machine to build or for the CLI to drive; "file in → text out" maps directly
  onto the engine's request→response nature.
- **Reuses the one true seam.** Funnels through `TranscriptionQueueService`, the
  same path the Conversations / file-import surfaces use, so the engine stays
  serialized and there is exactly one place transcription happens.

### Negative
- **Diverges from Utterheim's transport** (HttpListener vs Kestrel). Accepted: the
  divergence buys avoiding the Generic Host migration and the ASP.NET runtime; the
  *posture* (loopback HTTP, no auth, env override, CLI wrapper) is preserved, which
  is what consistency actually needs to buy.
- **Hand-rolled HTTP plumbing.** The accept loop, body reading, and JSON
  serialization are written by hand (no Minimal API model binding). Small and
  well-understood, but it is code Utterheim got for free from Kestrel.
- **Long files hold a connection open.** A blocking call risks client-side
  timeouts on very long audio. Mitigated by setting a generous CLI/HttpClient
  timeout; if this ever bites, the documented escape hatch is to add an
  async-accept `202 + GET /status?id=` mode later, reusing the queue item's
  existing `Guid Id` and `Stage` — a non-breaking addition.

### Neutral
- A future remote/browser/third-party consumer would need an auth story, Host-
  header validation, and possibly OpenAI `/v1/audio/transcriptions` compatibility
  (research §2, §6). All explicitly deferred; v1 is loopback-only, first-party.
- True streaming / partial results are out of scope and likely require a streaming
  Sherpa model, not just a transport change (research §5). Not blocked by this ADR.

## Alternatives considered
- **Kestrel / ASP.NET Core Minimal API (Utterheim's transport).** Rejected for v1:
  maximal house-consistency, but requires adopting the .NET Generic Host (which
  WhisperHeim's custom Velopack `Main` does not use) and bundling the ASP.NET Core
  runtime (~11 MB). Disproportionate to one batch endpoint. Reconsider if
  WhisperHeim later grows several HTTP surfaces or needs middleware (auth, CORS,
  SSE streaming).
- **Named pipe (`\\.\pipe\whisperheim`, `System.IO.Pipes`).** Strong runner-up:
  BCL-only, no open TCP port (immune to the browser/DNS-rebinding risk by
  construction), OS-ACL secured, native request/response duplex. Rejected only on
  the tie-break: not curl-/browser-inspectable, requires hand-rolled length-prefix
  framing, and breaks the "base URL + JSON" mental model shared with Utterheim. The
  ADR-0003 objection to pipes ("shell hooks can't talk to pipes") does **not** bind
  here — the CLI wrapper exists regardless — which is why this was a genuine
  contender, not a dismissal. Reconsider if "no open port" becomes a hard security
  requirement. (Confirmed with the user during modeling: HttpListener chosen over
  the pipe on the inspectability / house-shape tie-break.)
- **Async-accept (`202 Accepted` + `GET /status?id=` poll), Utterheim's `/speak`
  shape.** Rejected for v1 as unnecessary complexity: STT is request→response, so
  blocking is the natural and simpler fit, and the CLI wrapper would otherwise have
  to implement a poll loop. Kept explicitly as the forward-compatible escape hatch
  for long-file timeouts (the queue item's `Guid Id`/`Stage` already support it).
- **OpenAI `/v1/audio/transcriptions` multipart compatibility.** Deferred: its
  value is third-party/browser SDK reach, which a first-party CLI consumer does not
  need (research §2). Layer on later only if a non-Claude caller appears.
- **gRPC / WebSocket.** Overkill for batch "file in → text out"; WebSocket only
  pays off for true streaming, which is out of scope. Rejected.

## References
- Research: `.agentheim/knowledge/research/whisperheim-stt-api-exposure-2026-06-19.md`
  (§1 transports, §4 hosting, §6 security, §7 Utterheim house pattern)
- Sibling decision: Utterheim ADR-0003 (`...utterheim/.agentheim/knowledge/decisions/0003-claude-transport-http.md`)
- Integration target: `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs`,
  `src/WhisperHeim/Services/FileTranscription/IFileTranscriptionService.cs`,
  `src/WhisperHeim/App.xaml.cs` (composition root), `src/WhisperHeim/Program.cs`
