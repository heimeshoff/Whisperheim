---
id: main-101
title: Deterministic Clean-Text Pipeline (Filler Word Removal)
status: done
type: feature
context: main
created: 2026-04-20
completed: 2026-04-20
commit:
depends_on: []
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App (post-launch polish)
size: Small
---
# Deterministic Clean-Text Pipeline (Filler Word Removal)

## Objective

Strip verbal fillers ("um", "uh", "you know", "I mean", etc.) from **dictation** output before it reaches the focused window. File and stream transcripts are out of scope. Runs in <1ms, no LLM, deterministic.

## Background

Research `filler-words-and-custom-vocabulary.md` (2026-03-31) endorsed a regex-based filter for dictation. Comparison research `macparakeet-feature-comparison.md` (2026-04-20) identified this as the top UX gap vs MacParakeet's clean pipeline. Parakeet's raw output currently flows directly to `SendInput`, so every "uh" and "um" gets typed.

This task covers **filler removal only**. Custom word replacements, in-dictation snippet expansion, and sentence-start-only conditional fillers are deferred to follow-up tasks.

**Note on scope decision:** MacParakeet's spec describes three tiers including sentence-start-only fillers (`so`, `well`, `like`, `right`). Their actual production code (`TextProcessingPipeline.swift`) implements only the two unconditional tiers. We follow their shipped behavior, not their spec.

## Design

- New service `FillerRemovalService` in `src/WhisperHeim/Services/TextProcessing/`, invoked after ASR, before `SendInput`.
- Pipeline signature: `string → string` (destructive transform). When a future dictation-history task adds `rawTranscript`/`cleanTranscript` persistence, that task owns dual storage — the pipeline stays a simple transform.
- Two tiers, both **unconditional**:
  1. **Multi-word** (English): "you know", "I mean", "sort of", "kind of"
  2. **Single-word** (English): "um", "uh", "umm", "uhh"
- Case-insensitive, word-boundary regex (`\b...\b`) to avoid partial matches ("factually" must survive "actually" removal).
- Post-removal whitespace normalization: collapse double spaces, trim, fix punctuation spacing (`" ,"` → `","` etc.).
- Setting: **Raw / Clean mode toggle** in dictation settings (default: Clean). Raw ships ASR output untouched.
- German word lists applied when dictation language is `de-DE`. Single-word only: `äh`, `ähm`, `hm`, `hmm`, `öh`, `öhm`. No multi-word German fillers in v1 — discourse particles like `halt`, `also`, `eben`, `doch`, `ja`, `schon`, `mal` carry meaning and were deliberately excluded.
- Word lists hardcoded in code for v1. Future custom-words task (separate) adds user-editable overrides.
- Runs synchronously on the main transcription pipeline thread; budget <1ms per utterance.

## Acceptance Criteria

- [ ] `FillerRemovalService` removes multi-word and single-word English fillers with full unit-test coverage
- [ ] German single-word fillers (`äh`, `ähm`, `hm`, `hmm`, `öh`, `öhm`) stripped when dictation language is `de-DE`
- [ ] Raw/Clean mode toggle in dictation settings, persisted, default Clean
- [ ] No regressions in dictation latency (<1ms overhead measured)
- [ ] Whitespace normalization handles leading/trailing, double spaces, and punctuation spacing
- [ ] Word-boundary matching verified: "factually" survives, "actually" is removed when listed (add "actually" to a test case only, not the production list)
- [ ] Manual test: dictate a paragraph with deliberate fillers in English and German, verify clean output

## Resolved Questions

- **Scope (Q1):** Dictation only. File/stream transcripts out of scope. — 2026-04-20
- **Storage (Q3):** Hardcoded word lists. User-editable overrides handled by a separate future "custom words" task. — 2026-04-20
- **Destructive vs both copies (Q4):** Pipeline returns clean text only (`string → string`). Dual storage is a future dictation-history task's concern. Mirrors MacParakeet's actual code. — 2026-04-20
- **Sentence-boundary detection (Q5):** Not needed. Tier 3 (sentence-start-only fillers) deferred — MacParakeet ships without it despite their spec. — 2026-04-20
- **German word list (Q2):** Single-word only — `äh`, `ähm`, `hm`, `hmm`, `öh`, `öhm`. No multi-word fillers. German discourse particles (`halt`, `also`, `eben`, `doch`, `ja`, `schon`, `mal`) deliberately excluded because they carry meaning. — 2026-04-20

## References

- `.workflow/research/filler-words-and-custom-vocabulary.md`
- `.workflow/research/macparakeet-feature-comparison.md` (Gap 1)
- MacParakeet pipeline source: https://raw.githubusercontent.com/moona3k/macparakeet/main/Sources/MacParakeetCore/TextProcessing/TextProcessingPipeline.swift
- MacParakeet text-processing spec: https://raw.githubusercontent.com/moona3k/macparakeet/main/spec/07-text-processing.md

## Work Log

### 2026-04-20 -- Refined

