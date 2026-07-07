using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Services.Models;

/// <summary>
/// Manages downloading, verifying, and locating AI model files.
/// Models are stored locally (not synced) in the models/ subdirectory.
/// </summary>
public sealed class ModelManagerService
{
    private static string ModelsRoot =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhisperHeim",
            "models");

    /// <summary>
    /// Initializes the models root path from the data path service.
    /// Models stay local (not synced) so they use the local root.
    /// </summary>
    public static void Initialize(DataPathService dataPathService)
    {
        ModelsRoot = dataPathService.ModelsPath;
    }

    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    /// <summary>Parakeet TDT 0.6B v3 int8 model for multilingual speech recognition via sherpa-onnx.</summary>
    public static readonly ModelDefinition ParakeetTdt06B = new(
        Name: "Parakeet TDT 0.6B v3",
        Description: "NVIDIA Parakeet TDT 0.6B v3 (int8) — 25 European languages with auto-detection",
        SubDirectory: "parakeet-tdt-0.6b",
        Files: new[]
        {
            new ModelFileDefinition(
                "encoder.int8.onnx",
                "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/encoder.int8.onnx",
                ExpectedSizeBytes: 652_000_000), // ~622 MB
            new ModelFileDefinition(
                "decoder.int8.onnx",
                "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/decoder.int8.onnx",
                ExpectedSizeBytes: 12_600_000), // ~12 MB
            new ModelFileDefinition(
                "joiner.int8.onnx",
                "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/joiner.int8.onnx",
                ExpectedSizeBytes: 6_400_000), // ~6.1 MB
            new ModelFileDefinition(
                "tokens.txt",
                "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/tokens.txt",
                ExpectedSizeBytes: 9_600), // ~9.4 KB
        },
        ProjectUrl: "https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3");

    /// <summary>Silero VAD ONNX model for voice activity detection.</summary>
    public static readonly ModelDefinition SileroVad = new(
        Name: "Silero VAD",
        Description: "Silero Voice Activity Detection (~2 MB)",
        SubDirectory: "silero-vad",
        Files: new[]
        {
            new ModelFileDefinition(
                "silero_vad.onnx",
                "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx",
                ExpectedSizeBytes: 2_300_000), // ~2 MB
        },
        ProjectUrl: "https://github.com/snakers4/silero-vad");

    /// <summary>Pyannote segmentation 3.0 ONNX model for speaker diarization.</summary>
    public static readonly ModelDefinition PyannoteSegmentation = new(
        Name: "Pyannote Segmentation 3.0",
        Description: "Pyannote speaker segmentation 3.0 (int8) — speaker diarization (~1.5 MB)",
        SubDirectory: "pyannote-segmentation-3.0",
        Files: new[]
        {
            new ModelFileDefinition(
                "model.int8.onnx",
                "https://huggingface.co/csukuangfj/sherpa-onnx-pyannote-segmentation-3-0/resolve/main/model.int8.onnx",
                ExpectedSizeBytes: 1_540_000), // ~1.5 MB
        },
        ProjectUrl: "https://github.com/pyannote/pyannote-audio");

    /// <summary>3D-Speaker ERes2Net speaker embedding model for diarization clustering.</summary>
    public static readonly ModelDefinition SpeakerEmbedding = new(
        Name: "3D-Speaker ERes2Net Embedding",
        Description: "3D-Speaker ERes2Net base speaker embedding extractor (~38 MB)",
        SubDirectory: "speaker-embedding",
        Files: new[]
        {
            new ModelFileDefinition(
                "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx",
                "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-recongition-models/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx",
                ExpectedSizeBytes: 39_593_761), // ~38 MB
        },
        ProjectUrl: "https://github.com/modelscope/3D-Speaker");

    /// <summary>
    /// All known model definitions.
    /// </summary>
    public static IReadOnlyList<ModelDefinition> KnownModels { get; } =
        [ParakeetTdt06B, SileroVad, PyannoteSegmentation, SpeakerEmbedding];

    /// <summary>
    /// Models that are required for the app to deliver its primary value
    /// (dictation + call transcription with diarization). The first-run
    /// dialog gates on these. Today this is the same as <see cref="KnownModels"/>,
    /// but once Task 109 lands and Silero VAD / Pyannote Seg are bundled in
    /// the publish output, their files will already resolve on disk and
    /// <see cref="GetMissingRequiredModels"/> will naturally omit them.
    /// </summary>
    public static IReadOnlyList<ModelDefinition> RequiredModels { get; } =
        [ParakeetTdt06B, SileroVad, PyannoteSegmentation, SpeakerEmbedding];

    private static string ManifestPath => Path.Combine(ModelsRoot, "manifest.json");

    /// <summary>
    /// Returns the path to <c>models/manifest.json</c>. Public for diagnostics.
    /// </summary>
    public static string GetManifestPath() => ManifestPath;

    /// <summary>
    /// Returns the directory where a given model's files are stored in the
    /// per-user (writable) models root. This is the destination for downloads.
    /// Note: at runtime, a model file may instead resolve to the bundled
    /// location next to the EXE -- see <see cref="ResolveModelPath"/>.
    /// </summary>
    public static string GetModelDirectory(ModelDefinition model) =>
        Path.Combine(ModelsRoot, model.SubDirectory);

    /// <summary>
    /// Returns the path to a specific model file, preferring the bundled
    /// location alongside the EXE (<c>{AppDir}\models\{subdir}\{file}</c>) and
    /// falling back to the per-user models folder (<c>%APPDATA%\WhisperHeim\models</c>).
    /// Task 109 ships Silero VAD + Pyannote Seg in the publish output; they
    /// resolve to the bundled path. Parakeet has no bundled copy and falls
    /// through to the per-user folder, which is also where downloads land.
    /// </summary>
    public static string GetModelFilePath(ModelDefinition model, string fileName) =>
        ResolveModelPath(model, fileName);

    /// <summary>
    /// Bundled-first path resolution. If the file exists next to the EXE at
    /// <c>{AppContext.BaseDirectory}\models\{subdir}\{file}</c>, returns that.
    /// Otherwise returns the per-user path under <see cref="ModelsRoot"/>,
    /// regardless of whether the file exists there yet -- callers (download,
    /// existence checks) expect a stable "this is where it would live" path.
    /// </summary>
    public static string ResolveModelPath(ModelDefinition model, string fileName)
    {
        var bundled = Path.Combine(
            AppContext.BaseDirectory, "models", model.SubDirectory, fileName);
        if (File.Exists(bundled))
            return bundled;

        return Path.Combine(ModelsRoot, model.SubDirectory, fileName);
    }

    /// <summary>
    /// Convenience: returns the Silero VAD model path.
    /// </summary>
    public static string SileroVadModelPath =>
        GetModelFilePath(SileroVad, "silero_vad.onnx");

    /// <summary>
    /// Convenience: returns the Parakeet encoder path.
    /// </summary>
    public static string ParakeetEncoderPath =>
        GetModelFilePath(ParakeetTdt06B, "encoder.int8.onnx");

    /// <summary>
    /// Convenience: returns the Parakeet decoder path.
    /// </summary>
    public static string ParakeetDecoderPath =>
        GetModelFilePath(ParakeetTdt06B, "decoder.int8.onnx");

    /// <summary>
    /// Convenience: returns the Parakeet joiner path.
    /// </summary>
    public static string ParakeetJoinerPath =>
        GetModelFilePath(ParakeetTdt06B, "joiner.int8.onnx");

    /// <summary>
    /// Convenience: returns the Parakeet tokens path.
    /// </summary>
    public static string ParakeetTokensPath =>
        GetModelFilePath(ParakeetTdt06B, "tokens.txt");

    /// <summary>
    /// Convenience: returns the pyannote segmentation model path.
    /// </summary>
    public static string PyannoteSegmentationModelPath =>
        GetModelFilePath(PyannoteSegmentation, "model.int8.onnx");

    /// <summary>
    /// Convenience: returns the speaker embedding model path.
    /// </summary>
    public static string SpeakerEmbeddingModelPath =>
        GetModelFilePath(SpeakerEmbedding, "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx");

    /// <summary>
    /// Checks the status of all known models.
    /// </summary>
    public IReadOnlyList<ModelStatusInfo> CheckAllModels()
    {
        return KnownModels.Select(CheckModel).ToList();
    }

    /// <summary>
    /// Checks the status of a single model. Honors bundled-first resolution
    /// so files shipped in the publish output (Task 109: Silero VAD, Pyannote
    /// Seg 3.0) are recognized as Ready even when the per-user models folder
    /// is empty. The reported directory still points at the per-user folder
    /// because that is where downloads land; the bundled folder is read-only
    /// from the app's perspective.
    /// </summary>
    public ModelStatusInfo CheckModel(ModelDefinition model)
    {
        var dir = GetModelDirectory(model);
        long downloaded = 0;
        int presentCount = 0;

        foreach (var file in model.Files)
        {
            // Use ResolveModelPath so bundled files count as present even when
            // the per-user folder is empty.
            var path = ResolveModelPath(model, file.FileName);
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                downloaded += info.Length;
                presentCount++;
            }
        }

        ModelStatus status;
        if (presentCount == 0)
            status = ModelStatus.Missing;
        else if (presentCount < model.Files.Count)
            status = ModelStatus.Incomplete;
        else
            status = ModelStatus.Ready;

        return new ModelStatusInfo(model, status, dir, downloaded);
    }

    /// <summary>
    /// Returns true if all known models are ready (downloaded and verified).
    /// </summary>
    public bool AreAllModelsReady()
    {
        return KnownModels.All(m => CheckModel(m).Status == ModelStatus.Ready);
    }

    /// <summary>
    /// Returns a list of models that need downloading.
    /// </summary>
    public IReadOnlyList<ModelDefinition> GetMissingModels()
    {
        return KnownModels
            .Where(m => CheckModel(m).Status != ModelStatus.Ready)
            .ToList();
    }

    /// <summary>
    /// Returns the subset of <see cref="RequiredModels"/> whose files are not
    /// fully present on disk. Used by the first-run setup dialog to decide
    /// whether to surface itself and which models to list as rows. Once a
    /// future build bundles Silero VAD / Pyannote Seg into the publish
    /// output, those resolve as <see cref="ModelStatus.Ready"/> automatically
    /// and drop out of this list -- no code change needed.
    /// </summary>
    public IReadOnlyList<ModelDefinition> GetMissingRequiredModels()
    {
        return RequiredModels
            .Where(m => CheckModel(m).Status != ModelStatus.Ready)
            .ToList();
    }

    /// <summary>
    /// Writes <c>models/manifest.json</c> describing the models currently on
    /// disk. Subsequent launches can fast-path past the first-run dialog by
    /// reading this manifest instead of stat-ing every file. Best-effort:
    /// failures are logged but do not propagate.
    /// </summary>
    public void WriteManifest()
    {
        try
        {
            Directory.CreateDirectory(ModelsRoot);
            var entries = new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in KnownModels)
            {
                var info = CheckModel(model);
                if (info.Status == ModelStatus.Ready)
                {
                    entries[model.SubDirectory] = new ManifestEntry
                    {
                        DownloadedAt = DateTime.UtcNow.ToString("O"),
                        SizeBytes = info.DownloadedBytes,
                    };
                }
            }

            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ManifestPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                "[ModelManagerService] Failed to write manifest.json: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Reads <c>models/manifest.json</c>. Returns an empty dictionary if it
    /// doesn't exist or fails to parse. Best-effort.
    /// </summary>
    public IReadOnlyDictionary<string, ManifestEntry> ReadManifest()
    {
        try
        {
            if (!File.Exists(ManifestPath))
                return new Dictionary<string, ManifestEntry>();

            var json = File.ReadAllText(ManifestPath);
            return JsonSerializer.Deserialize<Dictionary<string, ManifestEntry>>(json)
                   ?? new Dictionary<string, ManifestEntry>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                "[ModelManagerService] Failed to read manifest.json: {0}", ex.Message);
            return new Dictionary<string, ManifestEntry>();
        }
    }

    /// <summary>
    /// Streaming variant of <see cref="DownloadAllMissingModelsAsync"/>:
    /// yields a <see cref="ModelDownloadProgress"/> roughly every 256 KB of
    /// transfer, plus once per file completion. Supports per-file pause
    /// (the caller can hold the returned async-iterator and stop pulling)
    /// and resume via HTTP Range when the CDN supports it -- partial bytes
    /// land in <c>file.tmp</c> and the next call picks up where this one
    /// left off. Cancellation is honored cooperatively.
    /// </summary>
    /// <param name="models">Models to ensure are downloaded. Files that are
    /// already present at an acceptable size are skipped.</param>
    /// <param name="ct">Cancellation token.</param>
    public async IAsyncEnumerable<ModelDownloadProgress> EnsureModelsAsync(
        IEnumerable<ModelDefinition> models,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var model in models)
        {
            ct.ThrowIfCancellationRequested();
            var dir = GetModelDirectory(model);
            Directory.CreateDirectory(dir);

            long totalDownloadedSoFar = 0;
            for (int i = 0; i < model.Files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var file = model.Files[i];
                var filePath = Path.Combine(dir, file.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                // Bundled-first short-circuit (Task 109): if the file ships in
                // the publish output next to the EXE, count it as present and
                // skip downloading -- don't litter the per-user folder.
                var bundledPath = Path.Combine(
                    AppContext.BaseDirectory, "models", model.SubDirectory, file.FileName);
                if (File.Exists(bundledPath))
                {
                    var bundledSize = new FileInfo(bundledPath).Length;
                    totalDownloadedSoFar += bundledSize;
                    yield return new ModelDownloadProgress
                    {
                        ModelName = model.Name,
                        CurrentFileName = file.FileName,
                        CurrentFileDownloaded = bundledSize,
                        CurrentFileTotal = bundledSize,
                        TotalDownloaded = totalDownloadedSoFar,
                        TotalExpected = model.TotalSizeBytes,
                        FileIndex = i,
                        FileCount = model.Files.Count,
                    };
                    continue;
                }

                // Skip if already present at an acceptable size.
                if (File.Exists(filePath))
                {
                    var existingSize = new FileInfo(filePath).Length;
                    if (IsFileSizeAcceptable(existingSize, file.ExpectedSizeBytes))
                    {
                        totalDownloadedSoFar += existingSize;
                        yield return new ModelDownloadProgress
                        {
                            ModelName = model.Name,
                            CurrentFileName = file.FileName,
                            CurrentFileDownloaded = existingSize,
                            CurrentFileTotal = existingSize,
                            TotalDownloaded = totalDownloadedSoFar,
                            TotalExpected = model.TotalSizeBytes,
                            FileIndex = i,
                            FileCount = model.Files.Count,
                        };
                        continue;
                    }

                    File.Delete(filePath);
                }

                await foreach (var p in DownloadFileResumableAsync(
                    file, filePath, model, i, totalDownloadedSoFar, ct))
                {
                    yield return p;
                }

                if (File.Exists(filePath))
                    totalDownloadedSoFar += new FileInfo(filePath).Length;
            }
        }
    }

    private async IAsyncEnumerable<ModelDownloadProgress> DownloadFileResumableAsync(
        ModelFileDefinition file,
        string filePath,
        ModelDefinition model,
        int fileIndex,
        long previousFilesTotal,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var tempPath = filePath + ".tmp";

        long alreadyOnDisk = 0;
        if (File.Exists(tempPath))
        {
            try { alreadyOnDisk = new FileInfo(tempPath).Length; }
            catch { alreadyOnDisk = 0; }
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, file.DownloadUrl);
        if (alreadyOnDisk > 0)
        {
            request.Headers.Range = new RangeHeaderValue(alreadyOnDisk, null);
        }

        HttpResponseMessage response;
        try
        {
            response = await SharedHttpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (OperationCanceledException)
        {
            // Partial download stays in .tmp for resume on next call.
            throw;
        }

        using (response)
        {
            // If server doesn't honor Range, the response will be 200 OK with
            // the full file. Restart from byte 0 in that case.
            bool serverHonoredRange = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
            if (alreadyOnDisk > 0 && !serverHonoredRange)
            {
                TryDeleteFile(tempPath);
                alreadyOnDisk = 0;
            }

            response.EnsureSuccessStatusCode();

            long contentLength = response.Content.Headers.ContentLength ?? file.ExpectedSizeBytes;
            // For partial-content responses, Content-Length is the *remaining*
            // bytes; total is alreadyOnDisk + remaining.
            long totalBytes = serverHonoredRange
                ? alreadyOnDisk + contentLength
                : contentLength;

            var fileMode = serverHonoredRange ? FileMode.Append : FileMode.Create;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(
                tempPath, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long bytesThisCall = 0;
            long bytesDownloaded = alreadyOnDisk;
            int bytesRead;
            long bytesSinceLastReport = 0;

            // Emit an initial progress tick so the UI shows immediate motion.
            yield return new ModelDownloadProgress
            {
                ModelName = model.Name,
                CurrentFileName = file.FileName,
                CurrentFileDownloaded = bytesDownloaded,
                CurrentFileTotal = totalBytes,
                TotalDownloaded = previousFilesTotal + bytesDownloaded,
                TotalExpected = model.TotalSizeBytes,
                FileIndex = fileIndex,
                FileCount = model.Files.Count,
            };

            while (true)
            {
                bytesRead = await contentStream.ReadAsync(buffer, ct);
                if (bytesRead <= 0) break;

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                bytesThisCall += bytesRead;
                bytesDownloaded += bytesRead;
                bytesSinceLastReport += bytesRead;

                if (bytesSinceLastReport >= 262_144) // ~256 KB
                {
                    bytesSinceLastReport = 0;
                    yield return new ModelDownloadProgress
                    {
                        ModelName = model.Name,
                        CurrentFileName = file.FileName,
                        CurrentFileDownloaded = bytesDownloaded,
                        CurrentFileTotal = totalBytes,
                        TotalDownloaded = previousFilesTotal + bytesDownloaded,
                        TotalExpected = model.TotalSizeBytes,
                        FileIndex = fileIndex,
                        FileCount = model.Files.Count,
                    };
                }
            }

            await fileStream.FlushAsync(ct);

            yield return new ModelDownloadProgress
            {
                ModelName = model.Name,
                CurrentFileName = file.FileName,
                CurrentFileDownloaded = bytesDownloaded,
                CurrentFileTotal = totalBytes,
                TotalDownloaded = previousFilesTotal + bytesDownloaded,
                TotalExpected = model.TotalSizeBytes,
                FileIndex = fileIndex,
                FileCount = model.Files.Count,
            };
        }

        // Move temp -> final atomically.
        if (File.Exists(filePath))
            File.Delete(filePath);
        File.Move(tempPath, filePath);
    }

    /// <summary>
    /// Downloads all files for a model, reporting progress.
    /// Skips files that already exist with the expected size.
    /// </summary>
    public async Task DownloadModelAsync(
        ModelDefinition model,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var dir = GetModelDirectory(model);
        Directory.CreateDirectory(dir);

        long totalDownloadedSoFar = 0;

        for (int i = 0; i < model.Files.Count; i++)
        {
            var file = model.Files[i];
            var filePath = Path.Combine(dir, file.FileName);

            // Ensure parent directory exists (for files in subdirectories like test_wavs/bria.wav)
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            // Bundled-first short-circuit (Task 109): if the file ships in
            // the publish output next to the EXE, count it as present and
            // skip downloading.
            var bundledPath = Path.Combine(
                AppContext.BaseDirectory, "models", model.SubDirectory, file.FileName);
            if (File.Exists(bundledPath))
            {
                totalDownloadedSoFar += new FileInfo(bundledPath).Length;
                continue;
            }

            // Skip if file already exists with correct size
            if (File.Exists(filePath))
            {
                var existingSize = new FileInfo(filePath).Length;
                if (IsFileSizeAcceptable(existingSize, file.ExpectedSizeBytes))
                {
                    totalDownloadedSoFar += existingSize;
                    continue;
                }

                // File exists but wrong size -- re-download
                File.Delete(filePath);
            }

            await DownloadFileAsync(
                file, filePath, model, i, totalDownloadedSoFar, progress, cancellationToken);

            if (File.Exists(filePath))
            {
                totalDownloadedSoFar += new FileInfo(filePath).Length;
            }
        }
    }

    /// <summary>
    /// Downloads all missing models.
    /// </summary>
    public async Task DownloadAllMissingModelsAsync(
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var missing = GetMissingModels();

        foreach (var model in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DownloadModelAsync(model, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes all files for a model.
    /// </summary>
    public void DeleteModel(ModelDefinition model)
    {
        var dir = GetModelDirectory(model);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private async Task DownloadFileAsync(
        ModelFileDefinition file,
        string filePath,
        ModelDefinition model,
        int fileIndex,
        long previousFilesTotal,
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tempPath = filePath + ".tmp";

        try
        {
            using var response = await SharedHttpClient.GetAsync(
                file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? file.ExpectedSizeBytes;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long bytesDownloaded = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesDownloaded += bytesRead;

                progress?.Report(new ModelDownloadProgress
                {
                    ModelName = model.Name,
                    CurrentFileName = file.FileName,
                    CurrentFileDownloaded = bytesDownloaded,
                    CurrentFileTotal = totalBytes,
                    TotalDownloaded = previousFilesTotal + bytesDownloaded,
                    TotalExpected = model.TotalSizeBytes,
                    FileIndex = fileIndex,
                    FileCount = model.Files.Count,
                });
            }

            // Flush and close before moving
            await fileStream.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Clean up partial download on cancellation
            TryDeleteFile(tempPath);
            throw;
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }

        // Move temp file to final location
        if (File.Exists(filePath))
            File.Delete(filePath);

        File.Move(tempPath, filePath);
    }

    /// <summary>
    /// Validates that a file size is within 10% of the expected size.
    /// This accommodates minor version differences in model files.
    /// </summary>
    private static bool IsFileSizeAcceptable(long actualSize, long expectedSize)
    {
        if (expectedSize <= 0) return actualSize > 0;
        double ratio = (double)actualSize / expectedSize;
        return ratio is >= 0.9 and <= 1.1;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
