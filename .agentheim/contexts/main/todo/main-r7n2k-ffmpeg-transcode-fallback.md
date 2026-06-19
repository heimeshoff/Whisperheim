---
id: main-r7n2k
title: Transcode any unsupported audio format via FFmpeg fallback (e.g. .opus)
status: todo
type: feature
context: main
created: 2026-06-19
completed:
depends_on: []
blocks: []
tags: [file-transcription, ffmpeg, decoding, http-api, claude-code-plugin]
related_adrs: [0001]
related_research: [whisperheim-stt-api-exposure-2026-06-19]
prior_art: [main-110, main-020, main-021, main-h7k2p, main-q4m8t]
---

## Why
WhisperHeim hard-rejects any audio extension outside {.wav, .mp3, .m4a, .ogg}.
A `.opus` file (and anything else FFmpeg can read) is refused even when FFmpeg
is installed and could trivially convert it. The native engine already shells
out to FFmpeg for OGG decode (`DecodeOggWithFfmpeg`); we should generalize that
so that "not natively supported" means "try to convert with FFmpeg" instead of
"reject". This must hold for every consumer — drag-drop UI, the file picker,
`POST /transcribe`, the `whisperheim-transcribe` CLI, and the Claude Code
plugin — because they all funnel through the same decode pipeline.

## What
Turn the FFmpeg decode path into a general last-resort fallback for any
extension the native readers don't handle, and remove the premature allowlist
rejections that stop such files from ever reaching it. The decoder stays
non-blocking/headless-safe (no modal from the decode path — main-110 contract).

Specifically:
1. **Decoder fallback (`AudioFileDecoder.Decode`)** — replace the
   `_ => throw NotSupportedException(...)` arm so unknown extensions attempt an
   FFmpeg transcode to 16kHz mono s16le PCM. Generalize the existing
   `DecodeOggWithFfmpeg` (rename/refactor to e.g. `DecodeWithFfmpeg`) so OGG and
   the new fallback share one routine. `.opus` routes through this fallback
   (Concentus need not be wired for `.opus`; ffmpeg is the feature).
2. **Distinguish "ffmpeg missing" from "decode failed"** — when an unknown
   extension needs FFmpeg and no FFmpeg is detected/usable, throw a *distinct*
   error whose message clearly says FFmpeg is required (and how to get it),
   separate from the corrupt/undecodable case.
3. **`FileTranscriptionService` allowlist** — the `SupportedExts` /
   `IsSupported` / validation gate currently rejects non-allowlisted extensions
   before decode. Make `IsSupported` permissive (accept any file with an
   extension; let the decoder be the authority) and remove the early
   `NotSupportedException`. Keep `SupportedExtensions` only as a *display hint*
   for the file-picker filter.
4. **HTTP handler (`TranscribeRequestHandler`)** — `ResolveExtension` must
   honour an `.opus` (or any) hint from `?filename=` / `X-Filename` (it already
   does; verify end-to-end). `ClassifyError` must map the new "ffmpeg
   missing/required" message to an appropriate status (recommend **501 Not
   Implemented** or **415** with a body that names FFmpeg — pick one and pin it
   in code + plugin docs; do NOT let it fall through to a generic 500).
5. **Error message hygiene** — the "Supported formats: .wav, .mp3, .m4a, .ogg"
   text is now misleading. Replace genuine-failure messages with: (a) ffmpeg
   missing → "Converting '<name>' requires FFmpeg, which isn't installed. …",
   (b) ffmpeg present but file undecodable → "Could not decode '<name>': the
   file appears corrupt or is not audio."

## Acceptance criteria
- [ ] `AudioFileDecoder.Decode` no longer throws `NotSupportedException` for an
      arbitrary unknown extension when FFmpeg is available; it attempts an
      FFmpeg transcode to 16kHz mono PCM and returns samples on success.
- [ ] OGG decode and the general fallback share a single FFmpeg routine (no
      duplicated `ffmpeg -i … -f s16le` blocks); existing OGG behaviour
      (ffmpeg-first, Concentus fallback, 60s kill, cancellation) is preserved.
