---
topic: Quantization options for NVIDIA Parakeet (NeMo/FastConformer-TDT) self-hosted inference, and how "Nemotron" relates to Parakeet for ASR
date: 2026-06-28
requested_by: user
related_tasks: []
---

# Research: Parakeet quantization + "Nemotron" vs Parakeet (mid-2026)

## Question
Follow-up to `best-stt-models-german-english-2026-06-28.md` for the self-hosted WhisperHeim system (currently NVIDIA Parakeet). Two questions:

1. **Quantization to optimize Parakeet** (`parakeet-tdt-0.6b-v2/v3`, NeMo / FastConformer-TDT): which precision/quantization options actually exist (FP16/BF16, INT8, FP8, lower), which toolchains work (NeMo export, TensorRT / TensorRT-LLM, ONNX Runtime, CTranslate2, parakeet-mlx, HF community builds), what you gain (VRAM, throughput/RTFx, smaller GPUs/CPU), and what you risk (WER degradation, sensitive layers, TDT-decoder quantizability). Also clarify the user's word "dequantization."
2. **"Nemotron" vs Parakeet**: disambiguate. Is there an ASR model called Nemotron? Compare Parakeet against the relevant NeMo alternatives (Canary / Canary-Qwen) on accuracy (DE+EN WER), speed (RTFx), size/VRAM, decoder type, German support, license — and address the "pair Parakeet with a Nemotron LLM for post-correction" pattern.

