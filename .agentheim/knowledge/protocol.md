# Protocol

Chronological log of everything that happens in this project.
Newest entries on top.

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

## 2026-06-29 18:42 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 task commit + this session-end line
**Scope:** infrastructure-v8k2m (notify-only in-app auto-update via Velopack + GitHub Releases). Single ready task, no dependencies. Todo/doing now empty across all BCs — backlog empty too; nothing left to refine or work.
**ADRs written:** 1 — ADR-0007 (notify-only in-app auto-update via a Velopack gateway seam + status-footer signal).

---

## 2026-06-29 18:40 -- Task verified and completed: infrastructure-v8k2m - In-app auto-update notify-only via Velopack + GitHub Releases

**Type:** Work / Task completion
**Task:** infrastructure-v8k2m - In-app auto-update — notify-only "new version available" via Velopack + GitHub Releases
**Summary:** App-owned `UpdateService` over Velopack + the public GitHub Releases feed checks → silently downloads → stages a newer release and surfaces "Update ready: vX.Y" in the always-visible status footer; applies only via Velopack's auto-apply-on-next-launch or an explicit "Restart & update now" click — never force-restarting, and a clean no-op (`IsInstalled` guard) on dev/unpacked runs.
**Verification:** PASS (iteration 1) — builds clean against pinned Velopack 0.0.1298 (the load-bearing API-surface check); 191/191 tests green incl. 9 new orchestration tests; footer signal lives in `SttStatusFooter` Grid.Row=3 collapsed-by-default; `SetAutoApplyOnStartup` confirmed never called (default left enabled); restart only on explicit click.
**Files changed:** 10
**Tests added:** 9
**ADRs written:** 0007-notify-only-in-app-update-via-velopack.md

---

## 2026-06-29 18:30 -- Batch started: [infrastructure-v8k2m]

**Type:** Work / Batch start
**Tasks:** infrastructure-v8k2m - In-app auto-update — notify-only "new version available" via Velopack + GitHub Releases
**Parallel:** no (1 worker) — only ready task, no dependencies.

---

## 2026-06-29 16:50 -- Modeling / Refined: infrastructure-v8k2m - In-app auto-update notify-only via Velopack + GitHub Releases

**Type:** Modeling / Refine
**BC:** infrastructure
**Status after:** todo
**Summary:** Resolved the one open "refine before work" item — the notify UX. Maintainer's call: surface the "Update ready: vX.Y" signal in the **always-visible bottom status footer** (`SttStatusFooter`, `MainWindow.xaml` Grid.Row=3), not a toast/tray balloon — noticeable, never interrupts dictation. Apply mode: keep Velopack's **auto-apply-on-next-launch** default + add an explicit **"Restart & update now"** footer action (`ApplyUpdatesAndRestart`); no consent-gated install-on-quit path. Acceptance criteria rewritten around the footer placement + the two apply paths. Sibling `infrastructure-p4w7n` landed (done) during this session — its `IAppVersionProvider` is now the reuse target for the footer's current-version half. Research + verification gotchas already captured; no new ADR (generic-BC plumbing, decision recorded in task Notes). Ready → promoted to todo.
**Split into:** none
**ADRs written:** none

---

## 2026-06-29 16:47 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 task commit + this session-end line
**Scope:** infrastructure-p4w7n (displayed app version sourced from Velopack packed release via a single `IAppVersionProvider`). Single ready task, no dependencies. Todo/doing now empty across all BCs; only the unrefined infrastructure backlog item `infrastructure-v8k2m` (in-app auto-update) remains — needs `modeling` refinement before `work` can pick it up.
**ADRs written:** 0 — small generic-BC refactor; the source-of-truth decision was already resolved and recorded in the task Notes during refinement.

---

## 2026-06-29 16:45 -- Task verified and completed: infrastructure-p4w7n - Source displayed app version from packed release

**Type:** Work / Task completion
**Task:** infrastructure-p4w7n - Source the displayed app version from the packed release version (dictation, settings, about pages)
**Summary:** The displayed app version is now sourced at runtime from Velopack's installed-version metadata (the packed `v*` tag) through a single shared `IAppVersionProvider`; the three hardcoded `v1.0` literals (Dictation, Settings, About) bind one formatted string — `vX.Y.Z` installed, `dev` unpacked.
**Verification:** PASS (iteration 1) — 4/4 provider unit tests green; two-tier logic matches the RESOLVED decision (no assembly tier, no release.yml change); `VelopackLocator.CreateDefaultForPlatform`/`CurrentlyInstalledVersion` confirmed real by successful build; three XAML bindings code-read.
**Files changed:** 7
**Tests added:** 4
**ADRs written:** none

---

## 2026-06-29 16:35 -- Batch started: [infrastructure-p4w7n]

