---
topic: Best speech-to-text (STT/ASR) models for German and English in mid-2026, focused on self-hostable / open-weight options
date: 2026-06-28
requested_by: user
related_tasks: []
---

# Research: Best STT/ASR models for German + English (mid-2026)

## Question
The requester self-hosts a local transcription system (WhisperHeim) currently running **NVIDIA Parakeet** and wants to know whether better options exist for **German and English** in mid-2026. Focus is on **open-weight / locally-runnable** models, with cloud APIs covered for completeness and as trade-off points. Where models trade off, give a clear overview.

## Summary (decision-relevant first)
- **Parakeet is still a very strong baseline, but make sure you are on `parakeet-tdt-0.6b-v3` (Aug 2025), not the older English-only v2.** v3 added German plus 24 other European languages, keeps the extreme speed (RTFx ~1,700–3,300), and posts German WER in the same ballpark as Whisper large-v3 and Canary. If you are still on a v2/English-only Parakeet, upgrading to v3 is the single highest-value change for German [1][6][7].
- **For maximum German/English accuracy among open weights, the contenders are NVIDIA Canary-1B-v2 and Mistral Voxtral Small (24B).** On the Open ASR Leaderboard multilingual track, German WER was ~4.10% (Canary-1B-v2) and ~3.01% (Voxtral Small 24B) vs ~4.20% (Parakeet v3) and ~4.26% (Whisper large-v3) — Voxtral Small leads open models but is far heavier (24B) [3]. **WER comparisons here are only valid because they come from the same benchmark table; do not mix with other sources' numbers.**
- **The accuracy↔speed split is decoder architecture:** TDT/CTC decoders (Parakeet) give 10–100× throughput at a small accuracy cost; transformer/LLM decoders (Canary-Qwen, Voxtral, Whisper, Granite) give the best WER but are much slower [1][3]. Pick based on whether you are latency/throughput-bound or accuracy-bound.
- **Whisper large-v3 remains the multilingual "safe default"** (99 languages, MIT license, huge ecosystem: faster-whisper, whisper.cpp, WhisperX), but is no longer the accuracy leader and is known to hallucinate on silence/non-speech [2][8]. For English-only speed, `large-v3-turbo` and Distil-Whisper are strong; neither helps German (turbo is multilingual but lower quality; Distil-Whisper is English-only) [8].
- **Licensing caveat for self-hosting commercially:** Parakeet and Canary are **CC-BY-4.0** (permissive, commercial OK, attribution required); Whisper/Distil-Whisper are **MIT**; Voxtral is **Apache 2.0**. All four families are commercially usable — none of the leading current models are the old CC-BY-**NC** trap, but verify per model card [4][6][8].
- **Cloud APIs still beat open weights on German accuracy** (ElevenLabs Scribe v2 ~2.27% German FLEURS/CoVoST, AssemblyAI Universal-3 Pro ~2.34%), but you lose self-hosting/privacy/cost control — likely not aligned with WhisperHeim's local-first design [3][9].

## Findings

### 1. Where Parakeet stands today (the current baseline)
NVIDIA ships two relevant Parakeet generations, and the distinction is the whole story for German:
- **`parakeet-tdt-0.6b-v2`** — English-only. This is the famous "fastest on the Open ASR Leaderboard" model but does **not** do German [1].
- **`parakeet-tdt-0.6b-v3`** (released 2025-08-14) — 600M params, **TDT decoder**, now covers **25 European languages including German**, with automatic language detection, punctuation/casing, word/segment/char-level timestamps, chunked streaming, and long-form audio up to ~24 min. License **CC-BY-4.0**. Reported RTFx ~3,332 (model card) — extremely fast. German WER on its own card: **FLEURS-25 ~5.04%, CoVoST ~4.84%** [6][7].

So if WhisperHeim is on v3 already, you have a fast, German-capable, permissively-licensed model. If still on v2, that explains any German weakness and v3 is the obvious upgrade.

There is also a **Parakeet CTC 1.1B** variant (CTC decoder) optimized purely for throughput (RTFx >2,000–2,800) at a small accuracy cost; it ranks lower on accuracy leaderboards than the TDT models [1][8].

### 2. Direct German WER comparison (same benchmark — valid to compare)
The most authoritative cross-model German numbers come from the **Open ASR Leaderboard paper (arXiv 2510.06961)**, multilingual track (FLEURS + CoVoST-2). German (de) WER, lower is better [3]:

