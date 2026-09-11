---
id: main-rc541
title: Overlay shows a brief "Nothing recognized" state when a real dictation decodes to nothing, instead of silently hiding — so the user knows to speak again rather than hunting for text that never arrived
status: doing
type: feature
context: main
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [dictation, overlay, ux]
related_adrs: []
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

- [ ] Orchestrator unit test: fake `ITranscriptionService` returning `""` for a
      recording above `MinSamples` → `NothingRecognized` raised exactly once, nothing
      typed, `_lastNormalDictation` unchanged.
- [ ] Orchestrator unit test: transcript non-empty but `FillerRemovalService.Clean`
      yields empty → `NothingRecognized` raised once.
- [ ] Orchestrator unit test: recording below `MinSamples` → event not raised.
- [ ] Orchestrator unit test: template mode with empty transcript → `NothingRecognized`
      raised, `TemplateNoMatch` not raised.
- [ ] Overlay state machine: `NothingRecognized` transition is logged
      (`[DictationOverlay] State: … -> NothingRecognized`), auto-reverts and hides
      after ~1.5 s, and is replaced by `Error` if `PipelineError` fires meanwhile.
- [ ] The pill is legible as "nothing came out of that" at a glance, without reading
      the log. [human-eye]
- [ ] Existing overlay behaviours are untouched: successful dictation still fades on
      release with no extra flash; warming-up and error states unchanged.

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
