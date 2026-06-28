---
id: 0005
title: GO on idle-unload of the Parakeet recognizer — Dispose returns ~680 MB, reload is a fixed ~4 s
scope: infrastructure
status: accepted
date: 2026-06-28
supersedes: []
superseded_by: []
related_tasks: [infrastructure-k9m3p, infrastructure-d2v7n, infrastructure-q4t8m, infrastructure-b3n6p, infrastructure-w7k9p, infrastructure-h4m2q]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
---

# ADR 0005: GO on idle-unload of the Parakeet recognizer

## Context
Idle-unloading the ~640 MB INT8 Parakeet recognizer is the highest-impact RAM
lever left for WhisperHeim, but it rested on two empirical unknowns that gate the
feature task `infrastructure-d2v7n`:

1. Does `OfflineRecognizer.Dispose()` actually return the committed memory to the
   OS, or does ONNX Runtime's CPU memory arena retain the freed blocks (making the
   unload pointless)?
2. How long does a reload really take — cold vs warm — since that sets the
   worst-case added latency for short push-to-talk utterances?

Spike `infrastructure-k9m3p` measured both directly on the target machine via a
throwaway console harness that drove the real `TranscriptionService`
(load → transcribe → Dispose+`GC.Collect`+`WorkingSetTrimmer.Trim()` → reload),
reading `WorkingSet64` / `PrivateMemorySize64` at each phase. Measurement ran
under the app's production GC mode (Workstation + concurrent, ADR-0002) and NumThreads=2.

## Measured outcome
RAM table (single process, INT8 Parakeet, ORT via `org.k2fsa.sherpa.onnx` 1.x):

| Phase                          | WorkingSet64 | PrivateMemorySize64 |
|--------------------------------|--------------|---------------------|
| baseline (no model)            | 5.4 MB       | 8.3 MB              |
| after `LoadModel()` #1         | 725.5 MB     | 707.2 MB            |
| after `Dispose()`+GC+Trim      | 1.6 MB       | **28.2 MB**         |
| after `LoadModel()` #2 (reload)| 706.7 MB     | 715.2 MB            |

- **Dispose returns the memory.** Load added ~699 MB of private bytes; Dispose+GC
  returned ~679 MB of it. Private bytes fell to 28 MB — only ~20 MB above the
  pristine baseline. **The feared ONNX arena retention did not materialize** for
  this INT8 build / ORT version: private bytes (the honest committed measure, which
  a working-set trim cannot move) genuinely collapsed. The trim was applied for
  fairness but was not load-bearing here — the real free happened on Dispose.

Load wall-times:

| Load                                   | Time    |
|----------------------------------------|---------|
| cold-first (first load after launch)   | 4289 ms |
| warm-after-dispose (immediate reload)  | 3983 ms |
| cold-after-gap (after 6 GB mem-pressure)| 4077 ms |

- **Reload is a fixed ~4 s, independent of file-cache state.** Cold-first,
  warm-after-dispose, and post-memory-pressure loads are all within ~300 ms of each
  other. Load cost is dominated by ONNX session construction / graph optimization,
  **not** disk I/O. Consequence: keeping the model *files* warm in cache buys
  nothing; the only way to avoid the ~4 s is to keep the *session* alive. Idle-unload
  is therefore a clean RAM-when-idle vs. fixed-~4 s-reload-penalty trade.

