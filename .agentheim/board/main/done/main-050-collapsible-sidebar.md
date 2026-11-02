---
id: main-050
title: Collapsible Sidebar Menu
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
size: Medium
---
# Collapsible Sidebar Menu

## Objective
Allow the sidebar menu to collapse to icons-only mode, giving more horizontal space to page content.

## Details
The sidebar currently always shows icons + text labels. Add the ability to collapse it to show only icons (and expand back to show labels). This helps when the user wants more content area, especially at smaller window sizes.

Consider: a toggle button (hamburger icon or chevron) at the top/bottom of the sidebar, or collapse on narrow window widths.

## Acceptance Criteria
- [x] Sidebar can be collapsed to icons-only mode
- [x] Sidebar can be expanded back to show icons + labels
- [x] Toggle is easily discoverable (button or similar)
- [x] Collapsed/expanded state feels smooth (animation optional but nice)
- [x] Works correctly in both light and dark themes

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22
- Added collapsible sidebar with toggle button at bottom of nav panel
- Sidebar collapses from 200px to 60px (icons-only mode) with smooth 200ms animation using custom `GridLengthAnimation`
- Toggle button uses PanelLeftContract24/PanelLeftExpand24 icons for clear affordance
- When collapsed: hides text labels, branding text, and centers icons; when expanded: restores full layout
- Sidebar collapsed state persists via `WindowSettings.SidebarCollapsed` in settings.json
- All styling uses DynamicResource brushes, so it works correctly in both light and dark themes
- Build verified: 0 errors, 0 warnings
