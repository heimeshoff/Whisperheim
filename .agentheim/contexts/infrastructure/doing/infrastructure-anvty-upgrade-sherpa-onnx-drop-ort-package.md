---
id: infrastructure-anvty
title: Upgrade sherpa-onnx to 1.13.8 (carries the NemoNormalizePerFeature fix that silently empties quiet dictations), drop the unused Microsoft.ML.OnnxRuntime package, and pin both native packages to exact versions instead of `1.*`
status: doing
type: chore
context: infrastructure
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [sherpa-onnx, onnxruntime, parakeet, dictation, packaging, nuget]
related_adrs: [0006]
related_research: [parakeet-quantization-and-nemotron-2026-06-28]
prior_art: [infrastructure-d2v7n]
---

## Why

Dictations are being lost. The user speaks a long message, the overlay shows the
waveform, the hotkey is released — and nothing is typed. "Repeat" then replays the
*previous* dictation, because `_lastNormalDictation` is only set right before typing.

Diagnosed 2026-09-11 from `%APPDATA%\WhisperHeim\whisperheim.log`: the recording is
captured in full and decoded in normal time, but sherpa-onnx returns an **empty
string** (`[TranscriptionService] Transcribed 27.10s audio in 1940ms (RTF=0.072): ""`),
and `DictationOrchestrator.TranscribeFinalAsync` returns silently on empty text.
Across the whole log the empty rate rises with length: 0.2 % of 3–10 s dictations,
1.2 % of 10–20 s, 2.4 % of 20–30 s, 4.1 % of ≥30 s (38 lost dictations ≥10 s).

Root cause is upstream: sherpa-onnx PR #3857 *"Fix float32 catastrophic cancellation
in NemoNormalizePerFeature"* (merged 2026-08-11, shipped in **1.13.5**). The per-feature
normalization computed variance as `E[x²] − (E[x])²` in float32; when one mel bin sits
at the log floor the subtraction cancels to 0, `inv_std` explodes to 1e5, that bin
normalizes to ≈ +7500, the INT8 encoder's per-tensor dynamic-quantization scale is set
by that outlier, every real feature collapses into one bucket, and the TDT decoder
emits blank for every frame — all-or-nothing, never a partial transcript.

Reproduced offline with synthesized speech (same model files, same
`OfflineRecognizerConfig` as `TranscriptionService`), decoding the same 18 s utterance
at decreasing gain. Empty results out of 12 gain steps:

| Combination | empty |
|---|---|
| sherpa-onnx 1.13.3 + ORT 1.27.0 — **the published app** | 6 (non-monotonic: ×0.1 empty, ×0.07 fine, ×0.05 empty, ×0.03 fine …) |
| sherpa-onnx 1.13.3 + ORT 1.28.2 | 6 |
| sherpa-onnx 1.13.4 (bundled ORT) | 6 |
| **sherpa-onnx 1.13.8 (bundled ORT 1.28.2)** | **0** |

So the fix is in sherpa-onnx itself, not in the ONNX Runtime version.

**Packaging hazard found on the way.** `WhisperHeim.csproj` references both
`org.k2fsa.sherpa.onnx` and `Microsoft.ML.OnnxRuntime` as `Version="1.*"`. The
managed ORT API (`InferenceSession`, `OrtEnv`) is referenced **nowhere** in `src/`
— the package's only effect is that its `onnxruntime.dll` (currently 1.27.0) overrides
the one sherpa bundles. sherpa-onnx 1.13.8 requires ORT API 28; combined with the
1.27 dll the recognizer constructor dies immediately:

```
The requested API version [28] is not available, only API versions [1, 27] are supported in this build. Current ORT Version is: 1.27.0
Fatal error. 0xC0000005
   at SherpaOnnx.OfflineRecognizer.SherpaOnnxCreateOfflineRecognizer(...)
```

