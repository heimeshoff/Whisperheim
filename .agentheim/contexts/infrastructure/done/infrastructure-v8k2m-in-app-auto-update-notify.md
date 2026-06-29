---
id: infrastructure-v8k2m
title: In-app auto-update — notify-only "new version available" via Velopack + GitHub Releases
status: done
type: feature
context: infrastructure
created: 2026-06-29
completed: 2026-06-29
depends_on: []
blocks: []
tags: [velopack, auto-update, github-releases, distribution, tray, update-manager]
related_adrs: [0007]
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
   and then **surface the signal in the always-visible bottom status footer**
   (`SttStatusFooter` in `MainWindow.xaml`, Grid.Row=3) — see the resolved UX
   decision below — *without* forcing a restart.
5. Apply (resolved UX): rely on Velopack's **default auto-apply-on-next-launch**
   for the zero-friction path (do **not** disable it), **and** offer an explicit
   **"Restart & update now"** action in the footer that calls
   `ApplyUpdatesAndRestart(update.TargetFullRelease)` (applies + relaunches
   immediately) for the user who wants it now.

The full implementation shape, version-accurate API surface, and gotchas are in
the research report `velopack-in-app-update-github-2026-06-29.md`.

## Acceptance criteria

- [ ] An installed copy detects a newer GitHub Release (freshly-pushed `v*` tag)
      and surfaces a visible "new version available" indication. The detection is
      automatic from the `RELEASES`/nupkg SemVer vs. the running build — no pipeline
      change required.
- [ ] **The signal lives in the always-visible bottom status footer**
      (`SttStatusFooter`, `MainWindow.xaml` Grid.Row=3) — e.g. an "Update ready:
      vX.Y" element shown alongside the existing STT-API status, hidden when no
      update is staged. It is **notify-only**: the user can keep working (keep
      dictating); the app does **not** force an immediate restart on detecting or
      downloading an update. No toast, no tray balloon, no modal.
- [ ] The footer exposes an explicit **"Restart & update now"** affordance that,
      on click, calls `ApplyUpdatesAndRestart` (applies the staged update and
      relaunches). This is the only path that restarts, and only on explicit click.
- [ ] If the user never clicks, the staged update **auto-applies on next launch**
      (Velopack default — `SetAutoApplyOnStartup` is left enabled / not called).
      No silent restart while the app is running.
- [ ] The update download happens silently in the background and does not block or
      stutter the UI thread.
- [ ] Running unpacked (dev: `publish.ps1` output / `dotnet run`) does **not**
      throw and does **not** show a bogus update indicator — the `IsInstalled` guard
      makes update checks a clean no-op and the footer element stays hidden.
- [ ] Polling is gentle (startup + multi-hour timer), and transient failures (no
      internet, GitHub unreachable, partial download) are swallowed/logged and never
      crash the app or nag the user.
- [ ] The user's `%APPDATA%` data (recordings, settings, downloaded ONNX models,
      FFmpeg) is untouched by an update — confirm the install-dir swap leaves the
      out-of-install-dir model/data files in place.

## Notes

