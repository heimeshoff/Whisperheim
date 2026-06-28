---
id: infrastructure-k9m3p
title: Spike — does disposing the Parakeet recognizer return RAM, and how fast does it reload?
status: todo
type: spike
context: infrastructure
created: 2026-06-28
completed:
depends_on: []
blocks: [infrastructure-d2v7n]
tags: [memory, asr, parakeet, lifecycle, spike]
related_adrs: []
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: []
---

## Why
The highest-impact RAM lever for WhisperHeim is unloading the ~640 MB Parakeet recognizer while idle and lazily reloading it on Ctrl+Win — since dictation is push-to-talk batch (audio accumulates on hold, transcription runs on release in `DictationOrchestrator.cs:181,241`), the model only needs to be ready *by release*, so a parallel background load mostly hides behind the time the user spends speaking. But the whole idea rests on two empirical unknowns that must be answered before building the feature (`infrastructure-d2v7n`):

1. **Does `OfflineRecognizer.Dispose()` actually return the ~640 MB to the OS?** sherpa-onnx wraps ONNX Runtime, whose CPU memory arena is known to *retain* freed blocks. If Dispose frees the managed handle but the process RSS barely drops, the idle-unload approach yields little and the feature is not worth building.
2. **How long does a reload actually take, cold vs warm?** The "1–3 s" figure is an estimate. After an idle unload the OS may reclaim the model's file-cache pages, making the next load cold (possibly >3 s) — which sets the worst-case added latency for short utterances.

## What
A throwaway measurement spike (instrument the existing `TranscriptionService`, or a small harness) to produce real numbers on the target machine:
- Measure process **working set / private bytes** at: baseline (no model), after `LoadModel()`, after `Dispose()` (+ forced `GC.Collect()` and a working-set trim to be fair), and after a second `LoadModel()`.
- Measure **load wall-time** for: first load after app launch (cold), load immediately after Dispose (likely warm file cache), and load after forcing memory pressure / a long gap (cold cache) if feasible.
- Confirm whether `Dispose()` → reload leaves the recognizer fully functional (no leaked state, correct transcripts).
- Note any allocator retention behavior (does private bytes stay elevated after Dispose even when working set drops?).

## Acceptance criteria
- [ ] RAM table recorded: baseline / loaded / disposed / reloaded (working set + private bytes).
- [ ] Load-time numbers recorded: cold-first, warm-after-dispose, cold-after-gap.
- [ ] Clear **go / no-go** recommendation for `infrastructure-d2v7n`: does Dispose return enough RAM to justify the feature, and is reload fast enough that short-utterance latency stays acceptable?
- [ ] If go: a recommended idle-unload threshold and any allocator caveats noted for the feature task.
- [ ] Findings written into this task's Notes (and/or a short ADR if the result is decision-shaping). No production code needs to ship from the spike.

## Notes
- **Constraint (shared across the RAM-optimization set):** the end goal must not break instant Ctrl+Win dictation for normal-length utterances. This spike only measures; it doesn't change runtime behavior.
- Measurement fairness: ONNX arena retention means a naive RSS read right after Dispose may mislead — pair Dispose with `GC.Collect()` + a working-set trim (see `infrastructure-w7k9p`) before reading, and also record private bytes, which the trim doesn't move.
- Gates `infrastructure-d2v7n` (lazy-load + keep-warm + idle-unload feature).
- Related levers: `infrastructure-h4m2q` (GC mode), `infrastructure-w7k9p` (working-set trim — overlaps the "idle" goal but trims rather than frees), `infrastructure-g3n5t` (startup GC), `main-t6r2k` (ASR threads).
- Context: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`. Hot-path mechanics: `TranscriptionService.cs` (LoadModel/Dispose), `DictationOrchestrator.cs` (push-to-talk batch transcribe-on-release).
