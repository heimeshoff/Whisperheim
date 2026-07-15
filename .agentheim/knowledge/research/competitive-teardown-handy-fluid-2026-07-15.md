---
topic: Competitive teardown — WhisperHeim vs Handy vs Fluid (Windows/desktop dictation tools)
date: 2026-07-15
requested_by: user
related_tasks: []
---

# Research: Competitive teardown — WhisperHeim vs Handy vs Fluid

## Question
Across the four standard axes (latency & model, features & workflows, UX & polish, cost/privacy/platform), what do the open-source dictation apps **Handy** (github.com/cjpais/Handy) and **Fluid/FluidVoice** (altic.dev/fluid) do that WhisperHeim doesn't — and, given WhisperHeim's fixed identity (local-first, free, private, Windows-only, DE+EN), which of those gaps are actually worth considering versus poor fits or not applicable? WhisperHeim itself was not re-researched; its baseline was supplied and is treated as ground truth.

## Summary
- **Fluid is not actually a Windows product yet.** It is a shipped, mature macOS app (v1.6.4, requires macOS 15 Sequoia+) with a Windows build still in early beta (v0.0.1–v0.0.5, all dated 08–14 Jul 2026, waitlist-gated) [7][9]. Any Windows-specific claims about Fluid are about a days-old beta, not a released product — treat with caution.
- **Both competitors are genuinely open source with permissive-to-copyleft licenses**: Handy is MIT [1][3]. Fluid is GPLv3 as of 2026-02-23 (Apache 2.0 before that date) [7]. WhisperHeim's own licensing is explicitly out of scope per the brief.
- **Handy is the closer analog to WhisperHeim**: single-purpose, local-only, cross-platform (Win/Mac/Linux), no cloud, no telemetry, MIT-licensed, and it already ships NVIDIA Parakeet TDT v3 (the same 25-language, German-inclusive model family WhisperHeim uses) alongside a full Whisper model lineup users can pick from [1][3][4].
- **Fluid's headline differentiator is post-processing, not transcription**: "Fluid-1," an optional ~3.5GB local LLM-style model that rewrites/reformats dictated text per active application ("tone adaptation"), plus a voice **Command Mode** that controls the OS (launch apps, run shortcuts, trigger system actions) [6][7][8]. The latter directly overlaps with a capability WhisperHeim explicitly excludes as a non-goal.
- **Neither competitor documents call transcription or voice-message-file transcription** — both are WhisperHeim differentiators with no counterpart found in either product's docs [1][3][6][7][8].
- **Neither competitor documents a templates/snippets system** the way WhisperHeim does (voice-triggered snippet insertion) — this appears to be a genuine WhisperHeim-only feature among the three [1][3][6][7][8].
- **Model choice is the biggest structural difference**: Handy lets the user pick from ~10 different ASR models (Whisper variants, Parakeet v2/v3, Canary, Moonshine, GigaAM, SenseVoice, Breeze) with GPU acceleration for Whisper [1][4]; Fluid offers a similarly wide model menu (Nemotron Speech 3.5, Parakeet Flash/TDT v2/v3, Cohere Transcribe, Apple Speech, Whisper) [6][7]. WhisperHeim ships one curated model (Parakeet TDT v3 INT8) — simpler but less user-configurable.

## Findings

### 1. Identity check — is Fluid actually comparable on "Windows/desktop"?
The product page and GitHub repo both currently describe FluidVoice as "the fastest and only **macOS** Dictation app with on-device STT," with "Windows & iOS waitlist open. Linux soon." [6][7]. The GitHub releases page shows a shipped, numbered macOS track (currently v1.6.4, 14 Jul 2026) and a separate, much younger Windows pre-release track (v0.0.1 through v0.0.5, all dated 08–14 Jul 2026 — i.e., released in the same week as this report) [9]. The Windows betas are gated behind a waitlist (altic.dev/fluid/waitlist) [10] and their release notes focus narrowly on NVIDIA CUDA compatibility and GPU-decoding performance [9], with no confirmation that the full macOS feature set (Command Mode, tone adaptation, Fluid-1) is present on Windows yet. **Conclusion: Fluid's Windows story should be read as "in active, very early beta," not as a shipping competitor on WhisperHeim's actual platform.** The axis comparisons below describe Fluid's documented (mostly macOS) feature set, which is the closest available signal for what a future Windows release would offer.

