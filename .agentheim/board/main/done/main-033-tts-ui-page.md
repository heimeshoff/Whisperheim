---
id: main-033
title: TTS UI page
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-029]
blocks: []
tags: [m4, tts]
related_adrs: []
related_research: []
prior_art: []
milestone: M4 - Text-to-Speech
size: Small
---
# TTS UI page

**Parent:** 023

## Objective
Create a WPF UI page for text-to-speech with text input, voice selection, and playback controls.

## Details
- New `TextToSpeechPage.xaml` in Views/Pages
- Text input: multi-line text box for pasting or typing text
- Voice selector: dropdown listing built-in voices + custom voices (with voice type indicator)
- Play/Stop buttons with generation progress indicator
- Voice preview button (reads a short test phrase)
- Navigation entry in MainWindow sidebar
- Follow existing Fluent UI patterns (WPF-UI)

## Acceptance Criteria
- [x] Text input field accepts multi-line text
- [x] Voice selector shows all available voices (built-in + custom)
- [x] Play button generates and plays speech
- [x] Stop button halts playback
- [x] Progress indicator during generation
- [x] Voice preview with test phrase

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21
- Created `TextToSpeechPage.xaml` with multi-line text input, voice ComboBox, Play/Stop buttons with icons, voice preview button, and indeterminate progress bar
- Created `TextToSpeechPage.xaml.cs` with full playback lifecycle: loads voices from ITextToSpeechService on page load, SpeakAsync with CancellationTokenSource for stop, preview plays "Hello, this is a voice preview."
- Added "Text to Speech" navigation entry in MainWindow.xaml sidebar (ReadAloud24 icon)
- Wired ITextToSpeechService through MainWindow constructor and NavigateTo switch
- Passed textToSpeechService from App.xaml.cs to MainWindow
- Build passes with 0 CS errors and 0 CS warnings (only file-lock warning from running exe)
