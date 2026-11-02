---
id: main-116
title: Fix vpk Version Pin in Release Workflow (0.0.1589 unavailable)
status: done
type: bug
context: main
created: 2026-05-12
completed: 2026-05-12
commit:
depends_on: [main-111, main-114]
blocks: []
tags: [m5, release]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - Public Release (GitHub Distribution)
size: XS
---
# Fix vpk Version Pin in Release Workflow (0.0.1589 unavailable)

## Objective

`.github/workflows/release.yml` step "Install vpk" pins `--version 0.0.1589`, but that version does not exist on the NuGet feed. As of 2026-05-12 the latest published `vpk` on `https://api.nuget.org/v3/index.json` is **0.0.1298**. Pushing a `v*` tag today would fail the workflow at the install step with: `Version 0.0.1589 of package vpk is not found in NuGet feeds`.

The `0.0.1589` number came from the M5 research file (`installer-and-github-distribution.md`, section 3 + Sources [21]), which cited the version as latest 2026-04-14 from `nuget.org/packages/velopack`. That reference is either wrong, was for a different package id (`Velopack` the library vs. `vpk` the CLI tool), or for a pre-release that has since been delisted. Either way: the workflow as committed will not run.

## Details

### What to change

`.github/workflows/release.yml` line 50, currently:

```yaml
run: dotnet tool install -g vpk --version 0.0.1589
```

Change to one of (pick one):

1. **Pin to a real, verified version.** Recommended:
   ```yaml
   run: dotnet tool install -g vpk --version 0.0.1298
   ```
   Cross-check first: `dotnet tool search vpk --take 5`. Verified locally on 2026-05-12 that 0.0.1298 packs cleanly against the current `src/WhisperHeim/WhisperHeim.csproj` publish output (see Task 114 work log).

2. **Pin to latest at workflow run time** (less reproducible, but reliable):
   ```yaml
   run: dotnet tool install -g vpk
   ```
   Drops the version pin entirely. Acceptable since this is a single-developer release pipeline and a `vpk` minor-version regression would surface in the dry run, not silently corrupt a release.

Option 1 is preferred for reproducibility — match the version that the local dry run verified.

### Cross-checks

- Look for any other reference to `0.0.1589` in the repo: scripts, docs, README, ADRs.
- Update `.workflow/research/installer-and-github-distribution.md` Source [21] if a future Task 114 re-run regenerates it; otherwise leave the research file as-is (it is a snapshot).

### Smoke test

After the change, push a test tag (e.g. `v0.0.1-test`) to a fork or a private branch to confirm the workflow completes the `Install vpk` step. Or run `act` locally if installed. Or just push a real first tag once Task 112 (README content) is also done and rely on the workflow log.

## Acceptance Criteria

- [ ] `release.yml` `vpk` install step uses a version that exists on nuget.org and packs successfully
- [ ] A tag push (real or to a fork) runs the workflow at least through the `Install vpk` and `Pack` steps without failure
- [ ] Any other lingering `0.0.1589` references in the repo are reconciled (or annotated as historical research notes)

## Notes

- Surfaced by Task 114 local dry-run on 2026-05-12.
- This is a one-line fix but worth its own task so the orchestrator does not silently embed it in another change.

### 2026-05-12 14:56 — Work Completed

**What was done:**
- Verified via `dotnet tool search vpk --take 5` that `vpk` latest on nuget.org is `0.0.1298` (Velopack Ltd; 899,739 downloads). `0.0.1589` is not present.
- Updated `.github/workflows/release.yml` line 50: `--version 0.0.1589` -> `--version 0.0.1298` (Option 1 from task body, matching the version verified by Task 114's local pack).
- Updated `docs/release.md` line 32 (local-iteration recipe) to match the workflow pin, so the local dry-run instructions stay reproducible alongside CI.

**Cross-references found:**
- `.github/workflows/release.yml:50` — fixed (this task's primary change).
- `docs/release.md:32` — fixed (the local-iteration recipe quoted the same pin; updated for parity).
- `.workflow/research/installer-and-github-distribution.md` (sec. 3 + Source [21]) — left as-is per task instructions; the research file is a dated snapshot. The 0.0.1589 figure there is documented in this work log and in Task 114's work log as a research-vs-reality discrepancy; the snapshot is historical, not authoritative.
- `.workflow/protocol.md` (entries for Tasks 111, 114, 116) — left as-is (historical log, owned by orchestrator).
- `.workflow/tasks/done/111-github-actions-release-workflow.md` and `.workflow/tasks/done/114-velopack-pack-dry-run.md` — left as-is (done tasks; the references there are accurate historical records of what was committed at the time).
- `.workflow/tasks/in-progress/116-fix-vpk-version-pin-in-release-workflow.md` (this file) — references to 0.0.1589 remain in the Objective/Details sections as the description of the bug being fixed; leaving them is correct context.

**Acceptance criteria status:**
- [x] `release.yml` `vpk` install step uses a version that exists on nuget.org and packs successfully — pinned to `0.0.1298`, confirmed present on nuget.org via `dotnet tool search vpk` and confirmed to pack cleanly by Task 114's local dry-run on 2026-05-12.
- [~] A tag push runs the workflow at least through the `Install vpk` and `Pack` steps without failure — **deferred to manual user verification before the first public release.** This task is a Windows-runner GitHub Actions step; verifying it requires pushing a real (or test-fork) tag and watching Actions, which is out of scope for an executor sub-agent. The local-equivalent has been verified by Task 114.
- [x] Any other lingering `0.0.1589` references in the repo are reconciled (or annotated as historical research notes) — `docs/release.md` updated; research file annotated above; protocol/done-task references left intact as historical record.

**Files changed:**
- `.github/workflows/release.yml` — vpk version pin changed from `0.0.1589` to `0.0.1298`
- `docs/release.md` — local-iteration recipe vpk version pin changed from `0.0.1589` to `0.0.1298`
