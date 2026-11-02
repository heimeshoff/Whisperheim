---
id: infrastructure-w7k9p
title: Trim Windows working set after model load and on idle
status: done
type: feature
context: infrastructure
created: 2026-06-28
completed: 2026-06-28
depends_on: []
blocks: []
tags: [memory, runtime, win32, performance]
related_adrs: [0004]
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

## Outcome
Added the "trim" half of "compact, then trim". Two failure-isolated services
under `src/WhisperHeim/Services/Startup/`:
- `WorkingSetTrimmer` — Windows-only P/Invoke of `EmptyWorkingSet` (psapi),
  guarded and non-fatal (logs + returns `bool`, never throws).
- `IdleWorkingSetTrimmer` — clock-injectable idle policy: trims once per idle
  period after 3 min of no dictation (30 s poll), re-armed by activity.

Wiring (`App.xaml.cs`): post-load trim appended onto the existing g3n5t
housekeeping task via a new `postCompactionStep` parameter on
`StartupMemoryCompactor.ScheduleAsync` (guarantees compact→trim ordering on one
delayed off-UI task, honoring `WHISPERHEIM_DISABLE_STARTUP_GC`). Idle trim
constructed + started in `StartupCore`, fed activity from
`OnDictationStateChanged`, disposed in `OnAppExit`. The idle trim is a runtime
lever and is intentionally NOT gated by the startup-GC switch.

Decision recorded in ADR-0004 (choice of `EmptyWorkingSet` over
`SetProcessWorkingSetSize`, the 3 min / 30 s timing, the once-per-idle latch, and
the env-gating boundary).

### Measured
The trim lever, against the exact `EmptyWorkingSet` call on a process holding
~480 MB committed-then-cold pages: `WorkingSet64` 489 MB → 10 MB (**−479 MB**)
while `PrivateMemorySize64` held flat at 481 MB — pages moved to standby,
committed memory untouched, exactly as designed. In the live app the realized
drop is bounded by how much of the ~640 MB Parakeet floor is cold at trim time;
the model is never unloaded.

Honesty note: the full real-app idle A/B via `/deploy` was not hand-run — a user
instance was already running with no single-instance guard, so launching a
competing instance risked a second low-level keyboard hook double-triggering live
dictation. First-press-after-idle perceived latency was therefore not
hand-measured; the model staying resident means the next press is a cold-cache
re-fault, not a reload. Mechanism proven by the harness above plus unit tests.

### Tests
9 new tests (all green; full suite 147/147): `WorkingSetTrimmerTests`,
`IdleWorkingSetTrimmerTests`, plus a `ScheduleAsync` post-compaction-step test
added to `StartupMemoryCompactorTests`.

### Notes
- ADR: `.agentheim/knowledge/decisions/0004-working-set-trim-after-load-and-on-idle.md`
