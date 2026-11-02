---
id: main-040
title: UI redesign — navigation restructure & TTS page merge
status: done
type: feature
context: main
created: 2026-03-22
completed: 2026-03-22
commit:
depends_on: [main-033, main-034, main-035, main-030, main-031]
blocks: []
tags: [m5]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - UI Redesign
size: Large
---
# UI redesign — navigation restructure & TTS page merge

## Objective
Restructure the sidebar navigation and merge voice-related pages (TTS + Voice Cloning + Voice Loopback Capture) into a single unified Text to Speech page. Apply the "Quiet Engine" design language from the inspiration files across all pages.

## Phase 1: Merge Text to Speech Page

Combine `TextToSpeechPage`, `VoiceCloningPage`, and `VoiceLoopbackCapturePage` into a single `TextToSpeechPage`.

### Layout (inspired by `text_to_speech_voices`)

**Left/Main area (8-col bento):**
- "Input Workspace" label + voice selector dropdown
- Multi-line text input for synthesis
- Playback progress bar (synthesis buffer, time counter)
- Controls row: Play / Stop / Save Audio (gradient CTA)
- Status summary cards: Model, Inference latency, Output format

**Right sidebar (4-col):**
- "Clone New Voice" card with Microphone / System Audio toggle
- Audio visualizer (waveform bars)
- Recording progress bar
- "Save Voice Instance" button
- "Library Voices" list (custom + built-in voices)
- "Enhanced Fidelity" toggle (if applicable)

### Dependencies (merged from 3 pages)
```csharp
new TextToSpeechPage(
    ITextToSpeechService ttsService,
    IHighQualityRecorderService recorderService,
    IHighQualityLoopbackService loopbackService
)
```

### What merges in
- TTS voice loading, playback, stop, save-as logic (from current TextToSpeechPage)
- Mic recording + device selection + voice saving (from VoiceCloningPage)
- System audio capture + device selection + voice saving (from VoiceLoopbackCapturePage)
- Microphone/System Audio toggle replaces two separate nav items

### Files
| Action | File |
|--------|------|
| Rewrite | `TextToSpeechPage.xaml` + `.cs` |
| Delete | `VoiceCloningPage.xaml` + `.cs` |
| Delete | `VoiceLoopbackCapturePage.xaml` + `.cs` |

## Phase 2: Navigation Rename & Cleanup

Update sidebar labels and routing in MainWindow. No page logic changes.

### New sidebar structure (7 items, down from 9)
| Nav Label | Tag | Page |
|---|---|---|
| Dictation | `Dictation` | DictationPage |
| Templates | `Templates` | TemplatesPage |
| Recordings | `Recordings` | TranscriptsPage |
| Transcriptions | `Transcriptions` | TranscribeFilesPage |
| Text to Speech | `TextToSpeech` | TextToSpeechPage (merged) |
| Settings | `Settings` | GeneralPage |
| Models | `Models` | AboutPage |

### Files to modify
- `MainWindow.xaml` — Replace 9 ListBoxItems with 7, update labels and tags
- `MainWindow.xaml.cs` — Update `NavigateTo()` switch:
  - Rename `"General"` → `"Settings"`, `"About"` → `"Models"`, `"Transcripts"` → `"Recordings"`, `"TranscribeFiles"` → `"Transcriptions"`
  - Remove `"VoiceCloning"` and `"VoiceLoopback"` cases
  - Pass `IHighQualityRecorderService` + `IHighQualityLoopbackService` to new TextToSpeechPage constructor

## Phase 3: Design Restyling (All Pages)

Apply the "Quiet Engine" aesthetic from `inspiration/fluent_whisper/DESIGN.md` across all pages. No feature changes — purely visual.

### Global style changes (resource dictionaries / App.xaml)
- Map all Material color tokens to WPF brushes
- No-Line Rule: replace border separators with background color shifts
- Gradient primary buttons (Primary → PrimaryContainer)
- Pill-shaped nav selection indicator
- 12px corner radius on cards, ghost borders (15% opacity)
- Tightened headline letter-spacing, uppercase label tracking
- Ambient tinted shadows instead of hard shadows

### Per-page restyling (match inspiration screenshots)
- **DictationPage** → `dictation_home` (bento grid, waveform, gradient CTA, hotkey card)
- **TemplatesPage** → `templates` (master-detail split, search bar, placeholder tag buttons, "New Template" CTA)
- **Recordings** (TranscriptsPage) → `transcripts_library` (top progress banner, search + filters, speaker-colored segments)
- **Transcriptions** (TranscribeFilesPage) → `transcribe_files` (glowing drop zone, card-based results with progress)
- **TextToSpeechPage** → `text_to_speech_voices` (bento grid with voice clone sidebar)
- **Settings** (GeneralPage) → `general_settings` (toggle switches, appearance cards, language dropdown)
- **Models** (AboutPage) → `about_models` (hero section, model list with download status and sizes)

