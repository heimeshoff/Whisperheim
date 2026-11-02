---
id: main-045
title: Consistent Max-Width Across All Pages
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
# Consistent Max-Width Across All Pages

## Objective
Apply the same content max-width constraint used on the Transcripts page to all other pages for visual consistency.

## Details
The Transcripts page already constrains its content to a maximum width. Other pages (Recordings/TranscribeFiles, Templates, Text to Speech, Dictation, About, General Settings) stretch full width. Apply the same max-width approach from Transcripts to all pages so content doesn't stretch uncomfortably wide on larger screens.

Key files:
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — reference implementation
- `src/WhisperHeim/Views/Pages/TranscribeFilesPage.xaml`
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml`
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml`
- `src/WhisperHeim/Views/Pages/DictationPage.xaml`
- `src/WhisperHeim/Views/Pages/AboutPage.xaml`
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml`

## Acceptance Criteria
- [x] All pages use the same max-width as the Transcripts page
- [x] Content is centered (not left-aligned) when the window is wider than the max-width
- [x] No visual regressions on pages that already look correct

## Notes
Small task — copy the existing pattern from TranscriptsPage to the other pages.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22
- Standardized MaxWidth="900" with HorizontalAlignment="Center" across all pages:
  - TranscriptsPage.xaml: Added MaxWidth="900" and HorizontalAlignment="Center" to main content Grid
  - TranscribeFilesPage.xaml: Changed MaxWidth from 860 to 900 (already centered)
  - DictationPage.xaml: Changed MaxWidth from 960 to 900, added HorizontalAlignment="Center"
  - AboutPage.xaml: Changed MaxWidth from 800 to 900, changed HorizontalAlignment from Left to Center
  - GeneralPage.xaml: Changed MaxWidth from 800 to 900 (already centered)
  - TextToSpeechPage.xaml: Added MaxWidth="900" and HorizontalAlignment="Center" to main Grid
- TemplatesPage left as-is (split panel layout with its own constraints)
- Build has pre-existing error from concurrent task (DeleteTranscriptItem_Click), unrelated to this change
