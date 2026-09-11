# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-09-11 11:21 -- Task verified and completed: main-rc541 - Overlay shows a brief "Nothing recognized" state when a real dictation decodes to nothing, instead of silently hiding — so the user knows to speak again rather than hunting for text that never arrived

**Type:** Work / Task completion
**Task:** main-rc541 - Overlay shows a brief "Nothing recognized" state when a real dictation decodes to nothing, instead of silently hiding — so the user knows to speak again rather than hunting for text that never arrived
**Summary:** Overlay shows a grey "Nothing recognized" state for ~1.5 s when a real dictation decodes to nothing (raw or clean-pipeline-reduced), via a NothingRecognized event derived from the widened EmptyResult hook; sequencing lives in the unit-tested NothingRecognizedOverlayCoordinator, Error keeps precedence
**Duration:** 16m
**Verification:** PASS (iteration 1)
**Files changed:** 13
**Tests added:** 11
**ADRs written:** 0011-empty-dictation-result-event-hook.md (amended in place)

---

## 2026-09-11 11:10 -- Batch started: [main-hh6zw]

**Type:** Work / Batch start
**Tasks:** main-hh6zw - Peak-normalize audio before decode in TranscriptionService so quiet recordings can no longer collapse to an empty transcript (defense in depth against the sherpa-onnx NemoNormalizePerFeature bug)
**Parallel:** yes (1 new worker joining main-rc541 still in flight — 2 of 3 slots; main-hh6zw was held from the first wave until infrastructure-anvty landed the shared quiet-audio regression test, now on main)

---

## 2026-09-11 11:10 -- Task verified and completed: infrastructure-anvty - Upgrade sherpa-onnx to 1.13.8 (carries the NemoNormalizePerFeature fix that silently empties quiet dictations), drop the unused Microsoft.ML.OnnxRuntime package, and pin both native packages to exact versions instead of `1.*`

**Type:** Work / Task completion
**Task:** infrastructure-anvty - Upgrade sherpa-onnx to 1.13.8 (carries the NemoNormalizePerFeature fix that silently empties quiet dictations), drop the unused Microsoft.ML.OnnxRuntime package, and pin both native packages to exact versions instead of `1.*`
**Summary:** Pin org.k2fsa.sherpa.onnx to exact 1.13.8 (NemoNormalizePerFeature fix for silently-empty quiet dictations), drop the unused floating Microsoft.ML.OnnxRuntime reference, add a real-model quiet-audio regression test (red on 1.13.4, green on 1.13.8)
**Duration:** 20m
**Verification:** PASS (iteration 1)
**Files changed:** 7
**Tests added:** 5
**ADRs written:** 0012-pin-sherpa-onnx-exact-drop-unused-onnxruntime-package.md

---

## 2026-09-11 11:04 -- Batch started: [main-rc541]

**Type:** Work / Batch start
**Tasks:** main-rc541 - Overlay shows a brief "Nothing recognized" state when a real dictation decodes to nothing, instead of silently hiding — so the user knows to speak again rather than hunting for text that never arrived
**Parallel:** yes (1 new worker joining infrastructure-anvty still in flight — 2 of 3 slots; main-rc541 was held from the first wave until main-ma9j8 landed its EmptyResult hook, now on main; main-hh6zw stays held until infrastructure-anvty lands the shared quiet-audio regression test)

---

## 2026-09-11 11:04 -- Task verified and completed: main-ma9j8 - Make an empty dictation result observable — warn with duration/RMS/peak and dump the raw samples as a WAV into a local diagnostics folder (capped ring), so the next lost dictation can be reproduced offline

**Type:** Work / Task completion
**Task:** main-ma9j8 - Make an empty dictation result observable — warn with duration/RMS/peak and dump the raw samples as a WAV into a local diagnostics folder (capped ring), so the next lost dictation can be reproduced offline
**Summary:** DictationOrchestrator raises a single EmptyResult event (samples/duration/RMS/peak/decode-ms/residency) on empty transcripts; self-subscribed diagnostics log Warning >=3s with a capped-ring WAV dump via EmptyDictationDumpService (Information <3s, no dump), opt-out WHISPERHEIM_DISABLE_DIAG_DUMP=1; Final: line gains RMS/peak
**Duration:** 14m
**Verification:** PASS (iteration 1)
**Files changed:** 8
**Tests added:** 10
**ADRs written:** 0011-empty-dictation-result-event-hook.md

