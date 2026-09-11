---
id: 0013
title: Peak-normalize audio before decode as defense in depth against quiet-audio empty transcripts
scope: main
status: accepted
date: 2026-09-11
supersedes: []
superseded_by: []
related_tasks: [main-hh6zw, infrastructure-anvty]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
---

# ADR 0013: Peak-normalize audio before decode as defense in depth

## Context
`infrastructure-anvty` diagnosed 38 lost dictations ≥10 s: sherpa-onnx's
`NemoNormalizePerFeature` has a float32 catastrophic-cancellation bug (upstream PR
#3857, fixed in 1.13.5) that can turn one floor-pinned mel bin into a +7500 outlier,
which wrecks the INT8 Parakeet encoder's dynamic quantization scale and silently
decodes a quiet-but-real utterance to `""`. That task pinned sherpa-onnx to 1.13.8,
which fixes the bug upstream.

An offline sweep (same model/config as the app, one 18 s synthesized utterance,
12 decreasing-gain steps) showed the fix is not the whole story:

| Input treatment | empty (sherpa 1.13.3/1.13.4) | empty (1.13.8) |
|---|---|---|
| raw | 6/12, non-monotonic with gain | 0/12 |
| + gaussian dither 1e-4 | 3/12 | 0/12 |
| **peak-normalized to 0.5** | **0/12** | **0/12**, including a residual 1.13.8 failure case (×0.005 gain + dither) |

Peak normalization neutralizes the failure independent of the sherpa-onnx version,
and the app's dictation mic (Elgato Wave:3, conversational distance) sits at
exactly the quiet level the sweep shows tripping the bug.

## Decision
- Add a pure, static `AudioLevelNormalizer.PeakNormalize(samples, targetPeak = 0.5,
  silenceFloor = 1e-4)` that scales a buffer up to `targetPeak` peak amplitude,
  **never attenuates** (peak already ≥ target → unchanged) and **never amplifies
  silence** (peak < `silenceFloor` → unchanged, so a hard-muted mic stays a
  no-speech result rather than manufactured noise/text).
- Apply it inside `TranscriptionService.DecodeAudio`, under the existing decode
  lock, after the ADR-0006 self-heal reload and before `stream.AcceptWaveform`.
  This is the single choke point every consumer of the shared engine passes
  through — hold-to-talk dictation, the VAD dictation pipeline, the HTTP STT API,
  file/stream transcription, and call transcription all get the protection without
  each needing its own copy of the logic (ADR-0006's shared-engine argument).
- Ship this **independent of and in addition to** the sherpa-onnx version pin
  (`infrastructure-anvty`): it is a pure, cheap, testable transform that also
  covers 1.13.8's residual failure case and any future model/runtime regression of
  the same shape.
- `targetPeak` (0.5) and `silenceFloor` (1e-4) are hardcoded constants with the
  sweep's provenance in a doc comment, not user-facing settings — there is no
  reason for a user to tune them, only a reason to re-run the sweep if the model
  or runtime changes materially.
- The `Transcribed …` trace line now reports the measured input peak and applied
  gain (`peak=0.012 gain=41.7x`, or `gain=1x`) so a future lost/lossy dictation can
  be correlated with input level without needing to reproduce it.

## Consequences
- Loud audio is provably untouched (never-attenuate rule) — no risk of the fix
  degrading transcription quality for users who already record at a healthy level.
- A hard-muted mic (exact-zero buffer) still decodes to `""` — the never-amplify
  rule preserves the existing "no mic input" behavior instead of hallucinating
  text from noise.
- One extra O(n) pass over the sample buffer per decode (measure peak) plus, for
  the quiet case only, one more O(n) pass to apply gain; negligible next to the
  ~seconds-scale ASR decode itself.
- Not placed in `DictationOrchestrator`: that would leave the HTTP API and
  file/stream paths unprotected. The single-choke-point placement is deliberate
  and mirrors ADR-0006's decode-is-shared reasoning.

## References
- `src/WhisperHeim/Services/Transcription/AudioLevelNormalizer.cs`
- `src/WhisperHeim/Services/Transcription/TranscriptionService.cs` (`DecodeAudio`)
- `tests/WhisperHeim.Tests/AudioLevelNormalizerTests.cs`
- `tests/WhisperHeim.Tests/QuietAudioTranscriptionRegressionTests.cs` (shared fixture with infrastructure-anvty)
- Builds on ADR-0006 (shared-engine decode lock / self-heal) and infrastructure-anvty's `0012-pin-sherpa-onnx-exact-drop-unused-onnxruntime-package.md`
