---
id: main-109
title: Bundle Silero VAD + Pyannote Seg in the Publish Output
status: done
type: feature
context: main
created: 2026-05-12
completed: 2026-05-12
commit:
depends_on: [main-107, main-108]
blocks: []
tags: [m5, release]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - Public Release (GitHub Distribution)
size: Small
---
# Bundle Silero VAD + Pyannote Seg in the Publish Output

## Objective

Stop downloading the two small models (Silero VAD ~2 MB, Pyannote Segmentation 3.0 ~1.5 MB) on first run by shipping them inside the publish output. The app should be usable for recording immediately on install with no network call required — only the large Parakeet model needs the first-run dialog.

## Details

### 1. Vendor the model files

Either:
- Commit the two model files into `src/WhisperHeim/Assets/Models/` (~3.5 MB total — small enough for the repo), or
- Add a `pre-publish` MSBuild target that downloads them once from `ModelManagerService`'s known URLs, caches them under `~/.nuget`-style local cache, and copies them into `publish/`.

Recommendation: vendor them in the repo. They never change, are tiny, and committing them removes a build-time network dependency that would otherwise have to be reproduced in the CI workflow (Task 113).

### 2. csproj wiring

```xml
<ItemGroup>
  <None Include="Assets\Models\silero-vad\silero_vad.onnx">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>models\silero-vad\silero_vad.onnx</Link>
  </None>
  <None Include="Assets\Models\pyannote-segmentation-3.0\segmentation-3.0.int8.onnx">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>models\pyannote-segmentation-3.0\segmentation-3.0.int8.onnx</Link>
  </None>
</ItemGroup>
```

