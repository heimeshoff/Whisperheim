# main -- Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 0
- **Todo:** 2
- **Doing:** 0
- **Done:** 131
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **main-hh6zw** — Peak-normalize audio before decode in TranscriptionService so quiet recordings can no longer collapse to an empty transcript (defense in depth against the sherpa-onnx NemoNormalizePerFeature bug) (bug) — `todo/main-hh6zw-peak-normalize-audio-before-decode.md`
- **main-rc541** — Overlay shows a brief "Nothing recognized" state when a real dictation decodes to nothing, instead of silently hiding — so the user knows to speak again rather than hunting for text that never arrived (feature) — `todo/main-rc541-overlay-nothing-recognized-state.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- no tasks in doing -->
<!-- doing-list:end -->

### Done (most recent 30; older entries archived verbatim under `done-archive/` — kept for prior-art search, ADR-0039 convention)
<!-- done-list:start -->
- **main-ma9j8** — Make an empty dictation result observable — warn with duration/RMS/peak and dump the raw samples as a WAV into a local diagnostics folder (capped ring), so the next lost dictation can be reproduced offline (feature) — `done/main-ma9j8-diagnostics-on-empty-dictation-result.md`
- **main-h7q3z** — Compare WhisperHeim to Handy and Altic Fluid (spike) — `done/main-h7q3z-compare-whisperheim-to-handy-and-altic-fluid.md`
- **main-d8m3p** — Delete unused HighQualityRecorderService / IHighQualityRecorderService (chore) — `done/main-d8m3p-delete-unused-high-quality-recorder-service.md`
- **main-c3x7q** — Call/voice-message recording ignores the saved microphone (always records from system default) (bug) — `done/main-c3x7q-recording-ignores-saved-microphone.md`
- **main-v7k2d** — Hotkey dictation ignores the selected microphone (always captures WaveIn device 0) (bug) — `done/main-v7k2d-dictation-ignores-selected-microphone.md`
- **main-k4t8p** — Speaker name for imported voice messages (feature) — `done/main-k4t8p-speaker-name-for-imported-voice-messages.md`
- **main-m6x4v** — Auto-export transcripts as Markdown to configured default folders (feature) — `done/main-m6x4v-auto-export-md-to-configured-folders.md`
- **main-r8m4q** -- About-page Parakeet link points to v2, app uses v3 -- 2026-06-29 -- `done/main-r8m4q-about-page-parakeet-v3-link.md`
- **main-t9w2k** -- Inline template creation dialog when no template matches -- 2026-06-29 -- `done/main-t9w2k-inline-template-creation-on-no-match.md`
- **main-p3k9d** -- First dictation overlay renders at wrong position (not bottom-center) -- 2026-06-28 -- `done/main-p3k9d-first-overlay-mispositioned.md`
- **main-t6r2k** -- Reduce ASR intra-op threads 4 → 2 -- 2026-06-28 -- `done/main-t6r2k-reduce-asr-threads.md`
- **main-r7n2k** -- Transcode any unsupported audio format via FFmpeg fallback (e.g. .opus) -- 2026-06-19 -- `done/main-r7n2k-ffmpeg-transcode-fallback.md`
- **main-q4m8t** -- whisperheim-transcribe CLI wrapper over POST /transcribe -- 2026-06-19 -- `done/main-q4m8t-whisperheim-transcribe-cli.md`
- **main-h7k2p** -- STT API — POST /transcribe HttpListener server + queue integration -- 2026-06-19 -- `done/main-h7k2p-transcribe-http-endpoint.md`
- **main-110** -- FFmpeg Detection + First-Use Install Prompt -- 2026-05-12 -- `done/main-110-ffmpeg-detection-and-install-prompt.md`
- **main-111** -- GitHub Actions Release Workflow (Tag-Triggered Velopack Build) -- 2026-05-12 -- `done/main-111-github-actions-release-workflow.md`
- **main-109** -- Bundle Silero VAD + Pyannote Seg in the Publish Output -- 2026-05-12 -- `done/main-109-bundle-small-models-in-publish.md`
- **main-107** -- Add Velopack to the Project (Custom Main + Bootstrap) -- 2026-05-12 -- `done/main-107-velopack-bootstrap.md`
- **main-108** -- First-Run Model Download Dialog -- 2026-05-12 -- `done/main-108-first-run-model-download-dialog.md`
- **main-115** -- Code Signing — Deferred Hook (Wire-Up Now, Flip Post-UG) -- 2026-05-12 -- `done/main-115-code-signing-deferred-hook.md`
- **main-116** -- Fix vpk Version Pin in Release Workflow (0.0.1589 unavailable) -- 2026-05-12 -- `done/main-116-fix-vpk-version-pin-in-release-workflow.md`
- **main-114** -- Velopack End-to-End Dry Run (Sanity Check Before 1.0.0) -- 2026-05-12 -- `done/main-114-velopack-pack-dry-run.md`
- **main-112** -- Public README + GitHub Release Page Content -- 2026-05-12 -- `done/main-112-readme-and-release-page-content.md`
- **main-113** -- Uninstall Data Preservation (Hygiene + Documentation) -- 2026-05-12 -- `done/main-113-uninstall-data-preservation.md`
- **main-104** -- Stage WAV Writes Outside the Synced Data Folder -- 2026-05-11 -- `done/main-104-stage-wav-writes-outside-data-folder.md`
- **main-105** -- Origin-Machine Owns Transcription (Multi-Machine Coordination) -- 2026-05-11 -- `done/main-105-origin-machine-owns-transcription.md`
- **main-106** -- No Window Frame Flash When Start-Minimized -- 2026-05-11 -- `done/main-106-no-frame-flash-when-start-minimized.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **ADR-0011** -- One shared `EmptyResult` orchestrator event is the single hook for every empty-dictation reaction (diagnostics dump now, overlay "Nothing recognized" next), instead of parallel ad-hoc branches in `TranscribeFinalAsync` -- `knowledge/decisions/0011-empty-dictation-result-event-hook.md`
- **ADR-0009** -- Honor NAudio WAVE_MAPPER (-1) as the real system default capture device -- `knowledge/decisions/0009-honor-system-default-capture-device.md`
- **ADR-0008** -- Auto-export identity is the Recording-session directory, not CallTranscript.Id -- `knowledge/decisions/0008-auto-export-identity-is-session-dir-not-transcript-id.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
- **STT API exposure** (2026-06-19) — transports, in-process hosting, security, and the resolved Utterheim house pattern for exposing STT to other apps. — `knowledge/research/whisperheim-stt-api-exposure-2026-06-19.md`
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- no concept pages yet -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
