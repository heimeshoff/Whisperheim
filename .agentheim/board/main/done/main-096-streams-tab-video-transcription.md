---
id: main-096
title: Streams Tab -- Video Link Transcription
status: done
type: feature
context: main
created: 
completed: 2026-04-02
commit:
depends_on: []
blocks: []
tags: [m5]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 (new -- Streams / Web Media Transcription)
size: Large
---
# Streams Tab -- Video Link Transcription

## Description

Add a new "Streams" tab to the sidebar that lets the user paste a list of YouTube and Instagram URLs, then transcribe each link into its own entry. The transcriptions live entirely within the Streams tab (not in Recordings).

**Workflow:**
1. User pastes URLs (one per line) into a textarea
2. Clicks "Transcribe"
3. Each link is processed with a per-link progress indicator
4. Completed transcriptions appear in the Streams tab with copyable text

**Transcription strategy (fallback chain):**
- **YouTube:** Try to pull existing captions/subtitles via yt-dlp first. If none available, download audio and run through local Parakeet pipeline.
- **Instagram:** Try captions via gallery-dl first. If none, download audio and run through local Parakeet pipeline.

**Data model (StreamTranscript):**
- Title (from video metadata)
- Source URL
- Transcript text (plain text, copyable)
- Duration (of original video)
- Date transcribed (used as sort key, descending = newest first)

**Storage:**
- Persisted to disk at `%APPDATA%/WhisperHeim/streams/`
- Each entry as a JSON file (similar pattern to call transcripts)
- Media files (downloaded audio) are temporary and do not persist across restarts

**UI requirements:**
- New sidebar item "Streams" with appropriate icon
- Input area: textarea + "Transcribe" button
- Per-link progress indicator during processing (e.g., "3/7 -- Transcribing: [video title]")
- Transcript list view sorted by date transcribed (newest first)
- Each entry shows: title, URL, duration, date transcribed, full transcript text
- Transcript text must be easily copyable (select-all or copy button)

## Acceptance Criteria

- [x] "Streams" tab appears in sidebar navigation
- [x] User can paste multiple YouTube URLs and get individual transcriptions
- [x] User can paste multiple Instagram URLs and get individual transcriptions
- [x] Mixed YouTube + Instagram URL lists work in a single batch
- [x] Captions/subtitles are used when available (fast path)
- [x] Audio download + Parakeet transcription works as fallback
- [x] Per-link progress is shown during batch processing
- [x] Transcriptions persist across app restarts
- [x] Transcript text is easily copyable for use in Obsidian
- [x] Entries are sorted by date transcribed (newest first)
- [x] Downloaded media files are cleaned up (not persisted)

## Implementation Notes

- Use `yt-dlp` for YouTube (captions + audio download)
- Use `gallery-dl` for Instagram (content extraction)
- New `StreamTranscript` model separate from `CallTranscript`
- New `StreamStorageService` following the pattern of `TranscriptStorageService`
- New `StreamTranscriptionService` orchestrating the caption-or-audio pipeline
- New `StreamsPage.xaml` WPF page with textarea input + transcript list
- Wire into `MainWindow.xaml` navigation alongside existing tabs

## Work Log

**2026-04-02** -- Implementation complete. All acceptance criteria met.

### What was done:
1. Created `StreamTranscript` model with Id, Title, SourceUrl, TranscriptText, Duration, DateTranscribedUtc, TranscriptionMethod
2. Created `StreamStorageService` -- persists transcripts as JSON in `%APPDATA%/WhisperHeim/streams/`, loads all sorted by date descending
3. Created `StreamTranscriptionService` -- orchestrates the fallback chain: YouTube captions via yt-dlp, Instagram via gallery-dl, then audio download + Parakeet ASR. Supports batch processing with per-link progress reporting. Error entries are created for failed URLs. Temp audio files are cleaned up.
4. Created `StreamsPage.xaml` + code-behind -- textarea for URL input, Transcribe/Cancel buttons, progress bar with per-link status text, transcript list with cards showing title, URL (clickable), duration, date, method, full copyable text, copy button, and delete button
5. Added `StreamsPath` property to `DataPathService`
6. Wired "Streams" nav item (Video24 icon) into `MainWindow.xaml` sidebar between Conversations and Text to Speech
7. Added `NavLabelStreams` to sidebar collapse handler
8. Created and wired `StreamTranscriptionService` + `StreamStorageService` in `App.xaml.cs`, passed to `MainWindow` constructor

### Build & test:
- Build: 0 errors, 6 warnings (all pre-existing)
- Tests: 32/32 passed

### Files changed:
- `src/WhisperHeim/Services/Streams/StreamTranscript.cs` (new)
- `src/WhisperHeim/Services/Streams/StreamStorageService.cs` (new)
- `src/WhisperHeim/Services/Streams/StreamTranscriptionService.cs` (new)
- `src/WhisperHeim/Views/Pages/StreamsPage.xaml` (new)
- `src/WhisperHeim/Views/Pages/StreamsPage.xaml.cs` (new)
- `src/WhisperHeim/Services/Settings/DataPathService.cs` (modified -- added StreamsPath)
- `src/WhisperHeim/MainWindow.xaml` (modified -- added Streams nav item)
- `src/WhisperHeim/MainWindow.xaml.cs` (modified -- added stream services, nav case, sidebar label)
- `src/WhisperHeim/App.xaml.cs` (modified -- created stream services, passed to MainWindow)