| Model | German WER | Notes |
|---|---|---|
| ElevenLabs Scribe v2 (cloud) | 2.27% | Closed-source leader |
| AssemblyAI Universal-3 Pro (cloud) | 2.34% | Closed |
| **Mistral Voxtral Small 24B (open)** | **3.01%** | Best **open** model for German here; heavy (24B) |
| Cohere Labs Transcribe | 3.84% | |
| Microsoft Phi-4 Multimodal | 3.96% | Open-ish multimodal LLM |
| **NVIDIA Canary-1B-v2 (open)** | **4.10%** | ~978M params, CC-BY-4.0 |
| **NVIDIA Parakeet TDT 0.6B v3 (open)** | **4.20%** | Fastest of this group |
| **OpenAI Whisper Large v3 (open)** | **4.26%** | MIT, 99 languages |

Key reading: among open weights, **Voxtral Small (24B) > Canary-1B-v2 ≈ Parakeet v3 ≈ Whisper large-v3** for German, but the gap between Canary/Parakeet/Whisper is small (~0.1 WER points) while Voxtral's lead (~1 point) comes at a much larger model size [3].

**Cross-check / discrepancy flag:** Parakeet v3's *own model card* reports German FLEURS-25 5.04% / CoVoST 4.84% [6], while the leaderboard paper lists 4.20% for German [3]. These differ because of aggregation/normalization differences (which subsets, text normalization, single-language vs averaged). **Treat the *relative ranking within one source* as reliable, not the absolute decimals across sources.** Canary's own card likewise reports only aggregate Fleurs-25 8.40% / CoVoST-13 8.85% without a per-German breakdown [4].

### 3. The open-weight contenders in detail

**NVIDIA Canary-1B-v2** (2025-08-14) — ~978M params (32 enc / 8 dec layers), Conformer encoder + transformer decoder. 25 EU languages incl. German, ASR **and** EN↔X translation. Word + segment timestamps. **CC-BY-4.0.** RTFx ~749 (much slower than Parakeet but still fast; transformer decoder is the cost). No documented native streaming (batch + dynamic chunking). No built-in diarization. Best when you want the accuracy of a transformer decoder but a model far smaller than Voxtral [4][3].

**NVIDIA Canary-Qwen-2.5B** — tops the Open ASR Leaderboard **English short-form** at ~5.63% WER, RTFx ~418. A Speech-Augmented LLM (SALM). But **English-only** and needs chunked inference for clips >~10s — not a German option [1][8].

