---
description: Transcribe an audio file (OGG/MP3/M4A/WAV, or any format FFmpeg can read) to text via WhisperHeim's local speech-to-text API
argument-hint: "<path-to-audio-file>"
allowed-tools: PowerShell, Bash
---

Transcribe an audio file to text through WhisperHeim's local STT API and show the user the transcript. WhisperHeim's tray app must be running (it hosts the loopback endpoint `http://127.0.0.1:7777`); the bundled script POSTs the file's raw bytes and prints only the transcript.

`$ARGUMENTS` is the path to the audio file (OGG / MP3 / M4A / WAV decode natively; other formats such as `.opus` are transcoded via FFmpeg when it's installed). The path may be quoted and may contain spaces.

## Behavior

- **Empty `$ARGUMENTS`** — don't call anything. Ask the user for the audio file path (or tell them to drag one in), then stop. Do **not** use `AskUserQuestion` for this; a one-line prompt is enough.
- **Otherwise** — run the bundled transcribe shim on the given path and report the result per the exit code below.

## Step 1 — run the transcription

On Windows, prefer the `PowerShell` tool (avoids Bash quoting issues with paths that contain spaces or backslashes). Run the bundled script, passing the file path from `$ARGUMENTS` as `-Path`, and capture both stdout and the exit code:

```powershell
$out = & "${CLAUDE_PLUGIN_ROOT}/scripts/transcribe.ps1" -Path "<the file path from $ARGUMENTS>"
"EXIT=$LASTEXITCODE"
$out
```

(The script waits as long as the transcription takes — do not add a timeout.)

On non-Windows, or if the `PowerShell` tool isn't available, WhisperHeim is a Windows-only app and won't be reachable — tell the user it only runs on Windows and stop.

## Step 2 — report by exit code

- **`EXIT=0`** — success. Everything the script printed before the `EXIT=` line is the transcript (it may be a single empty line for no-speech audio). Present it to the user verbatim as the transcript — for anything longer than a sentence, put it in a fenced block. Do not summarize unless the user asked. If the transcript is empty, say "No speech detected in the audio."
- **`EXIT=1`** — usage / file error (missing or unreadable file). Show the stderr message; it usually means the path was wrong. Ask for a correct path.
- **`EXIT=2`** — WhisperHeim returned an HTTP error. Show the error body the script printed to stderr verbatim. Common cases: `415` (the file is corrupt or not audio) and `501` (the format needs FFmpeg, which isn't installed — the body says how to install it, e.g. `winget install Gyan.FFmpeg`).
- **`EXIT=3`** — WhisperHeim isn't reachable. Tell the user: "WhisperHeim isn't reachable at `http://127.0.0.1:7777` — start the WhisperHeim tray app and try again." (Override the endpoint by setting `WHISPERHEIM_ENDPOINT`.)

Keep the reporting terse — the transcript is the payload; don't wrap it in commentary.
