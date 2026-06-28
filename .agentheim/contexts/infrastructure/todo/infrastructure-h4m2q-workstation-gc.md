---
id: infrastructure-h4m2q
title: Switch Server GC → Workstation GC + concurrent
status: todo
type: refactor
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
WhisperHeim sits at a steady ~1.3–1.4 GB resident footprint. A codebase investigation found the single biggest, lowest-risk lever is the .NET GC mode. `src/WhisperHeim/WhisperHeim.csproj:12` sets `<ServerGarbageCollection>true</ServerGarbageCollection>`, and line 13 sets `<GarbageCollectionAdaptationMode>0</GarbageCollectionAdaptationMode>`, which **disables DATAS** — the .NET 9 feature that would otherwise let Server GC shrink its heaps under low load. Server GC allocates a heap per CPU core and holds memory aggressively; it's the wrong mode for a mostly-idle desktop tray app. This is the most memory-hungry GC config available, for no benefit on this workload.

## What
Switch the app to Workstation GC with concurrent collection. In `WhisperHeim.csproj`:
- Set `<ServerGarbageCollection>false</ServerGarbageCollection>`.
- Add `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`.
- Remove (or revisit) the `<GarbageCollectionAdaptationMode>0</GarbageCollectionAdaptationMode>` line — DATAS is a Server-GC concern; once on Workstation GC it's moot, and leaving it set to 0 only serves to disable a memory-saving feature. (If for any reason Server GC is kept instead, flip this to `1` to enable DATAS.)

## Acceptance criteria
- [ ] `WhisperHeim.csproj` builds and the app launches normally with Workstation + concurrent GC.
- [ ] No regression in instant Ctrl+Win dictation responsiveness (Parakeet ~640 MB stays resident; this change does not touch model loading).
- [ ] Steady-state RAM measured **before and after** via `/deploy`: record the resident footprint at idle (a few minutes after launch, no transcription running) for both configs. Expected reduction ~200–400 MB (estimate — the measurement is the deliverable).
- [ ] Result (before/after MB) noted in the task on completion.

## Notes
- **Constraint (shared across the RAM-optimization set):** must not break instant Ctrl+Win dictation. The Parakeet INT8 recognizer (~640 MB) must remain resident; none of these tasks may unload it or defer its load past app startup.
- This is the recommended **first** lever to land and measure — it's a config one-liner and likely the largest single win, so isolating it gives the cleanest attribution before the other tasks stack on top.
- Sibling tasks in this RAM-optimization effort: working-set trimming (`infrastructure-w7k9p`), startup GC + LOH compaction (`infrastructure-g3n5t`), ASR thread reduction (`main-t6r2k`).
- Context: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md` (the ~640 MB floor is the resident Parakeet model; the gap to 1.4 GB is .NET/ONNX overhead).
