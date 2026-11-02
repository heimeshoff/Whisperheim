---
id: main-084
title: Sidebar Collapse Icon & Branding Reshuffle
status: done
type: feature
context: main
created: 
completed: 2026-03-25
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
# Sidebar Collapse Icon & Branding Reshuffle

## Description

Reorganize the sidebar and unify branding between the About page and the Dictation page.

## Acceptance Criteria

### Sidebar — Collapse Button → Chevron on Right Edge
- [x] Remove the current collapse button (Row 2: icon + "COLLAPSE" text) from the bottom of the sidebar
- [x] Replace it with a small chevron/arrow icon vertically centered on the right edge of the sidebar
  - Chevron points left when sidebar is expanded (click to collapse)
  - Chevron points right when sidebar is collapsed (click to expand)
  - Sits flush against the sidebar's right border, visually subtle
- [x] Collapse/expand behavior remains the same as today

### Sidebar — About Moved to Bottom
- [x] Move the "About" nav item from its current position (7th item) to the bottom of the sidebar, pinned below the other nav items (replacing the slot where the collapse button was)
- [x] Visually separate it from the main nav items above (e.g. auto margin or spacer)
- [x] Keep the same Info24 icon and pill style

### Dictation Page — Branding Header
- [x] Replace the current Dictation page header ("Dictation" title + its subtitle) with the About page's branding:
  - Logo (88px two-tone microphone with blue border, CornerRadius 18)
  - "WhisperHeim v1.0" title line (40px ExtraBold + version badge)
- [x] Keep the **Dictation page's** subtitle text: "Transform your voice into text locally and securely. Experience near-instant transcription with zero data leakage."
- [x] Use the same subtitle styling (centered or left-aligned to match current Dictation layout)

### About Page — Subtitle Update
- [x] Replace the About page's current subtitle ("High-fidelity local transcription powered by the quiet engine of modern neural networks.") with the Dictation page's subtitle: "Transform your voice into text locally and securely. Experience near-instant transcription with zero data leakage."
- [x] Both pages now share the same subtitle text

## Technical Notes

- The chevron can use WPF UI's `ChevronLeft24` / `ChevronRight24` symbols
- Position the chevron using a vertically-centered element on the right edge of `NavPanel`, possibly overlapping or sitting just inside the sidebar border
- The logo SVG Canvas is already defined in both MainWindow.xaml (branding header) and AboutPage.xaml — consider extracting to a shared resource if duplication becomes unwieldy
- The sidebar branding header (logo + "WhisperHeim" text) in the sidebar itself can remain as-is or be simplified since the Dictation page now carries the full branding

## Open Questions

- None — all clarified during capture.

## Work Log

### 2026-03-25
**All acceptance criteria met.**

**Changes:**
1. **MainWindow.xaml**: Removed collapse button (icon + "COLLAPSE" text) from sidebar Row 2. Replaced with a 16x36px chevron button overlaid on the right edge of the sidebar column, vertically centered. Uses ChevronLeft24/ChevronRight24 symbols. Moved "About" nav item from main NavList to a separate NavBottomList pinned in Row 2 with margin separator.
2. **MainWindow.xaml.cs**: Updated `ApplySidebarCollapsedState` to use ChevronLeft24/ChevronRight24 instead of PanelLeftContract24/PanelLeftExpand24, removed SidebarToggleLabel reference. Added `NavBottomList_SelectionChanged` handler with mutual exclusion logic so selecting About deselects main nav and vice versa. Updated icon margin loop to include NavBottomList items.
3. **DictationPage.xaml**: Replaced "Dictation" title header with About-style branding: 88px logo with blue border, "WhisperHeim v1.0" title (40px ExtraBold + version badge), kept existing subtitle text left-aligned.
4. **AboutPage.xaml**: Replaced subtitle text with the shared subtitle: "Transform your voice into text locally and securely. Experience near-instant transcription with zero data leakage."

**Files changed:**
- `src/WhisperHeim/MainWindow.xaml`
- `src/WhisperHeim/MainWindow.xaml.cs`
- `src/WhisperHeim/Views/Pages/DictationPage.xaml`
- `src/WhisperHeim/Views/Pages/AboutPage.xaml`
