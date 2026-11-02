---
id: main-047
title: Fix TTS Voice Cards Dark Mode Background
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
# Fix TTS Voice Cards Dark Mode Background

## Objective
Replace hardcoded white background on library voice cards in the Text to Speech page with theme-aware brushes so they look correct in dark mode.

## Details
The voice cards are built programmatically in `TextToSpeechPage.xaml.cs` (~line 665) using `Color.FromRgb(0xFF, 0xFF, 0xFF)` (hardcoded white). This ignores the active theme, so in dark mode the cards appear as bright white boxes while everything else adapts.

Fix: replace the hardcoded white with the dynamic theme brush `CardBackgroundFillColorDefaultBrush` (already used elsewhere on the same page). Also check the delete button foreground color (`Color.FromRgb(0xBA, 0x1A, 0x1A)`) for theme consistency.

Ideally, consider moving the voice card creation from code-behind to a XAML DataTemplate with proper `DynamicResource` bindings, but the minimal fix is just swapping the brush.

Key file:
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml.cs` — voice card Border creation

## Acceptance Criteria
- [x] Voice cards use theme-aware background brush instead of hardcoded white
- [x] Cards look correct in both Light and Dark themes
- [x] Text on cards remains readable in both themes

## Notes
The rest of the page already uses DynamicResource brushes correctly — this is just a missed spot in code-behind.

## Work Log
<!-- Appended by /work during execution -->
### 2026-03-22 — Completed
- Replaced hardcoded `Color.FromRgb(0xFF, 0xFF, 0xFF)` background on voice card `Border` with `FindResource("CardBackgroundFillColorDefaultBrush")` to use theme-aware brush.
- Brightened delete button foreground from `0xBA,0x1A,0x1A` to `0xE8,0x3A,0x3A` for better visibility on dark backgrounds.
- Text blocks inherit foreground from the theme automatically — readable in both light and dark modes.
- Build passes with zero errors and warnings.
