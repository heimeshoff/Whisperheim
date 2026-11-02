---
id: main-r8m4q
title: About-page Parakeet link points to v2, app uses v3
status: done
type: bug
context: main
created: 2026-06-29
completed: 2026-06-29
depends_on: []
blocks: []
tags: [about-page, parakeet, model-card, link]
related_adrs: []
related_research: []
prior_art: [main-056]
---

## Why
The About page renders a "project" link for each bundled model. For Parakeet it
links to the **v2** model card (`https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2`),
but WhisperHeim actually runs **Parakeet TDT 0.6B v3** (the int8 sherpa-onnx v3 build).
v2 is English-only; v3 added German + 24 EU languages — so the link sends users to
the wrong, less-capable model and misrepresents what the app uses.

## What
Point the Parakeet model card's `ProjectUrl` at the v3 card. The link is the
`ProjectUrl` of the `ParakeetTdt06B` definition in
`src/WhisperHeim/Services/Models/ModelManagerService.cs:61`; the About page binds it
via `AboutPage.xaml:313` (`Tag="{Binding ProjectUrl}"`) ← `AboutPage.xaml.cs:55`.

Change:
- from `https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2`
- to   `https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3`

## Acceptance criteria
- [ ] `ParakeetTdt06B.ProjectUrl` in `ModelManagerService.cs` resolves to the v3 card (`.../parakeet-tdt-0.6b-v3`).
- [ ] The About page's Parakeet model card opens the v3 NVIDIA Hugging Face page when clicked.
- [ ] No other model's `ProjectUrl` is touched.

## Notes
- Single-line copy fix; no architectural decision. The v3 card URL is confirmed in
  `knowledge/research/best-stt-models-german-english-2026-06-28.md` (ref [6]).
- Prior art `main-056` ("Link AI Model Cards to GitHub Projects") introduced the
  `ProjectUrl` field and the About-page binding.

## Outcome
Changed `ParakeetTdt06B.ProjectUrl` in
`src/WhisperHeim/Services/Models/ModelManagerService.cs` (line 61) from the v2 card
(`.../parakeet-tdt-0.6b-v2`) to the v3 card (`.../parakeet-tdt-0.6b-v3`), matching the
int8 sherpa-onnx v3 build the app actually runs. No other model's `ProjectUrl` touched.
The About page binds this value via `AboutPage.xaml` `Tag="{Binding ProjectUrl}"`, so
the Parakeet model card now opens the v3 NVIDIA Hugging Face page. Build succeeds
(0 errors). Single-line copy fix — no test added (no UI test infrastructure; verified by
inspection and clean build).
