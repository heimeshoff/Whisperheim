---
id: infrastructure-p4w7n
title: Source the displayed app version from the packed release version (dictation, settings, about pages)
status: backlog
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
      build's actual version, sourced at runtime — not a hardcoded string.
- [ ] The displayed version corresponds to the release tag the build was packed
      from (e.g. a build packed from `v0.3.1` shows `0.3.1` / `v0.3.1`).
- [ ] There is a **single** source/provider for the version; the three pages do not
      each re-derive it. Adding a new surface means binding to the provider, not
      copy-pasting a literal.
- [ ] A graceful fallback when no packed version is available (e.g. running
      unpacked in development) — a sensible dev placeholder, never a crash and never
      a stale wrong number presented as real.
- [ ] The `v`-prefix / formatting is consistent across all three pages.

## Notes

- **Current literals:** `Text="v1.0"` at `DictationPage.xaml:74`,
  `GeneralPage.xaml:35`, `AboutPage.xaml:35`.
- **Source-of-truth decision to make during refinement/work** — two viable
  sources, pick one (the research report's §3 "how newer version is detected" is
  the relevant reference):
  1. **Velopack's current-version API** (`UpdateManager.CurrentVersion` /
     `VelopackLocator`) — authoritative (it's literally the `--packVersion` =
     tag), but only populated when **installed**; needs a dev fallback.
  2. **Assembly informational version attribute** read via reflection — works in
     dev too, but **only reflects the tag if the CI injects it**. The release
     workflow currently passes the tag to `vpk pack --packVersion` but does **not**
     pass `-p:Version=`/`-p:InformationalVersion=` to `dotnet publish`, so the
     assembly version is still the default `1.0.0` (which is exactly why the pages
     reading a hardcoded `v1.0` looked "fine"). Choosing this path means a
     one-line `release.yml` change to inject the tag into the publish step.
  - A reasonable answer: Velopack version when installed, assembly/`"dev"`
    fallback otherwise — decide in refinement.
- **Sibling task:** `infrastructure-v8k2m` (in-app auto-update). Independent, but
  both want an authoritative "current version" — consider a shared provider so the
  updater's "you're on X, Y is available" and the pages' "vX" agree.
- No `contexts/design-system/` BC exists in this project, so no styleguide gate
  applies; keep the version label visually identical to today (same font/size/
  colour) — only its *source* changes.
