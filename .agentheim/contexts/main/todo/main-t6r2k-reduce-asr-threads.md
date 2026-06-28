---
id: main-t6r2k
title: Reduce ASR intra-op threads 4 → 2
status: todo
type: refactor
context: main
created: 2026-06-28
completed:
depends_on: []
blocks: []
tags: [memory, asr, parakeet, performance]
related_adrs: []
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: [main-008]
---

## Why
The sherpa-onnx OfflineRecognizer for Parakeet is configured with up to 4 intra-op threads: `src/WhisperHeim/Services/Transcription/TranscriptionService.cs:47` sets `config.ModelConfig.NumThreads = Environment.ProcessorCount > 4 ? 4 : Environment.ProcessorCount`. Each intra-op thread carries its own ONNX Runtime memory arena and thread stack, so thread count contributes to the recognizer's native footprint (and CPU use). Dictation clips are short, so fewer threads costs little wall-clock latency while trimming per-thread native memory.

## What
Lower the ASR intra-op thread cap from 4 to 2 in `TranscriptionService.LoadModel()`:
- Change the `NumThreads` computation to cap at 2 (e.g. `Math.Min(Environment.ProcessorCount, 2)`).
- This is the only sherpa-onnx memory lever reachable from the C# API — `OfflineRecognizerConfig` exposes only `NumThreads` / `Provider` / `Debug`; the ONNX arena itself is not configurable without forking sherpa-onnx (out of scope, see research report).

## Acceptance criteria
- [ ] `NumThreads` for the Parakeet recognizer is capped at 2.
- [ ] Instant Ctrl+Win dictation still works and remains responsive; transcription accuracy is unchanged (same model, fewer threads).
- [ ] Dictation latency measured **before and after** on a representative short clip via `/deploy` — confirm the added latency from fewer threads is acceptable (a small RTF increase is expected; instant-feel must be preserved).
- [ ] Steady-state / during-transcription RAM noted before and after (expected modest reduction, tens of MB).
- [ ] Result (before/after latency + MB) recorded on completion. If the latency hit is noticeable, document and consider reverting to 3.

## Notes
- **Constraint (shared across the RAM-optimization set):** must not break instant Ctrl+Win dictation. This task tunes the resident recognizer's thread count, not its presence — Parakeet stays loaded.
- Smaller win than the GC-mode change but free; reduces CPU as well as memory.
- Sibling tasks (infrastructure BC): `infrastructure-h4m2q` (Workstation GC), `infrastructure-w7k9p` (working-set trim), `infrastructure-g3n5t` (startup GC + LOH compaction).
- Context: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md` §"ONNX/sherpa session config".
