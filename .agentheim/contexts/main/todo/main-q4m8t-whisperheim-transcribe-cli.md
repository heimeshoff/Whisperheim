---
id: main-q4m8t
title: whisperheim-transcribe CLI wrapper over POST /transcribe
status: todo
type: feature
context: main
created: 2026-06-19
completed:
depends_on: [main-h7k2p]
blocks: []
tags: [api, transcription, cli, claude]
related_adrs: [0001]
related_research: [whisperheim-stt-api-exposure-2026-06-19]
prior_art: [main-h7k2p]
---

## Why
A thin CLI wrapper makes the transcribe endpoint trivially callable by Claude (and
by shell scripts) without remembering curl syntax — exactly the role
`utterheim-speak` plays for Utterheim's speak endpoint. The wrapper is also what
makes the transport an implementation detail: Claude just runs
`whisperheim-transcribe <file>` and reads the transcript from stdout.

## What
A new thin console project (mirroring `Utterheim.Cli`) that reads a file's bytes and
POSTs them **raw** to `http://127.0.0.1:7777/transcribe?filename=<name>`, parses the
JSON response, and prints **only the `text` field** to stdout. Honors
`WHISPERHEIM_ENDPOINT` for the base URL. Ships alongside the tray exe.

Note the live contract (now implemented in `main-h7k2p`, ADR-0001): the endpoint
takes **raw audio bytes in the request body** (not a JSON envelope, unlike
Utterheim's `/speak`) and returns JSON
`{text, audioDurationSeconds, transcriptionDurationSeconds, realTimeFactor, chunkCount}`.
v1 prints `text` only — the metadata fields are ignored (user decision, 2026-06-19).

## Acceptance criteria
- [ ] `whisperheim-transcribe path\to\audio.ogg` reads the file's bytes, POSTs them
      as the raw request body to `<endpoint>/transcribe?filename=audio.ogg`, parses
      the JSON response, prints **`result.text`** (and nothing else) to stdout, and
      exits `0`. Empty/no-speech audio returns `200` with `"text": ""` → prints an
      empty line, exits `0`.
- [ ] Reads `WHISPERHEIM_ENDPOINT` (default `http://127.0.0.1:7777`); passes the
      original filename via `?filename=` (and/or the `X-Filename` header) so the
      server preserves the extension for the decoder.
- [ ] Exit-code scheme mirrors `utterheim-speak`:
      `0` success;
      `1` usage / arg error (no path, `--help`, missing/invalid/unreadable file);
      `2` HTTP non-success — write the response body (the `{"error":"<message>"}`
      payload) to stderr;
      `3` connection refused / cannot reach the endpoint — clear
      "is WhisperHeim running?" message.
- [ ] `--help` / `-h` prints usage including the `WHISPERHEIM_ENDPOINT` env var; a
      missing or unreadable file path is reported clearly and exits `1` **before**
      any network call.
- [ ] HttpClient timeout is generous enough for long files — multi-minute or
      `Timeout.InfiniteTimeSpan` — since the call blocks until transcription
      completes. (The `Utterheim.Cli` reference uses a 5 s timeout; that is far too
      short for STT and must be overridden here.)
- [ ] The wrapper is packaged/shipped alongside the tray exe (build output +
      Velopack inclusion), as `utterheim-speak` is. Mirror `Utterheim.Cli`:
      project `WhisperHeim.Cli`, assembly/exe name `whisperheim-transcribe`.

## Notes
- **Dependency `main-h7k2p` is DONE** (endpoint live + 123 tests pass) — the
  `/transcribe` contract is now testable end-to-end, which is why this is promoted
  to `todo`. Live endpoints: `POST /transcribe` (raw bytes body, `?filename=` /
  `X-Filename`), `GET /health` (`{status, busy, queueDepth}`).
- **Reference to mirror:** `...utterheim/src/Utterheim.Cli/Program.cs` — copy its
  arg-parsing shape, the `*_ENDPOINT` override, and the `1`/`2`/`3` exit codes.
  Two deliberate differences from the reference: (a) transcribe sends **raw audio
  bytes** via `ByteArrayContent`/`StreamContent`, not `PostAsJsonAsync`; (b) it
  prints `result.text`, where speak prints `result.requestId`.
- **`GET /health` is optional.** A connection-refused on `/transcribe` already
  gives the "is WhisperHeim running?" signal (exit `3`); probing `/health` first is
  a nicety, not a requirement.
- Contract: ADR-0001 (`.agentheim/knowledge/decisions/0001-transcribe-endpoint-loopback-http.md`).
- The wrapper can be built and unit-tested against the documented ADR-0001 contract,
  then smoke-tested against the running tray app.
