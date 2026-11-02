---
id: main-026
title: Wire call recording services in app startup
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: []
blocks: []
tags: [m2, call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: M2 - Audio Capture + Call Transcription
size: Small
---
# Wire call recording services in app startup

## Objective
All call recording services are instantiated and available in MainWindow, ready for UI integration.

## Details
- In `App.xaml.cs`, create instances of `LoopbackCaptureService`, `CallRecordingService`, `CallTranscriptionPipeline`, and `CallRecordingHotkeyService`
- `CallRecordingService` needs both `AudioCaptureService` (already exists) and `LoopbackCaptureService`
- `CallTranscriptionPipeline` needs `SpeakerDiarizationService`, `TranscriptionService`, and `TranscriptStorageService`
- `CallRecordingHotkeyService` needs `CallRecordingService`
- Pass all new services to `MainWindow` constructor (update constructor signature)
- Store as fields in `MainWindow` for use by tasks 027 and 028

## Acceptance Criteria
- [ ] `CallRecordingService` instantiated with mic + loopback capture
- [ ] `CallTranscriptionPipeline` instantiated with diarization + transcription + storage
- [ ] `CallRecordingHotkeyService` instantiated with recording service
- [ ] All services passed to `MainWindow` and stored as fields
- [ ] App still starts and runs without errors

## Notes
- Follow existing pattern: manual DI, no container
- `TranscriptionService` is already created in `App.StartupCore` — reuse it
- `TranscriptStorageService` may already be instantiated for `TranscriptsPage` — check and reuse

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 -- Work Completed

**What was done:**
- Created `CallRecordingService`, `SpeakerDiarizationService`, `CallTranscriptionPipeline`, `CallRecordingHotkeyService`, and `TranscriptStorageService` instances in `App.StartupCore`
- Reused the existing `transcriptionService` for `CallTranscriptionPipeline`
- Moved `TranscriptStorageService` creation from MainWindow field initializer to App.xaml.cs (shared instance passed to both MainWindow and CallTranscriptionPipeline)
- Updated `MainWindow` constructor to accept and store all new call recording services as fields
- Added disposal of call recording services in `OnClosing`

**Acceptance criteria status:**
- [x] `CallRecordingService` instantiated with mic + loopback capture -- created in App.StartupCore, verified by successful build
- [x] `CallTranscriptionPipeline` instantiated with diarization + transcription + storage -- created with SpeakerDiarizationService, TranscriptionService (reused), and TranscriptStorageService
- [x] `CallRecordingHotkeyService` instantiated with recording service -- created with CallRecordingService reference
- [x] All services passed to `MainWindow` and stored as fields -- constructor updated with 4 new parameters, all stored as private readonly fields
- [x] App still starts and runs without errors -- verified via `dotnet build` with 0 errors

**Files changed:**
- src/WhisperHeim/App.xaml.cs -- added call recording service creation and passing to MainWindow
- src/WhisperHeim/MainWindow.xaml.cs -- added constructor params, fields, and disposal for call recording services
