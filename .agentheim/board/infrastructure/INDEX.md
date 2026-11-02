# infrastructure -- Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 0
- **Todo:** 0
- **Doing:** 0
- **Done:** 10
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- no tasks in doing -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **infrastructure-anvty** — Upgrade sherpa-onnx to 1.13.8 (carries the NemoNormalizePerFeature fix that silently empties quiet dictations), drop the unused Microsoft.ML.OnnxRuntime package, and pin both native packages to exact versions instead of `1.*` (chore) — `done/infrastructure-anvty-upgrade-sherpa-onnx-drop-ort-package.md`
- **infrastructure-n3p8w** — Keep the transcription model loaded — user-toggleable idle-unload (tray + settings) (feature) — `done/infrastructure-n3p8w-keep-model-loaded-toggle.md`
- **infrastructure-v8k2m** -- In-app auto-update — notify-only "new version available" via Velopack + GitHub Releases -- `done/infrastructure-v8k2m-in-app-auto-update-notify.md`
- **infrastructure-p4w7n** -- Source the displayed app version from the packed release version (dictation, settings, about pages) -- `done/infrastructure-p4w7n-version-from-packed-release.md`
- **infrastructure-q4t8m** -- "Warming up" overlay state when an utterance outruns the model load -- `done/infrastructure-q4t8m-warming-up-overlay-state.md`
- **infrastructure-d2v7n** -- Lazy-load + keep-warm + idle-unload of the Parakeet model — core lifecycle -- `done/infrastructure-d2v7n-lazy-load-keep-warm-idle-unload.md`
- **infrastructure-k9m3p** -- Spike — does disposing the Parakeet recognizer return RAM, and how fast does it reload? (GO for d2v7n) -- `done/infrastructure-k9m3p-model-unload-reload-spike.md`
- **infrastructure-w7k9p** -- Trim Windows working set after model load and on idle -- `done/infrastructure-w7k9p-trim-working-set.md`
- **infrastructure-g3n5t** -- Aggressive GC + LOH compaction once after startup -- `done/infrastructure-g3n5t-startup-gc-loh-compaction.md`
- **infrastructure-h4m2q** -- Switch Server GC → Workstation GC + concurrent -- `done/infrastructure-h4m2q-workstation-gc.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
<!-- no tasks in backlog -->
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/infrastructure/INDEX.md`
