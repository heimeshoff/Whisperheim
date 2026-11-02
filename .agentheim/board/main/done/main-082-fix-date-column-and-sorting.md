---
id: main-082
title: Fix Date Column & Add Column Sorting
status: done
type: bug
context: main
created: 2026-03-25
completed: 2026-03-25
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Medium
---
# Fix Date Column & Add Column Sorting

## Problem

The "Date" column in the recordings list shows "transcript" for all new-format recordings instead of the actual recording date. This is because new-format transcripts are stored as `transcript.json` inside `YYYYMMDD_HHmmss/` session directories, and the date parser only handles the legacy `transcript_YYYYMMDD_HHmmss.json` filename format.

Additionally, there is no way to sort recordings within groups by clicking column headers.

## Acceptance Criteria

### Date Column Fix
- [x] New-format recordings show the actual recording date (from `CallTranscript.RecordingStartedUtc` or the session directory name)
- [x] Legacy-format recordings continue to work as before
- [x] Date display format remains `MMM dd, yyyy HH:mm`
- [x] GroupKey and GroupDisplayName are correctly derived from the actual date

### Column Sorting
- [x] Clicking a column header (Title, Duration, Date) sorts items within each group by that column
- [x] Clicking the same column header again toggles between ascending and descending order
- [x] Visual indicator on the active sort column showing sort direction (e.g., arrow up/down)
- [x] Default sort order: Date descending (newest first within group)

## Implementation Notes

**Key files:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — column headers, list template
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — `TranscriptListItem` constructor (date parsing at ~line 1513), `ApplyFilter()` grouping/sorting logic
- `src/WhisperHeim/Services/CallTranscription/CallTranscript.cs` — `RecordingStartedUtc` field
- `src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs` — file discovery

**Date fix approach:**
- For new-format files, parse the session directory name (`YYYYMMDD_HHmmss`) to extract the date
- Alternatively, deserialize the transcript JSON and use `RecordingStartedUtc`
- Update `TranscriptListItem` constructor to handle both formats

**Sorting approach:**
- Make column headers clickable (Button or clickable TextBlock)
- Track current sort column and direction in page state
- Apply sort in `ApplyFilter()` after grouping, before rendering

## Work Log

### 2026-03-25
**Date column fix:**
- Rewrote `TranscriptListItem` constructor to handle both formats: legacy (`transcript_YYYYMMDD_HHmmss.json`) and new (`YYYYMMDD_HHmmss/transcript.json`)
- For new-format files, first tries parsing the parent directory name; falls back to `RecordingStartedUtc` from the deserialized transcript
- Date-derived properties (ParsedDate, DateDisplay, GroupKey, GroupDisplayName) now set after transcript deserialization, so all formats produce correct dates
- Added `ParsedDuration` property for duration-based sorting

**Column sorting:**
- Added `_sortColumn` and `_sortAscending` state fields (default: Date descending)
- Added `ApplySortWithinGroup()` method supporting Title, Duration, and Date columns
- Added `ColumnHeader_Click` event handler that toggles sort direction or changes column
- Added `UpdateSortIndicators()` to show arrow (up/down) on active sort column header
- Made column headers clickable Buttons in XAML with named TextBlocks for sort indicators
- Default sort indicator arrow shown on Date column

**All acceptance criteria met.** Build succeeds (0 errors), all 32 tests pass.

**Files changed:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — column headers now clickable with sort indicators
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — date parsing fix, sorting logic, new properties