**Mistral Voxtral** (2025-07-15, **Apache 2.0**) — Small (24B) for servers, Mini (3B) for edge, plus Transcribe API variants. Best open German WER in the leaderboard table (3.01% Small) [3]. Also adds Q&A/summarization/function-calling and **native streaming** ("Voxtral Realtime", 80ms–2.4s configurable delay) — Whisper has no native streaming. Downsides: heavier GPU needs (Small ~24B; Realtime ~16GB BF16, ~2.5GB at Q4), narrower language set (~8–13 well-supported languages vs Whisper's 99), and as of mid-2026 it **lacks word-level timestamps and diarization** (vendor says "coming soon") [5][10]. Note Mistral's own "outperforms Whisper" claims are **vendor marketing** [5].

**OpenAI Whisper large-v3** (MIT, 1.55B, ~10GB VRAM / ~4GB quantized) — still the **multilingual baseline and best open long-form model**, 99 languages, mature tooling. Good but no longer top German accuracy. Known to **hallucinate text on silence/non-speech segments** — a well-documented Whisper failure mode; mitigate with VAD (e.g. WhisperX/faster-whisper VAD filtering) [2][8][10].
- **large-v3-turbo** — 809M, ~6GB VRAM, RTFx ~216, ~6× faster than large-v3, multilingual, small accuracy drop (~7.75% vs 7.4% on the source's mixed English benchmark). Decent German fallback when speed matters [8].
- **Distil-Whisper large-v3** — 756M, within ~1% of large-v3 on English, ~5–6× faster, but **English-only** — not a German option [8].
- **Ecosystem note:** faster-whisper (CTranslate2), whisper.cpp (runs on CPU / 8GB laptops), and WhisperX (adds VAD, word-level timestamps, and diarization via pyannote) are the usual self-hosting wrappers; WhisperX is how most people get word-timestamps + diarization onto Whisper [8][10].

**IBM Granite-Speech-3.3-8B** — ~9B params, Apache 2.0, ~5.85% English WER (Open ASR Leaderboard), optimized for EN/FR/DE/ES ASR + translation. High VRAM, slower. A credible German option but heavy and less benchmarked for German specifically than Canary/Voxtral [1][8].

**Microsoft Phi-4 Multimodal** — strong multilingual (German 3.96% in the leaderboard table), but it is a general multimodal LLM, heavier to serve than a dedicated ASR model [3].

**Smaller/edge:** **Moonshine** (~27M params) targets mobile/embedded on-device English — relevant only if you need a tiny CPU/edge model, not for best German quality [8].

### 4. Trade-off cheat-sheet

- **Accuracy vs speed (decoder type):** CTC (fastest, RTFx 2,700+) < TDT (Parakeet, RTFx ~1,700–3,300, tiny accuracy cost) << transformer/LLM decoders (Canary, Voxtral, Whisper, Granite — best WER, RTFx ~40–750). For high-volume batch transcription, Parakeet TDT wins; for best-quality offline German, Canary/Voxtral win [1][3].
- **Model size / VRAM:** Parakeet 0.6B (~4GB) and Whisper turbo (~6GB) are laptop/single-GPU friendly; Whisper large-v3 ~10GB; Canary ~1B; Voxtral Small 24B and Granite 8–9B need serious GPUs. whisper.cpp / quantized variants enable CPU-only or 8GB machines at quality cost [5][8][10].
- **Language coverage incl. German:** English-only = Parakeet v2, Canary-Qwen, Distil-Whisper. German-capable open weights = Parakeet **v3**, Canary-1B-v2, Voxtral, Whisper large-v3/turbo, Granite, Phi-4. Whisper has by far the widest coverage (99) if you need languages beyond DE/EN [1][6][8].
- **Long-form / timestamps / diarization:** Parakeet v3 and Canary give word+segment timestamps natively. Whisper gives phrase-level; word-level + diarization typically via **WhisperX**. Voxtral currently lacks word-timestamps/diarization. **No open ASR model ships strong diarization built-in** — diarization is usually a separate stage (pyannote) [4][6][8][5].
- **Licensing:** MIT (Whisper, Distil-Whisper) and Apache 2.0 (Voxtral, Granite) are the most permissive; **CC-BY-4.0** (Parakeet, Canary) is commercial-OK but requires attribution. Verify each card; none of the *current leaders* are CC-BY-NC, but older NeMo models can be [4][6][8][5].
- **Streaming vs batch:** Native streaming → Voxtral Realtime, Parakeet (chunked). Whisper is batch-only and needs external VAD wrappers for pseudo-streaming [5][10].
- **Hallucination/robustness:** Whisper hallucinates on silence/music (mitigate with VAD); CTC/TDT models (Parakeet) are generally more robust to this but can be weaker on punctuation/rare words. Cloud streaming models lose 1–3 WER points and drop on alphanumeric/entity accuracy in production [2][9][10].

### 5. Cloud API options (for completeness — not local)
If privacy/self-hosting were relaxed, cloud leads on German accuracy: **ElevenLabs Scribe v2** (~2.27% German), **AssemblyAI Universal-3 Pro** (~2.34%, built-in diarization), **OpenAI gpt-4o-transcribe** (2–5% English, ~150ms realtime), **Deepgram Nova-3/Flux** (cheap, low-latency streaming), **Google Chirp 3** (125+ languages), **Microsoft MAI-Transcribe-1** (25 languages, ~3.8% FLEURS) [3][9]. Coval (independent, Jun 2026) cautions that vendor benchmarks are "marketing copy with measurements attached" and that clean-English WER has plateaued within 1–2 points across top providers [9]. For a local-first system like WhisperHeim these are mainly a quality ceiling reference and a fallback, not a primary path.

### Recommendation framing (decision left to you)
- **Optimize for throughput / many hours of audio / lowest VRAM, German+English good-enough:** stay on **Parakeet TDT 0.6B v3** (confirm you're on v3, not v2) [6][3].
- **Optimize for best open German/English accuracy, have a beefy GPU:** evaluate **Mistral Voxtral Small 24B** (best open German WER, native streaming, Apache 2.0) or, for a much smaller footprint, **Canary-1B-v2** (near-Voxtral accuracy at ~1B, CC-BY-4.0) [3][4][5].
- **Optimize for ecosystem maturity / widest languages / CPU fallback / easy diarization+word-timestamps:** **Whisper large-v3 + WhisperX** (add VAD to kill hallucinations); use **large-v3-turbo** when you need more speed [2][8][10].
- **A/B test on YOUR audio.** All cross-model WER gaps here are small and benchmark-specific; the only reliable comparison is running 2–3 candidates (Parakeet v3, Canary-1B-v2, Voxtral) on a representative German+English sample from WhisperHeim's real traffic.

## Sources
1. [Open ASR Leaderboard: Trends and Insights (HF blog)](https://huggingface.co/blog/open-asr-leaderboard) — official HF leaderboard analysis; decoder trade-offs, track leaders. Published 2025-11-21.
2. [Open ASR Leaderboard (HF Space)](https://huggingface.co/spaces/hf-audio/open_asr_leaderboard) — live English/multilingual/long-form rankings. Continuously updated.
3. [Open ASR Leaderboard paper, arXiv 2510.06961v4](https://arxiv.org/html/2510.06961v4) — primary source for per-language German WER (FLEURS/CoVoST-2 multilingual table) and RTFx-by-decoder. 2025–2026.
4. [nvidia/canary-1b-v2 model card (Hugging Face)](https://huggingface.co/nvidia/canary-1b-v2) — Canary languages incl. German, params, CC-BY-4.0, timestamps, RTFx. Released 2025-08-14.
5. [Voxtral announcement, Mistral AI](https://mistral.ai/news/voxtral) — VENDOR source; Voxtral variants, Apache 2.0, streaming, claims vs Whisper. 2025-07-15.
6. [nvidia/parakeet-tdt-0.6b-v3 model card (Hugging Face)](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3) — v3 German support, German FLEURS/CoVoST WER, RTFx, timestamps, license. Released 2025-08-14.
7. [NVIDIA: Open Dataset & Models for Multilingual Speech AI (blog)](https://blogs.nvidia.com/blog/speech-ai-dataset-models/) — Granary dataset, Parakeet v3 / Canary v2 25-language launch context. 2025.
8. [Northflank: Best open-source STT model in 2026 (benchmarks)](https://northflank.com/blog/best-open-source-speech-to-text-stt-model-in-2026-benchmarks) — secondary roundup; WER/RTFx/VRAM/license per model. 2026.
9. [Coval: Best STT providers 2026 (independent benchmarks)](https://www.coval.ai/blog/best-speech-to-text-providers-in-2026-independent-benchmarks-and-how-to-choose/) — independent cloud-API comparison + caveats. Published 2026-06-04.
10. [Weesper: Voxtral vs Whisper 2026 (WER, streaming, hardware)](https://weesperneonflow.ai/en/blog/2026-03-31-voxtral-whisper-open-source-speech-models-comparison-2026/) — secondary; mixes vendor + Artificial Analysis numbers, VRAM/streaming detail. 2026-03-31.

## Unverified / single-source claims (flagged)
- **Parakeet v3 German absolute WER** differs between its own model card (FLEURS 5.04% / CoVoST 4.84%, [6]) and the leaderboard paper (4.20%, [3]) due to aggregation/normalization. Rankings are consistent; absolute decimals are not directly comparable across these sources.
- **Voxtral Small German 3.01%** and the full German column come from a single table in [3]; corroborated directionally (Voxtral > Whisper) by [5][10] but those are vendor/secondary.
- VRAM/quantized-size figures in [8][10] are secondary-source estimates, not official model-card specs.
- Cloud-provider WER/latency/pricing in [9] is independent analysis but a single source; numbers shift frequently.

## Open questions
- **No clean, independent, German-only WER benchmark** (e.g. Common Voice DE vs FLEURS DE separately) was found that compares all open models head-to-head with identical text normalization. The arXiv table [3] is the closest but aggregates FLEURS+CoVoST. A local A/B test on WhisperHeim's own German audio remains the only fully trustworthy comparison.
- **Real-world hallucination rates** (Whisper on silence) are well-documented qualitatively but not quantified per-model in these sources.
- **Voxtral word-level timestamps / diarization** were "coming soon" as of mid-2025; whether they shipped by mid-2026 was not confirmed in fetched sources — verify the current Voxtral model card before relying on it for timestamped output.
- Confirm **which exact Parakeet build WhisperHeim currently runs** (v2 English-only vs v3 multilingual) — this determines whether any change is even needed for German.
