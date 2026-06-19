---
topic: Ways to expose WhisperHeim's local speech-to-text engine to other apps via an API
date: 2026-06-19
requested_by: user
related_tasks: [main-h7k2p, main-q4m8t]
---

# Research: Exposing WhisperHeim STT to Other Applications

## Question
WhisperHeim transcribes audio locally (Sherpa-ONNX / Parakeet, float32 16 kHz mono PCM, single serialized engine in a WPF .NET 9 tray app). The sibling TTS app Utterheim already exposes a local REST interface (text in → speech out). WhisperHeim wants the inverse: send audio (stream or file) to it over some transport and get recognized text back. What are the good transport/API options, how do comparable local STT tools do it, how do you host a server inside a WPF app, how does real-time streaming map onto a chunked engine, and what are the security trade-offs? This is an understanding report — it lays out the option space, not a decision.

> **Scope correction (2026-06-19, post-write):** The original request mentioned "cloud plugins," and this report was first written for an *arbitrary third-party / browser-reachable* consumer. The requester then clarified that the intended consumer is **Claude** (Claude Code) — a **local, first-party caller that can run a CLI or script**, not a browser or remote cloud service. That collapses much of the original framing: the OpenAI-compatibility "leverage" and the DNS-rebinding/CORS "sharp edge" were both consequences of the third-party/browser assumption and are **demoted** below. Named pipes, originally dimmed as "poor reach," are **reinstated as a first-class option**. The Summary below reflects the corrected scope; the original Findings sections are retained as background and cross-referenced.

