---
id: infrastructure-d2v7n
title: Lazy-load + keep-warm + idle-unload of the Parakeet model — core lifecycle
status: done
type: feature
context: infrastructure
created: 2026-06-28
completed: 2026-06-28
depends_on: [infrastructure-k9m3p]
blocks: [infrastructure-q4t8m]
tags: [memory, asr, parakeet, lifecycle, dictation]
related_adrs: [0005, 0006]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: []
---

## Why
The ~640 MB Parakeet recognizer is loaded eagerly at startup (`App.xaml.cs:332`) and held for the app lifetime, dominating WhisperHeim's ~1.3–1.4 GB idle footprint. Because Ctrl+Win dictation is push-to-talk batch — audio accumulates while the key is held and is transcribed in one shot on release (`DictationOrchestrator.cs:198,181,241`) — the model only needs to be resident *by release*, not at press. So we can free it while idle and reload it in the background the moment a dictation starts, with the load mostly hidden behind the time the user spends speaking.

The spike `infrastructure-k9m3p` (ADR-0005) measured this directly and returned **GO**:
- `Dispose()` returns **~680 MB of private bytes** to the OS (707 → 28 MB; the feared ONNX-arena retention did *not* bite for this INT8 build / ORT version).
- Reload is a **fixed ~4 s**, independent of file-cache state — it is session-init-bound, not I/O-bound. The only way to avoid it is to keep the *session* alive (keep-warm); warming the model files buys nothing.
- Dispose → reload is functionally clean (identical transcripts, no leaked state).

Net effect: idle footprint drops by ~680 MB while normal-length dictation stays perceptually instant. Added latency is bounded by `max(0, ~4 s − hold_duration)` — only very short utterances right after a deep idle pay anything, and that case is gated behind a generous idle threshold.

## What
Implement the **lazy-load + keep-warm + idle-unload** state machine for the transcription model. This task is the core lifecycle only; the "warming up" overlay feedback ([[infrastructure-q4t8m]]) and the lazy-vs-eager / timeout settings ([[infrastructure-b3n6p]]) are split out as dependents.

