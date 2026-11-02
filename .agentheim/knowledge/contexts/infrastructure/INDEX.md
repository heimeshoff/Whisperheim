## ADRs scoped to this BC

<!-- adr-local:start -->
- **ADR-0012** -- Pin sherpa-onnx to an exact version (1.13.8) and drop the unused Microsoft.ML.OnnxRuntime package so its floating onnxruntime.dll can never shadow the runtime sherpa bundles; any future ORT reference must be pinned to sherpa's bundled version -- `../../decisions/0012-pin-sherpa-onnx-exact-drop-unused-onnxruntime-package.md`
- **ADR-0010** -- Keep-model-loaded is a user-facing on/off gate on the existing idle-unload, not a new lifecycle mode -- `../../decisions/0010-keep-model-loaded-user-toggle.md`
- **ADR-0007** -- Notify-only in-app auto-update via a Velopack gateway seam + status-footer signal -- `../../decisions/0007-notify-only-in-app-update-via-velopack.md`
- **ADR-0006** -- Recognizer lifecycle ships lazy-on, and decode self-heals so every consumer survives an idle-unload -- `../../decisions/0006-lazy-on-recognizer-lifecycle-and-self-healing-decode.md`
- **ADR-0005** -- GO on idle-unload of the Parakeet recognizer — Dispose returns ~680 MB, reload is a fixed ~4 s -- `../../decisions/0005-idle-unload-of-parakeet-recognizer-go.md`
- **ADR-0004** -- Working-set trim after model load and on idle (the "trim" half of compact-then-trim) -- `../../decisions/0004-working-set-trim-after-load-and-on-idle.md`
- **ADR-0003** -- One-shot LOH-compacting GC after startup (precursor to the working-set trim) -- `../../decisions/0003-one-shot-startup-loh-compaction.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
- **Velopack in-app auto-update via GitHub Releases** (2026-06-29) — how to add the runtime update-check the existing `vpk`/GitHub-Releases pipeline already publishes for: `UpdateManager` + `GithubSource` against the public repo, a notify-only "new version available" UX that applies on quit (not forced restart), the `IsInstalled` dev-machine guard, the 60 req/hr unauthenticated GitHub limit, and why unsigned releases still update fine. API verified against Velopack 1.2.0 docs; 0.0.1298-exact signatures marked unverified. — `../../research/velopack-in-app-update-github-2026-06-29.md`
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
- **idle-memory-footprint** -- How WhisperHeim keeps its resident RAM low while idle without breaking instant Ctrl+Win dictation (synthesizes ADRs 0002–0005 + the RAM-optimization task set) -- `concepts/idle-memory-footprint.md`
<!-- concepts:end -->


## Pointers

- BC README (ubiquitous language, invariants): `README.md`
- Task board (tasks by status) for this BC: `../../../board/infrastructure/INDEX.md`
