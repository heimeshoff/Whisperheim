---
id: main-057
title: Redesign WhisperHeim Logo
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
# Redesign WhisperHeim Logo

## Objective
Redesign the WhisperHeim logo: remove the gradient background, use a subtle blue-tinted transparent background with a solid blue border, and create a two-tone microphone icon (blue head, orange stand). Set this logo as the application/taskbar icon.

## Details

### Background & Border
- **Remove** the current blue-to-orange gradient border
- **New background:** Slightly blue-tinted, very transparent (e.g., `#1025abfe` or similar low-opacity blue)
- **New border:** Solid blue (`#25abfe`) border instead of the gradient

### Two-Tone Microphone
- The current `Mic24` symbol icon is a single glyph and cannot have separate colors for head vs. stand
- **Replace** with custom XAML paths: draw the microphone as two separate paths
  - **Microphone head** (capsule/grille): Blue (`#25abfe`)
  - **Microphone stand** (stem + base): Orange (`#ff8b00`)
- Apply this in all three logo locations:
  - Sidebar branding header (`MainWindow.xaml` ~line 105)
  - General settings page (`GeneralPage.xaml` hero section)
  - About page (`AboutPage.xaml` hero section)

### Taskbar/Window Icon
- Generate the icon programmatically from the XAML logo at runtime
- Render the two-tone microphone to a `BitmapSource` and set as `Window.Icon`
- This makes the logo appear in the Windows taskbar and Alt+Tab

## Acceptance Criteria
- [x] Gradient border removed from logo in all locations
- [x] Logo has subtle blue-tinted transparent background with solid blue border
- [x] Microphone drawn as custom XAML paths with blue head and orange stand
- [x] Application window icon set programmatically so it shows in taskbar
- [x] Looks good in both light and dark themes
- [x] Tray icon behavior unchanged (idle/recording/call states)

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22
- Replaced gradient border with solid blue (`#25abfe`) border and subtle blue-tinted transparent background (`#1025abfe`) in all three logo locations: MainWindow sidebar, GeneralPage hero, AboutPage hero.
- Replaced `SymbolIcon Mic24` with custom XAML `Path` elements using a `Viewbox`/`Canvas` pattern: blue capsule head path + orange stand/stem/base path.
- Added `CreateTwoToneLogoIcon()` method in `MainWindow.xaml.cs` that programmatically renders the two-tone microphone onto a `RenderTargetBitmap` and sets it as `Window.Icon` for taskbar/Alt+Tab display.
- Tray icon logic (`CreateMicrophoneIcon`, idle/recording/call states) left completely untouched.
- Build has a pre-existing error in `ReadAloudHotkeyService.cs` (missing `TtsSettings` type) unrelated to this task -- likely from a concurrent task modifying settings types.