**Type:** Work / Batch start
**Tasks:** infrastructure-p4w7n - Source the displayed app version from the packed release version (dictation, settings, about pages)
**Parallel:** no (1 worker) — only ready task; backlog sibling infrastructure-v8k2m not yet promoted.

---

## 2026-06-29 16:30 -- Modeling / Refined: infrastructure-p4w7n - Source displayed app version from packed release

**Type:** Modeling / Refine
**BC:** infrastructure
**Status after:** todo
**Summary:** Resolved the open source-of-truth decision: read the version from Velopack's installed-version metadata (`VelopackLocator`, = the `--packVersion` tag), **no release.yml change** (maintainer's call). Key consequence baked in — since the pipeline stays unchanged, the assembly informational version is permanently `1.0.0`, so the "assembly fallback" tier is dropped: the honest logic is two tiers (`installed → "v"+version; else → "dev"`), never surfacing a misleading `v1.0.0`. Added: read via `VelopackLocator` (not `UpdateManager`, to avoid coupling display to a `GithubSource`/network); single `IAppVersionProvider` shape; unit-test AC for the UI-free resolve/format/fallback. Sibling `infrastructure-v8k2m` confirmed independent (agree-by-construction, no hard dependency). Refinement made it ready → promoted to todo.
**Split into:** none
**ADRs written:** none (small generic-BC refactor; decision recorded in task Notes)

---

## 2026-06-29 16:17 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 task commit + this session-end line
**Scope:** main-r8m4q (Parakeet model-card `ProjectUrl` v2 → v3). Single ready task, no dependencies. Todo/doing now empty across all BCs; only the two unrefined infrastructure backlog items (infrastructure-v8k2m, infrastructure-p4w7n) remain — they need `modeling` refinement before `work` can pick them up.
**ADRs written:** 0 — a one-line URL copy fix, no architectural decision.

---

## 2026-06-29 16:16 -- Task verified and completed: main-r8m4q - About-page Parakeet link points to v2, app uses v3

**Type:** Work / Task completion
**Task:** main-r8m4q - About-page Parakeet link points to v2, app uses v3
**Summary:** The Parakeet model card's `ProjectUrl` now points at the v3 NVIDIA Hugging Face card, matching the int8 sherpa-onnx v3 build the app actually runs (was the English-only v2 card).
**Verification:** PASS (iteration 1) — diff is a single literal change; About-page binding pre-exists from main-056; no other model's ProjectUrl touched. No build needed (string literal in a record ctor).
**Files changed:** 1
**Tests added:** 0
**ADRs written:** none

---

## 2026-06-29 16:15 -- Batch started: [main-r8m4q]

**Type:** Work / Batch start
**Tasks:** main-r8m4q - About-page Parakeet link points to v2, app uses v3
**Parallel:** no (1 worker) — only ready task, no dependencies. Single-line `ProjectUrl` copy fix in `ModelManagerService.cs` (Parakeet v2 → v3).

---

## 2026-06-29 16:05 -- Modeling / Captured: infrastructure-v8k2m - In-app auto-update notify-only via Velopack + GitHub Releases

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Client-side `UpdateService` (UpdateManager + GithubSource against the public repo) that detects a freshly-tagged GitHub Release, notifies "new version available" without forcing a restart, downloads silently, and applies on quit/restart. Guards on `IsInstalled` for dev, polls gently (60 req/hr GitHub limit). Distribution half already exists; this is the missing client side. Grounded in the velopack-in-app-update-github-2026-06-29 report. Open UX decision (where the notice appears) left for refine.

---

## 2026-06-29 16:05 -- Modeling / Captured: infrastructure-p4w7n - Source displayed app version from the packed release version

**Type:** Modeling / Capture
**BC:** infrastructure
**Filed to:** backlog
**Summary:** Replace the hardcoded `v1.0` literal on the Dictation, Settings (GeneralPage), and About pages with a single runtime-sourced version derived from the packed release version (the `v*` tag via `vpk pack --packVersion`). Open source-of-truth decision: Velopack CurrentVersion vs. assembly informational version (latter needs a one-line release.yml injection). Sibling of infrastructure-v8k2m.

---

## 2026-06-29 16:10 -- Modeling / Captured: main-r8m4q - About-page Parakeet link points to v2, app uses v3

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** The About page's Parakeet model card links to the v2 Hugging Face card, but the app runs Parakeet TDT 0.6B v3. Fix is a one-line `ProjectUrl` change in `ModelManagerService.cs:61` (v2 → v3). Well-scoped, filed straight to todo.

---

## 2026-06-29 15:50 -- Research: Velopack in-app auto-update via GitHub Releases

