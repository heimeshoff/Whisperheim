---
id: main-036
title: Transcript Naming (Editable Title)
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-22
commit:
depends_on: [main-019]
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Small
---
# Transcript Naming (Editable Title)

## Objective
Allow transcripts to have an editable name/title, displayed at the top of the transcript viewer and in the transcript list.

## Details
Add a `Name` field to the `CallTranscript` data model. Default to a generated name based on date/time (e.g. "Call 2026-03-21 14:30"). Display the name as an editable TextBox at the top of the transcript viewer — clicking it lets the user rename. The transcript list should show the name instead of (or alongside) the date. Persist the name in the transcript JSON. Update the storage service to handle the new field with backward compatibility for existing transcripts (use filename-derived default for old transcripts missing the field).

## Acceptance Criteria
- [x] `CallTranscript` model has a `Name` property
- [x] Default name generated from recording start time
- [x] Name displayed as editable field at top of transcript viewer
- [x] Name persisted to transcript JSON on edit
- [x] Transcript list shows the name
- [x] Existing transcripts without a name get a sensible default

## Notes
Keep backward compatibility — old JSON files without a `name` field should still load fine.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22 — Implementation complete

**Changes made:**

1. **CallTranscript.cs** — Added `Name` property with `[JsonPropertyName("name")]`, defaults to empty string for backward-compatible deserialization.
2. **CallTranscriptionPipeline.cs** — Sets default name `"Call {date} {time}"` when assembling new transcripts.
3. **TranscriptStorageService.cs** — On `LoadAsync`, generates default name for old transcripts missing the field. Added `UpdateAsync` method to persist in-place edits.
4. **ITranscriptStorageService.cs** — Added `UpdateAsync` to the interface.
5. **TranscriptsPage.xaml** — Replaced static `TextBlock` title with borderless `TextBox` for inline name editing. Added `Name` field to list item template (shown above date).
6. **TranscriptsPage.xaml.cs** — Added `TranscriptNameBox_LostFocus` and `TranscriptNameBox_KeyDown` handlers to persist name on blur/Enter. Updated `TranscriptListItem` with mutable `Name` property read from JSON. Search filter also matches on name.

**Acceptance criteria:** All 6 met.
**Build:** Compiles cleanly (only pre-existing warnings from other files).
**Files changed:** 6
