---
id: infrastructure-b3n6p
title: Make lazy-load / idle-unload configurable (lazy-vs-eager + idle timeout)
status: backlog
type: feature
context: infrastructure
created: 2026-06-28
completed:
depends_on: [infrastructure-d2v7n]
blocks: []
tags: [settings, asr, parakeet, lifecycle, configuration]
related_adrs: [0005, 0004]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: []
---

## Why
The core lifecycle ([[infrastructure-d2v7n]]) ships lazy-on with a hardcoded 5-min idle-unload fuse (ADR-0005's recommended default). But the win is machine- and usage-dependent, and the worst-case short-utterance pause is a real trade-off some users won't want. The behavior should be switchable off and the timeout tunable without a rebuild, so it can be turned off if field use shows it isn't worth the latency.

## What
Expose the lifecycle knobs in settings and wire them into [[infrastructure-d2v7n]].

- **Lazy-vs-eager toggle:** eager = the pre-d2v7n always-resident behavior (load at startup, never idle-unload); lazy = the d2v7n lifecycle.
- **Idle-unload timeout:** the no-dictation duration before unload. Default **5 min** (ADR-0005).
- Persist both in `settings.json`; have the core lifecycle read them in place of its hardcoded constants.

## Acceptance criteria
- [ ] Settings expose a lazy/eager toggle and an idle-unload timeout, both persisted in `settings.json`.
- [ ] Eager mode keeps the model resident — no idle-unload — i.e. reverts to pre-d2v7n behavior.
- [ ] Lazy mode uses the configured timeout; default = 5 min when unset.
- [ ] Invalid / missing values fall back to the defaults rather than crashing.
- [ ] Toggling eager → lazy (and back) takes effect without a restart, or the restart requirement is documented.

## Notes
- Depends on [[infrastructure-d2v7n]] landing the lifecycle and its hardcoded defaults; this task replaces the constants with settings reads. Pin the exact config seam against the core implementation, then promote.
- ADR-0004 already defines the idle-tracking/`NotifyActivity()` shape the timeout feeds into; keep the trim's 3-min fuse and the unload's (configurable) fuse as two distinct stages.
- Settings surface: follow the existing `settings.json` / settings-path resolution in the infrastructure BC (data-path / bootstrap config). No `design-system/` BC exists, so no styleguide gate.
