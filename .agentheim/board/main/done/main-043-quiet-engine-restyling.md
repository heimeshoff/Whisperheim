---
id: main-043
title: Faithful Quiet Engine Restyling (All Pages)
status: done
type: feature
context: main
created: 2026-03-22
completed: 2026-03-22
commit:
depends_on: [main-040]
blocks: []
tags: [m5]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - UI Redesign
size: X-Large
---
# Faithful Quiet Engine Restyling (All Pages)

## Objective

The previous restyling pass (Task 040 Phase 3) applied only superficial changes. The pages do not match the inspiration designs. This task is a faithful, pixel-level restyling of every page to match the HTML/PNG inspiration mockups and the Quiet Engine design system (`inspiration/fluent_whisper/DESIGN.md`).

## What went wrong in Task 040

1. **No bento grid layouts** — pages use flat StackPanels instead of weighted Grid columns (8-col + 4-col sidebar pattern)
2. **No gradient CTA buttons** — buttons are flat, should use `primary → primary-container` gradient
3. **Hotkey/info cards unstyled** — no `kbd` pill key caps, no shadow, no rounded-xl containers
4. **No "Quiet Engine" aesthetic** — missing ghost borders (15% opacity), ambient tinted shadows, uppercase tracking on labels, editorial typography
5. **Wrong background treatment** — brown/orange gradient instead of clean `#F9F9F9` surface
6. **Missing visual elements** — no audio level indicators, no waveform visualizer, no pulsing dot, no model info card
7. **Sidebar styling wrong** — rectangular highlight instead of pill-shaped, no uppercase tracking
8. **Typography flat** — page titles should be large (28-36px), bold, tight tracking

## Approach

For each page, the subagent MUST:
1. **Read the inspiration HTML** (`inspiration/<page>/code.html`) to understand the exact layout structure
2. **Read the inspiration screenshot** (`inspiration/<page>/screen.png`) to see the visual target
3. **Rewrite the XAML** to faithfully reproduce the layout using WPF Grid columns, not StackPanels
4. **Apply all Quiet Engine rules** from `inspiration/fluent_whisper/DESIGN.md`

## Page-by-Page Mapping

| Page | Inspiration Dir | Key Layout Features |
|------|----------------|-------------------|
| DictationPage | `dictation_home` | 12-col bento (8+4), mic selector card, hotkey card with kbd pills, gradient CTA, waveform bars, model info card |
| TemplatesPage | `templates` | Master-detail split, search bar, tag filter buttons, "New Template" gradient CTA |
| TranscriptsPage (Recordings) | `transcripts_library` | Top progress banner, search + filter row, speaker-colored transcript segments |
| TranscribeFilesPage (Transcriptions) | `transcribe_files` | Glowing drop zone, card-based results with progress indicators |
| TextToSpeechPage | `text_to_speech_voices` | Bento grid with voice clone sidebar, waveform visualizer, recording progress |
| GeneralPage (Settings) | `general_settings` | Toggle switches, appearance cards, language dropdown |
| AboutPage (Models) | `about_models` | Hero section, model list cards with download status and sizes |

## Global Style Rules (from DESIGN.md)

These MUST be applied consistently across all pages:

- **No-Line Rule:** NO 1px solid borders for sectioning. Use background color shifts only.
- **Surface hierarchy:** Background `#F9F9F9`, work area `#F3F3F3`, cards `#FFFFFF`, elevated `#F9F9F9` + blur
- **Ghost borders:** If needed, `outline-variant` at 15% opacity — felt, not seen
- **Gradient CTAs:** `LinearGradientBrush` from `#005FAA` to `#0078D4` (135°)
- **Ambient shadows:** Large, soft, primary-tinted: `rgba(0, 96, 171, 0.06)`
- **Typography:** Page titles 28-36px bold, -0.02em tracking. Section labels 10px uppercase, +0.05em tracking. Body text in `#404752`.
- **Cards:** 12px corner radius (`CornerRadius="12"`), no hard borders
- **Sidebar:** Pill-shaped active indicator (`CornerRadius="16"` or similar), uppercase nav labels
- **Spacing:** Use generous whitespace. Prefer spacing over separator lines.
- **Kbd pills:** For hotkey displays, use small rounded containers with bottom border to simulate physical keys

## Acceptance Criteria

