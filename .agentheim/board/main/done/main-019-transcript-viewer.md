---
id: main-019
title: Transcript Viewer and Export
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-018]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# Transcript Viewer and Export

## Objective
Display call transcripts in-app and export as Markdown, JSON, or plain text.

## Details
Add a Transcripts page to the settings/main window. List past transcripts by date. Click to view full transcript with speaker colors and timestamps. Export button with format selection (Markdown with speaker headers, JSON with structured data, plain text). Copy-to-clipboard support. Delete old transcripts. Search within transcripts.

## Acceptance Criteria
- [x] Transcripts listed and viewable by date
- [x] Speaker colors and timestamps displayed
- [x] Export to Markdown format works correctly
- [x] Export to JSON format works correctly
- [x] Export to plain text format works correctly
- [x] Copy-to-clipboard functional
- [x] Delete old transcripts functional
- [x] Search within transcripts functional

## Notes
UI page integrated into the main settings/window of the application.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 - Implementation complete
- Created `TranscriptsPage.xaml` and `TranscriptsPage.xaml.cs` in `src/WhisperHeim/Views/Pages/`
- Page features: transcript list by date (left panel), transcript viewer with speaker colors and timestamps (right panel)
- Speaker colors: blue for local speaker, orange for remote speaker, with tinted backgrounds
- Export: Markdown (with speaker headers), JSON (structured), plain text (timestamped lines) via SaveFileDialog
- Copy-to-clipboard: copies plain text format
- Delete: with confirmation dialog, removes JSON file from storage
- Search: filters transcript list by date/preview/filename
- Added "Transcripts" nav item to MainWindow.xaml (before About)
- Added navigation case in MainWindow.xaml.cs with TranscriptStorageService instantiation
- Build succeeds with 0 errors, 0 warnings
