---
id: main-049
title: Add WhisperHeim Logo to Sidebar
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
# Add WhisperHeim Logo to Sidebar

## Objective
Add the WhisperHeim logo to the top-left of the sidebar menu next to the app name, using blue and orange brand colors.

## Details
The WhisperHeim logo (currently visible in settings) should also appear in the sidebar header, to the left of the "WhisperHeim" text. The logo should use the brand colors from VocalFold:
- **Blue:** `#25abfe`
- **Orange:** `#ff8b00`

Currently the logo/microphone icon is just blue. Update it to incorporate both blue and orange.

## Acceptance Criteria
- [x] Logo appears in the top-left of the sidebar next to the app name
- [x] Logo uses both blue (#25abfe) and orange (#ff8b00)
- [x] Logo in settings page also updated to use both colors
- [x] Looks good in both light and dark themes

## Work Log
<!-- Appended by /work during execution -->
### 2026-03-22
- Added logo (Mic24 icon with blue-to-orange gradient border) to the sidebar branding header in MainWindow.xaml, positioned to the left of the "WhisperHeim" text
- Updated GeneralPage.xaml and AboutPage.xaml logo gradients from old blue (#005FAA/#0078D4) to brand colors blue (#25abfe) to orange (#ff8b00)
- Updated mic icon foreground in all three locations to use the same blue-to-orange gradient
- Logo uses DynamicResource CardBackgroundFillColorDefaultBrush for inner background, ensuring proper appearance in both light and dark themes
- Build verified: 0 warnings, 0 errors
