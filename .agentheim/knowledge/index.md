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
- **ADR-0002** -- Use Workstation GC (+ concurrent) instead of Server GC for the tray app -- `knowledge/decisions/0002-workstation-gc-for-idle-tray-app.md`
<!-- adr-global:end -->

## Cross-BC research

Research reports relevant to more than one BC (or to the project as a whole). BC-specific
reports are listed in each BC's `INDEX.md`.

<!-- research-global:start -->
- **Parakeet quantization & Nemotron comparison** (2026-06-28) — quantization paths for Parakeet (BF16/FP16 as the real win; INT8 ONNX community builds; CTranslate2 doesn't support TDT) with gains/risks, and a Parakeet-vs-Nemotron-Speech/Canary comparison (incl. the newly-shipped NVIDIA Nemotron Speech streaming ASR). — `knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`
- **Best STT models for German & English** (2026-06-28) — open-weight vs cloud ASR landscape (Parakeet v2/v3, Canary-1B-v2, Voxtral, Whisper large-v3) with German/English WER from the Open ASR Leaderboard and accuracy↔speed↔VRAM↔licensing trade-offs. — `knowledge/research/best-stt-models-german-english-2026-06-28.md`
- **STT API exposure** (2026-06-19) — ways to expose WhisperHeim's speech-to-text to other apps (REST / WebSocket / gRPC / OpenAI-compatible) and their trade-offs, plus in-process .NET hosting and localhost security. — `knowledge/research/whisperheim-stt-api-exposure-2026-06-19.md`
<!-- research-global:end -->

## Pointers

- Vision: `vision.md`
- Context map: `context-map.md` (if exists)
- Protocol (chronological log): `knowledge/protocol.md` -- newest entries on top
- All ADRs: `knowledge/decisions/`
- All research: `knowledge/research/`
