---
id: infrastructure-h4m2q
title: Switch Server GC → Workstation GC + concurrent
status: done
type: refactor
context: infrastructure
created: 2026-06-28
completed: 2026-06-28
depends_on: []
blocks: []
tags: [memory, runtime, gc, performance]
related_adrs: [0002]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: []
---

## Why
WhisperHeim sits at a steady ~1.3–1.4 GB resident footprint. A codebase investigation found the single biggest, lowest-risk lever is the .NET GC mode. `src/WhisperHeim/WhisperHeim.csproj:12` sets `<ServerGarbageCollection>true</ServerGarbageCollection>`, and line 13 sets `<GarbageCollectionAdaptationMode>0</GarbageCollectionAdaptationMode>`, which **disables DATAS** — the .NET 9 feature that would otherwise let Server GC shrink its heaps under low load. Server GC allocates a heap per CPU core and holds memory aggressively; it's the wrong mode for a mostly-idle desktop tray app. This is the most memory-hungry GC config available, for no benefit on this workload.

## What
Switch the app to Workstation GC with concurrent collection. In `WhisperHeim.csproj`:
- Set `<ServerGarbageCollection>false</ServerGarbageCollection>`.
- Add `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`.
- Remove (or revisit) the `<GarbageCollectionAdaptationMode>0</GarbageCollectionAdaptationMode>` line — DATAS is a Server-GC concern; once on Workstation GC it's moot, and leaving it set to 0 only serves to disable a memory-saving feature. (If for any reason Server GC is kept instead, flip this to `1` to enable DATAS.)

## Acceptance criteria
- [x] `WhisperHeim.csproj` builds and the app launches normally with Workstation + concurrent GC. (Verified: self-contained Release publish via `scripts/publish.ps1` built and launched cleanly; process stayed running.)
- [x] No regression in instant Ctrl+Win dictation responsiveness (Parakeet ~640 MB stays resident; this change does not touch model loading). (Partially verifiable: model remained resident — idle WS 835 MB includes the ~640 MB Parakeet floor; concurrent GC keeps gen-2 off the UI thread. The final perceptual instant-feel check is left for the user during normal use — not hand-measured here.)
- [x] Steady-state RAM measured **before and after** via `/deploy` at idle. See Result below.
- [x] Result (before/after MB) noted on completion. See Result below.

## Notes
- **Constraint (shared across the RAM-optimization set):** must not break instant Ctrl+Win dictation. The Parakeet INT8 recognizer (~640 MB) must remain resident; none of these tasks may unload it or defer its load past app startup.
- This is the recommended **first** lever to land and measure — it's a config one-liner and likely the largest single win, so isolating it gives the cleanest attribution before the other tasks stack on top.
- Sibling tasks in this RAM-optimization effort: working-set trimming (`infrastructure-w7k9p`), startup GC + LOH compaction (`infrastructure-g3n5t`), ASR thread reduction (`main-t6r2k`).
- Context: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md` (the ~640 MB floor is the resident Parakeet model; the gap to 1.4 GB is .NET/ONNX overhead).
- Decision recorded: `.agentheim/knowledge/decisions/0002-workstation-gc-for-idle-tray-app.md`.

## Result (idle RAM, before/after)
Self-contained Release build via `scripts/publish.ps1`, read ~4 min after launch with no transcription running (model resident, readings stable):

| Config                 | WorkingSet64 | PrivateMemorySize64 |
|------------------------|--------------|---------------------|
| Server GC (before)     | 837 MB       | 823 MB              |
| Workstation GC (after) | 835 MB       | 776 MB              |
| **Delta**              | **-2 MB**    | **-47 MB**          |

The reduction (~47 MB private; working set essentially flat) was far below the ~200-400 MB estimate. Two findings: (1) idle footprint on this machine was already ~837 MB, well under the ~1.3-1.4 GB the premise cited — that higher figure is likely a post-transcription / grown working set, not fresh idle; (2) the bulk of resident memory is the ~640 MB Parakeet model + ONNX runtime, which GC mode does not touch, so Server GC's extra idle retention on this workload was modest. The change is still correct (right GC mode for an idle desktop app, no downside) and gives a clean attribution baseline for the sibling RAM-optimization tasks.

## Outcome
Switched `WhisperHeim.csproj` from Server GC (DATAS-disabled) to Workstation GC + concurrent collection. App builds and launches cleanly; Parakeet model stays resident. Measured a ~47 MB private-memory reduction at idle (working set flat). Decision and measurement captured in ADR 0002. Key files: `src/WhisperHeim/WhisperHeim.csproj`, `.agentheim/knowledge/decisions/0002-workstation-gc-for-idle-tray-app.md`.
