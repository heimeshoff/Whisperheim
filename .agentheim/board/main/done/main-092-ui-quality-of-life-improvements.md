---
id: main-092
title: UI Quality-of-Life Improvements
status: done
type: feature
context: main
created: 2026-03-27
completed: 2026-03-31
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
# UI Quality-of-Life Improvements

## Description

Two UI polish fixes for the dictation and conversations screens:

### 1. Template list: single-line descriptions

In the dictation screen template list, multi-line template descriptions currently expand vertically, taking up too much space. The list should show descriptions truncated to a single line with ellipsis. The full content is already visible in the drawer when clicking a template.

**Note:** The XAML already has `TextTrimming="CharacterEllipsis"` and `TextWrapping="NoWrap"` — investigate why multi-line descriptions still expand vertically despite these settings. Likely a container height or row sizing issue.

**File:** `src/WhisperHeim/Views/Pages/DictationPage.xaml` (template list around lines 383-548)

### 2. Imported file: show filename while pending

When importing an audio file via browse, the pending state shows a generic "Call 2024-03-27 14:30" title derived from the session directory timestamp. It should instead show the original filename (without extension) as the title.

The filename is already available — `ImportAudioFile()` stores it as `TranscriptionQueueItem.Title`, but `LoadPendingSessions()` ignores queued item titles and always falls back to parsing the directory name.

**Fix:** In `LoadPendingSessions()`, check if a queued item exists for the pending session and use its `Title` property instead of the generic "Call {date}" format. Recordings started from within the app (not imported) should continue showing "Call {date}".

**Files:**
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — `LoadPendingSessions()` (lines 513-536), `ImportAudioFile()` (lines 411-455)
- `src/WhisperHeim/Services/Transcription/TranscriptionQueueService.cs` — queue item with Title property

## Acceptance Criteria

- [x] Template descriptions in the list view never exceed one line, with ellipsis for overflow
- [x] Full description remains visible in the drawer when clicking a template
- [x] Imported files show the original filename (without extension) as the title while pending
- [x] App-initiated recordings still show "Call {date}" while pending
- [x] After transcription completes, behavior is unchanged (filename is already used)

## Work Log

**2026-03-31:**

### Fix 1: Template list single-line descriptions
The root cause was that `TextWrapping="NoWrap"` does not prevent WPF from rendering literal newline characters in the bound text. Created a `SingleLineTextConverter` that replaces `\r\n`, `\r`, `\n` with spaces before display. Registered the converter in DictationPage.xaml resources and applied it to the description TextBlock binding. The full multi-line text remains unchanged in the data model and is still visible in the drawer.

### Fix 2: Imported file shows filename while pending
In `LoadPendingSessions()`, added a lookup against `_queueService.Items` to find a matching queue item by `SessionDir`. When a match is found with a non-empty `Title` (set by `ImportAudioFile()` from the original filename), that title is used instead of the generic "Call {date}" format. App-initiated recordings (which use `Session` not `SessionDir`) are unaffected and continue showing "Call {date}".

### Files changed
- `src/WhisperHeim/Converters/SingleLineTextConverter.cs` (new) - IValueConverter that collapses newlines to spaces
- `src/WhisperHeim/Views/Pages/DictationPage.xaml` - Added converter namespace, resource, and applied to description binding
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` - Added queue item title lookup in LoadPendingSessions()

### Build note
Pre-existing build error on `DeleteAudio_Click` (line 704 of TranscriptsPage.xaml) is unrelated to this task - likely from a concurrent task adding XAML without the handler method.
