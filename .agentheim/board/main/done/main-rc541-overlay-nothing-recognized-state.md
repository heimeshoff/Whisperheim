---
id: main-rc541
title: Overlay shows a brief "Nothing recognized" state when a real dictation decodes to nothing, instead of silently hiding — so the user knows to speak again rather than hunting for text that never arrived
status: done
type: feature
context: main
created: 2026-09-11
completed: 2026-09-11
depends_on: []
blocks: []
tags: [dictation, overlay, ux]
related_adrs: [0011]
related_research: []
prior_art: [main-p3k9d, main-t9w2k]
---

## Why

Today an empty decode is indistinguishable from success from the user's chair: the
pill fades out on key release exactly as it does when text is about to appear, and
then nothing appears. The user waits, checks the cursor, tries "Repeat" (which
replays the previous dictation — see `infrastructure-anvty`), and only then
concludes the message is gone. Ten seconds of confusion per lost dictation, plus
the temptation to re-speak into a bug they can't see.

The overlay already knows how to hold a state past key release (the "warming up"
state from `infrastructure-q4t8m`, and `Error` for `PipelineError`). "Nothing
recognized" is the missing third post-release outcome.

## What

- `DictationOrchestrator` raises a new event (e.g. `NothingRecognized`, payload:
  audio duration) when a recording that passed the `MinSamples` gate decodes to an
  empty raw transcript, **or** when the clean pipeline reduces the transcript to
  empty. Raised on the background thread like the other events; subscribers marshal.
  Not raised for recordings below `MinSamples` (those are already "too short,
  skipping").
- `DictationOverlayWindow` gets a state `OverlayMicState.NothingRecognized`: neutral
  grey pill with a short label ("Nothing recognized") or a clear glyph, flat bars.
  The overlay re-shows (or defers its hide, as the warming-up path does) for about
  1.5 s, then hides. Precedence stays `Error > NothingRecognized`; a `PipelineError`
  arriving in the window replaces it.
- The wiring lives where `WarmingUpChanged` and `PipelineError` are wired
  (`App.xaml.cs` / overlay host), on the UI dispatcher.
- Template mode: raise the same event — an empty transcript in template mode is
  equally "nothing recognized" (distinct from `TemplateNoMatch`, which has text).
- No sound, no toast, no tray balloon — the pill is the only surface.

## Acceptance criteria

- [x] Orchestrator unit test: fake `ITranscriptionService` returning `""` for a
      recording above `MinSamples` → `NothingRecognized` raised exactly once, nothing
      typed, `_lastNormalDictation` unchanged.
- [x] Orchestrator unit test: transcript non-empty but `FillerRemovalService.Clean`
      yields empty → `NothingRecognized` raised once.
- [x] Orchestrator unit test: recording below `MinSamples` → event not raised.
- [x] Orchestrator unit test: template mode with empty transcript → `NothingRecognized`
      raised, `TemplateNoMatch` not raised.
- [x] Overlay state machine: `NothingRecognized` transition is logged
      (`[DictationOverlay] State: … -> NothingRecognized`), auto-reverts and hides
      after ~1.5 s, and is replaced by `Error` if `PipelineError` fires meanwhile.
- [ ] The pill is legible as "nothing came out of that" at a glance, without reading
      the log. [human-eye] — pending the user's manual pass, see Outcome.
- [x] Existing overlay behaviours are untouched: successful dictation still fades on
      release with no extra flash; warming-up and error states unchanged.

## Outcome

`DictationOrchestrator.NothingRecognized` (payload: `TimeSpan` audio duration) is
derived from the existing `EmptyResult` event (task main-ma9j8) via a second
internal subscription wired in the constructor, rather than a new branch in
`TranscribeFinalAsync` (per ADR-0011). `EmptyResult` itself was widened (ADR-0011
amendment, dated 2026-09-11) from "raised on empty raw transcript" to "raised on
empty outcome" — it now also fires when a non-empty raw transcript is reduced to
empty by `FillerRemovalService.Clean`, via a new shared `RaiseEmptyResult` helper
called from both branch points in `TranscribeFinalAsync`. A new `ExceedsMinSamples`
static predicate was extracted from `StopRecording`'s inline gate (mirroring
`ShouldWarmUpOnRelease`'s seam) so the "below `MinSamples` never raises the event"
guarantee is directly unit-testable.

On the overlay side, `OverlayMicState.NothingRecognized` is a new state: same grey
palette as `NoMic`/`Idle` (no new colour), a "Nothing recognized" text label shown
only in this state, and flat static bars (falls into the existing NoMic/Error
branch of the bar-animation tick). The show/auto-revert/Error-preemption sequencing
is pulled out of the WPF window into a new, plain, fully unit-testable
`NothingRecognizedOverlayCoordinator` (mirrors the `ComputeBottomCenter` /
`ShouldWarmUpOnRelease` seam convention, since the project has no WPF UI-test
infrastructure): `Show()` re-shows the pill and arms a ~1.5 s revert; `Cancel()`
(called from `OnPipelineError` and from the fresh-dictation-start branch of
`OnDictationStateChanged` in `App.xaml.cs`) preempts a pending hold so an Error or
a brand-new recording can never be hidden out from under the user by a stale
revert timer. The real `DispatcherTimer` plumbing and the "Nothing recognized"
transition's log line (already emitted generically by `SetMicState` for every
state, unchanged here) are wired in `App.xaml.cs` and are WPF/integration
concerns.

**[human-eye] criterion:** the visual "legible at a glance" pass is a manual check
the project has no automated seam for (repo has no WPF UI-test infrastructure,
consistent with `main-p3k9d`/`main-t9w2k`'s prior art) — left for the user's own
`/deploy` pass. All other acceptance criteria are covered by the 11 new unit
tests below.

Key files:
- `src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs` — `NothingRecognized` event, `RaiseEmptyResult` helper, `ExceedsMinSamples` seam.
- `src/WhisperHeim/Views/NothingRecognizedOverlayCoordinator.cs` — new, testable hold/revert/cancel sequencing.
- `src/WhisperHeim/Views/OverlayMicState.cs` — new `NothingRecognized` state.
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml` / `.xaml.cs` — label + `SetMicState` case.
- `src/WhisperHeim/App.xaml.cs` — wiring (`OnNothingRecognized`, coordinator construction, `Cancel()` calls).
- `tests/WhisperHeim.Tests/DictationOrchestratorNothingRecognizedTests.cs`, `tests/WhisperHeim.Tests/DictationOverlayNothingRecognizedTests.cs` — new tests.
- `.agentheim/knowledge/decisions/0011-empty-dictation-result-event-hook.md` — amended in place.

## Notes

- Shared seam with `main-ma9j8` (diagnostics dump): both react to the same
  "empty result for a real recording" moment in `TranscribeFinalAsync`. Whichever
  lands first introduces the single explicit hook; the other subscribes to it.
- Reuse the deferred-hide mechanics of the warming-up state
  (`infrastructure-q4t8m`, ADR-0005/0006 context) rather than adding a second timer
  scheme.
- No `design-system/` BC or styleguide task exists in this project; the overlay's
  existing colours (grey for NoMic, amber for WarmingUp, red for Error) are the
  palette — pick grey with a text label, don't introduce a new colour.
