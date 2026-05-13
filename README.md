# Whisperheim

Whisperheim is the audio Swiss army knife for Windows 11. Local dictation, call
transcription with speaker separation, voice-message transcription. No cloud,
no subscription, no internet at runtime.

<!-- TODO: docs/media/install.mp4 - record the SmartScreen click-through on first public release -->
<!-- TODO: hero screenshot / short screen-recording of dictation in action -->

## Download

Grab the latest `WhisperHeim-*-win-Setup.exe` from the
[**Releases page**](https://github.com/heimeshoff/WhisperHeim/releases/latest).

The Release notes always include the SHA-256 of `Setup.exe` so you can verify
the download (`certutil -hashfile WhisperHeim-*-win-Setup.exe SHA256` in any
terminal).

## Install

Whisperheim is currently **unsigned** (see [why](docs/why-unsigned.md)), so
Windows will warn you on first run. Two paths:

1. **Windows Defender SmartScreen** — the dialog reads "Windows protected your PC".
   Click **More info**, then **Run anyway**.
2. **Smart App Control** — if the dialog reads "Smart App Control" instead of
   "Windows Defender SmartScreen", there is **no override**. SAC is opt-in but
   on by default on some fresh Windows 11 25H2 installs. You'll need to either
   turn SAC off system-wide (irreversible until OS reinstall) or wait for a
   signed build. Signing is on the roadmap post-UG registration —
   see [docs/why-unsigned.md](docs/why-unsigned.md).

<!-- TODO: docs/media/install.mp4 - 20-30 s screen recording of the SmartScreen click-through -->
A short screen recording of the click-through lives in
[`docs/media/install.mp4`](docs/media/install.mp4) once recorded.

## First run

- A small **setup window** opens and downloads the ~640 MB Parakeet ASR model
  from Hugging Face. One-time download; cached in `%APPDATA%\WhisperHeim\models`.
  You can skip and configure later — dictation will then prompt for the
  download on first use.
- Whisperheim drops to the **system tray**. Right-click the tray icon for the
  menu and settings.
- The Silero VAD and Pyannote segmentation models ship bundled inside the
  installer; you don't have to download those.

### Default hotkeys

| Hotkey | What it does |
|---|---|
| `Ctrl + Win` | Hold to dictate into the focused text field |
| `Ctrl + Win + R` | Toggle call recording (system audio + microphone, with speaker separation) |
| `Ctrl + Win + Alt` | Trigger a template by speaking its name |

All hotkeys are remappable from the General settings page.

## Optional: install FFmpeg

For YouTube and audio-stream transcription (and as a fallback OGG/Opus
decoder), Whisperheim needs FFmpeg on `PATH`. Whisperheim does **not** bundle
FFmpeg — the user picks the build and license. The easiest path:

```pwsh
winget install -e --id Gyan.FFmpeg
```

The first time you invoke a feature that needs FFmpeg, Whisperheim shows a
modal with the same command and an "Open download page" link. Drag-and-drop
transcription of common WAV/MP3 files keeps working without FFmpeg.

## Where is my data?

By default, everything user-generated lives under:

```
%APPDATA%\WhisperHeim\
```

- `settings.json`, `bootstrap.json` — your settings
- `recordings\` — raw WAV captures
- `transcripts\` — transcribed text
- `models\` — the downloaded Parakeet model

You can move the data root to any folder you like (e.g. a synced
OneDrive/Dropbox/Google Drive folder) from the **General** settings page.

**Uninstall does NOT delete this data.** If you want a fully clean removal,
delete `%APPDATA%\WhisperHeim\` and any custom data folder yourself after
uninstalling. Whisperheim drops a `WhisperHeim-data-location.txt` on your
desktop on uninstall to remind you where things live.

## System requirements

- Windows 11 (24H2 or later recommended), x64
- ~2 GB RAM idle, ~3 GB during transcription
- ~700 MB free disk for the Parakeet model
- A working microphone (and a loopback-capable audio device for call
  transcription — any standard Windows 11 setup qualifies)

## Privacy

- All transcription runs **locally** on your machine. No audio, transcripts, or
  metadata leave the device at runtime.
- **No telemetry.** Whisperheim does not call home — no analytics, no
  crash reporting, no usage pings.
- **No accounts.** No login, no cloud, no API keys required for the core
  features.
- The only outbound traffic on a fresh install is the one-time Parakeet model
  download from the Hugging Face CDN. Auto-updates check GitHub Releases.
- Optional integrations you opt into (Ollama for AI analysis, a synced data
  folder via your existing cloud client) are clearly labelled as such.

## Signing and verification

Whisperheim is **not yet code-signed**. Microsoft Trusted Signing isn't
available to individual developers in Germany at the time of writing; signing
is planned post-UG (Unternehmergesellschaft) registration. Until then:

- Every Release publishes the SHA-256 of `Setup.exe` in the Release notes.
- The full source is in this repository — `git log` is your audit trail.
- See [docs/why-unsigned.md](docs/why-unsigned.md) for the full story on
  SmartScreen, Smart App Control, and what the signing roadmap looks like.

## Build from source

```pwsh
git clone https://github.com/heimeshoff/WhisperHeim.git
cd WhisperHeim
dotnet build src/WhisperHeim/WhisperHeim.csproj
dotnet run --project src/WhisperHeim/WhisperHeim.csproj
```

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

For the release pipeline (Velopack pack, GitHub Actions, SHA-256 surfacing),
see [`docs/release.md`](docs/release.md).

## Support

If Whisperheim is useful to you and you want to chip in, there's a tip jar:

[**Ko-fi — heimeshoff**](https://ko-fi.com/heimeshoff)

## License

**TBD** — the licensing decision is pending the monetization research, see the
[roadmap](.workflow/roadmap.md). The repository currently carries an
[Unlicense](LICENSE) declaration; this may change before the first public
1.0 release.