**Type:** Research
**Requested by:** user
**Report:** knowledge/research/velopack-in-app-update-github-2026-06-29.md
**Review:** PASS (iteration 2)
**Summary:**
- Feasible with zero pipeline changes — the `RELEASES` manifest + nupkgs already uploaded on each `v*` tag are exactly what a runtime `UpdateManager` + `GithubSource(repo, null, false)` consume; remaining work is one client-side `UpdateService` + tray wiring.
- Notify-only / apply-on-quit is the supported, correct shape: `CheckForUpdatesAsync` → background `DownloadUpdatesAsync` → `WaitExitThenApplyUpdates(asset, silent:true, restart:false)` (or auto-apply-on-next-launch); `ApplyUpdatesAndRestart` is the "Restart now" button only.
- Two must-do guards: `if (!mgr.IsInstalled) return;` (unpacked dev runs throw `NotInstalledException`) and gentle polling (startup + multi-hour timer; unauthenticated GitHub API = 60 req/hr/IP). Unsigned releases update fine (hash-based integrity ≠ Authenticode); `%APPDATA%` model/FFmpeg files are outside the install dir and untouched by the swap.
- Provenance caveat: API signatures verified against the docs' Velopack **1.2.0** reference (no 0.0.1298-stamped page exists); 0.0.1298-exact signatures are assumed-stable but marked ⚠️ UNVERIFIED — confirmable in ~5 min via IntelliSense/decompile.

---

## 2026-06-29 12:12 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 task commit + this session-end line
**Scope:** main-t9w2k (inline template-creation modal on no-match). Single ready task, no dependencies. Board now fully empty (0 todo / 0 doing / 0 backlog across all BCs).
**ADRs written:** 0 — UI built on the existing `InlineDialog` modal pattern and `TemplateService.AddTemplate` path; save-only behavior was decided during refinement, no new architectural decision arose.
**Follow-up:** UI-only ACs (modal vs toast, prompt copy, Cancel/Escape, multiline body) are verified by code-reading + the test-covered model, not by an automated WPF UI test (none in repo). A `/deploy` and a live template-mode dictation with a nonsense word is the honest confirmation the modal pops centered and persists.

---

## 2026-06-29 12:10 -- Task verified and completed: main-t9w2k - Inline template creation dialog when no template matches

**Type:** Work / Task completion
**Task:** main-t9w2k - Inline template creation dialog when no template matches
**Summary:** A template-mode dictation that matches no template now opens a centered top-most modal to create the missing template inline (editable, pre-filled trigger term + multiline replacement body), persisted save-only via `TemplateService.AddTemplate`, replacing the old dead-end no-match toast.
**Verification:** PASS (iteration 1) — build clean, full suite green 178/178 (9 new `InlineTemplateCreationModelTests`). UI-only ACs (centered modal vs toast, prompt copy, Cancel/Escape) wired in the diff and exercised manually per the repo's documented lack of WPF UI-test infra; validation/persistence/save-only logic factored into a UI-free `InlineTemplateCreationModel` and unit-tested.
**Files changed:** 5
**Tests added:** 9
**ADRs written:** none

---

## 2026-06-29 12:00 -- Batch started: [main-t9w2k]

**Type:** Work / Batch start
**Tasks:** main-t9w2k - Inline template creation dialog when no template matches
**Parallel:** no (1 worker) — only ready task, no dependencies. Touches the no-match path in `App.xaml.cs` (the `TemplateNoMatch` handler currently calling `ToastWindow.Show`) and adds a new centered modal dialog (`Views/`), reusing the `InputDialog`/`DeleteConfirmationDialog` pattern; persists via `TemplateService.AddTemplate`.

---

## 2026-06-29 00:00 -- Modeling / Captured: main-t9w2k - Inline template creation dialog when no template matches

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** Replace the bottom-right "no template match" toast with a centered modal dialog that lets the user create the missing template inline — editable trigger term (pre-filled with the transcribed/misheard word) + editable replacement-text body, persisted via the same `TemplateService.AddTemplate` path as the start-page drawer. Save-only (no auto-insert into the focused app), per user decision. Grounded in the existing `TemplateNoMatch` event and `InputDialog` modal pattern; filed straight to todo.

---

## 2026-06-28 16:10 -- Follow-up bug fix (live debug): first dictation overlay top-right on scaled ultrawide [main-p3k9d]

