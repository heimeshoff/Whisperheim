---
id: infrastructure-q4t8m
title: "Warming up" overlay state when an utterance outruns the model load
status: done
type: feature
context: infrastructure
created: 2026-06-28
completed: 2026-06-28
depends_on: [infrastructure-d2v7n]
blocks: []
tags: [overlay, ui, asr, parakeet, lifecycle, dictation]
related_adrs: [0005, 0006]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: [main-012, main-025]
---

## Why
With lazy-load + idle-unload ([[infrastructure-d2v7n]], shipped), a short utterance right after an idle unload arrives at release *before* the ~4 s background reload has finished, so transcribe-on-release has to `await` the remaining load (`DictationOrchestrator.TranscribeFinalAsync` → `EnsureLoadedAsync`). Without feedback the dictation overlay just sits there — and worse, on the current release path it has already been told to **fade out** by the time that wait happens, so the user sees nothing while the app blocks. The result reads as **frozen / broken** rather than "loading". A brief "warming up" state makes the bounded wait legible so the worst-case short-utterance path feels intentional, not hung.

## What
Add a "warming up" visual state to the existing dictation overlay (`main-012` / `OverlayMicState`), shown only when transcribe-on-release is awaiting an in-flight model load, and keep the overlay alive through that wait.

- **Reuse the existing overlay** — add a state, don't build a new surface. Extend `OverlayMicState` (`Views/OverlayMicState.cs`) with a `WarmingUp` member and render it in `DictationOverlayWindow.SetMicState` + `OnBarAnimationTick`.
- **Visual treatment (decided): pulsing amber bars.** All 12 bars breathe up/down *in sync* on a ~1 s cycle in a distinct amber/yellow, ignoring RMS — clearly "busy", and unmistakably different from the orange RMS-driven `Speaking` bars and the flat grey `Idle` bars. (Add a `WarmingUp` branch to `OnBarAnimationTick`; the existing branch only animates `Speaking`/`Idle`.)
- **Drive it from the lifecycle state** ([[infrastructure-d2v7n]]): at release, decide off `ModelLifecycleManager.State` — if it is not yet `ModelResidencyState.Loaded`, show `WarmingUp`; clear it the moment `EnsureLoadedAsync` returns and decode begins.
- **Keep the overlay alive while warming.** The release path (`DictationOrchestrator.StopRecording`) currently calls `NotifyStateChanged(false)` → `App.OnDictationStateChanged(false)` → `_overlayWindow.HideOverlay()` *before* `TranscribeFinalAsync` runs, and `SetMicState` no-ops once `_isVisible` is false. So the warming-up state has to **defer/suppress that hide** while the model is still `Loading`, and hide only after the load completes and decode runs.
- **No flash for normal utterances.** When `State == Loaded` at release (the common case — load finished during speaking), behave exactly as today: fade out immediately, never enter `WarmingUp`.

### Pinned hooks (all exist in the shipped d2v7n code)
- **Signal source:** `ModelLifecycleManager.State` → `ModelResidencyState { Unloaded, Loading, Loaded }` (public getter, `Services/Transcription/ModelLifecycleManager.cs`). The await point is `DictationOrchestrator.TranscribeFinalAsync` (`Services/Orchestration/DictationOrchestrator.cs:248-254`): `EnterDictation()` → `await _modelLifecycle.EnsureLoadedAsync()` → `TranscribeAsync`. The warming-up window is exactly: `State != Loaded` checked *before* that await; cleared when it returns.
- **New enum state:** `OverlayMicState.WarmingUp` in `Views/OverlayMicState.cs`; new `case` in `DictationOverlayWindow.SetMicState` (`Views/DictationOverlayWindow.xaml.cs:291`) + new branch in `OnBarAnimationTick` (`:178`).
- **Plumbing:** the orchestrator does not know about the overlay — overlay state is driven from `App.xaml.cs` via orchestrator events (`AudioAmplitudeChanged` → `OnAudioAmplitudeChanged:554`, `PipelineError` → `OnPipelineError:564`). Mirror that pattern: add an orchestrator event (e.g. `WarmingUpChanged(bool)`) raised around the `EnsureLoadedAsync` await; `App` subscribes and calls `_overlayWindow.SetMicState(OverlayMicState.WarmingUp)` / restores. Marshal to the UI thread via the Dispatcher as the existing handlers do.
- **Hide suppression seam:** `App.OnDictationStateChanged` (`App.xaml.cs:533-536`) — the `isActive == false` branch that calls `HideOverlay()` is what must be deferred while warming. Simplest correct shape: when warming, do not hide on the state-change; hide when `WarmingUpChanged(false)` fires (after decode). Keep `Error` precedence intact — a pipeline error during warm-up still wins (`OnPipelineError`).

