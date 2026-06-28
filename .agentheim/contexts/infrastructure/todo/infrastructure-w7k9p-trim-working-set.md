---
id: infrastructure-w7k9p
title: Trim Windows working set after model load and on idle
status: todo
type: feature
context: infrastructure
created: 2026-06-28
completed:
depends_on: []
blocks: []
tags: [memory, runtime, win32, performance]
related_adrs: []
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: []
---

## Why
WhisperHeim's reported resident set (~1.3–1.4 GB) includes pages that are committed but not actively touched. Windows lets a process voluntarily release its working set so cold pages move to the standby list, dropping the number Task Manager reports. Nothing in the app currently does this. After the Parakeet model finishes loading (a large transient + resident allocation) and during idle periods, trimming the working set is a low-risk way to cut the visible footprint; the hot decode pages fault back in on the next hotkey, usually imperceptibly.

## What
Add a small Win32 interop helper and call it at the right moments:
- P/Invoke `SetProcessWorkingSetSize(handle, -1, -1)` (or `EmptyWorkingSet`) from `psapi`/`kernel32`.
- Call it once **after** the Parakeet model has finished loading at startup (after `TranscriptionService.LoadModel()` completes — see `App.xaml.cs` startup flow ~line 332).
- Call it again on an **idle trigger** — define idle as "no dictation/transcription activity for N minutes" (pick a sensible N, e.g. 2–5 min; a timer reset on hotkey/transcription activity is fine).
- Keep it Windows-only and guarded; a failed P/Invoke must never crash the app (log and continue).

## Acceptance criteria
- [ ] A working-set trim runs after model load and on the defined idle trigger.
- [ ] First Ctrl+Win dictation after an idle trim still produces a transcription with no perceptible added latency beyond normal cold-cache fault-in (sanity check by hand).
- [ ] Instant Ctrl+Win dictation otherwise unaffected; Parakeet stays resident (trimming the working set is not unloading the model).
- [ ] Steady-state RAM measured **before and after** via `/deploy`: capture reported resident set at idle, before and after a trim fires.
- [ ] P/Invoke failure path is handled (logged, non-fatal).
- [ ] Result (before/after MB, observed first-press latency note) recorded on completion.

## Notes
- **Constraint (shared across the RAM-optimization set):** must not break instant Ctrl+Win dictation. The Parakeet INT8 recognizer (~640 MB) must remain resident.
- Working-set trimming reduces *reported* RSS by moving pages to standby; it does not free committed memory the way unloading would, and pages re-fault on next access. That trade-off (slightly slower first access after idle) is the whole point — acceptable as long as it stays imperceptible.
- Recommended to land **after** the GC switch (`infrastructure-h4m2q`) so the measurements don't confound each other.
- Sibling tasks: `infrastructure-h4m2q`, `infrastructure-g3n5t`, `main-t6r2k`.
- Context: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`.
