# whisperheim-transcribe

A Claude Code plugin that transcribes audio files to text through [WhisperHeim](../README.md), the local speech-to-text tray app. Installed once into Claude Code; works in any project. The inverse of the sibling `utterheim-narrator` plugin — where that one *speaks*, this one *listens*.

## How it works

- The `/transcribe <file>` slash command runs a bundled PowerShell shim that POSTs the audio file's raw bytes to WhisperHeim's loopback endpoint `http://127.0.0.1:7777/transcribe` and prints back only the transcript text.
- Transcription is **on-demand / pull** (you ask for it), unlike the narrator's push hooks. There are no hooks — nothing fires on its own.
- **Zero dependencies:** the shim is pure PowerShell hitting the HTTP API (ADR-0001). It does not need the `whisperheim-transcribe.exe` CLI on your PATH or a path to the WhisperHeim repo.
- The WhisperHeim tray app must be running. If it isn't, the command reports exit `3` and tells you to start it.

## Install

From inside Claude Code, in any project:

```
/plugin marketplace add <path-to-whisperheim-repo>/claude-code-plugin
/plugin install whisperheim-transcribe@whisperheim-transcribe
```

`<path-to-whisperheim-repo>` is the absolute path to a local clone (e.g. `C:/src/heimeshoff/tooling/WhisperHeim`) or a `git` URL. The marketplace lives inside the `claude-code-plugin/` subdirectory of the WhisperHeim repo, so the plugin tracks the same source as the tray app itself. Restart Claude Code after installing so the command is picked up.

### Updates

Local/third-party marketplaces have auto-update **disabled** by default. To enable it, run `/plugin`, go to the **Marketplaces** tab, select `whisperheim-transcribe`, and choose "Enable auto-update". To update manually:

```
/plugin marketplace update whisperheim-transcribe
/plugin update whisperheim-transcribe@whisperheim-transcribe
/reload-plugins
```

The plugin has its own `version` field in `.claude-plugin/plugin.json`. Claude Code only prompts an update when *that* bumps — commits to the WhisperHeim app that don't change the plugin won't churn consumers.

## Usage

```
/transcribe C:\path\to\voice-message.ogg
/transcribe "C:\path\with spaces\meeting.m4a"
```

Claude runs the transcription and shows you the transcript. Empty/no-speech audio comes back as "No speech detected". For long files the call blocks until the engine finishes — there is no client timeout.

You can also call the shim directly (outside Claude):

```powershell
& "<plugin>/scripts/transcribe.ps1" -Path C:\path\to\audio.ogg
```

### Endpoint override

The shim resolves its endpoint as: `-Endpoint` parameter → `$env:WHISPERHEIM_ENDPOINT` → `http://127.0.0.1:7777`. Set `WHISPERHEIM_ENDPOINT` to point at a non-default port (matching `WHISPERHEIM_TRANSCRIBE_PORT` on the app side).

### Exit codes (`scripts/transcribe.ps1`)

| Code | Meaning |
|------|---------|
| `0` | success — transcript printed to stdout (a blank line for no-speech audio) |
| `1` | usage / file error — no path, or a missing / unreadable file (before any network call) |
| `2` | HTTP non-success — WhisperHeim returned an error (e.g. unsupported/corrupt audio); body on stderr |
| `3` | cannot reach the endpoint — WhisperHeim not running, port unreachable, or timed out |

## Platform support

WhisperHeim is a Windows-only WPF app and the command shells out to PowerShell. On **macOS / Linux** the endpoint won't be reachable; the command will report it isn't running.

## Layout

```
.claude-plugin/plugin.json         # plugin manifest
.claude-plugin/marketplace.json    # local-directory marketplace descriptor
commands/transcribe.md             # /transcribe slash command
scripts/transcribe-lib.ps1         # pure helpers: endpoint resolution, URL build, text extraction
scripts/transcribe.ps1             # POSTs raw audio bytes, prints the transcript
tests/transcribe-lib.Tests.ps1     # Pester spec for the pure helpers
```

## Relationship to the CLI

The WhisperHeim repo also ships a `whisperheim-transcribe.exe` CLI (built alongside the tray exe). The plugin and the CLI are independent front-ends over the same `POST /transcribe` endpoint — the plugin uses its own bundled PowerShell shim so it needs nothing on your PATH. Use the CLI when you want a plain shell command; use the plugin when you want Claude to transcribe for you in any project.