### 2. Latency & model

| | WhisperHeim (baseline) | Handy | Fluid |
|---|---|---|---|
| Architecture | VAD-segmented (Silero VAD), not word-by-word streaming; target text-appears <2s, ideally <1s | Push-to-talk / toggle, batch: record → release → transcribe (no streaming) [1][3] | Claims "<100ms perceived latency" and "up to 3,380x real-time factor" on its fastest model (Parakeet Flash), architecture described as multi-model routing between short bursts and long dictation — closer to streaming-feel than Handy [6] |
| ASR engine(s) | NVIDIA Parakeet TDT 0.6B v3, INT8 ONNX (csukuangfj build), via sherpa-onnx | User-selectable: Whisper (Small/Medium/Turbo/Large), Parakeet V2/V3, Canary 180M Flash / 1B v2, Moonshine (Tiny/Base/Small/Medium), GigaAM v3, SenseVoice, Breeze ASR — ~10 models total [1][4] | User-selectable: Nemotron Speech 3.5 (+ multilingual variant), Parakeet Flash (beta), Parakeet TDT v2/v3, Cohere Transcribe, Apple Speech, Whisper (Tiny–Large) [6][7] |
| On-device vs cloud | 100% on-device, no GPU required | 100% on-device for transcription; optional cloud "post-process" toggle exists in the CLI (`--toggle-post-process`) but its backend is not documented in the sources reviewed — unknown/not publicly documented whether this calls a cloud LLM [1] | Core dictation is on-device; **optional** AI-enhancement can route to user-configured cloud providers (OpenAI, Groq, custom) — explicitly opt-in, not required [6] |
| GPU / VRAM / footprint | CPU-only design, target <2GB RAM idle / <3GB active | Whisper models get "GPU acceleration when available" (NVIDIA/AMD/Intel on Win/Linux); Parakeet models are CPU-only, "~5x real-time on mid-range i5," minimum Intel Skylake (6th gen) or equivalent AMD. No explicit VRAM figures published [1][3] | Fluid-1 (optional enhancement model) needs ~3.5GB disk; base voice models ~1GB disk. Windows beta release notes specifically call out NVIDIA CUDA/GPU-decoding optimization, suggesting the Windows build may lean on GPU more than Handy or WhisperHeim — not confirmed as a hard requirement [6][9]. No VRAM figures published — unknown/not publicly documented |
| Languages (DE/EN) | German + English, 25 European languages via Parakeet v3 | German supported via Parakeet V3 (25 languages) [4], Whisper (99+ languages) [4], Canary 1B v2 (25 languages) [4], Canary 180M Flash (4 languages incl. German) [4]. Not restated by name in top-level marketing copy — confirmed only in the models doc | German explicitly listed under Parakeet TDT v3's 25-language set on the product/README model table [7]. Nemotron Speech 3.5 covers ~40 languages (German presumed included but not itemized in sources reviewed — unknown which specific languages) [6][7] |

### 3. Features & workflows

