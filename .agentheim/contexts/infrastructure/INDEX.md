# infrastructure -- Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 1
- **Todo:** 2
- **Doing:** 0
- **Done:** 2
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **infrastructure-w7k9p** -- Trim Windows working set after model load and on idle -- `todo/infrastructure-w7k9p-trim-working-set.md`
- **infrastructure-k9m3p** -- Spike — does disposing the Parakeet recognizer return RAM, and how fast does it reload? -- `todo/infrastructure-k9m3p-model-unload-reload-spike.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- no tasks in doing -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **infrastructure-g3n5t** -- Aggressive GC + LOH compaction once after startup -- `done/infrastructure-g3n5t-startup-gc-loh-compaction.md`
- **infrastructure-h4m2q** -- Switch Server GC → Workstation GC + concurrent -- `done/infrastructure-h4m2q-workstation-gc.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **infrastructure-d2v7n** -- Lazy-load + keep-warm + idle-unload of the Parakeet model (capture-in-parallel) -- `backlog/infrastructure-d2v7n-lazy-load-keep-warm-idle-unload.md`
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **ADR-0003** -- One-shot LOH-compacting GC after startup (precursor to the working-set trim) -- `../../knowledge/decisions/0003-one-shot-startup-loh-compaction.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- no research touching this BC -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- no concept pages yet -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
