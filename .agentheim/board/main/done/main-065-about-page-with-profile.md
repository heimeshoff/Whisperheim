---
id: main-065
title: About Page with Profile, Contact Links & Ko-fi
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
size: Small
---
# About Page with Profile, Contact Links & Ko-fi

## Description
Add an About page to WhisperHeim modeled after the VocalFold about page. The page already exists (`Views/Pages/AboutPage.xaml`) with app philosophy, AI models, and platform info, but is not wired into the sidebar navigation and lacks the personal profile section.

Update the page to include:
- **Profile section**: Profile photo (heimeshoff.jpg), Marco Heimeshoff's bio as DDD trainer/consultant/conference organiser, connection to why WhisperHeim exists
- **Contact links**: heimeshoff.de, Bluesky (@Heimeshoff.de), LinkedIn (linkedin.com/in/heimeshoff)
- **Ko-fi support link**: Prominent "Buy me a coffee" button linking to https://ko-fi.com/heimeshoff
- **GitHub link**: Link to WhisperHeim's GitHub repo

Wire the page into the sidebar navigation so it's accessible.

## Acceptance Criteria
- [x] About page is accessible from the sidebar navigation
- [x] Profile section with photo, name, and bio
- [x] Contact links: website, Bluesky, LinkedIn
- [x] Ko-fi support button
- [x] GitHub repo link
- [x] Profile image (heimeshoff.jpg) copied from VocalFold project into WhisperHeim assets
- [x] Existing About page content (philosophy, AI models, platform info) preserved
- [x] Consistent styling with the rest of the app (WPF Fluent Design, dark/light theme support)

## Technical Notes
- Existing AboutPage: `src/WhisperHeim/Views/Pages/AboutPage.xaml(.cs)`
- Profile image source: `C:\src\heimeshoff\VocalFold\VocalFold.WebUI\public\heimeshoff.jpg`
- Navigation wiring needed in `MainWindow.xaml` (sidebar ListBoxItem) and `MainWindow.xaml.cs` (NavigateTo switch)
- Reference design: VocalFold about page at `C:\src\heimeshoff\VocalFold\VocalFold.WebUI\src\Components\About.fs`

## Work Log

### 2026-03-22
- Copied `heimeshoff.jpg` from VocalFold project into `src/WhisperHeim/Assets/heimeshoff.jpg`
- Added `Resource` entry in `WhisperHeim.csproj` for the profile image
- Updated `AboutPage.xaml` with:
  - Profile section: circular photo with blue border, name, and bio text (DDD trainer/consultant/conference organiser)
  - Contact links: website (heimeshoff.de), Bluesky (@Heimeshoff.de with SVG icon), LinkedIn (with SVG icon)
  - Ko-fi "Buy me a coffee" button with blue gradient styling
  - GitHub repo link with star icon
  - All existing content preserved (hero, philosophy, AI models, platform, license cards)
- Updated `AboutPage.xaml.cs` with `Hyperlink_RequestNavigate` and `KofiButton_Click` event handlers
- Added "ABOUT" nav item with Info24 icon to sidebar in `MainWindow.xaml`
- Added `"About"` case to `NavigateTo` switch in `MainWindow.xaml.cs`
- Added `NavLabelAbout` visibility toggle for sidebar collapse support
- All styling uses DynamicResource brushes for dark/light theme compatibility
- Build verified: no new compilation errors introduced (pre-existing errors in App.xaml.cs from concurrent task)

**All acceptance criteria met.**

Files changed:
- `src/WhisperHeim/Assets/heimeshoff.jpg` (new - copied from VocalFold)
- `src/WhisperHeim/WhisperHeim.csproj` (added Resource include)
- `src/WhisperHeim/Views/Pages/AboutPage.xaml` (profile, contacts, Ko-fi, GitHub)
- `src/WhisperHeim/Views/Pages/AboutPage.xaml.cs` (new event handlers)
- `src/WhisperHeim/MainWindow.xaml` (About nav item)
- `src/WhisperHeim/MainWindow.xaml.cs` (NavigateTo + sidebar collapse)
