---
id: main-111
title: GitHub Actions Release Workflow (Tag-Triggered Velopack Build)
status: done
type: feature
context: main
created: 2026-05-12
completed: 2026-05-12
commit:
depends_on: [main-107, main-109]
blocks: []
tags: [m5, release]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - Public Release (GitHub Distribution)
size: Medium
---
# GitHub Actions Release Workflow (Tag-Triggered Velopack Build)

## Objective

Add `.github/workflows/release.yml` that publishes a Velopack-packaged WhisperHeim installer to GitHub Releases automatically on every `v*` tag push. Output: a `Setup.exe`, a full Velopack package, a delta package (against the previous release), and a `RELEASES` manifest — all uploaded to a GitHub Release named after the tag.

## Details

### Workflow shape

```yaml
name: Release
on:
  push:
    tags: ['v*']

permissions:
  contents: write   # required for vpk upload github to create/update Releases

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }

      - name: Extract version from tag
        id: ver
        shell: pwsh
        run: |
          $tag = "${{ github.ref_name }}".TrimStart('v')
          "version=$tag" >> $env:GITHUB_OUTPUT

      - name: Publish (self-contained, ReadyToRun, no trimming)
        run: dotnet publish src/WhisperHeim/WhisperHeim.csproj `
             -c Release -r win-x64 --self-contained `
             -p:PublishReadyToRun=true -o publish

      - name: Install vpk
        run: dotnet tool install -g vpk

      - name: Download previous releases (for delta computation)
        run: vpk download github `
             --repoUrl https://github.com/${{ github.repository }} `
             --token ${{ secrets.GITHUB_TOKEN }}

      - name: Pack
        run: vpk pack `
             --packId WhisperHeim `
             --packVersion ${{ steps.ver.outputs.version }} `
             --packDir publish `
             --mainExe WhisperHeim.exe `
             --packTitle "WhisperHeim" `
             --packAuthors "Marco Heimeshoff"
             # TODO post-UG: add --signParams or --azureTrustedSignFile here

      - name: SHA-256 of Setup.exe
        id: hash
        shell: pwsh
        run: |
          $hash = (Get-FileHash -Algorithm SHA256 Releases/*-Setup.exe).Hash
          "sha256=$hash" >> $env:GITHUB_OUTPUT

      - name: Upload to GitHub Release
        run: vpk upload github `
             --repoUrl https://github.com/${{ github.repository }} `
             --tag v${{ steps.ver.outputs.version }} `
             --releaseName "WhisperHeim ${{ steps.ver.outputs.version }}" `
             --publish `
             --token ${{ secrets.GITHUB_TOKEN }}
```

### What gets uploaded

Velopack's `vpk upload github` attaches the contents of `Releases/` to the GitHub Release:
- `WhisperHeim-{version}-full.nupkg` (full package)
- `WhisperHeim-{version}-delta.nupkg` (delta from previous release, if a previous release was downloaded)
- `WhisperHeim-{version}-win-Setup.exe` (single-file installer — primary user download)
- `RELEASES` (Velopack manifest used by auto-update)

### Pre-release / channel discipline

For now, every `v*` tag goes to one Release channel. When beta channels are needed later, the workflow can be parameterised on the tag shape (e.g. `v*-beta` → beta channel). Out of scope for this task; mention in the inline comment.

### Local dev iteration

Document in a short `docs/release.md` (or extend the README) how to run the same pipeline locally:
```pwsh
dotnet publish src/WhisperHeim/WhisperHeim.csproj -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true -o publish
vpk pack --packId WhisperHeim --packVersion 0.0.1-local --packDir publish --mainExe WhisperHeim.exe
```
…producing the same artifacts under `Releases/` for manual smoke-testing before tagging.

### Signing slot

Leave a TODO comment in the `vpk pack` step where signing flags will go. No structural change required to flip on later. See Task 115.

## Acceptance Criteria

- [ ] `.github/workflows/release.yml` exists with the structure above
- [ ] Triggered on `v*` tag push
- [ ] Publishes self-contained .NET 9 win-x64 with `PublishReadyToRun=true` and no trimming
- [ ] `vpk download github` runs before `vpk pack` so the first delta package is computed against the prior release (no-ops gracefully on the first ever release)
- [ ] `vpk pack` produces `Setup.exe` + full + delta + `RELEASES`
- [ ] SHA-256 of `Setup.exe` computed and exposed as a step output (consumed by Task 114 README updates)
- [ ] `vpk upload github` attaches all artifacts to the matching Release
- [ ] Signing TODO comment present at the `vpk pack` step
- [ ] First end-to-end run on a real tag (e.g. `v0.0.1`) succeeds and the Release is downloadable + installable
- [ ] Documented in `docs/release.md` or README how to run locally

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §3 "Concrete Velopack pipeline" + Implications #5.
- Reference: [GitHub Actions | Velopack](https://docs.velopack.io/distributing/github-actions).
- The bundled FFmpeg step from the original research is **removed** — Task 110's user-install strategy means the workflow has no FFmpeg step at all.
- The bundled small models are committed to the repo (Task 109), so this workflow doesn't need a model-fetch step either. Everything required to pack ships with the source.
- Velopack's `vpk pack` is incremental-signing-aware — when we add signing (Task 115), it signs both app binaries and Velopack's own `Update.exe` / `Setup.exe` in the correct order. Don't add a post-build `signtool` step.

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-12 14:45 — Work Completed

**What was done:**
- Created `.github/workflows/` and `docs/` directories (neither existed before).
- Authored `.github/workflows/release.yml`: tag-triggered (`v*`) Velopack pipeline on `windows-latest` — checkout → setup-dotnet 9.0.x → version extract → `dotnet publish` self-contained win-x64 ReadyToRun no-trim → `dotnet tool install -g vpk --version 0.0.1589` (pinned) → `vpk download github` (continue-on-error for first-ever release) → `vpk pack` (with signing TODO comment intact) → SHA-256 of Setup.exe captured as step output and written to job summary → `vpk upload github --publish`.
- Switched the PowerShell `>> $env:GITHUB_OUTPUT` redirections to `Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8` for robust UTF-8 (no BOM) writes on `pwsh` runners; `>>` works in pwsh too but `Out-File -Append` is the safer idiom and avoids accidental UTF-16 from Windows PowerShell mismatches.
- Validated YAML via Python's `yaml.safe_load` — parses cleanly, 10 steps under `jobs.build`, on/permissions/jobs keys present.
- Wrote `docs/release.md` runbook covering: artifact inventory, local-iteration `pwsh` recipe (publish → install vpk → pack → run installer), tag-triggering procedure, Release-notes checklist (SmartScreen instructions + SHA-256 + SAC caveat), and the deferred-signing pointer to Task 115.
- Signing TODO comment preserved verbatim at the `vpk pack` step exactly where Task 115 (documentation-only) expects to reference it.

**Acceptance criteria status:**
- [x] `.github/workflows/release.yml` exists with the structure above — created and YAML-validated.
- [x] Triggered on `v*` tag push — `on.push.tags: ['v*']`.
- [x] Publishes self-contained .NET 9 win-x64 with `PublishReadyToRun=true` and no trimming — `dotnet publish ... -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true` (no `PublishTrimmed`).
- [x] `vpk download github` runs before `vpk pack` and no-ops gracefully on first ever release — wrapped in `continue-on-error: true`.
- [x] `vpk pack` produces Setup.exe + full + delta + RELEASES — standard vpk behavior given the args supplied.
- [x] SHA-256 of Setup.exe exposed as a step output (`steps.hash.outputs.sha256`) and surfaced in the job summary for Task 114 README updates.
- [x] `vpk upload github` attaches all artifacts to the matching Release — `--tag v{version} --releaseName ... --publish`.
- [x] Signing TODO comment present at the `vpk pack` step — see lines 60-62 of release.yml; explicitly mentions `--signParams` and `--azureTrustedSignFile` and references Task 115.
- [ ] First end-to-end run on a real `v0.0.1` tag — **DEFERRED to Task 114** (manual dry-run + first tag). Cannot be exercised from inside the subagent; local-iteration recipe in `docs/release.md` lets the user reproduce the pipeline before tagging.
- [x] Documented in `docs/release.md` how to run locally — full pwsh recipe + tag-trigger procedure included.

**Files changed:**
- `.github/workflows/release.yml` — new file, the tag-triggered Velopack release pipeline.
- `docs/release.md` — new file, release runbook with local-iteration recipe.
