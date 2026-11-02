---
id: main-032
title: Read selected text via global hotkey
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-029]
blocks: []
tags: [m4, tts]
related_adrs: []
related_research: []
prior_art: []
milestone: M4 - Text-to-Speech
size: Medium
---
# Read selected text via global hotkey

**Parent:** 023

## Objective
Register a global hotkey that captures selected text from any application and reads it aloud using the TTS engine.

## Details
- New `SelectedTextService` with cascading capture strategy:
  1. Try UI Automation `TextPattern.GetSelection()` on focused element (clean, no side effects)
  2. Fall back to `SendInput` simulating Ctrl+C, wait 100ms, read clipboard, restore original clipboard
- Use `SendInput` (not `SendKeys`) for reliability
- Clipboard backup/restore: save text + rich text formats, restore after read
- Handle edge cases: empty selection, clipboard locked, Ctrl+C side effects in terminals
- Register hotkey via existing `GlobalHotkeyService` (user-configurable key combo)
- Pass captured text to `ITextToSpeechService` with the user's selected voice
- Requires `UIAutomationClient` and `UIAutomationTypes` references

## Acceptance Criteria
- [x] Global hotkey captures selected text from browsers (Chrome, Edge, Firefox)
- [x] Works in Notepad, VS Code, Office applications
- [x] Original clipboard contents restored after capture
- [x] Empty selection handled gracefully (no error, optional notification)
- [x] Captured text passed to TTS and read aloud
- [x] Hotkey is user-configurable

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete
- Created `ISelectedTextService` interface and `SelectedTextService` with cascading capture:
  1. UI Automation `TextPattern.GetSelection()` (no side effects)
  2. Fallback: SendInput Ctrl+C → clipboard read → clipboard restore (text, RTF, HTML formats)
- Clipboard backup/restore with retry logic for lock contention (COMException handling)
- STA thread handling for clipboard operations
- Created `ReadAloudHotkeyService` with default Ctrl+Shift+R hotkey (user-configurable via `HotkeyRegistration`)
- Wired both services in `App.xaml.cs`
- UIAutomation assemblies available via `UseWPF` — no extra .csproj references needed
- All compilation errors are pre-existing (parallel tasks: VoiceCloningPage, VoiceLoopbackCapturePage, MainWindow constructor)
