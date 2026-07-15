---
id: main-h7q3z
title: Compare WhisperHeim to Handy and Altic Fluid
status: done
type: spike
context: main
created: 2026-07-10
completed: 2026-07-15
depends_on: []
blocks: []
tags: [competitive-analysis, research]
related_adrs: []
related_research: [competitive-teardown-handy-fluid-2026-07-15]
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
- [x] A cited research report exists under `.agentheim/knowledge/research/` covering all three tools
      (WhisperHeim, Handy, Fluid), passing the standard `research-review` gate.
- [x] Each of the four axes above is addressed for **each** tool (a per-axis × per-tool matrix, with
      "unknown / not publicly documented" called out explicitly rather than guessed).
- [x] The report includes a dedicated **"WhisperHeim gaps"** section: a concrete, enumerated list of
      capabilities/UX/workflows Handy or Fluid have that WhisperHeim lacks, each tagged with a rough
      relevance note (aligned with WhisperHeim's local/free/private/Windows identity, or a poor fit for it).
- [x] Claims about Handy and Fluid are grounded in primary sources (product sites, docs, repos, release
      notes) with citations — no unsourced assertions about competitor capabilities.
- [x] The report does **not** create follow-up backlog tasks; gap-capture is left to the builder's triage.

## Outcome
Produced `.agentheim/knowledge/research/competitive-teardown-handy-fluid-2026-07-15.md`: a four-axis
(latency/model, features/workflows, UX/polish, cost/privacy/platform) teardown of WhisperHeim vs
**Handy** (github.com/cjpais/Handy — MIT, Windows/Mac/Linux, user-selectable ASR model menu incl.
Parakeet TDT v3, fully local, no cloud) vs **Fluid** (altic.dev/fluid — GPLv3, mature shipped macOS
app whose Windows build is a days-old waitlist-gated beta as of this report's date, with local-first
default plus optional opt-in cloud providers). Includes an 11-item enumerated "WhisperHeim gaps"
section (each tagged for identity fit — e.g. model-picker and CLI-scriptable dictation control flagged
"worth considering"; Fluid's voice-driven OS Command Mode flagged "poor fit" against WhisperHeim's
non-goal; cross-platform flagged "not applicable"), a "notable non-gaps" callout (call transcription
w/ diarization, voice-message transcription, and voice-triggered templates remain WhisperHeim
differentiators — not found in either competitor), 10 numbered primary-source citations, and an Open
Questions section. Passed the `research-reviewer` gate on the first iteration (PASS, no re-dispatch
needed) — all decision-critical checkable claims about Handy and Fluid verified against primary
sources (GitHub repos/READMEs, docs, release history, product pages); WhisperHeim's own baseline
column was drawn from `vision.md` + the BC README + the two prior STT-model reports per the task's
Notes, not independently re-researched. No backlog tasks were created; gap triage is left to the
builder per the task's scope.

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
