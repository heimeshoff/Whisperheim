# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

---

## 2026-06-28 14:35 -- Task verified and completed: infrastructure-q4t8m - "Warming up" overlay state when an utterance outruns the model load

**Type:** Work / Task completion
**Task:** infrastructure-q4t8m - "Warming up" overlay state when an utterance outruns the model load
**Summary:** Added a pulsing-amber `OverlayMicState.WarmingUp` shown when a held utterance is released while the recognizer is not yet `Loaded` — transcribe-on-release awaits the in-flight load and the overlay stays alive (deferred hide) through the bounded ~4 s wait instead of fading to a frozen-looking blank. Plumbed via a new `WarmingUpChanged(bool)` orchestrator event mirroring `AudioAmplitudeChanged`/`PipelineError`; Error precedence preserved.
**Verification:** PASS (iteration 1) — build clean, full suite green (166 passed, 4 new release-time decision tests). Deferred-hide race traced closed (WarmingUpChanged(true) raised before NotifyStateChanged(false), same-priority FIFO dispatcher).
**Files changed:** 6
**Tests added:** 4
**ADRs written:** none
**Deferred:** AC6 (live `/deploy` visual confirmation — force ≥5 min idle unload, fire a short <~4 s utterance, observe pulsing amber then transcription) is deploy-gated; code-complete and unit-tested, awaits a user `/deploy`.

---

## 2026-06-28 14:25 -- Batch started: [infrastructure-q4t8m]

**Type:** Work / Batch start
**Tasks:** infrastructure-q4t8m - "Warming up" overlay state when an utterance outruns the model load
**Parallel:** no (1 worker) — only ready task; dependency infrastructure-d2v7n (lazy-load core lifecycle) is done. Touches the dictation overlay + orchestrator event plumbing in main/ (Views/, App.xaml.cs, Services/Orchestration/).

---

## 2026-06-28 14:15 -- Modeling / Dismissed: infrastructure-b3n6p

**Type:** Modeling / Dismiss
**Dismissed:** infrastructure-b3n6p - Make lazy-load / idle-unload configurable (lazy-vs-eager + idle timeout) (infrastructure)

---

## 2026-06-28 14:10 -- Modeling / Refined: infrastructure-q4t8m - "Warming up" overlay state when an utterance outruns the model load

**Type:** Modeling / Refine
**BC:** infrastructure
**Status after:** todo
**Summary:** Pinned every implementation hook against the now-shipped d2v7n core: signal is `ModelLifecycleManager.State` (`ModelResidencyState.Loading` vs `Loaded`) checked at the `EnsureLoadedAsync` await in `DictationOrchestrator.TranscribeFinalAsync`; render a new `OverlayMicState.WarmingUp` in `DictationOverlayWindow.SetMicState`/`OnBarAnimationTick`; plumb via a new orchestrator event mirroring `AudioAmplitudeChanged`/`PipelineError`. Surfaced the key wrinkle: the overlay is told to `HideOverlay()` at release *before* the load-wait runs, so warming-up must defer that hide (else `SetMicState` no-ops on `!_isVisible`). User chose the visual treatment — pulsing amber bars, in sync, distinct from Speaking-orange and Idle-grey. Tightened AC (deferred-hide, Error precedence, no-flash for normal utterances) and promoted to todo (hook is no longer a TODO).
**Split into:** none.
**ADRs written:** none (added ADR-0006 to `related_adrs` — it records the lifecycle/state shape this binds to).

---

## 2026-06-28 13:56 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 task commit + this session-end line
**Scope:** infrastructure-d2v7n (lazy-load + keep-warm + idle-unload core lifecycle). Single ready task; its two dependents (q4t8m "warming up" overlay, b3n6p settings) remain in backlog — now unblocked but need `modeling` to promote to todo.
**ADRs written:** 0006 (scope: infrastructure).
**Follow-up:** 3 acceptance criteria are deploy-gated (idle private-bytes drop ~680 MB; short/long-utterance latency) — code-complete + unit-tested but await a live `/deploy` measurement to confirm the numbers.

---

## 2026-06-28 13:55 -- Task verified and completed: infrastructure-d2v7n - Lazy-load + keep-warm + idle-unload of the Parakeet model — core lifecycle

**Type:** Work / Task completion
**Task:** infrastructure-d2v7n - Lazy-load + keep-warm + idle-unload of the Parakeet model — core lifecycle
**Summary:** Implemented the `ModelLifecycleManager` state machine (Unloaded → Loading → Loaded → idle → Unloaded) — loads the ~640 MB Parakeet recognizer on Ctrl+Win key-DOWN, awaits that load on release, keeps warm through dictation, unloads after 5 min idle (Dispose+GC+trim, ~680 MB committed reclaimed), with self-healing decode so every consumer (HTTP API, file/stream) survives an unload under the shared decode lock.
**Verification:** PASS (iteration 1) — full suite green (162 tests, 15 new lifecycle tests covering all concurrency edges: await-before-loaded, single shared load on repeat presses, load-failure graceful, cancellation, idle-unload gating, NotifyActivity reset). Unload-safety invariant (no dispose mid-decode via shared lock) confirmed.
**Files changed:** 7
**Tests added:** 15
**ADRs written:** 0006 (scope: infrastructure)
**Deferred:** 3 acceptance criteria require a live `/deploy` RAM/latency measurement (idle private-bytes drop; short/long utterance latency) — code-complete and unit-tested; measurement steps recorded in the task Outcome. **User should run `/deploy` to capture the before/after numbers.**

---

## 2026-06-28 13:45 -- Batch started: [infrastructure-d2v7n]

**Type:** Work / Batch start
**Tasks:** infrastructure-d2v7n - Lazy-load + keep-warm + idle-unload of the Parakeet model — core lifecycle
**Parallel:** no (1 worker) — only ready task; dependency infrastructure-k9m3p (ADR-0005 GO) satisfied. Touches the model lifecycle hot path (TranscriptionService / DictationOrchestrator); its two dependents (q4t8m, b3n6p) stay in backlog until this lands.

---

## 2026-06-28 13:30 -- Modeling / Refined: infrastructure-d2v7n - Lazy-load + keep-warm + idle-unload of the Parakeet model

**Type:** Modeling / Refine
**BC:** infrastructure
**Status after:** todo
**Summary:** Folded the spike's measured numbers (ADR-0005: Dispose returns ~680 MB private bytes; reload a fixed ~4 s, session-init-bound; lazy-load on key-DOWN; 5-min idle fuse reusing the `NotifyActivity()` shape) into the task, sharpening Why/What/AC away from estimates. Confirmed the Nemotron re-validation caveat is moot — that branch name was retired, model stays INT8 Parakeet — clearing the only open risk. Split the feature into core lifecycle (d2v7n) + two dependents, then promoted the core to todo.
**Split into:** infrastructure-q4t8m ("warming up" overlay state), infrastructure-b3n6p (lazy-vs-eager + idle-timeout settings) — both depend_on d2v7n, left in backlog until the core lands.
**ADRs written:** none (ADR-0005 already records the decision; added the two child ids to its `related_tasks`).

---

## 2026-06-28 12:39 -- Concept page created: idle-memory-footprint

**Type:** Concept / Synthesis
**BC:** infrastructure
**Page:** contexts/infrastructure/concepts/idle-memory-footprint.md
**Derived from:** ADRs 0002–0005, the Parakeet quantization research report, and done tasks infrastructure-h4m2q / g3n5t / w7k9p / k9m3p + main-t6r2k.
**Why:** 3 workers independently flagged this convergence during the RAM-optimization run. Synthesizes the two lever families (GC/runtime tuning vs. post-startup housekeeping) and the recognizer lifecycle into one readable page so the next task touching footprint doesn't re-grep nine artifacts. Registered under the concepts marker in the infrastructure INDEX.

---

## 2026-06-28 12:37 -- Work session ended

**Type:** Work / Session end
**Completed:** 5 (first-try PASS: 5, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 6 (5 task commits + this session-end line)
**Scope:** the full RAM-optimization task set (h4m2q, g3n5t, w7k9p, main-t6r2k) + the idle-unload spike (k9m3p). Run sequentially by design — all tasks share the single /deploy app instance and needed clean before/after measurement attribution.
**ADRs written:** 0002 (global), 0003, 0004, 0005 (all scope: infrastructure).
**Concept convergence:** 3 workers independently flagged the same memory-housekeeping cluster (idle-memory-footprint-optimization / post-startup-memory-housekeeping / parakeet-recognizer-memory-lifecycle) across ADRs 0002–0005 + the 4 RAM tasks — strong signal for a single concept page (user decides).
**Follow-up:** k9m3p returned GO, clearing the blocker on backlog feature infrastructure-d2v7n (lazy-load + keep-warm + idle-unload). It stays in backlog until promoted to todo via modeling.

---

## 2026-06-28 12:36 -- Task verified and completed: infrastructure-k9m3p - Spike: does disposing the Parakeet recognizer return RAM, and how fast does it reload?

**Type:** Work / Task completion
**Task:** infrastructure-k9m3p - Spike: does disposing the Parakeet recognizer return RAM, and how fast does it reload?
**Summary:** GO for infrastructure-d2v7n. Throwaway harness (kept in scratchpad, outside the repo — app + `dotnet test` untouched) measured: Dispose() returns ~679 MB private bytes (707 → 28 MB, ~20 MB over the 8 MB baseline — ONNX arena retention did NOT bite), reload is a deterministic ~4 s independent of file-cache state (session-init-bound, not I/O-bound), transcripts identical across reloads. Recommend lazy-load on Ctrl+Win key-down + ~5-min idle-unload threshold. ADR-0005 (BC-local) records the GO + caveats. The blocker on backlog feature infrastructure-d2v7n is now cleared.
**Verification:** PASS (iteration 1) — RAM table + load-times present in both task Outcome and ADR-0005; private-byte deltas reconcile exactly; GO follows from the data; no production runtime change shipped.
**Files changed:** 1 (ADR-0005; throwaway harness kept out of the repo)
**Tests added:** 0 (measurement spike)
**ADRs written:** 0005-idle-unload-of-parakeet-recognizer-go.md (scope: infrastructure)

---

## 2026-06-28 12:35 -- Batch started: [infrastructure-k9m3p]

**Type:** Work / Batch start
**Tasks:** infrastructure-k9m3p - Spike: does disposing the Parakeet recognizer return RAM, and how fast does it reload?
**Parallel:** no (1 worker) — final task of the RAM-optimization run; throwaway measurement spike gating the backlog feature infrastructure-d2v7n.

---

## 2026-06-28 12:34 -- Task verified and completed: main-t6r2k - Reduce ASR intra-op threads 4 → 2

**Type:** Work / Task completion
**Task:** main-t6r2k - Reduce ASR intra-op threads 4 → 2
**Summary:** Lowered the Parakeet ASR intra-op thread cap from 4 to 2 (`Math.Min(Environment.ProcessorCount, 2)`) in `TranscriptionService.LoadModel()`. Machine-measured: decode +~60 ms on a 3 s clip (~160→220 ms, still ~13x real-time, instant-feel preserved), transcript text identical; per-thread RAM saving small (within noise) — the memory-mapped INT8 encoder masks it.
**Verification:** PASS (iteration 1) — build green, 147/147 tests pass; measurement figures internally consistent.
**Files changed:** 1
**Tests added:** 0 (config-tuning, no exposed seam)
**ADRs written:** none

---

## 2026-06-28 12:32 -- Batch started: [main-t6r2k]

**Type:** Work / Batch start
**Tasks:** main-t6r2k - Reduce ASR intra-op threads 4 → 2
**Parallel:** no (1 worker) — sequential RAM-optimization run; touches TranscriptionService.cs (the spike infrastructure-k9m3p touches the same file and runs after this).

---

## 2026-06-28 12:31 -- Task verified and completed: infrastructure-w7k9p - Trim Windows working set after model load and on idle

**Type:** Work / Task completion
**Task:** infrastructure-w7k9p - Trim Windows working set after model load and on idle
**Summary:** Added `WorkingSetTrimmer` (Windows `EmptyWorkingSet` P/Invoke, guarded + non-fatal) fired after model load via the post-startup hook's new `postCompactionStep` callback ("compact, then trim"), plus `IdleWorkingSetTrimmer` (3-min idle, 30s poll, re-armed by dictation activity, disposed on exit). Direct measurement of the trim: WorkingSet64 489→10 MB, PrivateMemorySize64 flat (trim moves cold pages to standby, doesn't free committed memory — the resident Parakeet recognizer is never unloaded). ADR-0004 (BC-local).
**Verification:** PASS (iteration 1) — 147/147 tests green incl. 9 new.
**Files changed:** 8
**Tests added:** 9
**ADRs written:** 0004-working-set-trim-after-load-and-on-idle.md (scope: infrastructure)

---

## 2026-06-28 12:28 -- Batch started: [infrastructure-w7k9p]

**Type:** Work / Batch start
**Tasks:** infrastructure-w7k9p - Trim Windows working set after model load and on idle
**Parallel:** no (1 worker) — appends the working-set trim onto the post-startup housekeeping hook landed by infrastructure-g3n5t ("compact, then trim"); measured on top of Workstation GC + startup compaction.

---

## 2026-06-28 12:27 -- Task verified and completed: infrastructure-g3n5t - Aggressive GC + LOH compaction once after startup

**Type:** Work / Task completion
**Task:** infrastructure-g3n5t - Aggressive GC + LOH compaction once after startup
**Summary:** Added `StartupMemoryCompactor` — a one-shot LOH-compacting gen-2 collection on a thread-pool thread ~5s after boot, wired into a shared post-startup housekeeping hook in `App.StartupCore` (with a `WHISPERHEIM_DISABLE_STARTUP_GC` kill switch) so the working-set trim (w7k9p) can append after it. Standalone RAM effect within measurement noise (836/777 → 840/782 MB) — kept as the "compact, then trim" precursor. ADR-0003 (BC-local) records it.
**Verification:** PASS (iteration 1) — 138/138 tests green incl. 3 new.
**Files changed:** 5
**Tests added:** 3
**ADRs written:** 0003-one-shot-startup-loh-compaction.md (scope: infrastructure)

---

## 2026-06-28 12:25 -- Batch started: [infrastructure-g3n5t]

**Type:** Work / Batch start
**Tasks:** infrastructure-g3n5t - Aggressive GC + LOH compaction once after startup
**Parallel:** no (1 worker) — sequential RAM-optimization run; measured on top of the now-landed Workstation GC (infrastructure-h4m2q).

---

## 2026-06-28 12:24 -- Task verified and completed: infrastructure-h4m2q - Switch Server GC → Workstation GC + concurrent

**Type:** Work / Task completion
**Task:** infrastructure-h4m2q - Switch Server GC → Workstation GC + concurrent
**Summary:** Switched the tray app from Server GC (DATAS-disabled) to Workstation GC + concurrent; idle private memory dropped ~47 MB (823 → 776 MB), well below the 200–400 MB estimate because the ~640 MB model + ONNX overhead dominates resident memory and GC mode doesn't touch it. ADR-0002 (global) records the decision and the measurement.
**Verification:** PASS (iteration 1)
**Files changed:** 2
**Tests added:** 0 (runtime-config + measurement task)
**ADRs written:** 0002-workstation-gc-for-idle-tray-app.md (scope: global)

---

## 2026-06-28 12:21 -- Batch started: [infrastructure-h4m2q]

**Type:** Work / Batch start
**Tasks:** infrastructure-h4m2q - Switch Server GC → Workstation GC + concurrent
**Parallel:** no (1 worker) — RAM-optimization set runs sequentially; all tasks share the single /deploy app instance and require clean before/after measurement attribution.

---

## 2026-06-28 12:20 -- Modeling / Captured: lazy-load / idle-unload of the Parakeet model (spike + feature)

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** todo (spike) + backlog (feature)
**Summary:** Capture the highest-impact RAM lever — free the ~640 MB recognizer while idle and lazily reload on Ctrl+Win, capturing audio in parallel; since dictation is push-to-talk batch (transcribe-on-release), the load hides behind speech for normal-length utterances. infrastructure-k9m3p (spike, todo) measures whether Dispose() actually returns RAM to the OS and cold/warm reload time → go/no-go; it `blocks` infrastructure-d2v7n (feature, backlog) implementing lazy-load + keep-warm + idle-unload + "warming up" overlay. Feature stays in backlog until the spike returns go.

