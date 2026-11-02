---
id: main-080
title: Drawer -- Remove Overlay, Crossfade Between Recordings
status: done
type: feature
context: main
created: 
completed: 2026-03-25
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
# Drawer -- Remove Overlay, Crossfade Between Recordings

## Description

Improve the Notion-style detail drawer on the Transcripts page:

1. **Remove dark overlay:** The semi-transparent black overlay (`#33000000`) behind the drawer is unnecessary. The drawer's existing drop shadow provides enough depth cue. Remove the overlay entirely so the recording list stays fully visible and interactive while the drawer is open.

2. **Click-through to switch recordings:** When the drawer is open and the user clicks a different recording in the list, crossfade the drawer content to show the newly selected recording. Do not close-then-reopen the drawer -- animate a quick crossfade of the content in place.

3. **Close behavior:** With no overlay to click, the drawer closes via:
   - The close button (already exists)
   - The Escape key (already exists)
   - No other close triggers needed

## Acceptance Criteria

- [x] Dark overlay border (`DrawerOverlay`) is removed from XAML
- [x] Clicking a recording while the drawer is open crossfades to the new recording's content (no slide-out/slide-in, no close/reopen)
- [x] Drawer still slides in on first open and slides out on close
- [x] Close button and Escape key still close the drawer
- [x] List items remain clickable while drawer is open
- [x] Drop shadow on the drawer panel is preserved as the depth cue

## Technical Notes

**Files to modify:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` -- Remove `DrawerOverlay` border (line ~360), remove `DrawerOverlay_Click` handler reference
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` -- Remove `DrawerOverlay_Click`, update `OpenTranscriptDrawer`/`AnimateDrawer`/`CloseDrawer` to skip overlay visibility toggling, add content crossfade animation (opacity fade on drawer content)

**Crossfade approach:**
- When drawer is already open and a new recording is clicked, fade current content opacity to 0 (~150ms), swap content, fade back to 1 (~150ms)
- Use `DoubleAnimation` on the content panel's `Opacity` property

## Dependencies

- Task 071 (Notion-style list view with detail drawer) -- done

## Work Log

### 2026-03-25
- Removed `DrawerOverlay` Border element from `TranscriptsPage.xaml` (overlay + click handler reference)
- Removed `DrawerOverlay_Click` method from code-behind
- Removed all `DrawerOverlay.Visibility` assignments from `OpenTranscriptDrawer`, `AnimateDrawer`, and active recording drawer open
- Added crossfade logic in `OpenTranscriptDrawer`: when drawer is already visible, fades `TranscriptDrawerContent` opacity to 0 (150ms), swaps content, fades back to 1 (150ms) -- no slide animation
- First open still uses slide-in animation; close still uses slide-out
- Close button and Escape key unchanged
- Drop shadow on `DrawerPanel` preserved
- Build succeeds with 0 errors