## Summary (decision-relevant first)
- **Your single biggest, zero-risk win is half precision (FP16/BF16), and you very likely already have it.** NVIDIA's own 10x NeMo speedups (RTFx ~2,000–6,000) come from **BF16/FP16 + the label-looping decoder + CUDA Graphs + full batching — *not* from INT8/FP8 quantization** [1]. Half precision roughly halves weight memory (0.6B params: ~2.4 GB FP32 → ~1.2 GB FP16) and NVIDIA states it does so "without compromising accuracy" [1]. If WhisperHeim is running Parakeet in FP32, switching to BF16/FP16 is the highest-value, lowest-risk change.
- **INT8 quantization for Parakeet is real but community-driven, not first-party.** There is no official NVIDIA INT8/FP8 Parakeet checkpoint; the working path is **ONNX Runtime weight-only dynamic INT8** (`onnx-asr` / `sherpa-onnx` runtimes) [4][5][8]. A community ARM test reported INT8 ONNX at ~7.8× realtime vs ~4.7× for FP16 (~40% faster) [5]. General conformer/transducer ASR literature puts INT8 accuracy impact at **typically <1% WER, often near-lossless**, with ~2–3.6× speedup and ~4× compression [9] — but **no one has published a German WER number for INT8 Parakeet specifically** (single-/no-source; flag). ⚠️ **GROUNDING CORRECTION (2026-06-28, post-review):** WhisperHeim is **already running an INT8 Parakeet v3 build** — `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` (the sherpa-onnx maintainer's build), wired up in `src/WhisperHeim/Services/Models/ModelManagerService.cs:37-61`. Unlike the `nasedkinpv` build this report originally cited, **this build ships `encoder.int8.onnx`, `decoder.int8.onnx`, AND `joiner.int8.onnx`** — the full transducer (encoder + decoder + joiner) is INT8, not encoder-only. So the "INT8 only exists as community builds / TDT decoder stays FP32" framing below is misleading for WhisperHeim's actual deployment: a German-capable, fully-INT8 v3 build exists and is in production. The genuinely-open item is narrower — no *measured German WER* is published for this build, so the accuracy delta vs FP16/FP32 is still local-A/B-test-only.
- **CTranslate2 does NOT support Parakeet/TDT.** The faster-whisper-style INT8 path is Whisper-only (CT2 targets transformer encoder-decoder / decoder models); for Parakeet the INT8 route is **ONNX Runtime or TensorRT**, not CTranslate2 [11]. `parakeet-mlx` (Apple-silicon MLX port) is a separate community option for Macs.
- **TERMINOLOGY UPDATE — and this overturns the task's premise:** As of **June 4, 2026** NVIDIA *does* ship ASR models branded "Nemotron." The **"Nemotron Speech" collection** includes `nemotron-3.5-asr-streaming-0.6b` (600M, cache-aware FastConformer + RNN-T, 40 locales **including German**, streaming) and English-only siblings [2][6][7]. So "Nemotron ASR" is now a real thing — it is NVIDIA's **streaming-optimized** ASR line, a sibling of Parakeet (both FastConformer). "Nemotron" still *also* names the LLM family, so the name is now genuinely overloaded.
- **Parakeet vs Nemotron Speech is a batch-vs-streaming choice, not better-vs-worse.** Parakeet-TDT-0.6B-v3 is the offline/batch throughput king (RTFx ~3,300, German WER ~4.2–5%). `nemotron-3.5-asr-streaming-0.6b` is built for **real-time / many concurrent streams** (configurable 80–1120 ms chunks, "~17× more concurrent streams" than Parakeet RNNT 1.1B on one H100), with German FLEURS WER ~8.31% **in a streaming config — not comparable to Parakeet's offline number** [6][9]. For batch transcription, stay on Parakeet; only look at Nemotron Speech if WhisperHeim needs low-latency live captioning. For best DE accuracy, Canary-1B-v2 still leads (see prior report).

## Findings

### 1. Clarifying "dequantization" vs quantization
The optimization technique is **quantization**: storing/computing weights (and sometimes activations) in fewer bits — FP32 → FP16/BF16 (16-bit), → INT8 (8-bit), → FP8, etc. This is what shrinks the model and speeds it up. **Dequantization** is the *inverse* runtime step (converting low-bit values back to higher precision for a given op); it happens automatically inside the runtime and is not something you "do" to optimize. The practical thing the user wants — "make Parakeet smaller/faster via reduced precision" — is **quantization**. The other plausible reading, *"run a pre-quantized checkpoint,"* is covered in §3 (download a ready INT8 ONNX build rather than quantizing yourself).

### 2. Precision/quantization options that actually exist for Parakeet/NeMo

**FP16 / BF16 (half precision) — the default, first-party, recommended baseline.**
NVIDIA's "Accelerating Leaderboard-Topping ASR Models 10x" (Sep 24, 2024) is explicit that the headline RTFx ~2,000–6,000 numbers for CTC/RNN-T/TDT come from a *bundle* of optimizations, and the precision component is **full half-precision inference in `bfloat16` or `float16`**, which "eliminates unnecessary casting overhead without compromising accuracy." The rest of the speedup is the **label-looping decoding algorithm**, **CUDA Graphs conditional nodes** (kills kernel-launch overhead), and **full batching** (~20% alone) — *not* quantization [1]. Enable via `model.to(torch.bfloat16)` or `compute_dtype=float16/bfloat16` in NeMo transcription scripts [1]. **Gain:** ~2× weight-memory reduction, large throughput gain, no documented accuracy cost. **This is the option to confirm first.**

**INT8 (post-training quantization) — works via ONNX, community-only.**
- No official NVIDIA INT8 Parakeet checkpoint exists (the HF v2 quantization request thread got no NVIDIA response) [4].
- The practical path is **ONNX Runtime weight-only dynamic quantization**. Two community builds matter here:
  - ⚠️ **`csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` — the build WhisperHeim actually uses** (sherpa-onnx maintainer). Ships **all three transducer components in INT8**: `encoder.int8.onnx`, `decoder.int8.onnx`, `joiner.int8.onnx` plus `tokens.txt` — i.e. the full encoder+decoder+joiner is quantized, not encoder-only. No WER published.
  - `nasedkinpv/parakeet-tdt-0.6b-v3-onnx-int8` (the build originally surveyed here) quantizes **only MatMul/Gemm ops; convolution layers stay FP32** (ConvInteger isn't supported on the ONNX Runtime Web/WASM backend), and the **decoder/joiner (~52 MB) stays unquantized** — i.e. mostly an *encoder* INT8 quantization. Total ~890 MB, CC-BY-4.0; no WER/RTFx published [3].
  - Takeaway: the "decoder/joiner stays FP32" detail is build-specific — it's true of the `nasedkinpv` build, **not** of the `csukuangfj` build WhisperHeim runs.
- Runtimes that load NeMo transducer models as INT8 ONNX: **`onnx-asr`** (PyPI) and **`sherpa-onnx`** (k2-fsa) — these are the realistic on-device/CPU INT8 paths [5][8].
- **Measured speedup (single source):** on an ARM Rockchip RK3588, INT8 ONNX ran a 7 s clip in 0.9 s (~7.8× realtime) vs 1.5 s for FP16 ONNX (~4.7×) — ~40% faster; **no accuracy measured** [5].

**FP8 — exists in the NeMo/TensorRT stack but is documented for LLMs (Nemotron), not Parakeet.**
NeMo supports BF16 and **FP8** mixed precision in training, and TensorRT can turn FP8 checkpoints into inference engines — but the NeMo *Quantization* user-guide page is NLP/LLM-oriented, and **no FP8 Parakeet ASR build or recipe was found** [10][12]. Treat FP8 Parakeet as "possible in principle, no public artifact" (flag).

**TensorRT / TensorRT-LLM.**
TensorRT export exists across NeMo, and the 10x blog's A100 RTFx ~2,053 number is from the optimized NeMo/GPU path [1]. However, a clean, public **TensorRT(-LLM) INT8/FP8 quantization recipe for Parakeet-TDT specifically** was not found — TensorRT-LLM's quantization tooling is centered on LLMs. For ASR, the documented gains are the half-precision + decoding-algorithm path, with TensorRT as the deployment engine (flag: no Parakeet-specific INT8 TensorRT benchmark located).

**CTranslate2 — does not support Parakeet/TDT.** CT2 powers faster-whisper's INT8 (fits Whisper-large in ~3 GB) but targets transformer encoder-decoder/decoder architectures; **it has no transducer/TDT support**, so the faster-whisper INT8 trick does not transfer to Parakeet [11]. Don't plan around it.

**Apple-silicon / `parakeet-mlx`.** A community MLX port runs Parakeet on Apple Silicon and supports reduced-precision; relevant only if WhisperHeim ever targets Macs (not deeply verified here — flag).

**Lower than INT8 (INT4 / 2-bit / 1-bit).** Research shows conformer ASR can go to INT4 (weights) with INT8 activations at minimal loss, and even 2-bit/1-bit with quantization-aware *training* [9]. These are research-grade, require retraining, and are **not** drop-in for Parakeet — ignore for production.

### 3. What you gain vs what you risk (quantization)

**Gains**
- **VRAM/size:** FP32→FP16/BF16 ≈ ½ the weights (~2.4 GB → ~1.2 GB for 0.6B). INT8 ≈ ½ again for the quantized layers. Note: at batch=8 an independent test measured Parakeet *peak* VRAM ~5.1 GB (weights + activations + buffers), well above raw weight size — so activation/batch memory dominates at scale, and weight-only INT8 helps model footprint more than peak working set [9].
- **Throughput/latency:** half precision is the bulk of NeMo's 10× speedup [1]; INT8 adds a further ~40% in the one ARM test [5], and ASR literature reports ~2–3.6× for INT8 broadly [9].
- **Smaller GPUs / CPU / edge:** INT8 ONNX via `onnx-asr`/`sherpa-onnx` is specifically what enables Parakeet on CPU/ARM/edge [5][8].

**Risks**
- **WER degradation:** half precision = effectively none per NVIDIA [1]. INT8 on conformer/transducer ASR = **typically <1% WER, often "near-lossless," ~4× compression** across multiple studies [9] — but these are *not* Parakeet-v3-German measurements; **no published INT8 German WER for Parakeet exists** (flag, single-/no-source).
- **Sensitive layers:** literature says **lower encoder layers are more robust; upper layers are more sensitive**, and convolution ops are awkward to quantize — which is exactly why the community ONNX build leaves convs in FP32 [3][9].
- **TDT decoder quantizability:** **unverified.** The community INT8 build quantizes the encoder and leaves the **decoder/joiner unquantized** [3], and no source directly measures TDT-decoder INT8 accuracy. The TDT/RNN-T decoder is small (~52 MB) so quantizing it buys little anyway — leaving it in FP16 is the pragmatic, low-risk choice.
- **Tooling/ecosystem risk:** all INT8 Parakeet artifacts are community-maintained (CC-BY-4.0), unbenchmarked for German, and tied to ONNX Runtime — not NVIDIA-supported. A/B test on your own German+English audio before trusting an INT8 build.

**Net recommendation (decision left to you):** confirm BF16/FP16 first (free, first-party, no accuracy cost). Only pursue INT8 ONNX if you need CPU/edge/very-low-VRAM deployment, and then **measure German WER yourself** because no one else has published it.

### 4. "Nemotron" vs Parakeet — disambiguation and comparison

**The premise has changed: there is now a real NVIDIA ASR family called "Nemotron."** The task brief assumed Nemotron = LLMs only and that the user must mean Canary. That was correct historically, but as of **June 4, 2026** NVIDIA published the **"Nemotron Speech" collection** — "production-ready enterprise speech models … for ASR, TTS, Speaker Diarization and S2S" [2]. Members include:
- **`nemotron-3.5-asr-streaming-0.6b`** — 600M params, **cache-aware FastConformer encoder + RNN-T decoder**, **40 language-locales** (German `de-DE` in the top "transcription-ready" tier of 19), streaming with **configurable 80–1120 ms chunks**, "~17× more concurrent streams" vs Parakeet RNNT 1.1B on a single H100, **license OpenMDW-1.1**, released 2026-06-04 [6].
- `nemotron-speech-streaming-en-0.6b` (English streaming), `parakeet-unified-en-0.6b`, `magpie_tts_multilingual_357m` (TTS), `personaplex-7b-v1` (audio-to-audio/S2S) [2].

So: **"Nemotron ASR" exists and is real** — it is NVIDIA's *streaming-optimized* ASR brand, architecturally a **sibling of Parakeet** (shared FastConformer lineage in NeMo). Crucially, **"Nemotron" now spans both LLMs *and* speech**, so the name is overloaded — naming that overlap clearly is itself the useful finding. (NVIDIA's *Nemotron LLMs* — Llama-Nemotron, Nemotron Nano — are still not ASR models.)

**The key architectural difference (cache-aware streaming):** Nemotron's cache-aware encoder processes each audio frame once and reuses cached state, so **chunk size affects latency but not accuracy** — a property Parakeet TDT lacks [9]. That makes Nemotron the right tool for live/streaming, and Parakeet the right tool for offline/batch.

**Comparison table** (mind the benchmark caveats below — do **not** read these as one apples-to-apples ranking):

| Model | Params | Decoder | German | Best at | German WER | RTFx / throughput | License |
|---|---|---|---|---|---|---|---|
| **Parakeet-TDT-0.6B-v3** | 0.6B | TDT (transducer) | Yes (25 EU) | Offline/batch throughput | ~4.2% (leaderboard) / ~4.84–5.04% (own card) [prior report 3][6'] | RTFx ~3,300 batch offline | CC-BY-4.0 |
| **nemotron-3.5-asr-streaming-0.6b** | 0.6B | RNN-T, cache-aware FastConformer | Yes (40 locales) | Real-time / many concurrent streams | ~8.31% FLEURS **streaming config** [6] | ~17× concurrent streams vs Parakeet RNNT 1.1B; p50 latency ~18 ms [6][9] | OpenMDW-1.1 |
| **Canary-1B-v2** | ~1B | Transformer (attention) | Yes (25 EU) | Best DE accuracy among NeMo offline | ~4.1% (leaderboard) [prior report 3] | RTFx ~749 | CC-BY-4.0 |
| **Canary-Qwen-2.5B** | 2.5B | Qwen LLM decoder (SALM) | **No (English-only)** | English accuracy leader | n/a | RTFx ~418 | check card |

**WER caveat (critical):** Nemotron's German ~8.31% is a **streaming FLEURS** number; Parakeet's ~4.2–5% are **offline** numbers from different tables/normalizations. Streaming inherently costs WER, so the gap **overstates** any real quality difference. (The same effect appears in an independent L4 benchmark where Parakeet scored a poor 15.72% WER *in a streaming-style config* while excelling at batch throughput [9].) **Never compare these two numbers as equivalent** — they are different tasks. The only valid comparison is your own audio, run in your own latency mode.

**Did the user mean "pair Parakeet with a Nemotron LLM for post-correction"?** That is a legitimate, separate pattern: ASR (Parakeet/Whisper) → transcript → **LLM error-correction/formatting** (punctuation, casing, entity/number fixing, domain rewriting). NVIDIA's **Canary-Qwen-2.5B** is the *integrated* version of this idea (Speech-Augmented LLM, encoder + Qwen LLM decoder), and it tops English accuracy but is **English-only and ~7× slower than Parakeet** [prior report]. A *decoupled* Parakeet→Nemotron-LLM pipeline would let you keep Parakeet's speed and add LLM cleanup, but trade-offs are: **added latency and GPU cost, and risk of LLM hallucination/over-rewriting** (the LLM can "correct" things that were actually right). For German specifically, verify the post-correction LLM is strong in German. This is worth a small experiment but is *not* a drop-in accuracy upgrade.

## Sources
1. [Accelerating Leaderboard-Topping ASR Models 10x with NVIDIA NeMo (NVIDIA Technical Blog)](https://developer.nvidia.com/blog/accelerating-leaderboard-topping-asr-models-10x-with-nvidia-nemo/) — primary: BF16/FP16 + label-looping + CUDA Graphs + batching drive the 10x; precision is half-precision, not INT8/FP8. Published 2024-09-24.
2. [NVIDIA Nemotron Speech collection (Hugging Face)](https://huggingface.co/collections/nvidia/nemotron-speech) — primary: confirms a real "Nemotron Speech" ASR/TTS/diarization/S2S family. Mid-2026.
3. [nasedkinpv/parakeet-tdt-0.6b-v3-onnx-int8 (Hugging Face)](https://huggingface.co/nasedkinpv/parakeet-tdt-0.6b-v3-onnx-int8) — community INT8 ONNX build; weight-only dynamic, MatMul/Gemm only, convs FP32, decoder/joiner unquantized; CC-BY-4.0; no WER/RTFx published.
4. [nvidia/parakeet-tdt-0.6b-v2 · "quantized model?" discussion (HF)](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2/discussions/26) — INT8 ONNX path; ARM RK3588 ~7.8× (INT8) vs ~4.7× (FP16) realtime; no NVIDIA response; no WER.
5. [onnx-asr (PyPI)](https://pypi.org/project/onnx-asr/) and [sherpa-onnx NeMo transducer models](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/offline-transducer/nemo-transducer-models.html) — runtimes that run NeMo transducer (Parakeet) as INT8 ONNX on CPU/edge.
6. [nvidia/nemotron-3.5-asr-streaming-0.6b (Hugging Face model card)](https://huggingface.co/nvidia/nemotron-3.5-asr-streaming-0.6b) — primary: 0.6B, cache-aware FastConformer + RNN-T, 40 locales incl German (de-DE), streaming 80–1120 ms, German FLEURS 8.31% / avg 8.84%, ~17× concurrent streams vs Parakeet RNNT 1.1B, OpenMDW-1.1, released 2026-06-04.
7. [Fine-tuning NVIDIA Nemotron Speech ASR on Amazon EC2 (AWS ML Blog)](https://aws.amazon.com/blogs/machine-learning/fine-tuning-nvidia-nemotron-speech-asr-on-amazon-ec2-for-domain-adaptation/) — context on Nemotron Speech ASR family and NeMo lineage.
8. [Turbocharge ASR Accuracy and Speed with NVIDIA NeMo Parakeet-TDT (NVIDIA Technical Blog)](https://developer.nvidia.com/blog/turbocharge-asr-accuracy-and-speed-with-nvidia-nemo-parakeet-tdt/) — Parakeet-TDT architecture, RTFx context.
9. [Benchmarking Open ASR on NVIDIA L4: Parakeet vs Whisper vs Nemotron (E2E Networks)](https://www.e2enetworks.com/blog/benchmarking-asr-models-nvidia-l4-parakeet-whisper-nemotron) — secondary benchmark: Nemotron = cache-aware FastConformer+RNNT, streaming-invariant accuracy, lowest VRAM; Parakeet best batch throughput but poor in streaming config (15.72% WER). Published 2026-03-27. Plus conformer/transducer INT8 quantization literature: [4-bit Conformer QAT (arXiv 2203.15952)](https://ar5iv.labs.arxiv.org/html/2203.15952), [E2E 4-bit RNN-T quantization (arXiv 2206.07882)](https://arxiv.org/pdf/2206.07882) — INT8 ~<1% WER, ~2–3.6× speedup, ~4× compression; lower layers more robust.
10. [Quantization — NVIDIA NeMo Framework User Guide](https://docs.nvidia.com/nemo-framework/user-guide/24.09/nemotoolkit/nlp/quantization.html) — NeMo PTQ/quantization docs are NLP/LLM-oriented (FP8/INT8 via TensorRT-LLM); not an ASR/Parakeet recipe.
11. [CTranslate2 Quantization docs](https://opennmt.net/CTranslate2/quantization.html) / [CTranslate2 repo](https://github.com/OpenNMT/CTranslate2) — INT8/FP16 for transformer enc-dec/decoder models (Whisper); no transducer/TDT support → not usable for Parakeet.
12. [Canary-1B-v2 & Parakeet-TDT-0.6B-v3 paper (arXiv 2509.14128)](https://arxiv.org/html/2509.14128v1) — model details, multilingual eval (FLEURS/CoVoST2/MLS), per-language WER in appendix. Sep 2025.
- Prior report: `.agentheim/knowledge/research/best-stt-models-german-english-2026-06-28.md` (German WER table from Open ASR Leaderboard arXiv 2510.06961; Parakeet v3 / Canary / Voxtral / Whisper comparison).

## Unverified / single-source claims (flagged)
- **No published German (or any-language) WER for an INT8-quantized Parakeet.** INT8 "<1% WER / near-lossless" is from *general* conformer/transducer studies [9], not Parakeet-v3-German. Treat INT8 accuracy as "probably fine, unmeasured for your case" until you A/B test.
- **The ARM INT8 ~40% speedup** [4] is a single hobbyist measurement on RK3588; not a GPU/server number and not NVIDIA-validated.
- **FP8 Parakeet** is supported by the toolchain in principle [10] but **no public FP8 Parakeet artifact or benchmark was found** — do not assume it exists ready-made.
- **TDT/RNN-T decoder INT8 behavior is untested** in the sources for *accuracy*. Note the `nasedkinpv` build leaves the decoder unquantized [3], but the `csukuangfj` build WhisperHeim runs ships a fully-INT8 `decoder.int8.onnx` + `joiner.int8.onnx` — so quantizing the TDT decoder is clearly *possible*; its German-WER impact is just unmeasured.
- **Nemotron German FLEURS 8.31%** [6] is a single model-card number in a *streaming* config; not comparable to Parakeet's offline WER. The E2E benchmark [9] is a single secondary source.
- **`parakeet-mlx` / Apple-silicon quantization** was noted from search context only, not deeply verified here.

## Open questions
- ~~Is WhisperHeim already running Parakeet in BF16/FP16 or still FP32?~~ **Resolved:** WhisperHeim runs the **INT8** `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` build (full encoder+decoder+joiner INT8) via sherpa-onnx — see `ModelManagerService.cs:37-61`. It's already on the most aggressive size-optimized tier; the open question is whether INT8 costs German accuracy vs an FP16 build, not whether half precision is enabled.
- **What does INT8 ONNX do to *German* Parakeet WER on WhisperHeim's real audio?** No external source answers this — needs a local A/B test (FP16 ONNX vs the in-use INT8 ONNX) on representative German+English clips. The user's lived experience with the INT8 build is currently the only signal on this.
- **Does WhisperHeim need streaming/low-latency at all?** If yes, `nemotron-3.5-asr-streaming-0.6b` (German-capable, cache-aware) is the relevant new option; if it's batch transcription, Parakeet-TDT-v3 remains the better fit and Nemotron Speech is not an upgrade.
- **Is a TensorRT(-LLM) INT8/FP8 Parakeet engine worth building?** No public recipe/benchmark found; would require in-house engineering and validation.
- **License check:** Nemotron Speech uses **OpenMDW-1.1** (verify terms before commercial self-hosting), vs Parakeet/Canary CC-BY-4.0.
