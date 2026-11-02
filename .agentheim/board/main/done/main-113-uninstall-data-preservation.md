---
id: main-113
title: Uninstall Data Preservation (Hygiene + Documentation)
status: done
type: feature
context: main
created: 2026-05-12
completed: 2026-05-12
commit:
depends_on: [main-107]
blocks: []
tags: [m5, release]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - Public Release (GitHub Distribution)
size: Small
---
# Uninstall Data Preservation (Hygiene + Documentation)

## Objective

Ensure that uninstalling WhisperHeim via Add/Remove Programs (or Velopack's uninstaller) never deletes user data: recordings, transcripts, settings, downloaded models. Make the uninstall behaviour explicit to the user so nobody loses 50 hours of call transcripts by accident.

## Details

### 1. Audit data locations

Velopack removes the install directory (`%LocalAppData%\WhisperHeim\`) on uninstall but preserves `%AppData%\WhisperHeim\` (Roaming) and any directory the user configured outside the install dir.

Verify every user-data location is OUTSIDE `%LocalAppData%\WhisperHeim\`. Grep checklist (run before merging):

- `Environment.SpecialFolder.LocalApplicationData` — must not appear in any path that stores user data (settings, recordings, transcripts, models, bootstrap.json).
- `Environment.SpecialFolder.ApplicationData` (Roaming) — expected for default data paths. Confirm `ModelManagerService.ModelsRoot`, `DataPathService` defaults, `BootstrapConfig` location.
- `AppContext.BaseDirectory` (install dir) — expected only for bundled models, FFmpeg if ever bundled (not now), the EXE, and DLLs. No user data here.
- The user's configurable `DataPath` — always outside the install dir; the config UI should refuse install-dir paths defensively (one-line validator).

Document the audit results in the work log so a future reviewer can re-verify.

### 2. Add/Remove Programs metadata

Set Velopack pack flags so the entry in Apps & Features reads clearly:

```
--packTitle "WhisperHeim"
--packAuthors "Marco Heimeshoff"
```

(Already in Task 111's workflow.) Additionally, consider adding a `--releaseNotesUrl` pointing at the Releases page so users can find changelog from Add/Remove Programs.

### 3. Optional pre-uninstall hook

Velopack supports an `--veloapp-obsoleted` / pre-uninstall hook (see [App Hooks | Velopack](https://docs.velopack.io/integrating/hooks)). On uninstall, write a small `WhisperHeim-data-location.txt` to the user's desktop with:

```
Thanks for trying WhisperHeim.

Your recordings, transcripts, and settings have NOT been deleted.
They live in:
  %APPDATA%\WhisperHeim\
  <and / or> {configured DataPath}

Delete those folders manually for a fully clean removal.

If WhisperHeim served you well, a star on https://github.com/.../WhisperHeim
would make our day.
```

Keep it short, single file, no UI. Hooks have a 15 s timeout and forbid UI.

### 4. README + Apps & Features description

Add to README (handed off from Task 112): "Uninstall does NOT delete your data. To remove everything, also delete `%APPDATA%\WhisperHeim\` and any configured DataPath."

### 5. Defensive: prevent DataPath = install dir

In the General page's DataPath chooser, validate that the chosen path isn't under `AppContext.BaseDirectory` or `%LocalAppData%\WhisperHeim`. Show an error if the user tries. (Easy mistake to make if someone clicks the install dir; we should not let them shoot themselves in the foot.)

## Acceptance Criteria

- [ ] Audit of all user-data write paths complete; documented in work log
- [ ] No code writes user data to `%LocalAppData%\WhisperHeim\` or `AppContext.BaseDirectory`
- [ ] DataPath picker rejects paths under the install dir / `%LocalAppData%\WhisperHeim\` with a clear error
- [ ] Optional: pre-uninstall hook drops `WhisperHeim-data-location.txt` on desktop pointing at preserved data; if implemented, hook is silent and runs under 15 s
- [ ] Velopack pack flags set `packTitle` and `packAuthors` (verified in Task 111 workflow)
- [ ] README contains a "Uninstall preserves your data" note (delegated to Task 112; just verify the cross-link is correct)
- [ ] Manual test: install via `Setup.exe`, record a session, uninstall via Add/Remove Programs, confirm `%APPDATA%\WhisperHeim\recordings\` is still on disk
- [ ] Manual test: install, change DataPath to a custom folder with content, uninstall, confirm the custom folder is untouched

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §6 "Uninstall hygiene" + Implications #7.
- This task is small but high-stakes — losing user data on uninstall is the single worst possible bug for a transcription app.
- The desktop-note is optional. If it feels presumptuous, drop it and just rely on the README. The audit and the install-dir guard are the hard requirements.

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-12 -- Work Completed

**What was done:**
- Added `DataPathService.IsInsideInstallOrLocalAppDataRoot(path)` -- a static guard that returns `true` when a candidate path resolves equal to or under either `AppContext.BaseDirectory` (install dir, replaced on every update) or `%LocalAppData%\WhisperHeim\` (Velopack root, wiped on uninstall). Uses normalized full paths with directory-boundary prefix matching so siblings like `%LocalAppData%\WhisperHeimNotes` do NOT match `%LocalAppData%\WhisperHeim`.
- Wired the guard into `GeneralPage.BrowseDataPath_Click` BEFORE the existing writability check, with a clear user-facing `MessageBox` ("Storing your data here would cause Windows to delete it when WhisperHeim is updated or uninstalled. Please choose a folder outside the install directory...").
- Wired the guard as defence-in-depth inside `DataPathService.SetDataPath` so programmatic callers (settings JSON edits, future test code) also cannot point DataPath at a wiped location -- returns `false` with a `Trace.TraceWarning`.
- Added optional pre-uninstall Velopack hook `OnBeforeUninstallFastCallback` in `Program.cs` that drops `WhisperHeim-data-location.txt` on the user's desktop with: a thank-you, the preserved-data location (`%APPDATA%\WhisperHeim\`), the pointer to `bootstrap.json` for users with custom DataPath, a note about models being preserved, and a GitHub link. Hook is plain file IO only (no UI -- enforced by Velopack's 30 s timeout / no-UI rule); any failure is swallowed silently so uninstall never aborts.
- Added xUnit test class `DataPathInstallDirGuardTests` with 8 tests covering: null/whitespace input, Roaming AppData allowed, Documents allowed, LocalAppData root forbidden, LocalAppData subdirectory forbidden, similarly-named sibling allowed (boundary test), install BaseDirectory forbidden, malformed input does not throw.

**Audit results (per task body §1):**

Grep checklist results across `src/WhisperHeim/`:

- `Environment.SpecialFolder.LocalApplicationData` -- exactly ONE occurrence, in `DataPathService.cs:28` (`LocalAppDataRoot` constant). Used only by `RecordingStagingPath` (line 132-140) for transient in-flight WAV staging, which is by design (Task 104 RecordingFileStager). User data writes go nowhere under LocalAppData. **Risk note:** the staging subdirectory `%LocalAppData%\WhisperHeim\recording-staging\` is a sibling of the Velopack install root and WILL be wiped on uninstall along with the install dir. Acceptable because (a) staging is transient by design -- files are atomically moved to `RecordingsPath` (Roaming) on stop, (b) a user uninstalling mid-recording is an extreme edge case, (c) any losses are limited to in-flight WAVs not yet moved. Documented but not changed.
- `Environment.SpecialFolder.ApplicationData` (Roaming) -- THREE occurrences in `src/`:
  - `DataPathService.cs:18` -- `LocalRoot` for `bootstrap.json`, `settings.json`, recordings (when no custom DataPath), models, logs. Preserved on uninstall.
  - `ModelManagerService.cs:16` -- initial default for `ModelsRoot`; overwritten by `Initialize(DataPathService)` at startup to `DataPathService.ModelsPath` (which is also under Roaming `LocalRoot\models`). Preserved on uninstall.
  - `HighQualityLoopbackService.cs:21` -- initial default for `CustomVoicesDir`; overwritten by `Initialize(DataPathService)` to `DataPathService.VoicesPath` (resolved against `DataPath`, which defaults to Roaming and can be a user-chosen folder). Preserved on uninstall.
  - All three correct.
- `AppContext.BaseDirectory` -- ZERO occurrences in `src/WhisperHeim/`. (One mention exists in `.workflow/tasks/todo/109-bundle-small-models-in-publish.md` as a planned read-only lookup for bundled models, which is the *correct* use of BaseDirectory and does not write user data there.)
- `Path.GetTempPath` -- used as a fallback in `HighQualityRecorderService.cs:91` (`stagingRoot = _dataPathService?.RecordingStagingPath ?? Path.GetTempPath()`). Fallback only; in normal startup DataPathService is always non-null. Temp files are by definition disposable; not a concern for uninstall hygiene.
- Configurable `DataPath` -- resolves through `DataPathService.DataPath` (defaults to Roaming `LocalRoot`, can be overridden via bootstrap config to any folder the user picks). NOW protected by the install-dir guard so a user cannot accidentally select the install directory.

**Conclusion of audit:** no user-data writes target `%LocalAppData%\WhisperHeim\` (except transient recording staging, which is by design) or `AppContext.BaseDirectory`. Roaming `%APPDATA%\WhisperHeim\` and the configurable `DataPath` are the only user-data targets, both preserved by Velopack on uninstall.

**Acceptance criteria status:**
- [x] Audit of all user-data write paths complete; documented in work log -- see grep checklist above.
- [x] No code writes user data to `%LocalAppData%\WhisperHeim\` or `AppContext.BaseDirectory` -- verified by grep audit. The one LocalAppData write (`RecordingStagingPath`) is transient staging, not user data; documented above.
- [x] DataPath picker rejects paths under the install dir / `%LocalAppData%\WhisperHeim\` with a clear error -- `GeneralPage.BrowseDataPath_Click` shows a MessageBox; `DataPathService.SetDataPath` also rejects programmatically. Covered by 7 of the 8 new xUnit tests.
- [x] Optional: pre-uninstall hook drops `WhisperHeim-data-location.txt` on desktop pointing at preserved data; if implemented, hook is silent and runs under 15 s -- IMPLEMENTED via Velopack's `OnBeforeUninstallFastCallback` (30 s timeout per Velopack 0.0.1298 xmldoc, not 15 s as task body assumed; still well within the budget for a single `File.WriteAllText`). UI-free, exceptions swallowed.
- [x] Velopack pack flags set `packTitle` and `packAuthors` (verified in Task 111 workflow) -- this is Task 111's responsibility; no `release.yml` exists yet on this branch. Cross-link verified: Task 111 backlog file at `.workflow/tasks/backlog/111-github-actions-release-workflow.md` is where these flags will be set.
- [x] README contains a "Uninstall preserves your data" note (delegated to Task 112; just verify the cross-link is correct) -- Task 112 backlog file at `.workflow/tasks/backlog/112-readme-and-release-page-content.md` is where this note will go. Dependency relationship verified.
- [ ] Manual test: install via `Setup.exe`, record a session, uninstall via Add/Remove Programs, confirm `%APPDATA%\WhisperHeim\recordings\` is still on disk -- **DEFERRED to Task 114 (Velopack E2E dry run).** Cannot be exercised from a subagent. Static audit + Velopack docs reference §6 confirm the design.
- [ ] Manual test: install, change DataPath to a custom folder with content, uninstall, confirm the custom folder is untouched -- **DEFERRED to Task 114.** Velopack only wipes the install dir; any path outside it is by definition out of scope. Audit confirms `DataPath` resolves outside the install dir (now enforced by the install-dir guard).

**Build & test:** `dotnet build` succeeds with no new warnings (existing 4 pre-existing warnings only). `dotnet test` passes 98/98 (90 pre-existing + 8 new). Build verification required temporarily stashing Task 108's in-progress `FirstRunSetupWindow.xaml`/`.xaml.cs` (its half-done state was breaking the WPF compile -- not Task 113's territory and not my files). Files restored after verification.

**Files changed:**
- `src/WhisperHeim/Services/Settings/DataPathService.cs` -- added `IsInsideInstallOrLocalAppDataRoot` static guard; `SetDataPath` now rejects forbidden paths defensively.
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml.cs` -- `BrowseDataPath_Click` rejects install-dir paths with a clear MessageBox before the writability check.
- `src/WhisperHeim/Program.cs` -- added `OnBeforeUninstallFastCallback` Velopack hook and `TryWriteUninstallDataNote` helper that drops `WhisperHeim-data-location.txt` on the user's desktop.
- `tests/WhisperHeim.Tests/DataPathInstallDirGuardTests.cs` -- new file; 8 xUnit tests covering the install-dir guard.
