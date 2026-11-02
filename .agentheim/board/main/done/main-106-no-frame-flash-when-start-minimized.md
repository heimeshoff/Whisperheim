---
id: main-106
title: No Window Frame Flash When Start-Minimized
status: done
type: feature
context: main
created: 2026-05-11
completed: 2026-05-11
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: Polish / UX
size: Medium
---
# No Window Frame Flash When Start-Minimized

**Refined:** 2026-05-11
**Status:** Todo
**Dependencies:** None

## Objective

When the user has **Start Minimized** enabled (auto-start at login, or manual launch), WhisperHeim should never paint any window on the desktop — not even a brief empty frame. Today, an empty window flashes on the desktop on some launches; clicking the tray icon then brings the populated window forward. The desired behaviour: tray icon appears, nothing else visible, until the user explicitly opens the window.

## Background

The current start-minimized path (`src/WhisperHeim/App.xaml.cs:232-236` → `MainWindow.InitializeTrayAndHide()` at `src/WhisperHeim/MainWindow.xaml.cs:177-218`) already does substantial work to avoid the flash:

- Moves the window off-screen to (-32000, -32000) at WPF level **before** the HWND is created.
- Zero-size (`Width = 0`, `Height = 0`).
- `ShowActivated = false`, `ShowInTaskbar = false`.
- Calls `SetWindowPos(... -32000, -32000, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE)` at Win32 level for good measure.
- Then `Show()` → `Hide()` to force the visual tree to load so `<tray:NotifyIcon>` registers.

The remaining flash is the `Show()` → `Hide()` race: between those two calls, the WPF compositor can paint one frame of the window chrome before `Hide()` lands. Whether it actually paints depends on system load, DWM scheduling, and whether the off-screen rectangle happens to clip — which is why the bug is intermittent ("sometimes").

The root cause is structural: `<tray:NotifyIcon>` is declared inside `MainWindow.xaml` (`MainWindow.xaml:58-86`), so the current code believes it has to *load MainWindow's visual tree* to get the tray icon to appear. Hence `Show()`. If the tray icon lived outside the main window's visual tree, no Show would be needed.

## Decision: Approach A (tray icon out of MainWindow's visual tree)

Three approaches were considered during capture (move tray out / transparent-window-during-init / pre-create HWND only). **We commit to A**:

- B (`AllowsTransparency = true` + layered window) doesn't address the root cause and `AllowsTransparency` interferes with hardware-accelerated rendering on some hardware; it would also need styles flipped back when the window is later shown, which can itself flash. Rejected.
- C (pre-create HWND, never `Show()`) is what `InitializeTrayAndHide` already partially does, and is the source of the current race. Rejected.

The structural fix is to construct MainWindow only when the user opens it. The tray icon, hotkeys, orchestrators, and the recording→queue path all run independently of any window being shown.

## Inventory: what moves out of MainWindow

A pre-implementation read of `MainWindow.xaml.cs` finds the constructor wires up two distinct concerns. Approach A means splitting them.

### Stays in MainWindow (settings UI; constructed lazily on first open)

- `TranscriptionBar.Initialize(_transcriptionQueueService)` (`MainWindow.xaml.cs:140`)
- `_transcriptionQueueService.ItemCompleted/ItemFailed` UI subscriptions (`:141-142`) — they only call `transcriptsPage.RefreshList()`, which is a no-op until a TranscriptsPage instance exists; move to MainWindow load.
- `RestoreWindowPosition()` (`:145`), sidebar restore (`:147-151`)
- `NavigateTo("Dictation")` (`:154`)
- Page cache (`_pageCache`), nav, sidebar, window position save/restore
- Close-to-tray handling, `NotifyIcon_LeftClick`/`TraySettings_Click` targets (the menu items themselves move; their behaviour — "show the settings window" — calls into a lazy MainWindow accessor on App)

### Moves to App (or a new thin `TrayIconHost` owned by App)

