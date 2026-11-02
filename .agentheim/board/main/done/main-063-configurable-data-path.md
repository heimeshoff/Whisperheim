---
id: main-063
title: Configurable Config/Data Path for Cloud Sync
status: done
type: feature
context: main
created: 2026-03-22
completed: 2026-03-22
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Medium
---
# Configurable Config/Data Path for Cloud Sync

## Description
Enable the app's user data to be stored at a configurable path for cloud sync (Google Drive, OneDrive, etc.). A small bootstrap config stays in `%APPDATA%\WhisperHeim\`, pointing to the actual data folder. By default, the data folder is co-located with the bootstrap config.

### Synced folder structure
```
[synced path]/
├── settings.json              # Shared preferences, templates
├── recordings/                # Per-session folders
│   ├── 20260322_143000/
│   │   ├── transcript.json
│   │   ├── mic.wav
│   │   └── system.wav
│   └── ...
└── voices/                    # Custom cloned voice samples
```

### Bootstrap config (stays in %APPDATA%\WhisperHeim\)
- Pointer to the synced data path
- Machine-local settings: WindowSettings, OverlaySettings, audio device selection

### What syncs
- `settings.json` (general settings, dictation settings, TTS voice preference, templates)
- `recordings/` (transcript JSON + WAV files, per-session folders)
- `voices/` (custom cloned voice samples)

### What stays local
- Bootstrap/pointer config
- `models/` (~800MB, re-downloadable)
- `whisperheim.log`
- Window position/size, overlay position, audio device (machine-specific)

## Acceptance Criteria
- [x] Bootstrap config in `%APPDATA%\WhisperHeim\` with a `dataPath` pointer
- [x] Default data path co-located with bootstrap config (no change for existing users)
- [x] Settings page has a folder picker to change the data path
- [x] Path validation: write a temp file to verify the path is writable before accepting
- [x] Split machine-local settings (WindowSettings, OverlaySettings, audio device) into bootstrap config
- [x] Recordings use per-session folders (`recordings/YYYYMMDD_HHMMSS/`) with transcript.json + WAV files
- [x] Deleting a recording deletes the entire session folder (transcript + WAVs)
- [x] No more temp recordings in `%TEMP%` — recordings go directly to the data path
- [x] Migration: move existing data from old flat structure to new structure on first run
- [x] All path-resolving services updated (SettingsService, TranscriptStorageService, TextToSpeechService, ModelManagerService)

## Technical Notes
- Conflict handling: last-write-wins for settings.json, no special logic needed
- User scenario: notebook + desktop synced via Google Drive
- Path resolution is already centralized in services — changes should be manageable
- Recordings, voices are unique timestamped files — no sync conflicts possible

## Refined from
ideas/2026-03-22-configurable-config-path.md

## Work Log

### 2026-03-22 — Implementation complete

**All acceptance criteria met.** Build succeeds, all 32 tests pass.

**New files:**
- `src/WhisperHeim/Models/BootstrapConfig.cs` — Bootstrap config model with dataPath pointer + machine-local settings (Window, Overlay, AudioDevice, TtsPlaybackDeviceId)
- `src/WhisperHeim/Services/Settings/DataPathService.cs` — Manages bootstrap config, resolves data paths, validates paths, handles migration from old flat structure to new per-session recordings

**Modified files:**
- `src/WhisperHeim/Services/Settings/SettingsService.cs` — Now depends on DataPathService; settings.json stored at data path; machine-local fields synced to/from bootstrap config
- `src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs` — Uses DataPathService for recordings path; saves to per-session folders (recordings/YYYYMMDD_HHmmss/transcript.json); added DeleteSession() method
- `src/WhisperHeim/Services/Models/ModelManagerService.cs` — Added Initialize(DataPathService) to set models root from local path (not synced)
- `src/WhisperHeim/Services/TextToSpeech/TextToSpeechService.cs` — Added Initialize(DataPathService) to set custom voices dir from synced data path
- `src/WhisperHeim/Services/Audio/HighQualityLoopbackService.cs` — Added Initialize(DataPathService) to set custom voices dir from synced data path
- `src/WhisperHeim/Services/Recording/CallRecordingService.cs` — Recordings go directly to data path recordings/ dir instead of %TEMP%; accepts DataPathService in constructor
- `src/WhisperHeim/Services/CallTranscription/CallTranscriptionPipeline.cs` — Audio saved as "recording.wav" in session folder
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml` — Added "Data Storage" section with folder picker, reset button, current path display, and sync info
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml.cs` — Added BrowseDataPath_Click (OpenFolderDialog), ResetDataPath_Click, UpdateDataPathDisplay, path validation
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` — Delete now uses DeleteSession() to remove entire session folder
- `src/WhisperHeim/App.xaml.cs` — Creates DataPathService first, runs migration, initializes all path-dependent services
