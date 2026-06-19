# Index

Top-level catalog of this project's bounded contexts, global decisions, and research.
For BC-scoped artifacts, see each BC's `INDEX.md`.

> Updated by: `model` (BC creation), `work` (global ADRs), `research` (reports tagged global / cross-BC), backfill script.
> Hand-edits are fine but the skills will append at the section markers below.

---

## Bounded contexts

<!-- bc-list:start -->
- **infrastructure** -- This BC owns *globally-true* infra concerns for WhisperHeim — runtime, packaging, distribution, code signing, FFmpeg detection, settings/data-path resolution, GitHub Actions release pipeline. BC-local infra (audio device adapters, transcription queue plumbing inside `main/`) stays inside the originating BC. -- `contexts/infrastructure/INDEX.md`
- **main** -- The whole WhisperHeim app — the single bounded context for live dictation, call transcription, voice-message transcription, and (historically) text-to-speech. The project shipped as one unified tray app, so all domain work flows through this BC. -- `contexts/main/INDEX.md`
<!-- bc-list:end -->

## Global ADRs (scope: global)

<!-- adr-global:start -->
- **ADR-0001** -- Expose WhisperHeim's transcribe endpoint over loopback HTTP (HttpListener, synchronous) -- `knowledge/decisions/0001-transcribe-endpoint-loopback-http.md`
<!-- adr-global:end -->

## Cross-BC research

Research reports relevant to more than one BC (or to the project as a whole). BC-specific
reports are listed in each BC's `INDEX.md`.

<!-- research-global:start -->
- **STT API exposure** (2026-06-19) — ways to expose WhisperHeim's speech-to-text to other apps (REST / WebSocket / gRPC / OpenAI-compatible) and their trade-offs, plus in-process .NET hosting and localhost security. — `knowledge/research/whisperheim-stt-api-exposure-2026-06-19.md`
<!-- research-global:end -->

## Pointers

- Vision: `vision.md`
- Context map: `context-map.md` (if exists)
- Protocol (chronological log): `knowledge/protocol.md` -- newest entries on top
- All ADRs: `knowledge/decisions/`
- All research: `knowledge/research/`
