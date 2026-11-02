---
id: main-073
title: Speaker Name List, Count Hint, and Manual Transcription
status: done
type: feature
context: main
created: 
completed: 2026-03-24
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: --
size: Medium
---
# Speaker Name List, Count Hint, and Manual Transcription

## Description

Rework the call recording post-processing flow:

1. **No automatic transcription after recording** — After a recording ends, do NOT auto-run the transcription pipeline. Instead, show the recording session with a "Transcribe" button.

2. **Speaker name list** — In the recording review UI, allow the user to define a list of speaker names for the remote participants. This list is editable at any point before or after transcription. The local mic speaker is always "me" (no diarization needed for mic.wav).

3. **Speaker count hint from name list** — When transcribing, pass `numSpeakers=len(speaker_names)` to the loopback diarization instead of `-1` (auto-detect). This constrains clustering to the correct number of remote speakers, fixing over-segmentation (e.g., 18 speakers detected for a single remote participant).

4. **Skip mic diarization** — The mic always has exactly one speaker (the local user). Skip diarization for mic.wav entirely and assign all mic segments to Speaker 0.

5. **Default speaker name setting** — Add a setting in the Settings page where the user can type their own name. This name replaces "You" as the default label for the local mic speaker in all transcripts.

6. **Speaker name selection in transcript** — In the transcript viewer, instead of only typing speaker names by hand, allow selecting from the predefined name list via a dropdown. Manual typing remains as a fallback.

7. **Re-transcribe button** — Add a button to re-run the full pipeline (diarization + transcription) on an existing recording. This lets the user adjust the speaker list and re-process to get better speaker assignment.

## Why

- Diarization over-segments loopback audio when speaker count is unknown (clustering threshold too sensitive for compressed call audio across 120s chunks with independent diarizer instances)
- Auto-transcription wastes compute when the user wants to set up speaker names first
- Typing speaker names by hand in the transcript is tedious when they're already known

## Acceptance Criteria

- [x] Recording does NOT auto-transcribe — shows "Transcribe" button instead
- [x] User can define a list of remote speaker names on the recording session
- [x] Speaker name list is editable before and after transcription
- [x] Transcribe passes `numSpeakers=N` (from name list length) to loopback diarization
- [x] Mic.wav diarization is skipped — all mic audio assigned to local user
- [x] Settings page has a "Default speaker name" field for the local user's name
- [x] Transcripts use the configured name instead of "You" for the mic speaker (falls back to "You" if empty)
- [x] Transcript viewer shows a dropdown to select from predefined names (in addition to manual typing)
- [x] Re-transcribe button re-runs diarization + transcription with current speaker list
- [x] Works correctly with 0 remote speakers defined (falls back to auto-detect)
- [x] Existing transcripts remain viewable (backward compatible)

## Technical Notes

- `CallTranscriptionPipeline`: remove auto-trigger after recording stop; add manual trigger path
- `SpeakerDiarizationService.DiarizeDualStreamAsync`: skip mic diarization, just create a single segment spanning full mic audio; pass name list length as `numSpeakers` for loopback
- Settings: add `DefaultSpeakerName` string property, used as mic speaker label (fallback: "You")
- `CallRecordingSession`: add `RemoteSpeakerNames` list property, persist to session metadata
- Transcript viewer: add name dropdown sourced from session's `RemoteSpeakerNames`
- Re-transcribe: reuse existing `CallTranscriptionPipeline.ProcessAsync` with updated session metadata

## Work Log

### 2026-03-24 — Implementation Complete

**Changes made:**

1. **Removed auto-transcription** (`MainWindow.xaml.cs`): `OnCallRecordingStopped` no longer calls `EnqueueTranscription`. Recordings appear as pending sessions with "click to transcribe".

2. **Added `RemoteSpeakerNames` to `CallRecordingSession`** (`CallRecordingSession.cs`): Mutable list of remote speaker names, carried through to the pipeline.

3. **Added `DefaultSpeakerName` setting** (`AppSettings.cs`): `GeneralSettings.DefaultSpeakerName` persisted in settings.json.

4. **Added Settings UI** (`GeneralPage.xaml`): "Call Recording" section with a "Default speaker name" text field.

5. **Updated `ICallTranscriptionPipeline` interface** (`ICallTranscriptionPipeline.cs`): `ProcessAsync` now accepts `remoteSpeakerNames` and `localSpeakerName` parameters.

6. **Reworked `CallTranscriptionPipeline.ProcessAsync`** (`CallTranscriptionPipeline.cs`):
   - Skips mic diarization entirely; creates a single segment spanning full mic audio duration
   - Passes `numSpeakers` from remote speaker name list length to loopback diarization (falls back to -1 for auto-detect when list is empty)
   - Uses configured local speaker name instead of hardcoded "You"
   - `GetSpeakerLabel` maps loopback speaker IDs to remote speaker names when available
   - Stores `RemoteSpeakerNames` in the transcript for later editing

7. **Added `RemoteSpeakerNames` to `CallTranscript`** (`CallTranscript.cs`): Persisted as JSON, used for dropdown options and re-transcription.

8. **Updated transcript viewer** (`TranscriptsPage.xaml` + `.cs`):
   - Speaker names panel with add/remove/edit controls
   - Re-transcribe button that deletes existing transcript and re-runs pipeline with current speaker names
   - Speaker name editing uses an editable `ComboBox` dropdown with predefined names from the session's speaker list
   - `SegmentViewModel` carries `AvailableSpeakerNames` for dropdown binding

9. **Updated `TranscriptionProgressDialog`** to pass speaker names through to the pipeline.

10. **Wired `ReTranscriptionRequested` event** in `MainWindow.xaml.cs` to enqueue re-transcription.

**Build:** 0 errors, 0 warnings. **Tests:** 32/32 passed.
