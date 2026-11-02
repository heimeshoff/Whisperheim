---
id: main-002
title: Tray Icon and Window
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-001]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Small
---
# Tray Icon and Window

## Objective
App runs as a tray icon with a show/hide settings window and exit context menu.

## Details
Configure WPF-UI.Tray NotifyIcon in App.xaml. Create a FluentWindow with Mica backdrop as the main settings window. Tray icon left-click toggles window visibility. Right-click context menu with "Settings" and "Exit" items. Window close hides to tray instead of exiting. App icon should be a simple microphone glyph from Segoe Fluent Icons. Window title: "WhisperHeim".

## Acceptance Criteria
- [x] App starts minimized to tray
- [x] Tray icon visible
- [x] Left-click shows/hides window
- [x] Right-click menu works
- [x] Exit quits the app
- [x] Window has Mica backdrop

## Notes
Use Segoe Fluent Icons for the microphone glyph tray icon.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 - Implementation complete
- Converted `MainWindow.xaml` from plain `Window` to `FluentWindow` with `WindowBackdropType="Mica"` and `ExtendsContentIntoTitleBar="True"`
- Added `tray:NotifyIcon` control with `MenuOnRightClick`, `LeftClick` toggle handler, and context menu (Settings + Exit)
- Tray icon rendered at runtime from Segoe Fluent Icons microphone glyph (U+E720) via `DrawingVisual`/`RenderTargetBitmap`
- Updated `App.xaml` to use WPF-UI theme resources (`ThemesDictionary`, `ControlsDictionary`) and removed `StartupUri` in favor of `Startup` event
- `App.xaml.cs` creates `MainWindow` programmatically on startup
- Window starts hidden (`Visibility.Hidden`, `ShowInTaskbar=false`); closing hides to tray unless Exit is chosen
- Build succeeds with 0 warnings, 0 errors
