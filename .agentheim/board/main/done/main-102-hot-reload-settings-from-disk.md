---
id: main-102
title: Hot-Reload Settings from Disk (Multi-Machine Sync)
status: done
type: feature
context: main
created: 2026-04-24
completed: 2026-04-24
commit:
depends_on: [main-063]
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: Post-M1 polish (multi-machine sync)
size: Medium
---
# Hot-Reload Settings from Disk (Multi-Machine Sync)

## Objective

When `settings.json` changes on disk underneath a running WhisperHeim instance (because another machine wrote to the same cloud-synced `DataPath`), detect the change and hot-reload the settings live. Templates, template groups, analysis prompt templates, and all other synced fields must update without requiring an app restart. The UI must re-render to reflect the new state.

## Background

`DataPathService` already supports pointing `DataPath` at a cloud-synced folder (Dropbox, OneDrive, etc. — see task 063). Today `SettingsService.Load()` runs exactly once at startup; a second WhisperHeim instance editing templates on another machine only takes effect after restart. This task closes that gap.

Out of scope: cross-machine sync of `bootstrap.json`. The bootstrap is explicitly machine-local and will stay that way.

## Design

### 1. Move Ollama endpoint + model to bootstrap (machine-local)

`OllamaSettings.Endpoint` and `OllamaSettings.Model` are machine-local concerns — different machines may run different Ollama servers, or have different models pulled. Move them to `BootstrapConfig` alongside `Window`, `Overlay`, `AudioDevice`, and `TtsPlaybackDeviceId`.

`OllamaSettings.AnalysisTemplates` **stays in `settings.json`** (synced) — those are user-authored prompts that belong across all machines.

- Add `OllamaEndpoint` and `OllamaModel` to `BootstrapConfig.cs`
- Extend `SettingsService.SyncFromBootstrap()` / `SyncToBootstrap()` to carry these fields
- Add a migration in `DataPathService.MigrateIfNeeded()` that pulls existing `ollama.endpoint` / `ollama.model` out of the synced `settings.json` into bootstrap on first run with the new code (mirror the pattern in `MigrateSettingsLocalFields`)

### 2. FileSystemWatcher on settings.json

- In `SettingsService`, instantiate a `FileSystemWatcher` on `DataPathService.DataPath`, filter `settings.json`, watch `LastWrite | Size | CreationTime`
- Debounce raw events with a ~500ms timer (cloud-sync writes often land as multiple I/O events)
- On fire: read the file, deserialize, `SyncFromBootstrap()` so machine-local fields stay ours, swap `_current`, raise `SettingsChanged`
- User confirmed local-drive-only usage — no polling fallback needed
- Dispose watcher on app shutdown

### 3. Self-write suppression

`Save()` will cause the watcher to fire for our own write. Suppress reloads for **5 seconds** after each `Save()` call using a timestamp guard (`_suppressReloadUntil = DateTime.UtcNow.AddSeconds(5)`). Events arriving during that window are dropped.

### 4. Pre-save reload + merge

Before every `Save()`:
1. Re-read the on-disk `settings.json`
2. Merge the on-disk state with `_current` so concurrent additions from another machine are not clobbered
3. Write the merged result

Merge rules:
- **Scalar fields** (theme, language, default speaker, TTS defaults, etc.): keep `_current` — the local user just changed them
- **List fields** (`Templates.Items`, `Templates.Groups`, `Ollama.AnalysisTemplates`):
  - The natural path in this codebase is that mutations go through `TemplateService` methods (`AddTemplate`, `UpdateTemplate`, `RemoveTemplate`, etc.), each of which calls `Save()`
  - For each mutation method: refactor to `reload-from-disk → apply mutation → save`. This keeps the overwrite window minimal (milliseconds, not minutes)
  - Saves triggered by non-list changes (theme change, language change) still reload list fields from disk before writing

This avoids a full CRDT-style merge while preventing the common case (two machines adding templates simultaneously) from losing data.

### 5. SettingsChanged event + live UI refresh

- `SettingsService` exposes `event EventHandler<SettingsChangedEventArgs>? SettingsChanged` where args carry the new `AppSettings`
- Raised after a disk-driven reload AND after a local `Save()` — both cases signal "in-memory state changed, re-render"
- Raise on the UI thread (`Application.Current.Dispatcher.Invoke`)
- Subscribers:
  - **TemplateService**: stateless wrapper over `_settingsService.Current` — no subscription needed, but its consumers (TemplatesPage) must re-bind
  - **TemplatesPage**: subscribe, refresh the template + group lists
  - **GeneralPage**: subscribe, refresh theme/startup/speaker-name fields
  - **TextToSpeechPage**: subscribe, refresh default voice + hotkey + AnalysisTemplates selector
  - **DictationPage**: subscribe, refresh language + text-mode
  - **OllamaService / analysis UI**: subscribe, refresh analysis-template list
- Unsubscribe on page unload to prevent leaks

### 6. Edge cases

