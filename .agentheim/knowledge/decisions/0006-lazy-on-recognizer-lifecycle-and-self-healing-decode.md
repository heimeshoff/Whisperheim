---
id: 0006
title: Recognizer lifecycle ships lazy-on, and decode self-heals so every consumer survives an idle-unload
scope: infrastructure
status: accepted
date: 2026-06-28
supersedes: []
superseded_by: []
related_tasks: [infrastructure-d2v7n, infrastructure-k9m3p, infrastructure-q4t8m]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
---

# ADR 0006: Lazy-on recognizer lifecycle + self-healing decode

## Context
ADR-0005 returned GO for lazy-load + keep-warm + idle-unload of the ~640 MB INT8
Parakeet recognizer and fixed the dictation-path constraints (load on key-DOWN,
await on release, ~5-min idle fuse). Building it (`infrastructure-d2v7n`) surfaced
two implementation decisions ADR-0005 did not settle:

1. **Eager vs lazy at startup.** The app previously loaded the recognizer eagerly
   in `App.StartupCore` and held it for the process lifetime.
2. **The recognizer is shared.** `TranscriptionService` is consumed not only by the
   Ctrl+Win dictation path but also by the loopback HTTP API (ADR-0001), file
   transcription, stream transcription, and the call-transcription pipeline. An
   idle-unload frees memory out from under *all* of them, but only the dictation
   path was given an explicit load/await handshake.

## Decision
- **Ship lazy-on.** Remove the eager startup `LoadModel()`. The model is loaded on
  the first key-DOWN (overlapping speaking time) or on the first transcription from
  any consumer. This delivers the ~680 MB idle saving from boot, not just after the
  first idle period. The lazy-vs-eager *toggle* is deferred to
  `infrastructure-b3n6p`; this task hardcodes lazy-on with a 5-min constant.
- **Decode self-heals.** `TranscriptionService.TranscribeAsync` no longer throws
  when the model is absent; inside the decode lock it calls `LoadModelLocked()` to
  (re)build the recognizer on demand. Any consumer — dictation, HTTP, file, stream —
  transparently survives an idle-unload and pays the ~4 s rebuild only if it is the
  one to wake the model.
- **One lock is the unload-safety invariant.** Load, decode, and `Unload()` all take
  the same `_lock`. This guarantees an idle-unload can never `Dispose()` the
  recognizer mid-decode, regardless of which consumer is decoding — so the
  `ModelLifecycleManager` busy-guard (dictation in flight) is a latency optimisation,
  not the correctness mechanism.
- **State machine is isolated from the model.** `ModelLifecycleManager` owns the
  Unloaded → Loading → Loaded → idle → Unloaded transitions over injected
  load/unload delegates, reusing the `IdleWorkingSetTrimmer` `NotifyActivity` + poll
  shape (ADR-0004). It is fully unit-tested without the real ~640 MB model.

## Consequences
- First dictation (and first HTTP/file transcription) after boot or after an idle
  unload pays up to ~4 s, hidden behind speaking time for utterances ≥ ~4 s
  (ADR-0005). Short utterances right after a deep idle feel like a pause; the
  perceived-loading overlay is `infrastructure-q4t8m`.
- `Unload()` is non-terminal (distinct from `Dispose()`): it frees the recognizer
  but leaves the service reusable. `Unload` pairs Dispose with `GC.Collect()` +
  `WorkingSetTrimmer.Trim()` per the k9m3p harness.
- The 5-min unload fuse stays deliberately longer than the 3-min working-set-trim
  fuse (ADR-0004): a ~4 s session rebuild costs far more than a cold-page re-fault.

## References
- `src/WhisperHeim/Services/Transcription/ModelLifecycleManager.cs` (state machine)
- `src/WhisperHeim/Services/Transcription/TranscriptionService.cs` (LoadModel/EnsureLoadedAsync/Unload/self-healing decode)
- `src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs` (key-DOWN BeginLoad, await-on-release)
- `src/WhisperHeim/App.xaml.cs` (lazy-on wiring, 5-min idle poll, unload action)
- `tests/WhisperHeim.Tests/ModelLifecycleManagerTests.cs`
- Builds on ADR-0005 (GO + constraints), ADR-0004 (trim + idle policy), ADR-0001 (HTTP consumer)
