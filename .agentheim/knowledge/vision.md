# Vision: WhisperHeim

## Problem

Voice interaction on Windows is fragmented and cloud-dependent. Dictation tools either require internet, cost money per minute, or produce results only after you stop speaking. Transcribing voice messages from WhatsApp/Telegram means manual file juggling. Meeting transcription requires third-party SaaS with privacy trade-offs. There is no single, local-first, always-available tool that unifies all voice-to-text and text-to-voice workflows on Windows 11.

## Target Users

Power users and knowledge workers on Windows 11 who:
- Dictate text across many applications (terminals, editors, browsers, chat)
- Receive voice messages on WhatsApp/Telegram and want them as searchable text
- Attend video calls (Zoom, Meet, Teams) and need transcripts with speaker attribution
- Value privacy and independence from cloud services
- Want zero ongoing cost

## Value Proposition

One tray app. All voice workflows. Fully local. No internet required, no subscription, no cloud.

WhisperHeim is the "audio Swiss army knife" for Windows 11 -- live dictation, voice message transcription, call recording with speaker separation, and text templates, all running on local AI models with zero cloud dependency.

## Key Features

### Milestone 1: Live Dictation + Core App

1. **System tray app** -- lightweight, always running, PowerToys-inspired Fluent UI (Mica backdrop, WPF + WPF UI)
2. **Live streaming dictation** -- press Ctrl+Win to start dictating. Text appears live (1-2s latency) at the cursor position in any application. Press hotkey again to stop.
3. **VAD-powered streaming** -- Silero VAD detects speech boundaries; only speech segments are transcribed. No hallucinations during silence.
4. **Primary ASR model: NVIDIA Parakeet TDT 0.6B** -- 10x faster than Whisper Turbo, lower VRAM (~2-3GB), no silence hallucinations. Supports German + English.
5. **Text templates** -- define named text snippets. Trigger via dedicated hotkey + voice (e.g., Alt+Win then say "greeting"). Template text is inserted at cursor.
6. **Global input simulation** -- uses Windows SendInput API to type into any focused application (terminal, browser, Word, Claude Code, etc.)
7. **Auto model download** -- models are downloaded on first run and stored locally (~600MB for Parakeet).

### Milestone 2: Audio Capture + Call Transcription

8. **WASAPI loopback recording** -- captures all system audio (Zoom, Meet, Teams, browser video, any app producing sound)
9. **Speaker diarization** -- separates speakers ("Speaker 1", "Speaker 2") using sherpa-onnx with pyannote ONNX models. Fully local.
10. **Transcript output** -- produces timestamped, speaker-attributed transcripts. Exportable as plain text, Markdown, or JSON.
11. **Mic + loopback dual capture** -- records both the user's mic and system audio simultaneously for complete call transcription.

### Milestone 3: Voice Message Transcription

12. **Drag-and-drop transcription** -- drag OGG/MP3/M4A files (from WhatsApp Web, Telegram, file explorer) onto the app window or tray icon to transcribe.
13. **Telegram bot integration** -- optional: connect a Telegram bot that auto-transcribes forwarded voice messages and replies with text.
14. **Batch processing** -- drop multiple files, get all transcripts.

### Milestone 4: Text-to-Speech (details TBD)

15. **AI voice generation** -- feed text, hear it read aloud with selectable voices. Model and approach to be decided later.

## Non-Goals

- **No voice commands / app launching** -- not building a voice assistant. Templates only.
- **No cloud dependency** -- all AI models run locally. No API keys, no accounts, no internet required at runtime.
- **No cross-platform** -- Windows 11 only. No compromises for portability.
- **No web UI** -- native Windows app only. No browser-based settings or dashboards.
- **No per-application audio capture** -- WASAPI loopback captures all system audio. Per-app isolation is out of scope.
- **No real-time call subtitles** -- call transcription produces a transcript after the call, not live captions.

## Technical Architecture

### Stack

| Layer | Technology |
|-------|-----------|
| **UI** | WPF + WPF UI (lepoco/wpfui) + WPF-UI.Tray |
| **Language** | C# (.NET 9+) |
| **ASR Model** | NVIDIA Parakeet TDT 0.6B (ONNX) via sherpa-onnx |
| **VAD** | Silero VAD (ONNX, ~1MB) |
| **Diarization** | sherpa-onnx with pyannote segmentation 3.0 ONNX models |
| **Audio Capture** | NAudio (microphone) + WASAPI loopback (system audio) |
| **Input Simulation** | Windows SendInput API |
| **Hotkeys** | Raw Win32 RegisterHotKey |
| **Model Runtime** | ONNX Runtime (Microsoft.ML.OnnxRuntime) |

### Key Design Decisions

- **Parakeet over Whisper**: 10x faster, no hallucinations, better accuracy for EN/DE. Trade-off: fewer languages (25 European vs 99+), acceptable for this use case.
- **sherpa-onnx over Python sidecar**: Native .NET NuGet, no Python dependency, simpler deployment. If diarization accuracy is insufficient, can add pyannote Python sidecar later.
- **WPF over WinUI 3**: WinUI 3 has no tray icon support. WPF UI provides identical Fluent Design aesthetics with native tray support.
- **C# over F#**: Project complexity is in systems plumbing (audio pipelines, Win32 interop, threading), not domain modeling. C# has better tooling and ecosystem for this.
- **SendInput over clipboard**: Clipboard-paste approach would clobber user's clipboard. SendInput simulates keystrokes natively.

### Streaming Dictation Pipeline

```
Microphone (16kHz mono)
    |
    v
Ring Buffer (~64ms chunks)
    |
    v
Silero VAD (speech/silence detection, <1ms per frame)
    |
    |-- silence --> discard, continue
    |
    v  speech detected
Accumulate into segment buffer
    |
    |-- silence after speech --> finalize segment
    |
    v
Parakeet TDT transcription (~200-500ms)
    |
    v
Diff against previous partial result
    |
    v
SendInput new characters to focused window
```

### Call Transcription Pipeline

```
WASAPI Loopback (system audio) + Microphone (user audio)
    |                                |
    v                                v
Separate audio streams, saved to temp files
    |
    v  (on stop recording)
Merge + Diarize (sherpa-onnx pyannote)
    |
    v
Transcribe each speaker segment (Parakeet)
    |
    v
Produce timestamped, speaker-attributed transcript
```

## Success Criteria

- **Dictation latency**: text appears within 2 seconds of speaking, ideally under 1 second
- **Dictation accuracy**: comparable to or better than Windows built-in speech recognition for EN/DE
- **Always available**: tray icon present, hotkey responsive, no perceptible startup delay
- **Zero cost**: no subscriptions, no API usage, no internet required
- **Memory footprint**: under 2GB RAM while idle (model loaded), under 3GB during active transcription
- **First-run experience**: download models automatically, be ready to dictate within 5 minutes of install
