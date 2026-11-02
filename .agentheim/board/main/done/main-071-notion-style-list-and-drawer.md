---
id: main-071
title: Notion-Style List View with Detail Drawer
status: done
type: feature
context: main
created: 
completed: 2026-03-24
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: --
size: Medium
---
# Notion-Style List View with Detail Drawer

## Description

Replace the current side-by-side card layout (list on left, content card on right) used by both Templates and Recordings pages with a Notion-inspired design:

1. **Compact list view** — Each item is a single row instead of a card.
   - **Recordings:** columns for Title, Attendees (list), Timestamp
   - **Templates:** columns for Name, Description snippet
2. **Grouping** — Rows can be organized into named, collapsible/expandable groups.
3. **Detail drawer** — Clicking a row opens a right-side overlay drawer showing the full content (template body or recording transcript). The drawer should:
   - Be as wide as the current content cards
   - Overlay the list (not push it aside)
   - Have a close button to dismiss it
   - Contain the delete action (removed from the list row)

## Why

- More items visible at a glance on a single screen
- Key metadata (attendees, timestamps) scannable without clicking
- Grouping allows logical organization and reduces clutter
- Drawer keeps full detail accessible without navigating away

## Acceptance Criteria

- [x] Recordings page shows a compact table/list with columns: Title, Duration, Date
- [x] Templates page shows a compact table/list with columns: Name, Description snippet
- [x] Clicking a row opens a detail drawer sliding in from the right
- [x] Drawer overlays the list and is wide enough to show full content
- [x] Drawer has a visible close button
- [x] Delete action lives inside the drawer, not on the list row
- [x] Rows can be organized into named, collapsible/expandable groups
- [x] Existing functionality (viewing, deleting templates/recordings) is preserved
- [x] Dark mode / theme support maintained

## Technical Notes

- Applies to both `TemplatesPage` and `RecordingsPage` (or equivalent components)
- Consider extracting a shared `ListWithDrawer` component usable by both pages
- Grouping state (expanded/collapsed) can be local component state initially

## Work Log

### 2026-03-24 — Implementation Complete

**Changes made:**

1. **TemplatesPage.xaml** — Replaced side-by-side layout (list on left, editor on right) with:
   - Full-width compact table with Name and Description columns
   - "Add New" button in header bar
   - Right-side overlay drawer (440px) for editing/creating templates
   - Delete button inside drawer footer
   - Drawer close button (X icon) and click-away-to-close overlay

2. **TemplatesPage.xaml.cs** — Refactored to drawer-based interaction:
   - Row click opens drawer with template data pre-filled
   - Add New opens drawer in create mode
   - Save button handles both add and update
   - Delete confirmation dialog triggered from drawer

3. **TranscriptsPage.xaml** — Replaced side-by-side layout with:
   - Full-width compact table with Title, Duration, Date columns
   - Collapsible/expandable groups by date (TranscriptGroupViewModel with toggle buttons)
   - Pending recordings shown in their own collapsible group
   - Right-side overlay drawer (520px) for transcript viewer
   - Delete button moved inside drawer footer (left side)
   - Export actions (Copy, MD, JSON) in drawer footer (right side)
   - Drawer close button and click-away overlay

4. **TranscriptsPage.xaml.cs** — Refactored to drawer-based interaction:
   - Added TranscriptGroupViewModel for collapsible date-based grouping
   - TranscriptListItem extended with GroupKey and GroupDisplayName for grouping
   - All transcript viewer functionality preserved in drawer (playback, speaker editing, export)
   - Added ToggleButton using System.Windows.Controls.Primitives

**Build:** Succeeded with 0 errors, 0 warnings
**Tests:** All 32 tests pass