---

## 2026-09-11 10:49 -- Batch started: [infrastructure-anvty, main-ma9j8]

**Type:** Work / Batch start
**Tasks:** infrastructure-anvty - Upgrade sherpa-onnx to 1.13.8 (carries the NemoNormalizePerFeature fix that silently empties quiet dictations), drop the unused Microsoft.ML.OnnxRuntime package, and pin both native packages to exact versions instead of `1.*`, main-ma9j8 - Make an empty dictation result observable — warn with duration/RMS/peak and dump the raw samples as a WAV into a local diagnostics folder (capped ring), so the next lost dictation can be reproduced offline
**Parallel:** yes (2 workers — 4 ready; main-hh6zw held to next wave: shares the quiet-audio regression test with infrastructure-anvty, whose task text says whichever lands first creates it; main-rc541 held to next wave: shares the empty-result hook in TranscribeFinalAsync with main-ma9j8, whose task text says whichever lands first introduces it and the other subscribes)

---

## 2026-09-11 10:40 -- Modeling / Captured: main-rc541 - Overlay shows a brief "Nothing recognized" state when a real dictation decodes to nothing

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** New `NothingRecognized` orchestrator event for recordings above `MinSamples` whose transcript (raw or cleaned) is empty; the pill re-shows in a grey "Nothing recognized" state for ~1.5 s (Error takes precedence) instead of fading as if text were coming. Shares the empty-result hook with main-ma9j8.

---

