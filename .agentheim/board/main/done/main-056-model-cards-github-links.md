---
id: main-056
title: Link AI Model Cards to GitHub Projects
status: done
type: feature
context: main
created: 2026-03-22
completed: 2026-03-22
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: --
size: Small
---
# Link AI Model Cards to GitHub Projects

## Objective
Make each AI model card in the settings page a clickable link to its respective GitHub or project page.

## Details
In the settings page, the AI model cards (Parakeet, Silero VAD, PiperTTS, etc.) should be clickable links that open the browser to the model's source project. This lets users learn more about each model.

Research the correct URLs for each model used in the project and add them as hyperlinks.

## Acceptance Criteria
- [x] Each model card links to its respective project page
- [x] Clicking opens the URL in the default browser
- [x] Visual affordance that the cards are clickable (cursor change, underline, etc.)

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22 — Implementation complete
- Added `ProjectUrl` optional parameter to `ModelDefinition` record
- Populated project URLs for all 6 models:
  - Parakeet TDT 0.6B v3 → HuggingFace NVIDIA page
  - Silero VAD → GitHub snakers4/silero-vad
  - Pyannote Segmentation 3.0 → GitHub pyannote/pyannote-audio
  - 3D-Speaker ERes2Net → GitHub modelscope/3D-Speaker
  - Pocket TTS FP32 & int8 → GitHub kyutai-labs/moshi
- Added `ProjectUrl` property to `ModelStatusViewModel`
- Updated model card templates in both `GeneralPage.xaml` and `AboutPage.xaml`:
  - Added `Cursor="Hand"` for pointer cursor affordance
  - Added `MouseLeftButtonUp` click handler
  - Added external-link icon (`Open24`) next to model name
- Added `ModelCard_Click` handler in both code-behind files to open URL via `Process.Start`
- Fixed `AboutPage` to actually load model data (was previously not calling `LoadModelStatus`)
- Build passes with 0 errors, 0 warnings