| | WhisperHeim (baseline) | Handy | Fluid |
|---|---|---|---|
| Live dictation | Ctrl+Win hotkey, SendInput text insertion (not clipboard) | Configurable global hotkey, push-to-talk (default) or toggle mode; direct paste into active field, falls back to clipboard on restricted systems (notably Wayland on Linux) [1][3] | Global hotkey (Option/⌥ key referenced in one source), "Direct Dictation" mode with live-preview overlay; "Smart Typing" — direct insertion via accessibility APIs [6][8] |
| Templates / snippets | Yes — dedicated hotkey + voice trigger word, "no match → create template" modal | Not documented — no mention found in README, docs site, or product site [1][3][4] | Not documented as a snippets feature; closest analog is Fluid-1's per-app "tone adaptation" (rewriting style, not inserting fixed snippets) [6][8] |
| Call transcription | Yes — WASAPI loopback + mic dual capture, diarization (sherpa-onnx + pyannote ONNX), exportable transcript | Not documented — no feature of this kind found | Not documented — no feature of this kind found |
| Voice-message transcription | Yes — drag-and-drop audio files (OGG/MP3/M4A/WAV + FFmpeg fallback) onto tray/window | Not documented | Not documented |
| Output formatting | Plain/Markdown/JSON export (call transcripts) | None documented beyond raw transcript insertion | **Fluid-1**: optional local model doing "smart formatting, context-aware capitalization, casing fixes, dates/names/numbers," described as rewriting text per-app tone/context [6][7][8] |
| Extra workflow modes | n/a (dictation + templates + calls + files) | Push-to-talk vs toggle; Raycast integration (macOS) [1] | Three distinct modes: **Write Mode** (write/rewrite in any field), **Command Mode** (voice-driven OS control — launch apps, run shortcuts, trigger system actions), **Direct Dictation** [8] |
| Hotkey design | Ctrl+Win (dictate), Alt+Win (templates) | Fully configurable global shortcut; separate CLI/signal-based remote control (`--toggle-transcription`, `--cancel`, SIGUSR1/2 on Wayland) [1][3] | "Global hotkey" for capture; specific bindings not detailed in sources reviewed |
| Insertion strategy | Windows SendInput (deliberately avoids clobbering clipboard) | Primarily direct paste into focused field; clipboard-based fallback where OS restricts synthetic input (Linux/Wayland) [1][3] | Accessibility-API-based direct insertion per marketing copy; exact mechanism (SendInput-equivalent vs clipboard) not documented [8] |

### 4. UX & polish

| | WhisperHeim (baseline) | Handy | Fluid |
|---|---|---|---|
| Onboarding / first run | Auto model download (~600MB Parakeet) on first run, goal: ready within 5 minutes | Grant mic/accessibility permissions, then configure shortcut in Settings; models auto-download, with a manual-install path documented for users behind restrictive proxies [1][3]. No structured onboarding wizard documented. | Not documented in sources reviewed beyond "download and verify" prompts for model updates — unknown/not publicly documented |
| On-screen indicator | "Pill/Overlay" showing mic state + live waveform | Recording overlay / "transcription icon" that lights up while active; overlay is configurable and can be turned off entirely (Settings → Advanced → Overlay Position: None); disabled by default on Linux due to a known paste-interference bug on X11 [1][3][5] | Live-preview overlay with "notch support" mentioned for Direct Dictation mode; no further design detail documented [8] |
| Settings surface | Full settings app (WPF UI) | Described as "really simple" — push-to-talk vs toggle, key rebinding; React/Tailwind settings UI; a settings "refactor" is called out as in-progress in the repo [1][3] | Per-application settings/customization (tone profiles); no screenshots or structure documented in sources reviewed |
| Tray behavior | System tray app, PowerToys-style Fluent UI (WPF UI, Mica) | System tray icon by default, can be suppressed via `--no-tray` CLI flag [1] | Not documented (macOS uses a menu-bar-style presence typical of the category, but this is inferred from category convention, not confirmed in sources — flagged as unconfirmed) |
| Debug/dev affordances | n/a documented | Debug mode via keyboard shortcut (Cmd/Ctrl+Shift+D), CLI flags (`--start-hidden`, `--debug`, `--help`) [1][3] | Not documented |
| Star count / project maturity | n/a | ~23,000 GitHub stars per one review source [2] (not independently confirmed via GitHub API in this pass — treat as approximate, single-source-adjacent) | ~8,000 GitHub stars per README badge as rendered [7] |

### 5. Cost, privacy, platform

| | WhisperHeim (baseline) | Handy | Fluid |
|---|---|---|---|
| Pricing | Free, no subscription, no API keys | Free, no paid tier, no subscription, no word limits, no account system [1][3] | "Free and open source... forever; no subscription tiers." Optional GitHub Sponsors donation channel [6][7] |
| Cloud dependency | None at runtime | None for core transcription; initial model download and optional update checks are the only network calls documented [1][3] | Core dictation is fully local; **optional** cloud AI-enhancement providers (OpenAI/Groq/custom) are opt-in and user-configured, not required [6] |
| OS support | Windows 11 only (explicit non-goal to expand) | Windows (x64), macOS (Intel + Apple Silicon), Linux (x64, Ubuntu 22.04/24.04) [1][3] | macOS 15+ shipped today; Windows in early/waitlist-gated beta (v0.0.x, this week); iOS waitlisted; Linux "soon" [6][7][9][10] |
| Licensing / open-source status | Out of scope (internal product) | **MIT License** for the app code; app name/logo/brand assets are explicitly *not* open source, and unofficial forks may not imply endorsement [1][3] | **GPLv3** as of 2026-02-23; releases before that date were Apache 2.0 [7] |
| Telemetry | Not documented as a concern (n/a) | Explicitly "no telemetry pipeline tied to your voice" [1][2] | Not explicitly addressed in sources reviewed beyond "no data leaves your Mac" for on-device operations — unknown/not publicly documented for the optional cloud path |