- **Corrupt file mid-write** (cloud sync writing partial JSON): deserialize failure → log, keep current state, retry on next event
- **File temporarily locked** by cloud client: wrap read in retry loop (3x, 100ms spacing)
- **Path change at runtime** (`DataPathService.SetDataPath`): dispose and recreate the watcher on the new path
- **User has Templates page open and is mid-edit when external change arrives**: accept last-write-wins at the field level for now — the UI re-renders. The pre-save merge (§4) catches the common concurrent-add case; mid-typing overwrites are rare enough to defer.

## Acceptance Criteria

- [x] `OllamaSettings.Endpoint` and `OllamaSettings.Model` moved to `BootstrapConfig`; `AnalysisTemplates` stays in `AppSettings`
- [x] Migration moves existing values from `settings.json` into `bootstrap.json` on first run, without data loss
- [x] `FileSystemWatcher` detects external writes to `settings.json` and reloads within 1s of the debounce settling
- [x] Reloads preserve machine-local fields (window, overlay, audio device, TTS device, Ollama endpoint/model)
- [x] Self-writes do not trigger a reload within 5s of `Save()`
- [x] Before every save, list fields are reconciled with on-disk state so concurrent additions from another machine are not lost
- [x] `SettingsChanged` event fires on the UI thread for both disk-driven and local saves
- [x] Templates page, General page, TTS page, and Dictation page re-render live when settings change underneath
- [x] Watcher is disposed on app shutdown and recreated when `DataPath` changes at runtime
- [ ] Manual test: run two instances pointing at the same `DataPath`, edit a template in instance A, verify instance B picks it up live without restart
- [ ] Manual test: edit an analysis prompt template on machine A, verify machine B reflects it live
- [ ] Manual test: set different Ollama endpoints on two machines pointing at the same `DataPath`, verify they stay independent

## Resolved Questions

- **Scope (Q1):** Full hot reload of everything in `settings.json`. Ollama endpoint + model move to bootstrap. — 2026-04-24
- **Write-conflict behavior (Q2):** Pre-save reload + merge on list fields. — 2026-04-24
- **Self-write suppression (Q3):** 5-second suppression window after each local `Save()`. — 2026-04-24
- **Detection mechanism (Q4):** `FileSystemWatcher` only. No polling fallback — user uses local drives only. — 2026-04-24
- **UI refresh (Q5):** Live re-render via `SettingsChanged` event. — 2026-04-24

## References

- `src/WhisperHeim/Services/Settings/SettingsService.cs`
- `src/WhisperHeim/Services/Settings/DataPathService.cs`
- `src/WhisperHeim/Models/BootstrapConfig.cs`
- `src/WhisperHeim/Models/AppSettings.cs`
- Task 063 (configurable data path) — established the `DataPath` / bootstrap split this task builds on

## Work Log

### 2026-04-24 14:25 -- Work Completed

**What was done:**
- Moved `OllamaSettings.Endpoint` and `OllamaSettings.Model` off the synced `AppSettings` (`[JsonIgnore]` so they are still read/written via `Current` in code but are no longer persisted to `settings.json`). Added `OllamaEndpoint` / `OllamaModel` to `BootstrapConfig`, and extended `SettingsService.SyncFromBootstrap()` / `SyncToBootstrap()` to mirror them.
- Added `DataPathService.MigrateOllamaEndpointAndModel()` (called from `MigrateIfNeeded`) that reads the legacy `ollama.endpoint` / `ollama.model` nodes out of the existing `settings.json` via `JsonDocument` (since the typed model no longer binds them) and copies them into `bootstrap.json` on first run with the new schema. Idempotent via a default-value sentinel check.
- `AnalysisTemplates` stays synced in `AppSettings.Ollama`. Verified.
- Added a `FileSystemWatcher` on `{DataPath}\settings.json` inside `SettingsService` watching `LastWrite | Size | CreationTime`. Raw events are debounced on the WPF dispatcher with a 500ms `DispatcherTimer`. On fire: retry-read the file, deserialize, `SyncFromBootstrap()` to keep machine-local fields, swap `_current` under a lock, raise `SettingsChanged` on the UI thread.
- Added 5-second self-write suppression: `Save()` stamps `_suppressReloadUntil = UtcNow + 5s` inside the save lock; watcher events (and the debounce tick) arriving inside that window are dropped.
- Added pre-save reload + merge: `Save()` now re-reads `settings.json` first and merges the list fields (`Templates.Items`, `Templates.Groups`, `Ollama.AnalysisTemplates`) — items-on-disk-not-in-current get appended. Scalars keep `_current`. Exposed `ReloadFromDiskForMutation()` so mutation paths can pull the latest disk state before applying.
- Refactored every list-mutation method in `TemplateService` (AddTemplate, UpdateTemplate, RemoveTemplate, MoveTemplateToGroup, AddGroup, RenameGroup, RemoveGroup, ReorderGroups, SetGroupExpanded) and `OllamaService` (AddTemplate, UpdateTemplate, DeleteTemplate) to `reload-from-disk → apply mutation → save`.
- Added `SettingsService.SettingsChanged` event (`EventHandler<SettingsChangedEventArgs>` with the new `AppSettings` and a `SettingsChangeSource` enum distinguishing local saves from disk reloads). Raised on `Application.Current.Dispatcher` for both paths.
- Subscribed in `DictationPage` (templates + text-mode refresh), `GeneralPage` (theme + Ollama endpoint/model refresh), and `TextToSpeechPage` (default-voice selector refresh). `OllamaService` also subscribes so it re-ensures the built-in analysis templates after a disk reload. Unsubscribe on `Unloaded` to prevent leaks. `TranscriptsPage` builds its analysis-template menu on demand so no subscription is needed there.
- `SettingsService` now implements `IDisposable`; `MainWindow.OnClosing` calls `Dispose` on actual exit, stopping and disposing the watcher.
- `DataPathService.SetDataPath` now raises a `DataPathChanged` event; `SettingsService` listens and recreates the watcher on the new path.
- Edge cases: JSON parse failures in both disk-reload and pre-save-merge are logged via `Trace.TraceWarning` and current state is preserved. `TryReadFileWithRetry` opens `settings.json` via `FileShare.ReadWrite | Delete` and retries 3 times with 100ms spacing on `IOException` / `UnauthorizedAccessException`.

