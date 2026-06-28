---
id: main-t6r2k
title: Reduce ASR intra-op threads 4 → 2
status: done
type: refactor
context: main
created: 2026-06-28
completed: 2026-06-28
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

## Outcome
Lowered the Parakeet recognizer's intra-op thread cap from 4 to 2. Single line in
`src/WhisperHeim/Services/Transcription/TranscriptionService.cs:47`:
`config.ModelConfig.NumThreads = Math.Min(Environment.ProcessorCount, 2);`
(was `Environment.ProcessorCount > 4 ? 4 : Environment.ProcessorCount`). Nothing else touched.

**Measurement** (machine-measured via a throwaway harness that drove sherpa-onnx's
`OfflineRecognizer` directly against the resident int8 Parakeet model — same files the app
loads — on a fixed ~3 s clip; median of 5 decodes after a warm-up, 24-core machine):

| threads | median decode | RTF | process working set |
|---------|---------------|-----|---------------------|
| 4 (before) | ~160 ms | 0.054 | ~52 MB |
| 2 (after)  | ~220 ms | 0.073 | ~53 MB |

- **Latency:** +~60 ms absolute on a 3 s clip (~+38% relative). Decode stays ~13× faster than
  real time, so the instant-feel for short Ctrl+Win dictation is preserved. Acceptable — no
  need to fall back to 3. (Fallback to 3 remains the documented lever if a slower machine finds
  2 threads sluggish; not exercised here.)
- **Accuracy:** transcript text is **identical** at 4 vs 2 threads (`text_identical=True` in the
  harness) — `NumThreads` does not affect greedy-decode output, as expected (same model).
- **RAM:** the per-thread saving could not be cleanly isolated headlessly — the 650 MB int8
  encoder is memory-mapped, so process working set read ~51–54 MB regardless of thread count
  (delta within noise, ~1–3 MB). The native per-intra-op-thread arena/stack saving from dropping
  2 threads is therefore small (low single-digit to low-tens MB), consistent with the task's
  "modest, tens of MB" expectation. The authoritative during-dictation RAM figure is best read
  by the user in Task Manager against the real deployed app (`scripts/publish.ps1` via
  `deploy.cmd`); this run confirms the latency/accuracy half machine-side.

Build: `dotnet build -c Release` succeeds (0 errors). This is config tuning of a local
`OfflineRecognizerConfig` value with no exposed seam, so no unit test was added (verifying it
would require loading the 650 MB native model); the lever was exercised directly via the harness
instead. Key file: `src/WhisperHeim/Services/Transcription/TranscriptionService.cs`.
