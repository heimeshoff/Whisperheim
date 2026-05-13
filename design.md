# Whisperheim -- UI Design Brief

## What is Whisperheim?

Whisperheim is a local-first Windows 11 tray application that unifies voice-to-text and text-to-voice workflows. No cloud, no subscriptions, no internet required. Think of it as the **audio Swiss army knife for Windows 11**.

Target users are power users and knowledge workers who dictate frequently, attend video calls, receive voice messages, and value privacy. The app lives in the system tray and is always one hotkey away.

### Current tech stack (for design context)

- WPF + WPF UI (Fluent Design with Mica backdrop)
- Single-window app with left sidebar navigation and a content area
- System tray icon with context menu
- 7 pages accessible from the sidebar

---

## App Shell

The app has a **single window** with two zones:

1. **Left sidebar** -- vertical navigation list with icons and labels for all 7 pages.
2. **Content area** -- the active page.

The window uses **Windows 11 Fluent Design** (Mica material backdrop, rounded corners, Segoe UI Variable). There is also a **system tray icon** with a right-click menu offering quick actions (start call recording, open settings, exit).

---

## Screens

### 1. General (Settings)

**Purpose:** Basic app-level preferences.

**What's on it:**
- "Start minimized" toggle -- launch hidden in the system tray.
- "Launch at startup" toggle -- auto-start with Windows.

**Design notes:** This is a simple, low-density settings page. It will likely grow over time as more app-wide preferences are added (theme, language, update checking, etc.). Design it to accommodate future growth without feeling empty today.

---

### 2. Dictation

**Purpose:** Configure the microphone for live dictation and show the user which global hotkeys are available.

**What's on it:**
- **Microphone selector** -- dropdown listing available audio input devices.
- **Device warning** -- an orange inline alert shown when the selected device is unavailable or misconfigured.
- **Hotkey reference card** listing all global shortcuts:
  - `Ctrl+Win` -- Toggle dictation on/off
  - `Ctrl+Win+Ä` -- Read selected text aloud (TTS)
  - `Ctrl+Shift+Win+R` -- Toggle call recording
  - `Alt+Win` -- Trigger template (planned)
- **Test area** -- a text box where the user can try dictation live and see output appear in real time.

**User workflow:** Select mic, glance at hotkeys, test dictation in the text box, then minimize and use the hotkeys system-wide.

**Design notes:** This is the "home base" most users will visit first. It should feel welcoming and make the hotkeys very scannable. The test area should clearly communicate "speak now" state vs. idle.

---

### 3. Templates

**Purpose:** Create and manage reusable text snippets with placeholders that can be inserted via voice command.

**What's on it:**
- **Template list** (left column) -- shows template name and a short preview of the text body.
- **Edit area** (right column):
  - Name field
  - Multi-line text body field
  - Supported placeholders: `{date}`, `{time}`, `{clipboard}`
  - Add / Update / Delete buttons

**User workflow:** Select a template from the list to edit it, or fill in the fields and click Add to create a new one. Templates are triggered by voice during dictation (e.g., say "greeting" to insert the "greeting" template).

**Design notes:** Master-detail layout. The list should support reordering in the future. Make placeholders visually distinct (e.g., pill/tag styling) so users understand they're dynamic.

---

### 4. Transcribe Files

**Purpose:** Batch-transcribe audio files (voice messages, recordings) via drag-and-drop.

**What's on it:**
- **Drop zone** -- large area with drag-and-drop affordance and a "Browse files" button. Accepted formats: `.ogg`, `.mp3`, `.m4a`, `.wav`.
- **Results list** -- one card per file showing:
  - File name and processing status
  - Progress bar (while transcribing)
  - Transcript text (after completion)
  - Duration info
  - Copy and Save buttons
  - Error message (on failure)

**User workflow:** Drag files (e.g., WhatsApp voice messages) onto the page. Transcription begins automatically. Results appear in-place. Copy or save individual transcripts.

**Design notes:** The drop zone should be prominent when the list is empty and shrink/collapse once files are added. Progress should feel responsive. Consider a "clear all" action for completed items.

---

### 5. Transcripts (Call Recordings)

**Purpose:** Browse, search, view, and export transcripts from recorded calls/meetings.

**What's on it:**
- **"Transcribing..." banner** -- shown at top when a recording is being processed.
- **Search box** with clear button.
- **Split layout:**
  - **Left panel** -- list of transcripts showing date, duration, and a text preview.
  - **Right panel** -- full transcript viewer:
    - Header with title and metadata (date, duration)
    - Speaker-attributed segments with timestamps, speaker labels, and color-coded backgrounds per speaker
    - Action bar: Copy to Clipboard, Export as Markdown / JSON / Text
- **Empty state placeholder** when no transcript is selected.

**User workflow:** Start a call recording via `Ctrl+Shift+Win+R`. After the call, come here to review the transcript. Search, browse, select, read, export.

**Design notes:** This is the most information-dense screen. Speaker colors should be distinct but not garish. Timestamps should be de-emphasized (secondary text). The transcript should be comfortable to read -- think chat-bubble or interview-transcript styling. Search should filter the list in real time.

---

### 6. Text to Speech

