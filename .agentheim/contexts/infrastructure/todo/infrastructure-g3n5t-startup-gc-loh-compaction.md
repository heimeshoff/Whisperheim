---
id: infrastructure-g3n5t
title: Aggressive GC + LOH compaction once after startup
status: todo
type: feature
context: infrastructure
created: 2026-06-28
completed:
depends_on: []
blocks: []
tags: [memory, runtime, gc, performance]
related_adrs: []
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
