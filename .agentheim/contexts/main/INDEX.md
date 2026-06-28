# main -- Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 0
- **Todo:** 0
- **Doing:** 0
- **Done:** 122
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- no tasks in todo -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- no tasks in doing -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **main-p3k9d** -- First dictation overlay renders at wrong position (not bottom-center) -- 2026-06-28 -- `done/main-p3k9d-first-overlay-mispositioned.md`
- **main-t6r2k** -- Reduce ASR intra-op threads 4 → 2 -- 2026-06-28 -- `done/main-t6r2k-reduce-asr-threads.md`
- **main-r7n2k** -- Transcode any unsupported audio format via FFmpeg fallback (e.g. .opus) -- 2026-06-19 -- `done/main-r7n2k-ffmpeg-transcode-fallback.md`
- **main-q4m8t** -- whisperheim-transcribe CLI wrapper over POST /transcribe -- 2026-06-19 -- `done/main-q4m8t-whisperheim-transcribe-cli.md`
- **main-h7k2p** -- STT API — POST /transcribe HttpListener server + queue integration -- 2026-06-19 -- `done/main-h7k2p-transcribe-http-endpoint.md`
- **main-110** -- FFmpeg Detection + First-Use Install Prompt -- 2026-05-12 -- `done/main-110-ffmpeg-detection-and-install-prompt.md`
- **main-111** -- GitHub Actions Release Workflow (Tag-Triggered Velopack Build) -- 2026-05-12 -- `done/main-111-github-actions-release-workflow.md`
- **main-109** -- Bundle Silero VAD + Pyannote Seg in the Publish Output -- 2026-05-12 -- `done/main-109-bundle-small-models-in-publish.md`
- **main-107** -- Add Velopack to the Project (Custom Main + Bootstrap) -- 2026-05-12 -- `done/main-107-velopack-bootstrap.md`
- **main-108** -- First-Run Model Download Dialog -- 2026-05-12 -- `done/main-108-first-run-model-download-dialog.md`
- **main-115** -- Code Signing — Deferred Hook (Wire-Up Now, Flip Post-UG) -- 2026-05-12 -- `done/main-115-code-signing-deferred-hook.md`
- **main-116** -- Fix vpk Version Pin in Release Workflow (0.0.1589 unavailable) -- 2026-05-12 -- `done/main-116-fix-vpk-version-pin-in-release-workflow.md`
- **main-114** -- Velopack End-to-End Dry Run (Sanity Check Before 1.0.0) -- 2026-05-12 -- `done/main-114-velopack-pack-dry-run.md`
- **main-112** -- Public README + GitHub Release Page Content -- 2026-05-12 -- `done/main-112-readme-and-release-page-content.md`
- **main-113** -- Uninstall Data Preservation (Hygiene + Documentation) -- 2026-05-12 -- `done/main-113-uninstall-data-preservation.md`
- **main-104** -- Stage WAV Writes Outside the Synced Data Folder -- 2026-05-11 -- `done/main-104-stage-wav-writes-outside-data-folder.md`
- **main-105** -- Origin-Machine Owns Transcription (Multi-Machine Coordination) -- 2026-05-11 -- `done/main-105-origin-machine-owns-transcription.md`
- **main-106** -- No Window Frame Flash When Start-Minimized -- 2026-05-11 -- `done/main-106-no-frame-flash-when-start-minimized.md`
- **main-102** -- Hot-Reload Settings from Disk (Multi-Machine Sync) -- 2026-04-24 -- `done/main-102-hot-reload-settings-from-disk.md`
- **main-103** -- Remove Text-to-Speech Feature -- 2026-04-24 -- `done/main-103-remove-text-to-speech-feature.md`
- **main-101** -- Deterministic Clean-Text Pipeline (Filler Word Removal) -- 2026-04-20 -- `done/main-101-deterministic-clean-text-pipeline.md`
- **main-098** -- Pending Transcription Drawer with Playback -- 2026-04-07 -- `done/main-098-pending-transcription-drawer.md`
- **main-097** -- Enter-to-Confirm in Drawer Text Fields -- 2026-04-07 -- `done/main-097-enter-to-confirm-drawer-fields.md`
- **main-100** -- Streams page visual polish -- 2026-04-07 -- `done/main-100-streams-visual-polish.md`
- **main-099** -- Explicit Transcription Queuing -- 2026-04-07 -- `done/main-099-transcription-queue-explicit.md`
- **main-096** -- Streams Tab -- Video Link Transcription -- 2026-04-02 -- `done/main-096-streams-tab-video-transcription.md`
- **main-095** -- Unified Recording & Transcript Drawer -- 2026-04-01 -- `done/main-095-unified-recording-drawer.md`
- **main-092** -- UI Quality-of-Life Improvements -- 2026-03-31 -- `done/main-092-ui-quality-of-life-improvements.md`
- **main-093** -- Show Distinct Speaker Names in Collapsed Date Groups -- 2026-03-31 -- `done/main-093-collapsed-group-speaker-names.md`
- **main-094** -- Delete Audio Files While Keeping Transcript -- 2026-03-31 -- `done/main-094-delete-audio-keep-transcript.md`
- **main-091** -- Reorder and resize conversation list columns -- 2026-03-27 -- `done/main-091-reorder-conversations-columns.md`
- **main-086** -- Transcripts list column redesign -- 2026-03-26 -- `done/main-086-transcripts-column-redesign.md`
- **main-085** -- Template Grouping with Collapsible Sections -- 2026-03-26 -- `done/main-085-template-grouping.md`
- **main-069** -- Transcript Analysis with Local LLM -- 2026-03-26 -- `done/main-069b-transcript-analysis-with-local-llm.md`
- **main-090** -- Add proper play icon to "Open in Player" button -- 2026-03-26 -- `done/main-090-open-in-player-icon.md`
- **main-089** -- Fix speaker dropdown selection not applying -- 2026-03-26 -- `done/main-089-fix-speaker-dropdown-selection.md`
- **main-088** -- System Templates — WhisperHeim Group with "Repeat" Command -- 2026-03-26 -- `done/main-088-system-templates-whisperhiem-group.md`
- **main-077** -- Fix Diarization -- VAD-Only Mic + Constrained Loopback -- 2026-03-25 -- `done/main-077-fix-diarization-vad-mic-constrained-loopback.md`
- **main-078** -- Fix Temporal Ordering -- Clock Drift Correction -- 2026-03-25 -- `done/main-078-fix-temporal-ordering-clock-drift.md`
- **main-075** -- Transcription Queue Service + Bottom Bar UI -- 2026-03-25 -- `done/main-075-transcription-queue-and-bottom-bar.md`
- **main-076** -- Active Recording Card + Auto-Transcribe on Stop -- 2026-03-25 -- `done/main-076-active-recording-card-and-auto-transcribe.md`
- **main-079** -- Fix Speaker Assignment UI -- ComboBox Bug + Per-Segment Reassignment -- 2026-03-25 -- `done/main-079-fix-speaker-assignment-ui.md`
- **main-083** -- Unify Recordings & File Transcription -- 2026-03-25 -- `done/main-083-unify-recordings-and-file-transcription.md`
- **main-084** -- Sidebar Collapse Icon & Branding Reshuffle -- 2026-03-25 -- `done/main-084-sidebar-collapse-icon-and-branding-reshuffle.md`
- **main-082** -- Fix Date Column & Add Column Sorting -- 2026-03-25 -- `done/main-082-fix-date-column-and-sorting.md`
- **main-080** -- Drawer -- Remove Overlay, Crossfade Between Recordings -- 2026-03-25 -- `done/main-080-drawer-no-overlay-crossfade.md`
- **main-081** -- Fix Library Voices Not Showing in TTS Combo Box -- 2026-03-25 -- `done/main-081-fix-library-voices-combo-box.md`
- **main-071** -- Notion-Style List View with Detail Drawer -- 2026-03-24 -- `done/main-071-notion-style-list-and-drawer.md`
- **main-072** -- Fix Recording Delete Not Removing List Item -- 2026-03-24 -- `done/main-072-fix-recording-delete-stale-ui.md`
- **main-074** -- Transcription Engine Busy Guard -- 2026-03-24 -- `done/main-074-transcription-engine-busy-guard.md`
- **main-073** -- Speaker Name List, Count Hint, and Manual Transcription -- 2026-03-24 -- `done/main-073-speaker-list-and-manual-transcribe.md`
- **main-068** -- Pill-Shaped Waveform Overlay at Last Click Position -- 2026-03-23 -- `done/main-068-pill-waveform-overlay.md`
- **main-067** -- Dictation Page Responsive Layout & Card Alignment -- 2026-03-23 -- `done/main-067-dictation-page-responsive-layout.md`
- **main-068** -- Transcripts Page Export Cleanup -- 2026-03-23 -- `done/main-068b-transcripts-export-cleanup.md`
- **main-070** -- Fix Pill Overlay Visualization -- 2026-03-23 -- `done/main-070-fix-pill-overlay-visualization.md`
- **main-069** -- Fix Start Minimized Setting Ignored on Launch -- 2026-03-23 -- `done/main-069-fix-start-minimized-setting.md`
- **main-061** -- Update Dictation Page Hotkey Labels -- 2026-03-22 -- `done/main-061-update-dictation-hotkey-labels.md`
- **main-060** -- Show Full Logo in Collapsed Sidebar -- 2026-03-22 -- `done/main-060-collapsed-sidebar-logo-visible.md`
- **main-059** -- Rework Read-Aloud Hotkey to Navigate to TTS Page -- 2026-03-22 -- `done/main-059-rework-read-aloud-hotkey.md`
- **main-066** -- Template Delete with Hover Trash Icon & Confirmation Dialog -- 2026-03-22 -- `done/main-066-template-delete-hover-trash.md`
- **main-065** -- About Page with Profile, Contact Links & Ko-fi -- 2026-03-22 -- `done/main-065-about-page-with-profile.md`
- **main-063** -- Configurable Config/Data Path for Cloud Sync -- 2026-03-22 -- `done/main-063-configurable-data-path.md`
- **main-044** -- Fix Theme Persistence and Settings Highlight -- 2026-03-22 -- `done/main-044-theme-persistence.md`
- **main-043** -- Faithful Quiet Engine Restyling (All Pages) -- 2026-03-22 -- `done/main-043-quiet-engine-restyling.md`
- **main-042** -- TTS Voice Pre-Caching on Startup -- 2026-03-22 -- `done/main-042-tts-voice-warm-up.md`
- **main-047** -- Fix TTS Voice Cards Dark Mode Background -- 2026-03-22 -- `done/main-047-tts-voice-cards-dark-mode.md`
- **main-046** -- Remember Window Size and Position -- 2026-03-22 -- `done/main-046-window-size-and-position.md`
- **main-045** -- Consistent Max-Width Across All Pages -- 2026-03-22 -- `done/main-045-consistent-page-max-width.md`
- **main-038** -- Transcript Audio Playback -- 2026-03-22 -- `done/main-038-transcript-audio-playback.md`
- **main-037** -- Speaker Name Editing -- 2026-03-22 -- `done/main-037-speaker-name-editing.md`
- **main-036** -- Transcript Naming (Editable Title) -- 2026-03-22 -- `done/main-036-transcript-naming.md`
- **main-041** -- Default Read-Aloud Voice from TTS Page -- 2026-03-22 -- `done/main-041-default-read-aloud-voice.md`
- **main-040** -- UI redesign — navigation restructure & TTS page merge -- 2026-03-22 -- `done/main-040-ui-redesign-navigation-and-tts-merge.md`
- **main-039** -- Read-Aloud Overlay Indicator -- 2026-03-22 -- `done/main-039-read-aloud-overlay.md`
- **main-048** -- Tray Icon Green When Recording -- 2026-03-22 -- `done/main-048-tray-icon-green-recording.md`
- **main-055** -- Rename Export Button to "MD" -- 2026-03-22 -- `done/main-055-export-button-rename-md.md`
- **main-054** -- Hover Trash Icon per Transcript -- 2026-03-22 -- `done/main-054-hover-delete-transcript.md`
- **main-056** -- Link AI Model Cards to GitHub Projects -- 2026-03-22 -- `done/main-056-model-cards-github-links.md`
- **main-058** -- Layout Fixes and Branding Cleanup -- 2026-03-22 -- `done/main-058-layout-fixes-and-branding-cleanup.md`
- **main-057** -- Redesign WhisperHeim Logo -- 2026-03-22 -- `done/main-057-logo-redesign.md`
- **main-051** -- Reduce Templates List Column Width -- 2026-03-22 -- `done/main-051-templates-column-width.md`
- **main-050** -- Collapsible Sidebar Menu -- 2026-03-22 -- `done/main-050-collapsible-sidebar.md`
- **main-049** -- Add WhisperHeim Logo to Sidebar -- 2026-03-22 -- `done/main-049-sidebar-logo.md`
- **main-053** -- Reduce Transcripts List Column Width -- 2026-03-22 -- `done/main-053-transcripts-list-width.md`
- **main-052** -- Remove Magic Replace from Edit Template -- 2026-03-22 -- `done/main-052-remove-magic-replace.md`
- **main-013** -- Template System -- 2026-03-21 -- `done/main-013-template-system.md`
- **main-012** -- Dictation Overlay -- 2026-03-21 -- `done/main-012-dictation-overlay.md`
- **main-010** -- Input Simulation -- 2026-03-21 -- `done/main-010-input-simulation.md`
- **main-011** -- End-to-End Dictation -- 2026-03-21 -- `done/main-011-end-to-end-dictation.md`
- **main-014** -- Microphone Selection -- 2026-03-21 -- `done/main-014-microphone-selection.md`
- **main-018** -- Call Transcription Pipeline -- 2026-03-21 -- `done/main-018-call-transcription-pipeline.md`
- **main-017** -- Speaker Diarization with sherpa-onnx -- 2026-03-21 -- `done/main-017-speaker-diarization.md`
- **main-015** -- WASAPI Loopback Audio Capture -- 2026-03-21 -- `done/main-015-wasapi-loopback.md`
- **main-016** -- Dual Audio Capture for Call Recording -- 2026-03-21 -- `done/main-016-dual-capture.md`
- **main-003** -- Settings Infrastructure -- 2026-03-21 -- `done/main-003-settings-infrastructure.md`
- **main-004** -- Global Hotkey -- 2026-03-21 -- `done/main-004-global-hotkey.md`
- **main-002** -- Tray Icon and Window -- 2026-03-21 -- `done/main-002-tray-icon-and-window.md`
- **main-001** -- Project Scaffolding -- 2026-03-21 -- `done/main-001-project-scaffolding.md`
- **main-005** -- Model Manager -- 2026-03-21 -- `done/main-005-model-manager.md`
- **main-008** -- Parakeet ASR Integration -- 2026-03-21 -- `done/main-008-parakeet-asr.md`
- **main-009** -- Streaming Dictation Pipeline -- 2026-03-21 -- `done/main-009-streaming-dictation-pipeline.md`
- **main-006** -- Audio Capture Service -- 2026-03-21 -- `done/main-006-audio-capture-service.md`
- **main-007** -- Silero VAD Integration -- 2026-03-21 -- `done/main-007-silero-vad.md`
- **main-019** -- Transcript Viewer and Export -- 2026-03-21 -- `done/main-019-transcript-viewer.md`
- **main-031** -- Voice cloning from system audio loopback -- 2026-03-21 -- `done/main-031-voice-cloning-loopback.md`
- **main-030** -- Voice cloning from microphone recording -- 2026-03-21 -- `done/main-030-voice-cloning-mic.md`
- **main-029** -- Pocket TTS engine service + model download -- 2026-03-21 -- `done/main-029-tts-engine-service.md`
- **main-032** -- Read selected text via global hotkey -- 2026-03-21 -- `done/main-032-read-selected-text.md`
- **main-035** -- TTS settings + hotkey configuration -- 2026-03-21 -- `done/main-035-tts-settings.md`
- **main-034** -- Audio export (MP3/OGG) -- 2026-03-21 -- `done/main-034-audio-export.md`
- **main-033** -- TTS UI page -- 2026-03-21 -- `done/main-033-tts-ui-page.md`
- **main-028** -- Post-recording transcription pipeline with progress UI -- 2026-03-21 -- `done/main-028-post-recording-transcription-ui.md`
- **main-023** -- Kyutai Pocket TTS Integration -- 2026-03-21 -- `done/main-023-tts-pocket-tts.md`
- **main-024** -- Windows startup auto-launch -- 2026-03-21 -- `done/main-024-windows-auto-launch.md`
- **main-020** -- Audio File Transcription Service -- 2026-03-21 -- `done/main-020-audio-file-transcription.md`
- **main-021** -- Drag-and-Drop Transcription UI -- 2026-03-21 -- `done/main-021-drag-drop-ui.md`
- **main-026** -- Wire call recording services in app startup -- 2026-03-21 -- `done/main-026-wire-call-recording-services.md`
- **main-027** -- Tray context menu for start/stop call recording -- 2026-03-21 -- `done/main-027-tray-menu-call-recording.md`
- **main-025** -- Overlay Microphone State Visualization -- 2026-03-21 -- `done/main-025-overlay-mic-state-visualization.md`
- **main-062** -- TTS Page Layout Cleanup -- `done/main-062-tts-page-layout-cleanup.md`
- **main-064** -- Fix Opaque Backgrounds and Delete Dialog -- `done/main-064-fix-opaque-backgrounds-and-dialog.md`
- **main-087** -- Branding Header as Sidebar Toggle -- `done/main-087-branding-header-sidebar-toggle.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
<!-- no tasks in backlog -->
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
<!-- no ADRs scoped to this BC -->
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
- **STT API exposure** (2026-06-19) — transports, in-process hosting, security, and the resolved Utterheim house pattern for exposing STT to other apps. — `knowledge/research/whisperheim-stt-api-exposure-2026-06-19.md`
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- no concept pages yet -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
