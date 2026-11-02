---
id: main-093
title: Show Distinct Speaker Names in Collapsed Date Groups
status: done
type: feature
context: main
created: 
completed: 2026-03-31
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
# Show Distinct Speaker Names in Collapsed Date Groups

## Description

When a date group in the transcripts list is collapsed, show the distinct remote speaker names from all recordings in that group next to the group header. When the group is expanded, hide those names since they are already visible per recording.

Example collapsed: `MARCH 31, 2026 (3) — Alice, Bob, Carol`
Example expanded: `MARCH 31, 2026 (3)`

## Acceptance Criteria

- [ ] Collapsed date groups display distinct remote speaker names after the count
- [ ] Names are comma-separated, deduplicated, and sorted
- [ ] Only remote speakers are shown (not the local user)
- [ ] When the group is expanded, the speaker names are hidden
- [ ] Long name lists truncate naturally via `TextTrimming="CharacterEllipsis"` when space runs out

## Implementation Notes

Key files:
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — group header template (lines ~410-467)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — `TranscriptGroupViewModel` class and grouping logic

Approach:
1. In `TranscriptGroupViewModel`, add a computed property `SpeakersSummary` that aggregates distinct `SpeakersDisplay` values from all items in the group
2. Add a `SpeakersSummaryVisibility` property that returns `Visible` when `!IsExpanded` and `Collapsed` when `IsExpanded`
3. In the XAML group header template, add a `TextBlock` bound to `SpeakersSummary` with visibility bound to the inverse of `IsExpanded`, using `TextTrimming="CharacterEllipsis"`
4. Ensure `OnPropertyChanged` for `IsExpanded` also raises change notification for the summary visibility

## Work Log

**2026-03-31** — Implemented collapsed group speaker names.

### Changes
- **`TranscriptGroupViewModel`** (`TranscriptsPage.xaml.cs`): Added `SpeakersSummary` property that pre-computes distinct, sorted remote speaker names from all items in the group at construction time. Format: ` — Alice, Bob, Carol`.
- **Group header XAML** (`TranscriptsPage.xaml`): Added a `TextBlock` bound to `SpeakersSummary` with `InverseBoolToVisibility` converter on `IsExpanded`, plus `TextTrimming="CharacterEllipsis"` for long name lists.

### Acceptance Criteria
- [x] Collapsed date groups display distinct remote speaker names after the count
- [x] Names are comma-separated, deduplicated, and sorted
- [x] Only remote speakers are shown (not the local user) — uses `SpeakersDisplay` which already contains only remote speakers
- [x] When the group is expanded, the speaker names are hidden — uses `InverseBoolToVisibility` on `IsExpanded`
- [x] Long name lists truncate naturally via `TextTrimming="CharacterEllipsis"` when space runs out

### Files Changed
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml`
