---
id: infrastructure-p4w7n
title: Source the displayed app version from the packed release version (dictation, settings, about pages)
status: todo
type: refactor
context: infrastructure
created: 2026-06-29
completed:
depends_on: []
blocks: []
tags: [velopack, versioning, release-tag, ui, about-page]
related_adrs: []
related_research: [velopack-in-app-update-github-2026-06-29]
prior_art: []
---

## Why

The app shows its version as a **hardcoded `v1.0`** literal in three XAML pages —
`DictationPage.xaml`, `GeneralPage.xaml` (the Settings page), and
`AboutPage.xaml`. It does not reflect the actual release. When the maintainer
tags `v0.3.1` and ships it, every page still says "v1.0", which is wrong, and
will be actively misleading once in-app auto-update (`infrastructure-v8k2m`)
lands and users are on different builds.

The version a user sees should be **the version they're actually running** —
i.e. the release tag the build was packed from.

## What

Replace the three hardcoded `v1.0` TextBlocks with a single runtime-sourced
version value derived from the packed release version (which the CI sets from the
`v*` git tag via `vpk pack --packVersion`).

Resolve, as part of this task, **where the runtime reads the version from** — the
source-of-truth decision (see Notes). Surface it through one shared provider so
all three pages bind to the same value and a future fourth surface can't drift.

## Acceptance criteria

- [ ] The Dictation, Settings (`GeneralPage`), and About pages display the running
      build's actual version, sourced at runtime — not a hardcoded string. The three
      `Text="v1.0"` literals (`DictationPage.xaml:74`, `GeneralPage.xaml:35`,
      `AboutPage.xaml:35`) are gone.
- [ ] On an installed (Velopack-packed) build, the displayed version is the release
      tag the build was packed from (a build packed from `v0.3.1` shows `v0.3.1`),
      read from Velopack's installed-version metadata at runtime.
- [ ] There is a **single** provider for the version; the three pages bind to it,
      none re-derives it. Adding a fourth surface means binding to the provider, not
      copy-pasting a literal.
- [ ] When run unpacked (development / `dotnet run` / raw `publish` output), the
      provider returns `"dev"` — never crashes, and never shows a stale `v1.0`/`v1.0.0`
      presented as a real version.
- [ ] The `v`-prefix and formatting are produced by the provider and identical
      across all three pages (the pages bind a single already-formatted string).
- [ ] The provider's resolve + format + dev-fallback logic is covered by unit tests
      (it is UI-free; the three XAML bindings are verified by code-reading per the
      repo's documented lack of WPF UI-test infra).

## Notes

- **Source-of-truth decision — RESOLVED (refine, 2026-06-29):** read from
  **Velopack's installed-version metadata** (`VelopackLocator` /
  `UpdateManager.CurrentVersion` — the `--packVersion` = `v*` tag). **No release.yml
  change** (maintainer's call — keep the pipeline untouched). This is a small,
  generic-BC refactor; no ADR.
  - **Consequence that shapes the implementation:** because the pipeline is left
    unchanged, the assembly informational version stays the default `1.0.0` forever
    — the publish step never injects the tag. So an "assembly version" fallback tier
    would only ever surface a misleading `v1.0.0`, which the dev-fallback AC forbids.
    The honest logic therefore collapses to **two tiers, not three**:
    `installed → "v" + Velopack version; else → "dev"`. Do **not** read the assembly
    version as a middle fallback under this decision.
- **Read the version without building an `UpdateManager`.** `UpdateManager.CurrentVersion`
  requires constructing an `UpdateManager`, which needs an `IUpdateSource`
  (`GithubSource`) — that couples a pure version *display* to the updater and would
  imply network config. Prefer `VelopackLocator.GetDefault(...)` to read the locally
  installed version with no source and no network. (Confirm the exact member —
  `CurrentVersion` / `CurrentlyInstalledVersion`, a `SemanticVersion?` — via
  IntelliSense against Velopack `0.0.1298`; the research report's signatures are
  marked ⚠️ unverified for that exact build.) Returns `null` when unpacked → `"dev"`.
- **Suggested shape:** an `IAppVersionProvider` with a single
  `string DisplayVersion { get; }` (e.g. `"v0.3.1"` or `"dev"`), one implementation
  that wraps the `VelopackLocator` read + null→`"dev"` + `v`-prefix formatting,
  registered once and bound by all three pages. The Velopack lookup can be done once
  and cached (the installed version doesn't change within a process run).
- **Sibling task:** `infrastructure-v8k2m` (in-app auto-update). Independent — do
  **not** block on it. Both derive the current version from the same Velopack
  `--packVersion`, so the updater's "you're on X, Y is available" and these pages'
  "vX" agree by construction even with separate readers. If v8k2m lands first or
  alongside, it may reuse this provider; no hard dependency either way.
- No `contexts/design-system/` BC exists in this project, so no styleguide gate
  applies; keep the version label visually identical to today (same font/size/
  colour) — only its *source* changes.
- **Reference:** research report `velopack-in-app-update-github-2026-06-29` §3
  (how the current version is detected; `IsInstalled` / `CurrentVersion` gating).
