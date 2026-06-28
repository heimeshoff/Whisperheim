---
id: infrastructure-g3n5t
title: Aggressive GC + LOH compaction once after startup
status: done
type: feature
context: infrastructure
created: 2026-06-28
completed: 2026-06-28
depends_on: []
blocks: []
tags: [memory, runtime, gc, performance]
related_adrs: [0003]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: []
---

## Why
Loading the Parakeet model and booting WPF allocates large transient buffers (model file reads, decode/setup scratch, XAML/resource init). On the managed heap, large allocations land on the Large Object Heap, which is not compacted by default and can leave the heap larger than the live set after startup settles. A single deliberate compacting collection once the app has finished booting can return that slack to the OS without affecting steady-state behavior.

## What
After startup completes (model loaded, main window/tray initialized), trigger one compacting GC:
- Set `GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce`.
- Call `GC.Collect()` (gen 2, blocking) followed by `GC.WaitForPendingFinalizers()` and a second `GC.Collect()`.
- Run it off the UI thread / after a short post-startup delay so it doesn't compete with first-frame rendering or the first dictation.
- Do this **once** at startup — not on a recurring timer (recurring forced GC would hurt, not help). Pairs naturally with the post-load working-set trim in `infrastructure-w7k9p`.

## Acceptance criteria
- [ ] A one-shot LOH-compacting gen-2 GC runs shortly after startup, off the UI thread.
- [ ] App startup and first Ctrl+Win dictation remain responsive (the collection must not block the user's first interaction).
- [ ] Instant dictation unaffected; Parakeet stays resident.
- [ ] Steady-state RAM measured **before and after** via `/deploy` (idle footprint a few minutes post-launch).
- [ ] Result (before/after MB) recorded on completion. (May be a modest win on its own; expected to compound with the GC-mode and working-set tasks.)

## Notes
- **Constraint (shared across the RAM-optimization set):** must not break instant Ctrl+Win dictation. Parakeet (~640 MB) stays resident.
- Interacts with `infrastructure-h4m2q` (GC mode): under Workstation GC the compaction behaves differently than under Server GC, so measure this *after* the GC switch has landed for a meaningful number.
- Could be implemented in the same post-startup hook as the working-set trim (`infrastructure-w7k9p`) — compact, then trim.
- Sibling tasks: `infrastructure-h4m2q`, `infrastructure-w7k9p`, `main-t6r2k`.
- Context: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`.
- Decision recorded: ADR-0003 (`.agentheim/knowledge/decisions/0003-one-shot-startup-loh-compaction.md`).

## Outcome
Added `StartupMemoryCompactor` (`src/WhisperHeim/Services/Startup/StartupMemoryCompactor.cs`):
a one-shot, idempotent, LOH-compacting blocking gen-2 collection
(`CompactOnce` → gen-2 `GC.Collect` → `WaitForPendingFinalizers` → gen-2
`GC.Collect`). It is scheduled on a thread-pool thread 5 s after boot from the
end of `App.StartupCore` (the new shared **post-startup housekeeping hook**), so
it never blocks first-frame render or the first Ctrl+Win dictation. The
working-set trim (`infrastructure-w7k9p`) appends after the compaction in the
same hook ("compact, then trim"). An escape-hatch env var
`WHISPERHEIM_DISABLE_STARTUP_GC=1` skips it.

Measured before/after via `scripts/publish.ps1` (self-contained Release, ~90 s
idle settle, model resident at the ~640 MB+ floor, Workstation GC):

| Config                  | WorkingSet64 | PrivateMemorySize64 |
|-------------------------|--------------|---------------------|
| Compaction off (before) | 836 MB       | 777 MB              |
| Compaction on  (after)  | 840 MB       | 782 MB              |
| **Delta**               | **+4 MB**    | **+5 MB**           |

Honest result: **no measurable standalone win** (within run-to-run noise) — under
Workstation GC + concurrent, background collection already reclaims most LOH
slack by idle, and the dominant ~640 MB Parakeet + ONNX cost is native memory a
managed compaction cannot touch. Kept as the precursor half of "compact, then
trim": it ensures the upcoming `w7k9p` working-set trim does not re-fault
compactable LOH garbage. Rationale in ADR-0003.

Tests: `tests/WhisperHeim.Tests/StartupMemoryCompactorTests.cs` (3 tests — the
compacting collection actually runs and reverts `CompactOnce` to `Default`; the
operation is one-shot; the scheduled run executes on a thread-pool thread off
the UI thread). Full suite: 138 passing.

Key files:
- `src/WhisperHeim/Services/Startup/StartupMemoryCompactor.cs` (new)
- `src/WhisperHeim/App.xaml.cs` (field + scheduling at end of `StartupCore`)
- `tests/WhisperHeim.Tests/StartupMemoryCompactorTests.cs` (new)
- `.agentheim/knowledge/decisions/0003-one-shot-startup-loh-compaction.md` (new)
