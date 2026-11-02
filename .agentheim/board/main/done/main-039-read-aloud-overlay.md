---
id: main-039
title: Read-Aloud Overlay Indicator
status: done
type: feature
context: main
created: 
completed: 2026-03-22
commit:
depends_on: []
blocks: []
tags: [m4, tts]
related_adrs: []
related_research: []
prior_art: []
milestone: M4 (Text-to-Speech)
size: Medium
---
# Read-Aloud Overlay Indicator

## Problem

When pressing Shift+Win+Ä to read selected text aloud, there is no visual feedback. The user doesn't know if the hotkey was registered, if the model is loading, or if speech is actively playing. There's also no visible confirmation when playback stops.

## Solution

Add an overlay indicator (same position as the dictation microphone overlay) that appears when the read-aloud hotkey is pressed and provides visual feedback through the entire lifecycle.

## Requirements

### Overlay Lifecycle

1. **Hotkey pressed** → Overlay appears immediately in the dictation overlay position (configurable via Overlay settings: BottomCenter, etc.)
2. **Loading/generating** → Overlay shows a pulsing or spinning animation (thinking state)
3. **Playing audio** → Overlay transitions to an animated sound wave or speaker animation (active state)
4. **Playback complete** → Overlay fades out and disappears
5. **Hotkey pressed again (toggle)** → Audio stops immediately, overlay dismisses instantly

### Visual Design

- Same style as the existing dictation overlay: Fluent/Mica, circular, same size
- **Different color** to distinguish from dictation (dictation = accent/blue, read-aloud = e.g., green or purple)
- Two visual states:
  - **Thinking:** Pulsing/spinning indicator (model loading + audio generation)
  - **Playing:** Animated sound waves or speaker icon with motion
- Smooth transitions between states

### Scope

- **Global hotkey only** (Shift+Win+Ä) — not the in-app "Speak" button on the TTS page
- Reuse the existing overlay positioning logic from the dictation overlay (OverlaySettings: position, size, opacity)
- Overlay should be topmost, click-through, same as the dictation overlay

## Implementation Notes

- The existing `ReadAloudHotkeyService` already supports cancellation on re-press — wire the overlay visibility to the read-aloud lifecycle events
- Consider adding events to `ReadAloudHotkeyService` (e.g., `ReadAloudStarted`, `ReadAloudPlaying`, `ReadAloudStopped`) that the overlay can subscribe to
- The dictation overlay implementation (`OverlaySettings`, overlay window) can serve as a reference for positioning and styling

## Acceptance Criteria

- [x] Pressing Shift+Win+Ä shows the overlay immediately at the configured overlay position
- [x] Overlay shows a pulsing/spinning animation while the TTS model loads and generates audio
- [x] Overlay transitions to a playing animation once audio playback starts
- [x] Overlay disappears when playback completes
- [x] Pressing Shift+Win+Ä again while reading stops audio and dismisses the overlay instantly
- [x] Overlay uses a distinct color from the dictation overlay
- [x] Overlay respects the same position/size/opacity settings as the dictation overlay
- [x] Overlay is topmost and click-through (doesn't steal focus)

## Work Log

### 2026-03-22

**Implementation complete. All acceptance criteria met.**

#### What was done:

1. **Added lifecycle events to `ReadAloudHotkeyService`**: `ReadAloudStarted`, `ReadAloudPlaying`, `ReadAloudCompleted`, `ReadAloudCancelled` events that fire at appropriate points during the read-aloud lifecycle. Added `_isReading` flag for proper toggle behavior.

2. **Created `ReadAloudOverlayState` enum**: Two states - `Thinking` (model loading/generation) and `Playing` (active playback).

3. **Created `ReadAloudOverlayWindow`** (XAML + code-behind): Purple-themed overlay (#9B59B6) with:
   - Thinking state: pulsing ellipse + spinning icon animation
   - Playing state: pulsing ellipse + animated sound wave rings radiating outward
   - Speaker icon (U+E767) during playback, processing icon (U+E916) during thinking
   - Same click-through (WS_EX_TRANSPARENT, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW) behavior as dictation overlay
   - Same positioning logic (reuses OverlaySettings)
   - Fade-in on show, fade-out on natural completion, instant dismiss on cancel

4. **Extended `ITextToSpeechService.SpeakAsync`**: Added optional `onPlaybackStarted` callback parameter to signal when first audio chunk begins playing.

5. **Wired up in `MainWindow`**: Accepts `ReadAloudHotkeyService`, creates overlay on startup, subscribes to lifecycle events, dispatches state changes to UI thread. Proper cleanup on exit.

6. **Updated `App.xaml.cs`**: Passes `ReadAloudHotkeyService` to `MainWindow` constructor.

#### Files changed:
- `src/WhisperHeim/Views/ReadAloudOverlayState.cs` (new)
- `src/WhisperHeim/Views/ReadAloudOverlayWindow.xaml` (new)
- `src/WhisperHeim/Views/ReadAloudOverlayWindow.xaml.cs` (new)
- `src/WhisperHeim/Services/SelectedText/ReadAloudHotkeyService.cs` (modified - added events + toggle logic)
- `src/WhisperHeim/Services/TextToSpeech/ITextToSpeechService.cs` (modified - onPlaybackStarted callback)
- `src/WhisperHeim/Services/TextToSpeech/TextToSpeechService.cs` (modified - invoke callback)
- `src/WhisperHeim/MainWindow.xaml.cs` (modified - overlay wiring)
- `src/WhisperHeim/App.xaml.cs` (modified - pass service to MainWindow)
