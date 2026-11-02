---
id: main-001
title: Project Scaffolding
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: []
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Small
---
# Project Scaffolding

## Objective
Create the .NET solution with a WPF project configured with all core NuGet packages.

## Details
Create a .NET 9 solution `WhisperHeim.sln` with a WPF app project `WhisperHeim`. Add NuGet packages: WPF-UI, WPF-UI.Tray, NAudio, org.k2fsa.sherpa.onnx, Microsoft.ML.OnnxRuntime. Set up basic App.xaml with ShutdownMode="OnExplicitShutdown". Configure for x64 only (ONNX models are 64-bit). Add .gitignore for .NET/WPF.

## Acceptance Criteria
- [x] Solution builds
- [x] App launches and exits cleanly
- [x] All NuGet packages resolve

## Notes
.NET 9, x64 only. ShutdownMode="OnExplicitShutdown" is required for tray icon lifecycle.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Execution
- Created `WhisperHeim.sln` with WPF project `src/WhisperHeim/WhisperHeim.csproj` targeting .NET 9.
- Configured x64-only build (`<Platforms>x64</Platforms>`, `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`).
- Added all required NuGet packages: WPF-UI, WPF-UI.Tray, NAudio, org.k2fsa.sherpa.onnx, Microsoft.ML.OnnxRuntime.
- Set `ShutdownMode="OnExplicitShutdown"` in App.xaml.
- Added .gitignore for .NET/WPF/models.
- Build succeeds: 0 warnings, 0 errors. All packages resolve.

**Files created/changed:**
- `WhisperHeim.sln`
- `src/WhisperHeim/WhisperHeim.csproj`
- `src/WhisperHeim/App.xaml`
- `src/WhisperHeim/App.xaml.cs` (template)
- `src/WhisperHeim/AssemblyInfo.cs` (template)
- `src/WhisperHeim/MainWindow.xaml` (template)
- `src/WhisperHeim/MainWindow.xaml.cs` (template)
- `.gitignore`
