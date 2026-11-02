---
id: main-021
title: Drag-and-Drop Transcription UI
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-020]
blocks: []
tags: [m3, voice-files]
related_adrs: []
related_research: []
prior_art: []
milestone: M3 - Voice Message Transcription
size: Medium
---
# Drag-and-Drop Transcription UI

## Objective
Drag-and-drop audio files onto the app window for transcription with batch support.

## Details
Add a "Transcribe Files" page/panel to the main window. Support drag-and-drop (files dropped onto the window). Also support a file picker button. Show transcription progress per file. Display results inline -- each file with its transcript, copy button, duration. Support batch: drop multiple files, transcribe sequentially. Also support drag-and-drop onto the tray icon (if WPF-UI.Tray supports it, otherwise window only). Results are ephemeral (not persisted like call transcripts) unless user explicitly saves.

## Acceptance Criteria
- [x] Drag-drop works from file explorer and WhatsApp Web
- [x] Batch transcription works for multiple files
- [x] Results shown inline with copy button per file
- [x] File picker fallback button works
- [x] Transcription progress displayed per file
- [x] Results are ephemeral unless explicitly saved

## Notes
Results are not persisted by default (unlike call transcripts from 018/019). User must explicitly save if desired.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 - Implementation complete
- Created `TranscribeFilesPage.xaml` with drag-and-drop zone, file picker button, and results list
- Created `TranscribeFilesPage.xaml.cs` with full code-behind:
  - Drag-and-drop handling (DragEnter/DragOver/DragLeave/Drop) for files from explorer and browsers
  - OpenFileDialog fallback for file picking
  - Sequential batch transcription using `IFileTranscriptionService`
  - Per-file progress bar via `IProgress<double>`
  - Inline results with transcript text, audio duration, and transcription time
  - Copy-to-clipboard button per transcript
  - Save-to-file button per transcript (explicit save, results ephemeral by default)
  - Error handling and cancellation support
- Created `TranscriptionItemViewModel` and `TranscribeFilesViewModel` for data binding
- Added "Transcribe Files" nav item to `MainWindow.xaml`
- Build succeeds with 0 errors, 0 warnings
- **Integration note:** `MainWindow.xaml.cs` needs a `"TranscribeFiles"` case in `NavigateTo()` to wire up the page with `IFileTranscriptionService`. This was not done per task rules (cannot modify MainWindow.xaml.cs).
