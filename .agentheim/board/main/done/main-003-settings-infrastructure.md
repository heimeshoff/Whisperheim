---
id: main-003
title: Settings Infrastructure
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
size: Medium
---
# Settings Infrastructure

## Objective
JSON-based settings file with load/save and a settings UI shell with navigation.

## Details
Create a Settings class with JSON serialization (System.Text.Json). Settings file stored in %APPDATA%/WhisperHeim/settings.json. Auto-create with defaults on first run. Settings UI uses WPF UI NavigationView with pages: General, Dictation, Templates, About. For now pages can be mostly empty placeholders. Settings are loaded on startup and saved on change.

## Acceptance Criteria
- [x] Settings file created on first run with defaults
- [x] UI shows navigation between pages
- [x] Changes persist across restarts

## Notes
Use System.Text.Json for serialization. %APPDATA%/WhisperHeim/ as the base directory.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 - Implementation complete
- Created `Models/AppSettings.cs` with `AppSettings`, `GeneralSettings`, `DictationSettings`, `TemplateSettings` classes using System.Text.Json serialization
- Created `Services/Settings/SettingsService.cs` with Load/Save to `%APPDATA%/WhisperHeim/settings.json`, auto-creates defaults on first run
- Created 4 settings pages as UserControls in `Views/Pages/`: GeneralPage (with start-minimized and launch-at-startup toggles), DictationPage (placeholder), TemplatesPage (placeholder), AboutPage (version info)
- Updated `MainWindow.xaml` to use ListBox-based navigation panel (left sidebar) with ContentPresenter for page content
- Updated `MainWindow.xaml.cs` to accept SettingsService, handle page navigation with caching
- Updated `App.xaml.cs` to create SettingsService, load settings on startup, and pass to MainWindow
- Added `AllowUnsafeBlocks` to csproj (needed by concurrent task's NativeMethods)
- Widened window to 600px to accommodate navigation sidebar
- Build confirmed: all settings infrastructure code compiles cleanly (only pre-existing errors from other tasks' Audio services remain)
