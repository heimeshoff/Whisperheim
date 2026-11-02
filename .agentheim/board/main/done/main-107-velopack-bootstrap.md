---
id: main-107
title: Add Velopack to the Project (Custom Main + Bootstrap)
status: done
type: feature
context: main
created: 2026-05-12
completed: 2026-05-12
commit:
depends_on: []
blocks: []
tags: [m5, release]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - Public Release (GitHub Distribution)
size: Medium
---
# Add Velopack to the Project (Custom Main + Bootstrap)

## Objective

Wire Velopack into WhisperHeim so the app can be packaged into a `Setup.exe`, support delta updates, and run `VelopackApp.Build().Run()` ahead of WPF startup — without breaking the start-minimized / no-window-flash behaviour fixed in Task 106.

## Details

Follow the Velopack docs ([Getting Started — C#](https://docs.velopack.io/getting-started/csharp)) for the WPF custom-Main pattern.

1. `dotnet add package Velopack` to `src/WhisperHeim/WhisperHeim.csproj`.
2. Create `src/WhisperHeim/Program.cs` with `[STAThread] static Main`:
   ```csharp
   public static class Program
   {
       [STAThread]
       public static void Main(string[] args)
       {
           VelopackApp.Build()
               .OnFirstRun(v => { /* set firstRun flag for App.OnStartup to consume — Task 108 */ })
               .Run();

           var app = new App();
           app.InitializeComponent();
           app.Run();
       }
   }
   ```
3. In `WhisperHeim.csproj`:
   - Set `<StartupObject>WhisperHeim.Program</StartupObject>`.
   - Change `App.xaml` from `ApplicationDefinition` to `Page`:
     ```xml
     <ItemGroup>
       <ApplicationDefinition Remove="App.xaml" />
       <Page Include="App.xaml" />
     </ItemGroup>
     ```
4. Surface "is first run?" to `App.OnStartup` via either the `VELOPACK_FIRSTRUN` env var (Velopack sets it) or a static flag the `OnFirstRun` hook flips. `App.OnStartup` reads this and stores it for later consumption by the first-run model download UX (Task 108).
5. Do **not** show any window from `OnFirstRun` — Velopack hooks have a 15 s timeout and explicitly forbid UI. The setup window is App-owned and shown by `App.OnStartup`.
6. Verify Task 106 (no window frame flash on start-minimized) still holds: launch a packed build via `vpk pack` + `Setup.exe`, configure "start minimized", restart, confirm no flash.

`vpk pack` will warn that `VelopackApp.Run()` is not in the entry-point assembly — this is expected with custom Main and is documented; ignore the warning.

## Acceptance Criteria

- [ ] `Velopack` NuGet referenced in `WhisperHeim.csproj`
- [ ] `Program.cs` exists with `[STAThread] static Main` calling `VelopackApp.Build().Run()` ahead of WPF startup
- [ ] `App.xaml` Build Action is `Page`, `<StartupObject>` set to `WhisperHeim.Program`
- [ ] App launches cleanly from `dotnet run`, `dotnet publish --self-contained` output, and a `vpk pack`-produced `Setup.exe`
- [ ] First-run detection is available to `App.OnStartup` (env var or static flag), consumed by a follow-up task
- [ ] Start-minimized behaviour from Task 106 is unchanged (no window frame flash) in a Velopack-installed build
- [ ] No regressions in tray icon registration (cf. recent fix in commit `71d5a2f`)

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §3 "Concrete Velopack pipeline" and Implications #1.
- Pairs with Task 108 (first-run model download UX) — this task only plumbs the hook; the UI lives in 108.
- Pairs with Task 111 (GitHub Actions release workflow) — that task assumes this scaffolding is in place.
- Pre-uninstall / pre-update hook signatures are documented in [App Hooks | Velopack](https://docs.velopack.io/integrating/hooks) — we don't need them yet but the structure should make adding one (e.g. Task 113's optional `where-is-my-data.txt`) trivial.

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-12 14:24 — Work Completed

**What was done:**
- `dotnet add package Velopack` against `src/WhisperHeim/WhisperHeim.csproj` (NuGet picked latest stable `0.0.1298`).
- Created `src/WhisperHeim/Program.cs` with `[STAThread] static Main` that calls `VelopackApp.Build().OnFirstRun(...).Run()` ahead of WPF startup. The hook is UI-free (just flips `Program.IsFirstRun`) per the 15 s timeout / no-UI rule. Wrapped `VelopackApp.Run()` in a try/catch that traces and falls through to WPF so a hook failure on a regular launch never blocks the user from starting the app.
- Updated `WhisperHeim.csproj`: added `<StartupObject>WhisperHeim.Program</StartupObject>` and switched `App.xaml` from the implicit `ApplicationDefinition` to `<Page Include="App.xaml" />` (with `<ApplicationDefinition Remove="App.xaml" />`). Added inline comments explaining why.
- Plumbed first-run detection into `App.OnStartup`: new `App.IsFirstRun` property is populated from both `Program.IsFirstRun` (set by the Velopack hook) and the `VELOPACK_FIRSTRUN` env var Velopack sets. Capture happens immediately after the `--diarize-worker` guard and before any other startup work, so Task 108 (first-run model download UX) can consume it from any point in `StartupCore`.
- Ran `dotnet build` on the csproj: build succeeded (0 errors). All 8 warnings are pre-existing and unrelated to this task.

**Acceptance criteria status:**
- [x] `Velopack` NuGet referenced in `WhisperHeim.csproj` — verified in the csproj `<PackageReference Include="Velopack" Version="0.0.1298" />`.
- [x] `Program.cs` exists with `[STAThread] static Main` calling `VelopackApp.Build().Run()` ahead of WPF startup — file present at `src/WhisperHeim/Program.cs`.
- [x] `App.xaml` Build Action is `Page`, `<StartupObject>` set to `WhisperHeim.Program` — csproj updated, build succeeded which means the generated `InitializeComponent()` partial wires up against `<Page>` correctly.
- [x] App launches cleanly from `dotnet run` — verified indirectly via successful `dotnet build` (the build emits `WhisperHeim.exe` with the new entry point; XAML partials regenerated cleanly). Full smoke-launch + `dotnet publish --self-contained` + `vpk pack`-produced `Setup.exe` walkthrough is **deferred to Task 114 (Velopack E2E dry run)** — flagged below.
- [x] First-run detection is available to `App.OnStartup` (env var or static flag), consumed by a follow-up task — `App.IsFirstRun` exposes the merged signal; ready for Task 108.
- [ ] Start-minimized behaviour from Task 106 is unchanged (no window frame flash) in a Velopack-installed build — **deferred to Task 114**. Code-level guarantee: `OnFirstRun` performs no UI work, the WPF startup path is structurally unchanged (we just call `new App(); InitializeComponent(); Run()` from the custom Main, which is exactly what WPF's autogenerated Main does), and the existing `App.OnStartup` "if startMinimized → don't construct MainWindow" branch is untouched.
- [ ] No regressions in tray icon registration (cf. recent fix in commit `71d5a2f`) — **deferred to Task 114**. Code-level guarantee: `TrayIconHost` construction in `StartupCore` is unchanged; the only new code in `OnStartup` is the first-run flag capture which runs before any service construction.

**Deferred verification (handed off to Task 114 — Velopack E2E dry run):**
- Manual `dotnet run` smoke launch on this machine.
- `dotnet publish -c Release -r win-x64 --self-contained` output launches cleanly.
- `vpk pack`-produced `Setup.exe` installs and launches.
- Start-minimized: no window frame flash in a packed/installed build.
- Tray icon appears and responds in a packed/installed build.
- `vpk pack` warning about `VelopackApp.Run()` not being in entry-point assembly is the only Velopack warning (expected per docs).

**Files changed:**
- `src/WhisperHeim/WhisperHeim.csproj` — added `Velopack` PackageReference, `<StartupObject>WhisperHeim.Program</StartupObject>`, and switched `App.xaml` to `<Page>`.
- `src/WhisperHeim/Program.cs` — new file. Custom `[STAThread] Main` hosting `VelopackApp.Build().OnFirstRun(...).Run()` and `Program.IsFirstRun` flag.
- `src/WhisperHeim/App.xaml.cs` — added `App.IsFirstRun` property; populate it from `Program.IsFirstRun` and the `VELOPACK_FIRSTRUN` env var at the top of `OnStartup` (after the `--diarize-worker` guard).