## 2026-09-11 10:40 -- Modeling / Captured: main-ma9j8 - Make an empty dictation result observable (warning with levels + capped WAV dump)

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** On an empty transcript for ≥3 s of audio the orchestrator logs a Warning with duration/RMS/peak/decode-ms/residency state and dumps the raw samples as a 16 kHz WAV into `%LOCALAPPDATA%\WhisperHeim\diagnostics\` (ring of 10, off via `WHISPERHEIM_DISABLE_DIAG_DUMP=1`, never the synced data path per main-104), so the next lost dictation is reproducible offline.

---

## 2026-09-11 10:40 -- Modeling / Captured: main-hh6zw - Peak-normalize audio before decode in TranscriptionService

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** Pure `PeakNormalize` (never attenuates, never amplifies below a 1e-4 silence floor, target peak 0.5) applied in `TranscriptionService.DecodeAudio` before `AcceptWaveform`, protecting every consumer of the shared engine (ADR-0006). Offline sweep: 6/12 gain steps decoded to "" raw on sherpa 1.13.3/1.13.4, 0/12 with peak normalization on every version. Independent of infrastructure-anvty; both should ship.

---

## 2026-09-11 10:40 -- Modeling / Captured: infrastructure-anvty - Upgrade sherpa-onnx to 1.13.8, drop unused Microsoft.ML.OnnxRuntime, pin exact versions

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** todo
**Summary:** Root cause of silently lost dictations (38 lost ≥10 s dictations in the log; empty rate 1.2 % at 10–20 s rising to 4.1 % at ≥30 s): sherpa-onnx ≤1.13.4 NeMo per-feature normalization cancels in float32 on floor-pinned mel bins and the INT8 encoder collapses → decoder emits blank for the whole utterance (upstream PR #3857, fixed in 1.13.5). Reproduced offline: 6/12 quiet-gain steps empty on 1.13.3 and 1.13.4 regardless of ORT version, 0/12 on 1.13.8. Task pins sherpa-onnx 1.13.8 exactly, removes the unused `Microsoft.ML.OnnxRuntime` reference whose 1.27 dll would crash sherpa 1.13.8 (API 28 required), and adds a quiet-audio regression test.

---

## 2026-08-12 19:28 -- Work session ended

**Type:** Work / Session end
**Duration:** 24m
**Completed:** 1 (first-try PASS: 0, re-dispatched: 1, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Dispatches:** infrastructure-n3p8w: 2
**Commits:** 2 (batch start, task integration)
**Vision-conformance:** none — batch aligns with vision (the keep-model-loaded toggle is default-off, preserving the "under 2 GB RAM while idle" success criterion for existing users; opting in trades RAM within that same stated budget for the "always available" / sub-2 s dictation-latency criteria; touches no cloud, web-UI, cross-platform, voice-command, per-app-capture, or live-subtitle non-goal)
**Carry-over:** none — working tree clean

---

## 2026-08-12 19:26 -- Task verified and completed: infrastructure-n3p8w - Keep the transcription model loaded — user-toggleable idle-unload (tray + settings)

**Type:** Work / Task completion
**Task:** infrastructure-n3p8w - Keep the transcription model loaded — user-toggleable idle-unload (tray + settings)
**Summary:** Machine-local keep-model-loaded toggle (tray + GeneralPage, symmetric immediate load/unload, default off) gating the idle-unload via injected predicate on ModelLifecycleManager
**Duration:** 23m
**Verification:** PASS (iteration 2)
**Files changed:** 10
**Tests added:** 5
**ADRs written:** 0010-keep-model-loaded-user-toggle.md

---

## 2026-08-12 19:23 -- Verification failed: infrastructure-n3p8w - Keep the transcription model loaded — user-toggleable idle-unload (tray + settings)

**Type:** Work / Verification failure
**Task:** infrastructure-n3p8w - Keep the transcription model loaded — user-toggleable idle-unload (tray + settings)
**Iteration:** 1 of 3
**Reasons:** ADR-0010 written without the standard YAML frontmatter (scope/status/date folded into the H1 as prose — breaks the convention consistent across ADRs 0001–0009 and makes the ADR non-machine-discoverable), missing related_tasks/supersedes/superseded_by fields. Code, tests (265 green) and scope all audited clean.
**Iteration hint:** likely-fixable
**Next:** re-dispatched worker

---

## 2026-08-12 19:04 -- Batch started: [infrastructure-n3p8w]

**Type:** Work / Batch start
**Tasks:** infrastructure-n3p8w - Keep the transcription model loaded — user-toggleable idle-unload (tray + settings)
**Parallel:** no (1 worker) - only ready task, no dependencies

---

## 2026-08-12 -- Modeling / Captured: infrastructure-n3p8w - Keep the transcription model loaded — user-toggleable idle-unload (tray + settings)

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** todo
**Summary:** A machine-local boolean (default off = today's 5-min idle-unload) that, when on, keeps the Parakeet recognizer resident for the process lifetime — surfaced as a toggling-label tray item directly below "Start Call Recording" and as a ToggleSwitch card on GeneralPage. Symmetric on toggle (on → load now, off → unload now, deferred if a dictation is in flight); warmed from the post-startup housekeeping hook when already on at launch, so ADR-0006's lazy-on-in-StartupCore rule survives for the default path. Deliberately narrower than the dismissed `infrastructure-b3n6p` (no configurable idle timeout) and wider in one respect (b3n6p had no tray affordance). Captured straight to todo — every implementation hook is named against the tree and all design forks are resolved.

---

## 2026-07-15 12:42 -- Work session ended

**Type:** Work / Session end
**Duration:** 12m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Dispatches:** main-h7q3z: 1
**Commits:** 2 (batch start, task integration)
**Vision-conformance:** none — batch aligns with vision (a research-only competitive teardown; touches no cloud, web-UI, cross-platform, voice-command, per-app-capture, or real-time-subtitle surface, and actively reinforces the no-voice-assistant non-goal by flagging Fluid's Command Mode as a poor fit)
**Carry-over:** none — working tree clean

---

## 2026-07-15 12:41 -- Task verified and completed: main-h7q3z - Compare WhisperHeim to Handy and Altic Fluid

**Type:** Work / Task completion
**Task:** main-h7q3z - Compare WhisperHeim to Handy and Altic Fluid
**Summary:** Cited competitive teardown of WhisperHeim vs Handy vs Fluid across four axes with an enumerated 11-item WhisperHeim gaps section
**Duration:** 7m
**Verification:** PASS via research-review gate (iteration 1)
**Files changed:** 2
**Tests added:** 0
**ADRs written:** none

---

## 2026-07-15 12:30 -- Batch started: [main-h7q3z]

**Type:** Work / Batch start
**Tasks:** main-h7q3z - Compare WhisperHeim to Handy and Altic Fluid
**Parallel:** no (1 worker) - only ready task, no dependencies

---

## 2026-07-15 12:28 -- Modeling / Promoted: main-h7q3z - Compare WhisperHeim to Handy and Altic Fluid

**Type:** Modeling / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-07-15 12:27 -- Modeling / Refined: main-h7q3z - Compare WhisperHeim to Handy and Altic Fluid

**Type:** Modeling / Refine
**BC:** main
**Status after:** todo
**Summary:** Pinned the blank purpose (gap-finding, not positioning), the four comparison axes (latency/model, features/workflows, UX/polish, cost/privacy/platform), and the deliverable (research report only — the report enumerates WhisperHeim gaps but does not auto-spawn captures; builder triages afterward). Wrote concrete acceptance criteria (per-axis × per-tool matrix, cited primary sources, dedicated "WhisperHeim gaps" section, research-review gate). Cited the two existing STT-model reports as the WhisperHeim-side baseline in Notes (below auto-link threshold). Auto-promoted to todo.
**Split into:** none
**ADRs written:** none

---

## 2026-07-15 12:21 -- Work session ended

**Type:** Work / Session end
**Duration:** 6m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Dispatches:** main-d8m3p: 1
**Commits:** 2 (batch start, task integration)
**Vision-conformance:** none — batch aligns with vision (deleting the unused HighQualityRecorderService sheds dead code and touches no cloud, web-UI, cross-platform, voice-command, or per-app-capture surface; pulls mildly toward the "always available / low footprint" quality bar)
**Carry-over:** none — working tree clean

---

## 2026-07-15 12:20 -- Task verified and completed: main-d8m3p - Delete unused HighQualityRecorderService / IHighQualityRecorderService

**Type:** Work / Task completion
**Task:** main-d8m3p - Delete unused HighQualityRecorderService / IHighQualityRecorderService
**Summary:** Deleted the confirmed-dead HighQualityRecorderService / IHighQualityRecorderService (44.1kHz voice-message recorder, never invoked) and its DI wiring in App.xaml.cs and MainWindow.xaml.cs.
**Duration:** 6m
**Verification:** PASS (iteration 1)
**Files changed:** 4
**Tests added:** 0
**ADRs written:** none

---

## 2026-07-15 12:15 -- Batch started: [main-d8m3p]

**Type:** Work / Batch start
**Tasks:** main-d8m3p - Delete unused HighQualityRecorderService / IHighQualityRecorderService
**Parallel:** no (1 worker) - only ready task, no dependencies

---

## 2026-07-15 12:09 -- Modeling / Promoted: main-d8m3p - Delete unused HighQualityRecorderService / IHighQualityRecorderService

**Type:** Modeling / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-07-15 12:15 -- Modeling / Refined: main-d8m3p - Delete unused HighQualityRecorderService / IHighQualityRecorderService

**Type:** Modeling / Refine
**BC:** main
**Status after:** todo
**Summary:** Verified the dead-code premise against current source — `HighQualityRecorderService` is constructed in `App.xaml.cs` and injected into `MainWindow` but never invoked (no `StartRecording`/`StopRecording`/`SaveRecording` caller anywhere). Wrote a precise deletion map (2 files + DI wiring in `App.xaml.cs` and `MainWindow.xaml.cs`), confirmed `RecordingStoppedEventArgs` dies cleanly with the interface, and fenced off the genuinely-live `HighQualityLoopbackService` sibling and `CallRecordingStoppedEventArgs`. Concrete acceptance criteria + an ADR-0009 fallback branch if a caller has reappeared. Auto-promoted to todo.
**Split into:** none
**ADRs written:** none

---

## 2026-07-15 12:03 -- Work session ended

**Type:** Work / Session end
**Duration:** 12m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Dispatches:** main-c3x7q: 1
**Commits:** 4 (batch start, task integration, stranded-vision reconciliation, this session-end line)
**Vision-conformance:** none — batch aligns with vision (making recording honor the saved microphone reuses the single Dictation.AudioDevice setting for consistency; pulls toward the "Dictation accuracy" / consistent-device success criteria; no cloud, web-UI, cross-platform, voice-command, or per-app-capture surface touched)
**Carry-over:** .agentheim/vision.md: committed (stranded cosmetic vision-title edit `# WhisperHeim -- Vision` → `# Vision: WhisperHeim`, left behind across prior sessions; reconciled this session per user disposition)

