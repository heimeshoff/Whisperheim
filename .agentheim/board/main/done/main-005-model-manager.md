---
id: main-005
title: Model Manager
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-003]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Medium
---
# Model Manager

## Objective
Auto-download Parakeet TDT 0.6B and Silero VAD models on first run with progress indication.

## Details
Models stored in %APPDATA%/WhisperHeim/models/. Check if models exist on startup. If missing, show a download dialog with progress bar. Download Parakeet TDT 0.6B ONNX model from HuggingFace (or sherpa-onnx model zoo). Download Silero VAD ONNX model (~1MB). Support cancellation. Show model status on the About settings page (model name, size, path). Use HttpClient with progress reporting.

## Acceptance Criteria
- [x] First run downloads models with visible progress
- [x] Subsequent runs skip download
- [x] Models are verified (file size/hash)
- [x] Cancellation works

## Notes
Parakeet TDT 0.6B from HuggingFace or sherpa-onnx model zoo. Silero VAD is ~1MB.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation Complete

**Files created:**
- `src/WhisperHeim/Services/Models/ModelDefinition.cs` — Model metadata records (ModelDefinition, ModelFileDefinition, ModelStatus, ModelStatusInfo)
- `src/WhisperHeim/Services/Models/ModelDownloadProgress.cs` — Progress reporting DTO
- `src/WhisperHeim/Services/Models/ModelManagerService.cs` — Core service: checks model status, downloads from HuggingFace/GitHub with progress, verifies file sizes, supports cancellation
- `src/WhisperHeim/Views/ModelDownloadDialog.xaml` + `.cs` — WPF dialog with progress bar and cancel button, shown on first run

**Files modified:**
- `src/WhisperHeim/App.xaml.cs` — Added ModelManagerService; checks models on startup, shows download dialog if missing
- `src/WhisperHeim/MainWindow.xaml.cs` — Accepts and passes ModelManagerService to AboutPage
- `src/WhisperHeim/Views/Pages/AboutPage.xaml` + `.cs` — Shows model status (name, description, size, path, Ready/Missing)

**Model sources:**
- Parakeet TDT 0.6B (int8): 4 files from `huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v2-int8` (~661 MB total)
- Silero VAD: 1 file from `github.com/snakers4/silero-vad` (~2 MB)

**Key design decisions:**
- Individual file downloads from HuggingFace (no tar.bz2 extraction needed on Windows)
- File size verification with 10% tolerance for version differences
- Temp file (.tmp) pattern prevents corrupt partial downloads
- HttpClient with 80KB buffer and async streaming for progress reporting
- Build: 0 errors, 0 warnings
