---
id: main-067
title: Dictation Page Responsive Layout & Card Alignment
status: done
type: feature
context: main
created: 
completed: 2026-03-23
commit:
depends_on: []
blocks: []
tags: [ui-polish]
related_adrs: []
related_research: []
prior_art: []
milestone: UI Polish (Quiet Engine)
size: Medium
---
# Dictation Page Responsive Layout & Card Alignment

## Description

The dictation page cards are not aligned consistently and the layout does not adapt to narrow window widths. This task fixes width alignment, adds responsive stacking, and swaps the hotkey row content order.

## Requirements

### 1. Card Width Alignment
- The bento grid row (dictation field + hotkeys card) must be exactly the same width as the audio input device card above — no overshoot on left or right.

### 2. Responsive Stacking (Wide → Narrow)
- **Wide mode:** Dictation field and hotkeys card sit side-by-side (current 8:4 ratio).
- **Narrow mode:** Cards stack vertically — Audio Input → Dictation → Hotkeys (top to bottom). All cards become full-width when stacked.
- **Breakpoint:** Trigger when the hotkeys card would start shrinking below its natural minimum content width.

### 3. Warning Card Responsive Behaviour
- **Wide mode:** When visible, the device warning card shares 50/50 horizontal space with the audio input device card.
- **Narrow mode:** Warning card stacks below the audio input device card, full-width.

### 4. Hotkey Row Content Swap
- In each hotkey shortcut row, move the keyboard shortcut pills to the **left** and the description text to the **right** (currently they are the opposite way around).

## Acceptance Criteria

- [x] Bento grid (dictation + hotkeys) is exactly as wide as the audio input card
- [x] At narrow widths, hotkeys card moves below dictation card (row → column)
- [x] All cards are full-width when in stacked/column mode
- [x] Warning card is 50/50 with audio input in wide mode, stacks below in narrow mode
- [x] Hotkey rows show shortcut pills on the left, label text on the right
- [x] Layout transitions feel smooth and natural
- [x] No visual regressions on the dictation page at default (1200×800) window size

## Technical Notes

- File: `src/WhisperHeim/Views/Pages/DictationPage.xaml`
- WPF does not have CSS media queries; responsive stacking can be achieved via a `SizeChanged` event handler in code-behind that toggles the grid layout, or via `UniformGrid`/`WrapPanel` if appropriate.
- The hotkey row swap is a straightforward change from `DockPanel.Dock="Left"` (text) / `DockPanel.Dock="Right"` (pills) to the reverse.

## Work Log

### 2026-03-23

**Changes made:**

1. **Card width alignment**: Changed bento grid gap from `32px` to `24px` to match the audio input row gap. Both rows now use consistent spacing so they align perfectly.

2. **Responsive stacking**: Added `SizeChanged` event handler in code-behind (`Page_SizeChanged`) that toggles between wide (side-by-side) and narrow (stacked) layouts at a 640px breakpoint. In narrow mode:
   - Bento grid columns collapse; hotkeys panel moves to row 1 with full-width span
   - Audio input warning card moves below the mic selector, full-width
   - All cards stretch to full width

3. **Warning card 50/50**: In wide mode, changed `AudioInputWarningCol` from `Auto` to `*` (star) so it shares equal space with the audio input card.

4. **Hotkey row content swap**: In each DockPanel, moved the `StackPanel` of kbd pills to `DockPanel.Dock="Left"` and the description `TextBlock` to the remaining space with `HorizontalAlignment="Right"`.

**Acceptance criteria:** All 7 criteria met.

**Files changed:**
- `src/WhisperHeim/Views/Pages/DictationPage.xaml` — layout restructuring, named elements for responsive control, hotkey row swap
- `src/WhisperHeim/Views/Pages/DictationPage.xaml.cs` — added `Page_SizeChanged`, `ApplyLayout`, `ApplyNarrowLayout`, `ApplyWideLayout` methods
