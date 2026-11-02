---
id: main-070
title: Fix Pill Overlay Visualization
status: done
type: bug
context: main
created: 2026-03-23
completed: 2026-03-23
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: Bug Fix
size: Medium
---
# Fix Pill Overlay Visualization

## Problem

The pill-shaped dictation overlay (added in Task 068, commit `c2f5d65`) has two bugs that have been present since it was built:

1. **Bars are invisible** — The pill appears but shows no frequency bars inside it. Audio capture works, but nothing is rendered visually.
2. **Border is always blue** — The pill border stays blue regardless of mic/speech state. Both `Idle` and `Speaking` states use `BlueBorderColor` in `SetMicState()`.

## Root Cause Analysis

### Bars not visible
- `DictationOverlayWindow.xaml`: Window is 150x40, Border has `Padding="8,4"`, `BorderThickness="2"`, `CornerRadius="20"`. The inner Canvas (`BarsCanvas`) may have 0 actual dimensions, or bars are too small/clipped.
- `InitializeBars()` creates 18 rectangles with orange fill. `LayoutBars()` checks `canvasWidth <= 0 || canvasHeight <= 0` and returns early — if Canvas never gets sized, bars never get laid out.
- Verify that `BarsCanvas.ActualWidth` and `ActualHeight` are non-zero after layout.

### Always blue border
- `SetMicState()` lines 320-325: both `Idle` and `Speaking` cases animate to `BlueBorderColor` and set bars to `OrangeBarColor`. There is no visual distinction between the two states.
- XAML default for `PillBorderBrush` is `#FF25abfe` (blue), so even before any state change it appears blue.

## Acceptance Criteria

- [ ] **Grey border + grey/muted bars** when idle (mic active, no speech detected)
- [ ] **Blue border + orange animated bars** when speech is detected — bars respond to voice volume
- [ ] **Grey border + static grey bars** when mic is muted or unavailable (`NoMic` state)
- [ ] **Red fill** for error state (already implemented, verify it works)
- [ ] Bars are actually visible inside the pill and animate smoothly
- [ ] No regression in click-through, positioning, or fade in/out behavior

## Key Files

- `src/WhisperHeim/Views/DictationOverlayWindow.xaml` — pill layout, Canvas, default colors
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs` — bar init, animation tick, state management
- `src/WhisperHeim/Views/OverlayMicState.cs` — state enum
- `src/WhisperHeim/MainWindow.xaml.cs` — amplitude callback wiring (~line 303)

## Implementation Notes

- Fix Canvas sizing: may need explicit `HorizontalAlignment="Stretch"` / `VerticalAlignment="Stretch"` or switch to a layout panel that reports size correctly
- Update `SetMicState(Idle)` to use grey border + grey bars instead of blue/orange
- Update XAML default `PillBorderBrush` to grey so it starts in the correct state
- Consider increasing pill size or bar dimensions if they're too small to see even when properly sized

### 2026-03-23 -- Work Completed

**What was done:**
- Fixed invisible bars by wrapping the Canvas in a Grid (`BarsGrid`). A WPF Canvas inside a Border reports 0x0 ActualWidth/Height; the Grid stretches to fill available space and its dimensions are propagated to the Canvas via `SizeChanged`.
- Fixed always-blue border: separated `Idle` and `Speaking` cases in `SetMicState()`. Idle now uses grey border + grey bars; Speaking uses blue border + orange bars.
- Changed XAML default `PillBorderBrush` from blue (`#FF25abfe`) to grey (`#FF999999`) so the pill starts in the correct idle appearance.
- Changed initial bar fill color from orange to grey to match idle state.
- Fixed initial state application: `ShowOverlay()` now sets a sentinel state before calling `SetMicState(Idle)` so the early-return guard doesn't skip the first state setup.
- Updated docstring to reflect corrected idle state colors.

**Acceptance criteria status:**
- [x] Grey border + grey/muted bars when idle -- Idle case now uses `GreyColor` for both border and bars; XAML default is grey
- [x] Blue border + orange animated bars when speech detected -- Speaking case uses `BlueBorderColor` + `OrangeBarColor`; bars respond to `_smoothedRms`
- [x] Grey border + static grey bars when mic muted/unavailable -- NoMic case unchanged, uses `GreyColor` for border and bars
- [x] Red fill for error state -- Error case unchanged, uses `RedColor`; verified in code review
- [x] Bars are actually visible inside the pill and animate smoothly -- Canvas now gets real dimensions from Grid wrapper; `LayoutBars()` and `OnBarAnimationTick()` use `BarsCanvas.Width/Height` instead of `ActualWidth/ActualHeight`
- [x] No regression in click-through, positioning, or fade in/out behavior -- No changes to click-through, positioning, or fade logic; build succeeds with 0 warnings

**Files changed:**
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml` -- Wrapped Canvas in Grid; changed default border color to grey
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs` -- Fixed canvas sizing via Grid propagation; separated Idle/Speaking color states; fixed initial state application; updated bar init color and docstring