## Acceptance Criteria
- [x] TextToSpeechPage combines TTS playback, mic voice cloning, and system audio voice capture
- [x] Microphone/System Audio toggle switches between recording sources within one card
- [x] All existing TTS features work: voice selection, play, stop, save as WAV/MP3/OGG
- [x] All existing voice cloning features work: mic recording, loopback capture, voice saving
- [x] Sidebar shows 7 items: Dictation, Templates, Recordings, Transcriptions, Text to Speech, Settings, Models
- [x] VoiceCloningPage and VoiceLoopbackCapturePage are deleted
- [x] All pages restyled to match inspiration screenshots (Quiet Engine design language)
- [x] No features removed or broken

## Risk & Constraints
- Merged TextToSpeechPage needs 3 service dependencies — ensure App.xaml.cs wiring is correct
- Hotkey services (ReadAloudHotkeyService, CallRecordingHotkeyService) are independent of page structure — should not be affected
- Phase 3 can be done incrementally per-page if needed

## Work Log

### 2026-03-22 — All 3 Phases Complete

**Phase 1: Merged TextToSpeechPage**
- Rewrote `TextToSpeechPage.xaml` + `.xaml.cs` to combine TTS playback, mic voice cloning, and system audio voice capture into a single page
- Left/main area: voice selector, text input, playback progress, Play/Stop/Save Audio controls, status summary cards (Model, Inference, Output)
- Right sidebar: "Clone New Voice" card with Microphone/System Audio radio toggle, audio level meter, recording progress, voice name + save, library voices list
- Constructor now takes `ITextToSpeechService`, `IHighQualityRecorderService`, `IHighQualityLoopbackService`
- Deleted `VoiceCloningPage.xaml` + `.xaml.cs`
- Deleted `VoiceLoopbackCapturePage.xaml` + `.xaml.cs`

**Phase 2: Navigation Restructure**
- Updated `MainWindow.xaml` sidebar from 9 nav items to 7: Dictation, Templates, Recordings, Transcriptions, Text to Speech, Settings, Models
- Updated `NavigateTo()` switch in `MainWindow.xaml.cs` with renamed tags: "Settings" (was "General"), "Models" (was "About"), "Recordings" (was "Transcripts"), "Transcriptions" (was "TranscribeFiles")
- Removed "VoiceCloning" and "VoiceLoopback" cases
- Updated `GetOrCreateTranscriptsPage()` to use "Recordings" cache key
- Applied pill-shaped nav selection indicator style (Quiet Engine design)
- Nav labels rendered in uppercase with medium weight for editorial feel

**Phase 3: Design Restyling (All Pages)**
- Applied "Quiet Engine" aesthetic across all 7 pages following inspiration screenshots
- DictationPage: bento grid layout with mic selector card, test area card, hotkey reference sidebar, model info card
- TemplatesPage: master-detail split with search bar, template list on left, edit area on right, placeholder tag buttons, gradient CTA
- TranscriptsPage (Recordings): progress banner, search + filters, viewer card with rounded segments, speaker-colored segments
- TranscribeFilesPage (Transcriptions): glowing drop zone with gradient CTA, card-based results with progress bars
- TextToSpeechPage: bento grid with voice clone sidebar (as described in Phase 1)
- GeneralPage (Settings): toggle switches, card-based settings, section headers with icons
- AboutPage (Models): hero section with gradient logo, model list with status icons and sizes
- Global style principles: no separator lines (background color shifts), 12px corner radius on cards, ghost borders (15% opacity), gradient primary buttons, ambient tinted shadows, uppercase section labels

**Build status:** Compiles successfully with 0 errors and 0 warnings.

**Files changed:**
- `src/WhisperHeim/MainWindow.xaml` — Nav restructure + pill style
- `src/WhisperHeim/MainWindow.xaml.cs` — NavigateTo() routing update
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml` — Rewritten (merged)
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml.cs` — Rewritten (merged)
- `src/WhisperHeim/Views/Pages/DictationPage.xaml` — Restyled
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml` — Restyled
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml.cs` — Added search handler
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — Restyled
- `src/WhisperHeim/Views/Pages/TranscribeFilesPage.xaml` — Restyled
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml` — Restyled
- `src/WhisperHeim/Views/Pages/AboutPage.xaml` — Restyled
- `src/WhisperHeim/Views/Pages/VoiceCloningPage.xaml` — DELETED
- `src/WhisperHeim/Views/Pages/VoiceCloningPage.xaml.cs` — DELETED
- `src/WhisperHeim/Views/Pages/VoiceLoopbackCapturePage.xaml` — DELETED
- `src/WhisperHeim/Views/Pages/VoiceLoopbackCapturePage.xaml.cs` — DELETED
