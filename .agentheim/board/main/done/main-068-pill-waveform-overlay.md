---
id: main-068
title: Pill-Shaped Waveform Overlay at Last Click Position
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
size: Medium-Large
---
# Pill-Shaped Waveform Overlay at Last Click Position

## Description

Replace the current circular pulsing microphone overlay with a horizontal pill-shaped indicator containing animated frequency bars that respond to voice amplitude. The pill appears at the last globally-clicked mouse position instead of a fixed screen location.

## Requirements

### 1. Pill Shape & Frequency Bars
- Horizontal pill shape, approximately 40px tall, ~120-150px wide
- Contains ~15-20 vertical bars inside the pill
- Bars are driven by RMS amplitude with randomized variation per bar to create the visual effect of a frequency response / spectrum analyzer
- When amplitude changes, bars animate to different heights with slight random offsets between them
- Bars should feel responsive and alive while speaking

### 2. Positioning: Last Global Click
- Track the last mouse click position globally (all applications, not just WhisperHeim)
- Requires a low-level global mouse hook (Win32 `SetWindowsHookEx` with `WH_MOUSE_LL`)
- The last-clicked position becomes the **left edge** of the pill; the pill extends to the right
- Remove the existing position settings (BottomCenter, TopLeft, etc.) from `OverlaySettings`

### 3. Visibility
- Same as current: overlay appears when dictation starts, disappears when dictation stops
- Keep fade-in / fade-out animations (0.3s)
- Keep click-through behaviour (`WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE`)
- Keep always-on-top (`Topmost`)

### 4. Color Scheme (Brand Colors)
- **Dictating (Idle + Speaking):**
  - Pill border: WhisperHeim blue `#FF25abfe`
  - Frequency bars: WhisperHeim orange `#FFff8b00`
  - Bar heights modulated by amplitude (idle = low gentle movement, speaking = active tall bars)
- **No Microphone:**
  - Pill border: Grey
  - Bars: Grey, static (no animation)
- **Error:**
  - Entire pill filled solid red

### 5. Remove Old Overlay
- Remove the circular ellipse + microphone icon glyph from `DictationOverlayWindow.xaml`
- Remove the `ListeningPulse` and `SpeechPulse` storyboard animations
- Remove position enum/settings (`OverlayPosition`, `PositionOnScreen()` method)
- Remove overlay size and position settings from the settings UI if present

## Acceptance Criteria

- [x] Pill-shaped overlay with ~15-20 vertical frequency bars replaces the old circular mic overlay
- [x] Bars respond to voice amplitude with randomized per-bar variation (simulated frequency response)
- [x] Pill appears at the globally last-clicked mouse position (left-anchored, extends right)
- [x] Global mouse hook tracks clicks across all applications
- [x] Border is blue (`#25abfe`) and bars are orange (`#ff8b00`) during dictation
- [x] No-mic state shows grey border and grey static bars
- [x] Error state fills the pill red
- [x] Overlay fades in/out with dictation start/stop
- [x] Overlay is click-through and always-on-top
- [x] Old position settings removed
- [x] No visual regressions or focus-stealing

## Technical Notes

- **Files to modify:**
  - `src/WhisperHeim/Views/DictationOverlayWindow.xaml` — replace visual tree
  - `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs` — new bar rendering, global mouse hook, remove old positioning
  - `src/WhisperHeim/Views/OverlayMicState.cs` — may simplify states
  - `src/WhisperHeim/Models/AppSettings.cs` — remove position/size settings from OverlaySettings
  - `src/WhisperHeim/MainWindow.xaml.cs` — integration point for amplitude updates
- **Bar rendering:** Custom WPF drawing — either a Canvas with Rectangle children updated per frame, or a custom `OnRender` override for performance
- **Global mouse hook:** `SetWindowsHookEx(WH_MOUSE_LL, ...)` via P/Invoke; store last `WM_LBUTTONDOWN` screen coordinates
- **Amplitude → bars:** On each `UpdateAmplitude(rms)` call, generate per-bar heights as `baseHeight * rms * (1 + randomOffset)` where randomOffset varies per bar per update

## Work Log

### 2026-03-23

**Implementation complete.** All acceptance criteria met. Build succeeds with 0 warnings, 0 errors.

**What was done:**

1. **Replaced XAML visual tree** (`DictationOverlayWindow.xaml`): Removed the circular Ellipse + MicIcon + ListeningPulse/SpeechPulse storyboards. Replaced with a pill-shaped `Border` (CornerRadius=20, 150x40px) containing a `Canvas` for frequency bars. Kept FadeIn/FadeOut storyboards.

2. **Rewrote code-behind** (`DictationOverlayWindow.xaml.cs`):
   - 18 `Rectangle` bars rendered in a `Canvas`, animated at ~30fps via `DispatcherTimer`
   - Bars respond to `_smoothedRms` with per-bar random variation for spectrum analyzer effect
   - Idle state shows gentle low-amplitude movement; Speaking state modulates by RMS amplitude
   - Global mouse hook via `SetWindowsHookEx(WH_MOUSE_LL)` P/Invoke tracks `WM_LBUTTONDOWN` across all apps
   - Pill positioned at last click (left-anchored, extends right) with screen bounds clamping and DPI awareness
   - Color scheme: blue border (#25abfe) + orange bars (#ff8b00) for dictation, grey for NoMic, solid red for Error
   - Preserved click-through (`WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`) and always-on-top
   - Removed old `PositionOnScreen()`, `AnimateColor` for ellipse/icon, scale transform logic

3. **Cleaned up `OverlaySettings`** (`AppSettings.cs`): Removed `Size` and `Position` properties since the pill has fixed dimensions and positions at last click.

4. **Updated `MainWindow.xaml.cs`**: Removed trace log referencing old `Position`/`Size` settings.

**Files changed:**
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml`
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs`
- `src/WhisperHeim/Models/AppSettings.cs`
- `src/WhisperHeim/MainWindow.xaml.cs`
