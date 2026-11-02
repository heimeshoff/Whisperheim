---
id: main-086
title: Transcripts list column redesign
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
# Transcripts list column redesign

## Description

Redesign the three columns in the transcripts/conversations list:

1. **Title** — unchanged
2. **Time** (renamed from "Duration") — shows start time of day, dash, then compact duration. Format: `14:30 – 45m` or `14:30 – 1h 12m`
3. **Speakers** (renamed from "Date") — shows remote speaker names from session metadata, comma-separated. Empty if no names recorded.

Date grouping (collapsible day headers) remains unchanged — the date is still visible as the group header, just not as a column on each row.

Default sort changes to the Time column (by start time, newest first).

## Acceptance Criteria

- [ ] "Duration" column header renamed to "TIME", displays `HH:mm – Xh Ym` or `HH:mm – Xm` format
- [ ] "Date" column header renamed to "SPEAKERS", displays comma-separated `RemoteSpeakerNames`
- [ ] Empty speakers column when no remote speaker names are recorded
- [ ] Default sort is by start time (newest first) via the Time column
- [ ] Sorting by Time column sorts by start time, not duration
- [ ] Sort header arrows update correctly for the renamed columns
- [ ] Date-based grouping with collapsible sections still works unchanged
- [ ] Search still works (should search speaker names too)

## Files to Modify

- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — column headers, bindings
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — TranscriptListItem properties, sort logic, display formatting

## Work Log

### 2026-03-26

**Changes made:**

1. **XAML column headers**: Renamed "DURATION" to "TIME" (Tag="Time"), "DATE" to "SPEAKERS" (Tag="Speakers"). TIME column shows default descending arrow. Row bindings changed from `DurationDisplay`/`DateDisplay` to `TimeDisplay`/`SpeakersDisplay`. Added `TextTrimming` on speakers column.

2. **TranscriptListItem**: Removed `DateDisplay` and `DurationDisplay` properties. Added `TimeDisplay` (format: `HH:mm – Xh Ym` or `HH:mm – Xm`) and `SpeakersDisplay` (comma-separated `RemoteSpeakerNames`).

3. **Sort logic**: Default sort column changed from "Date" to "Time". `ApplySortWithinGroup` updated — "Time" sorts by `ParsedDate` (start time), "Speakers" sorts alphabetically. `UpdateSortIndicators` references renamed to `TimeSortHeader`/`SpeakersSortHeader`.

4. **Search**: Now includes `SpeakersDisplay` and `TimeDisplay` in search filter.

5. **Delete dialog**: Fixed `DateDisplay` reference to use `FileName` as fallback display name.

**Acceptance criteria status:** All met.
- [x] "Duration" column header renamed to "TIME", displays `HH:mm – Xh Ym` or `HH:mm – Xm` format
- [x] "Date" column header renamed to "SPEAKERS", displays comma-separated `RemoteSpeakerNames`
- [x] Empty speakers column when no remote speaker names are recorded
- [x] Default sort is by start time (newest first) via the Time column
- [x] Sorting by Time column sorts by start time, not duration
- [x] Sort header arrows update correctly for the renamed columns
- [x] Date-based grouping with collapsible sections still works unchanged
- [x] Search still works (searches speaker names too)

**Build:** Succeeded (0 errors). **Tests:** 32/32 passed.

**Files changed:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml`
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`