---

## 2026-06-28 12:05 -- Modeling / Captured: RAM-optimization task set (4 tasks)

**Type:** Modeling / Capture
**BC:** infrastructure (3) + main (1)
**Filed to:** todo
**Summary:** Captured four tasks to cut WhisperHeim's ~1.3–1.4 GB steady-state footprint without breaking instant Ctrl+Win dictation (Parakeet ~640 MB stays resident), from the codebase investigation behind the Parakeet-quantization research report. infrastructure-h4m2q (Server GC → Workstation GC + concurrent, biggest win), infrastructure-w7k9p (Windows working-set trim after load / on idle), infrastructure-g3n5t (one-shot startup GC + LOH compaction), main-t6r2k (ASR intra-op threads 4→2 in TranscriptionService.cs:47). Each carries a before/after RAM measurement via /deploy.

---

## 2026-06-28 11:40 -- Research: Parakeet quantization & Nemotron comparison

**Type:** Research
**Requested by:** user
**Report:** knowledge/research/parakeet-quantization-and-nemotron-2026-06-28.md
**Review:** PASS (iteration 1)
**Summary:**
- Quantization's real win is BF16/FP16 (halves VRAM, ~10x NeMo speedup from half-precision + label-looping + CUDA Graphs + batching, no accuracy loss); INT8 only exists as community ONNX builds with no published German WER (A/B test required); CTranslate2 doesn't support Parakeet/TDT.
- "Nemotron" is now overloaded: NVIDIA shipped a real Nemotron Speech streaming ASR (`nemotron-3.5-asr-streaming-0.6b`, 2026-06-04, German-capable, OpenMDW-1.1) — it's a streaming sibling of Parakeet, not an LLM.
- Parakeet (batch, RTFx ~3,300) vs Nemotron Speech (streaming, ~17x more concurrent streams) is a batch-vs-streaming choice, not better-vs-worse; Canary-1B-v2 still leads NeMo German accuracy.

---

## 2026-06-28 11:24 -- Research: Best STT models for German & English

**Type:** Research
**Requested by:** user
**Report:** knowledge/research/best-stt-models-german-english-2026-06-28.md
**Review:** PASS (iteration 1)
**Summary:**
- Check Parakeet version first: v2 is English-only; v3 (`parakeet-tdt-0.6b-v3`, Aug 2025) adds German + 24 EU langs at extreme speed — likely the highest-value, lowest-effort change.
- Best open German accuracy: Voxtral Small 24B (~3.01% German WER, Apache 2.0) or lighter Canary-1B-v2 (~4.10%, CC-BY-4.0); accuracy↔speed split is TDT decoder (fast) vs transformer/LLM decoder (accurate).
- Whisper large-v3 + WhisperX remains the safe default for coverage/tooling but no longer the accuracy leader; cloud APIs beat all open weights but break local-first.

---

## 2026-06-19 16:33 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1

---

## 2026-06-19 16:31 -- Task verified and completed: main-r7n2k - Transcode any unsupported audio format via FFmpeg fallback (e.g. .opus)

**Type:** Work / Task completion
**Task:** main-r7n2k - Transcode any unsupported audio format via FFmpeg fallback (e.g. .opus)
**Summary:** Turned the FFmpeg decode path into an open last-resort fallback so any non-native format (e.g. .opus) transcodes instead of being rejected, across the UI, POST /transcribe, the CLI and the Claude Code plugin. FFmpeg-missing now surfaces as a distinct HTTP 501 (body names FFmpeg) versus 415 for corrupt/not-audio; the decode path still never blocks on the install modal (main-110 contract holds).
**Verification:** PASS (iteration 1) — full solution builds clean, 135/135 tests pass (12 new). All 10 acceptance criteria covered by tests or inspectable wiring.
**Files changed:** 10 (8 src/test/doc modified + 2 new test files)
**Tests added:** 12 (across AudioFileDecoderTests, FileTranscriptionServiceTests, TranscribeRequestHandlerTests)
**ADRs written:** none (implements ADR-0001; the 415-vs-501 choice was pre-authorized within the task)

---

## 2026-06-19 16:23 -- Batch started: [main-r7n2k]

**Type:** Work / Batch start
**Tasks:** main-r7n2k - Transcode any unsupported audio format via FFmpeg fallback (e.g. .opus)
**Parallel:** no (1 worker)

---

