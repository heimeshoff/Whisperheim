---
id: infrastructure-q4t8m
title: "Warming up" overlay state when an utterance outruns the model load
status: backlog
type: feature
context: infrastructure
created: 2026-06-28
completed:
depends_on: [infrastructure-d2v7n]
blocks: []
tags: [overlay, ui, asr, parakeet, lifecycle, dictation]
related_adrs: [0005]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: [main-012, main-025]
---

## Why
With lazy-load + idle-unload ([[infrastructure-d2v7n]]), a short utterance right after an idle unload arrives at release *before* the ~4 s background reload has finished, so transcribe-on-release has to wait on the remaining load. Without feedback the dictation overlay just sits there and reads as **frozen / broken** rather than "loading". A brief "warming up" state makes the bounded wait legible so the worst-case short-utterance path feels intentional, not hung.

## What
Add a "warming up" visual state to the existing dictation overlay, shown only when transcribe-on-release is awaiting an in-flight model load.

- Reuse the existing dictation overlay (`main-012`) and its mic-state visualization (`main-025`) — add a state, don't build a new surface.
- Drive it from the lifecycle state machine in [[infrastructure-d2v7n]]: when release occurs while the model is still `Loading`, show "warming up"; clear it the moment the load completes and decode runs, transitioning into the normal transcription/result presentation.
- Normal-length utterances (load already finished by release) must never flash the warming-up state.

## Acceptance criteria
- [ ] When release occurs while the model is still `Loading`, the overlay shows a distinct "warming up" state (not the idle/frozen look).
- [ ] The state clears and transcription proceeds normally as soon as the load completes.
- [ ] For a normal-length utterance (load finished by release) the overlay behaves exactly as today — no warming-up flash.
- [ ] Verified via `/deploy`: force an idle unload, fire a short utterance, observe the warming-up state then the transcription.

## Notes
- Depends on [[infrastructure-d2v7n]] exposing a `Loading`/await signal the overlay can bind to — pin the exact hook against the core implementation once it lands, then promote.
- Prior art: `main-012` (dictation overlay), `main-025` (overlay mic-state visualization), `main-011` (dictation wiring).
- No `design-system/` BC exists, so the styleguide gate does not apply; this reuses the already-shipped overlay.
