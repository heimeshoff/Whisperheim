---
id: 0012
title: Pin org.k2fsa.sherpa.onnx to an exact version and drop the unused Microsoft.ML.OnnxRuntime package reference
scope: infrastructure
status: accepted
date: 2026-09-11
supersedes: []
superseded_by: []
related_tasks: [infrastructure-anvty]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
---

# ADR 0012: Pin sherpa-onnx exact + drop unused Microsoft.ML.OnnxRuntime

> Note on ADR numbering: drafted as 0011 in a parallel worktree; renumbered to 0012 at integration because 0011 was taken by the sibling task main-ma9j8 that merged first.

## Context
Dictations were being silently lost: long or quiet recordings decoded to an empty
transcript, and `DictationOrchestrator.TranscribeFinalAsync` returns silently on
empty text (no user-visible error). Diagnosed 2026-09-11 (infrastructure-anvty) to
sherpa-onnx's `NemoNormalizePerFeature` — computing per-feature variance as
`E[x²] − (E[x])²` in float32 — catastrophically cancelling to 0 when a mel bin sits
at the log floor. `inv_std` then explodes, one feature normalizes to an extreme
outlier, the INT8 encoder's per-tensor dynamic-quantization scale is set by that
outlier, every real feature collapses into one bucket, and the TDT decoder emits
blank for the whole utterance. Fixed upstream in sherpa-onnx 1.13.5 (PR #3857).
Reproduced offline: 6/12 gain steps empty on 1.13.3/1.13.4, 0/12 on 1.13.8.

While diagnosing, `WhisperHeim.csproj` was found to reference both
`org.k2fsa.sherpa.onnx` and `Microsoft.ML.OnnxRuntime` as floating `Version="1.*"`.
The managed ORT API (`InferenceSession`, `OrtEnv`) is referenced nowhere in `src/`
— the second package's only effect was letting its `onnxruntime.dll` (1.27.0)
override the one sherpa-onnx bundles. sherpa-onnx 1.13.8 requires ORT API 28;
paired with the stray 1.27 dll, the recognizer constructor crashes the process
outright (`0xC0000005`, "requested API version [28] ... only ... [1, 27] ...
supported"). With two floating `1.*` references, any fresh restore could already
reproduce this crash the moment either package published a newer major/minor on
NuGet — which is exactly what happened (sherpa 1.13.8 and ORT 1.30.0 both
published within a day of each other).

## Decision
- Pin `org.k2fsa.sherpa.onnx` to the **exact** version `1.13.8` — no `1.*`
  wildcard. The version carries a load-bearing correctness fix (not just a
  feature bump), so an unreviewed automatic upgrade to a version that changes
  behavior again is exactly the failure mode this pin prevents.
- Remove `Microsoft.ML.OnnxRuntime` entirely rather than pin it alongside sherpa.
  It has no call sites and no reason to exist independent of the runtime
  sherpa-onnx already bundles (`org.k2fsa.sherpa.onnx.runtime.win-x64`, which
  ships ORT 1.28.2 for sherpa 1.13.8). Keeping an unused package "just to be
  safe" is what caused the crash in the first place.
- If a future task needs the managed ORT API directly, it must pin the ORT
  package to the exact version sherpa's corresponding release bundles — never
  float it independently of sherpa's version.

## Consequences
- Every consumer of `TranscriptionService` (dictation, HTTP API, file and stream
  transcription — ADR-0006) gets the fix at once, since they all share one
  recognizer construction path (`LoadModelLocked`).
- Future sherpa-onnx upgrades are a deliberate one-line version-string edit plus
  a re-verify of the bundled ORT version, not a silent `dotnet restore` side
  effect.
- A quiet-audio regression test (`tests/WhisperHeim.Tests/QuietAudioTranscriptionRegressionTests.cs`,
  fixture in `SynthesizedSpeechFixture.cs`) now guards this specific failure
  mode end-to-end against the real model, gated to skip (not fail) on machines
  without the ~640 MB Parakeet model files downloaded.

## References
- `src/WhisperHeim/WhisperHeim.csproj` (pinned/removed package references)
- `tests/WhisperHeim.Tests/QuietAudioTranscriptionRegressionTests.cs`
- `tests/WhisperHeim.Tests/SynthesizedSpeechFixture.cs` (reused by main-hh6zw)
- Upstream: k2-fsa/sherpa-onnx PR #3857, issue #3853, issue #2258, release notes v1.13.5
- ADR-0006 (shared `TranscriptionService` across dictation/HTTP/file/stream consumers)
