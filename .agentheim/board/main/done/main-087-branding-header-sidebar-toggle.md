---
id: main-087
title: Branding Header as Sidebar Toggle
status: done
type: feature
context: main
created: 
completed: 
commit:
depends_on: [main-084]
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Small
---
# Branding Header as Sidebar Toggle

## Description

Remove the dedicated `SidebarToggleButton` (chevron on the sidebar edge) and make the branding header (microphone logo + "WhisperHeim" title) the click target for collapsing/expanding the sidebar.

## Acceptance Criteria

- [x] `SidebarToggleButton` is removed from `MainWindow.xaml`
- [x] Clicking the branding header (`BrandingHeader` StackPanel) collapses the sidebar when expanded
- [x] Clicking the branding header (just the microphone icon when collapsed) expands the sidebar
- [x] Mouse cursor changes to pointer (Hand) when hovering over the branding header
- [x] No tooltip on the branding header
- [x] No hover highlight or opacity change — cursor change is the only affordance
- [x] Existing collapse/expand animation and state persistence continue to work
- [x] All other sidebar behavior unchanged (nav item clicks, label visibility, etc.)

## Implementation Notes

- Wire the existing `SidebarToggle_Click` handler (or equivalent logic) to the branding header
- Remove `SidebarToggleButton` XAML and any related styles/resources
- Clean up `ApplySidebarCollapsedState` to remove chevron icon swapping logic
- Set `Cursor="Hand"` on `BrandingHeader`

## Files to Modify

- `src/WhisperHeim/MainWindow.xaml` — remove toggle button, add click handler to branding header
- `src/WhisperHeim/MainWindow.xaml.cs` — update/clean up toggle logic

## Work Log

- Removed `SidebarToggleButton` (40-line XAML block including custom template, chevron icon, hover triggers)
- Added `Cursor="Hand"` and `MouseLeftButtonDown="BrandingHeader_MouseLeftButtonDown"` to the `BrandingHeader` StackPanel
- Renamed `SidebarToggle_Click` to `BrandingHeader_MouseLeftButtonDown` (with matching `MouseButtonEventArgs` signature)
- Removed chevron icon symbol swapping and tooltip update logic from `ApplySidebarCollapsedState`
- Build verified: 0 errors, only pre-existing warnings