## Acceptance criteria
- [ ] When release occurs while `ModelLifecycleManager.State != Loaded`, the overlay shows the `WarmingUp` state — **pulsing amber bars** — and stays visible (does not fade out) until the load completes.
- [ ] The `WarmingUp` state is visually distinct from `Idle` (flat grey), `Speaking` (orange RMS-driven), `NoMic`, and `Error` — it reads as "working", not frozen.
- [ ] The state clears and transcription proceeds normally the moment `EnsureLoadedAsync` returns; the overlay then fades out as it does today.
- [ ] For a normal-length utterance (load already `Loaded` by release) the overlay behaves exactly as today — immediate fade-out, no warming-up flash.
- [ ] A pipeline error during warm-up still shows the `Error` state (Error precedence preserved).
- [ ] Verified via `/deploy`: force an idle unload (≥5 min idle), fire a short (<~4 s) utterance, observe the pulsing-amber warming-up state persist, then the transcription appear.

## Notes
- **Depends on [[infrastructure-d2v7n]] (done).** The `Loading`/await signal it had to expose is `ModelLifecycleManager.State` + `EnsureLoadedAsync` — both shipped and pinned above. Hook is no longer a TODO; task is ready to work.
- **Key wrinkle surfaced during refinement:** the overlay is dismissed at release *before* the load-wait, so the naive "set state during the await" does nothing (`SetMicState` no-ops when `!_isVisible`). The deferred-hide handling above is the real work — don't skip it.
- Prior art: `main-012` (dictation overlay), `main-025` (overlay mic-state visualization), `main-011` (dictation wiring).
- No `design-system/` BC exists, so the styleguide gate does not apply; this reuses the already-shipped overlay and its existing brand colors (amber ≈ the existing `#FFff8b00` family — pick a clearly distinct warm tone if reusing it alongside Speaking-orange would be ambiguous).
- Brand colors already in the overlay (`DictationOverlayWindow.xaml.cs:44-48`): blue `#FF25abfe`, orange `#FFff8b00`, grey, red. Choose the warming amber to stay distinct from Speaking-orange.

## Outcome
Shipped the `WarmingUp` overlay state and kept the overlay alive through the transcribe-on-release model-load wait.

**What was built**
- `OverlayMicState.WarmingUp` added (`Views/OverlayMicState.cs`).
- `DictationOverlayWindow` (`Views/DictationOverlayWindow.xaml.cs`): new amber bar/border colour `#FFFFC107` (deliberately yellower than Speaking-orange `#FFff8b00`), a `WarmingUp` branch in `SetMicState`, and a `WarmingUp` branch in `OnBarAnimationTick` that drives all 12 bars with one synchronized ~1 s cosine breathing pulse (RMS ignored).
- `DictationOrchestrator` (`Services/Orchestration/DictationOrchestrator.cs`): new `WarmingUpChanged(bool)` event plus the pure, unit-tested decision `internal static bool ShouldWarmUpOnRelease(ModelResidencyState?)` (= `state is not null && != Loaded`). `StopRecording` now decides warming *before* `NotifyStateChanged(false)` and raises `WarmingUpChanged(true)` ahead of the fade-out so the deferred-hide flag is set before the hide is queued (closes the race the refinement flagged). `TranscribeFinalAsync` raises `WarmingUpChanged(false)` only on the success path right after `EnsureLoadedAsync` returns; on a load failure it falls through to `PipelineError` so Error precedence holds.
- `App.xaml.cs`: subscribes to `WarmingUpChanged`; `OnDictationStateChanged(false)` defers the overlay hide while `_isWarmingUp`; `OnWarmingUpChanged` shows WarmingUp / hides on completion; `OnPipelineError` clears `_isWarmingUp` so an error wins; the `isActive==true` path clears the flag to prevent leakage across sessions.

**Tests** — 4 new xUnit tests in `tests/WhisperHeim.Tests/DictationOverlayWarmUpTests.cs` cover the release-time decision (Loading→warm, Unloaded→warm, Loaded→no flash, no-lifecycle→no warm), i.e. AC1/AC4. Full suite green: 166 passed (was 162). The pulsing-amber rendering, the deferred-hide plumbing, and the Error-precedence path are WPF/integration behaviour exercised by the code but not unit-testable here.

**Deferred to user (`/deploy`-gated, AC6):** live visual confirmation — force an idle unload (≥5 min), fire a short (<~4 s) utterance, observe the pulsing-amber WarmingUp state persist, then the transcription appear. Cannot be run from here; deferred exactly as d2v7n deferred its RAM measurements.

**Key files:** `src/WhisperHeim/Views/OverlayMicState.cs`, `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs`, `src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs`, `src/WhisperHeim/App.xaml.cs`, `tests/WhisperHeim.Tests/DictationOverlayWarmUpTests.cs`.

No new ADR: visual treatment and hooks were already decided in refinement; no new architectural decision was made.
