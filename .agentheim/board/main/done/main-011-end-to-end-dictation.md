---
id: main-011
title: End-to-End Dictation
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-004, main-009, main-010]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Medium
---
# End-to-End Dictation

## Objective
Pressing the hotkey starts dictation; spoken text appears at the cursor; pressing again stops.

## Details
Wire the global hotkey to the dictation pipeline. First press: start audio capture + pipeline, update tray icon to "recording" state. Pipeline partial/final results feed into InputSimulator. Second press: stop pipeline, finalize any pending text, restore tray icon. Handle edge cases: hotkey pressed while pipeline is starting, rapid double-press, error during transcription. Tray icon should change color/icon to indicate active dictation state.

## Acceptance Criteria
- [x] Full flow works: hotkey -> speak -> text appears -> hotkey -> stops
- [x] Works across multiple applications
- [x] Tray icon reflects state
- [x] No crashes on rapid toggle

## Notes
Toggle behavior: first press starts, second press stops. Tray icon state change for visual feedback.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Integration wiring complete

**Created:**
- `src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs` — New orchestration service that wires hotkey toggle to dictation pipeline start/stop, feeds pipeline partial results into InputSimulator via TypeTextAsync, updates tray icon state via UI-thread callback, and guards against rapid double-press with a toggle lock.

**Modified:**
- `src/WhisperHeim/App.xaml.cs` — Instantiates SileroVadService, TranscriptionService (with LoadModel), DictationPipeline, and InputSimulator; passes them to MainWindow constructor.
- `src/WhisperHeim/MainWindow.xaml.cs` — Extended constructor to accept IDictationPipeline + IInputSimulator; registers GlobalHotkeyService on Loaded event; creates and starts DictationOrchestrator; tray icon now has idle (white) and recording (red) states; disposes orchestrator and hotkey service on exit.

**Details:**
- Hotkey press toggles pipeline: first press calls `_pipeline.Start()`, second press calls `_pipeline.Stop()`.
- Pipeline PartialResult events invoke `_inputSimulator.TypeTextAsync()` to type incremental text into the active window.
- Tray icon switches between white (idle) and red (recording) microphone glyph.
- Rapid toggle guard (`_isToggling` flag) prevents concurrent start/stop.
- Pipeline errors auto-restore idle tray state.
- Build: 0 errors, 0 warnings. Tests: 21 passed, 0 failed.
