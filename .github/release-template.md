<!--
Release notes template for Whisperheim.

The `Release` workflow (`.github/workflows/release.yml`) attaches Setup.exe,
the full + delta nupkgs, and the RELEASES manifest. It also surfaces the
SHA-256 of Setup.exe in the workflow job summary. After the workflow finishes,
edit the Release on GitHub and paste this template, filling in the four
placeholders. Velopack's `vpk upload --releaseNotes <file>` can also consume
this file directly during the workflow if/when we automate the body.

Placeholders to fill:
  {{VERSION}}     e.g. 0.1.0
  {{SETUP_NAME}}  e.g. WhisperHeim-0.1.0-win-Setup.exe
  {{SHA256}}      from the workflow job summary
  {{CHANGES}}     bullets describing what's new
-->

# Whisperheim {{VERSION}}

## What changed

{{CHANGES}}

## Install

1. Download **`{{SETUP_NAME}}`** from the Assets list below.
2. Run it. Windows SmartScreen will warn ("Windows protected your PC") — click
   **More info** → **Run anyway**.
3. Whisperheim drops to the system tray. Right-click for the menu.

Full install instructions (including the Smart App Control caveat) are in the
[README](https://github.com/heimeshoff/WhisperHeim#install). The SmartScreen
explanation lives in [`docs/why-unsigned.md`](https://github.com/heimeshoff/WhisperHeim/blob/main/docs/why-unsigned.md).

> First run downloads the ~640 MB Parakeet ASR model from Hugging Face. The
> Silero VAD and Pyannote segmentation models ship bundled inside Setup.exe.

## Verification

`Setup.exe` is **not yet code-signed** — signing is planned post-UG
registration, see [why-unsigned.md](https://github.com/heimeshoff/WhisperHeim/blob/main/docs/why-unsigned.md).
Verify the download with:

```pwsh
certutil -hashfile {{SETUP_NAME}} SHA256
```

Expected SHA-256:

```
{{SHA256}}
```

## Optional: install FFmpeg

For YouTube and stream transcription:

```pwsh
winget install -e --id Gyan.FFmpeg
```

Whisperheim will prompt you the first time a feature needs it. Drag-and-drop
WAV/MP3 transcription works without FFmpeg.

## Known issues

<!-- Fill in or delete this section. -->

## Notes

- Uninstall does **not** delete your data in `%APPDATA%\WhisperHeim\`. The
  app drops a `WhisperHeim-data-location.txt` on the desktop on uninstall to
  remind you.
- Auto-updates check this Release feed via Velopack. To opt out, decline the
  in-app update prompt or block `github.com` for the WhisperHeim process.
