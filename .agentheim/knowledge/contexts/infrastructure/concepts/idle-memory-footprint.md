---
name: idle-memory-footprint
description: How WhisperHeim keeps its resident RAM low while idle without breaking instant Ctrl+Win dictation
context: infrastructure
created: 2026-06-28
last_updated: 2026-06-28
derived_from:
  - 0002                                         # Workstation GC
  - 0003                                         # one-shot startup LOH compaction
  - 0004                                         # working-set trim after load + on idle
  - 0005                                         # GO on idle-unload of the recognizer
  - parakeet-quantization-and-nemotron-2026-06-28  # research: the ~640 MB floor
  - infrastructure-h4m2q                         # done: GC mode
  - infrastructure-g3n5t                         # done: startup compaction
  - infrastructure-w7k9p                         # done: working-set trim
  - infrastructure-k9m3p                         # done: dispose/reload spike
  - main-t6r2k                                   # done (main BC): ASR threads 4→2
max_lines: 60
---

# Idle memory footprint — concept

## What it is
WhisperHeim is a mostly-idle Windows tray app that holds the INT8 Parakeet
recognizer (~640 MB) resident so Ctrl+Win dictation starts instantly. This
concept is the standing effort to shrink the *idle resident footprint* (~1.3–1.4 GB
at the outset) **without** ever making that first keypress feel slow.

## Why it exists
The research report established the hard floor: ~640 MB is the model + ONNX
runtime, and GC/runtime tuning can't touch it — so the levers split into two
families. Each lever ships with its own before/after `/deploy` measurement; the
measurement *is* the deliverable, which is why they were landed sequentially.

## Current shape
Two families of levers, plus the recognizer lifecycle that is the real prize:

- **Runtime/GC tuning (resident overhead above the floor):** ADR-0002 — Workstation
  GC + concurrent (idle private mem −47 MB). main-t6r2k — ASR intra-op threads 4→2
  (+~60 ms decode, still ~13× real-time).
- **Post-startup housekeeping hook** in `App.StartupCore` ("compact, then trim"):
  ADR-0003 `StartupMemoryCompactor` (one-shot LOH-compacting gen-2 GC ~5 s after
  boot, off the UI thread) → ADR-0004 `WorkingSetTrimmer`/`IdleWorkingSetTrimmer`
  (`EmptyWorkingSet` P/Invoke after load + on 3-min idle; drops reported RSS to
  standby, doesn't free committed memory). Both in `src/WhisperHeim/Services/Startup/`.
- **Recognizer lifecycle (the big lever):** ADR-0005 — the k9m3p spike proved
  `Dispose()` returns ~679 MB private bytes (ONNX arena retention did *not* bite)
  and reload is a deterministic ~4 s. GO for the idle-unload feature.

## The invariant that binds them
Every lever in this concept must preserve **instant Ctrl+Win dictation**: the
recognizer stays resident (trimming ≠ unloading), housekeeping runs off the UI
thread after a delay, and the future idle-unload reloads lazily in parallel with
the user's speech. Any change here is judged against that constraint first.

## Open questions
- **Idle-unload not yet built.** ADR-0005 is GO; `infrastructure-d2v7n` (lazy-load
  + keep-warm + idle-unload, ~5-min threshold) is still in backlog awaiting
  promotion to todo. It's the only lever that reclaims the ~640 MB floor.
- **Re-validate after a model swap.** The dispose-returns-RAM and reload-time
  numbers are Parakeet/ONNX-specific; a Nemotron Speech swap would need re-measuring.
- **Perceptual checks are user-owned.** The "feels instant" criteria across these
  tasks were machine-measured where possible and otherwise left for the user.

## See also
- `[[0002]]` Workstation GC · `[[0003]]` startup LOH compaction · `[[0004]]` working-set trim · `[[0005]]` idle-unload GO
- `[research/parakeet-quantization-and-nemotron-2026-06-28]` — the ~640 MB floor
- `[done/infrastructure-h4m2q]` · `[done/infrastructure-g3n5t]` · `[done/infrastructure-w7k9p]` · `[done/infrastructure-k9m3p]` · `[done/main-t6r2k]` (main BC)
- `[backlog/infrastructure-d2v7n]` — the unbuilt idle-unload feature
