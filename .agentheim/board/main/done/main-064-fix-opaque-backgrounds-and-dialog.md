---
id: main-064
title: Fix Opaque Backgrounds and Delete Dialog
status: done
type: bug
context: main
created: 
completed: 
commit:
depends_on: []
blocks: []
tags: [ui-polish]
related_adrs: []
related_research: []
prior_art: []
milestone: UI Polish
size: Small
---
# Fix Opaque Backgrounds and Delete Dialog

## Description

Two related UI fixes:

### 1. Recordings & Templates page background
The Recordings (TranscriptsPage) and Templates page have a background color that appears opaque/solid, unlike other screens. They should use the same normal background that all other screens have — likely by not painting over the window's mica/acrylic effect, or by matching whatever approach the other pages (e.g., DictationPage) use.

Both pages currently set `Background="{DynamicResource ApplicationBackgroundBrush}"` on their root Grid. Investigate whether this brush is the problem or if the issue is elsewhere.

### 2. Delete confirmation dialog — remove glass effect
The `DeleteConfirmationDialog` currently uses a semi-transparent `#CC202020` background for a frosted glass/acrylic look. This looks bad. Replace with an **opaque, theme-aware surface/card color**:
- Dark theme: solid dark surface color
- Light theme: solid light/whitish surface color

Use `{DynamicResource CardBackgroundFillColorDefaultBrush}` or equivalent so it respects the current theme. Remove the transparency-related styling (the `#CC202020` background and the `#20FFFFFF` border overlay).

## Acceptance Criteria

- [x] Recordings page background matches other screens (e.g., Dictation, General)
- [x] Templates page background matches other screens
- [x] Delete confirmation dialog has a solid opaque background
- [x] Dialog background adapts to dark/light theme automatically
- [x] No transparency or glass effects remain on the dialog

## Files to Modify

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — background fix
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml` — background fix
- `src/WhisperHeim/Views/DeleteConfirmationDialog.xaml` — replace glass effect with opaque themed background

## Work Log

- Removed `Background="{DynamicResource ApplicationBackgroundBrush}"` from the root Grid in TranscriptsPage.xaml and TemplatesPage.xaml. This was painting an opaque solid color over the window's mica effect, unlike other pages which either don't set a background on their root container or use a ScrollViewer.
- Replaced the semi-transparent `#CC202020` background and `#20FFFFFF` border overlay in DeleteConfirmationDialog.xaml with `CardBackgroundFillColorDefaultBrush` (solid, theme-aware) and `ControlStrokeColorDefaultBrush` border.
- Build succeeds. All acceptance criteria met.

### Files Changed
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml`
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml`
- `src/WhisperHeim/Views/DeleteConfirmationDialog.xaml`
