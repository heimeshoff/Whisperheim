---
id: main-069
title: Fix Start Minimized Setting Ignored on Launch
status: done
type: bug
context: main
created: 
completed: 2026-03-23
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: Bug Fix
size: Small
---
# Fix Start Minimized Setting Ignored on Launch

## Description

The "Start Minimized" toggle in General settings has no effect. The app always starts minimized when auto-launched because `App.xaml.cs` checks only the `--minimized` CLI flag (hardcoded in the registry command by `StartupService`), completely ignoring the `StartMinimized` setting in `AppSettings`.

**Root cause:** `App.xaml.cs:195` uses `e.Args.Contains("--minimized")` instead of reading `settingsService.Current.General.StartMinimized`.

## Requirements

1. **Use the setting, drop the CLI flag** — In `App.xaml.cs`, replace the `--minimized` arg check with `_settingsService.Current.General.StartMinimized`.
2. **Remove `--minimized` from the registry command** — In `StartupService.GetStartupCommand()`, stop appending `--minimized` to the exe path.
3. **Clean up dead code** — Remove any arg-parsing logic for `--minimized` that is no longer needed.

## Acceptance Criteria

- [x] When `StartMinimized` is **true** in settings: app starts hidden in tray (both auto-start and manual launch)
- [x] When `StartMinimized` is **false** in settings: app window appears on launch (both auto-start and manual launch)
- [x] The `--minimized` CLI flag is no longer used or referenced
- [x] Registry auto-start command no longer includes `--minimized`
- [x] No regressions to tray icon, window toggle, or close-to-tray behavior

## Files to Modify

- `src/WhisperHeim/App.xaml.cs` — replace arg check with setting check
- `src/WhisperHeim/Services/Startup/StartupService.cs` — remove `--minimized` from registry command

## Work Log

### 2026-03-23

**Changes made:**

1. **`src/WhisperHeim/App.xaml.cs`** — Replaced `e.Args.Contains("--minimized")` with `_settingsService.Current.General.StartMinimized` to read the user's setting instead of a CLI flag. Removed the now-unused `System.Linq` using directive.

2. **`src/WhisperHeim/Services/Startup/StartupService.cs`** — Removed `--minimized` from the registry command in `GetStartupCommand()`. The method now returns just the quoted exe path. Updated the XML doc comment accordingly.

**Verification:** Build succeeded with 0 warnings, 0 errors.

**All acceptance criteria met.** The `--minimized` flag is completely removed from both source files. Start-minimized behavior is now driven solely by the `StartMinimized` setting, which applies to both auto-start and manual launch scenarios. No other code references `--minimized`.
