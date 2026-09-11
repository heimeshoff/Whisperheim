---
id: 0011
title: Single EmptyResult event hook for empty-dictation reactions (diagnostics + overlay)
scope: main
status: accepted
date: 2026-09-11
supersedes: []
superseded_by: []
related_tasks: [main-ma9j8, main-rc541]
related_research: []
---

# ADR 0011: Single `EmptyResult` event hook for empty-dictation reactions

## Context
Before this task, `DictationOrchestrator.TranscribeFinalAsync` silently dropped an
empty raw transcript: `if (string.IsNullOrEmpty(rawText)) return;` with no trace,
no audio, no signal to anything else. Two tasks landed in the same wave needing to
react to exactly this moment:

- **main-ma9j8** (this task): log a Warning/Information trace line with
  duration/levels/decode context, and dump the raw samples as a capped-ring WAV
  for offline repro.
- **main-rc541** (next wave): show a brief "Nothing recognized" overlay state.

Both only care about "a real recording (above the hold-to-talk minimum) that
decoded to nothing" — the same predicate, the same data. Implementing each as its
own inline `if (string.IsNullOrEmpty(rawText)) { ... }` branch inside
`TranscribeFinalAsync` would duplicate the predicate and the data-gathering
(duration, RMS/peak, decode ms, residency state) across two unrelated tasks, and
would make `TranscribeFinalAsync` progressively harder to read as more consumers
of "empty result" show up later (e.g. analytics, a settings-page counter).

## Decision
Introduce one explicit event, `DictationOrchestrator.EmptyResult` (payload type
`EmptyDictationResult`), raised exactly once per empty raw transcript inside
`TranscribeFinalAsync`, carrying: the raw `float[] Samples`, `AudioDuration`,
`SampleCount`, `Rms`, `Peak`, `DecodeMs`, `TemplateMode`, and the
`ModelResidencyState?` residency at decode time.

The orchestrator's own diagnostics (this task's Warning/Info trace line + WAV
dump) are implemented as a private handler (`OnEmptyResult`) subscribed to this
same public event in the constructor (`EmptyResult += OnEmptyResult`), rather
than as inline logic in `TranscribeFinalAsync`. This makes the orchestrator's own
reaction just the first subscriber, not a special case — `main-rc541` (or any
future consumer) adds a second subscriber from the outside (e.g. in
`App.xaml.cs`, mirroring how `PipelineError`/`WarmingUpChanged` are already
consumed) instead of adding a second branch inside the method.

The threshold split (Warning + dump attempt at >= 3 s of audio, Information + no
dump below it) and the dump itself are internal to `EmptyResultWarnThresholdSeconds`
and `OnEmptyResult` — `main-rc541`'s own threshold/precedence logic for the
overlay is independent and reads the same event's `AudioDuration`/other fields
however it needs to.

## Consequences
- `TranscribeFinalAsync` stays a single `EmptyResult?.Invoke(...); return;` at the
  empty-transcript branch point — no growing pile of ad-hoc `if` branches as more
  consumers are added.
- `EmptyDictationResult` carries the full raw sample buffer, which is heavier than
  strictly needed for consumers that only care about duration (e.g. `main-rc541`'s
  overlay) — accepted because the diagnostics dump subscriber genuinely needs the
  samples, and float arrays for a single hold-to-talk utterance are small (a few
  hundred KB at most).
- Both this task's dump service and any later subscriber can be unit-tested by
  invoking `TranscribeFinalAsync` directly (existing internal test seam, mirrors
  `StartCaptureForDevice`) with a fake `ITranscriptionService` returning `""`, and
  asserting on either the trace output or a test subscriber attached to
  `EmptyResult`.
- Subscriber order is significant only insofar as the orchestrator's own
  diagnostics handler is wired first (in the constructor); external subscribers
  added afterward do not affect its behavior since it does not consume/cancel the
  event.
