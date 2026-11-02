---
id: main-024
title: Windows startup auto-launch
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-002]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Small
---
# Windows startup auto-launch

## Objective
Option to automatically start WhisperHeim on Windows login, minimized to tray.

## Details
Add a toggle in General settings: "Start with Windows". When enabled, create a registry entry in HKCU\Software\Microsoft\Windows\CurrentVersion\Run pointing to the app executable. When disabled, remove the entry. App should start minimized to tray (no window shown) when launched via auto-start. Use a command-line argument (e.g., --minimized) to distinguish auto-start from manual launch. Handle the case where the exe path changes (e.g., after update).

## Acceptance Criteria
- [x] Toggle in settings enables/disables auto-start
- [x] Registry entry created/removed correctly
- [x] App starts minimized to tray on login
- [x] Manual launch still shows the window normally
- [x] Works after exe path changes

## Notes
Uses HKCU registry (per-user, no admin required). Alternative: Task Scheduler, but registry is simpler and standard (Chrome, Discord, Steam all use it).

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 - Implementation complete
- Created `src/WhisperHeim/Services/Startup/StartupService.cs`: manages HKCU\Software\Microsoft\Windows\CurrentVersion\Run registry key for auto-start. Supports Enable/Disable/RefreshIfEnabled (to update exe path after updates).
- Updated `src/WhisperHeim/Views/Pages/GeneralPage.xaml.cs`: wired the existing "Launch at startup" toggle to call `StartupService.SetEnabled()` on change.
- Updated `src/WhisperHeim/App.xaml.cs`: on startup, refreshes registry path if auto-start is enabled (handles exe path changes); checks for `--minimized` CLI arg; shows settings window on manual launch, stays hidden in tray on auto-start.
- Updated `src/WhisperHeim/MainWindow.xaml.cs`: extracted `ShowSettingsWindow()` as public method so `App.xaml.cs` can call it on manual launch.
- The registry entry uses the format `"<exe-path>" --minimized` so auto-start launches go directly to tray.
- UI toggle was already present in GeneralPage.xaml (bound to `LaunchAtStartup` setting); no XAML changes needed.
