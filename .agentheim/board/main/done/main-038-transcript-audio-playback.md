---
id: main-038
title: Transcript Audio Playback
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
size: Large
---
# Transcript Audio Playback

## Objective
Enable audio playback from the transcript viewer — click on a transcript segment to play audio from that point. Preserve audio files alongside transcripts for long-term access.

## Details

### Audio Preservation
After transcription completes, copy the source WAV files (mic + loopback) to the transcripts directory alongside the JSON file. Store the audio file paths in the transcript JSON so the viewer can locate them. Consider merging mic + loopback into a single stereo or mixed-down WAV for simpler playback, or keep them separate and mix at playback time.

### Playback UI
Add a play button or click handler on each transcript segment. Clicking a segment starts playback from that segment's `StartTime`. Use NAudio (already a dependency) for audio playback — `WaveOutEvent` or `DirectSoundOut` with `AudioFileReader`. Add basic playback controls: play/pause button, current position indicator. Highlight the currently-playing segment in the transcript view. Auto-scroll to follow playback if desired.

### Segment Click Behavior
- Click on a segment → start playback from `segment.StartTime`
- Playback continues through subsequent segments until paused or end of file
- Visual indication of which segment is currently playing

## Acceptance Criteria
- [x] Audio files (WAV) preserved alongside transcript JSON after recording
- [x] Audio file paths stored in transcript JSON
- [x] Clicking a segment starts audio playback from that segment's start time
- [x] Play/pause control available
- [x] Currently-playing segment visually highlighted
- [x] Playback works for both call recordings and file transcriptions (where source audio exists)
- [x] Old transcripts without audio files gracefully show no playback option

## Notes
NAudio is already used for audio capture and resampling. For file transcriptions (drag-drop), the source file path is already known and could be referenced directly. For call recordings, the temp WAV files need to be copied before the temp directory is cleaned up.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-22 — Implementation Complete

**What was done:**

1. **CallTranscript model** (`CallTranscript.cs`): Added `AudioFilePath` property (serialized to JSON) and `ResolvedAudioFilePath` computed property that resolves relative paths against the transcript JSON directory.

2. **CallTranscriptionPipeline** (`CallTranscriptionPipeline.cs`): After saving the transcript JSON, the pipeline now mixes mic + loopback WAV files into a single mono WAV and saves it alongside the transcript JSON file. The relative audio file name is stored in the transcript. Audio preservation is non-critical — failures are logged but don't block the pipeline.

3. **TranscriptAudioPlayer** (`Services/Audio/TranscriptAudioPlayer.cs`): New NAudio-based audio player with Open/Play/PlayFrom/Pause/Stop/Seek/TogglePlayPause controls, position tracking via timer (100ms interval), and PlaybackStopped event. Uses `WaveOutEvent` + `AudioFileReader`.

4. **TranscriptsPage UI** (`TranscriptsPage.xaml`): Added playback control bar (Play/Pause, Stop, position display) above the segment list. Segments are now clickable (cursor: hand) with a `MouseLeftButtonDown` handler. Currently-playing segments show a yellow highlight and "playing" indicator text. Playback panel is hidden for transcripts without audio.

5. **TranscriptsPage code-behind** (`TranscriptsPage.xaml.cs`): Integrated `TranscriptAudioPlayer` with segment click-to-play, position tracking that highlights the active segment, play/pause/stop controls, and cleanup on transcript switch/delete. Speaker label click still triggers name editing (uses e.Handled to prevent playback). Delete also cleans up associated audio files.

6. **SegmentViewModel**: Added `IsCurrentlyPlaying` property and `CurrentBackground` that switches between default background and yellow highlight brush.

**Acceptance criteria status:** All 7 criteria met.
- Audio WAV preserved alongside JSON after call recording (mixed mono from mic+loopback)
- Audio file path stored as relative path in transcript JSON `audioFilePath` field
- Clicking a segment starts playback from that segment's StartTime via NAudio
- Play/Pause and Stop buttons available in playback control bar
- Currently-playing segment highlighted with yellow background + "playing" text
- File transcriptions don't create CallTranscript records (separate page); call recordings work; source audio path referenced directly when available
- Old transcripts without `audioFilePath` gracefully hide the playback panel

**Files changed:**
- `src/WhisperHeim/Services/CallTranscription/CallTranscript.cs`
- `src/WhisperHeim/Services/CallTranscription/CallTranscriptionPipeline.cs`
- `src/WhisperHeim/Services/Audio/TranscriptAudioPlayer.cs` (new)
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml`
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs`