---

## 2026-07-15 12:02 -- Task verified and completed: main-c3x7q - Call/voice-message recording ignores the saved microphone (always records from system default)

**Type:** Work / Task completion
**Task:** main-c3x7q - Call/voice-message recording ignores the saved microphone (always records from system default)
**Summary:** Recording (TranscriptsPage record button and call-recording hotkey) now resolves the saved Dictation.AudioDevice microphone via a new CallRecordingService.ResolveMicDeviceIndex seam before mic capture, instead of always opening the system default; the redundant micDeviceIndex parameter was removed from StartRecording/ToggleRecording since resolution is centralized in the service.
**Duration:** 10m
**Verification:** PASS (iteration 1)
**Files changed:** 6
**Tests added:** 4
**ADRs written:** none

---

## 2026-07-15 11:51 -- Batch started: [main-c3x7q]

**Type:** Work / Batch start
**Tasks:** main-c3x7q - Call/voice-message recording ignores the saved microphone (always records from system default)
**Parallel:** no (1 worker) - only ready task, no dependencies

---

## 2026-07-15 11:19 -- Modeling / Promoted: main-c3x7q - Call/voice-message recording ignores the saved microphone (always records from system default)

**Type:** Modeling / Promote
**BC:** main
**From → To:** backlog → todo