## 2026-06-19 14:05 -- Modeling / Captured: main-r7n2k - Transcode any unsupported audio format via FFmpeg fallback (e.g. .opus)

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** Captured the "don't reject formats the native engine can't read — convert them with FFmpeg first" feature, motivated by `.opus` files being refused. The orchestrator found the rejection lives at three gates (`FileTranscriptionService` allowlist — which the UI drag/browse filter hits first and silently drops `.opus` today; `AudioFileDecoder`'s `_ =>` throw; and the HTTP `ClassifyError`), all of which must change. Design: generalize the existing `DecodeOggWithFfmpeg` routine into an open last-resort fallback (try ffmpeg on any non-native extension to 16kHz mono PCM), make `IsSupported` permissive, keep the decode path non-blocking (main-110 contract), and surface a distinct ffmpeg-missing error (mapped to a non-500 HTTP status for the CLI / Claude Code plugin). No ADR — implements existing direction (main-110, ADR-0001) with no new dependency or transport. Filed straight to todo: design fully resolved, nothing blocking.

---

## 2026-06-19 13:36 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1

---

## 2026-06-19 13:35 -- Task verified and completed: main-q4m8t - whisperheim-transcribe CLI wrapper over POST /transcribe

**Type:** Work / Task completion
**Task:** main-q4m8t - whisperheim-transcribe CLI wrapper over POST /transcribe
**Summary:** Added whisperheim-transcribe, a thin CLI wrapper over POST /transcribe that POSTs an audio file's raw bytes (with a ?filename= / X-Filename hint) and prints only the transcript text to stdout, honoring WHISPERHEIM_ENDPOINT and the 0/1/2/3 exit-code scheme; ships alongside the tray exe via publish.ps1.
**Verification:** PASS (iteration 1) — full solution builds clean, CLI test project green (14/14), all acceptance criteria covered by tests or inspectable wiring.
**Files changed:** 7
**Tests added:** 1 test file (CliCoreTests, 14 tests)
**ADRs written:** none (implements ADR-0001)

---

## 2026-06-19 13:30 -- Batch started: [main-q4m8t]

**Type:** Work / Batch start
**Tasks:** main-q4m8t - whisperheim-transcribe CLI wrapper over POST /transcribe
**Parallel:** no (1 worker)

---

## 2026-06-19 13:19 -- Modeling / Refined: main-q4m8t - whisperheim-transcribe CLI wrapper over POST /transcribe

**Type:** Modeling / Refine
**BC:** main
**Status after:** todo
**Summary:** Sharpened the CLI-wrapper task against the now-live `/transcribe` contract (dependency main-h7k2p shipped). Made explicit that the endpoint takes raw audio bytes (not a JSON envelope) and returns JSON `{text, ...}` — the wrapper POSTs raw bytes with `?filename=` and prints **only `result.text`** to stdout (user decision; plain text, no metadata). Pinned the exit-code scheme to the `Utterheim.Cli` reference (`0`/`1`/`2`/`3`), flagged the 5 s reference timeout as too short (use multi-minute / infinite), and named the two deliberate deltas from the speak CLI (raw bytes vs `PostAsJsonAsync`; print `.text` vs `.requestId`). Added main-h7k2p as prior_art. Promoted backlog → todo since the only blocker (the live endpoint) is now done and tested.
**Split into:** none
**ADRs written:** none

---

## 2026-06-19 12:13 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1

---

## 2026-06-19 12:12 -- Task verified and completed: main-h7k2p - STT API — POST /transcribe HttpListener server + queue integration

**Type:** Work / Task completion
**Task:** main-h7k2p - STT API — POST /transcribe HttpListener server + queue integration
**Summary:** Exposed the WhisperHeim transcription engine as a loopback-only HTTP STT API (POST /transcribe + GET /health on 127.0.0.1:7777) per ADR-0001, funnelling requests through the existing single-engine TranscriptionQueueService with synchronous block-and-return-JSON.
**Verification:** PASS (iteration 1) — build green, 123 tests pass, all 8 acceptance criteria covered by tests.
**Files changed:** 9
**Tests added:** 2 test files (TranscribeRequestHandlerTests, TranscribeServerTests)
**ADRs written:** none (implements ADR-0001)

---

## 2026-06-19 12:04 -- Batch started: [main-h7k2p]

**Type:** Work / Batch start
**Tasks:** main-h7k2p - STT API — POST /transcribe HttpListener server + queue integration
**Parallel:** no (1 worker)

---

## 2026-06-19 11:25 -- Modeling / Captured: main-h7k2p + main-q4m8t — STT API (transcribe endpoint)

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo (main-h7k2p), backlog (main-q4m8t)
**Summary:** Captured the "expose STT to Claude as a local API" feature, scoped to batch-only v1 (file in → full transcript out), first-party Claude consumer (no auth, no OpenAI-compat). The architect made the transport + shape decision, ratified by the user, written as ADR-0001: HttpListener loopback `127.0.0.1:7777`, synchronous `POST /transcribe` blocking through the existing single-engine `TranscriptionQueueService`, plus `GET /health`. main-h7k2p (server + queue integration) is ready in todo; main-q4m8t (`whisperheim-transcribe` CLI wrapper) waits on it in backlog. Named pipe was the runner-up; rejected on the inspectability / house-shape tie-break.
**ADRs written:** 0001

---

## 2026-06-19 10:55 -- Research: Exposing WhisperHeim STT as an API

**Type:** Research
**Requested by:** user
**Report:** knowledge/research/whisperheim-stt-api-exposure-2026-06-19.md
**Review:** PASS (iteration 1); revised 11:10 after scope clarification (consumer is Claude, local/first-party — not cloud) + read of Utterheim's source (§7: Kestrel Minimal API, loopback 127.0.0.1:7223, no auth, IHostedService on Generic Host, CLI wrapper). Named pipes reinstated first-class; OpenAI-compat + DNS-rebinding concerns demoted.
**Summary:**
- Batch (send file → full text) fits the actual use cases and the existing engine; real-time streaming partials are a separate, larger commitment likely needing a streaming model.
- OpenAI `POST /v1/audio/transcriptions` (multipart `file`+`model`, `response_format` json|text|srt|verbose_json|vtt) is the de-facto interop standard — whisper.cpp server, Speaches, LocalAI all adopted it; implementing the subset lets existing client SDKs reach WhisperHeim by changing only the base URL.
- In-process hosting forks between embedded Kestrel/Minimal API (full features, larger footprint) and `HttpListener` (lighter, batch-HTTP only); the single shared `TranscriptionQueueService` is the real constraint. Loopback bind alone is insufficient (DNS rebinding bypasses CORS) — needs Host-header validation + bearer token.

---

## 2026-05-13 -- Migration: `.workflow/` → `.agentheim/`

**Type:** Repo Migration
**Summary:** Converted the existing `.workflow/` directory (vision, roadmap, protocol log, research, and 118+ tasks across backlog/todo/in-progress/done) into the canonical `.agentheim/` layout produced by the agentheim plugin: single bounded context `main/` (matches the existing flat task numbering), infrastructure BC scaffolded per agentheim convention, research files relocated under `knowledge/research/`, top-level + per-BC `INDEX.md` generated. Existing `.workflow/` preserved for comparison; will be removed manually.

---

## 2026-05-12 14:57 -- Task Completed: 116 - Fix vpk Version Pin in Release Workflow

**Type:** Task Completion
**Task:** 116 - Fix vpk Version Pin in Release Workflow (0.0.1589 unavailable)
**Summary:** Pinned vpk to verified version `0.0.1298` (latest on nuget.org per `dotnet tool search vpk`) in both `.github/workflows/release.yml` and `docs/release.md`. Research file left as historical snapshot. Tag-push smoke test deferred to manual user verification before first public release.
**Files changed:** 3 files

---

## 2026-05-12 14:56 -- Batch Started: [116]

**Type:** Batch Start
**Tasks:** 116 - Fix vpk Version Pin in Release Workflow
**Mode:** Parallel (batch of 1; last task in todo)

---

## 2026-05-12 14:55 -- Task Completed: 115 - Code Signing — Deferred Hook

**Type:** Task Completion
**Task:** 115 - Code Signing — Deferred Hook (Wire-Up Now, Flip Post-UG)
**Summary:** Refined inline TODO at `vpk pack` step in `.github/workflows/release.yml` to enumerate both signtool (`CERT_PFX_BASE64` / `CERT_PASSWORD`) and Azure Trusted Signing paths. Expanded `docs/release.md` Signing section into a full post-UG runbook covering both paths, SmartScreen reputation impact (OV warming vs EV/Trusted Signing instant trust), and a 7-step follow-up checklist. No certs purchased; no actual signing wired up. README disclaimer verified accurate.
**Files changed:** 3 files

---

## 2026-05-12 14:53 -- Batch Started: [115]

**Type:** Batch Start
**Tasks:** 115 - Code Signing — Deferred Hook (Wire-Up Now, Flip Post-UG)
**Mode:** Parallel (batch of 1; 116 promoted from backlog but demoted in this batch due to `release.yml` conflict)

---

## 2026-05-12 14:52 -- Task Completed: 114 - Velopack End-to-End Dry Run

**Type:** Task Completion
**Task:** 114 - Velopack End-to-End Dry Run
**Summary:** Local Velopack pipeline verified end-to-end: `dotnet publish` produces 206 MB self-contained output with both bundled small models correctly placed, `vpk pack 0.0.1-test` emits Setup.exe (92 MB), full nupkg, RELEASES manifest, and modern release JSON. Steps 2-8 (clean-profile install, first-run UX, delta update, uninstall, SAC behaviour) deferred to manual user verification before first public tag. **Surfaced regression:** release.yml pins `vpk 0.0.1589` (does not exist on nuget.org — latest is 0.0.1298). Filed as Task 116 in backlog.
**Files changed:** 3 files (incl. new backlog task 116)

---

## 2026-05-12 14:52 -- Task Completed: 112 - Public README + GitHub Release Page Content

**Type:** Task Completion
**Task:** 112 - Public README + GitHub Release Page Content
**Summary:** Rewrote top-level README (hero, download/install with SmartScreen+SAC click-through, first-run, hotkeys, optional FFmpeg, data location, privacy, TBD license), added `docs/why-unsigned.md`, created `.github/release-template.md`, appended SHA-256 surfacing section to `docs/release.md`, dropped `docs/media/README.md` placeholder. `install.mp4` recording and friend-tested install deferred to manual follow-up.
**Files changed:** 6 files

---

## 2026-05-12 14:46 -- Batch Started: [112, 114]

**Type:** Batch Start
**Tasks:** 112 - Public README + GitHub Release Page Content, 114 - Velopack End-to-End Dry Run
**Mode:** Parallel (batch of 2; 115 demoted due to `docs/release.md` conflict with 112)

---

## 2026-05-12 14:45 -- Task Completed: 111 - GitHub Actions Release Workflow (Tag-Triggered Velopack Build)

**Type:** Task Completion
**Task:** 111 - GitHub Actions Release Workflow
**Summary:** Created `.github/workflows/release.yml` (tag-triggered, publish self-contained win-x64 ReadyToRun → pinned `vpk 0.0.1589` → download-prior (continue-on-error) → pack with signing-TODO preserved → SHA-256 capture → upload to GitHub Release) and `docs/release.md` with the local-iteration pwsh recipe. YAML validated. "First real tag end-to-end" criterion deferred to Task 114.
**Files changed:** 3 files

---

## 2026-05-12 14:44 -- Batch Started: [111]

**Type:** Batch Start
**Tasks:** 111 - GitHub Actions Release Workflow (Tag-Triggered Velopack Build)
**Mode:** Parallel (batch of 1; only 111 unblocked after batch 3)

---

## 2026-05-12 14:43 -- Task Completed: 110 - FFmpeg Detection + First-Use Install Prompt

**Type:** Task Completion
**Task:** 110 - FFmpeg Detection + First-Use Install Prompt
**Summary:** Added `FfmpegDetector` (singleton, PATH probe + winget-location fallback, 2 s timeout, StateChanged event) and WPF-UI `FfmpegMissingDialog` (winget runner with streamed log, gyan.dev link, "I installed it" re-detect, winget-absent/access-denied edge cases). Wired UI-agnostic `IFfmpegPromptService` into `StreamTranscriptionService` (hard-require with retry-once) and `AudioFileDecoder` (Concentus fallback preserved ahead of modal). Live-updating FFmpeg status card on General page.
**Files changed:** 10 files

---

## 2026-05-12 14:43 -- Task Completed: 109 - Bundle Silero VAD + Pyannote Seg in the Publish Output

**Type:** Task Completion
**Task:** 109 - Bundle Silero VAD + Pyannote Seg in the Publish Output
**Summary:** Vendored Silero VAD (~2 MB) and Pyannote Segmentation 3.0 (~1.5 MB) ONNX models into `src/WhisperHeim/Assets/Models/`, wired them through csproj with `PreserveNewest` and `<Link>` so publish output places them at `{publish}/models/<subdir>/<file>`. Added bundled-first `ResolveModelPath` to `ModelManagerService` consulted by `CheckModel`, `EnsureModelsAsync`, and `DownloadModelAsync`. MIT license attribution added to About page.
**Files changed:** 5 files

---

## 2026-05-12 14:33 -- Batch Started: [109, 110]

**Type:** Batch Start
**Tasks:** 109 - Bundle Silero VAD + Pyannote Seg in the Publish Output, 110 - FFmpeg Detection + First-Use Install Prompt
**Mode:** Parallel (batch of 2; no file overlap)

---

## 2026-05-12 14:32 -- Task Completed: 113 - Uninstall Data Preservation (Hygiene + Documentation)

**Type:** Task Completion
**Task:** 113 - Uninstall Data Preservation
**Summary:** Added install-dir guard rejecting DataPaths under `AppContext.BaseDirectory` / `%LocalAppData%\WhisperHeim\` in both `GeneralPage.xaml.cs` and `DataPathService.SetDataPath` as defence-in-depth; implemented optional pre-uninstall Velopack hook dropping `WhisperHeim-data-location.txt` on desktop; completed user-data audit (only transient recording staging hits LocalAppData by design). 98/98 tests pass (8 new).
**Files changed:** 5 files

---

## 2026-05-12 14:32 -- Task Completed: 108 - First-Run Model Download Dialog

**Type:** Task Completion
**Task:** 108 - First-Run Model Download Dialog
**Summary:** Implemented WPF-UI styled `FirstRunSetupWindow` (per-model progress, pause/resume via HTTP Range, skip, retry-on-error), added `EnsureModelsAsync` / `GetMissingRequiredModels` / `models/manifest.json` IO to `ModelManagerService`, rewired `App.OnStartup` to gate on `IsFirstRun || missingRequired` while leaving the lazy-fallback `ModelDownloadDialog` intact.
**Files changed:** 5 files

---

## 2026-05-12 14:26 -- Batch Started: [108, 113]

**Type:** Batch Start
**Tasks:** 108 - First-Run Model Download Dialog, 113 - Uninstall Data Preservation (Hygiene + Documentation)
**Mode:** Parallel (batch of 2; 109 and 110 demoted due to ModelManagerService / App.xaml.cs conflict with 108)

---

## 2026-05-12 14:25 -- Task Completed: 107 - Add Velopack to the Project (Custom Main + Bootstrap)

**Type:** Task Completion
**Task:** 107 - Add Velopack to the Project (Custom Main + Bootstrap)
**Summary:** Wired Velopack via custom `[STAThread] Main` in Program.cs with UI-free `OnFirstRun` hook, switched App.xaml from ApplicationDefinition to Page, set `<StartupObject>` to `WhisperHeim.Program`, exposed first-run detection (`App.IsFirstRun` via `Program.IsFirstRun` + `VELOPACK_FIRSTRUN` env var) for Task 108 to consume. `dotnet build` clean. Full `vpk pack` smoke-tests deferred to Task 114.
**Files changed:** 4 files

---

## 2026-05-12 14:22 -- Batch Started: [107]

**Type:** Batch Start
**Tasks:** 107 - Add Velopack to the Project (Custom Main + Bootstrap)
**Mode:** Parallel (batch of 1; 109 and 110 demoted due to csproj / App.xaml.cs conflict with 107)

---

## 2026-05-12 -- Planning: M5 Public Release backlog from installer research

**Type:** Planning
**Summary:** Captured the 9 implications from `.workflow/research/installer-and-github-distribution.md` as backlog tasks under a new milestone M5: Public Release (GitHub Distribution). Each task links back to the research file and includes acceptance criteria, dependencies, and concrete file/code touch points so a worker can pick any one up cold. Dependency spine: 107 (Velopack bootstrap) → 108 (first-run dialog), 109 (bundle small models), 110 (FFmpeg detect/prompt), 111 (release workflow, also depends on 109), 112 (README, depends on 111), 113 (uninstall hygiene, depends on 107), 114 (E2E dry run, depends on most), 115 (signing hook, depends on 111).
**Milestones created/updated:** Added M5: Public Release (GitHub Distribution) to `roadmap.md`; marked M4 (TTS) as removed per Task 103.
**Tasks created:** 107-velopack-bootstrap, 108-first-run-model-download-dialog, 109-bundle-small-models-in-publish, 110-ffmpeg-detection-and-install-prompt, 111-github-actions-release-workflow, 112-readme-and-release-page-content, 113-uninstall-data-preservation, 114-velopack-pack-dry-run, 115-code-signing-deferred-hook (all in `backlog/`).
**Tasks moved to backlog:** n/a (all newly created in backlog).
**Ideas incorporated:** n/a.

---

## 2026-05-12 -- Research Completed: Installer & GitHub Distribution

**Type:** Research
**Topic:** How should WhisperHeim be packaged into a GitHub-released Windows installer with all dependencies bundled (or downloaded on first run) in 2026?
**Output:** `.workflow/research/installer-and-github-distribution.md`
**Summary:** Don't bundle the 640 MB Parakeet model — move from "first-use" to "first-launch with progress dialog" (matches LM Studio / Whisper Desktop / Buzz). DO bundle the tiny models (Silero VAD, Pyannote Seg ~3 MB total). FFmpeg: **do NOT bundle — detect at startup and prompt the user to install it themselves via `winget install Gyan.FFmpeg` or a download-page link** (decided 2026-05-12 by user after initial recommendation to bundle BtbN `lgpl-shared`). This eliminates LGPL source-mirror / attribution / unmodified-binary obligations entirely; the user picks the build under their own license. Features that need FFmpeg (Stream/YouTube transcription) surface the same prompt on first invocation; OGG decode already falls back to Concentus. The bundling section is retained in the report as reference if we ever revisit. User data MUST live in `%AppData%\WhisperHeim` (Roaming) not `%LocalAppData%\WhisperHeim` (the install dir Velopack wipes). SmartScreen tightened in Win11 25H2; Smart App Control hard-blocks unsigned with no override — release page needs a click-through video and a SAC caveat. Latest Velopack 0.0.1589 (Apr 2026); code-signing flags exist in `vpk pack` and can be flipped on post-UG without re-architecting. Index updated. Punch list of 9 concrete tasks in the report.
**Caller:** user (one-shot research request)

---

## 2026-05-11 11:29 -- Task Completed: 105 - Origin-Machine Owns Transcription

**Type:** Task Completion
**Task:** 105 - Origin-Machine Owns Transcription (Multi-Machine Coordination)
**Summary:** Added per-machine `MachineId` (sanitised `Environment.MachineName`, persisted in bootstrap.json), stamped recording session directories with `_{machineId}` and an in-session `session.json`, gated `TranscriptStorageService.ListPendingSessions` to this machine's origin (with directory-suffix and legacy fallback), added `ListPendingSessionsFromOtherMachines`, built an "Other machine" pending section in TranscriptsPage with an advisory-lock "Transcribe here" takeover, surfaced MachineId in General page + startup trace. `HighQualityRecorderService` deliberately not modified (voice-clone samples aren't auto-transcribed; Streams pipeline is URL-based) — flagged in the work log. Build clean, 90/90 tests pass. Manual cross-machine verification remains for the user.
**Files changed:** 12 files

---

## 2026-05-11 11:18 -- Batch Started: [105]

**Type:** Batch Start
**Tasks:** 105 - Origin-Machine Owns Transcription (Multi-Machine Coordination)
**Mode:** Parallel (batch of 1; only remaining todo task)

---

## 2026-05-11 11:24 -- Task Completed: 104 - Stage WAV Writes Outside the Synced Data Folder

**Type:** Task Completion
**Task:** 104 - Stage WAV Writes Outside the Synced Data Folder
**Summary:** Implemented machine-local WAV staging via a new shared `RecordingFileStager` helper, added `RecordingStagingPath` to `DataPathService`, wired both `CallRecordingService` and `HighQualityRecorderService` to stage writes outside the synced data folder and atomically move on stop, and added a startup orphan-recovery sweep in `App.xaml.cs`. 8 new xUnit tests added; 82/82 tests pass. Manual cloud-sync verification (Drive, mid-recording kill, unwritable destination) remains for the user.
**Files changed:** 7 files

---

## 2026-05-11 11:12 -- Batch Started: [104]

**Type:** Batch Start
**Tasks:** 104 - Stage WAV Writes Outside the Synced Data Folder
**Mode:** Parallel (batch of 1; 105 deferred — conflicts with 104 on CallRecordingService.StartRecording, HighQualityRecorderService, DataPathService)

---

## 2026-05-11 -- Model / Promoted: 104, 105

**Type:** Model / Promote
**BC:** WhisperHeim (single-context project — `.workflow/tasks/`)
**From → To:** backlog → todo
**Tasks:**
- 104 - Stage WAV writes outside the synced data folder
- 105 - Origin-machine owns transcription (multi-machine coordination)

Both depend on 063 (done) and 102 (done) — dependencies satisfied. Tasks are independent of each other and can run in either order.

---

## 2026-05-11 10:58 -- Task Completed: 106 - No Window Frame Flash When Start-Minimized

**Type:** Task Completion
**Task:** 106 - No Window Frame Flash When Start-Minimized
**Summary:** Hoisted the tray icon, global hotkeys, dictation orchestrator, dictation overlay, and call-recording → transcription-queue plumbing out of MainWindow into App-owned services (`TrayIconHost`, `AutoTranscriptionService`) so MainWindow can be constructed lazily on first user request. Start-minimized path no longer instantiates a window at all, structurally eliminating the empty-frame flash. Build green, 74 tests pass; manual cold-launch verification still required.
**Files changed:** 7 files

---

## 2026-05-11 10:42 -- Task Started: 106 - No Window Frame Flash When Start-Minimized

**Type:** Task Start
**Task:** 106 - No Window Frame Flash When Start-Minimized
**Milestone:** Polish / UX

---

## 2026-05-11 -- Model / Promoted: 106 - No window frame flash when start-minimized

**Type:** Model / Promote
**BC:** WhisperHeim (single-context project — `.workflow/tasks/`)
**From → To:** backlog → todo

---

## 2026-05-11 -- Model / Refined: 106 - No window frame flash when start-minimized

**Type:** Model / Refine
**BC:** WhisperHeim (single-context project — `.workflow/tasks/`)
**Status after:** backlog (ready for promotion)
**Summary:** Committed to Approach A (move tray icon out of MainWindow's visual tree, lazy MainWindow construction); rejected B (AllowsTransparency risks) and C (the current racy `Show()`/`Hide()` dance). Grounded the plan in actual code: produced an inventory of what stays in MainWindow vs. moves to App / new `TrayIconHost`. Surfaced a hidden coupling — `TranscriptsPage` itself subscribes to `RecordingStopped` and drives the transcription queue, so MainWindow eagerly constructs it. Under lazy MainWindow that breaks call-recording auto-transcription; refinement adds an extraction step for a headless `AutoTranscriptionService` as step 1 of the plan. Added six ordered implementation steps, expanded acceptance criteria to cover hotkeys/overlay/auto-transcription working before any window is opened, and listed risks (first-open latency, settings hot-reload subscriber timing, overlay disposal). Size firmed up from "Small-Medium (depends on approach)" to **Medium**.

---

## 2026-05-11 -- Model / Captured: 106 - No window frame flash when start-minimized

**Type:** Model / Capture
**BC:** WhisperHeim (single-context project — `.workflow/tasks/`)
**Filed to:** backlog
**Summary:** Bug: when StartMinimized is on, an empty window frame sometimes paints on the desktop before the tray icon takes over. Root cause is the Show()/Hide() race in `MainWindow.InitializeTrayAndHide()` — the tray icon is declared inside MainWindow.xaml, so the current workaround calls Show() to force visual-tree load. Task proposes moving the NotifyIcon out of MainWindow's visual tree into App.xaml so no window ever has to be shown on the start-minimized path.

---

## 2026-04-24 14:54 -- Task Completed: 103 - Remove Text-to-Speech Feature

**Type:** Task Completion
**Task:** 103 - Remove Text-to-Speech Feature
**Summary:** Removed TTS end-to-end — deleted `TextToSpeechPage`, `Services/TextToSpeech/`, `Services/SelectedText/` (incl. `ReadAloudHotkeyService`), Pocket TTS model defs, `TtsSettings`, and `TtsPlaybackDeviceId`. Added a one-shot `TtsCleanupDone` bootstrap-flag migration in `DataPathService` that purges Pocket TTS model files, `{DataPath}/voices/`, and the `"tts"` key from `settings.json`. Build clean, 74/74 tests pass.
**Files changed:** 17 files

---

## 2026-04-24 14:46 -- Task Started: 103 - Remove Text-to-Speech Feature

**Type:** Task Start
**Task:** 103 - Remove Text-to-Speech Feature
**Note:** Includes cleanup of `SettingsChanged` subscription added to TextToSpeechPage by task 102 (sequencing inverted from capture note).

---

## 2026-04-24 14:26 -- Task Completed: 102 - Hot-Reload Settings from Disk (Multi-Machine Sync)

**Type:** Task Completion
**Task:** 102 - Hot-Reload Settings from Disk (Multi-Machine Sync)
**Summary:** Implemented hot-reload of `settings.json` via a debounced `FileSystemWatcher` with 5s self-write suppression and pre-save list-merge; moved Ollama endpoint/model to machine-local `bootstrap.json` with a first-run migrator; added a UI-thread `SettingsChanged` event with subscribers on Templates/General/TTS/Dictation pages; watcher disposes on shutdown and recreates on `DataPath` change.
**Files changed:** 10 files

---

## 2026-04-24 -- Idea Captured: Remove Text-to-Speech Feature

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/103-remove-text-to-speech-feature.md
**Summary:** Remove TTS in its entirety — page, services (`TextToSpeechService`, `AudioExportService`), Pocket TTS model definitions (FP32 + int8), voice cloning, `ReadAloudHotkeyService`, `TtsSettings`, `TtsPlaybackDeviceId` in bootstrap, and navigation. Add one-time first-run cleanup to delete downloaded Pocket TTS model files, `%APPDATA%\WhisperHeim\voices`, and strip the `"tts"` key from users' `settings.json`. Shared infra (NAudio, sherpa-onnx, Concentus, shared recorder services) stays — used by dictation/recording. Coordinate with task 102: land 103 first so 102 doesn't need to subscribe TextToSpeechPage to `SettingsChanged`.

---

## 2026-04-24 14:16 -- Task Started: 102 - Hot-Reload Settings from Disk (Multi-Machine Sync)

**Type:** Task Start
**Task:** 102 - Hot-Reload Settings from Disk (Multi-Machine Sync)
**Milestone:** Post-M1 polish (multi-machine sync)

---

## 2026-04-24 -- Idea Captured: Hot-Reload Settings from Disk (Multi-Machine Sync)

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/102-hot-reload-settings-from-disk.md
**Summary:** When another WhisperHeim instance edits `settings.json` on a shared cloud-synced `DataPath`, the running instance should detect and hot-reload the change. FileSystemWatcher + 500ms debounce, 5s self-write suppression, pre-save reload+merge to protect concurrent list-field additions, `SettingsChanged` event for live UI refresh. Ollama endpoint + model move to machine-local `bootstrap.json`; `AnalysisTemplates` stays synced.

---

## 2026-04-20 12:55 -- Task Completed: 101 - Deterministic Clean-Text Pipeline (Filler Word Removal)

**Type:** Task Completion
**Task:** 101 - Deterministic Clean-Text Pipeline (Filler Word Removal)
**Summary:** Implemented `FillerRemovalService` stripping English multi-word + single-word fillers and German single-word fillers (de/de-DE), wired into `DictationOrchestrator` between ASR and SendInput, added persisted Raw/Clean toggle (default Clean) in Dictation settings, 42 unit tests passing (74/74 total).
**Files changed:** 7 files

---

## 2026-04-20 12:48 -- Task Started: 101 - Deterministic Clean-Text Pipeline (Filler Word Removal)

**Type:** Task Start
**Task:** 101 - Deterministic Clean-Text Pipeline (Filler Word Removal)
**Milestone:** M1 - Live Dictation + Core App (post-launch polish)

---

## 2026-04-20 -- Task Promoted: 101 - Deterministic Clean-Text Pipeline (Filler Word Removal)

**Type:** Task Promotion
**From:** backlog
**To:** todo
**Summary:** Refined scope after inspecting MacParakeet's actual code (vs their spec): dropped sentence-start-only tier 3, confirmed clean-only pipeline return, hardcoded word lists. Size reduced Medium→Small. German list locked to single-word unconditional (äh, ähm, hm, hmm, öh, öhm); multi-word German excluded because discourse particles carry meaning.

---

## 2026-04-20 -- Research: MacParakeet Feature Comparison

**Type:** Research
**Topic:** Compare MacParakeet (moona3k/macparakeet) to WhisperHeim and identify behaviors not yet implemented
**File:** research/macparakeet-feature-comparison.md
**Key findings:**
- WhisperHeim already has YouTube transcription, dual-capture call recording, speaker diarization, transcript viewer/playback, local-LLM transcript analysis, templates, plus TTS + voice cloning + Read Aloud (which MacParakeet does not)
- Biggest gap: deterministic clean-text pipeline (filler removal, custom word replacements, in-dictation snippet expansion, whitespace cleanup, Raw/Clean toggle) — research already done but not implemented
- Second gap: per-utterance dictation history log (unlocks private dictation mode, voice stats, favorites)
- Third gap: export depth (SRT/VTT/JSON with word-level timestamps, DOCX/PDF) — Parakeet already provides the word timestamps
- Fourth gap: LLM layer expansion — prompt library, multi-summary per transcript, multi-conversation transcript chat (on top of existing Ollama integration)
- Polish gaps: soft-cancel undo window, first-run onboarding flow, push-to-talk vs latched hotkey modes, CLI tool for batch automation, synced playback word-highlighting

---

## 2026-04-09 14:00 -- Research: Gemma 4 vs Qwen 2.5 14B for Transcript Analysis

**Type:** Research
**Topic:** Whether Google's Gemma 4 (released April 2, 2026) can replace Qwen 2.5 14B for transcript analysis on RTX 3080 10GB
**File:** research/gemma-4-vs-qwen-2.5-for-transcript-analysis.md
**Key findings:**
- Gemma 4 E4B (effective 4B params, ~2.5GB VRAM) fits easily but scores 10+ points lower on MMLU and much worse on instruction following than Qwen 2.5 14B
- The competitive Gemma 4 models (26B MoE at 15GB, 31B Dense at 18GB) don't fit on 10GB VRAM
- Qwen 2.5 14B remains the best choice for structured transcript extraction on this hardware
- Open question: Qwen 3.5-9B (~6GB VRAM) may be a better upgrade path than any Gemma model

---

## 2026-04-07 -- Task Completed: 100 - Streams page visual polish

**Type:** Task Completion
**Task:** 100 - Streams page visual polish
**Summary:** Visual polish applied to Streams page — bold title with subtitle, URL input card with link icon and platform indicators, "RECENT TRANSCRIPTS" section label, richer transcript cards with platform badges, method pills, labeled metadata columns, icon-only action buttons with hover accents, and consistent 40px margins with MaxWidth 900.
**Files changed:** 2 files

---

## 2026-04-07 -- Task Started: 100 - Streams page visual polish

**Type:** Task Start
**Task:** 100 - Streams page visual polish
**Milestone:** UI Polish

---

## 2026-04-07 -- Idea Captured: Streams page visual polish

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/100-streams-visual-polish.md
**Summary:** Visual polish for the Streams page inspired by the editorial design in inspiration/youtube/. Covers richer header, URL input with platform indicators, improved transcript cards with source badges and metadata columns, and consistency fixes to match other pages. Stays within the existing WPF UI theme.

---

## 2026-04-07 -- Task Completed: 099 - Explicit Transcription Queuing

**Type:** Task Completion
**Task:** 099 - Explicit Transcription Queuing
**Summary:** Added visual distinction for queued vs unqueued pending items (amber/orange styling for queued, blue for actively transcribing). Most work was already done by task 098.
**Files changed:** 2 files

---

## 2026-04-07 -- Task Completed: 098 - Pending Transcription Drawer with Playback

**Type:** Task Completion
**Task:** 098 - Pending Transcription Drawer with Playback
**Summary:** Implemented pending drawer with full editing UI, audio playback with seek bar, and explicit "Queue Transcription" button. Removed auto-enqueue on click. Added metadata persistence via transcript_name.json.
**Files changed:** 2 files

---

## 2026-04-07 -- Task Started: 099 - Explicit Transcription Queuing

**Type:** Task Start
**Task:** 099 - Explicit Transcription Queuing
**Milestone:** M2

---

## 2026-04-07 -- Task Completed: 097 - Enter-to-Confirm in Drawer Text Fields

**Type:** Task Completion
**Task:** 097 - Enter-to-Confirm in Drawer Text Fields
**Summary:** Added Enter-to-confirm behavior for transcript name and speaker name fields. Pressing Enter dismisses focus and immediately updates the list entry. Pressing Escape dismisses without committing.
**Files changed:** 1 file

---

## 2026-04-07 -- Task Started: 098 - Pending Transcription Drawer with Playback

**Type:** Task Start
**Task:** 098 - Pending Transcription Drawer with Playback
**Milestone:** M2

---

## 2026-04-07 -- Task Started: 097 - Enter-to-Confirm in Drawer Text Fields

**Type:** Task Start
**Task:** 097 - Enter-to-Confirm in Drawer Text Fields
**Milestone:** M2

---

## 2026-04-07 -- Batch Promoted: 097, 098, 099

**Type:** Batch Promotion
**Tasks:** 097 - Enter-to-Confirm in Drawer Text Fields, 098 - Pending Transcription Drawer with Playback, 099 - Explicit Transcription Queuing
**Summary:** Promoted all three drawer UX tasks to todo for sequential execution based on dependency chain.

---

## 2026-04-07 -- Ideas Captured: 097, 098, 099

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/backlog/097-enter-to-confirm-drawer-fields.md, tasks/backlog/098-pending-transcription-drawer.md, tasks/backlog/099-transcription-queue-explicit.md
**Summary:** Three tasks from deep-capture session refining drawer UX: (097) Enter-to-confirm behavior for name and speaker text fields, (098) full pending transcription drawer with playback/scrubbing and explicit queue button, (099) explicit transcription queuing replacing auto-enqueue on click.

---

## 2026-04-02 -- Task Completed: 096 - Streams Tab -- Video Link Transcription

**Type:** Task Completion
**Task:** 096 - Streams Tab -- Video Link Transcription
**Summary:** Implemented Streams tab with StreamTranscript model, StreamStorageService (JSON persistence), StreamTranscriptionService (yt-dlp/gallery-dl captions with Parakeet ASR fallback), and StreamsPage UI (textarea input, progress bar, transcript cards with copy/delete). Build succeeds, all 32 tests pass.
**Files changed:** 11 files

---

## 2026-04-02 -- Task Started: 096 - Streams Tab -- Video Link Transcription

**Type:** Task Start
**Task:** 096 - Streams Tab -- Video Link Transcription
**Milestone:** M5 (Streams / Web Media Transcription)

---

## 2026-04-02 -- Idea Captured: Streams Tab (Video Link Transcription)

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/backlog/096-streams-tab-video-transcription.md
**Summary:** New "Streams" sidebar tab where the user pastes YouTube/Instagram URLs into a textarea and gets individual transcriptions per link. Uses yt-dlp and gallery-dl with a caption-first, audio-fallback strategy. Transcriptions persist to disk, are sorted by date, and are designed for easy copy-paste into Obsidian.

---

## 2026-04-01 15:17 -- Task Completed: 095 - Unified Recording & Transcript Drawer

**Type:** Task Completion
**Task:** 095 - Unified Recording & Transcript Drawer
**Summary:** Unified the active-recording and transcript drawers into a single live-transitioning drawer. Fixed input bug caused by z-order overlap, replaced +/- speaker counter with add/remove name list, and added live state transition when transcription completes.
**Files changed:** 3 files

---

## 2026-04-01 15:10 -- Task Started: 095 - Unified Recording & Transcript Drawer

**Type:** Task Start
**Task:** 095 - Unified Recording & Transcript Drawer
**Milestone:** --

---

## 2026-04-01 15:00 -- Idea Captured: Unified Recording & Transcript Drawer

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/095-unified-recording-drawer.md
**Summary:** Unify the active-recording drawer and transcript drawer into a single live-transitioning drawer. Fix the current bug where the active-recording drawer doesn't accept input. Replace speaker counter with add/remove name list. Drawer stays open and live-updates when transcription completes.

---

## 2026-04-01 14:00 -- Research: TTS Voice Cloning Models 2026

**Type:** Research
**Topic:** State of the art TTS models with voice cloning (April 2026), prompted by Mistral's Voxtral release
**File:** research/tts-voice-cloning-models-2026.md
**Key findings:**
- Voxtral TTS (Mistral, 4B params) has excellent voice cloning + German, but CC BY-NC license blocks commercial use and no ONNX/sherpa-onnx support
- Chatterbox Turbo (Resemble AI, 350M, MIT, 23 langs incl. German, ONNX available) is the best upgrade candidate
- Qwen3-TTS 0.6B (Apache 2.0, 10 langs incl. German, community ONNX) is a strong runner-up
- ZipVoice (k2-fsa, 123M) already has native sherpa-onnx support with voice cloning but no German
- Pocket TTS remains best integrated option; Kyutai confirmed German is planned but no timeline

---

## 2026-03-31 -- Task Completed: 094 - Delete Audio Keep Transcript

**Type:** Task Completion
**Task:** 094 - Delete Audio Files While Keeping Transcript
**Summary:** Added file size display (MB) and red DELETE AUDIO button to PlaybackPanel. Button shows confirmation dialog, deletes WAV files, clears audioFilePath in transcript JSON, and hides PlaybackPanel. Disabled when no transcription segments exist.
**Files changed:** 2 files

---

## 2026-03-31 -- Task Completed: 092 - UI Quality-of-Life Improvements

**Type:** Task Completion
**Task:** 092 - UI Quality-of-Life Improvements
**Summary:** Fixed template descriptions to display as single lines using a new SingleLineTextConverter, and imported audio files now show original filename as pending title instead of generic "Call {date}".
**Files changed:** 3 files

---

## 2026-03-31 -- Task Completed: 093 - Collapsed Group Speaker Names

**Type:** Task Completion
**Task:** 093 - Show Distinct Speaker Names in Collapsed Date Groups
**Summary:** Added SpeakersSummary property to TranscriptGroupViewModel and bound it to the group header with inverse visibility on IsExpanded, showing distinct sorted remote speaker names when collapsed.
**Files changed:** 2 files

---

## 2026-03-31 -- Batch Started: [092, 093, 094]

**Type:** Batch Start
**Tasks:** 092 - UI Quality-of-Life Improvements, 093 - Collapsed Group Speaker Names, 094 - Delete Audio Keep Transcript
**Mode:** Parallel (batch of 3)

---

## 2026-03-31 -- Idea Captured: Delete Audio Keep Transcript

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/094-delete-audio-keep-transcript.md
**Summary:** Add ability to delete WAV files from a recording while preserving the full transcript. Shows file size in MB next to playback duration, adds a red delete-audio button (disabled until transcription exists), and hides the playback panel after audio removal. Bottom delete button remains unchanged.

---

## 2026-03-31 -- Idea Captured: Collapsed Group Speaker Names

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/093-collapsed-group-speaker-names.md
**Summary:** Show distinct remote speaker names in collapsed date group headers; hide them when expanded since individual recordings already show speakers.

---

## 2026-03-31 18:00 -- Research: WAV-to-MP3 Before Transcription

**Type:** Research
**Topic:** Feasibility of converting WAV recordings to MP3 before transcription and diarization
**File:** research/wav-to-mp3-before-transcription.md
**Key findings:**
- ASR accuracy unaffected at 64+ kbps (Whisper tested down to 32 kbps, Parakeet decodes to PCM internally)
- Speaker diarization safe at 128 kbps mono; MP3 removes subtle spectral features below that
- 4x storage reduction: dual-stream 1-hour call drops from ~460 MB to ~115 MB
- NAudio.Lame NuGet integrates directly with existing NAudio stack, few lines of code
- AudioFileDecoder already supports MP3 — pipeline changes are minimal

---

## 2026-03-31 15:30 -- Research: Filler Words & Custom Vocabulary

**Type:** Research
**Topic:** Filler word filtering from ASR output and custom vocabulary/proper noun correction
**File:** research/filler-words-and-custom-vocabulary.md
**Key findings:**
- Parakeet TDT / sherpa-onnx have no built-in filler word filtering — regex post-processing is the standard approach
- Sherpa-onnx hotword boosting exists but is incompatible with Parakeet TDT's stateful decoder (track issues #2541, #2753)
- Practical path: replacement dictionary (like MacWhisper's Global Replace) + optional Double Metaphone phonetic matching
- LLM-based correction tested by Vosk team — hallucinated in 25% of cases, worst at proper nouns specifically
- Dictation should filter fillers by default; transcription should keep them with optional cleanup

---

## 2026-03-31 14:00 -- Research: Pre-Computed Voice Embeddings

**Type:** Research
**Topic:** How to get pre-trained/pre-computed voice tensor models for faster TTS, matching original Python Pocket TTS behavior
**File:** research/pre-computed-voice-embeddings.md
**Key findings:**
- Pocket TTS Python has `export-voice` CLI and `export_model_state()` API to save KV cache as `.safetensors` — loads instantly vs slow Mimi encoder
- sherpa-onnx does NOT support loading pre-computed embeddings — only accepts raw WAV audio
- Recommended path: (1) warm up all voices at startup now, (2) pre-export voices with Python CLI, (3) submit PR to sherpa-onnx to accept safetensors

---

## 2026-03-27 -- Idea Captured: UI Quality-of-Life Improvements

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/092-ui-quality-of-life-improvements.md
**Summary:** Two UI fixes: (1) truncate template descriptions to single line with ellipsis in list view, (2) show original filename for imported files in pending state instead of generic "Call {date}" title.

---

## 2026-03-27 -- Task Completed: 091 - Reorder and resize conversation list columns

**Type:** Task Completion
**Task:** 091 - Reorder and resize conversation list columns
**Summary:** Reordered conversation list columns from Title/Time/Speakers to Title/Speakers/Time with auto-sized widths and right-aligned Time column.
**Files changed:** 2 files

---

## 2026-03-27 -- Task Started: 091 - Reorder and resize conversation list columns

**Type:** Task Start
**Task:** 091 - Reorder and resize conversation list columns
**Milestone:** --

---

## 2026-03-27 -- Idea Promoted: Reorder conversations columns

**Type:** Idea Promotion
**From:** ideas/2026-03-27-reorder-conversations-columns.md
**To:** tasks/todo/091-reorder-conversations-columns.md
**Summary:** Reorder conversation list columns to Title → Speakers → Time, auto-size title, right-align time.

---

## 2026-03-27 -- Idea Captured: Reorder conversations columns

**Type:** Idea Capture
**Mode:** Quick
**Filed to:** ideas/2026-03-27-reorder-conversations-columns.md

---

## 2026-03-27 15:30 -- Research: MacWhisper Growth Playbook

**Type:** Research
**Topic:** How MacWhisper (Jordi Bruin) reached 250K+ downloads with zero paid advertising
**File:** research/macwhisper-growth-playbook.md
**Key findings:**
- 250K+ downloads in ~17 months (Jan 2023 - May 2024), zero paid ads
- Primary channels: Twitter/X audience (build in public), Apple App Store featuring (7 apps featured), organic press (iMore App of Year), Product Hunt (2 launches, 4.86/5)
- Core strategy: perfect timing on Whisper hype + 2-2-2 shipping method (2h prototype, 2d polish, 2w launch) + each version update treated as mini-launch event
- Directly applicable to WhisperHeim: Windows is underserved, "local AI" narrative is current, same freemium + one-time purchase model works

---

## 2026-03-27 14:00 -- Research: Auto-Update & Distribution

**Type:** Research
**Topic:** Auto-update and distribution strategies for Windows desktop .NET WPF apps
**File:** research/auto-update-and-distribution.md
**Key findings:**
- Velopack is the best update framework: free, open-source, Rust-based, delta updates, first-class WPF support, replaces unmaintained Squirrel.Windows
- MSIX is a poor fit — containerization restricts WASAPI loopback, global hotkeys, and SendInput access
- Code signing: Microsoft Trusted Signing excludes German individual developers; OV certs cost $200-500/yr with 2-8 week SmartScreen warmup; EV certs require a registered business
- Recommended path: ship unsigned initially, sign after UG registration with EV cert
- Don't trim WPF apps (.NET 9 has known assembly-stripping bugs); use self-contained + ReadyToRun instead
- Host updates on GitHub Releases (free) during open-source/beta phase

---

## 2026-03-26 -- Task Completed: 069 - Transcript Analysis with Local LLM

**Type:** Task Completion
**Task:** 069 - Transcript Analysis with Local LLM
**Summary:** Added OllamaSharp integration with streaming analysis, model auto-detection, 4 built-in prompt templates (Action Items, Key Decisions, Ideas, Meeting Summary), Ollama settings section on GeneralPage, and Analyze button with streaming results panel on TranscriptsPage.
**Files changed:** 10 files

---

## 2026-03-26 -- Task Started: 069 - Transcript Analysis with Local LLM

**Type:** Task Start
**Task:** 069 - Transcript Analysis with Local LLM
**Milestone:** Milestone 2 (Audio Capture + Call Transcription)

---

## 2026-03-26 -- Task Completed: 089 - Fix speaker dropdown selection

**Type:** Task Completion
**Task:** 089 - Fix speaker dropdown selection not applying
**Summary:** Fixed LostFocus/SelectionChanged race condition by deferring LostFocus commit via Dispatcher.BeginInvoke and adding a _speakerSelectionCommitted flag to prevent duplicate commits.
**Files changed:** 1 file

---

## 2026-03-26 -- Task Completed: 090 - Open in Player icon

**Type:** Task Completion
**Task:** 090 - Add proper play icon to Open in Player button
**Summary:** Replaced raw Unicode glyph with SymbolIcon Open24 in a StackPanel, matching the pattern of other buttons.
**Files changed:** 1 file

---

## 2026-03-26 -- Task Completed: 088 - System Templates — WhisperHeim Group

**Type:** Task Completion
**Task:** 088 - System Templates — WhisperHeim Group with "Repeat" Command
**Summary:** Added SystemTemplate model, WhisperHeim group with reduced-contrast non-interactive UI, fuzzy matching integration with precedence over user templates, and "Repeat" command that re-types last normal dictation.
**Files changed:** 6 files

---

## 2026-03-26 -- Task Completed: 087 - Branding Header as Sidebar Toggle

**Type:** Task Completion
**Task:** 087 - Branding Header as Sidebar Toggle
**Summary:** Removed SidebarToggleButton, wired BrandingHeader as collapse/expand toggle via MouseLeftButtonDown with Hand cursor. Cleaned up chevron icon swapping and tooltip logic.
**Files changed:** 2 files

---

## 2026-03-26 14:00 -- Idea Captured: Fix speaker dropdown selection

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/089-fix-speaker-dropdown-selection.md
**Summary:** Speaker name ComboBox selection reverts to original label due to LostFocus/SelectionChanged race condition

---

## 2026-03-26 14:00 -- Idea Captured: Open in Player icon

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/090-open-in-player-icon.md
**Summary:** Replace raw Unicode glyph with proper Wpf.Ui SymbolIcon on the "Open in Player" button

---

## 2026-03-26 -- Batch Started: [087, 088]

**Type:** Batch Start
**Tasks:** 087 - Branding Header as Sidebar Toggle, 088 - System Templates — WhisperHeim Group
**Mode:** Parallel (batch of 2)

---

## 2026-03-26 -- Idea Captured: System Templates — WhisperHeim Group

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/088-system-templates-whisperhiem-group.md
**Summary:** Add an immutable "WhisperHeim" group at the bottom of the templates list for built-in command templates. First command: "Repeat" re-types the last normally-dictated text. System templates are non-editable, non-deletable, non-draggable, displayed with reduced contrast, and not clickable.

---

## 2026-03-26 -- Idea Captured: Branding Header as Sidebar Toggle

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/087-branding-header-sidebar-toggle.md
**Summary:** Remove the dedicated sidebar collapse chevron button and make the branding header (microphone logo + title) the click target for toggling sidebar collapse/expand. Pointer cursor is the only hover affordance.

---

## 2026-03-26 -- Task Completed: 086 - Transcripts column redesign

**Type:** Task Completion
**Task:** 086 - Transcripts list column redesign
**Summary:** Renamed Duration→Time (HH:mm – Xh Ym format), Date→Speakers (comma-separated remote speaker names). Default sort now by start time. Search includes speaker names. Build and all 32 tests pass.
**Files changed:** 2 files

---

## 2026-03-26 -- Task Started: 086 - Transcripts column redesign

**Type:** Task Start
**Task:** 086 - Transcripts list column redesign
**Milestone:** --

---

## 2026-03-26 -- Idea Captured: Transcripts column redesign

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/086-transcripts-column-redesign.md
**Summary:** Redesign transcripts list columns: rename Duration→Time (show start time + compact duration), rename Date→Speakers (show remote speaker names). Keep date grouping, sort by start time.

---

## 2026-03-26 -- Research: Legal Risks of Commercial Release

**Type:** Research
**Topic:** Legal liability for recording capability, voice cloning/TTS, and German/EU business requirements
**File:** research/legal-risks-commercial-release.md
**Key findings:**
- Recording: developer NOT liable under Sony Betamax doctrine — user bears responsibility. Add consent disclaimers.
- Voice cloning: EU AI Act requires machine-readable watermarking by August 2, 2026. German court (LG Berlin 2025) ruled AI voice clones violate personality rights. Tennessee ELVIS Act targets tool makers.
- Form UG (haftungsbeschraenkt) before release — Product Liability Directive 2024/2853 makes software subject to strict liability from December 2026, "as-is" disclaimers void in Germany
- Get IT-Haftpflichtversicherung (~300-600 EUR/year), use merchant of record for EU VAT

---

## 2026-03-26 14:20 -- Task Completed: 085 - Template Grouping with Collapsible Sections

**Type:** Task Completion
**Task:** 085 - Template Grouping with Collapsible Sections
**Summary:** Implemented template grouping with collapsible sections, drag-and-drop reordering/moving, group CRUD, expand/collapse-all on both Templates and Transcripts pages, and migration for existing ungrouped templates. All 32 tests pass.
**Files changed:** 9 files

---

## 2026-03-26 14:10 -- Task Started: 085 - Template Grouping with Collapsible Sections

**Type:** Task Start
**Task:** 085 - Template Grouping with Collapsible Sections
**Milestone:** N/A

---

## 2026-03-26 14:00 -- Idea Captured: Template Grouping

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/085-template-grouping.md
**Summary:** User-defined collapsible template groups with drag-and-drop reordering, inline renaming, per-group add buttons, and persistent collapse state. Also includes collapse/expand-all toggle for both Templates and Transcripts pages.

---

## 2026-03-26 -- Research: Monetization, Licensing & Marketing

**Type:** Research
**Topic:** Commercial viability — dependency licenses, monetization models, go-to-market strategy
**File:** research/monetization-and-marketing.md
**Key findings:**
- All dependencies (Parakeet CC-BY-4.0, sherpa-onnx Apache-2.0, rest MIT) allow commercial use — no blockers
- Freemium + one-time purchase (Open Core) is the best-fit model; MacWhisper sold ~300K copies at $35-79 with this exact approach
- Privacy-as-architecture ("your data physically cannot leave your machine") is the #1 differentiator vs cloud competitors
- NLNet/NGI grants (EUR 5K-50K) are an excellent fit for the project's privacy + accessibility profile
- Launch sequence: pre-launch email list → Show HN → Product Hunt → Reddit (r/selfhosted, r/privacy) → Microsoft Store

---

## 2026-03-25 23:15 -- Task Completed: 083 - Unify Recordings & File Transcription

**Type:** Task Completion
**Task:** 083 - Unify Recordings & File Transcription
**Summary:** Added Start/Stop Recording and Browse buttons to the Recordings page, implemented file import with move/copy-to-session-dir, produces CallTranscript-compatible JSON, supports re-transcription with diarization. Removed TranscribeFilesPage and Transcriptions nav item. Build passes, 32 tests pass.
**Files changed:** 8 files

---

## 2026-03-25 23:06 -- Task Started: 083 - Unify Recordings & File Transcription

**Type:** Task Start
**Task:** 083 - Unify Recordings & File Transcription
**Milestone:** M3 (Voice Message Transcription)

---

## 2026-03-25 23:05 -- Task Completed: 084 - Sidebar Collapse Icon & Branding Reshuffle

**Type:** Task Completion
**Task:** 084 - Sidebar Collapse Icon & Branding Reshuffle
**Summary:** Replaced sidebar collapse button with a chevron on the right edge, moved About to bottom of nav, replaced Dictation header with About-style branding, unified subtitle text.
**Files changed:** 4 files

---

## 2026-03-25 23:04 -- Task Completed: 082 - Fix Date Column & Sorting

**Type:** Task Completion
**Task:** 082 - Fix Date Column & Sorting
**Summary:** Fixed date parsing for new-format session directories and added clickable column headers with within-group sorting and sort direction indicators.
**Files changed:** 2 files

---

## 2026-03-25 23:00 -- Batch Started: [082, 084]

**Type:** Batch Start
**Tasks:** 082 - Fix Date Column & Sorting, 084 - Sidebar Collapse Icon & Branding Reshuffle
**Mode:** Parallel (batch of 2)

---

## 2026-03-25 22:45 -- Idea Captured: Sidebar Collapse Icon & Branding Reshuffle

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/084-sidebar-collapse-icon-and-branding-reshuffle.md
**Summary:** Replace sidebar collapse button with a chevron on the right edge, move About to bottom of nav, replace Dictation page header with About-style branding (logo + title + version), unify subtitle text across both pages using the Dictation page's version.

---

## 2026-03-25 22:30 -- Idea Captured: Unify Recordings & File Transcription

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/083-unify-recordings-and-file-transcription.md
**Summary:** Merge the Transcriptions page into the Recordings page. Add Start/Stop Recording and Browse buttons. Imported files get moved into recordings/ and transcribed without diarization by default. Re-transcribe in drawer triggers diarization when multiple speakers defined. Remove TranscribeFilesPage and dead code afterward.

---

## 2026-03-25 21:00 -- Idea Captured: Fix Date Column & Add Column Sorting

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/082-fix-date-column-and-sorting.md
**Summary:** Fix the recordings date column showing "transcript" instead of actual dates (caused by new session-directory format not being parsed), and add click-to-sort on column headers with toggle asc/desc within groups.

---

## 2026-03-25 19:15 -- Task Completed: 081 - Fix Library Voices Combo Box

**Type:** Task Completion
**Task:** 081 - Fix Library Voices Combo Box
**Summary:** Replaced hardcoded CustomVoicesDir with DataPathService.VoicesPath and fixed LibraryVoice_Click ID comparison to use "custom:{name}" format.
**Files changed:** 4 files

---

## 2026-03-25 19:15 -- Task Completed: 080 - Drawer No Overlay Crossfade

**Type:** Task Completion
**Task:** 080 - Drawer No Overlay Crossfade
**Summary:** Removed dark overlay from drawer, added crossfade animation when switching recordings while drawer is open, close only via close button or Escape.
**Files changed:** 3 files

---

## 2026-03-25 19:00 -- Batch Started: [080, 081]

**Type:** Batch Start
**Tasks:** 080 - Drawer No Overlay Crossfade, 081 - Fix Library Voices Combo Box
**Mode:** Parallel (batch of 2)

---

## 2026-03-25 18:45 -- Idea Captured: Fix Library Voices Not Showing in TTS Combo Box

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/081-fix-library-voices-combo-box.md
**Summary:** Path mismatch bug -- custom data path causes TTS service to scan a different voices directory than where the page saves/lists cloned voices. Secondary bug: library voice card click uses wrong ID format for combo box lookup.

---

## 2026-03-25 18:30 -- Idea Captured: Drawer -- Remove Overlay, Crossfade Between Recordings

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/080-drawer-no-overlay-crossfade.md
**Summary:** Remove the dark overlay behind the detail drawer (drop shadow is sufficient), enable clicking other recordings to crossfade drawer content in-place, close only via close button or Escape key.

---

## 2026-03-25 16:45 -- Task Completed: 079 - Fix Speaker Assignment UI

**Type:** Task Completion
**Task:** 079 - Fix Speaker Assignment UI
**Summary:** Fixed ComboBox click event bubbling to audio playback handler. Added per-segment speaker reassignment with "Apply to all" bulk update prompt. Speaker name header editing propagates renames to all matching segments.
**Files changed:** 2 files

---

## 2026-03-25 16:30 -- Task Completed: 078 - Fix Temporal Ordering (clock drift)

**Type:** Task Completion
**Task:** 078 - Fix Temporal Ordering -- Clock Drift Correction
**Summary:** Linear clock drift correction scales loopback segment timestamps by micDuration/loopbackDuration before merging, fixing out-of-order segments from WASAPI hardware clock divergence. Drift logged for diagnostics.
**Files changed:** 1 file

---

## 2026-03-25 16:30 -- Task Completed: 076 - Active Recording Card + Auto-Transcribe

**Type:** Task Completion
**Task:** 076 - Active Recording Card + Auto-Transcribe on Stop
**Summary:** Active recording card at top of Transcripts page with pulsing red indicator, live duration counter, and drawer for editing title/speaker count/speaker names. Auto-enqueues into TranscriptionQueueService on recording stop with pre-filled metadata.
**Files changed:** 3 files

---

## 2026-03-25 16:10 -- Batch Started: [076, 078]

**Type:** Batch Start
**Tasks:** 076 - Active Recording Card + Auto-Transcribe, 078 - Fix Temporal Ordering (clock drift)
**Mode:** Parallel (batch of 2)

---

## 2026-03-25 16:00 -- Task Completed: 077 - Fix Diarization (VAD mic + constrained loopback)

**Type:** Task Completion
**Task:** 077 - Fix Diarization -- VAD-Only Mic + Constrained Loopback
**Summary:** VAD-only mic stream processing replaces fixed 120s chunks. Loopback diarization constrained with NumClusters from speaker count. Threshold raised to 0.80. Cross-chunk speaker ID consistency for group calls. Out-of-process worker updated.
**Files changed:** 5 files

---

## 2026-03-25 16:00 -- Task Completed: 075 - Transcription Queue Service + Bottom Bar UI

**Type:** Task Completion
**Task:** 075 - Transcription Queue Service + Bottom Bar UI
**Summary:** Replaced modal TranscriptionProgressDialog and TranscriptionBusyService with FIFO TranscriptionQueueService and persistent TranscriptionBottomBar. Sequential background processing with per-item stage tracking, cancel/remove/retry, collapsible bar across all pages.
**Files changed:** 9 files

---

## 2026-03-25 15:30 -- Batch Started: [075, 077]

**Type:** Batch Start
**Tasks:** 075 - Transcription Queue Service + Bottom Bar UI, 077 - Fix Diarization (VAD mic + constrained loopback)
**Mode:** Parallel (batch of 2)

---

## 2026-03-25 15:00 -- Idea Captured: Transcription Engine Overhaul

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/075 through 079
**Summary:** Overhaul the transcription engine into 5 tasks: (075) transcription queue with bottom bar UI, (076) active recording card with auto-transcribe on stop, (077) fix diarization with VAD-only mic + constrained loopback, (078) fix temporal ordering via clock drift correction, (079) fix speaker assignment UI combo box bug + per-segment reassignment. All filed to todo, all under M2.

---

## 2026-03-25 14:00 -- Research: Transcription Engine Overhaul

**Type:** Research
**Topic:** Diarization accuracy, dual-stream merging, long-recording stability, and transcription queue architecture
**File:** research/transcription-engine-overhaul.md
**Key findings:**
- For dual-stream recordings, replace full diarization with VAD-only per stream (mic = "You", loopback = "Remote Speaker") -- eliminates over-segmentation, fixes ordering, reduces memory usage
- Over-segmentation caused by low clustering threshold (0.5) and independent per-chunk speaker IDs; fix by setting NumClusters explicitly or raising threshold to 0.75-0.85
- Clock drift between WASAPI mic and loopback streams (~1s per 16min) causes temporal ordering errors; fix with linear drift correction based on WAV duration comparison
- Replace modal TranscriptionProgressDialog with persistent bottom-bar queue UI; auto-enqueue recordings on stop and files on import

---

## 2026-03-24 -- Task Completed: 071 - Notion-Style List View with Detail Drawer

**Type:** Task Completion
**Task:** 071 - Notion-Style List View with Detail Drawer
**Summary:** Replaced side-by-side card layouts on TemplatesPage and TranscriptsPage with compact table views and right-side overlay detail drawers. Transcripts grouped by date with collapsible sections. Delete actions moved inside drawers. 32/32 tests pass.
**Files changed:** 5 files

---

## 2026-03-24 -- Task Started: 071 - Notion-Style List View with Detail Drawer

**Type:** Task Start
**Task:** 071 - Notion-Style List View with Detail Drawer
**Milestone:** --

---

## 2026-03-24 -- Task Completed: 073 - Speaker Name List and Manual Transcribe

**Type:** Task Completion
**Task:** 073 - Speaker Name List and Manual Transcribe
**Summary:** Removed auto-transcription, added speaker name list with add/remove/edit, numSpeakers hint for loopback diarization, skipped mic diarization, DefaultSpeakerName setting, editable ComboBox for speaker name selection in transcript viewer, and re-transcribe button. Build clean, 32/32 tests passed.
**Files changed:** 10 files

---

## 2026-03-24 -- Task Completed: 074 - Transcription Engine Busy Guard

**Type:** Task Completion
**Task:** 074 - Transcription Engine Busy Guard
**Summary:** Created centralized TranscriptionBusyService with TryAcquire/Release pattern, integrated into all transcription entry points with disabled buttons and "Engine busy" overlay when engine is in use.
**Files changed:** 6 files

---

## 2026-03-24 -- Task Completed: 072 - Fix Recording Delete Stale UI

**Type:** Task Completion
**Task:** 072 - Fix Recording Delete Stale UI
**Summary:** Fixed stale UI after recording deletion by moving LoadTranscriptList() outside try block and adding Loaded event handler to refresh list on page navigation.
**Files changed:** 2 files

---

## 2026-03-24 -- Batch Started: [072, 073, 074]

**Type:** Batch Start
**Tasks:** 072 - Fix Recording Delete Stale UI, 073 - Speaker Name List and Manual Transcribe, 074 - Transcription Engine Busy Guard
**Mode:** Parallel (batch of 3)

---

## 2026-03-24 -- Idea Captured: Transcription Engine Busy Guard

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/074-transcription-engine-busy-guard.md
**Summary:** Bug fix — concurrent transcriptions silently fail (stuck "Transcribing" forever). Add engine busy detection, gray out transcribe buttons with "Engine busy" label, auto-enable when engine frees up. No parallel transcriptions allowed.

---

## 2026-03-24 -- Idea Captured: Speaker Name List and Manual Transcription

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/073-speaker-list-and-manual-transcribe.md
**Summary:** Rework call recording flow: no auto-transcription, user defines remote speaker names, count hint fixes diarization over-segmentation, skip mic diarization, name dropdown in transcript viewer, re-transcribe button for re-processing.

---

## 2026-03-24 -- Idea Captured: Fix Recording Delete Stale UI

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/072-fix-recording-delete-stale-ui.md
**Summary:** Bug fix — deleting a recording removes files on disk but the list item remains in the UI, even after tab navigation. State/cache not being invalidated after delete.

---

## 2026-03-24 -- Idea Captured: Notion-Style List View with Detail Drawer

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/071-notion-style-list-and-drawer.md
**Summary:** Replace side-by-side card layout on Templates and Recordings pages with compact single-row list view (with grouping) and a right-side overlay detail drawer. Improves information density, scannability, and organization.

---

## 2026-03-23 -- Task Completed: 070 - Fix Pill Overlay Visualization

**Type:** Task Completion
**Task:** 070 - Fix Pill Overlay Visualization
**Summary:** Fixed two bugs: bars invisible because WPF Canvas reported 0x0 inside Border (wrapped in Grid to propagate size), and border always blue because Idle/Speaking both used BlueBorderColor (Idle now uses grey border/bars, Speaking uses blue/orange).
**Files changed:** 2 files

---

## 2026-03-23 -- Task Started: 070 - Fix Pill Overlay Visualization

**Type:** Task Start
**Task:** 070 - Fix Pill Overlay Visualization
**Milestone:** Bug Fix

---

## 2026-03-23 -- Idea Captured: Fix Pill Overlay Visualization

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/070-fix-pill-overlay-visualization.md
**Summary:** Pill overlay shows no bars and border is always blue. Two bugs: Canvas likely has 0 size so bars never render, and Idle/Speaking states both use blue instead of grey for idle.

---

## 2026-03-23 -- Task Completed: 069 - Fix Start Minimized Setting

**Type:** Task Completion
**Task:** 069 - Fix Start Minimized Setting Ignored on Launch
**Summary:** Replaced --minimized CLI flag check with StartMinimized setting read, removed --minimized from registry command. Build clean.
**Files changed:** 2 files

---

## 2026-03-23 -- Task Started: 069 - Fix Start Minimized Setting

**Type:** Task Start
**Task:** 069 - Fix Start Minimized Setting Ignored on Launch
**Milestone:** Bug Fix

---

## 2026-03-23 -- Idea Captured: Fix Start Minimized Setting Ignored

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/069-fix-start-minimized-setting.md
**Summary:** StartMinimized setting is ignored because App.xaml.cs checks only the --minimized CLI flag instead of the setting. Fix: use the setting, drop the flag.

---

## 2026-03-23 -- Task Completed: 068 - Transcripts Export Cleanup

**Type:** Task Completion
**Task:** 068 - Transcripts Export Cleanup
**Summary:** Removed TXT download button, changed Copy to use FormatAsMarkdown(), added transient "Copied!" indicator, removed ExportText_Click handler.
**Files changed:** 2 files

---

## 2026-03-23 -- Task Completed: 068 - Pill Waveform Overlay

**Type:** Task Completion
**Task:** 068 - Pill Waveform Overlay
**Summary:** Replaced circular overlay with pill-shaped frequency bar visualizer (18 bars, RMS-driven). Added global mouse hook for click-position tracking. Applied brand colors. Removed old position/size settings.
**Files changed:** 4 files

---

## 2026-03-23 -- Task Completed: 067 - Dictation Page Responsive Layout

**Type:** Task Completion
**Task:** 067 - Dictation Page Responsive Layout
**Summary:** Aligned bento grid width with audio input card, added responsive stacking at 640px breakpoint, made warning card 50/50 in wide mode, swapped hotkey row content order.
**Files changed:** 2 files

---

## 2026-03-23 -- Batch Started: [067, 068-pill, 068-export]

**Type:** Batch Start
**Tasks:** 067 - Dictation Page Responsive Layout, 068 - Pill Waveform Overlay, 068 - Transcripts Export Cleanup
**Mode:** Parallel (batch of 3)

---

## 2026-03-23 -- Idea Captured: Transcript Analysis with Local LLM

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/backlog/069-transcript-analysis-with-local-llm.md
**Summary:** AI-powered transcript analysis using Ollama + local LLM (Qwen 2.5 14B). User-defined prompt templates (action items, decisions, ideas, etc.) run against any recording. Streams results in real-time. Fully local, zero cost.

---

## 2026-03-23 14:30 -- Idea Captured: Pill-Shaped Waveform Overlay

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/068-pill-waveform-overlay.md
**Summary:** Replace circular pulsing mic overlay with a horizontal pill containing animated frequency bars (orange on blue border, brand colors). Pill anchors at last global mouse click position extending rightward. Bars simulate frequency response driven by RMS amplitude with per-bar random variation. Removes old overlay position settings.

---

## 2026-03-23 -- Research: Transcript Analysis with LLM

**Type:** Research
**Topic:** How to analyze transcripts with AI using an existing Anthropic subscription, or alternatives
**File:** research/transcript-analysis-with-llm.md
**Key findings:**
- Claude subscription cannot be used programmatically — Anthropic blocked OAuth in third-party apps since Jan 2026, API is separately billed
- Best alternative: Ollama + local LLM (Qwen 2.5 14B) — zero cost, fully local, aligns with WhisperHeim vision
- OllamaSharp NuGet provides mature .NET integration via IChatClient (Microsoft.Extensions.AI)
- 1.5h transcripts (~18K tokens) fit easily in Qwen 2.5's 128K context window

---

## 2026-03-23 -- Idea Captured: Transcripts Page Export Cleanup

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/068-transcripts-export-cleanup.md
**Summary:** Remove TXT download button from transcripts page, keep only MD and JSON. Change Copy button to copy Markdown format instead of plain text, and add a brief "Copied!" tooltip for feedback.

---

## 2026-03-23 14:00 -- Idea Captured: Dictation Page Responsive Layout

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/067-dictation-page-responsive-layout.md
**Summary:** Fix card width alignment on dictation page, add responsive stacking (row → column) for narrow windows, make warning card responsive 50/50 → stacked, and swap hotkey row content order (pills left, label right).

---

## 2026-03-22 -- Task Completed: 063 - Configurable Data Path

**Type:** Task Completion
**Task:** 063 - Configurable Config/Data Path for Cloud Sync
**Summary:** Implemented bootstrap config in %APPDATA%, DataPathService for path resolution, folder picker in Settings UI, per-session recording folders, machine-local settings split, and migration from old flat structure. 32 tests pass.
**Files changed:** 13 files

---

## 2026-03-22 -- Task Completed: 065 - About Page with Profile

**Type:** Task Completion
**Task:** 065 - About Page with Profile, Contact Links & Ko-fi
**Summary:** Added profile section with photo and bio, contact links (website, Bluesky, LinkedIn), Ko-fi support button, and GitHub link. Wired page into sidebar navigation with collapse support.
**Files changed:** 7 files

---

## 2026-03-22 -- Task Completed: 066 - Template Delete Hover Trash

**Type:** Task Completion
**Task:** 066 - Template Delete with Hover Trash Icon & Confirmation Dialog
**Summary:** Added hover trash icon to template list items with fade animation (matching TranscriptsPage pattern), wired to DeleteConfirmationDialog, removed old Delete Template button from detail panel.
**Files changed:** 2 files

---

## 2026-03-22 -- Batch Started: [063, 065, 066]

**Type:** Batch Start
**Tasks:** 063 - Configurable Data Path, 065 - About Page with Profile, 066 - Template Delete Hover Trash
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 -- Idea Captured: Template Delete with Hover Trash Icon

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/066-template-delete-hover-trash.md
**Summary:** Replace the detail panel "Delete Template" button with a hover trash icon on each template card, using the existing DeleteConfirmationDialog. Matches the recordings deletion pattern.

---

## 2026-03-22 -- Idea Captured: About Page with Profile & Ko-fi

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/065-about-page-with-profile.md
**Summary:** Add personal profile, contact links, Ko-fi support button, and GitHub link to the About page (modeled after VocalFold). Wire the existing AboutPage into the sidebar navigation.

---

## 2026-03-22 -- Task Completed: 064 - Fix Opaque Backgrounds and Delete Dialog

**Type:** Task Completion
**Task:** 064 - Fix Opaque Backgrounds and Delete Dialog
**Summary:** Removed opaque ApplicationBackgroundBrush from TranscriptsPage and TemplatesPage root grids so mica shows through. Replaced glass effect on DeleteConfirmationDialog with solid theme-aware CardBackgroundFillColorDefaultBrush.
**Files changed:** 4 files

---

## 2026-03-22 -- Task Completed: 062 - TTS Page Layout Cleanup

**Type:** Task Completion
**Task:** 062 - TTS Page Layout Cleanup
**Summary:** Removed "INPUT WORKSPACE" label and relocated voice/speaker selector below play/stop buttons, left-aligned. Build verified clean.
**Files changed:** 2 files

---

## 2026-03-22 -- Task Completed: 061 - Update Dictation Hotkey Labels

**Type:** Task Completion
**Task:** 061 - Update Dictation Hotkey Labels
**Summary:** Labels already matched acceptance criteria — no code changes needed.
**Files changed:** 1 file

---

## 2026-03-22 -- Batch Started: [061, 062, 064]

**Type:** Batch Start
**Tasks:** 061 - Update Dictation Hotkey Labels, 062 - TTS Page Layout Cleanup, 064 - Fix Opaque Backgrounds and Delete Dialog
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 -- Idea Captured: Fix Opaque Backgrounds and Delete Dialog

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/063-fix-opaque-backgrounds-and-dialog.md
**Summary:** Fix recordings & templates pages having wrong opaque background (should match other screens). Replace ugly glass/transparency effect on delete confirmation dialog with solid theme-aware surface color.

---

## 2026-03-22 -- Idea Promoted: Configurable Config/Data Path for Cloud Sync

**Type:** Idea Promotion
**From:** ideas/2026-03-22-configurable-config-path.md
**To:** tasks/todo/063-configurable-data-path.md
**Summary:** Configurable data path with bootstrap config, per-session recording folders, cloud sync support via Google Drive.

---

## 2026-03-22 -- Idea Refined: Configurable Config/Data Path for Cloud Sync

**Type:** Idea Refinement
**Idea:** ideas/2026-03-22-configurable-config-path.md
**Status:** Ready
**Summary:** Resolved all open questions: defined sync vs. local split, chose last-write-wins for conflicts, lightweight path validation. Redesigned recordings as first-class data with per-session folders. Idea is ready to promote.

---

## 2026-03-22 -- Idea Captured: TTS Page Layout Cleanup

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/062-tts-page-layout-cleanup.md
**Summary:** Remove "INPUT WORKSPACE" label from TTS card header; relocate voice selector ComboBox to below play/stop buttons, left-aligned.

---

## 2026-03-22 -- Idea Captured: Update Dictation Page Hotkey Labels

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/061-update-dictation-hotkey-labels.md
**Summary:** Fix hotkey labels on dictation page: rename Start/Stop to Dictation, correct Read Aloud shortcut from Shift+Win+A to Ctrl+Win+^, keep Call Recording as-is.

---

## 2026-03-22 -- Task Completed: 060 - Show Full Logo in Collapsed Sidebar

**Type:** Task Completion
**Task:** 060 - Show Full Logo in Collapsed Sidebar
**Summary:** Increased SidebarCollapsedWidth from 60px to 64px and adjusted logo margin to 0 when collapsed, ensuring the 32px logo fits perfectly centered with no clipping.
**Files changed:** 2 files

---

## 2026-03-22 -- Task Started: 060 - Show Full Logo in Collapsed Sidebar

**Type:** Task Start
**Task:** 060 - Show Full Logo in Collapsed Sidebar
**Milestone:** --

---

## 2026-03-22 -- Task Completed: 058 - Layout Fixes and Branding Cleanup

**Type:** Task Completion
**Task:** 058 - Layout Fixes and Branding Cleanup
**Summary:** Fixed transcript list card overflow with ClipToBounds, made Transcripts and TTS pages stretch to fill available width, removed "LOCAL-FIRST AI" subtitle from sidebar.
**Files changed:** 4 files

---

## 2026-03-22 -- Task Started: 058 - Layout Fixes and Branding Cleanup

**Type:** Task Start
**Task:** 058 - Layout Fixes and Branding Cleanup
**Milestone:** --

---

## 2026-03-22 -- Task Completed: 059 - Rework Read-Aloud Hotkey to Navigate to TTS Page

**Type:** Task Completion
**Task:** 059 - Rework Read-Aloud Hotkey to Navigate to TTS Page
**Summary:** Changed hotkey from Shift+Win+Ä to Ctrl+Win+Ä with new flow: captures selected text, brings window to foreground, navigates to TTS page, pastes text. Removed all overlay infrastructure (3 files deleted) and inline TTS logic.
**Files changed:** 7+ files

---

## 2026-03-22 -- Task Completed: 057 - Redesign WhisperHeim Logo

**Type:** Task Completion
**Task:** 057 - Redesign WhisperHeim Logo
**Summary:** Replaced gradient borders with solid blue border + transparent blue-tinted background, replaced SymbolIcon with custom two-tone XAML paths (blue mic head, orange stand), added programmatic window icon generation for taskbar/Alt+Tab.
**Files changed:** 4 files

---

## 2026-03-22 -- Batch Started: [057, 059]

**Type:** Batch Start
**Tasks:** 057 - Redesign WhisperHeim Logo, 059 - Rework Read-Aloud Hotkey to Navigate to TTS Page
**Mode:** Parallel (batch of 2)

---

## 2026-03-22 -- Idea Captured: Make Templates Work

**Type:** Idea Capture
**Mode:** Quick
**Filed to:** ideas/2026-03-22-make-templates-work.md

---

## 2026-03-22 -- Idea Captured: Configurable Config Path for Cloud Sync

**Type:** Idea Capture
**Mode:** Quick
**Filed to:** ideas/2026-03-22-configurable-config-path.md

---

## 2026-03-22 -- Idea Captured: Show Full Logo in Collapsed Sidebar

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/060-collapsed-sidebar-logo-visible.md
**Summary:** Increase collapsed sidebar width from 60px to 64px so the logo isn't clipped. Hide app name when collapsed, show both logo and name when expanded.

---

## 2026-03-22 -- Idea Captured: Rework Read-Aloud Hotkey

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/059-rework-read-aloud-hotkey.md
**Summary:** Change hotkey to Ctrl+Win+Ä, rework flow to capture text → bring window to foreground → navigate to TTS page → paste into input workspace. Remove read-aloud overlay entirely.

---

## 2026-03-22 -- Idea Captured: Layout Fixes and Branding Cleanup

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/058-layout-fixes-and-branding-cleanup.md
**Summary:** Fix transcript card overflow, fix Transcripts and TTS pages not filling available width, remove "LOCAL-FIRST AI" sidebar subtitle.

---

## 2026-03-22 -- Idea Captured: Redesign WhisperHeim Logo

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/057-logo-redesign.md
**Summary:** Redesign logo with subtle blue-tinted transparent background, solid blue border (no gradient), two-tone XAML microphone (blue head, orange stand), and set as taskbar/window icon.

---

## 2026-03-22 17:27 -- Task Completed: 051 - Reduce Templates List Column Width

**Type:** Task Completion
**Task:** 051 - Reduce Templates List Column Width
**Summary:** Reduced templates list column from 300px to 200px with responsive min/max constraints, tightened padding for more edit space.
**Files changed:** 2 files

---

## 2026-03-22 17:25 -- Task Started: 051 - Reduce Templates List Column Width

**Type:** Task Start
**Task:** 051 - Reduce Templates List Column Width
**Milestone:** --

---

## 2026-03-22 17:24 -- Task Completed: 054 - Hover Trash Icon per Transcript

**Type:** Task Completion
**Task:** 054 - Hover Trash Icon per Transcript
**Summary:** Replaced "Delete Selected" button with per-item hover trash icon at bottom-right of each transcript card, triggering existing delete confirmation.
**Files changed:** 3 files

---

## 2026-03-22 17:23 -- Task Completed: 045 - Consistent Max-Width Across All Pages

**Type:** Task Completion
**Task:** 045 - Consistent Max-Width Across All Pages
**Summary:** Standardized MaxWidth=900 with centered alignment across 6 pages for consistent content width on wide screens.
**Files changed:** 7 files

---

## 2026-03-22 17:19 -- Batch Started: [045, 054]

**Type:** Batch Start
**Tasks:** 045 - Consistent Max-Width Across All Pages, 054 - Hover Trash Icon per Transcript
**Mode:** Parallel (batch of 2)

---

## 2026-03-22 17:18 -- Task Completed: 056 - Link AI Model Cards to GitHub Projects

**Type:** Task Completion
**Task:** 056 - Link AI Model Cards to GitHub Projects
**Summary:** Added clickable project links to all 6 AI model cards on GeneralPage and AboutPage, opening GitHub/HuggingFace pages in default browser.
**Files changed:** 6 files

---

## 2026-03-22 17:17 -- Task Completed: 053 - Reduce Transcripts List Column Width

**Type:** Task Completion
**Task:** 053 - Reduce Transcripts List Column Width
**Summary:** Reduced transcripts list column from fixed 280px to 200px with MinWidth=160/MaxWidth=280 for responsive behavior.
**Files changed:** 2 files

---

## 2026-03-22 17:16 -- Task Completed: 050 - Collapsible Sidebar Menu

**Type:** Task Completion
**Task:** 050 - Collapsible Sidebar Menu
**Summary:** Implemented collapsible sidebar with toggle button, animated between 200px and 60px icons-only mode, state persisted in settings.
**Files changed:** 4 files

---

## 2026-03-22 17:11 -- Batch Started: [050, 053, 056]

**Type:** Batch Start
**Tasks:** 050 - Collapsible Sidebar Menu, 053 - Reduce Transcripts List Column Width, 056 - Link AI Model Cards to GitHub Projects
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 17:10 -- Task Completed: 055 - Rename Export Button to MD

**Type:** Task Completion
**Task:** 055 - Rename Export Button to MD
**Summary:** Renamed "EXPORT" button to "MD" in TranscriptsPage for consistent naming (MD, JSON, TXT).
**Files changed:** 2 files

---

## 2026-03-22 17:09 -- Task Completed: 049 - Add WhisperHeim Logo to Sidebar

**Type:** Task Completion
**Task:** 049 - Add WhisperHeim Logo to Sidebar
**Summary:** Added logo with blue-to-orange gradient to sidebar header, updated GeneralPage and AboutPage logos to use brand colors.
**Files changed:** 4 files

---

## 2026-03-22 17:08 -- Task Completed: 047 - Fix TTS Voice Cards Dark Mode Background

**Type:** Task Completion
**Task:** 047 - Fix TTS Voice Cards Dark Mode Background
**Summary:** Replaced hardcoded white background with theme-aware CardBackgroundFillColorDefaultBrush and brightened delete button for dark mode.
**Files changed:** 2 files

---

## 2026-03-22 17:06 -- Batch Started: [047, 049, 055]

**Type:** Batch Start
**Tasks:** 047 - Fix TTS Voice Cards Dark Mode Background, 049 - Add WhisperHeim Logo to Sidebar, 055 - Rename Export Button to MD
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 17:05 -- Task Completed: 052 - Remove Magic Replace from Edit Template

**Type:** Task Completion
**Task:** 052 - Remove Magic Replace from Edit Template
**Summary:** Removed magic replace UI card, clipboard placeholder pill, and clipboard expansion logic from template editor.
**Files changed:** 3 files

---

## 2026-03-22 17:04 -- Task Completed: 048 - Tray Icon Green When Recording

**Type:** Task Completion
**Task:** 048 - Tray Icon Green When Recording
**Summary:** Changed tray icon recording color from red to green (0x44CC44), matching the overlay indicator.
**Files changed:** 2 files

---

## 2026-03-22 17:03 -- Task Completed: 046 - Remember Window Size and Position

**Type:** Task Completion
**Task:** 046 - Remember Window Size and Position
**Summary:** Implemented window size/position persistence with 1200x800 default, save on close, restore on startup with off-screen guard using Win32 EnumDisplayMonitors.
**Files changed:** 4 files

---

## 2026-03-22 17:00 -- Batch Started: [046, 048, 052]

**Type:** Batch Start
**Tasks:** 046 - Remember Window Size and Position, 048 - Tray Icon Green When Recording, 052 - Remove Magic Replace from Edit Template
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 16:45 -- Ideas Captured: UI Polish Batch

**Type:** Idea Capture
**Mode:** Deep (batch)
**Tasks created:**
- `tasks/todo/048-tray-icon-green-recording.md` — Tray icon green instead of red when recording
- `tasks/todo/049-sidebar-logo.md` — Add logo to sidebar with blue (#25abfe) + orange (#ff8b00) colors
- `tasks/todo/050-collapsible-sidebar.md` — Collapsible sidebar menu (Medium)
- `tasks/todo/051-templates-column-width.md` — Reduce templates list column width
- `tasks/todo/052-remove-magic-replace.md` — Remove magic replace from edit template
- `tasks/todo/053-transcripts-list-width.md` — Reduce transcripts list column width
- `tasks/todo/054-hover-delete-transcript.md` — Hover trash icon per transcript instead of delete button
- `tasks/todo/055-export-button-rename-md.md` — Rename Export button to MD
- `tasks/todo/056-model-cards-github-links.md` — Link AI model cards to GitHub projects

---

## 2026-03-22 16:35 -- Idea Captured: TTS Voice Cards Dark Mode

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/047-tts-voice-cards-dark-mode.md
**Summary:** Library voice cards in TTS page use hardcoded white background instead of theme-aware brush. Breaks dark mode.

---

## 2026-03-22 16:30 -- Idea Captured: Window Size and Position

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/046-window-size-and-position.md
**Summary:** Default window size 1200x800 centered. Persist size/position across restarts. Reset to centered if saved position is off-screen.

---

## 2026-03-22 16:20 -- Idea Captured: Consistent Page Max-Width

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/045-consistent-page-max-width.md
**Summary:** Apply the Transcripts page's max-width constraint to all other pages for visual consistency across the app.

---

## 2026-03-22 16:15 -- Task Completed: 044 - Fix Theme Persistence and Settings Highlight

**Type:** Task Completion
**Task:** 044 - Fix Theme Persistence and Settings Highlight
**Summary:** Fixed theme persistence by adding theme restoration on app startup in App.xaml.cs and fixed theme card highlighting in GeneralPage.xaml.cs by moving HighlightActiveTheme() to a Loaded event handler.
**Files changed:** 2 files

---

## 2026-03-22 16:10 -- Task Started: 044 - Fix Theme Persistence and Settings Highlight

**Type:** Task Start
**Task:** 044 - Fix Theme Persistence and Settings Highlight
**Milestone:** --

---

## 2026-03-22 16:00 -- Idea Captured: Fix Theme Persistence

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/044-theme-persistence.md
**Summary:** Theme choice (Light/Dark/System) not restored on startup and not highlighted in settings. Two small fixes needed in App.xaml.cs and GeneralPage.xaml.cs.

---

## 2026-03-22 15:30 -- Task Completed: 043 - Faithful Quiet Engine Restyling

**Type:** Task Completion
**Task:** 043 - Faithful Quiet Engine Restyling (All Pages)
**Summary:** Faithfully restyled all 7 pages + sidebar to match inspiration mockups. Applied bento grid layouts, gradient CTAs, kbd pill key caps, ambient tinted shadows, surface hierarchy, ghost borders, editorial typography. Build succeeds.
**Files changed:** 9 files

---

## 2026-03-22 15:20 -- Task Started: 043 - Faithful Quiet Engine Restyling

**Type:** Task Start
**Task:** 043 - Faithful Quiet Engine Restyling (All Pages)
**Milestone:** M5 - UI Redesign

---

## 2026-03-22 15:10 -- Task Completed: 042 - TTS Voice Pre-Caching on Startup

**Type:** Task Completion
**Task:** 042 - TTS Voice Pre-Caching on Startup
**Summary:** Enabled sherpa-onnx embedding cache (capacity 10), added in-memory WAV sample cache, and WarmUpAsync() triggered from App.xaml.cs on background thread after UI startup.
**Files changed:** 4 files

---

## 2026-03-22 -- Task Created: 043 - Faithful Quiet Engine Restyling

**Type:** Task Creation
**Task:** 043 - Faithful Quiet Engine Restyling (All Pages)
**Summary:** Redo the visual restyling from Task 040 Phase 3 to faithfully match all inspiration mockups. Covers all 7 pages + sidebar with bento grid layouts, gradient CTAs, ghost borders, ambient shadows, kbd pills, and editorial typography.

---

## 2026-03-22 15:05 -- Task Started: 042 - TTS Voice Pre-Caching on Startup

**Type:** Task Start
**Task:** 042 - TTS Voice Pre-Caching on Startup
**Milestone:** —

---

## 2026-03-22 15:00 -- Task Completed: 041 - Default Read-Aloud Voice

**Type:** Task Completion
**Task:** 041 - Default Read-Aloud Voice
**Summary:** VoiceCombo selection now persists DefaultVoiceId to TtsSettings, and page load pre-selects the saved voice with fallback.
**Files changed:** 3 files

---

## 2026-03-22 14:55 -- Task Started: 041 - Default Read-Aloud Voice

**Type:** Task Start
**Task:** 041 - Default Read-Aloud Voice
**Milestone:** —

---

## 2026-03-22 14:50 -- Task Completed: 040 - UI Redesign Navigation & TTS Merge

**Type:** Task Completion
**Task:** 040 - UI Redesign Navigation & TTS Merge
**Summary:** Merged VoiceCloningPage and VoiceLoopbackCapturePage into TextToSpeechPage, restructured sidebar from 9 to 7 items, restyled all pages to Quiet Engine design language. Build succeeds.
**Files changed:** 15 files

---

## 2026-03-22 -- Idea Captured: TTS Voice Pre-Caching on Startup

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/042-tts-voice-warm-up.md
**Summary:** Pre-cache the default TTS voice on app startup via background thread — load TTS model, warm the sherpa-onnx embedding cache with a dummy generation, and keep the default voice's WAV samples in memory. Eliminates the ~1-3s encoder delay on first read-aloud hotkey press.

---

## 2026-03-22 14:40 -- Task Started: 040 - UI Redesign Navigation & TTS Merge

**Type:** Task Start
**Task:** 040 - UI Redesign Navigation & TTS Merge
**Milestone:** M5 - UI Redesign

---

## 2026-03-22 14:35 -- Task Completed: 038 - Transcript Audio Playback

**Type:** Task Completion
**Task:** 038 - Transcript Audio Playback
**Summary:** Audio preserved alongside transcripts, segment click-to-play with NAudio, play/pause/stop controls, position tracking, currently-playing segment highlight. Old transcripts gracefully hide playback.
**Files changed:** 6 files

---

## 2026-03-22 14:25 -- Task Started: 038 - Transcript Audio Playback

**Type:** Task Start
**Task:** 038 - Transcript Audio Playback
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-22 14:20 -- Task Completed: 037 - Speaker Name Editing

**Type:** Task Completion
**Task:** 037 - Speaker Name Editing
**Summary:** Implemented global rename (click speaker label) and per-segment override (Shift+Click) with SpeakerNameMap dictionary and SpeakerOverride property. 32 tests pass (10 new).
**Files changed:** 6 files

---

## 2026-03-22 14:15 -- Task Started: 037 - Speaker Name Editing

**Type:** Task Start
**Task:** 037 - Speaker Name Editing
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-22 14:10 -- Task Completed: 039 - Read-Aloud Overlay Indicator

**Type:** Task Completion
**Task:** 039 - Read-Aloud Overlay Indicator
**Summary:** Implemented purple-themed read-aloud overlay with Thinking (pulsing/spinning) and Playing (sound wave) animations, lifecycle events on ReadAloudHotkeyService, and onPlaybackStarted callback in SpeakAsync.
**Files changed:** 8 files

---

## 2026-03-22 14:05 -- Task Completed: 036 - Transcript Naming

**Type:** Task Completion
**Task:** 036 - Transcript Naming
**Summary:** Added editable Name property to CallTranscript model with JSON persistence, editable TextBox in transcript viewer header, name display in transcript list, and backward compatibility for existing transcripts.
**Files changed:** 7 files

---

## 2026-03-22 14:00 -- Batch Started: [036, 039]

**Type:** Batch Start
**Tasks:** 036 - Transcript Naming, 039 - Read-Aloud Overlay Indicator
**Mode:** Parallel (batch of 2)

---

## 2026-03-22 -- Idea Captured: Default Read-Aloud Voice from TTS Page

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/041-default-read-aloud-voice.md
**Summary:** Persist the voice selected on the TTS page as the default for the read-aloud hotkey (Shift+Win+Ä). Small wiring change — settings model and hotkey service already support it, just needs the UI to write it back.

---

## 2026-03-22 -- Idea Captured: UI Redesign — Navigation & TTS Merge

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/040-ui-redesign-navigation-and-tts-merge.md
**Summary:** Restructure sidebar from 9 to 7 items (Dictation, Templates, Recordings, Transcriptions, Text to Speech, Settings, Models). Merge TextToSpeechPage + VoiceCloningPage + VoiceLoopbackCapturePage into a single unified TTS page. Apply "Quiet Engine" design language from inspiration files across all pages.

---

## 2026-03-21 23:30 -- Idea Captured: Read-Aloud Overlay Indicator

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/039-read-aloud-overlay.md
**Summary:** Visual overlay indicator for the read-aloud hotkey (Shift+Win+Ä) — shows thinking state while model loads, animated playback state while reading, auto-dismisses on completion, toggle-stops on re-press. Same position as dictation overlay but distinct color.

---

## 2026-03-21 22:15 -- Research: TTS Naturalness & Sentence Boundary Artifacts

**Type:** Research
**Topic:** Why Pocket TTS output sounds rushed with voice-breaking artifacts between sentences, and how to fix it
**File:** research/tts-naturalness-and-pacing.md
**Key findings:**
- sherpa-onnx splits text into sentences, generates each independently via `GenerateSingleSentence()`, and concatenates audio with NO silence between them -- this is the root cause of the "rushed" and "breaking voice" artifacts
- Several `Extra` parameters are available but unused: `frames_after_eos` (default 3), `temperature` (default 0.7), configurable via `genConfig.Extra` hashtable
- `NumSteps` (flow matching diffusion iterations) can be increased from 5 to 8-10 for smoother audio quality
- Best fix: app-level sentence splitting with configurable silence injection (300ms+) between generated segments
- The 15s reference audio gets truncated to 12s; int8 quantization may also degrade voice cloning fidelity

---

## 2026-03-21 21:40 -- Idea Captured: Transcript Usability Improvements

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/036-transcript-naming.md, tasks/todo/037-speaker-name-editing.md, tasks/todo/038-transcript-audio-playback.md
**Summary:** Three tasks to improve transcript usability long-term: (1) editable transcript names with auto-default from date/time, (2) speaker name editing with global rename + per-segment override, (3) click-to-play audio playback from segments with WAV files preserved alongside transcripts. All added to M2 milestone.

---

## 2026-03-21 21:35 -- Milestone M4 Complete: Text-to-Speech (Kyutai Pocket TTS)

**Type:** Milestone Completion
**Milestone:** M4 - Text-to-Speech
**Tasks completed:** 023, 029, 030, 031, 032, 033, 034, 035 (7 subtasks + parent)
**Summary:** Full TTS milestone implemented — Pocket TTS engine via sherpa-onnx, voice cloning from mic and system audio, read-selected-text hotkey, TTS UI page, MP3/OGG/WAV export, configurable settings.

---

## 2026-03-21 21:30 -- Task Completed: 034 - Audio export (MP3/OGG/WAV)

**Type:** Task Completion
**Task:** 034 - Audio export (MP3/OGG/WAV)
**Summary:** Created AudioExportService with WAV, MP3 (resampled to 44.1kHz via MediaFoundationEncoder), and OGG/Opus (resampled to 48kHz via Concentus) export. Added "Save as..." button to TextToSpeechPage with SaveFileDialog and format selection.
**Files changed:** 4 files

---

## 2026-03-21 21:20 -- Task Started: 034 - Audio export (MP3/OGG)

**Type:** Task Start
**Task:** 034 - Audio export (MP3/OGG/WAV)
**Milestone:** M4 - Text-to-Speech

---

## 2026-03-21 21:15 -- Task Completed: 035 - TTS settings + hotkey configuration

**Type:** Task Completion
**Task:** 035 - TTS settings + hotkey configuration
**Summary:** Added TtsSettings model with DefaultVoiceId, ReadAloudHotkey, PlaybackDeviceId persisted via SettingsService. ReadAloudHotkeyService now reads hotkey config from settings with live re-registration. SpeakAsync accepts playback device parameter.
**Files changed:** 6 files

---

## 2026-03-21 21:12 -- Task Completed: 033 - TTS UI page

**Type:** Task Completion
**Task:** 033 - TTS UI page
**Summary:** Created TextToSpeechPage with multi-line text input, voice selector (built-in + custom), Play/Stop with CancellationTokenSource, indeterminate progress bar, and voice preview button. Wired into MainWindow sidebar navigation.
**Files changed:** 5 files

---

## 2026-03-21 21:05 -- Batch Started: [033, 035]

**Type:** Batch Start
**Tasks:** 033 - TTS UI page, 035 - TTS settings + hotkey configuration
**Mode:** Parallel (batch of 2)

---

## 2026-03-21 21:00 -- Task Completed: 032 - Read selected text via global hotkey

**Type:** Task Completion
**Task:** 032 - Read selected text via global hotkey
**Summary:** Implemented SelectedTextService with cascading capture (UI Automation TextPattern first, then SendInput Ctrl+C with clipboard backup/restore) and ReadAloudHotkeyService (Ctrl+Shift+R default) that speaks captured text via ITextToSpeechService.
**Files changed:** 5 files

---

## 2026-03-21 20:58 -- Task Completed: 031 - Voice cloning from system audio loopback

**Type:** Task Completion
**Task:** 031 - Voice cloning from system audio loopback
**Summary:** Created HighQualityLoopbackService capturing system audio at native 48kHz via WasapiLoopbackCapture. Built VoiceLoopbackCapturePage UI with device selection, level meter, duration display, voice naming, and save to voices directory.
**Files changed:** 6 files

---

## 2026-03-21 20:55 -- Task Completed: 030 - Voice cloning from microphone recording

**Type:** Task Completion
**Task:** 030 - Voice cloning from microphone recording
**Summary:** Implemented HighQualityRecorderService recording mic at 44.1kHz and VoiceCloningPage UI with level meter, duration tracking, 5s minimum indicator, device selection, voice naming, and background noise warning.
**Files changed:** 7 files

---

## 2026-03-21 20:50 -- Batch Started: [030, 031, 032]

**Type:** Batch Start
**Tasks:** 030 - Voice cloning from mic, 031 - Voice cloning from loopback, 032 - Read selected text via hotkey
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 20:45 -- Task Completed: 029 - Pocket TTS engine service + model download

**Type:** Task Completion
**Task:** 029 - Pocket TTS engine service + model download + built-in voice playback
**Summary:** Implemented ITextToSpeechService with Pocket TTS via sherpa-onnx C# bindings. Supports GenerateAudioAsync, streaming generation with callback, and SpeakAsync with NAudio WaveOutEvent playback at 24kHz. Added PocketTtsInt8 model (~200MB, 9 files) to ModelManagerService for auto-download from HuggingFace. Build succeeds.
**Files changed:** 5 files

---

## 2026-03-21 19:15 -- Task Started: 029 - Pocket TTS engine service + model download

**Type:** Task Start
**Task:** 029 - Pocket TTS engine service + model download + built-in voice playback
**Milestone:** M4 - Text-to-Speech

---

## 2026-03-21 18:50 -- Idea Captured: Kyutai Pocket TTS Integration

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/023-tts-pocket-tts.md (parent) + subtasks 029–035
**Summary:** Full TTS milestone using Kyutai Pocket TTS — voice cloning from mic/loopback, read-selected-text hotkey (UI Automation + Ctrl+C fallback), TTS UI page, MP3/OGG export, and settings. Researched feasibility of all components: Pocket TTS runs CPU-only via sherpa-onnx (already a dependency), text selection capture is proven pattern, loopback capture infrastructure exists. English-only, 7 tasks total.

---

## 2026-03-21 -- Task Completed: 028 - Post-recording transcription pipeline with progress UI

**Type:** Task Completion
**Task:** 028 - Post-recording transcription pipeline with progress UI
**Summary:** Created TranscriptionProgressDialog with dual progress bars, stage description, and cancel button. Wired into MainWindow to auto-trigger pipeline when recording stops, with navigation to TranscriptsPage on success.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Started: 028 - Post-recording transcription pipeline with progress UI

**Type:** Task Start
**Task:** 028 - Post-recording transcription pipeline with progress UI
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Task Completed: 027 - Tray context menu for start/stop call recording

**Type:** Task Completion
**Task:** 027 - Tray context menu for start/stop call recording
**Summary:** Added "Start Call Recording" tray menu item with Record24 icon, Ctrl+Win+R hotkey, and live recording state feedback (orange tray icon, duration in menu text and tooltip). Added DurationUpdated to ICallRecordingService interface.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Started: 027 - Tray context menu for start/stop call recording

**Type:** Task Start
**Task:** 027 - Tray context menu for start/stop call recording
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Task Completed: 026 - Wire call recording services in app startup

**Type:** Task Completion
**Task:** 026 - Wire call recording services in app startup
**Summary:** Wired CallRecordingService, CallTranscriptionPipeline, CallRecordingHotkeyService, SpeakerDiarizationService, and TranscriptStorageService in App.xaml.cs and passed them to MainWindow constructor as fields.
**Files changed:** 3 files

---

## 2026-03-21 -- Task Started: 026 - Wire call recording services in app startup

**Type:** Task Start
**Task:** 026 - Wire call recording services in app startup
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Planning: Call Recording UI Integration

**Type:** Planning
**Summary:** Planned 3 tasks to wire up the existing call recording backend to the UI — service registration, tray context menu with Ctrl+Win+R hotkey, and post-recording transcription progress dialog with auto-navigation.
**Milestones created/updated:** M2 (added tasks 026-028)
**Tasks created:** 026-wire-call-recording-services, 027-tray-menu-call-recording, 028-post-recording-transcription-ui
**Tasks moved to backlog:** none
**Ideas incorporated:** none

---

## 2026-03-21 -- Task Completed: 025 - Overlay Microphone State Visualization

**Type:** Task Completion
**Task:** 025 - Overlay Microphone State Visualization
**Summary:** Implemented dynamic overlay mic states (green idle, green+RMS-driven ring scaling while speaking, grey for no mic, red for errors). Added OverlayMicState enum, replaced hardcoded red with animated color brushes, wired real-time audio amplitude through orchestrator to drive smooth ring scaling.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Started: 025 - Overlay Microphone State Visualization

**Type:** Task Start
**Task:** 025 - Overlay Microphone State Visualization
**Milestone:** --

---

## 2026-03-21 -- Idea Captured: Overlay Mic State Visualization

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/025-overlay-mic-state-visualization.md
**Summary:** Dynamic mic icon colors (green=idle/speaking, grey=no mic, red=error) with amplitude-driven ring scaling animation during speech. Overlay only.

---

## 2026-03-21 -- Task Completed: 024 - Windows Auto-Launch

**Type:** Task Completion
**Task:** 024 - Windows Auto-Launch
**Summary:** StartupService manages HKCU Run registry, --minimized flag for tray-only auto-start, path refresh on each launch.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Completed: 014 - Microphone Selection

**Type:** Task Completion
**Task:** 014 - Microphone Selection
**Summary:** Dropdown on Dictation page with NAudio device enumeration, persisted selection, fallback for missing devices.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Completed: 005 - Model Manager

**Type:** Task Completion
**Task:** 005 - Model Manager
**Summary:** Auto-downloads Parakeet TDT 0.6B int8 (~661MB) and Silero VAD (~2MB) on first run with progress dialog and cancellation.
**Files changed:** 9 files

---

## 2026-03-21 -- Task Completed: 007 - Silero VAD Integration

**Type:** Task Completion
**Task:** 007 - Silero VAD Integration
**Summary:** ONNX Runtime-based Silero VAD with state machine, configurable thresholds, pre-speech padding, SpeechStarted/SpeechEnded events.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 004 - Global Hotkey

**Type:** Task Completion
**Task:** 004 - Global Hotkey
**Summary:** Win32 RegisterHotKey/UnregisterHotKey with configurable Ctrl+LWin hotkey, event system, and conflict handling.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 003 - Settings Infrastructure

**Type:** Task Completion
**Task:** 003 - Settings Infrastructure
**Summary:** JSON settings with AppSettings model, SettingsService for %APPDATA% persistence, and 4 navigable settings pages in MainWindow.
**Files changed:** 13 files

---

## 2026-03-21 -- Batch Started: [003, 004, 007]

**Type:** Batch Start
**Tasks:** 003 - Settings Infrastructure, 004 - Global Hotkey, 007 - Silero VAD Integration
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 -- Task Completed: 010 - Input Simulation

**Type:** Task Completion
**Task:** 010 - Input Simulation
**Summary:** Win32 SendInput P/Invoke with KEYEVENTF_UNICODE, backspace correction, configurable delay, cancellation support.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 006 - Audio Capture Service

**Type:** Task Completion
**Task:** 006 - Audio Capture Service
**Summary:** NAudio WaveInEvent capture at 16kHz/mono, float32 conversion, thread-safe ring buffer, device enumeration. 8 passing unit tests.
**Files changed:** 8 files

---

## 2026-03-21 -- Task Completed: 002 - Tray Icon and Window

**Type:** Task Completion
**Task:** 002 - Tray Icon and Window
**Summary:** FluentWindow with Mica backdrop, tray icon with Segoe Fluent microphone glyph, show/hide toggle, right-click context menu.
**Files changed:** 5 files

---

## 2026-03-21 -- Batch Started: [002, 006, 010]

**Type:** Batch Start
**Tasks:** 002 - Tray Icon and Window, 006 - Audio Capture Service, 010 - Input Simulation
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 -- Task Completed: 001 - Project Scaffolding

**Type:** Task Completion
**Task:** 001 - Project Scaffolding
**Summary:** Created .NET 9 WPF solution with all core NuGet packages, x64-only config, and ShutdownMode=OnExplicitShutdown. Builds with 0 warnings.
**Files changed:** 8 files

---

## 2026-03-21 -- Task Started: 001 - Project Scaffolding

**Type:** Task Start
**Task:** 001 - Project Scaffolding
**Milestone:** M1 - Live Dictation + Core App

---

## 2026-03-21 -- Planning: Full roadmap and task breakdown for all milestones

**Type:** Planning
**Summary:** Created 4-milestone roadmap with 24 tasks. M1 (Live Dictation + Core App) has 15 tasks covering project setup through end-to-end dictation with overlay and templates. M2 (Call Transcription) has 5 tasks for WASAPI loopback, diarization, and transcript export. M3 (Voice Messages) has 3 tasks including backlogged Telegram bot. M4 (TTS) is a single placeholder task in backlog.
**Milestones created:** M1, M2, M3, M4
**Tasks created:** 001 through 024
**Tasks moved to backlog:** 022-telegram-bot, 023-tts-integration
**Ideas incorporated:** None (no ideas existed)

---

## 2026-03-21 -- Brainstorm: Initial product vision for WhisperHeim

**Type:** Brainstorm
**Summary:** Defined WhisperHeim as a local-first, Windows 11 tray app unifying all voice workflows: live streaming dictation, call transcription with speaker diarization, voice message transcription, and text-to-speech. Chose C#/WPF with WPF UI for the native shell, Parakeet TDT 0.6B for ASR, sherpa-onnx for diarization, and Silero VAD for streaming.
**Vision updated:** Yes
**Key decisions:**
- Complete restart from VocalFold -- new architecture, no code reuse
- C# across the board (systems-level complexity favors C# over F#)
- WPF + WPF UI (not WinUI 3) for tray app with PowerToys aesthetics
- Parakeet TDT 0.6B over Whisper (faster, no hallucinations, EN/DE sufficient)
- sherpa-onnx for both ASR and diarization (native .NET, no Python sidecar)
- WASAPI loopback for system audio capture (call transcription = Milestone 2)
- Text-to-speech deferred to Milestone 4, details TBD
- No voice commands -- templates only, triggered by hotkey + voice
- Telegram bot integration as stretch goal for voice message transcription

---
