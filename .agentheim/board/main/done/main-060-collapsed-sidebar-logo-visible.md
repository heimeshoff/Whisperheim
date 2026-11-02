---
id: main-060
title: Show Full Logo in Collapsed Sidebar
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
# Show Full Logo in Collapsed Sidebar

## Objective
Ensure the logo is fully visible when the sidebar is collapsed, and only show the app name when the sidebar is expanded.

## Details

### Problem
When the sidebar collapses to 60px, the 32px logo with 16px margins on each side (= 64px total) gets clipped on the right. The logo should remain fully visible and centered in the collapsed sidebar.

### Solution
- Increase `SidebarCollapsedWidth` from 60px to 64px in `MainWindow.xaml.cs` (line ~91)
- When collapsed: center the logo horizontally within the sidebar, hide the app name (`BrandingTitle`)
- When expanded: show both logo and app name side by side (current behavior)
- Adjust the branding header margins when collapsed so the logo is properly centered in the 64px width

### Files
- `MainWindow.xaml` — branding header layout, logo margins
- `MainWindow.xaml.cs` — `SidebarCollapsedWidth` constant, `ApplySidebarCollapsedState` method

## Acceptance Criteria
- [x] Logo is fully visible and centered when sidebar is collapsed
- [x] App name ("WhisperHeim") is hidden when collapsed, visible when expanded
- [x] No clipping of the logo at collapsed width
- [x] Works correctly in both light and dark themes

## Work Log
<!-- Appended by /work during execution -->
### 2026-03-22
- Increased `SidebarCollapsedWidth` from 60px to 64px in `MainWindow.xaml.cs` (16px margin + 32px logo + 16px margin = 64px)
- Added `x:Name="BrandingHeader"` and `x:Name="BrandingLogo"` to XAML elements for code-behind access
- In `ApplySidebarCollapsedState`: remove logo right margin (10px) when collapsed so it fits exactly in the content area
- BrandingTitle was already hidden via `labelVisibility` — no change needed there
- Theme-independent: only layout margins changed, no color/brush changes
- Build verified: 0 warnings, 0 errors