---

## 2026-07-15 11:18 -- Modeling / Refined: main-c3x7q - Call/voice-message recording ignores the saved microphone

**Type:** Modeling / Refine
**BC:** main
**Status after:** todo
**Summary:** Investigated the original "HighQualityRecorderService clamps -1 to device 0" premise and found it doesn't manifest — that clamp is dead code (the service is injected into MainWindow but its StartRecording is never called), and the live recording path (CallRecordingService → AudioCaptureService) already honors WAVE_MAPPER via main-v7k2d. Surfaced the real adjacent gap: recording never resolves the saved Dictation.AudioDevice, so it always records from the system default mic. Re-scoped the task (renamed slug) to fix that, with concrete acceptance criteria mirroring the dictation-side fix, and auto-promoted to todo.
**Split into:** none
**ADRs written:** none

---

## 2026-07-15 10:26 -- Work session ended

**Type:** Work / Session end
**Duration:** 19m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Dispatches:** main-v7k2d: 1 (resumed from a prior interrupted session; verified PASS on iteration 1)
**Commits:** 2 (task integration, this session-end line)
**Vision-conformance:** none — batch aligns with vision (fixing dictation to honor the selected microphone pulls toward the "Dictation accuracy" and "Always available" success criteria; no cloud, web-UI, cross-platform, or voice-command surface touched)
**Carry-over:** .agentheim/vision.md: left behind (owner: user / pre-existing, a vision-title cosmetic edit present before this session, not this session's work)

---

## 2026-07-15 10:24 -- Task verified and completed: main-v7k2d - Hotkey dictation ignores the selected microphone (always captures WaveIn device 0)

**Type:** Work / Task completion
**Task:** main-v7k2d - Hotkey dictation ignores the selected microphone (always captures WaveIn device 0)
**Summary:** Hotkey dictation resolves the saved microphone name to a WaveIn device index on every press (via AudioDeviceResolver) instead of always opening device 0; also removed an incorrect -1 to 0 clamp so the system-default fallback reaches NAudio WAVE_MAPPER
**Duration:** 8m
**Verification:** PASS (iteration 1)
**Files changed:** 6
**Tests added:** 4
**ADRs written:** 0009-honor-system-default-capture-device.md

---

## 2026-07-15 10:07 -- Batch started: [main-v7k2d]

**Type:** Work / Batch start
**Tasks:** main-v7k2d - Hotkey dictation ignores the selected microphone (always captures WaveIn device 0)
**Parallel:** no (1 worker) - only ready task, no dependencies

---

## 2026-07-15 -- Modeling / Captured: main-v7k2d - Hotkey dictation ignores the selected microphone

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** Diagnosed from whisperheim.log — the hotkey path (`DictationOrchestrator.OnHotkeyPressed`) calls `StartCapture()` with no device index, so it always opens WaveIn device 0 and ignores the saved microphone. Fix: resolve the saved device name and pass the index through.

---

## 2026-07-10 -- Capture / Captured: main-h7q3z - Compare WhisperHeim to Handy and Altic Fluid

**Type:** Capture
**BC:** main
**Filed to:** backlog
**Summary:** Compare WhisperHeim against the Handy and Fluid (https://altic.dev/fluid) dictation tools.

---

## 2026-07-09 16:42 -- Work session ended

**Type:** Work / Session end
**Duration:** 13m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Dispatches:** main-k4t8p: 1
**Commits:** 3 (batch start, task integration, this session-end line)
**Vision-conformance:** none — batch aligns with vision (native WPF import-time prompt on the local file path; no cloud dependency, no web UI, no cross-platform surface, no voice-command scope; leaves dictation latency and memory-footprint criteria untouched)
**Carry-over:** none — working tree clean

---

## 2026-07-09 16:41 -- Task verified and completed: main-k4t8p - Speaker name for imported voice messages

**Type:** Work / Task completion
**Task:** main-k4t8p - Speaker name for imported voice messages
**Summary:** Importing an audio file now prompts once per file (in selection order) for the speaker name, threading it into the transcript segment and RemoteSpeakerNames so the SPEAKER NAMES panel and Markdown export attribute it; empty or dismissed falls back to the literal "Speaker" label
**Duration:** 11m20s
**Verification:** PASS (iteration 1)
**Files changed:** 6
**Tests added:** 7
**ADRs written:** none

---

## 2026-07-09 16:29 -- Batch started: [main-k4t8p]

**Type:** Work / Batch start
**Tasks:** main-k4t8p - Speaker name for imported voice messages
**Parallel:** no (1 worker) - only ready task, no dependencies

---

## 2026-07-09 -- Modeling / Captured: main-k4t8p - Speaker name for imported voice messages

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** Imported voice messages (e.g. WhatsApp) always transcribe as the literal label `"Speaker"` — `SaveFileImportTranscript` hardcodes it and leaves `RemoteSpeakerNames` empty, so the existing SPEAKER NAMES panel shows no rows and a typed name renames nothing; the Markdown auto-export (main-m6x4v) inherits `### Speaker`. Capture: prompt for the speaker name at import time (one prompt per selected file, empty field, skippable → falls back to `"Speaker"`), thread it into the segment label and `RemoteSpeakerNames`. STT API / CLI path untouched. Prior art: main-037, main-073 (both recording-only). Captured straight to todo — both open decisions resolved with the builder.
**ADRs written:** none

---

## 2026-07-09 15:33 -- Work session ended

**Type:** Work / Session end
**Duration:** 21m
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Dispatches:** main-m6x4v: 1
**Commits:** 3 (batch start, task integration, this session-end line)
**Vision-conformance:** none — batch aligns with vision (local-only Markdown export to user-configured folders; no cloud dependency, no web UI, no cross-platform surface, no voice-command scope; leaves dictation latency and memory-footprint criteria untouched)
**Carry-over:** none — working tree clean

---

## 2026-07-09 15:32 -- Task verified and completed: main-m6x4v - Auto-export transcripts as Markdown to configured default folders

**Type:** Work / Task completion
**Task:** main-m6x4v - Auto-export transcripts as Markdown to configured default folders
**Summary:** Recorded conversations and imported voice messages auto-export as <name>.md into two independently-configurable machine-local Settings folders on transcription completion; re-transcription overwrites in place, same-titled sibling sessions disambiguate to name (2).md per ADR-0008.
**Duration:** 19m53s
**Verification:** PASS (iteration 1)
**Files changed:** 13
**Tests added:** 40
**ADRs written:** none

---

## 2026-07-09 15:12 -- Batch started: [main-m6x4v]

**Type:** Work / Batch start
**Tasks:** main-m6x4v - Auto-export transcripts as Markdown to configured default folders
**Parallel:** no (1 worker) - only ready task, no dependencies

---

## 2026-07-09 -- Modeling / Captured: main-m6x4v - Auto-export transcripts as Markdown to configured default folders

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** Two optional machine-local settings (recorded-conversations folder, imported-voice-messages folder); on transcription completion, write `<recording name>.md` into the matching folder. Routed via `QueueItemType.Recording` vs `File` at the `ItemCompleted` seam, so no domain-model provenance flag is needed. Streams and the STT API stay out of scope. Captured straight to todo — decisions resolved with the builder, ADR-0008 written for the overwrite-identity rule.
**ADRs written:** 0008 (auto-export identity is the Recording-session directory, not `CallTranscript.Id`, which is regenerated per transcription)

---

