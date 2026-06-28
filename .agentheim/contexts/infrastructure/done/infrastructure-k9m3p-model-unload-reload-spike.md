---
id: infrastructure-k9m3p
title: Spike — does disposing the Parakeet recognizer return RAM, and how fast does it reload?
status: done
type: spike
context: infrastructure
created: 2026-06-28
completed: 2026-06-28
depends_on: []
blocks: [infrastructure-d2v7n]
tags: [memory, asr, parakeet, lifecycle, spike]
related_adrs: [0005]
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

## Outcome

**Verdict: GO for `infrastructure-d2v7n`.** Dispose genuinely returns the RAM (the
ONNX arena-retention risk did not bite), and reload is a correct, fixed ~4 s.
Decision captured in **ADR-0005** (`.agentheim/knowledge/decisions/0005-idle-unload-of-parakeet-recognizer-go.md`).

Measured on the target machine via a throwaway console harness driving the real
`TranscriptionService` (load → transcribe → Dispose+`GC.Collect`+`WorkingSetTrimmer.Trim()`
→ reload), under production GC mode (Workstation+concurrent, ADR-0002), NumThreads=2.
Functional input was a 4.42 s SAPI-synthesized utterance. Harness was run from the
scratchpad and not committed (no production runtime change shipped).

### RAM table (WorkingSet64 / PrivateMemorySize64)
| Phase                       | Working set | Private bytes |
|-----------------------------|-------------|---------------|
| baseline (no model)         | 5.4 MB      | 8.3 MB        |
| after `LoadModel()` #1      | 725.5 MB    | 707.2 MB      |
| after `Dispose()`+GC+Trim   | 1.6 MB      | **28.2 MB**   |
| after `LoadModel()` #2      | 706.7 MB    | 715.2 MB      |

Dispose returned **~679 MB of private bytes** of the ~699 MB the load added —
private bytes fell to ~28 MB, only ~20 MB over a pristine baseline. The
working-set trim was applied for fairness but was not load-bearing; the real free
happened on `Dispose()`. Private bytes (which a trim cannot move) is the honest
proof the memory was actually returned to the OS, not merely trimmed to standby.

### Load wall-times
| Load                                    | Time    |
|-----------------------------------------|---------|
| cold-first (first load after launch)    | 4289 ms |
| warm-after-dispose (immediate reload)   | 3983 ms |
| cold-after-gap (after 6 GB mem-pressure)| 4077 ms |

All within ~300 ms of each other → load is **session-init-bound, not I/O-bound**.
File-cache warmth is irrelevant; only keeping the session alive avoids the ~4 s.

### Functional confirmation
Same utterance transcribed identically across all three loads
("The quick brown fox jumps over the lazy dog.", decode ~336 ms each) → Dispose →
reload leaves no leaked state.

### Recommendation for `infrastructure-d2v7n`
- **Start lazy-load on Ctrl+Win key-DOWN** so the ~4 s overlaps speaking time;
  transcribe-on-release `await`s the in-flight load. Utterances >~4 s hide it fully.
- **First short utterance after an idle-unload pays up to ~4 s** — unavoidable worst
  case; the reason to gate unload behind real idle.
- **Idle-unload threshold: ~5–10 min** of no dictation (default 5 min), deliberately
  longer than the 3-min `IdleWorkingSetTrimmer` window — a 4 s session rebuild is far
  costlier to recover from than a cold-page re-fault. Reuse the existing
  `NotifyActivity()` idle-tracking shape.
- **Allocator caveat:** the no-retention result is tied to this INT8 model + current
  `org.k2fsa.sherpa.onnx`/ORT version. Re-run this harness to re-confirm Dispose
  still returns private bytes after the Nemotron model swap on this branch.