## WhisperHeim gaps
Concrete things Handy and/or Fluid do that WhisperHeim currently doesn't, each tagged for identity fit. These are candidates only — no backlog tasks were created or referenced.

1. **User-selectable ASR model menu** (Handy: ~10 models incl. Whisper variants with GPU accel, Canary, Moonshine, GigaAM, SenseVoice, Breeze; Fluid: Nemotron 3.5, multiple Parakeet variants, Cohere, Apple Speech, Whisper) [1][4][6][7]. — *Aligned with local/free/private/Windows identity — worth considering*, at least as an optional "advanced" model picker for power users or non-DE/EN languages, without disturbing the single-curated-model default that keeps onboarding simple.
2. **Optional GPU acceleration path for Whisper-family models** (Handy) [1][3], and Windows-beta CUDA-specific decode optimization (Fluid) [9]. — *Aligned with local/free/private identity, poor fit if it complicates the CPU-only promise* — worth considering only as a strictly optional accelerator, not a requirement, since WhisperHeim's baseline explicitly targets no-dedicated-GPU operation.
3. **Local post-processing/rewrite layer (Fluid-1)** — smart formatting, context-aware capitalization, per-app tone adaptation [6][7][8]. — *Aligned with local-first/private identity if kept fully local (Fluid-1 itself is local); worth considering* as a scoped "clean up my dictation" feature, but note Fluid's model is a heavyweight ~3.5GB optional download, so it would need right-sizing.
4. **Voice-driven OS Command Mode** (Fluid: launch apps, run shortcuts, trigger system actions) [8]. — *Poor fit — directly conflicts with WhisperHeim's documented non-goal of "no voice-command/app-launching assistant features."* Flagged explicitly as out of scope by the existing brief, not a gap to close.
5. **Cross-platform coverage (Mac/Linux)** (Handy: Win/Mac/Linux; Fluid: Mac shipped, Win/Linux/iOS in progress) [1][3][6][7]. — *Not applicable* — WhisperHeim is explicitly Windows-11-only by design; this is a non-goal, not a gap.
6. **CLI/signal-based remote control of dictation** (Handy: `--toggle-transcription`, `--cancel`, `--start-hidden`, Wayland SIGUSR1/2 signals) [1][3]. — *Aligned with local/free/private/Windows identity — worth considering*, since WhisperHeim already ships a local HTTP API and CLI wrapper for transcription; a similar scriptable start/stop/cancel surface for live dictation (not just file transcription) could be a natural, low-risk extension of an existing pattern.
7. **Manual/offline model installation path for restricted networks** (Handy) [1][3]. — *Aligned with local/private identity — worth considering* for users on locked-down corporate networks who can't reach the model host at first run.
8. **Configurable/removable on-screen overlay, including a documented "None" option** (Handy) [1][3]. — *Aligned with UX polish — worth considering* as a settings toggle, since WhisperHeim's Pill/Overlay is currently described as always-present; giving users the option to disable it is a low-cost parity item.
9. **Push-to-talk as an explicit alternative dictation mode** (Handy: push-to-talk default vs toggle) [1][3]. — *Aligned with UX polish and Windows-native feel — worth considering* as an alternative to WhisperHeim's current toggle-hotkey (Ctrl+Win) model, for users who prefer hold-to-talk.
10. **Custom/BYO model support** (Handy: "Custom Whisper GGML models supported via auto-discovery") [1]. — *Aligned with local/free/private identity — worth considering* for advanced users, but adds surface area/support burden; likely lower priority than a curated model picker (gap #1).
11. **Explicit "brand assets are not open source" carve-out despite MIT code license** (Handy) [1][3]. — *Not applicable to feature parity* — this is a licensing/branding practice note, relevant only if WhisperHeim's own licensing posture is revisited (out of scope per this report's brief).

