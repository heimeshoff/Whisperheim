---
id: main-028
title: Post-recording transcription pipeline with progress UI
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-027]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# Post-recording transcription pipeline with progress UI

## Objective
When call recording stops, the transcription pipeline runs automatically with a visible progress indicator, and the user is navigated to the finished transcript.

## Details

### Auto-trigger pipeline
- In `MainWindow`, subscribe to `CallRecordingService.RecordingStopped`
- On stop, call `CallTranscriptionPipeline.ProcessAsync(session, progress, cancellationToken)` on a background thread
- Pass an `IProgress<TranscriptionPipelineProgress>` to drive the progress UI

### Progress UI
- Show a modal or overlay dialog while the pipeline runs
- Display: current stage name (Loading Audio, Diarizing, Transcribing, Assembling, Saving), stage progress bar, overall progress bar
- Include a "Cancel" button wired to a `CancellationTokenSource`
- Pipeline stages have weighted progress: Load 5%, Diarize 30%, Transcribe 55%, Assemble 5%, Save 5%
- Close the dialog automatically when pipeline reaches `PipelineStage.Completed`

### Navigation to result
- After pipeline completes successfully, refresh `TranscriptsPage` (call `RefreshList()`)
- Navigate to "Transcripts" page via `NavigateTo("Transcripts")`
- Show the main window if it was hidden (tray-only mode)
- Optionally auto-select the new transcript in the list

### Error handling
- If pipeline throws, close the progress dialog and show an error message
- If cancelled, clean up gracefully (no partial transcript saved)

## Acceptance Criteria
- [x] Pipeline auto-starts when recording stops
- [x] Progress dialog shows current stage and percentage
- [x] Cancel button aborts the pipeline
- [x] On success: TranscriptsPage refreshed, navigated to, window shown
- [x] On error: error message displayed, no crash
- [x] On cancel: dialog closes cleanly, no partial transcript

## Notes
- `CallTranscriptionPipeline` is CPU-intensive (diarization + ASR) — must run on background thread, marshal progress to UI thread
- Consider using `Task.Run` with `IProgress<T>` pattern
- The progress dialog could be a simple `Window` with a `ProgressBar` and text labels — no need for a full page

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete
- Created `TranscriptionProgressDialog.xaml` + `.xaml.cs` — modal dialog with stage progress bar, overall progress bar, stage description, cancel button
- Dialog follows `ModelDownloadDialog` pattern: `ShowAndProcess()` static method, `IProgress<T>` for UI updates, `CancellationTokenSource` for cancel, `Task.Run` for background execution
- Pipeline runs on background thread via `Task.Run`, progress marshalled to UI thread via `Progress<T>`
- Cancel button wires to `CancellationTokenSource`; dialog prevents closing while pipeline is active (cancels instead)
- On success: auto-closes dialog, refreshes TranscriptsPage, navigates to it, syncs nav highlight, shows window
- On error: displays error message in dialog, user clicks Close
- On cancel: shows "cancelled" message, user clicks Close; no partial transcript saved (pipeline throws `OperationCanceledException`)
- Wired into `MainWindow.OnCallRecordingStopped` — skips transcription if recording stopped with an error
- Build: 0 errors, tests: 21/21 passing
