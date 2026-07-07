---
id: 0007
title: Notify-only in-app auto-update via a Velopack gateway seam + status-footer signal
scope: infrastructure
status: accepted
date: 2026-06-29
supersedes: []
superseded_by: []
related_tasks: [infrastructure-v8k2m, infrastructure-p4w7n]
related_research: [velopack-in-app-update-github-2026-06-29, auto-update-and-distribution]
---

# ADR 0007: Notify-only in-app auto-update via a Velopack gateway seam + status-footer signal

## Context
WhisperHeim already *publishes* updates (every `v*` tag runs `vpk pack` + uploads
`Setup.exe` + full/delta `.nupkg` + `RELEASES` to a public GitHub Release), but the
running app never looked at GitHub, so an installed copy stayed on its version
forever. Task `infrastructure-v8k2m` adds the client side. The app is a tray /
dictation tool that must never yank itself out from under a user mid-dictation.

Velopack `0.0.1298` is already referenced and bootstrapped (`VelopackApp.Build().Run()`
in `Program.cs`). The runtime side needs `UpdateManager` + `GithubSource`, which
(a) throw `NotInstalledException` when run unpacked (dev), (b) hit the network, and
(c) are concrete classes that are awkward to unit-test.

## Decision
1. **Notify-only, never auto-restart.** The updater drives the Velopack lifecycle
   one stage at a time — `CheckForUpdatesAsync` → silent background
   `DownloadUpdatesAsync` → *stop*. A staged update is **surfaced**, not applied.
   Two apply paths only: Velopack's **default auto-apply-on-next-launch** (left
   enabled — we deliberately never call `SetAutoApplyOnStartup(false)`) for the
   passive user, and an explicit **"Restart & update now"** footer button
   (`ApplyUpdatesAndRestart`) for the impatient one. No toast, no tray balloon, no
   modal, no install-on-quit / consent-gated path.

2. **Signal lives in the always-visible bottom status footer** (`SttStatusFooter`,
   `MainWindow.xaml` Grid.Row=3), collapsed by default, flipped visible with
   "Update ready: vX.Y" + the restart button once a download is staged. Chosen over
   a toast/balloon because the footer is persistent and non-interrupting.

3. **A gateway seam (`IUpdateGateway`) isolates all Velopack/network contact** from
   the orchestration. `UpdateService` (UI-free, fully unit-tested with a fake
   gateway) owns the check → download → stage → notify state machine, the
   `IsInstalled` guard, transient-failure swallowing, the gentle multi-hour poll
   timer, and the idempotent "already staged" short-circuit. `VelopackUpdateGateway`
   is the thin real implementation. This is why the logic is testable at all:
   `UpdateManager` itself is not mockable.

4. **App-owned, not window-owned.** `UpdateService` is constructed and `Start()`ed
   in `App.StartupCore` (like `TrayIconHost`), so it polls even on start-minimized
   and a staged update survives the window being closed. `MainWindow` reads
   `StagedVersion` on construction and subscribes to `UpdateStaged` — so an update
   staged while the window was closed still surfaces the moment it opens.

5. **Pinned-build API, verified by reflection.** The research report's signatures
   were read off Velopack's 1.2.0 docs. Reflecting the restored `0.0.1298`
   `Velopack.dll` confirmed: `ApplyUpdatesAndRestart` takes a `VelopackAsset` (there
   is **no** `UpdateInfo` extension overload in this build), so the gateway passes
   `UpdateInfo.TargetFullRelease`; `CheckForUpdatesAsync` has **no**
   `CancellationToken` overload, so cancellation is honoured at the boundary;
   `GithubSource(repoUrl, accessToken, prerelease, …)` matches `(url, null, false)`.

## Consequences
- A startup + 6-hour poll stays well within GitHub's 60 req/hr/IP unauthenticated
  cap; transient failures (offline, 403, partial download) are logged and swallowed,
  retried next cycle — never crash or nag.
- Dev/unpacked runs are a clean no-op: `IsInstalled` is `false`, the footer element
  stays collapsed, and no `NotInstalledException` fires.
- `%APPDATA%` user data (recordings, settings, ONNX models, FFmpeg) is outside the
  install dir, so the install-dir swap leaves it untouched — confirmed by the
  research report; no special handling needed.
- Unsigned releases do not affect updates (Velopack integrity is hash-based);
  code-signing (`main-115`) only affects first-install SmartScreen.
- The current-version half of the footer reuses `IAppVersionProvider`
  (`infrastructure-p4w7n`); the updater only adds the "vY is available" half — no
  second version reader.
- If Velopack is later bumped to 1.x, the gateway is the single place that touches
  the SDK; the orchestration and tests are insulated from the change.
