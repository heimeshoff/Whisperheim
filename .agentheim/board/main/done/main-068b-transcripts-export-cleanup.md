---
id: main-068
title: Transcripts Page Export Cleanup
status: done
type: feature
context: main
created: 
completed: 2026-03-23
commit:
depends_on: []
blocks: []
tags: [ui-polish]
related_adrs: []
related_research: []
prior_art: []
milestone: UI Polish (Quiet Engine)
size: Small
---
# Transcripts Page Export Cleanup

## Description

Streamline the export options on the transcripts/recordings page:

1. **Remove the TXT download button** — only MD and JSON downloads are needed.
2. **Copy button copies Markdown** — change `CopyToClipboard_Click` to use `FormatAsMarkdown()` instead of `FormatAsPlainText()`.
3. **Brief "Copied!" tooltip** — show a short visual confirmation when the copy succeeds (e.g., a tooltip or transient text near the button that fades after ~1–2 seconds).

## Acceptance Criteria

- [ ] TXT button is removed from the action panel in `TranscriptsPage.xaml`
- [ ] `ExportText_Click` handler can be removed from code-behind
- [ ] Copy button calls `FormatAsMarkdown()` and copies the result to clipboard
- [ ] A brief "Copied!" indicator appears after clicking Copy and disappears automatically
- [ ] MD and JSON download buttons remain unchanged

## Files to Modify

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` (remove TXT button)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` (change copy format, add tooltip feedback, optionally remove `ExportText_Click`)

## Work Log

### 2026-03-23

**Changes made:**

1. **Removed TXT button** from the action panel in `TranscriptsPage.xaml` (line 435-438 removed).
2. **Changed Copy to use Markdown**: Updated `CopyToClipboard_Click` to call `FormatAsMarkdown()` instead of `FormatAsPlainText()`.
3. **Added "Copied!" indicator**: Added a `TextBlock` named `CopiedIndicator` next to the Copy button that appears for 1.5 seconds after clicking Copy, using a `DispatcherTimer`.
4. **Removed `ExportText_Click` handler** from code-behind since TXT export is no longer needed.

**Acceptance criteria status:**
- [x] TXT button is removed from the action panel in `TranscriptsPage.xaml`
- [x] `ExportText_Click` handler removed from code-behind
- [x] Copy button calls `FormatAsMarkdown()` and copies the result to clipboard
- [x] A brief "Copied!" indicator appears after clicking Copy and disappears automatically (1.5s)
- [x] MD and JSON download buttons remain unchanged

**Files changed:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml`
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`

**Build note:** Pre-existing build errors exist in `DictationOverlayWindow.xaml.cs` and `DictationPage.xaml` (likely from concurrent tasks). No errors from TranscriptsPage files.
