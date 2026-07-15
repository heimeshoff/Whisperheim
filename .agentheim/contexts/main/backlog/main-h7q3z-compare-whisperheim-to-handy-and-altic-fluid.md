---
id: main-h7q3z
title: Compare WhisperHeim to Handy and Altic Fluid
status: backlog
type: spike
context: main
created: 2026-07-10
completed:
depends_on: []
blocks: []
tags: [competitive-analysis, research]
related_adrs: []
related_research: []
prior_art: []
---

## Why
WhisperHeim occupies a specific niche — local-first, zero-cost, DE+EN, Windows tray dictation
plus call/voice-message transcription. Two other tools live in the same dictation space: **Handy**
and **Fluid** (https://altic.dev/fluid). We want a competitive teardown to surface features, UX, and
capabilities they have that WhisperHeim lacks, so those gaps can be triaged into the backlog
deliberately rather than discovered by accident.

The comparison is **gap-finding, not positioning**: the goal is a concrete list of "things they do
that we don't (and whether we'd want to)", weighted against WhisperHeim's fixed identity (local,
free, private, Windows).

## What
A competitive research spike comparing **WhisperHeim vs Handy vs Fluid** across four axes:

1. **Latency & model** — streaming vs post-hoc, ASR engine/model, on-device vs cloud, VRAM/footprint,
   languages supported (esp. DE + EN).
2. **Features & workflows** — live dictation, templates/snippets, call transcription, voice-message
   transcription, output formatting, hotkey design, clipboard/insertion strategy.
3. **UX & polish** — onboarding, first-run/model-download experience, on-screen indicator (overlay/pill),
   settings surface, tray behavior.
4. **Cost, privacy, platform** — pricing model, cloud dependency, OS support, licensing / open-source
   status.

**Deliverable: a research report only** (in `.agentheim/knowledge/research/`). Each WhisperHeim gap the
teardown surfaces is *listed in the report as a candidate*, but this spike does **not** auto-create
backlog tasks — the builder triages the report afterward and captures whichever gaps are worth pursuing.

## Acceptance criteria
- [ ] A cited research report exists under `.agentheim/knowledge/research/` covering all three tools
      (WhisperHeim, Handy, Fluid), passing the standard `research-review` gate.
- [ ] Each of the four axes above is addressed for **each** tool (a per-axis × per-tool matrix, with
      "unknown / not publicly documented" called out explicitly rather than guessed).
- [ ] The report includes a dedicated **"WhisperHeim gaps"** section: a concrete, enumerated list of
      capabilities/UX/workflows Handy or Fluid have that WhisperHeim lacks, each tagged with a rough
      relevance note (aligned with WhisperHeim's local/free/private/Windows identity, or a poor fit for it).
- [ ] Claims about Handy and Fluid are grounded in primary sources (product sites, docs, repos, release
      notes) with citations — no unsourced assertions about competitor capabilities.
- [ ] The report does **not** create follow-up backlog tasks; gap-capture is left to the builder's triage.

## Notes
Captured via `quick-capture` on 2026-07-10; refined 2026-07-15 (purpose, axes, and deliverable pinned
with the builder).

- **Handy** — appears to be an open-source local dictation app; verify engine (Whisper vs Parakeet vs
  other), platform coverage, and licensing during the teardown.
- **Fluid** — https://altic.dev/fluid; verify whether it's local-first or cloud-backed, its pricing model,
  and platform support (these are the axes most likely to differentiate it from WhisperHeim).
- **WhisperHeim baseline (our side of the comparison)** is already documented — draw the "us" column from
  `vision.md` and the `main` BC README, and lean on two existing reports for the model/latency axis rather
  than re-deriving it: `knowledge/research/best-stt-models-german-english-2026-06-28.md` and
  `knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md`. (These scored below the auto-link
  threshold on slug/tag overlap, so they're cited here in Notes rather than in `related_research`.)
- Execution path: run this spike via the `research` skill (it produces the report + runs the reviewer gate).
- Deliberate scope tension to preserve: purpose is gap-finding, but delivery is report-only — the report
  *names* the gaps; it does **not** spawn captures. That triage stays with the builder.
