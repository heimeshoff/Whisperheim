---
id: main-012
title: Dictation Overlay
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-011]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Medium
---
# Dictation Overlay

## Objective
Show a small, animated on-screen indicator during active dictation that is visible but non-intrusive.

## Details
Create a small always-on-top, click-through WPF window (borderless, transparent background). Position near the bottom-center of the screen (or configurable). Show a pulsing microphone icon or animated waveform visualization during recording. Use subtle animation -- gentle pulse or breathing effect when listening, faster animation when speech is detected. The overlay should be tiny (~40-60px). Fade in on dictation start, fade out on stop. Make it configurable: enable/disable, position, size, opacity. Should not steal focus or interfere with typing.

## Acceptance Criteria
- [x] Overlay appears on dictation start
- [x] Animates during speech
- [x] Fades on stop
- [x] Does not steal focus
- [x] Does not block clicks
- [x] Is toggleable in settings

## Notes
Click-through via WS_EX_TRANSPARENT extended window style. ~40-60px size. Configurable position and opacity.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete

**Files created:**
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml` — XAML layout with pulsing/speech storyboard animations, fade in/out, ellipse + microphone icon
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs` — Code-behind with WS_EX_TRANSPARENT/WS_EX_NOACTIVATE/WS_EX_TOOLWINDOW for click-through and no-focus; screen positioning; animation control (listening pulse vs speech pulse)
- `src/WhisperHeim/Models/AppSettings.cs` — AppSettings model including new `OverlaySettings` class (Enabled, Opacity, Size, Position)

**Files modified:**
- `src/WhisperHeim/MainWindow.xaml.cs` — Wired overlay to DictationOrchestrator state callback: ShowOverlay on start, HideOverlay on stop. Hooked IDictationPipeline.PartialResult to trigger fast speech animation with 1.5s pause timer to revert. Cleanup on exit.
- `src/WhisperHeim/WhisperHeim.csproj` — Added AllowUnsafeBlocks (needed for pre-existing LibraryImport in NativeMethods.cs)

**Design decisions:**
- Overlay uses DllImport (classic P/Invoke) for GetWindowLong/SetWindowLong to set click-through styles
- Two animation states: gentle 1.2s breathing pulse when listening, fast 0.4s pulse when speech detected (triggered by partial results)
- Speech pause timer (1.5s) automatically reverts from fast to gentle animation when no partial results arrive
- Overlay is 48px default, positioned at bottom-center of work area with 20px margin
- All settings configurable via OverlaySettings: enable/disable, opacity (0-1), size, position (6 named positions)