Notable non-gaps (confirmed absent in both competitors, so not areas where WhisperHeim is behind): call transcription with diarization, voice-message/audio-file transcription, and voice-triggered template/snippet insertion were not found documented in either Handy's or Fluid's public sources [1][3][4][6][7][8]. These remain WhisperHeim differentiators rather than gaps.

## Sources
1. [GitHub — cjpais/Handy](https://github.com/cjpais/Handy) — primary repo: README, license (MIT), architecture (Tauri/Rust/React), model list, insertion mechanism, CLI flags. Retrieved 2026-07-15.
2. [Spokenly — Handy Dictation Review 2026](https://spokenly.app/blog/handy-review) — third-party review; source of the ~23,000-star figure and "no telemetry pipeline" phrasing (marketing-adjacent third-party source, flagged as such). Retrieved 2026-07-15.
3. [Handy — handy.computer](https://handy.computer) — product site: onboarding framing, overlay description, settings simplicity, pricing statement ("Accessibility tooling belongs in everyone's hands, not behind a paywall"). Retrieved 2026-07-15.
4. [Handy Docs — Models](https://handy.computer/docs/models) — full model table with sizes and per-model language coverage, including explicit German confirmation for Parakeet V3, Whisper, Canary 180M Flash, and Canary 1B v2. Retrieved 2026-07-15.
5. Handy README (as fetched via GitHub) — overlay-disabled-by-default-on-Linux detail and X11 paste-interference note. Retrieved 2026-07-15.
6. [FluidVoice — GitHub repo (altic-dev/FluidVoice)](https://github.com/altic-dev/FluidVoice) — repo description ("Fastest and only macOS Dictation app..."), platform status line ("Windows & iOS waitlist open. Linux soon."), model list. Retrieved 2026-07-15.
7. [FluidVoice README](https://github.com/altic-dev/FluidVoice/blob/main/README.md) — license history (Apache 2.0 → GPLv3 on 2026-02-23), full model/language/size table, system requirements, star count. Retrieved 2026-07-15.
8. [altic.dev/fluid — FluidVoice product page](https://altic.dev/fluid) — feature framing: Write Mode, Command Mode, Direct Dictation, Fluid Intelligence/Fluid-1 description, Smart Typing insertion claim. Retrieved 2026-07-15.
9. [FluidVoice Releases — GitHub](https://github.com/altic-dev/FluidVoice/releases) — release history showing macOS track at v1.6.4 (14 Jul 2026) vs. Windows beta track v0.0.1–v0.0.5 (08–14 Jul 2026), with Windows release notes focused on CUDA/GPU-decode compatibility. Retrieved 2026-07-15.
10. [FluidVoice iOS & Windows Waitlist](https://altic.dev/fluid/waitlist) — confirms Windows access is currently waitlist-gated. Retrieved 2026-07-15.

## Open questions
- **Fluid's Windows feature parity is unknown.** The Windows beta (v0.0.1–v0.0.5) release notes only discuss CUDA/GPU-decode changes; it is not documented whether Command Mode, tone adaptation, or Fluid-1 are present, partial, or absent on Windows. This should be re-checked once Fluid's Windows build leaves beta.
- **Handy's `--toggle-post-process` CLI flag implies some kind of post-processing step**, but its backend (local vs cloud, what model) is not documented in the sources reviewed — worth a follow-up look at Handy's settings docs or source if this becomes decision-relevant.
- **Neither Handy nor Fluid publishes explicit VRAM requirements** for their GPU-accelerated paths (Handy's Whisper+GPU mode, Fluid's Windows CUDA path) — only qualitative "GPU acceleration when available" / "NVIDIA CUDA compatibility" language was found.
- **Star counts (Handy ~23k via a third-party review, Fluid ~8k via README badge)** were not independently re-verified against the live GitHub API in this pass and should be treated as approximate/point-in-time.
- **German-specific WER/accuracy numbers** for either competitor's models were not part of this teardown's scope (covered instead in the existing report `best-stt-models-german-english-2026-06-28.md`); this report only confirms *language coverage*, not comparative accuracy.