- [ ] A `.opus` file dropped on / browsed into the **UI** is accepted (not
      silently filtered at `TranscriptsPage` `IsSupported` checks) and
      transcribes end-to-end when FFmpeg is installed.
- [ ] A `.opus` file sent to **`POST /transcribe`** (with `?filename=x.opus` or
      `X-Filename: x.opus`) transcribes end-to-end and returns 200 with text;
      same verified through the `whisperheim-transcribe` CLI / Claude Code
      plugin path.
- [ ] `FileTranscriptionService` no longer throws the fixed-allowlist
      `NotSupportedException` for `.opus` (or other ffmpeg-decodable formats)
      before decode is attempted.
- [ ] **FFmpeg-missing case:** an `.opus` request with no FFmpeg available
      produces a clear, FFmpeg-naming error (no modal on the decode path); over
      HTTP this maps to the chosen non-500 status (415 or 501) with a body that
      names FFmpeg, and `ClassifyError` is updated and unit-tested for it.
- [ ] Corrupt/undecodable input yields the "appears corrupt or not audio"
      message, distinct from the ffmpeg-missing message.
- [ ] No user-facing error or log still claims the supported set is exactly
      "{.wav, .mp3, .m4a, .ogg}".
- [ ] UI drag path may still surface the existing FFmpeg install prompt at a
      higher layer, but `AudioFileDecoder`/`FileTranscriptionService` do NOT
      call `IFfmpegPromptService` or block on a modal (main-110 contract holds).
- [ ] Unit tests cover: unknown-extension→ffmpeg-fallback success, ffmpeg-
      missing→distinct error + correct HTTP status, corrupt→distinct error.

## Notes
- **Design rationale (open fallback vs. extended allowlist):** chose
  "try ffmpeg on any non-native extension" over adding `.opus`/etc to a fixed
  list, matching the user's stated intent ("if someone sends a .opus file, it
  should not be rejected"). Trade-off: `SupportedExtensions` stops being an
  exhaustive truth source, so it is demoted to a file-picker display hint and
  `IsSupported` becomes permissive. The picker already exposes "All files
  (*.*)", so usability is preserved.
- **Three gates, not one** (verified 2026-06-19):
  - `FileTranscriptionService.cs` (`SupportedExts` ~16-19, validation ~33-58) —
    primary allowlist, hits UI path first; also feeds the drag-drop/browse
    filter via `SupportedExtensions` (`TranscriptsPage.xaml.cs` ~458, 629, 642,
    960, 992, 1855, 2393). A `.opus` drop is silently ignored here today.
  - `AudioFileDecoder.cs:40-41` — deepest gate, the `_ =>` throw.
  - `TranscribeRequestHandler.cs:99-128` — `ResolveExtension` (default `.ogg`,
    honours filename hint — OK) and `ClassifyError` (only "not supported"→415).
- **Reuse, don't reinvent:** generalize `DecodeOggWithFfmpeg`
  (`AudioFileDecoder.cs:121-180`) — it already does exactly the right
  conversion with kill-timeout + cancellation. Detector path comes from the
  injected `FfmpegDetector` (`SetDetector`, App startup); fall back to PATH
  `"ffmpeg"` when no detector, same as the OGG path does today.
- **Non-blocking contract (main-110):** the decode path must never call
  `IFfmpegPromptService`. The install modal stays a higher-layer UI concern.
- **HTTP status decision is open within the task** — worker should pick 415 vs
  501 and update plugin/CLI docs accordingly; either is acceptable so long as
  it's not a generic 500 and the body names FFmpeg.
- Relevant files: `src\WhisperHeim\Services\FileTranscription\AudioFileDecoder.cs`,
  `src\WhisperHeim\Services\FileTranscription\FileTranscriptionService.cs`,
  `src\WhisperHeim\Services\Http\TranscribeRequestHandler.cs`,
  `src\WhisperHeim\Views\Pages\TranscriptsPage.xaml.cs`,
  `src\WhisperHeim\Services\Ffmpeg\FfmpegDetector.cs`.
</content>
</invoke>
