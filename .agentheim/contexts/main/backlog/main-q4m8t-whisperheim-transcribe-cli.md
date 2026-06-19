---
id: main-q4m8t
title: whisperheim-transcribe CLI wrapper over POST /transcribe
status: backlog
type: feature
context: main
created: 2026-06-19
completed:
depends_on: [main-h7k2p]
blocks: []
tags: [api, transcription, cli, claude]
related_adrs: [0001]
related_research: [whisperheim-stt-api-exposure-2026-06-19]
prior_art: []
---

## Why
A thin CLI wrapper makes the transcribe endpoint trivially callable by Claude (and
by shell scripts) without remembering curl syntax — exactly the role
`utterheim-speak` plays for Utterheim's speak endpoint. The wrapper is also what
makes the transport an implementation detail: Claude just runs
`whisperheim-transcribe <file>` and reads the transcript from stdout.

## What
A new thin console project (mirroring `Utterheim.Cli`) that POSTs a file's bytes to
`http://127.0.0.1:7777/transcribe` and prints the returned transcript to stdout.
Honors `WHISPERHEIM_ENDPOINT` for the base URL. Ships alongside the tray exe.

## Acceptance criteria
- [ ] `whisperheim-transcribe path\to\audio.ogg` prints the transcript text to
      stdout and exits `0`.
- [ ] Reads `WHISPERHEIM_ENDPOINT` (default `http://127.0.0.1:7777`); passes the
      filename (query/header) so the server preserves the extension for decoding.
- [ ] Non-success HTTP → writes the `{"error"}` message to stderr with a non-zero
      exit code; connection refused → a clear "is WhisperHeim running?" message and
      a distinct non-zero exit code (mirror `utterheim-speak`'s exit-code scheme).
- [ ] `--help` prints usage including the env var; a missing/invalid file path is
      reported clearly.
- [ ] HttpClient timeout is generous enough for long files (configurable /
      multi-minute), since the call blocks until transcription completes.
- [ ] The wrapper is packaged/shipped alongside the tray exe (build output +
      Velopack inclusion), as `utterheim-speak` is.

## Notes
- Depends on `main-h7k2p` — needs the live `/transcribe` contract to test against;
  promote to `todo` once that endpoint is testable. The wrapper itself can be
  written against the documented ADR-0001 contract in parallel.
- Reference implementation to mirror: `...utterheim/src/Utterheim.Cli/Program.cs`
  (arg parsing, `WHISPERHEIM_ENDPOINT`/`UTTERHEIM_ENDPOINT` override, exit codes).
- Contract: ADR-0001 (`.agentheim/knowledge/decisions/0001-transcribe-endpoint-loopback-http.md`).