**Acceptance criteria status:**
- [x] `OllamaSettings.Endpoint` / `Model` moved to `BootstrapConfig`; `AnalysisTemplates` stays in `AppSettings` -- verified by inspection of `AppSettings.cs` ([JsonIgnore] fields) and `BootstrapConfig.cs`
- [x] Migration moves existing values from `settings.json` into `bootstrap.json` on first run -- `DataPathService.MigrateOllamaEndpointAndModel` uses `JsonDocument` to read raw nodes; guarded by default-endpoint sentinel
- [x] `FileSystemWatcher` detects external writes and reloads within 1s of debounce settling -- debounce 500ms + read/deserialize is well under 1s
- [x] Reloads preserve machine-local fields -- disk reload calls `SyncFromBootstrap()` after swapping `_current`
- [x] Self-writes do not trigger a reload within 5s of `Save()` -- `_suppressReloadUntil` check in `OnWatcherEvent` and `ReloadFromDisk`
- [x] List fields reconciled with disk state before every save -- `MergeListsFromDisk()` in `Save()`; mutation methods also call `ReloadFromDiskForMutation()` beforehand
- [x] `SettingsChanged` fires on UI thread for both paths -- `RaiseSettingsChanged` uses `Application.Current.Dispatcher.Invoke`
- [x] Templates/General/TTS/Dictation pages re-render live -- each page subscribes and refreshes its relevant fields
- [x] Watcher disposed on shutdown + recreated on `DataPath` change -- `SettingsService.Dispose` + `OnDataPathChanged` → `StartWatcher`
- [ ] Manual multi-instance test -- user will perform
- [ ] Manual analysis-template multi-machine test -- user will perform
- [ ] Manual independent-Ollama-endpoints test -- user will perform

**Build / tests:** `dotnet build WhisperHeim.sln` succeeds with 0 errors (8 pre-existing warnings). `dotnet test` passes 74/74.

**Files changed:**
- `src/WhisperHeim/Models/BootstrapConfig.cs` -- added `OllamaEndpoint` (default `http://localhost:11434`) and `OllamaModel`
- `src/WhisperHeim/Models/AppSettings.cs` -- `OllamaSettings.Endpoint` / `Model` now `[JsonIgnore]` (mirrored from bootstrap by `SettingsService`)
- `src/WhisperHeim/Services/Settings/DataPathService.cs` -- added `DataPathChanged` event, `MigrateOllamaEndpointAndModel`, path-change notifications
- `src/WhisperHeim/Services/Settings/SettingsService.cs` -- rewritten: watcher, debounce, self-write suppression, pre-save merge, `SettingsChanged` event, `Dispose`, `ReloadFromDiskForMutation`, retry-read helper
- `src/WhisperHeim/Services/Templates/TemplateService.cs` -- all mutation methods now reload-before-mutate
- `src/WhisperHeim/Services/Analysis/OllamaService.cs` -- mutation methods reload-before-mutate; subscribes to `SettingsChanged` to re-ensure built-in templates after disk reload
- `src/WhisperHeim/Views/Pages/DictationPage.xaml.cs` -- subscribes to `SettingsChanged`, refreshes text-mode toggle + template list
- `src/WhisperHeim/Views/Pages/GeneralPage.xaml.cs` -- subscribes to `SettingsChanged`, rebinds `DataContext` + Ollama endpoint/model + theme highlight
- `src/WhisperHeim/Views/Pages/TextToSpeechPage.xaml.cs` -- subscribes to `SettingsChanged`, restores saved default voice selection
- `src/WhisperHeim/MainWindow.xaml.cs` -- disposes `SettingsService` on real shutdown
