---
id: 0003
title: One-shot LOH-compacting GC after startup (precursor to the working-set trim)
scope: infrastructure
status: accepted
date: 2026-06-28
supersedes: []
superseded_by: []
related_tasks: [infrastructure-g3n5t, infrastructure-w7k9p, infrastructure-h4m2q]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
---

# ADR 0003: One-shot LOH-compacting GC after startup

## Context
Loading the INT8 Parakeet model and initializing WPF allocates large transient
buffers (model file reads, decode/setup scratch, XAML/resource init). Large
managed allocations land on the Large Object Heap, which under .NET's default
policy is **not** compacted, so after startup settles the managed heap can stay
larger than the live set. The RAM-optimization task set
(`infrastructure-g3n5t`) hypothesized that a single deliberate compacting gen-2
collection once the app finishes booting could return that slack to the OS
without affecting steady-state behaviour. This runs under Workstation GC +
concurrent (ADR-0002), which had just landed.

## Decision
Add a `StartupMemoryCompactor` (`Services/Startup/`) that performs **one**
LOH-compacting blocking gen-2 collection
(`GCLargeObjectHeapCompactionMode.CompactOnce` followed by a gen-2
`GC.Collect` → `WaitForPendingFinalizers` → gen-2 `GC.Collect`). It is:
- **One-shot**, guarded by an interlocked flag — never on a recurring timer
  (recurring forced GC would hurt, not help).
- Scheduled on a **thread-pool thread after a 5 s post-startup delay**, so it
  never competes with first-frame rendering or the user's first Ctrl+Win
  dictation.
- Wired at the end of `App.StartupCore` as the shared **post-startup
  housekeeping hook**. The working-set trim (`infrastructure-w7k9p`) appends
  onto this same delayed task, immediately after the compaction
  ("compact, then trim").
- Gated by an escape-hatch env var `WHISPERHEIM_DISABLE_STARTUP_GC=1` (set it to
  skip the compaction), which also enabled clean A/B measurement.

## Measured outcome
Idle footprint, self-contained Release via `scripts/publish.ps1`, sampled ~90 s
after launch (model resident at the ~640 MB+ floor, readings stable), Workstation
GC:

| Config                        | WorkingSet64 | PrivateMemorySize64 |
|-------------------------------|--------------|---------------------|
| Compaction off (before)       | 836 MB       | 777 MB              |
| Compaction on  (after)        | 840 MB       | 782 MB              |
| **Delta**                     | **+4 MB**    | **+5 MB**           |

**No measurable standalone win** — the ~5 MB difference is within run-to-run
noise. Under Workstation GC + concurrent, idle-time background collection has
already reclaimed most reclaimable LOH slack by the time the app settles, and
the dominant resident cost (~640 MB Parakeet + ONNX native runtime) lives in
native, not managed, memory that a managed compaction cannot touch.

## Consequences
### Positive
- The compaction is harmless (one-shot, off-thread, after a delay) and is the
  natural **precursor to the working-set trim** (`infrastructure-w7k9p`):
  compacting the LOH first means the subsequent `EmptyWorkingSet` trim does not
  immediately re-fault compactable garbage back in. Its value is as a building
  block of "compact, then trim", not as a standalone lever.
- The post-startup housekeeping hook is now structured so the trim drops in
  cleanly without re-touching the App startup flow.
- `WHISPERHEIM_DISABLE_STARTUP_GC` gives maintainers a runtime A/B toggle.

### Negative / Neutral
- On its own this returns no measurable steady-state RAM. Kept anyway as the
  precursor described above; if the working-set trim is ever abandoned and the
  compaction still shows no benefit, this can be removed.

## Alternatives considered
- **Recurring forced GC on a timer.** Rejected outright: repeated blocking
  collections waste CPU and battery and can stall the UI for no idle-RAM gain.
- **Skip the compaction, only do the working-set trim.** Viable, but trimming a
  fragmented, un-compacted LOH leaves compactable pages resident that re-fault
  on next touch; compacting first is the cheaper, cleaner ordering.
- **Drop the feature given the null measurement.** Rejected: it is the precursor
  half of the working-set-trim task and the coordination contract for
  `infrastructure-w7k9p` depends on the shared hook existing.

## References
- Task: `.agentheim/contexts/infrastructure/done/infrastructure-g3n5t-startup-gc-loh-compaction.md`
- Code: `src/WhisperHeim/Services/Startup/StartupMemoryCompactor.cs`, `src/WhisperHeim/App.xaml.cs`
- Tests: `tests/WhisperHeim.Tests/StartupMemoryCompactorTests.cs`
- Prior GC decision: ADR-0002 (Workstation GC)
- Research: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`
