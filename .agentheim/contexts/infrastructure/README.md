# infrastructure

## Purpose
This BC owns *globally-true* infra concerns for WhisperHeim — runtime, packaging, distribution, code signing, FFmpeg detection, settings/data-path resolution, GitHub Actions release pipeline. BC-local infra (audio device adapters, transcription queue plumbing inside `main/`) stays inside the originating BC.

## Classification
generic

Standard ops/distribution plumbing. Not differentiating; chosen for fit (Velopack for delta updates, GitHub Releases for distribution, winget for FFmpeg sidestep on LGPL/GPL).

## Actors
- **Maintainer** — tags releases, watches workflow runs, decides when to sign.
- **End user (transitively)** — experiences the infra layer as first-run model download, auto-update, uninstall behavior.

## Ubiquitous language
- **Velopack / vpk** — packaging + auto-update framework. `vpk pack` emits `Setup.exe` + delta nupkg + RELEASES manifest.
- **Bootstrap config** — small pointer file in `%AppData%\WhisperHeim\` that locates the real configurable data path (supports cloud-synced setups).
- **Data path** — user-configurable folder containing `settings.json`, `recordings/`, `voices/`. Defaults next to the bootstrap config but may live in a cloud-synced drive.
- **SmartScreen / SAC** — Windows reputation / Smart App Control gates. The app ships unsigned today; SAC hard-blocks unsigned binaries in 25H2, mitigated via a release-page click-through video.
- **Post-startup housekeeping hook** — a single fire-and-forget background task scheduled at the end of `App.StartupCore` (~5 s after boot, off the UI thread) that performs idle memory housekeeping without competing with first-frame render or the first dictation. It runs the one-shot LOH compaction (`StartupMemoryCompactor`) then a working-set trim (`WorkingSetTrimmer`) — "compact, then trim" (ADR-0003/0004). Disable via `WHISPERHEIM_DISABLE_STARTUP_GC=1`.
- **Working-set trim** — `WorkingSetTrimmer` P/Invokes `EmptyWorkingSet` (psapi) to release the process working set so cold pages move to the OS standby list, dropping *reported* RSS (`WorkingSet64`) without unloading anything; committed/private memory is unchanged and the resident Parakeet recognizer stays loaded (pages re-fault on next dictation). Windows-only, failure-isolated (logs and continues). Fired once after model load (on the housekeeping hook) and again on idle.
- **Idle working-set trim** — `IdleWorkingSetTrimmer` trims again after **3 min** with no dictation activity (30 s poll, once per idle period, re-armed by `OnDictationStateChanged` → `NotifyActivity`). Steady-state runtime lever, *not* gated by `WHISPERHEIM_DISABLE_STARTUP_GC` (only the startup trim is).
- **Recognizer lifecycle (lazy-load + keep-warm + idle-unload)** — `ModelLifecycleManager` (in `Services/Transcription/`) owns the residency state machine for the ~640 MB INT8 Parakeet recognizer: **Unloaded → Loading → Loaded → (idle) → Unloaded** (ADR-0005, ADR-0006). Unlike the trim (which only moves pages to standby), an **idle-unload** disposes the recognizer and reclaims ~680 MB of *committed/private* memory, paired with `GC.Collect()` + working-set trim. The model is **lazy-on** — no eager startup load; it loads on Ctrl+Win **key-DOWN** (`BeginLoad`, fire-and-forget; the fixed ~4 s session build overlaps speaking time) and transcribe-on-release **awaits the same load** (`EnsureLoadedAsync`) rather than starting a second one. Kept warm through active dictation (`EnterDictation`/`ExitDictation` + `NotifyActivity`); unloaded after **5 min** idle (30 s poll) — a deliberately longer fuse than the 3-min trim because a ~4 s session rebuild costs far more than a cold-page re-fault. The unload fuse is hardcoded here; configurability is a follow-up (`infrastructure-b3n6p`), as is the "warming up" overlay (`infrastructure-q4t8m`).
- **Self-healing decode** — `TranscriptionService.TranscribeAsync` (re)loads the recognizer on demand inside the decode lock, so *every* consumer (Ctrl+Win dictation, the loopback HTTP API, file/stream/call transcription) transparently survives an idle-unload. The shared decode/load/`Unload()` lock is the unload-safety invariant: a dispose can never race a mid-flight decode. `Unload()` is non-terminal (reusable); `Dispose()` is terminal.

## Aggregates
- **Release artifact** — a tagged `v*` build comprising `Setup.exe`, delta nupkg, RELEASES manifest. Reproducible from the GitHub Actions workflow.
- **Bootstrap+data-path pair** — protects the invariant that machine-local settings never sync, while user data can.

## Key events
- `ReleaseTagged` / `ReleasePublished`
- `ModelDownloadStarted` / `ModelDownloadCompleted` (first-run)
- `FFmpegDetected` / `FFmpegMissing`

## Key commands
- `PackRelease` (CI)
- `BootstrapDataPath` (first run)
- `ResolveFFmpeg`

## Relationships with other contexts
- **Upstream of:** `main/` — every domain feature runs on top of this BC's runtime + packaging foundation.

## Open questions
- Code signing — currently stubbed (Task `main-115`); to be flipped post-UG registration. Decision between signtool with PFX vs. Azure Trusted Signing still open.
- One-time-purchase license-key infrastructure — implied by monetization research; no task yet.