- **Functional confirmation:** the same SAPI-synthesized utterance transcribed
  identically across all three loads ("The quick brown fox jumps over the lazy
  dog."), decode ~336 ms each. Dispose→reload leaves no leaked state.

## Decision
**GO** for `infrastructure-d2v7n` (lazy-load + keep-warm + idle-unload). Disposing
the recognizer reclaims ~680 MB of committed RAM — easily enough to justify the
feature for a mostly-idle tray app. Reload is correct and a tolerable ~4 s, but
only *if* it is hidden behind speaking time and gated by a generous idle threshold.

Constraints the feature build must honour:

- **Lazy-load must start on Ctrl+Win key-DOWN, not on transcribe (release).**
  Dictation is push-to-talk batch (audio accumulates on hold, transcription runs on
  release — `DictationOrchestrator.cs`). Kicking the background load off at press
  overlaps it with the time the user spends speaking. For utterances longer than
  ~4 s the reload is fully hidden; transcription on release must `await` the
  in-flight load rather than starting a second one.

- **First short utterance after an idle-unload pays up to ~4 s.** For utterances
  shorter than the load time, release arrives before the session is ready and the
  user waits on the remaining load. This is the unavoidable worst case and is the
  reason the unload must be gated behind real idle, not fired eagerly.

- **Recommended idle-unload threshold: ~5–10 minutes of no dictation activity**
  (suggest 5 min as the default). This is deliberately *longer* than the 3-min
  `IdleWorkingSetTrimmer` window (ADR-0004): a working-set trim is nearly free to
  recover from (cold-page re-fault), but an unload costs a fixed ~4 s session
  rebuild, so unloading should only happen once the session is very likely cold for
  a while. Reuse the existing `NotifyActivity()` idle-tracking shape from
  `IdleWorkingSetTrimmer` rather than inventing a second activity signal.

- **Layering with the trim lever:** unload supersedes the trim during deep idle
  (it frees committed memory the trim only hides). Keep the 3-min trim as the cheap
  first stage and the ~5-min unload as the second; do not unload while a dictation
  is in flight.

## Allocator caveats
- The "arena retains freed blocks" risk is real in ONNX Runtime generally; here it
  did **not** bite, but the result is tied to this INT8 model + current
  `org.k2fsa.sherpa.onnx` / ORT version. If the model or ORT version changes
  (e.g. the Nemotron migration on this branch, or a quantization change), re-run the
  k9m3p harness to confirm Dispose still returns private bytes before trusting the
  idle-unload win.
- A naive RSS-only read right after Dispose would still *under*-report retention in
  the general case — always read `PrivateMemorySize64`, not just `WorkingSet64`,
  when validating an unload.

## Consequences
### Positive
- ~680 MB reclaimable during idle — the largest single RAM lever available.
- Reload is correct and deterministic (~4 s), and hides behind speaking time for
  normal-length dictation when load starts on key-down.

### Negative / Neutral
- First short utterance after deep idle adds up to ~4 s latency — bounded, and the
  cost of the RAM win. Mitigated by the generous idle threshold.
- Result is ORT/model-version dependent; must be re-validated after the model swap.

## Alternatives considered
- **Keep-warm only (never unload), rely on the working-set trim (ADR-0004).**
  Rejected as the RAM ceiling: the trim moves reported RSS but leaves ~700 MB of
  private/committed memory resident. Only an unload returns committed memory.
- **Unload on the same 3-min idle window as the trim.** Rejected — a 4 s reload is
  far more expensive to recover from than a cold-page re-fault, so the unload needs
  a longer fuse than the trim.
- **Warm the model files (prefetch into cache) to speed reload.** Rejected — load
  is session-init-bound, not I/O-bound (cold-after-gap ≈ warm), so file warming
  buys nothing.

## References
- Task: `.agentheim/contexts/infrastructure/done/infrastructure-k9m3p-model-unload-reload-spike.md`
- Gated feature: `infrastructure-d2v7n`
- Code exercised: `src/WhisperHeim/Services/Transcription/TranscriptionService.cs`
  (LoadModel / Dispose), `src/WhisperHeim/Services/Startup/WorkingSetTrimmer.cs`
  (fairness trim)
- Hot path: `src/WhisperHeim/Services/Dictation/DictationOrchestrator.cs`
  (push-to-talk batch transcribe-on-release)
- Related: ADR-0002 (Workstation GC), ADR-0004 (working-set trim + idle policy)
- Research: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`
