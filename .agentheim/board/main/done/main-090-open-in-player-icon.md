---
id: main-090
title: Add proper play icon to "Open in Player" button
status: done
type: feature
context: main
created: 2026-03-26
completed: 2026-03-26
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Small
---
# Add proper play icon to "Open in Player" button

## Objective

Replace the raw Unicode glyph on the "Open in Player" button with a proper Wpf.Ui `SymbolIcon`, matching the pattern used by every other button in the transcript view.

## Details

The `OpenExternalButton` in `TranscriptsPage.xaml` (line 684-691) uses `Content="&#xE768;  Open in Player"` — a raw Unicode character that doesn't render as a proper icon. All other buttons use `ui:SymbolIcon` elements.

### Fix

Replace the plain text `Content` with a `StackPanel` containing a `ui:SymbolIcon` (e.g., `Play24` or `Open24`) and a `TextBlock`, following the same pattern as the other buttons in the file.

## Acceptance Criteria

- [x] "Open in Player" button shows a recognizable play icon
- [x] Icon style matches other buttons in the transcript view

## Work Log

**2026-03-26:** Replaced raw Unicode `Content="&#xE768;  Open in Player"` with a `StackPanel` containing `ui:SymbolIcon Symbol="Open24"` and `TextBlock`, matching the pattern used by Browse and other buttons. Build succeeds with 0 errors.