- [x] DictationPage matches `dictation_home/screen.png` layout and styling
- [x] TemplatesPage matches `templates/screen.png` layout and styling
- [x] TranscriptsPage matches `transcripts_library/screen.png` layout and styling
- [x] TranscribeFilesPage matches `transcribe_files/screen.png` layout and styling
- [x] TextToSpeechPage matches `text_to_speech_voices/screen.png` layout and styling
- [x] GeneralPage matches `general_settings/screen.png` layout and styling
- [x] AboutPage matches `about_models/screen.png` layout and styling
- [x] Sidebar uses pill-shaped selection with uppercase tracking
- [x] All gradient CTAs use primary→primary-container gradient
- [x] No 1px borders used for sectioning anywhere
- [x] Ghost borders (15% opacity) where boundaries are needed
- [x] Ambient tinted shadows on floating cards
- [x] Build succeeds with 0 errors
- [x] No features removed or broken

## Notes

- This is purely visual — no feature changes, no new services, no model changes.
- The code-behind (.xaml.cs) files should generally NOT need changes unless event handlers need updating for renamed elements.
- Read the HTML inspiration files carefully — they contain the exact Tailwind classes that map to WPF properties.
- The inspiration uses Tailwind `grid-cols-12` — in WPF, approximate with proportional Grid columns (e.g., `8*` + `4*`).
- WPF doesn't have CSS `backdrop-filter` — skip blur effects, approximate with semi-transparent backgrounds.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22 — Faithful Quiet Engine Restyling Complete

**Pages restyled (8 files):**

1. **MainWindow.xaml** — Added WhisperHeim/Local-First AI branding header, expanded sidebar width to 200px, improved icon/text spacing, maintained pill-shaped nav items with blue active state.

2. **DictationPage.xaml** — Rewrote with 36px bold header, bento 8+4 grid layout, audio input device card with level indicators, warning card with tertiary-fixed colors, pulsing dot status indicator, gradient CTA "Stop Dictation" button, hotkey card with kbd pill key caps (bottom border simulating physical keys), model info card with GPU memory progress bar, ambient tinted shadows throughout.

3. **TemplatesPage.xaml** — Master-detail split with 300px template list panel, left-border active indicator on selected template, search bar, placeholder tag pills ({date}, {time}, {clipboard}), gradient CTA "Update Template" button, bottom bento row with "Magic Replacement" info card and gradient "New Template" card.

4. **TranscriptsPage.xaml** — Gradient status banner with pulse dot, search + filters row, 280px recent transcripts sidebar with card-based list items, white viewer panel with ambient shadow, speaker-colored transcript segments with timestamp display, styled Copy/Export action buttons.

5. **TranscribeFilesPage.xaml** — Full-width glowing drop zone with gradient border hint, 64px upload icon circle, gradient CTA "Browse Files" button, card-based results with file icon, progress bar, transcript preview in surface-container background, model info card at bottom.

6. **TextToSpeechPage.xaml** — Bento 8*+4* grid, white input workspace card with voice selector, text input with rounded corners, synthesis progress bar, circular Play/Stop controls, gradient "Save Audio" CTA, clone new voice sidebar with source toggle, level meter, progress bar, save button, library voices list.

7. **GeneralPage.xaml** — 36px "General" header, APP START section with icon, toggle cards for Start Minimized and Launch at Startup, bento row with Interface Language dropdown card and Appearance card (light/dark/system theme previews), Advanced Mode section with gradient CTA.

8. **AboutPage.xaml** — Hero section with gradient-bordered logo, large app name with version badge, bento grid with Philosophy card (Ethereal Precision) and AI Models card with status badges, bottom Platform/License info cards.

**Code-behind changes (1 file):**
- **DictationPage.xaml.cs** — Updated DeviceWarning references to use `DeviceWarningCard` for the container border visibility control.

**Design system rules applied consistently:**
- No 1px borders for sectioning (background color shifts only)
- Surface hierarchy: #F9F9F9 → #F3F3F3 → #FFFFFF
- Ghost borders at 15% opacity where needed
- Gradient CTAs from #005FAA to #0078D4
- Ambient primary-tinted shadows (rgba(0,96,171,0.06))
- 10px uppercase bold section labels
- 30-36px bold page titles
- Body text in #404752
- 12px corner radius on cards
- Kbd pill key caps with bottom border

**Build result:** 0 errors, 0 warnings
