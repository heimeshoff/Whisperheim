using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WhisperHeim.Models;

namespace WhisperHeim.Services.Settings;

/// <summary>
/// Manages the bootstrap configuration and resolves data paths.
/// The bootstrap config lives in %APPDATA%\WhisperHeim\ and contains a pointer
/// to the actual data folder (which may be on a cloud-synced drive).
/// </summary>
public sealed class DataPathService
{
    /// <summary>The fixed local directory for bootstrap config, models, and logs.</summary>
    public static readonly string LocalRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhisperHeim");

    /// <summary>
    /// Machine-local root for transient files that must never live in a synced
    /// <see cref="DataPath"/>. Currently used to stage in-flight WAV recordings
    /// before they are atomically moved into the synced data folder on stop.
    /// Rooted in <c>%LOCALAPPDATA%\WhisperHeim\</c> so it survives data-path
    /// reconfiguration and is invisible to Google Drive / OneDrive / Dropbox.
    /// </summary>
    public static readonly string LocalAppDataRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhisperHeim");

    private static readonly string BootstrapPath =
        Path.Combine(LocalRoot, "bootstrap.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private BootstrapConfig _bootstrap = new();

    /// <summary>
    /// Raised after <see cref="SetDataPath"/> successfully changes the data path
    /// at runtime. Subscribers (e.g. <see cref="SettingsService"/>) can use this to
    /// recreate path-dependent watchers.
    /// </summary>
    public event EventHandler<string>? DataPathChanged;

    /// <summary>The current bootstrap configuration (machine-local settings + data path pointer).</summary>
    public BootstrapConfig Bootstrap => _bootstrap;

    /// <summary>
    /// The resolved data path. If the bootstrap config has a custom dataPath set,
    /// that path is used; otherwise falls back to the local root.
    /// </summary>
    public string DataPath =>
        !string.IsNullOrWhiteSpace(_bootstrap.DataPath) ? _bootstrap.DataPath : LocalRoot;

    /// <summary>Path to settings.json (synced).</summary>
    public string SettingsPath => Path.Combine(DataPath, "settings.json");

    /// <summary>Root directory for recordings (synced).</summary>
    public string RecordingsPath => Path.Combine(DataPath, "recordings");

    /// <summary>Root directory for stream transcripts (synced).</summary>
    public string StreamsPath => Path.Combine(DataPath, "streams");

    /// <summary>Root directory for custom voice samples (synced).</summary>
    public string VoicesPath => Path.Combine(DataPath, "voices");

    /// <summary>Root directory for AI models (local, not synced).</summary>
    public string ModelsPath => Path.Combine(LocalRoot, "models");

    /// <summary>
    /// Machine-local staging directory for in-flight WAV recordings. WAV files
    /// are written here while NAudio holds an exclusive write handle, then
    /// atomically moved into <see cref="RecordingsPath"/> on stop so cloud
    /// sync clients (Google Drive, OneDrive, Dropbox) only see a complete
    /// file appear. Created on first access.
    /// </summary>
    /// <remarks>
    /// Lives on the OS drive (<c>%LOCALAPPDATA%</c>). Long recordings at
    /// 32-bit float / 48 kHz can occupy hundreds of MB while in flight —
    /// watch disk space on the system drive.
    /// </remarks>
    public string RecordingStagingPath
    {
        get
        {
            var path = Path.Combine(LocalAppDataRoot, "recording-staging");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>Path to log file (local, not synced).</summary>
    public string LogPath => Path.Combine(LocalRoot, "whisperheim.log");

    /// <summary>
    /// Loads the bootstrap config from disk. Creates with defaults on first run.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(BootstrapPath))
            {
                var json = File.ReadAllText(BootstrapPath);
                _bootstrap = JsonSerializer.Deserialize<BootstrapConfig>(json, JsonOptions) ?? new BootstrapConfig();
            }
            else
            {
                _bootstrap = new BootstrapConfig();
                Save();
            }
        }
        catch
        {
            _bootstrap = new BootstrapConfig();
            Save();
        }
    }

    /// <summary>
    /// Persists the bootstrap config to disk.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(LocalRoot);
        var json = JsonSerializer.Serialize(_bootstrap, JsonOptions);
        File.WriteAllText(BootstrapPath, json);
    }

