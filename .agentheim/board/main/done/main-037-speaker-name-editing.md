---
id: main-037
title: Speaker Name Editing
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-22
commit:
depends_on: [main-036]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Medium
---
# Speaker Name Editing

## Objective
Allow users to rename speakers in a transcript, both globally (rename all segments from a speaker) and per-segment.

## Details
Add speaker name editing to the transcript viewer. By default, speakers are labeled "You", "Other", or "Speaker N". Users should be able to click on a speaker label and rename it. A global rename updates all segments with that speaker label throughout the transcript. Additionally, allow per-segment override — if a user wants to attribute a single segment to a different speaker, they can change just that one. Persist speaker name mappings in the transcript JSON. Consider adding a speaker name map at the transcript level (original label → custom name) for global renames, while per-segment overrides are stored on individual segments.

## Acceptance Criteria
- [x] Clicking a speaker label opens an inline edit field
- [x] Global rename: changing "Other" to "Alice" updates all "Other" segments
- [x] Per-segment override: can change a single segment's speaker independently
- [x] Speaker name changes persisted to transcript JSON
- [x] Speaker colors remain consistent after rename
- [x] Existing transcripts without custom names load correctly

## Notes
The data model needs a speaker name mapping. Consider a dictionary at the transcript level for global renames, plus an optional override field on `TranscriptSegment`.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22 — Implementation complete

**Model changes:**
- Added `SpeakerNameMap` dictionary to `CallTranscript` for global speaker renames (original label -> custom name)
- Added `SpeakerOverride` nullable property to `TranscriptSegment` for per-segment overrides
- Added `GetDisplaySpeaker(segment)` method to resolve display name (per-segment override > global map > original label)
- Added `RenameSpeakerGlobally(original, newName)` method that updates the map and clears redundant overrides

**UI changes:**
- Speaker labels in transcript viewer are now clickable (click = global rename, Shift+Click = per-segment override)
- Inline TextBox appears on click with the current name pre-filled and selected
- Enter commits, Escape cancels, clicking away commits
- Added `InverseBoolToVisibilityConverter` for toggling between label and edit box

**ViewModel changes:**
- `SegmentViewModel` now implements `INotifyPropertyChanged` with `DisplaySpeaker`, `IsEditingSpeaker`, `EditingSpeakerName` properties
- Colors remain based on `IsLocalSpeaker` (not speaker name), ensuring consistency after renames

**Export updates:**
- Plain text, Markdown, and JSON exports now use `GetDisplaySpeaker()` for resolved names

**Tests:**
- Added 10 unit tests covering global rename, per-segment override, priority rules, edge cases, and backward compatibility
- All 32 tests pass (10 new + 22 existing)

**Files changed:**
- `src/WhisperHeim/Services/CallTranscription/CallTranscript.cs` — model changes
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` — inline speaker edit UI
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — event handlers, SegmentViewModel update
- `src/WhisperHeim/Converters/InverseBoolToVisibilityConverter.cs` — new converter
- `tests/WhisperHeim.Tests/SpeakerNameEditingTests.cs` — new test file