- **On hotkey key-DOWN:** start audio capture immediately (already happens) and, if the model isn't resident, kick off an async background `LoadModelAsync` on a worker thread. Start on **key-DOWN, not on release** (ADR-0005) so the ~4 s reload overlaps speaking time.
- **On release:** `await` the in-flight load task (if any) — never start a second load — then transcribe the accumulated buffer as today.
- **Keep-warm:** once loaded, stay resident through active dictation; reset the idle timer on each dictation.
- **Idle-unload:** after the idle timeout (**5 min default**, hardcoded constant in this task), `Dispose()` the recognizer and free the ~680 MB, paired with `GC.Collect()` + `WorkingSetTrimmer.Trim()` (per `infrastructure-w7k9p` / ADR-0004). Never unload while a dictation is in flight.
- **State machine:** Unloaded → Loading → Loaded → (idle) → Unloaded. Handle: release-before-load-completes (wait), repeat presses during Loading (single shared load task), load failure / missing model (surface gracefully, don't crash), cancellation.
- **Idle tracking:** reuse the existing `NotifyActivity()` / idle-tracking shape from `IdleWorkingSetTrimmer` (ADR-0004) rather than inventing a second activity signal. The unload fuse (5 min) is deliberately *longer* than the 3-min working-set-trim fuse — a 4 s session rebuild is far costlier to recover from than a cold-page re-fault.

## Acceptance criteria
- [ ] After ~5 min idle with no dictation, process **private bytes** (`PrivateMemorySize64`, not just `WorkingSet64` — ADR-0005 caveat) drop by ~680 MB, verified via `/deploy`.
- [ ] A normal-length utterance (≥ ~4 s, the measured reload time) after an idle unload produces a transcription with **no perceptible added latency** vs the always-resident baseline (load hidden behind speaking time).
- [ ] Rapid consecutive dictations stay instant — keep-warm holds the model, no reload between them; each dictation resets the idle timer.
- [ ] Lazy-load starts on Ctrl+Win key-DOWN; transcribe-on-release awaits the in-flight load rather than starting a second one.
- [ ] Concurrency edge cases handled: release-before-loaded (await), multiple presses during Loading (single shared load task), load failure / missing model (graceful, no crash), cancellation.
- [ ] State machine Unloaded → Loading → Loaded → (idle) → Unloaded implemented; idle-unload does `Dispose()` + `GC.Collect()` + `WorkingSetTrimmer.Trim()` and reuses the `NotifyActivity()` idle shape.
- [ ] Before/after idle RAM and short/long-utterance latency measured via `/deploy` and recorded.

## Notes
- **Spike cleared the blocker.** `infrastructure-k9m3p` returned GO (ADR-0005). Dispose returns ~680 MB; reload is a fixed ~4 s. The Nemotron-swap re-validation caveat in ADR-0005 is moot — that branch name was retired and the model stays INT8 Parakeet.
- **Constraint:** must not break instant Ctrl+Win dictation for normal-length speech. The design hinges on transcribe-on-release + faster-than-realtime decode, so the model-ready-by-release window covers typical utterances (anything ≥ ~4 s of speaking fully hides the reload).
- **Short-utterance worst case:** an utterance shorter than ~4 s right after a deep idle pays up to the remaining load time. This task awaits the load (correct but feels like a pause); the perceived-loading feedback is [[infrastructure-q4t8m]].
- **Defaults are hardcoded here; configurability is [[infrastructure-b3n6p]]** — this task ships lazy-on with a 5-min constant so it works end-to-end on its own.
- Interacts with `infrastructure-w7k9p` (working-set trim): unloading *frees* the model outright (committed memory), which supersedes the trim during deep idle; the 3-min trim stays as the cheap first stage and the ~5-min unload as the second. The trim still helps the non-model .NET/WPF/ONNX overhead.
- Prior art (main BC, for reuse): `main-008` (Parakeet ASR integration — LoadModel/Dispose surface), `main-011` (end-to-end dictation wiring), `main-004` (global hotkey).
- Context: ADR-0005 (`.agentheim/knowledge/decisions/0005-idle-unload-of-parakeet-recognizer-go.md`), research `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`. Hot-path mechanics: `TranscriptionService.cs` (LoadModel/Dispose), `DictationOrchestrator.cs` (push-to-talk batch transcribe-on-release).
- Implementation decision recorded in **ADR-0006** (`0006-lazy-on-recognizer-lifecycle-and-self-healing-decode.md`): ship lazy-on (no eager startup load) + self-healing decode so all shared consumers survive an unload.

## Outcome
Implemented the lazy-load + keep-warm + idle-unload lifecycle for the ~640 MB INT8
Parakeet recognizer. The model is no longer loaded eagerly at startup; it loads on
Ctrl+Win key-DOWN, stays warm through dictation, and idle-unloads after 5 min,
reclaiming ~680 MB of committed memory (per ADR-0005).

**Design.** A new `ModelLifecycleManager` (`src/WhisperHeim/Services/Transcription/ModelLifecycleManager.cs`)
owns the Unloaded → Loading → Loaded → idle → Unloaded state machine over injected
load/unload delegates, reusing the `IdleWorkingSetTrimmer` `NotifyActivity`+poll
shape (ADR-0004). The heavyweight recognizer is isolated behind the delegate seam so
the lifecycle is unit-tested without the real model. `TranscriptionService` gained
`EnsureLoadedAsync` (background, idempotent, shared load), non-terminal `Unload()`
(Dispose + return memory, distinct from terminal `Dispose()`), thread-safe
`LoadModel`, and **self-healing decode** (`TranscribeAsync` reloads under the decode
lock) so the loopback HTTP API, file/stream/call transcription all survive an unload.
The shared decode/load/unload lock is the unload-safety invariant. Wiring in
`App.xaml.cs`: lazy-on, 5-min idle poll (30 s cadence), unload = Unload+GC+trim,
`NotifyActivity` on dictation state changes. `DictationOrchestrator` calls
`BeginLoad()` on key-DOWN and `EnsureLoadedAsync()` before transcribe-on-release,
with `EnterDictation`/`ExitDictation` bracketing the in-flight guard.

**Acceptance criteria status:**
- ✅ Lazy-load on key-DOWN; transcribe-on-release awaits the in-flight load, never
  starts a second — *code-complete + unit-tested* (`RepeatPressesDuringLoading_ShareASingleLoadTask`,
  `EnsureLoadedAsync_AwaitsInFlightLoad_StartedByKeyDown`).
- ✅ Concurrency edges — release-before-loaded (await), repeat presses (single shared
  load), load failure / missing model (graceful + retry, no crash), cancellation —
  *code-complete + unit-tested* (15 tests in `ModelLifecycleManagerTests`).
- ✅ State machine + idle-unload does Dispose + `GC.Collect()` + `WorkingSetTrimmer.Trim()`
  and reuses the `NotifyActivity` shape — *code-complete + unit-tested*
  (`PollOnce_UnloadsAfterIdle_*`, `AfterIdleUnload_NextLoadReloads`).
- ✅ Rapid consecutive dictations stay warm; each resets the idle timer; never unload
  while a dictation is in flight — *code-complete + unit-tested*
  (`PollOnce_DoesNotUnload_WhileDictationInFlight`, `NotifyActivity_PreventsUnload_ByResettingIdleClock`).
- ⏳ **Needs live `/deploy` measurement (not runnable in this environment):**
  - Idle RAM drop ~680 MB after ~5 min: launch `/deploy`, idle 5+ min with no
    dictation, confirm **`PrivateMemorySize64`** (not just `WorkingSet64` — ADR-0005
    caveat) falls by ~680 MB (expect ~700 MB → ~30 MB for the recognizer portion).
  - Short/long-utterance latency: a ≥~4 s utterance after an idle unload should feel
    instant vs. the always-resident baseline (load hidden behind speaking); a <4 s
    utterance right after a deep idle pays up to the remaining ~4 s load (expected
    worst case, perceived-loading overlay is `infrastructure-q4t8m`).

The full test suite is green (162 passed, incl. 15 new lifecycle tests) and the WPF
app builds clean.
