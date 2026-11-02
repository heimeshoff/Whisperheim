---
id: main-066
title: Template Delete with Hover Trash Icon & Confirmation Dialog
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
size: Small
---
# Template Delete with Hover Trash Icon & Confirmation Dialog

## Description
Make template deletion work like recording deletion: a trash can icon appears on hover over each template card in the list, and clicking it opens the existing `DeleteConfirmationDialog` for confirmation. Remove the existing "Delete Template" text button from the detail panel.

## Acceptance Criteria
- [x] Trash icon appears on hover over each template card in the list (same pattern as TranscriptsPage)
- [x] Clicking the trash icon opens the existing `DeleteConfirmationDialog` showing the template name
- [x] Confirming deletion removes the template
- [x] Existing "Delete Template" button in the detail panel is removed
- [x] Hover animation matches the recordings page (fade in/out on mouse enter/leave)

## Technical Notes
- **Pattern to follow:** `TranscriptsPage.xaml` lines 185-235 — hover trash button with visibility animation
- **Dialog to reuse:** `Views/DeleteConfirmationDialog.xaml` — pass template name instead of transcript name
- **Template card:** `TemplatesPage.xaml` lines 131-147 — wrap existing StackPanel in a Grid to add the trash button overlay
- **Remove:** "Delete Template" button at `TemplatesPage.xaml` lines 194-201

## Work Log

### 2026-03-22
- Wrapped template list item `StackPanel` in a `Grid` with a hover trash icon button, matching the exact pattern from `TranscriptsPage.xaml` (visibility animation on MouseEnter/MouseLeave)
- Added `DeleteTemplateItem_Click` handler that opens `DeleteConfirmationDialog` with the template name and "Delete Template" title, then removes the template on confirmation
- Removed the "Delete Template" button from the detail panel and its associated `DeleteButton` references in code-behind
- Build succeeded with 0 errors, 0 warnings

**Acceptance Criteria:** All met
**Files changed:**
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml` — added hover trash icon to item template, removed Delete Template button
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml.cs` — replaced `DeleteButton_Click` with `DeleteTemplateItem_Click` using confirmation dialog
