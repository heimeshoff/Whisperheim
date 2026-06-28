---
id: 0004
title: Working-set trim after model load and on idle (the "trim" half of compact-then-trim)
scope: infrastructure
status: accepted
date: 2026-06-28
supersedes: []
superseded_by: []
related_tasks: [infrastructure-w7k9p, infrastructure-g3n5t, infrastructure-h4m2q]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
---

# ADR 0004: Working-set trim after model load and on idle

## Context
WhisperHeim's reported resident set (~1.3–1.4 GB historically) includes pages
that are committed but not actively touched. Windows lets a process voluntarily
release its working set so cold pages move to the standby list, dropping the
number Task Manager reports — without unloading anything. The dominant resident
cost is the INT8 Parakeet recognizer (~640 MB, mostly native ONNX memory) plus
WPF; a managed GC compaction (ADR-0003) could not move that, and on its own
showed no measurable idle win. A working-set trim attacks the *reported* footprint
directly. ADR-0003 built the shared post-startup housekeeping hook explicitly as
the "compact, then trim" precursor; this ADR is the "trim" half plus an idle
trigger.

## Decision
Add two small services under `Services/Startup/`, mirroring the g3n5t style
(failure-isolated, focused tests):

- **`WorkingSetTrimmer`** — P/Invokes `EmptyWorkingSet(hProcess)` from `psapi.dll`
  on the current process. Windows-only (guarded by `OperatingSystem.IsWindows()`),
  fully failure-isolated: a `false` return or any exception is logged via `Trace`
  and swallowed (`Trim()` returns `bool`, never throws). Chosen over
  `SetProcessWorkingSetSize(handle, -1, -1)` because `EmptyWorkingSet` is the
  single-purpose, intent-revealing call for "release the whole working set"; both
  move pages to standby identically.

- **`IdleWorkingSetTrimmer`** — decouples the *idle policy* from wall-clock time
  via an injectable clock so it is deterministically unit-testable. A 30 s poll
  timer checks elapsed idle; when the **3-minute** idle threshold is crossed it
  trims **exactly once per idle period** (`ShouldTrim()` latches until the next
  activity). `NotifyActivity()` resets the clock and re-arms.

Wiring in `App.xaml.cs`:
- **Post-load trim** is appended onto the existing delayed housekeeping task by
  passing a `postCompactionStep` action to `StartupMemoryCompactor.ScheduleAsync`
  — guaranteeing "compact, then trim" ordering on one background task, after a 5 s
  delay, off the UI thread. Compacting first means the trim does not immediately
  re-fault compactable garbage back in.
- **Activity signal** is `OnDictationStateChanged` (fires on every dictation
  start/stop) → `NotifyActivity()`. So "idle" = no dictation for 3 min; a trim can
  never fire mid-dictation and the working set stays warm while in use.

## Choices and their rationale
- **Idle threshold 3 min / poll 30 s.** Within the task's suggested 2–5 min band.
  Long enough that brief pauses between dictations don't trigger a cold-cache
  re-fault on the next press; short enough to reclaim during real idle. The 30 s
  poll keeps the timer cost negligible.
- **Trim once per idle period, not repeatedly.** Continued idle touches nothing
  new, so re-trimming would only burn CPU; the next activity re-arms.
- **Idle trim is NOT gated by `WHISPERHEIM_DISABLE_STARTUP_GC`.** That switch is a
  *startup* A/B lever (ADR-0003). The idle trim is a steady-state runtime behavior
  and stays on independently. The startup *trim* (post-load) does honor the switch
  since it rides the same disabled-able startup hook.

## Measured outcome
The trim lever itself, measured against the exact `EmptyWorkingSet` call on a
process holding ~480 MB of committed-then-cold pages:

| Metric              | Before  | After  | Delta     |
|---------------------|---------|--------|-----------|
| WorkingSet64        | 489 MB  | 10 MB  | **−479 MB** |
| PrivateMemorySize64 | 481 MB  | 481 MB | 0 MB      |

Exactly the expected shape: reported RSS collapses as pages move to standby,
committed (private) memory is unchanged — the trim does not free memory the way
unloading would, and pages re-fault on next touch. In the live app the realized
WorkingSet64 drop is bounded by how much of the ~640 MB Parakeet floor is cold at
trim time; the resident model is never unloaded.

**Honesty note:** the full real-app idle A/B via `/deploy` was *not* hand-run for
this task. A user instance was already running with no single-instance guard, and
launching a competing instance would have installed a second low-level keyboard
hook that could double-trigger the user's live dictation. The trim mechanism is
proven by the harness above and by unit tests; first-press perceived latency after
an idle trim was not hand-measured and remains the user's call during normal use
(the model staying resident means the re-fault is cold-cache, not a reload).

## Consequences
### Positive
- Lower reported footprint after load and during idle, with no model unload.
- Idle policy is pure and clock-injectable → deterministic tests, no timing flake.
- Drops cleanly onto the g3n5t hook; App startup flow barely changed.

### Negative / Neutral
- First dictation after a long idle faults cold pages back in (slightly slower);
  this is the intended trade-off and expected to be imperceptible.
- Private/committed memory is unchanged — this lever only moves reported RSS.

## Alternatives considered
- **`SetProcessWorkingSetSize(h, -1, -1)`** instead of `EmptyWorkingSet`:
  equivalent effect; rejected for being less intent-revealing.
- **Trim on a fixed recurring timer regardless of activity:** rejected — trimming
  mid-use forces needless re-faults; gating on idle is strictly better.
- **Skip the idle trim, post-load only:** rejected — long-running tray sessions
  accumulate cold pages well after startup; idle is where the win persists.

## References
- Task: `.agentheim/contexts/infrastructure/done/infrastructure-w7k9p-trim-working-set.md`
- Code: `src/WhisperHeim/Services/Startup/WorkingSetTrimmer.cs`,
  `src/WhisperHeim/Services/Startup/IdleWorkingSetTrimmer.cs`,
  `src/WhisperHeim/Services/Startup/StartupMemoryCompactor.cs`,
  `src/WhisperHeim/App.xaml.cs`
- Tests: `tests/WhisperHeim.Tests/WorkingSetTrimmerTests.cs`,
  `tests/WhisperHeim.Tests/IdleWorkingSetTrimmerTests.cs`,
  `tests/WhisperHeim.Tests/StartupMemoryCompactorTests.cs`
- Precursor: ADR-0003 (compact, then trim); GC policy: ADR-0002
- Research: `.agentheim/knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`
