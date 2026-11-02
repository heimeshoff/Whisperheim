---
id: main-054
title: Hover Trash Icon per Transcript
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
# Hover Trash Icon per Transcript

## Objective
Replace the "delete selected" button with a trash icon that appears on hover for each transcript in the list.

## Details
Currently there's a "delete selected" button at the bottom of the transcript list. Instead, show a small trash can icon in the top-right (or bottom-right) corner of each transcript item when the user hovers over it. This is more intuitive and doesn't require selection + scrolling to a button.

## Acceptance Criteria
- [x] "Delete selected" button is removed from the bottom of the list
- [x] Trash icon appears on hover for each transcript item
- [x] Clicking the trash icon triggers the delete confirmation
- [x] Icon position doesn't interfere with other content

## Work Log
<!-- Appended by /work during execution -->
### 2026-03-22
- Removed the "Delete Selected" button from the bottom of the transcript list (and its third row definition)
- Added a trash icon (Delete24 symbol) to each transcript list item, positioned at bottom-right
- Icon uses Grid.Triggers with EventTrigger for MouseEnter/MouseLeave to show/hide via storyboard animations
- New `DeleteTranscriptItem_Click` handler extracts the `TranscriptListItem` from the button's DataContext
- Click event is marked as Handled to prevent it from bubbling up and selecting the item
- Removed `DeleteButton.IsEnabled` references from `TranscriptList_SelectionChanged` and `ClearViewer`
- Build succeeds with 0 warnings, 0 errors