## Summary (decision-relevant first — Claude-as-consumer scope)
- **The consumer is local and first-party (Claude via a CLI wrapper).** Whatever transport is chosen, Claude calls it through a thin `whisperheim-transcribe <file>` wrapper (exactly as Utterheim ships `utterheim-speak`). So the transport is largely an *implementation detail behind the wrapper* — reach/browser/cross-language concerns mostly evaporate, and the decision reduces to **footprint + simplicity + house-consistency**.
- **The workload is batch and request→response.** "Here is a file/buffer → give me the full text back." This is the opposite shape from Utterheim's fire-and-forget `POST /speak` (202 + poll). A duplex/synchronous transport (named pipe, or a blocking HTTP call) is the natural fit; the single-engine queue just serializes the caller's turn.
- **Three real transport candidates, all viable:** **(a) Named pipe** (`System.IO.Pipes`, BCL-only, no open port, OS-ACL secured, native request/response duplex — strong default here); **(b) HttpListener loopback** (BCL-only, http.sys, small footprint, curl-able for debugging — middle ground); **(c) Kestrel/ASP.NET Core Minimal API** (matches Utterheim's house pattern, richest features, but pulls in the ASP.NET Core runtime ~11 MB and the .NET Generic Host) [11][12][13].
- **The single shared engine is the real constraint, not the transport.** Whatever surface is chosen must funnel through the existing FIFO `TranscriptionQueueService` (enqueue, or reject-when-busy, or block the caller's connection until its turn). Independent of pipe vs HTTP.
- **House pattern (resolved — see §7): Utterheim uses Kestrel Minimal API on `127.0.0.1:7223`, loopback-only, no auth, hosted as an `IHostedService` on the .NET Generic Host, with a CLI wrapper.** Consistency argues for Kestrel; footprint and the "no open port" security posture argue for named pipe or HttpListener. WhisperHeim is *not* on the Generic Host today, so the Kestrel path carries extra wiring that the BCL options avoid.
- **OpenAI `/v1/audio/transcriptions` compatibility — demoted to optional.** It was "highest leverage" only for zero-effort third-party/browser reach (still true, see §2), which Claude doesn't need. Worth layering on later *only if* a non-Claude caller appears.
- **DNS-rebinding / CORS security — demoted to N/A for the Claude path.** That risk (see §6) is about a *browser* reaching a loopback port. A local CLI consumer never invokes it, and a named pipe has no port to reach at all. Utterheim's "loopback, no auth in v1" baseline is sufficient.

## Findings

### 1. Transport / protocol options and trade-offs

**HTTP/REST, multipart-file or raw-body upload (batch).** Send the whole audio file/buffer in one request, get the full text back. Simplest possible model: stateless, every language has an HTTP client, trivially reachable by cloud plugins and curl. Latency is "whole-utterance" — you wait for the entire transcription before any text. This matches WhisperHeim's file/voice-message use case directly and matches how the engine works today (whole utterance → VAD-chunk → transcribe). Multipart `file` upload is the universal convention; raw-body (e.g. `audio/wav` POST) is marginally simpler to produce but less standard. In .NET this is the lowest-complexity option [1][4][12].

**WebSocket streaming (real-time partial results).** A persistent bidirectional connection: client streams audio frames, server emits incremental/partial transcripts. Lower perceived latency for live mic input; standard for browser-reachable real-time STT (Voicegain, Deepgram-style services use WS) [8]. More complex: connection lifecycle, framing of 16 kHz PCM, partial-vs-final result semantics. Browser-reachable without extra tooling. This is the right shape *only if live streaming is a requirement* — it does not match WhisperHeim's current batch use cases [8].

**gRPC (unary + bidirectional streaming).** HTTP/2-based; protobuf payloads. Comparative analyses report gRPC achieving meaningfully higher throughput and lower latency than REST and lower CPU overhead than WebSocket for audio streaming, due to HTTP/2 multiplexing and binary serialization [8]. First-class C#/.NET support and easy multi-language client generation. Downsides for *this* use case: not natively reachable from browsers without grpc-web, more friction for casual third-party callers and "POST a file with curl" simplicity, and protobuf tooling overhead. Strong for performance-critical streaming between cooperating services; weaker for "any app/cloud plugin just calls it" [8].

**Local IPC alternatives (named pipes, Unix domain sockets, localhost TCP).** Lowest overhead, no network port exposed, OS-level access control. The original draft dimmed these as "poor reach for cloud plugins" — but **with the corrected scope (consumer = Claude, local, via a CLI wrapper), a named pipe is a first-class fit, arguably the best default.** `System.IO.Pipes.NamedPipeServerStream` is BCL-only (no ASP.NET Core dependency), opens **no TCP port** (immune to the DNS-rebinding/browser risk in §6 by construction), is access-controlled by Windows ACLs, and is a **native request/response duplex** — write the audio bytes, read the transcript back on the same connection — which matches the "send audio, get text" shape better than Utterheim's async-accept HTTP. Cost: a bit more hand-rolled code (server loop + per-connection `NamedPipeServerStream` instances + your own length-prefix framing for the audio + JSON reply), Windows-only (a non-issue — the app is already `net9.0-windows`), and not trivially curl-/browser-inspectable (the CLI wrapper is the interface, so debugging happens through it). Claude reaches it exactly as it would HTTP: a `whisperheim-transcribe <file>` wrapper that opens the pipe.

Trade-off summary: REST = simplest + most reachable, batch only. WebSocket = real-time, browser-reachable, more complex. gRPC = fastest streaming + great .NET support, weaker third-party/browser reach. IPC = lowest overhead, poorest reach.

### 2. OpenAI `/v1/audio/transcriptions` as a de-facto standard

Verified against OpenAI's API reference [1][5]. Request is **multipart/form-data**:
- **`file`** (required) — audio file (flac, mp3, mp4, mpeg, mpga, m4a, ogg, wav, webm).
- **`model`** (required) — e.g. `whisper-1`, `gpt-4o-transcribe`, `gpt-4o-mini-transcribe`. Local servers accept this field but typically ignore/alias it to their own loaded model.
- Optional: **`response_format`** (`json` default, `text`, `srt`, `verbose_json`, `vtt`; OpenAI now also lists `diarized_json`), **`language`** (ISO-639-1), **`prompt`**, **`temperature`**, **`timestamp_granularities`** (`word`/`segment`), **`stream`** (boolean — Server-Sent Events; *not* supported for `whisper-1`), **`chunking_strategy`**, **`include`** [1][5].
- `verbose_json` returns `text`, `language`, `duration`, `segments[]` (with timing/token data), and `words[]` (when word granularity requested) — this maps well onto WhisperHeim's existing diarized/segmented output [1][5].

**Compatibility benefit:** implementing this exact shape means existing OpenAI SDKs (Python `openai`, JS, etc.) and a large body of third-party tools work against WhisperHeim by changing only the base URL and a dummy API key. This is precisely why most local servers adopted it [4][5][6][7]. **Constraints/caveats:** the multipart `file` contract assumes a *whole file*, so it's inherently batch; OpenAI's optional `stream:true` is SSE (a streamed *response*, not streamed *upload*) and is a newer addition that not all local servers implement; the surface carries fields (`prompt`, `temperature`) that only loosely map to a non-Whisper engine like Parakeet and may be accepted-and-ignored. WhisperHeim would implement a *subset* and document which fields are honored.

### 3. How comparable local/offline STT tools expose their APIs

| Tool | Transport | OpenAI-compatible? | Streaming? | Notes |
|---|---|---|---|---|
| **whisper.cpp `server`** | HTTP | Partially — default path `/inference` (multipart `file`), `--inference-path` remaps to `/v1/audio/transcriptions`. `--host`/`--port` bind. | No WS/streaming in the core server example | Lightweight C++ HTTP server; `response_format` incl. json [4][2] |
| **faster-whisper-server → Speaches** | HTTP + SSE | Yes — "all tools/SDKs that work with OpenAI's API should work" | Yes — transcript streamed via SSE as audio is transcribed; also a Realtime API | Renamed to Speaches; explicitly markets OpenAI compatibility [3][6] |
| **LocalAI** | HTTP | Yes — `/v1/audio/transcriptions` | Batch | Drop-in OpenAI replacement across modalities [7] |
| **hwdsl2/whisper-server** | HTTP | Yes (faster-whisper backend) | Batch | Self-host, OpenAI-compatible [6] |
| **Wyoming (Rhasspy/Home Assistant)** | TCP / Unix socket / stdio | No — own scheme | Yes (chunked) | JSONL event header + optional binary payload; STT events `audio-start`/`audio-chunk`/`audio-stop` → `transcript`. URI scheme e.g. `tcp://127.0.0.1:10300` [9][10][14] |
| **Sherpa-ONNX own examples** | WebSocket (and C API) | No | Both streaming and non-streaming WS servers shipped | The engine WhisperHeim already uses ships Python WS server examples; non-streaming server supports multiple concurrent clients [13] |

Disagreement/nuance: the field splits into two camps. The "reach any app/cloud" camp converged on **OpenAI-compatible HTTP** (whisper.cpp, Speaches, LocalAI). The "voice-assistant pipeline" camp uses **Wyoming** (its own streaming TCP/stdio framing) because it needs low-latency bidirectional audio with wake-word/intent stages. Sherpa-ONNX's *own* examples use WebSocket, not OpenAI HTTP — notable because that's WhisperHeim's actual engine, but those examples are reference servers, not the in-house pattern.

### 4. Hosting an HTTP/WebSocket server inside a .NET WPF app

**Embedded Kestrel / ASP.NET Core Minimal API in-process.** Well-documented and "not that different from a proper web app" [11]. Bind to loopback via `options.Listen(IPAddress.Parse("127.0.0.1"), port)` [11]. Pros: full feature set — Minimal API routing, WebSocket support, gRPC hosting, model binding, multipart handling, auth/CORS middleware. Cons: requires the **ASP.NET Core runtime** to be present (it's not part of the base .NET runtime), which matters for a Velopack-packaged desktop app's footprint; ~11 MB runtime memory in one comparison [12]. Community packages exist to ease the desktop scenario (e.g. `RunAsDesktopTool()` / Rick Strahl's WPF sample) [11].

**`System.Net.HttpListener` (http.sys).** Lighter self-host (~5 MB), simpler for "just serve some endpoints," and adequate when bound to loopback on a machine running only your app [12]. Cons: considered too simple for large-file/complex handling and security-sensitive use; you'd hand-roll multipart parsing; WebSocket support exists (`GetWebSocketContext`) but you build more yourself. On Windows, non-admin loopback binding via http.sys can require URL ACL reservation for non-`localhost` prefixes — a known operational wrinkle (single-source/general knowledge; verify against Utterheim's setup).

**gRPC server in-process.** Hosted via the same ASP.NET Core/Kestrel infrastructure; first-class on .NET. Adds protobuf tooling; reachable by cooperating services but not browsers without grpc-web.

Cross-cutting concerns for all three:
- **Loopback-only binding** (`127.0.0.1`/`::1`) is the default safe posture and typically avoids the Windows Firewall prompt that LAN binding (`0.0.0.0`) triggers (general knowledge; confirm on target machines).
- **Port selection/conflicts:** pick a default port, detect conflicts, optionally allow override (Utterheim presumably already solved this — mirror it).
- **Lifecycle:** start the host when the tray app starts, stop on exit; the host runs on background threads while WPF owns the UI thread.
- **Threading into the single engine:** request handlers run concurrently, but the engine is serialized. Handlers must enqueue onto `TranscriptionQueueService` (or `TryAcquire`/`Release` the lock and return 429/503 when busy). This is the same constraint regardless of transport.

### 5. Real-time streaming transcription specifically

If streaming is pursued, the patterns are: **WebSocket frames**, **gRPC bidi**, or **chunked HTTP / SSE** for the response side. Common framing is 16 kHz PCM in fixed-size chunks with a small JSON/metadata header — exactly the Wyoming model (`audio-start` with rate/width/channels, repeated `audio-chunk` with PCM payload, `audio-stop`, then `transcript`) [10][14]. OpenAI's `stream:true` is SSE delivering the *response* incrementally; Speaches does the same [1][6].

**Mapping onto WhisperHeim's engine:** the engine today works on whole utterances/files and is VAD-chunked internally (Silero). Real streaming would require either (a) accumulating incoming frames and running the existing VAD/utterance pipeline at utterance boundaries to emit "final" segments (achievable, gives finals but not true low-latency partials), or (b) a genuinely streaming decode path, which Parakeet/Sherpa-ONNX offline models do not provide out of the box (Sherpa-ONNX has *separate* streaming models; the offline Parakeet model is non-streaming) [13]. So true word-by-word partials would likely need a different/streaming model, not just a different transport. This is the most significant architectural gap between "expose batch" and "expose streaming." (The "offline Parakeet is non-streaming" point is inferred from Sherpa-ONNX's streaming-vs-non-streaming distinction [13] plus the grounding facts; worth confirming against the engine config.)

### 6. Security & access control for a localhost API

- **Loopback binding is necessary but not sufficient.** A server on `127.0.0.1` is still reachable from a malicious website in the victim's browser via **DNS rebinding**, which "does not require a misconfiguration or bug" and bypasses CORS entirely [15][16]. This is the headline risk for any desktop-app localhost server.
- **Mitigations that work** [15][16]: (1) **Validate the `Host` header** against an allow-list of expected local names — primary defense against DNS rebinding. (2) **Require a bearer token / API key** on sensitive endpoints — browsers won't send your credentials from an unintended origin, so this blocks the cross-site path. (3) **Avoid wildcard/loose CORS**; use exact-match origins; beware `endsWith`/suffix matching bugs (e.g. `attackerstripe.com` passing an `endsWith("stripe.com")` check) [16].
- **LAN exposure (`0.0.0.0`)** widens the surface to the local network and triggers firewall prompts — only do it deliberately, and then auth becomes mandatory, not optional.
- **CORS for cloud-plugin/browser callers:** if cloud plugins call from a browser context, you must set explicit allowed origins; if they call server-to-server, CORS is irrelevant but token auth still matters. Note the documented pattern that OAuth/OIDC **bearer tokens can bypass CORS/CSRF** concerns because they're not ambient credentials [16].
- **Net:** even for a "just localhost" API, the responsible baseline is loopback bind + Host-header validation + an API key, with CORS locked to known origins. Mirror whatever Utterheim already does so the two apps are consistent.

### 7. The in-house pattern: what Utterheim actually uses (resolved)

Read directly from Utterheim's source (`C:\src\heimeshoff\tooling\utterheim`) — this resolves the report's biggest original open question and supersedes the earlier "read Utterheim's source rather than re-researching" note.

- **Transport:** HTTP/1.1 JSON over **Kestrel + ASP.NET Core Minimal API** (`WebApplication.CreateBuilder()`, `app.MapPost`/`MapGet`) — *not* `HttpListener`. Source: `src/Utterheim/Services/Http/SpeakServer.cs`; decision in ADR 0003 (`.agentheim/knowledge/decisions/0003-claude-transport-http.md`).
- **Binding:** `127.0.0.1:7223`, **loopback-only**.
- **Auth:** **none in v1** — explicitly "single-user, localhost-only, by design." ADR 0003 notes a *remote* consumer would need an auth story later; a local one does not.
- **Hosting:** `SpeakServer : IHostedService`, registered in the **.NET Generic Host** (`Host.CreateDefaultBuilder`), started via `host.Start()` *before* the WPF message loop and torn down on exit (`src/Utterheim/EntryPoint.cs`). The whole app runs the Generic Host and WPF together.
- **Port override:** `appsettings.json` / `UTTERHEIM_` env / command line (`Utterheim:Http:Port`); port-collision surfaces a clear error and the user overrides.
- **API shape (async-accept):** `POST /speak` → **`202 Accepted` + `{requestId, queuePosition}`**, does not block on audio; `GET /status` polls; plus `/stop`, `/voices`. This fire-and-forget shape suits TTS *playback*; STT is request→response, so WhisperHeim would more naturally **block-and-return-text** (or async-accept + poll a `GET /status?id=` that returns the transcript, reusing `TranscriptionQueueService`'s existing item IDs/stages).
- **Shared seam:** the HTTP handler and the GUI button both call the same `SpeakService.Enqueue` — one in-process seam, two surfaces. WhisperHeim should mirror this by routing the API through the same enqueue path the Conversations tab uses.
- **CLI wrapper:** `Utterheim.Cli` → `utterheim-speak --voice alba "text"`, a thin `HttpClient` wrapper over `POST /speak` honoring `UTTERHEIM_ENDPOINT` (`src/Utterheim.Cli/Program.cs`). The wrapper exists *regardless of transport* — which is why ADR 0003's rejection of named pipes ("shell hooks can't talk to pipes without a helper binary") does **not** carry over to WhisperHeim: the helper binary exists anyway.
- **Explicitly rejected in ADR 0003:** named pipes (for the helper-binary reason above), per-call CLI as the *primary* surface (~50–100 ms process spawn), and gRPC/WebSocket/protobuf ("overkill for `{text, voice}`").

**Implication for WhisperHeim:** Kestrel maximizes house-consistency but (a) pulls in the ASP.NET Core runtime (~11 MB, §4) and (b) requires adopting the .NET Generic Host, which WhisperHeim's custom Velopack `Main` does not use today (`Microsoft.NET.Sdk`, not `.Sdk.Web`). Named pipe / HttpListener avoid both. The named-pipe rejection in ADR 0003 was context-specific to Utterheim's shell-hook trigger and does not bind WhisperHeim.

## Sources
1. [Create transcription — OpenAI API Reference](https://developers.openai.com/api/reference/resources/audio/subresources/transcriptions/methods/create) — primary source for request fields, response_format values, verbose_json shape, streaming. Current as of 2026-06.
2. [whisper.cpp — ggml-org/whisper.cpp (server example)](https://github.com/ggml-org/whisper.cpp) — `/inference` multipart endpoint, `--inference-path`/`--host`/`--port` flags.
3. [faster-whisper — Medium overview (Dzianis Vashchuk)](https://medium.com/@dzianisv/software-engineering-faster-whisper-b93f7edb087e) — perf context (faster-whisper ~4x faster). Secondary/blog.
4. [Whisper-API-Server (OpenAI-compatible wrapper)](https://github.com/ziozzang/Whisper-API-Server/) — example of wrapping whisper into the OpenAI endpoint.
5. [Audio — OpenAI API Reference (platform docs)](https://platform.openai.com/docs/api-reference/audio?lang=javascript) — multipart curl example, verbose_json fields. (Primary; some pages 403 to bots.)
6. [faster-whisper-server / Speaches — fedirz/faster-whisper-server](https://github.com/fedirz/faster-whisper-server) — OpenAI-compatible, SSE streaming, renamed to Speaches.
7. [Audio to Text — LocalAI](https://localai.io/features/audio-to-text/) — `/v1/audio/transcriptions` OpenAI-compatible local server.
8. [gRPC Performance for Audio and Voice Streaming vs REST/WebSockets — MAT Journals](https://matjournals.net/engineering/index.php/IJEITSEC/article/view/2794) and [Voicegain streaming audio](https://www.voicegain.ai/post/streaming-real-time-audio-to-voicegain-speech-to-text) — transport latency/throughput comparison; WebSocket as common streaming choice. Academic + vendor (vendor flagged).
9. [Wyoming Protocol — Home Assistant](https://www.home-assistant.io/integrations/wyoming/) — Wyoming as the HA local STT/TTS integration scheme.
10. [Wyoming protocol spec — rhasspy/rhasspy3 docs](https://github.com/rhasspy/rhasspy3/blob/master/docs/wyoming.md) — primary: JSONL header + binary payload framing, audio-start/chunk/stop + transcript events, stdio/socket transport.
11. [Embedding a minimal ASP.NET Web Server into a Desktop Application — Rick Strahl](https://weblog.west-wind.com/posts/2023/Nov/27/Embedding-a-minimal-ASPNET-Web-Server-into-a-Desktop-Application) — Kestrel-in-WPF, loopback binding, runtime caveat. Nov 2023.
12. [Host Kestrel Web Server in .NET 6 Windows Form Application — Jason Ge](https://jason-ge.medium.com/host-kestrel-web-server-in-net-6-windows-form-application-8b0fd70b4288) and [HttpListener vs Kestrel discussion — dotnet/aspnetcore #62810](https://github.com/dotnet/aspnetcore/discussions/62810) — memory/footprint comparison (~5 MB vs ~11 MB), ASP.NET runtime requirement.
13. [Sherpa-ONNX non-streaming WebSocket server](https://k2-fsa.github.io/sherpa/onnx/python/non-streaming-websocket-server.html) and [non_streaming_server.py](https://github.com/k2-fsa/sherpa-onnx/blob/master/python-api-examples/non_streaming_server.py) — the actual engine's own WS server examples; streaming vs non-streaming distinction.
14. [Wyoming integration — Grokipedia](https://grokipedia.com/page/Wyoming_Home_Assistant_integration) — corroborates TCP/Unix/stdio transport and event model. Secondary.
15. [Localhost dangers: CORS and DNS rebinding — GitHub Security Blog](https://github.blog/security/application-security/localhost-dangers-cors-and-dns-rebinding/) — primary security source: loopback servers reachable via DNS rebinding; Host-header validation + auth mitigations.
16. [CORS errors explained — SuperTokens](https://supertokens.com/blog/cors-errors) and [Microsoft Q&A: CORS + bearer token](https://learn.microsoft.com/en-us/answers/questions/717634/) — CORS pitfalls, suffix-match bypass, bearer-token interaction.

## Open questions
- ~~**What exactly does Utterheim use?**~~ **Resolved — see §7.** Kestrel Minimal API on `127.0.0.1:7223`, loopback-only, no auth, `IHostedService` on the Generic Host, CLI wrapper.
- **House-consistency vs footprint — the live decision.** Kestrel matches Utterheim but needs the ASP.NET Core runtime *and* adoption of the .NET Generic Host (WhisperHeim doesn't use it today). Named pipe / HttpListener avoid both at the cost of diverging from the sibling app's transport. This is the trade-off to settle in modeling.
- **Sync-blocking vs async-accept+poll API shape.** STT is request→response, unlike Utterheim's fire-and-forget `/speak`. Decide whether the wrapper blocks until the transcript is ready (simplest for Claude) or enqueues + polls (reuses `TranscriptionQueueService` IDs/stages, better for long files that risk client timeouts).
- **Is true streaming actually wanted?** The stated use cases are batch (files / WhatsApp voice messages). Live partial-result streaming would be a separate, larger commitment and may require a *streaming* Sherpa model, not just a new transport. Out of scope unless confirmed.
- **Does the offline Parakeet model support any streaming decode?** Inferred non-streaming from Sherpa-ONNX docs + grounding facts; verify against the actual model/engine config before assuming streaming requires a model swap.
- **Velopack footprint impact** of bundling the ASP.NET Core runtime (for Kestrel) vs staying on a BCL-only transport (named pipe / HttpListener) — not quantified here.
