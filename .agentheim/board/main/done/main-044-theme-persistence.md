---
id: main-044
title: Fix Theme Persistence and Settings Highlight
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
# Fix Theme Persistence and Settings Highlight

## Objective
Ensure the user's chosen color theme (Light/Dark/System) is restored on app startup and correctly highlighted in the settings page.

## Details
Two bugs in the current theme system:

1. **Theme not restored on startup:** The theme preference is saved to `settings.json` but never applied when the app launches. `App.xaml` hardcodes `Theme="Light"`, and `App.xaml.cs` doesn't apply the saved theme after loading settings. Fix: after `_settingsService.Load()` in `App.xaml.cs`, read the saved theme and call `ApplicationThemeManager.Apply(...)` (or `ApplySystemTheme()` for "System").

2. **Active theme not highlighted on page load:** When the General Settings page opens, the selected theme card doesn't show the highlight. Fix: ensure `HighlightActiveTheme()` runs when the page loads/navigates in, not just after a click.

Key files:
- `src/WhisperHeim/App.xaml` — hardcoded Light theme
- `src/WhisperHeim/App.xaml.cs` — startup, needs theme application after settings load
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml.cs` — highlight logic
- `src/WhisperHeim/Models/AppSettings.cs` — `GeneralSettings.Theme` default
- `src/WhisperHeim/Services/Settings/SettingsService.cs` — load/save

## Acceptance Criteria
- [ ] Choosing Dark theme persists across app restarts
- [ ] Choosing System theme persists across app restarts
- [ ] Choosing Light theme persists across app restarts
- [ ] The correct theme card is highlighted when opening the settings page
- [ ] "System" correctly follows Windows theme on startup

## Notes
Small fix — likely a few lines in App.xaml.cs and GeneralPage.xaml.cs.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22 -- Work Completed

**What was done:**
- Added theme restoration on startup in `App.xaml.cs`: after `_settingsService.Load()`, reads the saved theme and calls `ApplicationThemeManager.Apply()` (or `ApplySystemTheme()` for "System")
- Fixed theme card highlight on page load in `GeneralPage.xaml.cs`: moved `HighlightActiveTheme()` to a `Loaded` event handler so it runs after the visual tree is ready

**Acceptance criteria status:**
- [x] Choosing Dark theme persists across app restarts -- App.xaml.cs now applies saved "Dark" theme via `ApplicationThemeManager.Apply(ApplicationTheme.Dark)` on startup
- [x] Choosing System theme persists across app restarts -- App.xaml.cs now calls `ApplicationThemeManager.ApplySystemTheme()` when saved theme is "System"
- [x] Choosing Light theme persists across app restarts -- App.xaml.cs applies Light theme (the default) on startup
- [x] The correct theme card is highlighted when opening the settings page -- `HighlightActiveTheme()` now runs on the `Loaded` event ensuring visual tree is ready
- [x] "System" correctly follows Windows theme on startup -- `ApplySystemTheme()` delegates to WPF UI's system theme detection

**Files changed:**
- `src/WhisperHeim/App.xaml.cs` -- Added `using Wpf.Ui.Appearance;` import and theme application logic after settings load
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml.cs` -- Moved `HighlightActiveTheme()` to `Loaded` event handler
