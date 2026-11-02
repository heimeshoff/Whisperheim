---
id: main-046
title: Remember Window Size and Position
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
# Remember Window Size and Position

## Objective
Set the default window size to 1200x800 centered on screen, and persist window size/position across restarts with a safety check for off-screen positions.

## Details
1. **New default size:** Change MainWindow from 600x500 to 1200x800, centered on screen.
2. **Persist size and position:** Save window Left, Top, Width, Height to settings.json on close. Restore on startup.
3. **Off-screen guard:** On startup, if the saved position would place the window mostly or entirely outside any connected monitor's work area, discard the saved position and fall back to centered on the primary screen at default size.

Key files:
- `src/WhisperHeim/MainWindow.xaml` — default size and startup location
- `src/WhisperHeim/MainWindow.xaml.cs` — save on close, restore on load
- `src/WhisperHeim/Models/AppSettings.cs` — add window position/size fields
- `src/WhisperHeim/Services/Settings/SettingsService.cs` — persistence

## Acceptance Criteria
- [x] Default window size is 1200x800
- [x] Window starts centered on screen on first launch
- [x] Window size and position are saved when the app closes
- [x] On next launch, the window restores to the saved size and position
- [x] If saved position is off-screen (e.g., monitor disconnected), window resets to centered at default size
- [x] Maximized state is also remembered

## Notes
Use `System.Windows.Forms.Screen.AllScreens` or `System.Windows.SystemParameters` to check if saved bounds intersect any monitor's work area.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22 — Implementation complete
- Changed default window size from 600x500 to 1200x800 in `MainWindow.xaml`
- Set `WindowStartupLocation="Manual"` to allow code-based positioning
- Added `WindowSettings` class to `AppSettings.cs` with Left, Top, Width, Height, and IsMaximized fields
- Added `RestoreWindowPosition()` to restore saved position on startup, with off-screen guard using Win32 `EnumDisplayMonitors`/`GetMonitorInfo` P/Invoke
- Added `SaveWindowPosition()` called in `OnClosing` to persist window bounds (uses `RestoreBounds` when maximized)
- Falls back to centered on primary screen at default size when no saved position exists or saved position is off-screen
- Build passes with 0 errors, 0 warnings
