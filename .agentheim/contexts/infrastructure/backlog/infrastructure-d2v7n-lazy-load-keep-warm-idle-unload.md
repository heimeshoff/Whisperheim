---
id: infrastructure-d2v7n
title: Lazy-load + keep-warm + idle-unload of the Parakeet model (capture-in-parallel)
status: backlog
type: feature
context: infrastructure
created: 2026-06-28
completed:
depends_on: [infrastructure-k9m3p]
blocks: []
tags: [memory, asr, parakeet, lifecycle, dictation]
related_adrs: []
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: []
---

## Why
The ~640 MB Parakeet recognizer is loaded eagerly at startup (`App.xaml.cs:332`) and held for the app lifetime, dominating WhisperHeim's ~1.3–1.4 GB idle footprint. Because Ctrl+Win dictation is push-to-talk batch — audio accumulates while the key is held and is transcribed in one shot on release (`DictationOrchestrator.cs:198,181,241`) — the model only needs to be resident *by release*, not at press. So we can free it while idle and reload it in the background the moment a dictation starts, with the load mostly hidden behind the time the user spends speaking. Net effect: idle footprint drops toward ~400–600 MB while normal-length dictation stays perceptually instant. Added latency is bounded by `max(0, load_time − hold_duration)`, i.e. only very short utterances right after a long idle pay anything.

## What
Implement a **lazy-load + keep-warm + idle-unload** lifecycle for the transcription model:
- **On hotkey press:** start audio capture immediately (already happens) and, if the model isn't resident, kick off an async background `LoadModelAsync` on a worker thread.
- **On release:** await the in-flight load task (if any), then transcribe the accumulated buffer as today.
- **Keep-warm:** once loaded, stay resident through an active dictation session; reset the idle timer on each dictation.
- **Idle-unload:** after a configurable idle timeout (default from the spike's recommendation, e.g. 5–10 min), `Dispose()` the recognizer and free the ~640 MB (pair with `GC.Collect()` + working-set trim per `infrastructure-w7k9p`).
- **State machine:** Unloaded → Loading → Loaded → (idle) → Unloaded. Handle: release-before-load-completes (wait), repeat presses during Loading (single shared load task), load failure / missing model (surface gracefully, don't crash), cancellation.
- **Overlay feedback:** when an utterance outruns the load, the dictation overlay shows a brief "warming up" state so it reads as loading, not frozen (reuse the existing overlay — see prior art below).
- Make eager-vs-lazy and the idle timeout configurable (settings), so the behavior can be turned off if the spike or field use shows it's not worth it.

## Acceptance criteria
- [ ] After the idle timeout with no dictation, process RAM drops by approximately the model size (verify the spike's measured delta actually materializes in steady state).
- [ ] A normal-length utterance (≥ ~3 s) after an idle unload produces a transcription with **no perceptible added latency** vs the always-resident baseline.
- [ ] A short utterance right after idle has only bounded added latency (≈ remaining load time) and shows the "warming up" overlay state rather than appearing frozen.
- [ ] Rapid consecutive dictations stay instant (keep-warm holds the model; no reload between them).
- [ ] Concurrency edge cases handled: release-before-loaded, multiple presses during load, load failure / missing model.
- [ ] Idle timeout and lazy-vs-eager are configurable; default chosen from spike findings.
- [ ] Before/after idle RAM and short/long-utterance latency measured via `/deploy` and recorded.

## Notes
- **Gated by `infrastructure-k9m3p`** (spike). If the spike finds `Dispose()` does not return meaningful RAM to the OS, this feature is not worth building as specified — re-scope or drop. Do not promote to `todo/` until the spike returns go.
- **Constraint:** must not break instant Ctrl+Win dictation for normal-length speech. The whole design hinges on transcribe-on-release + faster-than-realtime decode, so the model-ready-by-release window covers typical utterances.
- Prior art (main BC, for reuse): `main-008` (Parakeet ASR integration — LoadModel/Dispose surface), `main-011` (end-to-end dictation wiring), `main-012` (dictation overlay) and `main-025` (overlay mic-state visualization) for the "warming up" overlay state, `main-004` (global hotkey).
- Interacts with `infrastructure-w7k9p` (working-set trim): unloading *frees* the model outright, which is stronger than trimming its pages; the trim still helps the non-model .NET/WPF/ONNX overhead. The two compose.
- Consider (optional, later): pre-warm the load on a cheaper signal than full hotkey detection (e.g. modifier-down) to hide load before release on short utterances. Out of scope for the first cut.
- Context: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`.
