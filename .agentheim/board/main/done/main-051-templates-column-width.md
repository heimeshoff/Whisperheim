---
id: main-051
title: Reduce Templates List Column Width
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
# Reduce Templates List Column Width

## Objective
Shrink the templates list column on the edit template page so the editor has more space, especially at smaller window sizes.

## Details
On the Templates page, the template list column takes too much horizontal space. At half-screen width, the list dominates and the edit area gets only ~300px, making editing difficult. Reduce the template list to roughly 1/3 of its current width, or make the split resizable.

## Acceptance Criteria
- [x] Template list column is significantly narrower (about 1/3 of current width)
- [x] Edit area has adequate space even at half-screen window size
- [x] Template names remain readable in the narrower list

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22 — Completed
- Reduced template list column from fixed 300px to 200px (with MinWidth=160, MaxWidth=260) so the edit area gets more space at all window sizes.
- Tightened internal padding (StackPanel margins, ListBox margins, list item padding/corner radius) to make better use of the narrower column.
- Build verified: 0 errors.
