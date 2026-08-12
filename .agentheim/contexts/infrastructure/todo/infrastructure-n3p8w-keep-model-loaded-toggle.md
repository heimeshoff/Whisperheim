---
id: infrastructure-n3p8w
title: Keep the transcription model loaded — user-toggleable idle-unload (tray + settings)
status: todo
type: feature
context: infrastructure
created: 2026-08-12
completed:
depends_on: []
blocks: []
tags: [model-lifecycle, memory, tray, settings]
related_adrs: [0004, 0005, 0006]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: [infrastructure-d2v7n, infrastructure-q4t8m, infrastructure-k9m3p]
---

## Why

The recognizer currently unloads after 5 minutes of no dictation activity
(`ModelLifecycleManager`, hardcoded `TimeSpan.FromMinutes(5)` in `App.xaml.cs:355`).
That reclaims ~680 MB of committed memory (ADR-0005) but costs a fixed ~4 s session
rebuild on the next use — and the rebuild only hides behind speaking time for
utterances longer than ~4 s. On a machine with RAM to spare, the trade is simply
not worth it: the user would rather hold ~700 MB permanently and never wait.

The trade-off is a **per-machine judgment**, not a global one, and today it is not
the user's to make. ADR-0006 explicitly deferred the toggle
(*"the lazy-vs-eager toggle is deferred to `infrastructure-b3n6p`; this task
hardcodes lazy-on with a 5-min constant"*), and `infrastructure-b3n6p` was
subsequently dismissed on 2026-06-28 — leaving the hardcoded constant as the only
behaviour. This task closes that gap with a deliberately narrower scope than
b3n6p had: **one boolean, no configurable timeout**.

## What

A single machine-local boolean — **keep the transcription model loaded** — exposed
on two surfaces (tray context menu and Settings), defaulting to today's behaviour.

- **OFF (default)** — unchanged: unload after 5 min idle, 30 s poll.
- **ON** — the idle unload never fires; the recognizer stays resident for the
  process lifetime.

Toggling is **symmetric and immediate**, on either surface: turning it ON loads the
model right away if it is unloaded; turning it OFF unloads it right away rather
than waiting out the idle timer. If the flag is already ON at launch, the model is
warmed on the existing post-startup housekeeping hook (~5 s after boot, off the UI
thread) — *not* eagerly in `StartupCore`, so ADR-0006's lazy-on-at-startup rule is
preserved for the default path and boot stays fast.

The 3-minute working-set trim (ADR-0004) is a separate, orthogonal lever and is
**untouched** by this setting — it moves cold pages to standby, it never unloads.

## Acceptance criteria

### Setting + persistence
- [ ] New machine-local `KeepModelLoaded` bool on `BootstrapConfig`
      (`%APPDATA%\WhisperHeim\bootstrap.json`), **default `false`** — existing users
      see no behaviour change on upgrade.
- [ ] Mirrored onto `AppSettings` via `SettingsService.SyncFromBootstrap` /
      `SyncToBootstrap`, following the `Dictation.AudioDevice` / `Overlay` precedent,
      so it never travels through a cloud-synced data path.
- [ ] Toggling from either surface persists immediately (`_settingsService.Save()`).

### Lifecycle behaviour
- [ ] `ModelLifecycleManager.PollOnce()` returns `false` (no unload) whenever
      keep-loaded is on, regardless of elapsed idle.
- [ ] The manager learns this via an **injected `Func<bool>` predicate**, not by
      reading `SettingsService` directly — it must stay UI-free, settings-free and
      clock-injectable so the existing unit tests keep working without the real model.
- [ ] Toggle ON while `Unloaded` → `BeginLoad()` fires immediately (fire-and-forget,
      background). While `Loading` or `Loaded` → no-op.
- [ ] Toggle OFF while `Loaded` and idle → unload immediately, running the same
      unload action as the idle path (`TranscriptionService.Unload()` +
      `GC.Collect()`/`WaitForPendingFinalizers()`/`Collect()` + `WorkingSetTrimmer.Trim()`).
- [ ] Toggle OFF while a dictation is in flight (`_busyCount > 0`) must **not**
      unload mid-decode — defer to the next normal idle poll. The busy guard and the
      shared decode/load/unload lock (ADR-0006) stay the correctness mechanism.
- [ ] Flag ON at launch → model warmed from the post-startup housekeeping hook
      (after the LOH compaction + trim), off the UI thread. No load in `StartupCore`.
- [ ] The 3-min `IdleWorkingSetTrimmer` behaviour is unchanged in both states.

### Tray surface
- [ ] New `Wpf.Ui.Controls.MenuItem` in `TrayIconHost` positioned **directly below
      "Start Call Recording"**, above the first `Separator`.
- [ ] Action-verb label that toggles, mirroring the `_callRecordingMenuItem` idiom:
      `"Keep Model Loaded"` when currently auto-unloading → `"Unload Model When Idle"`
      when currently kept loaded.
- [ ] Held as a field; every `Header` reassignment marshalled through
      `Application.Current?.Dispatcher?.BeginInvoke(...)`, exactly as the call-recording
      item does.
- [ ] Any event subscriptions added are unsubscribed in `TrayIconHost.Dispose()`.

### Settings surface
- [ ] `ui:ToggleSwitch` card on `GeneralPage`, using the established card idiom
      (title + description + right-docked toggle). GeneralPage rather than
      DictationPage because the recognizer is shared across *all* consumers —
      dictation, the loopback HTTP API, file/stream/call transcription (ADR-0006) —
      so this is app-level runtime behaviour, not a dictation setting.
- [ ] Description text names the trade-off honestly (roughly: *"Uses ~700 MB of RAM
      while idle, but avoids the ~4 s model reload after a pause."*).

### Both surfaces stay in sync
- [ ] Flipping the toggle in the tray updates the Settings page when it is open
      (via `SettingsService.SettingsChanged` → `RefreshFromSettings()`).
- [ ] Flipping it in Settings updates the tray menu item's `Header`.
- [ ] A settings change arriving from disk (the cross-machine file watcher) does not
      desync either surface, and does not retrigger a save loop.

### Tests
- [ ] `ModelLifecycleManagerTests` extended with the fake clock: keep-loaded
      suppresses unload well past the idle threshold; flipping it off while
      loaded-and-idle unloads; the `_busyCount` guard still wins over an
      immediate off-toggle.
- [ ] Full suite green.

## Notes

**Prior art — this is a narrowed revival of a dismissed task.**
`infrastructure-b3n6p` ("Make lazy-load / idle-unload configurable — lazy-vs-eager
+ idle timeout") was split out of `infrastructure-d2v7n` and dismissed on
2026-06-28 (bare protocol entry, no reason recorded). ADR-0006 still cites it as
the deferred toggle. This task is deliberately **smaller** than b3n6p was — no
configurable idle timeout, no lazy-vs-eager exposure, just one boolean — and
deliberately **wider** in one respect: b3n6p had no tray affordance.

**Implementation hooks (verified against the tree, 2026-08-12):**

| Concern | Location |
|---|---|
| Residency state machine | `src/WhisperHeim/Services/Transcription/ModelLifecycleManager.cs` — `PollOnce()`, `BeginLoad()`, `EnterDictation`/`ExitDictation` busy guard |
| Wiring + the 5-min constant | `src/WhisperHeim/App.xaml.cs:345-355` (construction), `:501` (30 s poll start), `:580-581` (activity re-arm) |
| Post-startup housekeeping hook | `App.xaml.cs:491-500` — `StartupMemoryCompactor.ScheduleAsync` + `postCompactionStep`; the seam to warm from |
| Load / unload / self-healing decode | `src/WhisperHeim/Services/Transcription/TranscriptionService.cs` — `EnsureLoadedAsync`, `Unload()` (non-terminal), `LoadModelLocked()` under the decode lock |
| Tray menu construction | `src/WhisperHeim/Services/Tray/TrayIconHost.cs:128-157`; toggling-label precedent at `:225`, `:236`, `:256` |
| Machine-local settings | `src/WhisperHeim/Models/BootstrapConfig.cs`; mirroring in `Services/Settings/SettingsService.cs` `SyncFromBootstrap`/`SyncToBootstrap` |
| Settings UI card idiom | `src/WhisperHeim/Views/Pages/GeneralPage.xaml` (Start-minimized / Launch-at-startup cards), `.xaml.cs` `OnSettingChanged` + `RefreshFromSettings()` |

**Measured baseline (ADR-0005, spike `infrastructure-k9m3p`):** load adds ~699 MB
private bytes; `Dispose()` + GC returns ~679 MB. Reload is a fixed ~4 s and is
session-init-bound, not I/O-bound — so keeping the *session* alive is the only way
to avoid it. That is precisely what this toggle buys.

**Re-validation caveat carried over from ADR-0005:** the "Dispose returns committed
memory" result is tied to the current INT8 model + `org.k2fsa.sherpa.onnx` / ORT
version. If the model is swapped, re-run the k9m3p harness before trusting the
OFF path's reclaim.

**Possible ADR:** if the implementation lands a durable rule — e.g. that the
keep-loaded flag is machine-local by policy, or how it composes with ADR-0004's
trim and ADR-0006's lazy-on — that is worth a short ADR amending the ADR-0004/0005/0006
family rather than leaving it implicit in code.
