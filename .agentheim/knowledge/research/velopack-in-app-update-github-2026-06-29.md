---
topic: Velopack in-app auto-update detection and apply flow against GitHub Releases (WhisperHeim)
date: 2026-06-29
requested_by: user
related_tasks: []
---

# Research: Velopack in-app auto-update against public GitHub Releases (Velopack 0.0.1298)

## Question
Given WhisperHeim already ships with Velopack `0.0.1298` bootstrapped (`VelopackApp.Build()...Run()`) and a GitHub Actions pipeline that does `vpk pack` + `vpk upload github` (Setup.exe + full/delta `.nupkg` + `RELEASES` manifest to a public Release), how do we implement the *runtime* side: check GitHub Releases, detect a newer tag, surface a "new version available" tray notification, and apply on restart — using the version-accurate `0.0.x` API? Specifically: API surface, notify-only UX, version-detection semantics, public-repo token needs, the dev-machine not-installed gotcha, unsigned-release implications, and polling cadence/pitfalls.

## Summary
- **Feasible with the existing setup, no pipeline changes needed.** The artifacts you already publish (`RELEASES` manifest + full/delta `.nupkg` on a public GitHub Release) are exactly what the runtime `UpdateManager` + `GithubSource` consume. The remaining work is purely client-side C#: ~1 small service class plus tray wiring. [1][2][6]
- **Notify-only with apply-on-quit is a first-class supported pattern.** Split the lifecycle: `CheckForUpdatesAsync` (detect) → `DownloadUpdatesAsync` (silent background) → then *do not* call apply immediately. Either call `WaitExitThenApplyUpdates(...)` (installs after the app exits, no forced restart) or rely on Velopack's default **auto-apply-on-next-launch** (a downloaded pending update installs automatically the next time the app starts). `ApplyUpdatesAndRestart` is the "restart now" path you would gate behind a user click. [4][6]
- ⚠️ **Version provenance (UNVERIFIED for 0.0.1298).** The docs.velopack.io API reference renders against **Velopack 1.2.0** (the docs' current "latest") — every reference page footer reads "Generated from Velopack 1.2.0", NOT 0.0.1298. Your pinned `0.0.1298` exists on NuGet (published 2025-06-07) but there is no 0.0.1298-stamped reference page to confirm against. The signatures below (`UpdateManager`, `UpdateInfo`, `GithubSource(string repoUrl, string? accessToken, bool prerelease, IFileDownloader? downloader = null)`) are **assumed stable across 0.0.x→1.x but were NOT confirmed at 0.0.1298**. The user can confirm in ~5 min via IntelliSense or decompiling the restored `0.0.1298` `Velopack.dll`. [3][7][9][13]
- **No token required for your public repo**, but unauthenticated GitHub API is capped at **60 requests/hour/IP** — so poll on a sane cadence (startup + a multi-hour timer), not aggressively. [5]
- **Critical dev-machine guard:** `UpdateManager` throws `NotInstalledException` from check/download calls when run unpacked (`dotnet run`, `publish.ps1` output). Guard every entry point with `if (!mgr.IsInstalled) return;`. [6]
- **Unsigned does NOT break in-app updates.** Update integrity is enforced by SHA1/SHA256 hashes in the `RELEASES`/manifest, independent of Authenticode code signing. Delta application works identically unsigned. The only thing unsigned affects is SmartScreen on first install (already documented), not the update channel. [10][11]
- **Your `%APPDATA%` ONNX/FFmpeg files are irrelevant to update application** — Velopack only swaps its own `current` install dir; files outside it are never touched. (See caveat about files *locked* during the swap below.) [4][6]

## Findings

### 1. API surface

⚠️ **Read the version caveat first.** The signatures below are transcribed from the docs.velopack.io reference pages, which **render against Velopack 1.2.0** (each page footer reads "Generated from Velopack 1.2.0"). They are **not** confirmed against a 0.0.1298-stamped page — no such page is published. Velopack's public API for `UpdateManager`/`GithubSource` has been stable across 0.0.x and into 1.x, so these are very likely correct for the pinned `0.0.1298`, but treat them as **assumed-stable, not version-verified**. Confirm against the restored `0.0.1298` `Velopack.dll` (IntelliSense or a decompiler) before relying on any exact signature. [3][7][9]

Members listed on the 1.2.0 reference (namespace `Velopack`, source types in `Velopack.Sources`):

- **Constructors** [3]
  - `UpdateManager(string urlOrPath, UpdateOptions? options = null, IVelopackLocator? locator = null)`
  - `UpdateManager(IUpdateSource source, UpdateOptions? options = null, IVelopackLocator? locator = null)` ← use this with `GithubSource`
- **`GithubSource(string repoUrl, string? accessToken, bool prerelease, IFileDownloader? downloader = null)`** [9] — note `accessToken` and `prerelease` are **positional/required** (no defaults) in this constructor; pass `null, false` for a public-repo stable channel.
- **`Task<UpdateInfo?> CheckForUpdatesAsync()`** — returns `null` when up to date; otherwise an `UpdateInfo` whose `TargetFullRelease` (a `VelopackAsset`) is the new version, plus `DeltasToTarget`, `BaseRelease`, `IsDowngrade`. [3][12]
- **`Task DownloadUpdatesAsync(UpdateInfo updates, Action<int>? progress = null, CancellationToken cancelToken = default)`** [3]
- **`void ApplyUpdatesAndRestart(VelopackAsset? toApply, string[]? restartArgs = null)`** — exits immediately, installs, relaunches. [3]
- **`void ApplyUpdatesAndExit(VelopackAsset? toApply)`** — installs and exits without relaunch. [3]
- **`void WaitExitThenApplyUpdates(VelopackAsset? toApply, bool silent = false, bool restart = true, string[]? restartArgs = null)`** — spawns `Update.exe`, which waits up to 60s for your process to exit, then installs (and relaunches iff `restart: true`). This is the apply-on-quit primitive. [3][4]
- **Properties:** `bool IsInstalled`, `SemanticVersion? CurrentVersion`, `VelopackAsset? UpdatePendingRestart`, `string? AppId`. [3]

⚠️ Overload note: the official WPF sample passes the whole `UpdateInfo` object straight into `ApplyUpdatesAndRestart(_update)` and `DownloadUpdatesAsync(_update, ...)`. [6] The reference page lists the parameter type as `VelopackAsset?`. [3] Velopack ships extension overloads in `UpdateManagerExtensions` that accept `UpdateInfo` and forward `update.TargetFullRelease`, which is why the sample compiles — but I could not directly open that source file (404 on the `develop` path) to confirm it exists verbatim in the 0.0.1298 tag. **Safe approach: pass `update.TargetFullRelease` explicitly** to the `VelopackAsset` overloads and you don't depend on the extension method.

Minimal correct sketch (check → notify → background download → apply on quit):

```csharp
using Velopack;
using Velopack.Sources;

public sealed class UpdateService
{
    private readonly UpdateManager _mgr = new(
        new GithubSource("https://github.com/heimeshoff/WhisperHeim", null, false));
    private UpdateInfo? _pending;

    public bool CanUpdate => _mgr.IsInstalled;            // dev-machine guard, see §5
    public SemanticVersion? CurrentVersion => _mgr.IsInstalled ? _mgr.CurrentVersion : null;

    /// Returns the new version string if one is available (call off the UI thread).
    public async Task<string?> CheckAsync(CancellationToken ct = default)
    {
        if (!_mgr.IsInstalled) return null;              // no-op when run unpacked
        _pending = await _mgr.CheckForUpdatesAsync();    // null == up to date
        return _pending?.TargetFullRelease.Version?.ToString();
    }

    /// Silent background download. Safe to call right after Check.
    public async Task DownloadAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (_pending is null) return;
        await _mgr.DownloadUpdatesAsync(_pending, p => progress?.Report(p), ct);
    }

    /// User clicked "Update now" -> restart immediately.
    public void ApplyAndRestart()
    {
        if (_pending is null) return;
        _mgr.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
    }

    /// User chose "later" -> install when the app next exits, no forced restart.
    public void ApplyOnExit()
    {
        if (_pending is null) return;
        _mgr.WaitExitThenApplyUpdates(_pending.TargetFullRelease, silent: true, restart: false);
        // ...then trigger your own graceful shutdown.
    }
}
```

If you call neither apply method, the downloaded package is still applied automatically on the next app launch (see §2). [4]

### 2. Notify-only UX — apply without forcing a restart

Velopack explicitly supports decoupling the three stages so "your users control when to check, download, or apply." [1][6] For a tray app that should never yank the window away:

1. **Detect** — `CheckForUpdatesAsync` returns non-null. Raise a tray balloon / menu badge "New version X available".
2. **Download silently** — `DownloadUpdatesAsync` into Velopack's packages cache. The app keeps running on the old version; nothing is swapped yet. [4]
3. **Apply later, two documented options:**
   - **Auto-apply on next launch (default).** "If you do not call any of these Apply methods, when you re-launch your app, by default Velopack will detect that there is a pending update and install it then." It can be disabled via `SetAutoApplyOnStartup(false)`. This is the lowest-friction option for a tray app the user quits/reboots regularly. [4]
   - **`WaitExitThenApplyUpdates(asset, silent: true, restart: false)`.** Launches `Update.exe` which waits for your process to exit (up to 60s, then force-kills) and installs afterwards. Call this when the user clicks "install on quit", then start your own shutdown. [3][4]
   - `ApplyUpdatesAndRestart` remains the "Restart now" button — gate it behind explicit consent only. [3]

`UpdateManager.UpdatePendingRestart` (a `VelopackAsset?`) lets you detect, after a download, that an update is staged and waiting — useful to flip the tray menu from "Download" to "Restart to update". [3][6]

Net: yes, apply-on-exit (not just apply-and-restart-now) is a supported, documented path — either explicitly via `WaitExitThenApplyUpdates` or implicitly via auto-apply-on-next-launch.

### 3. How "newer version" is detected

`CheckForUpdatesAsync` reads the **`RELEASES` manifest** Velopack publishes to the GitHub Release (the modern form is a `releases.{channel}.json`; the legacy `RELEASES` file is also produced by your pipeline). [4][6] For `GithubSource` specifically, Velopack queries the GitHub Releases API, picks the appropriate release, and downloads the manifest listing each `.nupkg` asset with its **SemVer version and hash**. [1][9]

- The comparison is **SemVer parsed from the package version inside the manifest/nupkg**, not the git tag text and not the GitHub Release title. The git tag only matters insofar as your pipeline derives `--packVersion` from it during `vpk pack`. [9][12]
- **The running app's "current version"** is `UpdateManager.CurrentVersion`, which Velopack reads from the **installed Velopack package metadata** (the version baked in at `vpk pack` time via `--packVersion`/csproj `<Version>`), via the locator — **not** directly from the .NET assembly `[AssemblyVersion]`. [3] Caveat for your pipeline: ensure the version `vpk pack` stamps is the one derived from the tag; if the assembly version and the pack version ever diverge, the *pack* version is what governs update comparisons. A new tag is seen as "newer" only because its `--packVersion` SemVer sorts above the installed pack version.
- `UpdateInfo.IsDowngrade` is set when the source's newest version is *below* the running version (relevant if you ever re-point a channel). [12]

### 4. Public-repo specifics

- **Token:** not required for a public repo — `GithubSource(repoUrl, null, false)` works. A token is only for private repos or higher rate limits. When supplied it is sent as `Authorization: Bearer {token}`. [5][9]
- **Rate limit:** unauthenticated GitHub API is **60 requests/hour/IP**. [5] A check is a small number of API calls, so a startup check plus a multi-hour timer is well within budget; do not poll every few minutes.
- **Prerelease:** `prerelease: false` ignores GitHub pre-releases; `true` lets pre-releases be update candidates. [5][9] Keep `false` unless you intentionally publish prereleases.
- **Channels:** your pipeline uses the single default channel, so no channel argument is needed. Velopack derives a default channel per-RID; as long as pack and runtime use the same default there's nothing to configure. (Channels only matter if you later split e.g. `stable`/`beta`.) [4]

### 5. The dev-machine / not-installed gotcha

When the app runs **unpacked** — `dotnet run`, debugging from the IDE, or launching the raw `publish.ps1` output rather than from a real Velopack install — `UpdateManager` is not anchored to an install dir. Check/download calls throw **`NotInstalledException`**. [6]

Correct guard: check **`UpdateManager.IsInstalled`** before any check/download and no-op if false (already in the sketch above). [3][6] This makes update logic silently inert during development and only active in installed builds. `CurrentVersion` is also `null`/throwing when not installed, so gate UI that displays it behind `IsInstalled` too.

### 6. Unsigned-release implications

- **Update integrity is hash-based, independent of Authenticode signing.** Velopack assets carry SHA1 (and newer SHA256) checksums in the manifest and verify downloaded packages against them; this is what makes updates tamper-evident. [10][11] Your releases being **unsigned does not disable or weaken this** — the hash check still runs.
- **Code signing (`vpk` signing of `Update.exe`/`Setup.exe`/the app) is a separate concern** that affects the **first-install SmartScreen prompt**, not the in-app update transport. [10] This matches what you already documented.
- **Delta updates apply the same whether signed or unsigned.** If a delta is present and valid it is used; on any delta error Velopack falls back to downloading the full `.nupkg`. [11] No signing dependency in that path.
- ⚠️ The only signing-adjacent caveat: on a future where you *do* sign, the new `Update.exe` shipped inside an update must be signed too, but that's a packaging-time concern and out of scope for the current unsigned state.

### 7. Polling cadence & pitfalls

- **Cadence:** check once shortly after startup (after `IsInstalled` passes), then on a periodic timer. Given the 60 req/hr unauthenticated cap [5], a multi-hour interval (e.g. every 4–6h) is plenty for a tray app; you can also re-check on resume-from-sleep. Avoid tight loops.
- **Threading:** run `CheckForUpdatesAsync`/`DownloadUpdatesAsync` **off the WPF UI thread** (they're async/IO-bound; `await` them from a background context). Marshal only the UI updates back via the `Dispatcher` — the official WPF sample uses `Dispatcher.InvokeAsync` inside the progress callback and `ConfigureAwait(true)` to resume on the UI thread for UI mutation. [6] The apply methods (`ApplyUpdatesAndRestart`, `WaitExitThenApplyUpdates`) terminate/handoff the process, so call them last and from a stable state.
- **Failure modes to handle gracefully (catch and no-op/retry-later):** no internet / DNS failure, GitHub API down or rate-limited (HTTP 403), partial/interrupted download (re-check next cycle), and `NotInstalledException` (dev). Wrap check/download in try/catch and surface nothing to the user on transient failure.
- **Files in use during apply:** application happens **after** your process exits (Update.exe swaps the `current` folder). Anything your *running* process has locked is moot because apply runs post-exit. The one real risk is a *second* helper process (e.g. an FFmpeg child or a model-loader subprocess) still holding a file inside the **install dir** when the swap runs — ensure such child processes are terminated on shutdown so the directory swap isn't blocked.
- **`%APPDATA%` ONNX models / FFmpeg outside the install dir:** confirmed **not a problem**. Velopack only replaces its own versioned `current` directory under the install root; it does not touch user-data paths like `%APPDATA%`. Keeping the large model files outside the install dir is in fact the recommended shape — it also keeps them off the delta/replace path so updates stay small and fast. [4][6]

## Sources
1. [Integrating Overview | Velopack](https://docs.velopack.io/integrating/overview) — update sources incl. GithubSource, lifecycle split, apply-method semantics, NotInstalledException, channels. Current docs.
2. [Velopack — homepage](https://velopack.io/) — vendor (marketing): background download + launch-newest-on-next-launch claim. Flagged as vendor copy; corroborated by docs.
3. [UpdateManager reference | Velopack](https://docs.velopack.io/reference/cs/Velopack/UpdateManager) — constructors, methods (incl. ApplyUpdatesAndRestart `(VelopackAsset?, string[]?)`, ApplyUpdatesAndExit, WaitExitThenApplyUpdates), properties. ⚠️ Page footer reads "Generated from Velopack 1.2.0", NOT 0.0.1298 — treated as assumed-stable, not version-matched.
4. [Integrating Overview / applying updates | Velopack](https://docs.velopack.io/integrating/overview) — auto-apply-on-next-launch, SetAutoApplyOnStartup, WaitExitThenApplyUpdates 60s behavior, install-dir replacement.
5. [Integrating / GithubSource auth & rate limits | Velopack](https://docs.velopack.io/integrating/update-sources) — public repo needs no token; 60 req/hr unauthenticated; Bearer token; prerelease flag.
6. [CSharpWpf sample — MainWindow.xaml.cs | velopack/velopack](https://github.com/velopack/velopack/blob/develop/samples/CSharpWpf/MainWindow.xaml.cs) — real WPF flow: CheckForUpdatesAsync, DownloadUpdatesAsync(progress), ApplyUpdatesAndRestart, IsInstalled/CurrentVersion guard, Dispatcher threading, UpdatePendingRestart. PRIMARY sample (sample branch, not version-pinned).
7. [UpdateInfo reference | Velopack](https://docs.velopack.io/reference/cs/Velopack/UpdateInfo) — TargetFullRelease, BaseRelease, DeltasToTarget, IsDowngrade. ⚠️ Footer "Generated from Velopack 1.2.0", NOT 0.0.1298.
8. _(removed — the previously cited `.../methods/ApplyUpdatesAndRestart` URL 404'd; the method signature is covered by the `UpdateManager` reference, source [3].)_
9. [GithubSource reference | Velopack](https://docs.velopack.io/reference/cs/Velopack.Sources/GithubSource) — constructor `(string repoUrl, string? accessToken, bool prerelease, IFileDownloader? downloader = null)`. ⚠️ Page generated from 1.2.0; constructor assumed identical in 0.0.x (unconfirmed).
10. [Code Signing | Velopack](https://docs.velopack.io/packaging/signing) — signing is for installer/SmartScreen, distinct from update transport.
11. [Support Sha256 for release hash · Issue #105 | velopack/velopack](https://github.com/velopack/velopack/issues/105) — hash-based (SHA1→SHA256) package verification; delta fallback to full. Issue thread, secondary.
12. [CheckForUpdatesAsync reference | Velopack](https://docs.velopack.io/reference/cs/Velopack/UpdateManager/methods/CheckForUpdatesAsync) — returns null when current; UpdateInfo contents and SemVer/delta semantics.
13. [Velopack 0.0.1298 | NuGet](https://www.nuget.org/packages/Velopack/0.0.1298) — published 2025-06-07; deps NuGet.Versioning, Newtonsoft.Json. Confirms the pinned build exists; page notes 1.2.0 is current latest. The API reference docs are generated from 1.2.0, so the pinned version's exact signatures are not documented online.

## Open questions / unverified claims
- ⚠️ **Version provenance — signatures NOT confirmed at 0.0.1298.** The entire API surface in §1 is transcribed from docs.velopack.io pages whose footers read "Generated from Velopack 1.2.0". 0.0.1298 (NuGet, 2025-06-07) has no published reference page. `UpdateManager`/`GithubSource`/`UpdateInfo` are believed stable across 0.0.x→1.x, but this is an **assumption**, not a verification. Definitive check (~5 min): restore `Velopack 0.0.1298`, then inspect `Velopack.dll` via IntelliSense or a decompiler (ILSpy/dnSpy) for the exact `UpdateManager`/`GithubSource` members and signatures.
- ⚠️ **`UpdateInfo`-vs-`VelopackAsset` overloads.** The WPF sample passes `UpdateInfo` directly to apply/download; the 0.0.1298 reference lists `VelopackAsset?`. I could not open `UpdateManagerExtensions.cs` at the 0.0.1298 tag (404 on the `develop` path) to confirm the `UpdateInfo` extension overloads exist verbatim there. Mitigation in the sketch: pass `update.TargetFullRelease` explicitly, which targets the documented `VelopackAsset` overloads and avoids the dependency. Quick way to settle it: `dotnet` IntelliSense against the restored `0.0.1298` package, or decompile `Velopack.dll`.
- **Exact default channel name** Velopack uses for `win-x64` at 0.0.1298 was not pinned down in docs; since your pipeline pack and runtime both use the default, this is cosmetic — but worth a one-line confirmation by inspecting the `RELEASES`/`releases.*.json` asset name on a published Release.
- **Whether `SetAutoApplyOnStartup` exists by that exact name in 0.0.1298** (vs an `UpdateOptions`/builder flag) — the auto-apply-on-launch *behavior* is confirmed; the exact toggle API name should be verified against the restored package before relying on it to disable the behavior.
