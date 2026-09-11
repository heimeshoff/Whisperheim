---
id: main-hh6zw
title: Peak-normalize audio before decode in TranscriptionService so quiet recordings can no longer collapse to an empty transcript (defense in depth against the sherpa-onnx NemoNormalizePerFeature bug)
status: doing
type: bug
context: main
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [dictation, transcription, audio, parakeet, robustness]
related_adrs: [0006]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: [main-t6r2k, main-v7k2d]
---

## Why

Long dictations are occasionally lost: the audio is captured and decoded, but
sherpa-onnx returns `""` and nothing is typed (see `infrastructure-anvty` for the
full diagnosis — 38 lost dictations ≥10 s in the current log, rate rising with
length). The upstream cause is a float32 cancellation in NeMo per-feature
normalization that turns one floor-pinned mel bin into a +7500 outlier, which then
wrecks the INT8 encoder's dynamic quantization scale for the whole utterance.

Offline sweep (same model, same config as the app, one 18 s synthesized utterance at
decreasing gain, 12 steps):

| Input treatment | empty results (sherpa 1.13.3 / 1.13.4) | empty (1.13.8) |
|---|---|---|
| raw | 6 — non-monotonic: ×0.1 empty, ×0.07 ok, ×0.05 empty, ×0.03 ok … | 0 |
| + gaussian dither 1e-4 | 3 | 0 |
| **peak-normalized to 0.5** | **0** | **0** |

Peak normalization neutralizes the failure on every version and is a pure,
cheap, testable transform. It belongs in the app regardless of the package bump:
it also covers the residual case 1.13.8 still fails (×0.005 gain with dither) and
any future model/runtime regression of the same shape. The Elgato Wave:3 dictation
mic sits at a low level at conversational distance — exactly the regime the sweep
shows kipping.

## What

Add a pure normalizer and apply it at the single choke point every consumer shares:

- New pure function (e.g. `AudioLevelNormalizer.PeakNormalize(float[] samples,
  float targetPeak = 0.5f, float silenceFloor = 1e-4f)`) returning the samples scaled
  so that `max|x| == targetPeak`. Rules:
  - **Never attenuates.** If the peak is already ≥ `targetPeak`, return the input
    unchanged (loud audio decodes fine today; don't touch it).
  - **Never amplifies silence.** If the peak is below `silenceFloor`, return the
    input unchanged — a muted mic must stay a no-speech result, not amplified noise.
  - Otherwise multiply by `targetPeak / peak`. Output is finite for all inputs
    (no NaN/Inf for empty arrays, zero arrays, or arrays containing ±1.0).
- `TranscriptionService.DecodeAudio` applies it to `samples` before
  `stream.AcceptWaveform` (under the existing lock, after the self-heal reload —
  ADR-0006). This single placement protects hold-to-talk dictation, the VAD
  `DictationPipeline`, the HTTP API, file/stream chunks, and call transcription.
- The existing `[TranscriptionService] Transcribed …` log line additionally reports
  the input peak and the gain applied (`peak=0.012 gain=41.7x`, or `gain=1x`), so a
  future empty result can be correlated with level without reproducing it.
- The target peak is a constant, not a setting. 0.5 is the value the sweep verified;
  keep it as a named constant with the sweep's provenance in a comment.

## Acceptance criteria

- [ ] `AudioLevelNormalizer.PeakNormalize` exists as a pure, static function with
      unit tests covering: empty array; all-zero array; peak below `silenceFloor`
      (unchanged); peak above `targetPeak` (unchanged, same instance or equal
      contents); quiet signal (result peak equals `targetPeak` within 1e-6); result
      contains no NaN/Infinity for inputs containing ±1.0.
- [ ] `TranscriptionService.DecodeAudio` calls it before `AcceptWaveform`; the
      `Transcribed …` log line includes the measured input peak and the applied gain.
- [ ] Regression test with the real model (skipped with a reason when
      `ModelManagerService.ParakeetEncoderPath` is absent): a synthesized ≥15 s
      utterance decoded at gains ×1, ×0.1, ×0.05, ×0.02, ×0.01 returns non-empty
      text at every gain and the ×1 and ×0.05 transcripts are identical after
      whitespace normalization. Shared with `infrastructure-anvty` — create once,
      reuse.
- [ ] A pure-silence buffer (all zeros, 3 s) still decodes to an empty string — the
      normalizer must not manufacture text from nothing.
- [ ] All existing tests remain green; decode latency for a 30 s buffer changes by
      less than 5 ms (the normalizer is one pass over the samples).

## Notes

- Silence floor rationale: NAudio delivers exact digital zeros when the Wave:3 is
  hard-muted; the overlay's `NoMic` detection uses RMS < 1e-4 over 10 frames. A
  peak floor of 1e-4 keeps those buffers untouched.
- Do not put the normalization into `DictationOrchestrator` — that would leave the
  HTTP API and file paths exposed. The shared-engine argument is ADR-0006's.
- Interaction with `SilenceChunker` (file path): chunks are normalized
  independently, which is fine — each chunk is its own decode.
- Independent of `infrastructure-anvty` (package bump). Either order; both should
  ship. If the bump lands first, this task's regression test already passes on raw
  input — that is expected; the unit tests on the normalizer are the proof for
  this task.
