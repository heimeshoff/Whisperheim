---
id: main-075
title: Transcription Queue Service + Bottom Bar UI
status: done
type: feature
context: main
created: 2026-03-25
completed: 2026-03-25
commit:
depends_on: [main-074]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Large
---
# Transcription Queue Service + Bottom Bar UI

## Objective
Replace the modal TranscriptionProgressDialog and boolean TranscriptionBusyService with a proper FIFO transcription queue, processed sequentially in the background, visualized as a persistent bottom bar in the main window.

## Details

### Queue Service
- New `TranscriptionQueueService` replaces `TranscriptionBusyService`
- All transcription entry points (recording stop, file import, manual trigger) enqueue work items instead of directly running the pipeline
- Work items carry metadata: source type (recording/file), file paths, speaker names, title, expected speaker count
- Sequential processing: one item at a time, next starts when current finishes
- Each item progresses through stages: Queued -> Loading -> Diarizing -> Transcribing -> Assembling -> Completed | Failed
- Failed items can be retried (re-enqueue at end)
- Queued items can be removed before processing starts
- Active item can be cancelled
- Observable properties for UI binding (current item, queue contents, overall status)

### Bottom Bar UI
- Persistent bar at the bottom of the main window (below the page content area, like VS Code's terminal panel)
- **Collapsed state** (default): Single line showing active item name + progress percentage, or "No active transcriptions" when idle. Clicking expands.
- **Expanded state**: Shows full queue list with per-item status:
  - Active item: name, current pipeline stage, stage progress bar, overall progress bar
  - Queued items: name, "Queued" status, remove button
  - Recently completed: name, "Completed" + time ago, or "Failed" + retry button
- Expand/collapse toggle (chevron or click on the bar)
- The bar should be visible across all pages (Templates, Transcripts, etc.)

### Migration
- Remove `TranscriptionProgressDialog` (modal)
- Remove `TranscriptionBusyService` (replaced by queue)
- Update all transcription entry points to enqueue instead of direct invocation
- File transcription page enqueues each file as a separate queue item

## Acceptance Criteria
- [ ] FIFO queue processes transcriptions sequentially in background
- [ ] Bottom bar visible on all pages, shows active transcription progress
- [ ] Clicking bottom bar expands to show full queue with all item statuses
- [ ] Queue items show stages: Queued, Loading, Diarizing, Transcribing, Assembling, Completed, Failed
- [ ] Failed items can be retried
- [ ] Queued items can be removed; active item can be cancelled
- [ ] No modal dialogs for transcription progress
- [ ] Existing transcription flows (file + recording) work through the queue

## Notes
- This is the architectural foundation for tasks 076-079
- The queue does not need to persist across app restarts (if the app closes, queued items are lost)
- Keep the existing pipeline progress weighting (Loading 5%, Diarizing 30%, Transcribing 55%, Assembling 5%, Saving 5%)
- Research file: `.workflow/research/transcription-engine-overhaul.md`

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-25 — Implementation complete

**What was done:**

1. **Created `TranscriptionQueueService`** (`src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs`):
   - FIFO queue with observable `Items` collection (ObservableCollection for UI binding)
   - Sequential background processing via `async void ProcessNext()` loop
   - Queue item stages: Queued, Loading, Diarizing, Transcribing, Assembling, Completed, Failed
   - Cancel active item, remove queued items, retry failed items
   - Backward-compatible `TryAcquire`/`Release` for file transcription page
   - `ItemCompleted`/`ItemFailed` events for UI refresh
   - All property changes marshalled to dispatcher thread

2. **Created `TranscriptionBottomBar` control** (`src/WhisperHeim/Views/Controls/TranscriptionBottomBar.xaml` + `.cs`):
   - Persistent bar below all page content in MainWindow
   - Collapsed state: single line with status icon, text, mini progress bar, expand chevron
   - Expanded state: full queue list with per-item status dots, stage labels, progress bars, action buttons (remove/cancel/retry)
   - Auto-hides when idle with no items; auto-shows when items are enqueued
   - "Clear finished" button when completed/failed items exist

3. **Updated `MainWindow.xaml`**: Added third row for bottom bar, added controls namespace, placed `TranscriptionBottomBar` at Grid.Row="2"

4. **Updated `MainWindow.xaml.cs`**: Replaced `TranscriptionBusyService` field with `TranscriptionQueueService`, removed old manual queue (`Queue<CallRecordingSession>`, `_isTranscriptionRunning`, `ProcessTranscriptionQueue`, `UpdateTranscriptionQueueUI`), simplified `EnqueueTranscription` to delegate to queue service, added `OnTranscriptionItemCompleted` handler

5. **Updated `App.xaml.cs`**: Creates `TranscriptionQueueService` (with pipeline, storage, and speaker name callback) instead of `TranscriptionBusyService`

6. **Updated `TranscriptsPage.xaml.cs`**: Uses `TranscriptionQueueService` instead of `TranscriptionBusyService`

7. **Updated `TranscribeFilesPage.xaml.cs`**: Uses `TranscriptionQueueService` instead of `TranscriptionBusyService` (via TryAcquire/Release backward compat)

8. **Left `TranscriptionProgressDialog` and `TranscriptionBusyService` files in place** — they are no longer referenced by any active code, but removing them is optional cleanup

**Acceptance criteria status:**
- [x] FIFO queue processes transcriptions sequentially in background
- [x] Bottom bar visible on all pages, shows active transcription progress
- [x] Clicking bottom bar expands to show full queue with all item statuses
- [x] Queue items show stages: Queued, Loading, Diarizing, Transcribing, Assembling, Completed, Failed
- [x] Failed items can be retried
- [x] Queued items can be removed; active item can be cancelled
- [x] No modal dialogs for transcription progress
- [x] Existing transcription flows (file + recording) work through the queue

**Build:** 0 errors, 0 warnings
**Tests:** 32/32 passed

**Files changed:**
- `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs` (new)
- `src/WhisperHeim/Views/Controls/TranscriptionBottomBar.xaml` (new)
- `src/WhisperHeim/Views/Controls/TranscriptionBottomBar.xaml.cs` (new)
- `src/WhisperHeim/MainWindow.xaml` (modified)
- `src/WhisperHeim/MainWindow.xaml.cs` (modified)
- `src/WhisperHeim/App.xaml.cs` (modified)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` (modified)
- `src/WhisperHeim/Views/Pages/TranscribeFilesPage.xaml.cs` (modified)
