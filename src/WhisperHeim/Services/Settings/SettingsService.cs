using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using WhisperHeim.Models;

namespace WhisperHeim.Services.Settings;

/// <summary>
/// Event args carrying the up-to-date <see cref="AppSettings"/> after a reload
/// from disk or a local save.
/// </summary>
public sealed class SettingsChangedEventArgs : EventArgs
{
    public SettingsChangedEventArgs(AppSettings settings, SettingsChangeSource source)
    {
        Settings = settings;
        Source = source;
    }

    /// <summary>The current settings after the change.</summary>
    public AppSettings Settings { get; }

    /// <summary>Whether the change came from a local save or a disk-driven reload.</summary>
    public SettingsChangeSource Source { get; }
}

/// <summary>Identifies what caused a <see cref="SettingsService.SettingsChanged"/> fire.</summary>
public enum SettingsChangeSource
{
    /// <summary>A local <see cref="SettingsService.Save"/> call.</summary>
    LocalSave = 0,

    /// <summary>The on-disk <c>settings.json</c> changed under us.</summary>
    DiskReload = 1,
}

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON.
/// The settings file location is resolved by <see cref="DataPathService"/>.
/// Machine-local settings (window, overlay, audio device, Ollama endpoint/model)
/// live in the bootstrap config.
///
/// Watches <c>settings.json</c> for external writes from another WhisperHeim
/// instance (cloud-sync scenario) and raises <see cref="SettingsChanged"/> so
/// the UI can re-render. Self-writes are suppressed for 5s after every
/// <see cref="Save"/>.
/// </summary>
public sealed class SettingsService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private const int DebounceMilliseconds = 500;
    private const int SelfWriteSuppressionSeconds = 5;
    private const int FileReadRetryCount = 3;
    private const int FileReadRetryDelayMs = 100;

    private readonly DataPathService _dataPathService;
    private readonly object _sync = new();

    private AppSettings _current = new();
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _debounceTimer;
    private DateTime _suppressReloadUntil = DateTime.MinValue;
    private bool _isDisposed;

    /// <summary>
    /// Raised after the in-memory settings change, either from a local
    /// <see cref="Save"/> or from an external write to <c>settings.json</c>.
    /// Always raised on the UI thread via <see cref="Application.Current"/>'s
    /// dispatcher.
    /// </summary>
    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public SettingsService(DataPathService dataPathService)
    {
        _dataPathService = dataPathService;
        _dataPathService.DataPathChanged += OnDataPathChanged;
    }

    /// <summary>The current in-memory settings.</summary>
    public AppSettings Current => _current;

    /// <summary>The data path service for resolving paths and accessing machine-local settings.</summary>
    public DataPathService DataPathService => _dataPathService;

    /// <summary>
    /// Loads settings from disk. If the file does not exist, creates it with defaults.
    /// Also synchronizes machine-local settings from bootstrap config into the in-memory model.
    /// Starts watching the settings file for external changes.
    /// </summary>
    public void Load()
    {
        var settingsPath = _dataPathService.SettingsPath;
        var needsInitialSave = false;
        try
        {
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                _current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                _current = new AppSettings();
                needsInitialSave = true;
            }
        }
        catch
        {
            // If the file is corrupt, reset to defaults
            _current = new AppSettings();
            needsInitialSave = true;
        }

        // Synchronize machine-local settings from bootstrap config into the in-memory model
        // so that existing code reading Current.Window/Overlay/Dictation.AudioDevice still works.
        // Do this before any Save() so bootstrap-sourced fields (Ollama endpoint/model, etc.)
        // survive the initial file creation.
        SyncFromBootstrap();

        if (needsInitialSave)
        {
            Save(); // create the file with defaults
        }

        StartWatcher();
    }

    /// <summary>
    /// Persists the current settings to disk.
    /// Before writing, re-reads the on-disk <c>settings.json</c> and merges list
    /// fields (templates, template groups, analysis templates) so that concurrent
    /// additions from another machine are not clobbered.
    /// Machine-local settings are also persisted to the bootstrap config.
    /// Raises <see cref="SettingsChanged"/> on the UI thread.
    /// </summary>
    public void Save()
    {
        lock (_sync)
        {
            // Pre-save reload + merge: pull in any list additions that landed from
            // another machine between our last Load/reload and this write.
            MergeListsFromDisk();

            // Before saving, push machine-local settings back to bootstrap config
            SyncToBootstrap();

            var settingsPath = _dataPathService.SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            var json = JsonSerializer.Serialize(_current, JsonOptions);
            File.WriteAllText(settingsPath, json);

            // Suppress the watcher event our own write will trigger.
            _suppressReloadUntil = DateTime.UtcNow.AddSeconds(SelfWriteSuppressionSeconds);
        }

        RaiseSettingsChanged(SettingsChangeSource.LocalSave);
    }

    /// <summary>
    /// Copies machine-local settings from bootstrap config into the in-memory AppSettings.
    /// This allows existing code to read window/overlay/device/ollama settings from AppSettings.
    /// </summary>
    private void SyncFromBootstrap()
    {
        var bootstrap = _dataPathService.Bootstrap;
        _current.Window = bootstrap.Window;
        _current.Overlay = bootstrap.Overlay;
        _current.Dictation.AudioDevice = bootstrap.AudioDevice;
        _current.Ollama.Endpoint = bootstrap.OllamaEndpoint;
        _current.Ollama.Model = bootstrap.OllamaModel;
        _current.General.KeepModelLoaded = bootstrap.KeepModelLoaded;
    }

    /// <summary>
    /// Pushes machine-local settings from AppSettings back into the bootstrap config.
    /// </summary>
    private void SyncToBootstrap()
    {
        var bootstrap = _dataPathService.Bootstrap;
        bootstrap.Window = _current.Window;
        bootstrap.Overlay = _current.Overlay;
        bootstrap.AudioDevice = _current.Dictation.AudioDevice;
        bootstrap.OllamaEndpoint = _current.Ollama.Endpoint;
        bootstrap.OllamaModel = _current.Ollama.Model;
        bootstrap.KeepModelLoaded = _current.General.KeepModelLoaded;
        _dataPathService.Save();
    }

    // ────────────────────────────────────────────────
    //  Pre-save merge
    // ────────────────────────────────────────────────

    /// <summary>
    /// Re-reads <c>settings.json</c> and merges list fields (Templates.Items,
    /// Templates.Groups, Ollama.AnalysisTemplates) into <see cref="_current"/>.
    /// Scalar fields keep <c>_current</c>'s values (local user just changed them).
    /// Best-effort: any failure is logged and we proceed with the current state.
    /// </summary>
    private void MergeListsFromDisk()
    {
        var settingsPath = _dataPathService.SettingsPath;
        var json = TryReadFileWithRetry(settingsPath);
        if (json is null)
            return;

        AppSettings? onDisk;
        try
        {
            onDisk = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            Trace.TraceWarning(
                "[SettingsService] Pre-save merge skipped: settings.json could not be parsed ({0})",
                ex.Message);
            return;
        }

        if (onDisk is null)
            return;

        // Merge template items: union by (Name, Group) — add anything on disk that
        // our in-memory state doesn't have. The local user's list is authoritative
        // for what we already know about.
        MergeTemplateItems(_current.Templates.Items, onDisk.Templates.Items);
        MergeTemplateGroups(_current.Templates.Groups, onDisk.Templates.Groups);
        MergeAnalysisTemplates(_current.Ollama.AnalysisTemplates, onDisk.Ollama.AnalysisTemplates);
    }

    private static void MergeTemplateItems(List<TemplateItem> current, List<TemplateItem> onDisk)
    {
        foreach (var remote in onDisk)
        {
            var match = current.FirstOrDefault(t =>
                string.Equals(t.Name, remote.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.Group ?? string.Empty, remote.Group ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                current.Add(remote);
            }
        }
    }

    private static void MergeTemplateGroups(List<TemplateGroup> current, List<TemplateGroup> onDisk)
    {
        foreach (var remote in onDisk)
        {
            if (!current.Any(g => string.Equals(g.Name, remote.Name, StringComparison.OrdinalIgnoreCase)))
            {
                current.Add(remote);
            }
        }
    }

    private static void MergeAnalysisTemplates(List<AnalysisPromptTemplate> current, List<AnalysisPromptTemplate> onDisk)
    {
        foreach (var remote in onDisk)
        {
            if (!current.Any(t => string.Equals(t.Id, remote.Id, StringComparison.OrdinalIgnoreCase)))
            {
                current.Add(remote);
            }
        }
    }

    // ────────────────────────────────────────────────
    //  Disk-driven reload (FileSystemWatcher)
    // ────────────────────────────────────────────────

    private void StartWatcher()
    {
        StopWatcher();

        var dir = _dataPathService.DataPath;
        try
        {
            Directory.CreateDirectory(dir);

            // FileName is required for Created/Renamed to fire — many editors and
            // cloud-sync daemons (Google Drive, OneDrive, VS Code) write atomically
            // via a temp file that is then renamed over the target, which only
            // surfaces as Created/Renamed, not as an in-place Changed.
            _watcher = new FileSystemWatcher(dir, "settings.json")
            {
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size
                             | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnWatcherEvent;
            _watcher.Created += OnWatcherEvent;
            _watcher.Renamed += OnWatcherEvent;

            Trace.TraceInformation("[SettingsService] Watching {0} for external changes.",
                _watcher.Filter);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[SettingsService] Failed to start FileSystemWatcher: {0}", ex.Message);
            _watcher = null;
        }
    }

    private void StopWatcher()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnWatcherEvent;
            _watcher.Created -= OnWatcherEvent;
            _watcher.Renamed -= OnWatcherEvent;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_debounceTimer is not null)
        {
            _debounceTimer.Stop();
            _debounceTimer = null;
        }
    }

    private void OnWatcherEvent(object? sender, FileSystemEventArgs e)
    {
        if (DateTime.UtcNow < _suppressReloadUntil)
            return;

        // Debounce on the UI thread so multiple rapid I/O events collapse to one reload.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        dispatcher.BeginInvoke(() =>
        {
            if (_isDisposed) return;

            if (_debounceTimer is null)
            {
                _debounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds)
                };
                _debounceTimer.Tick += OnDebounceTick;
            }

            // Reset the debounce window on every new event.
            _debounceTimer.Stop();
            _debounceTimer.Start();
        });
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();
        ReloadFromDisk();
    }

    /// <summary>
    /// Public entry point for subscribers (e.g. mutation methods in
    /// <see cref="WhisperHeim.Services.Templates.TemplateService"/>) to pull the
    /// latest on-disk state into <see cref="_current"/> before applying a mutation.
    /// Does <b>not</b> raise <see cref="SettingsChanged"/> — the caller will save
    /// shortly after, and the save path raises the event.
    /// </summary>
    public void ReloadFromDiskForMutation()
    {
        if (_isDisposed) return;

        var settingsPath = _dataPathService.SettingsPath;
        var json = TryReadFileWithRetry(settingsPath);
        if (json is null)
            return;

        try
        {
            var incoming = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (incoming is null) return;

            lock (_sync)
            {
                _current = incoming;
                SyncFromBootstrap();
            }
        }
        catch (JsonException ex)
        {
            Trace.TraceWarning(
                "[SettingsService] Pre-mutation reload skipped: settings.json could not be parsed ({0})",
                ex.Message);
        }
    }

    /// <summary>
    /// Reads <c>settings.json</c> from disk, preserves machine-local fields, swaps
    /// <see cref="_current"/>, and raises <see cref="SettingsChanged"/>.
    /// Failures are logged; current state is preserved.
    /// </summary>
    private void ReloadFromDisk()
    {
        if (_isDisposed) return;

        if (DateTime.UtcNow < _suppressReloadUntil)
            return;

        var settingsPath = _dataPathService.SettingsPath;
        var json = TryReadFileWithRetry(settingsPath);
        if (json is null)
        {
            // File may have vanished or been locked for too long. Keep current state.
            return;
        }

        AppSettings? incoming;
        try
        {
            incoming = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            // Partial / mid-write JSON — the cloud sync client might still be writing.
            // Keep current state; the next event will retry.
            Trace.TraceWarning(
                "[SettingsService] Disk reload skipped: settings.json could not be parsed ({0})",
                ex.Message);
            return;
        }

        if (incoming is null)
            return;

        lock (_sync)
        {
            _current = incoming;
            // Machine-local fields are always ours.
            SyncFromBootstrap();
        }

        Trace.TraceInformation("[SettingsService] Reloaded settings.json from disk.");
        RaiseSettingsChanged(SettingsChangeSource.DiskReload);
    }

    /// <summary>
    /// Reads a file text, retrying a few times on <see cref="IOException"/> so that
    /// transient cloud-sync locks don't cause data loss.
    /// </summary>
    private static string? TryReadFileWithRetry(string path)
    {
        for (var attempt = 0; attempt < FileReadRetryCount; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                // Open read-share so we don't block a concurrent cloud-sync writer,
                // and re-reading the whole buffer on each attempt keeps things simple.
                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException)
            {
                if (attempt == FileReadRetryCount - 1)
                    return null;
                Thread.Sleep(FileReadRetryDelayMs);
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == FileReadRetryCount - 1)
                    return null;
                Thread.Sleep(FileReadRetryDelayMs);
            }
        }
        return null;
    }

    private void RaiseSettingsChanged(SettingsChangeSource source)
    {
        var handler = SettingsChanged;
        if (handler is null) return;

        var args = new SettingsChangedEventArgs(_current, source);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            handler(this, args);
        }
        else
        {
            dispatcher.Invoke(() => handler(this, args));
        }
    }

    private void OnDataPathChanged(object? sender, string newPath)
    {
        Trace.TraceInformation("[SettingsService] DataPath changed to {0}; recreating watcher.", newPath);
        StartWatcher();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _dataPathService.DataPathChanged -= OnDataPathChanged;
        StopWatcher();
    }
}
