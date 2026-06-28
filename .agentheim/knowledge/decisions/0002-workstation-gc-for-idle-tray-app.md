---
id: 0002
title: Use Workstation GC (+ concurrent) instead of Server GC for the tray app
scope: global
status: accepted
date: 2026-06-28
supersedes: []
superseded_by: []
related_tasks: [infrastructure-h4m2q, infrastructure-w7k9p, infrastructure-g3n5t, main-t6r2k]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
---

# ADR 0002: Use Workstation GC (+ concurrent) instead of Server GC for the tray app

## Context
WhisperHeim is a mostly-idle Windows desktop tray app: it sits waiting for a
Ctrl+Win dictation trigger, with the INT8 Parakeet recognizer (~640 MB) held
resident so dictation starts instantly. The csproj shipped with
`<ServerGarbageCollection>true</ServerGarbageCollection>` and
`<GarbageCollectionAdaptationMode>0</GarbageCollectionAdaptationMode>` (the
latter disabling .NET 9's DATAS, which would otherwise let Server GC shrink its
heaps under low load).

Server GC allocates one heap per CPU core and retains memory aggressively to
optimize allocation throughput on multi-core server workloads. That is the wrong
trade for a single-user app that spends almost all its time idle: it pays heap
overhead per core for throughput the app never needs. This was flagged as the
single biggest, lowest-risk RAM lever in the RAM-optimization task set.

## Decision
Switch to Workstation GC with concurrent collection:
- `<ServerGarbageCollection>false</ServerGarbageCollection>`
- `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`
- Remove `<GarbageCollectionAdaptationMode>0</GarbageCollectionAdaptationMode>`
  (DATAS is a Server-GC concern; moot under Workstation GC, and leaving it set to
  0 only disabled a memory-saving feature).

Concurrent collection keeps gen-2 collections off the UI thread, preserving the
app's instant-feel responsiveness.

## Measured outcome
Idle resident footprint, self-contained Release build via `scripts/publish.ps1`,
read a few minutes after launch with no transcription running (model resident,
stable readings):

| Config                  | WorkingSet64 | PrivateMemorySize64 |
|-------------------------|--------------|---------------------|
| Server GC (before)      | 837 MB       | 823 MB              |
| Workstation GC (after)  | 835 MB       | 776 MB              |
| **Delta**               | **-2 MB**    | **-47 MB**          |

The reduction was much smaller than the ~200-400 MB the task estimated. The idle
footprint on this machine was already ~837 MB (well below the ~1.3-1.4 GB cited in
the task's premise — that higher figure is likely a post-transcription / grown
working-set reading, not fresh idle). The bulk of resident memory is the ~640 MB
Parakeet model plus ONNX runtime, which GC mode does not touch; Server GC's extra
retention at idle on this workload turned out to be modest (~47 MB private).

## Consequences
### Positive
- ~47 MB private-memory reduction at idle, zero added runtime weight (config-only).
- Correct GC mode for a single-user idle desktop app; concurrent collection avoids
  UI-thread stalls so dictation responsiveness is preserved.
- Clean attribution baseline for the sibling RAM-optimization tasks
  (`infrastructure-w7k9p` working-set trim, `infrastructure-g3n5t` startup
  GC+LOH, `main-t6r2k` ASR thread reduction) to stack on top of.

### Negative / Neutral
- The win is small, so GC mode is not the lever that closes the gap to the
  ~640 MB Parakeet floor — the remaining overhead is ONNX/runtime, addressed by
  the sibling tasks, not by GC tuning.
- If Server GC is ever reinstated (e.g. for a future throughput-bound server
  surface), flip DATAS back on with `<GarbageCollectionAdaptationMode>1</...>`.

## Alternatives considered
- **Keep Server GC, enable DATAS (`GarbageCollectionAdaptationMode>1`).** Would
  let Server GC shrink heaps under low load without abandoning Server mode.
  Rejected: Workstation GC is the more direct fit for a single-core-bound idle
  desktop app, and DATAS only mitigates a problem Workstation GC avoids by
  construction.
- **Non-concurrent Workstation GC.** Rejected: blocking gen-2 collections on the
  UI thread risk perceptible stalls during/after dictation; concurrent is the
  right default for an interactive app.

## References
- Task: `.agentheim/contexts/infrastructure/done/infrastructure-h4m2q-workstation-gc.md`
- Research: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`
  (~640 MB resident Parakeet floor)
- Config: `src/WhisperHeim/WhisperHeim.csproj`
