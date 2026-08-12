---
id: 0010
title: Keep-model-loaded is a user-facing on/off gate on the existing idle-unload, not a new lifecycle mode
scope: infrastructure
status: accepted
date: 2026-08-12
supersedes: []
superseded_by: []
related_tasks: [infrastructure-n3p8w]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
---

# ADR 0010: Keep-model-loaded is a user-facing on/off gate on the existing idle-unload, not a new lifecycle mode

## Context

ADR-0006 shipped lazy-on + a hardcoded 5-min idle-unload, and explicitly deferred
"the lazy-vs-eager toggle" to `infrastructure-b3n6p`. That follow-up was dismissed
on 2026-06-28 without a recorded reason, leaving the hardcoded fuse as the only
behaviour. On a machine with RAM to spare, the ~4 s reload after every idle period
is a cost the user might not want to pay, and that is a per-machine judgment call
the app was not previously giving the user a way to make.

Task `infrastructure-n3p8w` picked this back up, deliberately narrower than
b3n6p: **one boolean**, no configurable idle timeout, no lazy-vs-eager exposure —
just whether the existing idle-unload is allowed to fire at all.

## Decision

- **A single machine-local boolean** (`BootstrapConfig.KeepModelLoaded` /
  `AppSettings.General.KeepModelLoaded`, default `false`), mirrored through
  `SettingsService.SyncFromBootstrap`/`SyncToBootstrap` exactly like
  `Dictation.AudioDevice` and `Overlay` — so it is a per-machine RAM/latency
  preference that never rides the cloud-synced `settings.json` path, and a
  disk-driven reload from another machine can never desync it.

- **`ModelLifecycleManager` learns the flag through an injected `Func<bool>
  keepLoaded` predicate**, not a `SettingsService` reference. `PollOnce()`
  checks it alongside the busy guard and idle threshold — when it returns
  `true`, `PollOnce()` is a no-op regardless of elapsed idle. This keeps the
  state machine settings-free and deterministically testable with a fake clock
  and a mutable local bool, the same shape the existing tests already use.

- **A new `UnloadNow()` bypasses the idle threshold and the keep-loaded flag,
  but not the busy guard.** Turning the toggle OFF should reclaim memory
  immediately rather than waiting out the idle timer — but if a dictation is
  in flight, `UnloadNow()` returns `false` and the unload is silently deferred
  to the next normal `PollOnce()` once the dictation ends. The shared
  decode/load/unload lock inside `TranscriptionService` (ADR-0006) remains the
  actual correctness mechanism; the busy guard here is a latency optimisation
  that also happens to make "toggle OFF mid-dictation" safe without any new
  synchronization.

- **Toggling is symmetric and immediate**, funnelled through one method,
  `App.ToggleKeepModelLoaded(bool)`, called by both the tray menu item and the
  `GeneralPage` toggle: persist via `Save()`, then ON → `BeginLoad()` (already
  idempotent), OFF → `UnloadNow()`.

- **Startup stays lazy-on.** If the flag is already ON at launch, the model is
  warmed from the existing post-startup housekeeping hook (after LOH compact +
  working-set trim, off the UI thread, ~5 s post-boot) rather than eagerly in
  `StartupCore`. This preserves ADR-0006's lazy-on-at-startup rule for the
  default (OFF) path and keeps boot fast regardless of the setting — the
  trade-off this ADR makes is "warm a few seconds after boot", never "block
  boot on model load".

- **`GeneralPage`, not `DictationPage`.** The recognizer is shared across every
  consumer (Ctrl+Win dictation, the loopback HTTP API, file/stream/call
  transcription — ADR-0006), so this is app-level runtime behaviour, not a
  dictation-specific setting.

## Composition with the other RAM levers

- **Unaffected: the 3-min working-set trim (ADR-0004).** It only moves cold
  pages to standby; it never unloads. Keep-loaded has no bearing on it, in
  either state.
- **Gated: the 5-min idle-unload (ADR-0005/0006).** Keep-loaded ON suppresses
  it entirely; OFF restores today's behaviour unchanged.

## Consequences

- Users on RAM-constrained machines keep today's default behaviour with zero
  change.
- Users who opt in trade ~700 MB of permanently-resident memory for never
  paying the ~4 s reload after a pause.
- The idle timeout itself (5 min) is still not configurable — that gap is
  still open if a future task wants it; this ADR only settles the on/off
  question that ADR-0006 deferred.

## References
- Code: `src/WhisperHeim/Services/Transcription/ModelLifecycleManager.cs` (`PollOnce`, `UnloadNow`), `src/WhisperHeim/App.xaml.cs` (`ToggleKeepModelLoaded`, housekeeping-hook warm), `src/WhisperHeim/Services/Tray/TrayIconHost.cs`, `src/WhisperHeim/Views/Pages/GeneralPage.xaml(.cs)`, `src/WhisperHeim/Models/BootstrapConfig.cs`, `src/WhisperHeim/Models/AppSettings.cs`, `src/WhisperHeim/Services/Settings/SettingsService.cs`
- Tests: `tests/WhisperHeim.Tests/ModelLifecycleManagerTests.cs`
- Task: `infrastructure-n3p8w`
- Supersedes the deferred/dismissed `infrastructure-b3n6p` for the on/off half of that task; the configurable-idle-timeout half remains open.