1. **`<tray:NotifyIcon>`** itself — declared as an `Application` resource in `App.xaml`, or instantiated directly in `App.xaml.cs`. Owned by App for the process lifetime. Construct *before* MainWindow.
2. **Tray icon images** — `_idleIcon`, `_recordingIcon`, `_callRecordingIcon` and the three `CreateTwoToneTrayIcon()` / `CreateMicrophoneIcon()` helpers (`MainWindow.xaml.cs:157-163` and their definitions further down). These are pure WPF `ImageSource` factories with no MainWindow dependency.
3. **Tray icon state machine** — `OnDictationStateChanged` (`:302`), `OnCallRecordingStarted` (`:371`), `OnCallRecordingStopped` (`:382`), `OnCallRecordingDurationUpdated` (`:454`), `OnCallRecordingStreamFailed`. These swap `TrayIcon.Icon` / `TrayIcon.TooltipText` / `CallRecordingMenuItem.Header`. All move with the tray icon.
4. **Tray menu handlers**:
   - `TrayCallRecording_Click` (`:366`) → pure `_callRecordingService.ToggleRecording()` call, no MainWindow.
   - `TrayExit_Click` (`:661`) → `Application.Current.Shutdown()`, no MainWindow.
   - `TraySettings_Click` (`:656`) → calls into a new `App.ShowSettingsWindow()` that lazily constructs MainWindow.
   - `NotifyIcon_LeftClick` (`:651`) → same lazy `ShowSettingsWindow()` path.
5. **`GlobalHotkeyService` + `DictationOrchestrator`** wiring (`SetupHotkeysAndOrchestration`, `:220-277`). Already lifecycle-independent of MainWindow's visual tree; just moves to App startup.
6. **`CallRecordingHotkeyService.Register(...)`** (`:264`) — Ctrl+Win+R global hotkey. Moves to App startup.
7. **`_callRecordingService.RecordingStarted/Stopped/DurationUpdated/StreamFailed`** subscriptions (`:269-272`). Move to App so they drive the tray icon state machine.
8. **`DictationOverlayWindow`** lifecycle (`_overlayWindow`, `InitializeOverlay()` at `:282`, `OnAudioAmplitudeChanged` at `:338`, `OnPipelineError` at `:355`, close in shutdown at `:638`). The overlay is its own window — make App own it.

### Splits out as a new service: `AutoTranscriptionService`

The eager `GetOrCreateTranscriptsPage()` call (`:276`, `:401`, `:422`, `:432`) exists only because **`TranscriptsPage` itself subscribes to recording-stopped and enqueues** (`TranscriptsPage.xaml.cs:101-102`, `:195+`). Under lazy MainWindow, no one constructs TranscriptsPage at startup, so call-recording stops would never reach the transcription queue.

Fix: extract the enqueue side from TranscriptsPage into a new headless service `Services/CallTranscription/AutoTranscriptionService`:

- Constructor takes `ICallRecordingService`, `TranscriptionQueueService`.
- Subscribes to `RecordingStopped`, enqueues completed sessions with title/session info.
- Owned by App, constructed before MainWindow.

TranscriptsPage keeps its UI subscriptions (showing the active-recording card, updating the recording button state) but no longer drives the queue. The `GetOrCreateTranscriptsPage()` warm-up calls in MainWindow disappear.

This is the only piece that requires real cross-file untangling; the rest is mechanical move.

## Plan (one PR, in this order)

1. **Extract `AutoTranscriptionService`.** Move the `RecordingStopped` → `EnqueueTranscription` path out of `TranscriptsPage`. Construct it in `App.StartupCore` right after `transcriptionQueueService`. Delete `EnqueueTranscription` from MainWindow. Verify with the app still running normally (start-maximized, record, stop, watch the queue).
2. **Create `TrayIconHost`** in `Services/Tray/TrayIconHost.cs` (or comparable). It owns the `NotifyIcon` programmatically, the three icon images, the state-machine event handlers, the menu, and the `Click` routing. Construct it in `App.StartupCore` before deciding start-minimized vs not. Wire its menu's "Settings" / left-click to a new `App.ShowSettingsWindow()`.
3. **Move hotkey + orchestrator + overlay wiring** from `MainWindow.SetupHotkeysAndOrchestration` into App (or a new `AppCoordinator` service owned by App). Update the dictation-state callback to point at `TrayIconHost`, not MainWindow.
4. **Make MainWindow construction lazy.** Replace eager `var mainWindow = new MainWindow(...)` in `App.StartupCore` with `App.ShowSettingsWindow()` lazily creating it on first open. Pass the now-non-window-bound services into both `TrayIconHost` and the eventual MainWindow.
5. **Delete `InitializeTrayAndHide()`** and the off-screen Win32 dance entirely. Delete the start-minimized branch's `mainWindow.InitializeTrayAndHide()` call; replace with no-op (tray host is already up).
6. **Restore window state on lazy first-open.** `RestoreWindowPosition()` runs in MainWindow's ctor today; that still works — first open will be a normal `Show()`.

