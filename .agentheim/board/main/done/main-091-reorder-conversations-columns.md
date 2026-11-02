---
id: main-091
title: Reorder and resize conversation list columns
status: done
type: feature
context: main
created: 2026-03-27
completed: 2026-03-27
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
# Reorder and resize conversation list columns

## Objective
Change the column layout in the Conversations tab for better readability.

## Details
The conversations list currently shows columns in order: Title, Time, Speakers. Change to:

1. **Column order:** Title → Speakers → Time
2. **Title column:** Auto-width sized to the longest title, plus a small gap
3. **Speakers column:** Appears immediately after the title column
4. **Time column:** Right-aligned, pushed to the right boundary (fills remaining space)

## Acceptance Criteria
- [x] Column order is Title, Speakers, Time
- [x] Title column width fits content (auto-sized)
- [x] Small gap between title and speakers columns
- [x] Time column is right-aligned at the right edge

## Work Log

### 2026-03-27
**Changes made:**
- Reordered table header columns from Title/Time/Speakers to Title/Speakers/Time
- Reordered data row columns in the transcript group item template to match
- Changed column widths from fixed (*/160/120) to auto-sized (Auto/Auto/*) for both header and rows
- Added 12px right margin on Title column for gap between Title and Speakers
- Added 12px right padding on Title header button for consistent gap
- Moved HorizontalAlignment="Right" to the Time column (was not present before since it was last with fixed width)
- Moved TextTrimming/TextWrapping from Time column to Speakers column in data rows

**Files changed:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` - column order, widths, and alignment in header grid and item template grid

**All acceptance criteria met.**
