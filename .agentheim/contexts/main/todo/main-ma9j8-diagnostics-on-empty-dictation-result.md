---
id: main-ma9j8
title: Make an empty dictation result observable — warn with duration/RMS/peak and dump the raw samples as a WAV into a local diagnostics folder (capped ring), so the next lost dictation can be reproduced offline
status: todo
type: feature
context: main
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [dictation, diagnostics, logging, audio]
related_adrs: []
related_research: []
prior_art: [main-v7k2d, main-104]
---

## Why

When sherpa-onnx returned `""` for a 27 s dictation on 2026-09-11, the only trace was
the `Transcribed … : ""` info line — no level, no audio, no explicit "dropped" line.
`DictationOrchestrator.TranscribeFinalAsync` hits `if (string.IsNullOrEmpty(rawText))
return;` and nothing else happens. Diagnosing the root cause required
re-synthesizing speech and guessing at gain levels (`infrastructure-anvty`), because
the actual failing audio no longer existed anywhere.

Empty results will keep happening legitimately (muted mic, accidental press) and
occasionally illegitimately (model/runtime regressions). Each illegitimate one should
leave behind exactly what is needed to reproduce it: the samples and their levels.

## What

In the hold-to-talk path (`DictationOrchestrator.TranscribeFinalAsync`), when the raw
transcript is empty **and** the audio is at least 3 s long:

1. **Log at Warning level** one line that names the situation and carries the
   numbers: audio duration (s), sample count, RMS, peak, decode time (ms), template
   mode, and the recognizer residency state at decode time
   (`ModelLifecycleManager.State`). Below 3 s, log the same at Information level
   (short empties are usually legitimate).
2. **Dump the samples** as a 16 kHz mono 16-bit PCM WAV to
   `%LOCALAPPDATA%\WhisperHeim\diagnostics\empty-dictation-<yyyyMMdd-HHmmss>.wav`
   — the *machine-local* root (`DataPathService.LocalRoot` sibling), deliberately
   **not** the cloud-syncable data path, following `main-104`'s "stage WAV writes
   outside the synced folder" rule. Keep a ring of the **10 most recent** files;
   delete older ones on each write. The log line above includes the written path.
3. **Opt-out switch** `WHISPERHEIM_DISABLE_DIAG_DUMP=1` (environment variable,
   mirroring `WHISPERHEIM_DISABLE_STARTUP_GC`) disables the WAV write only — the
   warning line is always emitted. Default is **on**: the files are the user's own
   speech, on the user's own machine, capped at ~10 × 1 MB.
4. The write is best-effort and off the decode path's critical section: any I/O
   failure is caught and logged at Warning; it never surfaces as `PipelineError`
   and never delays or changes what is typed.
5. Also add RMS and peak to the existing per-dictation `Final:` info line, so
   successful dictations give a baseline to compare against (cheap: one pass).

## Acceptance criteria

- [ ] Empty raw transcript for audio ≥ 3 s → exactly one `Warning`-level trace line
      tagged `[DictationOrchestrator]` containing duration, sample count, RMS, peak,
      decode ms, template flag, residency state, and the dump path (or "dump disabled").
- [ ] Empty raw transcript for audio < 3 s → the same fields at Information level,
      no WAV written.
- [ ] A dumped file is a valid RIFF/WAVE, 16 kHz, mono, 16-bit, whose sample count
      equals the recorded sample count, written under
      `%LOCALAPPDATA%\WhisperHeim\diagnostics\`, never under the configurable data path.
- [ ] Ring cap: after 11 dumps only the 10 newest files remain (unit-testable via an
      injectable directory/clock).
- [ ] `WHISPERHEIM_DISABLE_DIAG_DUMP=1` suppresses the WAV write; the warning line
      still appears and states that the dump is disabled.
- [ ] An I/O failure during the dump (e.g. unwritable directory in the test) is
      logged and does not raise `PipelineError`, does not throw out of
      `TranscribeFinalAsync`, and does not change the typed result.
- [ ] The `Final:` log line for successful dictations now includes RMS and peak.
- [ ] Unit tests for the orchestrator use a fake `ITranscriptionService` returning `""`
      and assert the Warning path; the WAV writer and ring cap are tested as a pure
      service with an injected root directory.

## Notes

- Keep the WAV writer a small standalone service (e.g. `EmptyDictationDumpService`)
  injected into `DictationOrchestrator` as an optional collaborator, like
  `ModelLifecycleManager` — the orchestrator stays constructible in tests without it.
- `NAudio.Wave.WaveFileWriter` is already a dependency; converting float → int16
  is the inverse of `AudioCaptureService.OnDataAvailable`.
- Shared seam with `main-rc541` (overlay "nothing recognized"): both react to the
  same "empty result for a real recording" moment. Whichever task lands first should
  introduce one explicit hook — e.g. an `EmptyResult` event carrying duration/levels —
  that the other then subscribes to, rather than two ad-hoc branches.
- Consider a "Open diagnostics folder" link on the Dictation settings page later; out
  of scope here.