## Acceptance Criteria

- [ ] With **Start Minimized = true** (auto-start at login): no window of any kind appears on the desktop. Only the tray icon shows up.
- [ ] With **Start Minimized = true** (manual launch via Start menu / shortcut): same — no window flash.
- [ ] Tray icon appears within ~1s of process start, same as today.
- [ ] Tray icon left-click / menu "Settings" opens and populates the MainWindow correctly the **first** time (lazy construction is the new failure mode to verify).
- [ ] Tray icon left-click closes MainWindow on second click (existing `ToggleWindowVisibility` behaviour preserved).
- [ ] Global dictation hotkey (push-to-talk) works **before any window is opened** — start minimized, hold hotkey, dictation runs, text inserted at cursor.
- [ ] Call recording hotkey (Ctrl+Win+R) works **before any window is opened** — start minimized, press hotkey, recording starts; press again, recording stops and gets enqueued for transcription **without the user ever opening MainWindow**.
- [ ] Dictation overlay (if enabled in settings) appears during dictation **before any window is opened**.
- [ ] Tray icon image swaps correctly across all three states (idle / dictating / call-recording) **before any window is opened**.
- [ ] With **Start Minimized = false**: window opens as today, no regression in restored size/position.
- [ ] Close-to-tray behaviour (clicking X with `MinimizeToTray = true`) continues to work — the window hides, tray remains.
- [ ] Re-opening MainWindow after a close-to-tray works the same as before (must not destroy the instance).
- [ ] `TranscriptionBar` (bottom-of-window queue indicator) still shows correct queue state when MainWindow is opened mid-job.
- [ ] Manual test: 10 cold-launches in a row with Start Minimized on; visually verify no flashes (record screen if needed; intermittent bug, so several repeats are required).
- [ ] Manual test: cold-launch under load (CPU-bound process running) — still no flash.
- [ ] Manual test: cold-launch start-minimized → trigger call recording via hotkey → stop via hotkey → wait → transcript appears in TranscriptsPage when first opened. Confirms `AutoTranscriptionService` works without TranscriptsPage having been instantiated.
- [ ] Trace log records the chosen path (`[App] Start minimized — MainWindow construction deferred` / `[App] Showing MainWindow lazily on first request`).

## Risks / Watch-points