With two floating `1.*` references, a fresh restore on any machine can already
produce this crash today (sherpa 1.13.8 is on NuGet since 2026-09-11, ORT 1.30.0 since
2026-09-10 — whichever resolves wins the dll copy).

## What

1. Pin `org.k2fsa.sherpa.onnx` to the exact version `1.13.8` (no wildcard).
2. Remove the `Microsoft.ML.OnnxRuntime` package reference — it is unused and only
   exists to (mis)supply `onnxruntime.dll`. sherpa-onnx's `runtime.win-x64` package
   ships the matching runtime (1.28.2 for 1.13.8). If a later reason surfaces to keep
   a separate ORT package, it must be pinned to the ORT version sherpa's release
   bundles — never floated independently.
3. Verify the published output (`scripts/publish.ps1` → `publish/`) carries the
   sherpa-bundled `onnxruntime.dll` and that the recognizer loads and dictates.
4. Add the quiet-audio regression test (shared with `main-hh6zw`, whichever lands
   first creates it): decode a synthesized utterance at gains ×1, ×0.1, ×0.05, ×0.02,
   ×0.01 through `TranscriptionService` and assert non-empty text for every step.
   Skips (does not fail) when the Parakeet model files are not present on the machine.

## Acceptance criteria

- [ ] `src/WhisperHeim/WhisperHeim.csproj` references `org.k2fsa.sherpa.onnx` with the
      exact version `1.13.8` — no `*` remains in either native-package reference.
- [ ] `Microsoft.ML.OnnxRuntime` is no longer referenced by `WhisperHeim.csproj`, and
      `obj/project.assets.json` after restore lists no `Microsoft.ML.OnnxRuntime*` package.
- [ ] `dotnet build` and `dotnet test` (both test projects) are green.
- [ ] After `scripts/publish.ps1`, `publish/onnxruntime.dll` reports file version 1.28.x
      (the version sherpa-onnx 1.13.8 bundles) and `publish/sherpa-onnx.dll` reports 1.13.8.
- [ ] The published app starts, `[TranscriptionService] Parakeet TDT 0.6B model loaded
      successfully` appears in the log, and a hold-to-talk dictation types text.
- [ ] Regression test present: synthesized-speech fixture decoded at gains ×1 / ×0.1 /
      ×0.05 / ×0.02 / ×0.01 yields non-empty text at every gain; the test is skipped
      (with a reason) when `ModelManagerService.ParakeetEncoderPath` does not exist.
- [ ] `CHANGELOG.md` gets an entry naming the lost-dictation fix and the package pin.

## Notes

- Diagnosis session 2026-09-11; the offline reproduction was a throwaway console
  project (sherpa-onnx + NAudio, same config as `TranscriptionService.LoadModelLocked`)
  driven by a Windows-TTS WAV at 16 kHz mono. Rebuild it in 10 minutes if needed —
  or, better, make it the regression test above. Windows TTS (`System.Speech`,
  voice "Microsoft Zira Desktop") can synthesize the fixture at test time so no
  binary WAV needs committing; alternatively commit a short 16 kHz mono WAV.
- Upstream references: k2-fsa/sherpa-onnx PR #3857 (fix), issue #3853 (trigger),
  issue #2258 (older report of the same symptom, dither workaround), release notes
  v1.13.5. sherpa-onnx 1.13.8 NuGet published 2026-09-11.
- The C# binding exposes no `dither` on `FeatureConfig`, so the upstream dither
  workaround is not available from managed code — the version bump is the real fix.
- Defense in depth lives in `main-hh6zw` (peak-normalize before decode): in the
  sweep, peak normalization to 0.5 produced 0 empties on **every** version, and even
  1.13.8 still emptied one absurd case (×0.005 gain with heavy dither). Both tasks are
  independent and can ship in either order.
- ADR-0006 matters here because `TranscriptionService` is shared by dictation, the
  HTTP API, file and stream transcription — the package bump changes every consumer
  at once, which is the intent.