**Type:** Bug fix / Interactive (outside work loop)
**Task:** main-p3k9d (done) — its original fix did not resolve the symptom on the maintainer's 57" G95NC super-ultrawide at 125% scaling.
**Root cause (poller-confirmed):** first-`Show()` layout/DPI settling glitch — the very first realization of the overlay HWND rests at (4611,13) top-right; every subsequent show is correct at (3022,1620). WPF `Left`/`Top` governs final position; a `SetWindowPos` physical-pixel detour was always overridden by WPF and was reverted.
**Fix:** `DictationOverlayWindow.PrewarmFirstShow()` does the throwaway first show invisibly (Opacity 0) at startup via `App.InitializeOverlay`, so the first real dictation is already a clean "second show".
**Verified:** live Win32 poll caught the pre-warm absorbing the (4611,13) glitch at startup, then the user's first real dictation at (3022,1620); maintainer confirmed. Tests 169/169.
**Files:** `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs`, `src/WhisperHeim/App.xaml.cs`. Outcome appended to the main-p3k9d task.
**Residual (accepted, not fixed):** SYSTEM_AWARE (not Per-Monitor-V2) means the pill renders unscaled (100×40 not 125×50) and centers on the DIP-derived point, not true physical center. A PMv2 manifest would fix both but is an app-wide change, deliberately not bundled.

---

## 2026-06-28 15:30 -- Deploy verification: infrastructure-q4t8m AC6 confirmed live

**Type:** Work / Deploy verification
**Task:** infrastructure-q4t8m - "Warming up" overlay state when an utterance outruns the model load
**Result:** The user ran `/deploy` and confirmed the deploy-gated AC6 live — a short utterance after an idle unload shows the pulsing-amber warming-up state, then the transcription lands. All 6 acceptance criteria now satisfied (ACs 1–5 by the verifier's trace + 4 unit tests at completion; AC6 now confirmed on the running app). Task fully done. (Logged after the 15:26 session because the live test happened post-session.)

---

## 2026-06-28 15:26 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 task commit + this session-end line
**Scope:** main-p3k9d (first dictation overlay mispositioned). Single ready task, no dependencies. Board now fully empty (0 todo / 0 doing / 0 backlog across all BCs).
**ADRs written:** 0.
**Follow-up:** The Show()/DPI/first-show lifecycle is verified only by unit-pinned geometry + reasoning; a live `/deploy` (fresh app launch → first Ctrl+Win dictation) is the honest confirmation that the very first pill now lands bottom-center.

---

## 2026-06-28 15:25 -- Task verified and completed: main-p3k9d - First dictation overlay renders at wrong position (not bottom-center)

**Type:** Work / Task completion
**Task:** main-p3k9d - First dictation overlay renders at wrong position (not bottom-center)
**Summary:** First dictation pill now lands at bottom-center on the very first show after launch, not just on subsequent ones — positioning moved to *after* `Show()` (window then has an HWND/DPI context + completed layout) and the dead `_hasBeenLoaded` first-show guard removed; `PositionAtBottomCenter()` now prefers `ActualWidth`/`ActualHeight`, with the centering math extracted into a pure, unit-tested `ComputeBottomCenter`.
**Verification:** PASS (iteration 1) — build clean, full suite green 169/169 (3 new geometry tests). No flash confirmed (XAML `Opacity="0"`, fade-in after positioning); no remaining `_hasBeenLoaded` readers; multi-monitor behavior unchanged.
**Files changed:** 2
**Tests added:** 3
**ADRs written:** none

---

## 2026-06-28 15:20 -- Batch started: [main-p3k9d]

**Type:** Work / Batch start
**Tasks:** main-p3k9d - First dictation overlay renders at wrong position (not bottom-center)
**Parallel:** no (1 worker) — only ready task, no dependencies. Touches the dictation overlay positioning in `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs` (PositionAtBottomCenter / ShowOverlay / OnLoaded).

---

## 2026-06-28 15:10 -- Modeling / Captured: main-p3k9d - First dictation overlay renders at wrong position (not bottom-center)

**Type:** Modeling / Capture
**BC:** main
**Filed to:** todo
**Summary:** First dictation overlay after app start renders top / two-thirds-from-left instead of bottom-center; later shows are fine. Root cause grounded: `PositionAtBottomCenter()` uses the `Width`/`Height` properties (`NaN` pre-measure) and the first show skips the post-`Show()` reposition (`_hasBeenLoaded` guard). Filed straight to todo — clear repro, scope, and AC.

---

## 2026-06-28 14:36 -- Work session ended

**Type:** Work / Session end
**Completed:** 1 (first-try PASS: 1, re-dispatched: 0, skipped: 0)
**Bounced:** 0
**Failed:** 0
**Escalated after verification:** 0
**Commits:** 1 task commit + this session-end line
**Scope:** infrastructure-q4t8m ("warming up" overlay state). Single ready task; its dependency infrastructure-d2v7n was done. Board now fully empty (0 todo / 0 doing / 0 backlog across all BCs).
**ADRs written:** 0 (the visual treatment was already decided during refinement; no new architectural decision arose).
**Follow-up:** AC6 is deploy-gated — run `/deploy`, force a ≥5 min idle unload, fire a short (<~4 s) utterance, and confirm the pulsing-amber warming-up state persists before the transcription lands.

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

