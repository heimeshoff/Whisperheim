# infrastructure -- Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 1
- **Todo:** 1
- **Doing:** 0
- **Done:** 5
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **infrastructure-q4t8m** -- "Warming up" overlay state when an utterance outruns the model load -- `todo/infrastructure-q4t8m-warming-up-overlay-state.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- no tasks in doing -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **infrastructure-d2v7n** -- Lazy-load + keep-warm + idle-unload of the Parakeet model — core lifecycle -- `done/infrastructure-d2v7n-lazy-load-keep-warm-idle-unload.md`
- **infrastructure-k9m3p** -- Spike — does disposing the Parakeet recognizer return RAM, and how fast does it reload? (GO for d2v7n) -- `done/infrastructure-k9m3p-model-unload-reload-spike.md`
- **infrastructure-w7k9p** -- Trim Windows working set after model load and on idle -- `done/infrastructure-w7k9p-trim-working-set.md`
- **infrastructure-g3n5t** -- Aggressive GC + LOH compaction once after startup -- `done/infrastructure-g3n5t-startup-gc-loh-compaction.md`
- **infrastructure-h4m2q** -- Switch Server GC → Workstation GC + concurrent -- `done/infrastructure-h4m2q-workstation-gc.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **infrastructure-b3n6p** -- Make lazy-load / idle-unload configurable (lazy-vs-eager + idle timeout) -- `backlog/infrastructure-b3n6p-lazy-unload-settings.md`
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **ADR-0006** -- Recognizer lifecycle ships lazy-on, and decode self-heals so every consumer survives an idle-unload -- `../../knowledge/decisions/0006-lazy-on-recognizer-lifecycle-and-self-healing-decode.md`
- **ADR-0005** -- GO on idle-unload of the Parakeet recognizer — Dispose returns ~680 MB, reload is a fixed ~4 s -- `../../knowledge/decisions/0005-idle-unload-of-parakeet-recognizer-go.md`
- **ADR-0004** -- Working-set trim after model load and on idle (the "trim" half of compact-then-trim) -- `../../knowledge/decisions/0004-working-set-trim-after-load-and-on-idle.md`
- **ADR-0003** -- One-shot LOH-compacting GC after startup (precursor to the working-set trim) -- `../../knowledge/decisions/0003-one-shot-startup-loh-compaction.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- no research touching this BC -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
- **idle-memory-footprint** -- How WhisperHeim keeps its resident RAM low while idle without breaking instant Ctrl+Win dictation (synthesizes ADRs 0002–0005 + the RAM-optimization task set) -- `concepts/idle-memory-footprint.md`
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