- **UX decision — RESOLVED (refine, 2026-06-29):**
  - *Signal placement:* the **bottom status footer** (`SttStatusFooter` in
    `MainWindow.xaml`, the always-visible STT-API status bar at Grid.Row=3), not a
    toast/tray balloon. The maintainer's call: fold the "Update ready: vX.Y" signal
    into that persistent bar so it's noticeable but never interrupts. The footer
    `Border`/`StackPanel` is the insertion point; add a collapsed-by-default update
    element (icon + version + the restart action) that flips visible once a download
    is staged. Keep it visually consistent with the existing Fluent/WPF-UI footer
    (same fonts/opacity as `SttStatusFooter`'s children); there is no
    `contexts/design-system/` BC, so no styleguide gate applies.
  - *Apply mode:* **auto-apply-on-next-launch (default, kept) + explicit "Restart &
    update now"** in the footer. Zero-friction for the passive user; immediate for
    the impatient one. Do not add an "install on quit only" / consent-gated path.
- **Primary reference:** `.agentheim/knowledge/research/velopack-in-app-update-github-2026-06-29.md`
  (passed the review gate, iteration 2). It contains a minimal correct C# sketch,
  the notify/apply pattern, and the guards.
- **⚠️ Verify against the pinned build first.** The report's API signatures were
  read from Velopack's **1.2.0** docs (no 0.0.1298-stamped reference page exists)
  and are *assumed* stable across 0.0.x→1.x. Before coding, confirm via
  IntelliSense / a decompile of the restored `Velopack.dll` (0.0.1298): (a) the
  `UpdateInfo`→`VelopackAsset` apply/download overloads — the sketch sidesteps this
  by passing `update.TargetFullRelease`; (b) the exact name of the
  auto-apply-on-startup toggle (`SetAutoApplyOnStartup`?) — only needed to confirm
  the default-on behaviour we rely on, since we deliberately do **not** disable it.
  If they differ, adjust or bump the `Velopack` package.
- **Unsigned is fine for updating** — Velopack's update integrity is hash-based,
  independent of Authenticode. The deferred code-signing (`main-115`) only affects
  the SmartScreen warning on *first install*, not in-app updates.
- **Reuse the current-version provider from `infrastructure-p4w7n`.** p4w7n (now
  **done**) introduced a shared `IAppVersionProvider` reading Velopack's installed
  version. The footer's "you're on vX" half should bind to that existing provider;
  the updater only needs to add the "vY is available" half. No hard dependency
  (both derive from the same Velopack `--packVersion`, so they agree by
  construction), but reuse the shared provider rather than adding a second version
  reader.
- **Cross-BC prior art** (release pipeline, in the `main` BC, not same-BC so not in
  `prior_art`): `main-107` (Velopack bootstrap), `main-111` (GitHub Actions release
  workflow), `main-114` (pack dry run), `main-115` (code-signing slot). Runbook:
  `docs/release.md`.
- **Sibling task:** `infrastructure-p4w7n` (source the *displayed* version from the
  packed release version) — **done**. Shared the "authoritative current version =
  the Velopack packed version from the tag" concept; its `IAppVersionProvider`
  serves the footer's current-version half here.
- **Decision recorded:** ADR-0007 (`.agentheim/knowledge/decisions/0007-notify-only-in-app-update-via-velopack.md`).

## Outcome

Implemented a notify-only in-app updater over Velopack `0.0.1298` against the public
GitHub Releases feed. All acceptance criteria met.

- **Gateway seam** `IUpdateGateway` (`src/WhisperHeim/Services/Update/IUpdateGateway.cs`)
  isolates all Velopack/network contact; `VelopackUpdateGateway` is the real impl over
  `UpdateManager` + `GithubSource(repo, null, false)`, holding the pending `UpdateInfo`
  between check/download/apply.
- **`UpdateService`** (UI-free, fully unit-tested) drives check → silent
  `DownloadUpdatesAsync` → stage → raise `UpdateStaged`. Guards on `IsInstalled`
  (dev/unpacked = clean no-op), swallows transient failures, gentle poll (startup + 6 h
  timer), idempotent on an already-staged version. **Never** auto-applies/restarts.
- **Footer signal**: `MainWindow.xaml` footer restructured into a Grid; a collapsed
  `UpdateReadyPanel` ("Update ready: vX.Y" + "Restart & update now" `ui:Button`) flips
  visible on staging. App-owned `UpdateService` (constructed + `Start()`ed in
  `App.StartupCore`) so it polls on start-minimized and a staged update surfaces when the
  window later opens. The restart button is the only restart path, only on click;
  un-clicked, Velopack auto-applies on next launch (default, never disabled).
- **API verified by reflection** against the restored `Velopack.dll` 0.0.1298 (not the
  1.2.0 docs): apply takes a `VelopackAsset` → pass `UpdateInfo.TargetFullRelease`;
  `CheckForUpdatesAsync` has no `CancellationToken` overload → cancel at the boundary.
- **Tests:** 9 new in `tests/WhisperHeim.Tests/UpdateServiceTests.cs` (fake gateway):
  not-installed no-op, download+stage+notify, no-update, never-restarts, check-failure
  swallowed, download-failure swallowed, explicit restart applies, restart-when-nothing
  no-op, idempotent re-check. Full suite 191/191 green; main project builds clean.

Key files: `src/WhisperHeim/Services/Update/{IUpdateGateway,UpdateService,UpdateStagedEventArgs,VelopackUpdateGateway}.cs`,
`src/WhisperHeim/MainWindow.xaml(.cs)`, `src/WhisperHeim/App.xaml.cs`,
`tests/WhisperHeim.Tests/UpdateServiceTests.cs`, ADR-0007.