**Purpose:** The unified page for all speech synthesis -- type text and hear it spoken, manage voices, and create new voices by cloning from mic or system audio.

This page has two sections that work together: **Speak** (the main area) and **Voices** (voice management and cloning).

#### Section A: Speak

The primary area of the page. This is what the user sees and uses most.

**What's on it:**
- **Text input card** -- multi-line text area with a character counter.
- **Voice selector** -- dropdown listing built-in and custom (cloned) voices. Includes a Preview button to hear a short sample.
- **Progress bar** -- shown during speech generation.
- **Playback controls:**
  - Play (primary action, prominent)
  - Stop
  - Save as... (export to MP3/OGG/WAV)
- **Status text** -- current state (e.g., "Generating...", "Playing", idle).
- **Tip** about the `Ctrl+Win+Ä` hotkey for reading selected text from any app.

**User workflow:** Type/paste text, pick a voice, hit Play. Optionally save the audio file. Or: select text anywhere on the system, press `Ctrl+Win+Ä`, and hear it read aloud without opening this page.

#### Section B: Voices

Below (or collapsible/expandable beneath) the Speak section. This is where the user manages and creates custom voices.

**What's on it:**

- **Custom Voices list** -- saved voices with preview and delete actions. This is always visible so the user can see what they have.

- **Clone New Voice** area with a **source toggle** to switch between two modes:
  - **Microphone** -- record your own voice via a mic input.
  - **System Audio** -- capture a voice from what's playing on the PC (YouTube, podcast, audiobook, etc.).

- **Shared controls** (identical in both modes -- reuse the same component):
  - Voice name input (max 50 characters)
  - Start / Stop recording buttons
  - Audio level meter -- real-time visual feedback of input volume
  - Duration display with "minimum 5 seconds" indicator and a progress bar filling to the minimum
  - Save Voice button -- enabled once the minimum duration is reached
  - Status / result text

- **Mode-specific controls** (the only thing that changes between modes):
  - *Microphone mode:* Microphone device selector dropdown + quality tip ("use a quiet environment").
  - *System Audio mode:* Output device selector dropdown + tip ("close other audio apps, play the voice you want to clone").

**User workflow (Microphone):** Toggle to Microphone, select mic, enter a voice name, hit record, speak for 10-15 seconds, stop, save. The voice immediately appears in the voice selector above.

**User workflow (System Audio):** Toggle to System Audio, select output device, start playing a video/podcast of the target voice, enter a name, hit record, let it capture 5-15 seconds, stop, save.

**Design notes:**
- The **source toggle** (Microphone / System Audio) should be a segmented control or tab pair -- clearly communicating two modes of the same action. Use distinct icons: a microphone icon vs. a speaker/monitor icon.
- The shared recording controls (level meter, duration bar, start/stop, save) should be a single reusable component that looks and behaves identically in both modes. Only the device selector and tip text swap out.
- The recording state should be unmistakable (pulsing indicator, red accent). The level meter helps users confirm audio is being captured.
- The 5-second minimum progress bar should feel like a "fill the bar" mini-goal. Optimal is 10-15 seconds -- communicate this without making 5s feel like a failure.
- The Speak section should feel like the **primary** purpose of the page. Voices/cloning is supporting -- important but secondary. Consider making the Voices section collapsible or placed below a subtle divider so the Speak area dominates.
- The play button remains the dominant call-to-action on the page.

---

### 7. About

**Purpose:** Display app info, version, and AI model status.

**What's on it:**
- **App logo and title** ("Whisperheim").
- **Tagline:** "Live dictation powered by Whisper"
- **Version number** (currently 0.1.0).
- **AI Models list** -- one row per model showing:
  - Model name
  - Status (downloading / ready / error) with color-coded indicator
  - Description of what the model does
  - File size

**Design notes:** This doubles as a model management / health-check page. On first run, models auto-download, so this page shows progress. Status indicators should use clear iconography (checkmark = ready, spinner = downloading, warning = error). Consider grouping models by function (ASR, TTS, VAD, diarization).

---

## Global UI Elements (not tied to a specific page)

### System Tray Icon
- Always visible in the Windows 11 notification area.
- Left-click: show/restore the window.
- Right-click context menu:
  - Start/Stop Call Recording
  - Settings
  - Exit

### Dictation Overlay (not a page)
- A small floating indicator shown on screen when dictation is active.
- Communicates mic state (listening / processing / idle).
- Should be minimal and non-intrusive -- think a small pill or dot near the cursor or screen edge.

---

## Design Principles

1. **Local-first confidence** -- The UI should reinforce that everything runs locally. No "connecting..." spinners, no account/login flows. The app is self-contained.
2. **Hotkey-first, UI-second** -- Most features are triggered via global hotkeys. The UI is for setup, configuration, and review -- not the primary interaction surface during work.
3. **Progressive disclosure** -- Simple pages (General, About) should stay simple. Complex pages (Transcripts) should reveal detail on demand.
4. **Windows 11 native feel** -- Mica material, rounded corners, Segoe UI Variable, Fluent iconography. It should feel like a first-party Windows app.
5. **Accessible** -- High contrast ratios, keyboard navigable, screen-reader-friendly labels.
