---
id: infrastructure-v8k2m
title: In-app auto-update — notify-only "new version available" via Velopack + GitHub Releases
status: backlog
type: feature
context: infrastructure
created: 2026-06-29
completed:
depends_on: []
blocks: []
tags: [velopack, auto-update, github-releases, distribution, tray, update-manager]
related_adrs: []
related_research: [velopack-in-app-update-github-2026-06-29, auto-update-and-distribution]
prior_art: []
---

## Why

WhisperHeim already *publishes* updates: every `v*` tag runs the GitHub Actions
release workflow, which `vpk pack`s the app and uploads `Setup.exe`, the full +
delta `.nupkg`, and the `RELEASES` manifest to a public GitHub Release. The
distribution half is done — what's missing is the **client side**: nothing in the
running app ever looks at GitHub to notice that the maintainer bumped a tag. So a
user who installed v0.1 stays on v0.1 forever unless they manually re-download.

The maintainer's ask: when a new version is tagged, an installed copy should
**surface that a new version is available** and be able to update itself —
without yanking the app out from under the user mid-dictation.

## What

Add a small client-side update path on top of the existing Velopack bootstrap
(`VelopackApp.Build().Run()` is already wired in `Program.cs`; `Velopack`
0.0.1298 is already referenced). A new `UpdateService` that:

1. Builds `new UpdateManager(new GithubSource("https://github.com/heimeshoff/WhisperHeim", null, false))`
   — `null` token (public repo), `prerelease: false` (single default channel,
   matching the pipeline).
2. **Guards on `UpdateManager.IsInstalled`** and no-ops cleanly when the app runs
   unpacked (dev runs via `publish.ps1` / `dotnet run` throw
   `NotInstalledException` otherwise).
3. `CheckForUpdatesAsync()` on startup and on a **gentle periodic timer** (multi-hour;
   unauthenticated GitHub REST is 60 req/hr/IP — do not poll aggressively), off
   the UI thread.
4. On a found update: **download silently in the background** (`DownloadUpdatesAsync`)
   and **notify** the user that a new version is available — *without* forcing a
   restart.
5. Apply **on the user's terms**: either install-on-quit
   (`WaitExitThenApplyUpdates(asset, silent: true, restart: false)` or
   auto-apply-on-next-launch), or an explicit "Restart now" affordance
   (`ApplyUpdatesAndRestart`).

The full implementation shape, version-accurate API surface, and gotchas are in
the research report `velopack-in-app-update-github-2026-06-29.md`.

## Acceptance criteria

- [ ] An installed copy detects a newer GitHub Release (freshly-pushed `v*` tag)
      and surfaces a visible "new version available" indication. The detection is
      automatic from the `RELEASES`/nupkg SemVer vs. the running build — no pipeline
      change required.
- [ ] The notification is **notify-only**: the user can keep working (keep
      dictating); the app does **not** force an immediate restart on detecting or
      downloading an update.
- [ ] The new version installs on the user's terms — at minimum, applied on next
      quit/launch; ideally with an explicit "Restart & update now" action.
- [ ] The update download happens silently in the background and does not block or
      stutter the UI thread.
- [ ] Running unpacked (dev: `publish.ps1` output / `dotnet run`) does **not**
      throw and does **not** show a bogus update prompt — the `IsInstalled` guard
      makes update checks a clean no-op.
- [ ] Polling is gentle (startup + multi-hour timer), and transient failures (no
      internet, GitHub unreachable, partial download) are swallowed/logged and never
      crash the app or nag the user.
- [ ] The user's `%APPDATA%` data (recordings, settings, downloaded ONNX models,
      FFmpeg) is untouched by an update — confirm the install-dir swap leaves the
      out-of-install-dir model/data files in place.

## Notes

- **Primary reference:** `.agentheim/knowledge/research/velopack-in-app-update-github-2026-06-29.md`
  (passed the review gate, iteration 2). It contains a minimal correct C# sketch,
  the notify/apply-on-quit pattern, and the guards.
- **⚠️ Verify against the pinned build first.** The report's API signatures were
  read from Velopack's **1.2.0** docs (no 0.0.1298-stamped reference page exists)
  and are *assumed* stable across 0.0.x→1.x. Before coding, confirm via
  IntelliSense / a decompile of the restored `Velopack.dll` (0.0.1298): (a) the
  `UpdateInfo`→`VelopackAsset` apply/download overloads — the sketch sidesteps this
  by passing `update.TargetFullRelease`; (b) the exact name of the
  auto-apply-on-startup toggle (`SetAutoApplyOnStartup`?). If they differ, adjust
  or bump the `Velopack` package.
- **Unsigned is fine for updating** — Velopack's update integrity is hash-based,
  independent of Authenticode. The deferred code-signing (`main-115`) only affects
  the SmartScreen warning on *first install*, not in-app updates.
- **Open UX decision (refine before work):** *where* the "new version available"
  signal appears. Options for a WPF tray app — a tray balloon/toast, an in-app
  banner on the Dictation/Settings/About pages, and/or an "Update available →
  Restart to update" control on the About page. Pick one (or a primary +
  secondary) during refinement; the styleguide note — there is no
  `contexts/design-system/` BC in this project, so no styleguide gate applies, but
  keep the surface consistent with the existing Fluent/WPF-UI pages.
- **Cross-BC prior art** (release pipeline, in the `main` BC, not same-BC so not in
  `prior_art`): `main-107` (Velopack bootstrap), `main-111` (GitHub Actions release
  workflow), `main-114` (pack dry run), `main-115` (code-signing slot). Runbook:
  `docs/release.md`.
- **Sibling task:** `infrastructure-p4w7n` (source the *displayed* version from the
  packed release version). Independent of this task, but shares the "authoritative
  current version = the Velopack packed version from the tag" concept; a shared
  current-version provider could serve both.