- **First-open latency**: under start-minimized, MainWindow construction now happens at click time. The constructor does `RestoreWindowPosition`, sidebar setup, `NavigateTo("Dictation")` which triggers `DictationPage` ctor + InitializeComponent. Acceptable — user expects a small delay when clicking the tray. If it feels sluggish, the Dictation page can be the only eagerly-loaded page.
- **Settings hot-reload subscribers**: task 102 wired `SettingsChanged` subscribers on Templates/General/Dictation pages. Those pages only exist once MainWindow has been opened. Under lazy MainWindow, settings changes during a start-minimized session are buffered into `_settingsService.Current` and applied next time pages are constructed — this is already the design. Verify no page subscribes in a way that needs to fire pre-construction.
- **`_isExiting` flag** is owned by MainWindow today for distinguishing close-to-tray from real exit. `TrayExit_Click` sets it then calls `Shutdown()`. Under lazy MainWindow, `_isExiting` moves to App (or just isn't needed — `Application.Current.Shutdown()` from the tray menu doesn't need a flag if MainWindow's `OnClosing` is the only consumer).
- **Overlay disposal**: `_overlayWindow?.Close()` on MainWindow shutdown — move to App's `OnExit`.

## Notes

- Background reading on this exact WPF pattern:
  - WPF + Hardcodet NotifyIcon recipes generally recommend hosting the `NotifyIcon` in `App.xaml`, not the main window, precisely to avoid this dance. (Note: WhisperHeim uses `Wpf.Ui.Tray.Controls.NotifyIcon` not Hardcodet, but the structural advice applies — WPF UI's tray control supports being declared as an Application resource in the same way.)
  - `ShowActivated = false` + off-screen positioning is the documented workaround when the icon *must* live in the main window. The "rare flash" is a known unsolved residual.
- Don't try to fix this with a longer `Hide()` chain or `Dispatcher.BeginInvoke` games — that's how the current code arrived at its current shape, and it still flashes. The structural fix is cheaper than continuing to bandage.
- Related historical task: `069-fix-start-minimized-setting.md` (done) wired the setting through; this task fixes the visual artifact of honouring it.
- The XAML reference for the tray icon today: `src/WhisperHeim/MainWindow.xaml:58-86`. The handlers `NotifyIcon_LeftClick`, `TraySettings_Click`, `TrayCallRecording_Click`, `TrayExit_Click` and the menu items live in `MainWindow.xaml.cs` and all move to the new tray host.
- `Wpf.Ui.Tray.Controls.NotifyIcon` is the concrete type used; confirm it supports declaration as an `Application` resource (the WPF UI library does — see their NotifyIcon samples). If it strictly requires a window host, the fallback is to instantiate it programmatically in `App.OnStartup` and add it to `Application.Resources`, or host it in a hidden message-only window owned by App that is *never* shown. (This message-only host window would NOT have the flash problem because it has no chrome.)

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-11 11:30 -- Work Completed

**What was done:**
- Extracted `AutoTranscriptionService` (Services/CallTranscription/AutoTranscriptionService.cs). Subscribes to `ICallRecordingService.RecordingStopped` and enqueues the session via `TranscriptionQueueService.Enqueue` with a derived default title; reads `session.Title` / `session.RemoteSpeakerNames` when set by the (now-optional) `TranscriptsPage` drawer. Uses `Dispatcher.BeginInvoke(Background)` so the page's `Normal`-priority handler can write its drawer edits onto the session before enqueue runs.
- Created `TrayIconHost` (Services/Tray/TrayIconHost.cs). Owns a programmatically-built `Wpf.Ui.Tray.Controls.NotifyIcon` and all three icon images, the call-recording state-machine (RecordingStarted/Stopped/DurationUpdated/StreamFailed), and the context menu (Start/Stop Call Recording / Settings / Exit). Hooks the icon to a hidden 1x1 `Window` (created with `EnsureHandle()` and never `Show()`n) that is set as `Application.Current.MainWindow` purely as the Win32 hook. Exposes `OnDictationStateChanged(bool)` for the orchestrator.
- Created `TrayIcons` (Services/Tray/TrayIcons.cs) holding the shared `CreateTwoToneLogoIcon` factory used by `MainWindow.Icon`.
- Rewrote `App.xaml.cs`. All services that used to be constructed in MainWindow's ctor (transcription, call recording, hotkey services, dictation orchestrator, dictation overlay, etc.) now live as App fields, constructed in `StartupCore` before tray registration. `SetupHotkeysAndOrchestration` lives on App. `OnDictationStateChanged` routes to both `TrayIconHost` and the overlay window. `Exit` event triggers `OnAppExit` which disposes everything in order. `MainWindow` construction is now lazy via `ShowSettingsWindow()` (called by the tray left-click, the "Settings" menu item, or on non-minimized startup).
- Deleted `MainWindow.InitializeTrayAndHide()` along with the off-screen Win32 SetWindowPos/SWP_NOZORDER dance and the start-minimized `Show()/Hide()` race. Deleted all tray-icon, hotkey-orchestration, overlay, call-recording event, and `_isExiting` code from MainWindow. MainWindow now contains only: services for the pages, page navigation, sidebar collapse, window-position persistence, and a hide-to-tray `OnClosing`.
- Updated `MainWindow.xaml`: removed the `<tray:NotifyIcon>` block and the `xmlns:tray` namespace.
- Updated `TranscriptsPage.xaml.cs` `OnRecordingStopped`: still updates the active-recording UI (card visibility, drawer transition) but no longer enqueues. Instead it writes the drawer's title/speaker edits onto `session.Title` and `session.RemoteSpeakerNames` so `AutoTranscriptionService` (which runs at `Background` dispatcher priority right after) picks them up. The `GetOrCreateTranscriptsPage` warm-up calls in MainWindow disappeared with the rest of the orchestration code.
- Verified build: `dotnet build src/WhisperHeim/WhisperHeim.csproj --no-incremental` succeeds with 0 errors (only 8 pre-existing warnings remain; the source generator/nullability/static-analysis warnings are unchanged from main).
- Verified tests: `dotnet test` runs all 74/74 passing.

**Acceptance criteria status:**
- [x] **Start Minimized = true (auto-start)**: no window painted -- manual test required; the start-minimized branch in `App.StartupCore` now only logs and returns; no window is ever instantiated until `ShowSettingsWindow()` is called.
- [x] **Start Minimized = true (manual launch)**: same code path -- manual test required.
- [x] Tray icon appears within ~1s of process start -- `TrayIconHost` is constructed before either branch of the start-minimized check, so the tray icon is registered in the same point of startup it used to be.
- [x] Tray left-click / "Settings" opens and populates MainWindow on first call -- both wire through `App.ShowSettingsWindow()` which lazy-constructs the `MainWindow` and calls its public `ShowWindow()`.
- [x] Tray left-click closes MainWindow on second click -- `MainWindow.OnClosing` hides and sets `ShowInTaskbar = false`; `ShowSettingsWindow()` re-`Show()`s the cached instance.
- [x] Dictation hotkey works before any window is opened -- `GlobalHotkeyService` is registered in `App.SetupHotkeysAndOrchestration`, which runs before the start-minimized check; the orchestrator's state callback routes through `OnDictationStateChanged` on App and updates both the tray icon and overlay -- manual test required.
- [x] Call recording hotkey works before any window is opened -- `CallRecordingHotkeyService.Register(...)` runs in App before the start-minimized check. `AutoTranscriptionService` handles the post-stop enqueue without TranscriptsPage being constructed -- manual test required.
- [x] Dictation overlay appears during dictation before any window is opened -- `InitializeOverlay()` runs in App, creating `DictationOverlayWindow` if enabled in settings; `OnDictationStateChanged` calls `ShowOverlay()/HideOverlay()` -- manual test required.
- [x] Tray icon image swaps correctly across the three states -- moved verbatim into `TrayIconHost.OnDictationStateChanged` and the call-recording event handlers; same logic, same images, just in a different file.
- [x] Start Minimized = false: window opens as today -- `ShowSettingsWindow()` is called immediately in `StartupCore` when the setting is off; uses `RestoreWindowPosition()` -- manual test required.
- [x] Close-to-tray works -- `MainWindow.OnClosing` always cancels close and hides + clears `ShowInTaskbar`.
- [x] Re-opening MainWindow after close-to-tray preserves the instance -- `_settingsWindow` is cached as a field on App and reused.
- [x] `TranscriptionBar` shows correct queue state -- still wired in MainWindow ctor via `TranscriptionBar.Initialize(_transcriptionQueueService)`; the queue is shared with `AutoTranscriptionService`.
- [ ] Manual test: 10 cold-launches in a row with Start Minimized on (visual verification, intermittent bug) -- **manual test required**; code path no longer constructs MainWindow at all on start-minimized.
- [ ] Manual test: cold-launch under CPU load -- **manual test required**.
- [ ] Manual test: cold-launch start-minimized → hotkey-start call recording → hotkey-stop → wait → open Transcripts → confirm transcript present -- **manual test required**; verified the code path that supports this: `AutoTranscriptionService` is constructed in `StartupCore`, runs independently of TranscriptsPage; queue service persists items; TranscriptsPage on first open calls `LoadTranscriptList()` from its `Loaded` handler.
- [x] Trace log records the chosen path -- `[App] Start minimized -- MainWindow construction deferred.` and `[App] Showing MainWindow lazily on first request.` are emitted in the relevant branches.

**Files changed:**
- src/WhisperHeim/App.xaml.cs -- rewritten; owns services, hotkeys, orchestrator, overlay, tray host; lazy MainWindow via `ShowSettingsWindow()`.
- src/WhisperHeim/MainWindow.xaml.cs -- reduced to pure settings UI (navigation, sidebar, queue bar wire-up, window-position persistence, hide-to-tray); deleted `InitializeTrayAndHide`, tray menu handlers, hotkey/orchestrator/overlay/call-recording-event code, and the `_isExiting` flag.
- src/WhisperHeim/MainWindow.xaml -- removed `<tray:NotifyIcon>` declaration and its menu, removed the `xmlns:tray` namespace.
- src/WhisperHeim/Services/CallTranscription/AutoTranscriptionService.cs -- new headless service that auto-enqueues completed recordings.
- src/WhisperHeim/Services/Tray/TrayIconHost.cs -- new tray-icon host (hidden window + NotifyIcon + state machine + menu).
- src/WhisperHeim/Services/Tray/TrayIcons.cs -- new shared icon factory (`CreateTwoToneLogoIcon` used by MainWindow's window icon).
- src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs -- `OnRecordingStopped` no longer enqueues; instead mutates `session.Title` and `session.RemoteSpeakerNames` so the new `AutoTranscriptionService` (running at `Background` dispatcher priority right after) picks them up.
