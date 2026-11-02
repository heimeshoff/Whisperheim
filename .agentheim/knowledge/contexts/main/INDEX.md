## ADRs scoped to this BC

<!-- adr-local:start -->
- **ADR-0013** -- Peak-normalize samples to 0.5 inside `TranscriptionService.DecodeAudio` (never attenuate, never amplify below a 1e-4 silence floor) as defense in depth for every shared-engine consumer, chosen over dither or per-caller normalization -- `knowledge/decisions/0013-peak-normalize-audio-before-decode.md`
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
- Task board (tasks by status) for this BC: `../../../board/main/INDEX.md`