    /// <summary>
    /// Validates that a given path is writable by creating and deleting a temp file.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <returns>True if the path exists and is writable.</returns>
    public static bool ValidatePath(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var testFile = Path.Combine(path, $".whisperheim_write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Changes the data path. Validates the new path is writable before accepting.
    /// Does NOT move existing data — the caller is responsible for migration if needed.
    /// </summary>
    /// <param name="newPath">The new data path, or null/empty to reset to default.</param>
    /// <returns>True if the path was changed successfully.</returns>
    public bool SetDataPath(string? newPath)
    {
        if (string.IsNullOrWhiteSpace(newPath))
        {
            // Reset to default (co-located with bootstrap)
            _bootstrap.DataPath = null;
            Save();
            Trace.TraceInformation("[DataPathService] Data path reset to default: {0}", LocalRoot);
            DataPathChanged?.Invoke(this, DataPath);
            return true;
        }

        if (!ValidatePath(newPath))
        {
            Trace.TraceWarning("[DataPathService] Path validation failed: {0}", newPath);
            return false;
        }

        _bootstrap.DataPath = newPath;
        Save();
        Trace.TraceInformation("[DataPathService] Data path changed to: {0}", newPath);
        DataPathChanged?.Invoke(this, DataPath);
        return true;
    }

    /// <summary>
    /// Migrates existing data from the old flat structure (all in %APPDATA%\WhisperHeim\)
    /// to the new structure on first run. Safe to call multiple times.
    /// </summary>
    public void MigrateIfNeeded()
    {
        MigrateSettingsLocalFields();
        MigrateOllamaEndpointAndModel();
        MigrateTranscriptsToRecordings();
        CleanupTextToSpeechArtifacts();
    }

    /// <summary>
    /// One-shot cleanup that runs on the first launch after the Text-to-Speech
    /// feature was removed (task 103). Deletes the Pocket TTS model directories
    /// (FP32 and int8), the user's custom voices folder, and strips the
    /// <c>"tts"</c> key from <c>settings.json</c> so it does not linger in the
    /// synced settings file. Gated by
    /// <see cref="BootstrapConfig.TtsCleanupDone"/> so it runs exactly once.
    /// </summary>
    private void CleanupTextToSpeechArtifacts()
    {
        if (_bootstrap.TtsCleanupDone)
            return;

        var removed = new List<string>();

        // 1. Delete Pocket TTS model directories (both FP32 and int8 variants).
        var modelsRoot = ModelsPath;
        foreach (var subDir in new[] { "pocket-tts-fp32", "pocket-tts-int8" })
        {
            var dir = Path.Combine(modelsRoot, subDir);
            if (TryDeleteDirectory(dir))
                removed.Add($"models/{subDir}");
        }

        // 2. Delete the custom voices folder.
        var voicesDir = VoicesPath;
        if (TryDeleteDirectory(voicesDir))
            removed.Add("voices/");

        // 3. Strip the "tts" key from settings.json so the synced file no longer
        //    carries it. Safe to do even if the key is absent.
        var settingsPath = SettingsPath;
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = File.ReadAllText(settingsPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("tts", out _))
                {
                    // Rebuild the object without the "tts" property.
                    using var ms = new MemoryStream();
                    using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();
                        foreach (var property in doc.RootElement.EnumerateObject())
                        {
                            if (property.NameEquals("tts"))
                                continue;
                            property.WriteTo(writer);
                        }
                        writer.WriteEndObject();
                    }
                    File.WriteAllBytes(settingsPath, ms.ToArray());
                    removed.Add("settings.json:tts");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "[DataPathService] TTS cleanup: failed to strip 'tts' from settings.json: {0}",
                    ex.Message);
            }
        }

        _bootstrap.TtsCleanupDone = true;
        Save();