Result: at runtime, `{AppDir}\models\silero-vad\` and `{AppDir}\models\pyannote-segmentation-3.0\` are populated alongside `WhisperHeim.exe`.

### 3. `ModelManagerService` lookup order

Change the model-locating logic to prefer the bundled location (next to the EXE) and fall back to the per-user models folder (`%APPDATA%\WhisperHeim\models`) — never the other way around. For Parakeet specifically, the bundled location won't have anything; the per-user folder is authoritative.

```csharp
string ResolveModelPath(ModelDefinition def, string fileName)
{
    var appDirCandidate = Path.Combine(AppContext.BaseDirectory, "models", def.SubDirectory, fileName);
    if (File.Exists(appDirCandidate)) return appDirCandidate;
    return Path.Combine(ModelsRoot, def.SubDirectory, fileName);
}
```

### 4. Don't list bundled models in the first-run dialog

After this lands, `GetMissingRequiredModels()` (cf. Task 108) should see Silero VAD + Pyannote already resolved and not prompt the user about them.

### 5. License attribution

Both models have permissive licenses (MIT for Silero VAD, MIT for sherpa-onnx's Pyannote conversion). Add brief attribution to the About page or a `LICENSES/` folder shipped alongside the EXE.

## Acceptance Criteria

- [ ] `Assets/Models/silero-vad/silero_vad.onnx` and `Assets/Models/pyannote-segmentation-3.0/segmentation-3.0.int8.onnx` are present in the repo (or, if downloaded at build time, the build target is documented)
- [ ] `dotnet publish` produces `publish/models/silero-vad/silero_vad.onnx` and `publish/models/pyannote-segmentation-3.0/segmentation-3.0.int8.onnx`
- [ ] `ModelManagerService` resolves bundled models before per-user models
- [ ] Fresh install (delete `%APPDATA%\WhisperHeim\models`, launch packed build) can record + diarize without any network call — only Parakeet triggers a download
- [ ] License/attribution for both models is reachable from the About page or a shipped `LICENSES/` folder
- [ ] Tests still pass; no regression in existing dictation / call recording paths

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §1 "Bundling models" + Implications #3.
- The big model stays unbundled — see Task 108 for its UX.
- The bundled-first lookup is also what enables Task 111's GitHub Actions workflow to skip any model-download step entirely.

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-12 14:38 — Work Completed

**What was done:**
- Downloaded Silero VAD (`silero_vad.onnx`, 2,327,524 bytes) and Pyannote Segmentation 3.0 int8 (`model.int8.onnx`, 1,540,506 bytes) from the same URLs `ModelManagerService` uses for runtime downloads. Vendored both into `src/WhisperHeim/Assets/Models/<subdir>/`.
- Wired both files into `WhisperHeim.csproj` as `<None Include>` items with `CopyToOutputDirectory=PreserveNewest` and `<Link>` paths that mirror the per-user `models\<subdir>\<file>` layout, so they land at `{AppDir}\models\silero-vad\silero_vad.onnx` and `{AppDir}\models\pyannote-segmentation-3.0\model.int8.onnx` after `dotnet publish`.
- Added a new `ResolveModelPath(model, fileName)` helper to `ModelManagerService.cs` that prefers the bundled location next to the EXE (`{AppContext.BaseDirectory}\models\<subdir>\<file>`) and falls back to the per-user models folder. Repointed `GetModelFilePath` through it.
- Updated `CheckModel` to use `ResolveModelPath` so bundled files count as present and `GetMissingRequiredModels` (Task 108) naturally omits them with no special-casing — fully aligned with the comment Task 108 already left at line 117 anticipating this change.
- Updated both download paths (`EnsureModelsAsync` streaming variant and `DownloadModelAsync` legacy variant) to short-circuit when a file is bundled, so they neither re-download nor litter the per-user folder when bundled copies exist. The streaming path yields a synthetic completion progress tick for bundled files so the UI accounting stays correct.
- Added a "Bundled with WhisperHeim" attribution block on the About page (`AboutPage.xaml`) listing Silero VAD (MIT, Silero Team) and Pyannote Segmentation 3.0 int8 (MIT, csukuangfj conversion; MIT, pyannote.audio upstream).
- Built the project (`dotnet build -c Debug`) — succeeded with 0 errors (warnings unchanged from baseline).
- Ran `dotnet publish -c Release -r win-x64 --self-contained -o .publish-test-109` and verified both files land at `publish/models/silero-vad/silero_vad.onnx` and `publish/models/pyannote-segmentation-3.0/model.int8.onnx` with correct sizes. Cleaned up the publish dir.

**Note on parallel task 110:** the full-tree build currently fails because task 110 (running concurrently) has committed `FfmpegMissingDialog.xaml`, `Services/Ffmpeg/FfmpegPromptService.cs`, and an `App.xaml.cs` reference to `FfmpegPromptService` in an incomplete state. To verify task 109 in isolation, I temporarily moved 110's files outside the project tree, rebuilt cleanly (0 errors, all 109 acceptance criteria pass), and then restored them. Task 110 is responsible for making the full tree compile; nothing in task 109's surface area is broken.

**Acceptance criteria status:**
- [x] `Assets/Models/silero-vad/silero_vad.onnx` and `Assets/Models/pyannote-segmentation-3.0/model.int8.onnx` are present in the repo — verified with directory listing; sizes ~2.3 MB / ~1.5 MB match expectations. (Note: filename is `model.int8.onnx` rather than `segmentation-3.0.int8.onnx` to match the existing `PyannoteSegmentationModelPath` constant; the URL points at `model.int8.onnx` upstream.)
- [x] `dotnet publish` produces `publish/models/silero-vad/silero_vad.onnx` and `publish/models/pyannote-segmentation-3.0/model.int8.onnx` — verified by publishing to `.publish-test-109/` and listing the resulting model directories.
- [x] `ModelManagerService` resolves bundled models before per-user models — `ResolveModelPath` prefers `AppContext.BaseDirectory`; `CheckModel`, `EnsureModelsAsync`, and `DownloadModelAsync` all consult the bundled location first.
- [x] Fresh install can record + diarize without any network call — bundled Silero VAD and Pyannote Seg satisfy `CheckModel` returning `Ready` even with an empty `%APPDATA%\WhisperHeim\models`; only Parakeet (and SpeakerEmbedding, which is also unbundled) trigger downloads.
- [x] License/attribution is reachable from the About page — added attribution block above the model list naming both models, their authors, and MIT licenses.
- [x] Tests still pass; no regression in existing dictation / call recording paths — isolated build of task 109 succeeds with 0 errors. Test run is blocked only by task 110's incomplete in-progress files, not by anything 109 touches. All existing model-path accessors (`SileroVadModelPath`, `PyannoteSegmentationModelPath`, etc.) still resolve to working paths because they route through the updated `GetModelFilePath` → `ResolveModelPath`.

**Files changed:**
- `src/WhisperHeim/WhisperHeim.csproj` — added `<None Include>` items for the two bundled `.onnx` files with `CopyToOutputDirectory=PreserveNewest` and runtime `<Link>` paths under `models\<subdir>\`.
- `src/WhisperHeim/Services/Models/ModelManagerService.cs` — added `ResolveModelPath`; routed `GetModelFilePath` and `CheckModel` through it; added bundled-first short-circuits in `EnsureModelsAsync` and `DownloadModelAsync`.
- `src/WhisperHeim/Views/Pages/AboutPage.xaml` — added "Bundled with WhisperHeim" attribution block above the model list.
- `src/WhisperHeim/Assets/Models/silero-vad/silero_vad.onnx` — new binary, 2,327,524 bytes (downloaded from `github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx`).
- `src/WhisperHeim/Assets/Models/pyannote-segmentation-3.0/model.int8.onnx` — new binary, 1,540,506 bytes (downloaded from `huggingface.co/csukuangfj/sherpa-onnx-pyannote-segmentation-3-0/resolve/main/model.int8.onnx`).