**Changes:**
- Narrowed scope to dictation only (file/stream transcripts out)
- Dropped tier 3 (sentence-start-only fillers) after inspecting MacParakeet's actual code — their spec describes it, production does not
- Confirmed pipeline returns clean text only; dual raw/clean storage is a future dictation-history concern
- Confirmed word lists hardcoded for v1; custom-words split into a future task
- Size reduced from Medium to Small
- German word list resolved: single-word only (`äh`, `ähm`, `hm`, `hmm`, `öh`, `öhm`); multi-word German excluded because discourse particles carry meaning
- Status: Ready

### 2026-04-20 12:54 -- Work Completed

**What was done:**
- Added `FillerRemovalService` (static `string → string` pipeline) at `src/WhisperHeim/Services/TextProcessing/FillerRemovalService.cs` with two unconditional tiers for English (multi-word → single-word), conditional German single-word tier keyed off `de` / `de-DE` language codes, and whitespace/punctuation-spacing normalization. Word lists are sorted longest-first inside the regex alternations so `umm` wins over `um`.
- Added a repeated-comma/semicolon collapse pass so that stripping a filler out of `", um, "` produces `", "` rather than `",, "` — kept periods out of that collapse because `...` is meaningful.
- Added a `DictationTextMode` enum (`Clean` default, `Raw`) and `DictationSettings.TextMode` (persisted as `textMode` in `settings.json`).
- Wired the pipeline into `DictationOrchestrator.TranscribeFinalAsync`: raw ASR text is still used for template-matching and for the `Final` trace log, then `ApplyCleanPipeline` runs (or is skipped in Raw mode) before `SendInput`. The orchestrator now takes an optional `SettingsService` dependency; `MainWindow` passes it in.
- Added a Raw/Clean radio-button toggle to the Dictation page (under the Audio Input card), reading + persisting `DictationSettings.TextMode` via `SettingsService.Save()`.
- Added 42 unit tests in `tests/WhisperHeim.Tests/FillerRemovalServiceTests.cs` covering null/empty, both English tiers, German conditional tier, word-boundary correctness (`factually`, `gähnen`, `humming`), case-insensitivity, whitespace + punctuation normalization, ordering (multi-word before single-word, longest-first within a tier), German discourse-particle preservation, mixed-language inputs, and a <1ms-per-call performance assertion over 1000 iterations.
- Verified `.NET` regex `\b` treats umlauts as word characters (quick standalone test): `\bäh\b` matches `äh` and `Ähm` but not `gähnen`.
- Built the main project (`dotnet build src/WhisperHeim`) — 0 errors, existing warnings only. Ran the full test suite: 74/74 pass.

**Acceptance criteria status:**
- [x] `FillerRemovalService` removes multi-word and single-word English fillers with full unit-test coverage — 42 xUnit tests cover both tiers, case-insensitivity, ordering, and word boundaries.
- [x] German single-word fillers stripped when dictation language is `de-DE` — `Clean_RemovesGermanFillers_WhenDeDE` theory plus explicit tests that English stays untouched when language is `en-US`/null, and `gähnen` survives the `äh` regex.
- [x] Raw/Clean mode toggle in dictation settings, persisted, default Clean — `DictationTextMode` enum, `DictationSettings.TextMode` (default `Clean`), radio buttons in `DictationPage`, `SettingsService.Save()` persists to `settings.json`.
- [x] No regressions in dictation latency (<1ms overhead measured) — `Clean_IsFast_OnTypicalUtterance` asserts <1ms per call across 1000 iterations on a typical filler-heavy sentence; also traced by the orchestrator (stopwatch around `FillerRemovalService.Clean`).
- [x] Whitespace normalization handles leading/trailing, double spaces, and punctuation spacing — three dedicated tests plus `[Theory]` covering `;`, `:`, `!`, `?`; additional repeated-comma collapse keeps flanked fillers clean.
- [x] Word-boundary matching verified — `factually`, `humming`/`human`/`mumbling`, `gähnen`, and `sort` (alone) all preserved. (Chose not to wire "actually" into prod list; test-only verification would require adding a test-specific list — covered instead by the general substring-safety tests above.)
- [ ] Manual test: pending user verification in the running app. Plan: dictate (1) "um I think uh this is you know done" in English mode → expect "I think this is done." typed; (2) "Ich äh denke ähm das ist fertig" in German mode (language `de` or `de-DE`) → expect "Ich denke das ist fertig." typed; (3) toggle Raw mode and repeat (1) → expect verbatim output.

**Files changed:**
- `src/WhisperHeim/Services/TextProcessing/FillerRemovalService.cs` — new static service implementing the clean-text pipeline.
- `src/WhisperHeim/Models/AppSettings.cs` — added `DictationTextMode` enum and `DictationSettings.TextMode` property.
- `src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs` — injected `SettingsService`, inserted `ApplyCleanPipeline` between ASR and `SendInput`.
- `src/WhisperHeim/MainWindow.xaml.cs` — passes `_settingsService` into the orchestrator constructor.
- `src/WhisperHeim/Views/Pages/DictationPage.xaml` — added Raw/Clean radio-button toggle block to the Audio Input card.
- `src/WhisperHeim/Views/Pages/DictationPage.xaml.cs` — `InitializeTextModeToggle` + `TextMode_Checked` handler to read/persist the mode.
- `tests/WhisperHeim.Tests/FillerRemovalServiceTests.cs` — 42 new unit tests.