        if (removed.Count > 0)
        {
            Trace.TraceInformation(
                "[DataPathService] TTS cleanup complete. Removed: {0}",
                string.Join(", ", removed));
        }
        else
        {
            Trace.TraceInformation(
                "[DataPathService] TTS cleanup complete. Nothing to remove.");
        }
    }

    /// <summary>
    /// Best-effort recursive directory delete. Returns <c>true</c> if the directory
    /// existed and was deleted (or could not be deleted — in which case a warning
    /// is logged), <c>false</c> if it didn't exist.
    /// </summary>
    private static bool TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return false;

        try
        {
            Directory.Delete(path, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[DataPathService] Failed to delete {0}: {1}", path, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Migrates machine-local fields from settings.json into bootstrap.json.
    /// This handles the split of WindowSettings, OverlaySettings, and audio device
    /// from the synced settings into the local bootstrap config.
    /// </summary>
    private void MigrateSettingsLocalFields()
    {
        var settingsPath = Path.Combine(LocalRoot, "settings.json");
        if (!File.Exists(settingsPath))
            return;

        // Only migrate if bootstrap doesn't already have window settings
        // (indicating this is the first run with the new structure)
        if (_bootstrap.Window.Left.HasValue || _bootstrap.Window.Top.HasValue)
            return;

        try
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null)
                return;

            // Move machine-local settings to bootstrap
            _bootstrap.Window = settings.Window;
            _bootstrap.Overlay = settings.Overlay;
            _bootstrap.AudioDevice = settings.Dictation.AudioDevice;

            Save();
            Trace.TraceInformation("[DataPathService] Migrated machine-local settings to bootstrap config.");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[DataPathService] Failed to migrate local settings: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Migrates the Ollama endpoint + model out of the synced <c>settings.json</c>
    /// into the machine-local <c>bootstrap.json</c>. Runs on first launch with the
    /// new split so existing installs don't lose their configured endpoint/model.
    /// Idempotent: once bootstrap holds a non-default endpoint or a model, we
    /// skip. We read the raw JSON because the new <see cref="AppSettings"/> no
    /// longer deserializes those two fields.
    /// </summary>
    private void MigrateOllamaEndpointAndModel()
    {
        // If bootstrap already carries a non-default endpoint or any model,
        // the user has already been on the new schema at least once.
        const string DefaultEndpoint = "http://localhost:11434";
        if (!string.IsNullOrEmpty(_bootstrap.OllamaModel) ||
            (!string.IsNullOrEmpty(_bootstrap.OllamaEndpoint) && _bootstrap.OllamaEndpoint != DefaultEndpoint))
        {
            return;
        }

        var settingsPath = Path.Combine(DataPath, "settings.json");
        if (!File.Exists(settingsPath))
            return;

        try
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ollama", out var ollamaElement))
                return;

            var migrated = false;
            if (ollamaElement.TryGetProperty("endpoint", out var endpointElement) &&
                endpointElement.ValueKind == JsonValueKind.String)
            {
                var endpoint = endpointElement.GetString();
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    _bootstrap.OllamaEndpoint = endpoint;
                    migrated = true;
                }
            }

            if (ollamaElement.TryGetProperty("model", out var modelElement) &&
                modelElement.ValueKind == JsonValueKind.String)
            {
                var model = modelElement.GetString();
                if (!string.IsNullOrWhiteSpace(model))
                {
                    _bootstrap.OllamaModel = model;
                    migrated = true;
                }
            }

            if (migrated)
            {
                Save();
                Trace.TraceInformation(
                    "[DataPathService] Migrated Ollama endpoint/model to bootstrap config.");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[DataPathService] Failed to migrate Ollama settings: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Migrates transcripts from the old flat transcripts/ folder to per-session
    /// recordings/ folders. Each transcript_YYYYMMDD_HHmmss.json (and matching .wav)
    /// gets its own subfolder: recordings/YYYYMMDD_HHmmss/.
    /// </summary>
    private void MigrateTranscriptsToRecordings()
    {
        var oldTranscriptsDir = Path.Combine(DataPath, "transcripts");
        if (!Directory.Exists(oldTranscriptsDir))
            return;

        var transcriptFiles = Directory.GetFiles(oldTranscriptsDir, "transcript_*.json");
        if (transcriptFiles.Length == 0)
            return;

        var recordingsDir = RecordingsPath;
        Directory.CreateDirectory(recordingsDir);

        foreach (var transcriptFile in transcriptFiles)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(transcriptFile);
                // Extract timestamp from "transcript_YYYYMMDD_HHmmss" or "transcript_YYYYMMDD_HHmmss_1"
                var sessionName = fileName.Replace("transcript_", "");
                // Remove any suffix like "_1" that was added for uniqueness
                var parts = sessionName.Split('_');
                if (parts.Length >= 2)
                {
                    sessionName = parts[0] + "_" + parts[1]; // YYYYMMDD_HHmmss
                }

                var sessionDir = Path.Combine(recordingsDir, sessionName);

                // Skip if already migrated
                if (Directory.Exists(sessionDir) &&
                    File.Exists(Path.Combine(sessionDir, "transcript.json")))
                    continue;

                Directory.CreateDirectory(sessionDir);

                // Move transcript JSON
                var newTranscriptPath = Path.Combine(sessionDir, "transcript.json");
                File.Move(transcriptFile, newTranscriptPath, overwrite: false);

                // Move associated WAV file if it exists
                var wavFile = Path.ChangeExtension(transcriptFile, ".wav");
                if (File.Exists(wavFile))
                {
                    var newWavPath = Path.Combine(sessionDir, "recording.wav");
                    File.Move(wavFile, newWavPath, overwrite: false);

                    // Update the audioFilePath reference in the transcript JSON
                    try
                    {
                        var json = File.ReadAllText(newTranscriptPath);
                        json = json.Replace(
                            $"\"{Path.GetFileName(wavFile)}\"",
                            "\"recording.wav\"");
                        File.WriteAllText(newTranscriptPath, json);
                    }
                    catch
                    {
                        // Non-critical — the relative path will still work
                    }
                }

                Trace.TraceInformation(
                    "[DataPathService] Migrated transcript to recordings/{0}/", sessionName);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "[DataPathService] Failed to migrate {0}: {1}",
                    Path.GetFileName(transcriptFile), ex.Message);
            }
        }

        // Remove old transcripts directory if it's now empty
        try
        {
            if (Directory.GetFiles(oldTranscriptsDir).Length == 0 &&
                Directory.GetDirectories(oldTranscriptsDir).Length == 0)
            {
                Directory.Delete(oldTranscriptsDir);
                Trace.TraceInformation("[DataPathService] Removed empty old transcripts directory.");
            }
        }
        catch
        {
            // Non-critical
        }
    }
}
