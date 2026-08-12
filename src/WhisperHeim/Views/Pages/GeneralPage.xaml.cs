using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using WhisperHeim.Services.Analysis;
using WhisperHeim.Services.Ffmpeg;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Startup;

namespace WhisperHeim.Views.Pages;

public partial class GeneralPage : UserControl
{
    private readonly SettingsService _settingsService;
    private readonly OllamaService _ollamaService;
    private readonly StartupService _startupService = new();
    private readonly FfmpegDetector? _ffmpegDetector;

    // Guards against the Checked/Unchecked handler re-firing when
    // RefreshFromSettings() pushes an externally-changed value (tray toggle, or
    // a disk reload) onto KeepModelLoadedToggle -- without this, that programmatic
    // assignment would itself raise Checked/Unchecked and re-trigger a save
    // (task infrastructure-n3p8w).
    private bool _suppressKeepModelLoadedToggleEvent;

    public GeneralPage(SettingsService settingsService, OllamaService ollamaService)
    {
        _settingsService = settingsService;
        _ollamaService = ollamaService;
        // App owns the detector singleton; resolve via the running Application.
        _ffmpegDetector = (Application.Current as App)?.FfmpegDetector;
        DataContext = _settingsService.Current.General;
        InitializeComponent();
        UpdateDataPathDisplay();
        UpdateExportFolderDisplays();
        InitializeOllamaSettings();
        RefreshFfmpegStatus();

        // Re-render when settings change underneath us (disk reload from
        // another machine, or a local Save() via another page).
        // Subscribe on Loaded / unsubscribe on Unloaded so the hook survives
        // navigation cycles (pages are cached in MainWindow._pageCache).
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _settingsService.SettingsChanged += OnSettingsChanged;
        if (_ffmpegDetector is not null)
            _ffmpegDetector.StateChanged += OnFfmpegStateChanged;
        // Catch up on any disk reload that landed while we were unloaded,
        // and highlight the active theme now that the visual tree is ready.
        RefreshFromSettings();
        RefreshFfmpegStatus();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
        if (_ffmpegDetector is not null)
            _ffmpegDetector.StateChanged -= OnFfmpegStateChanged;
    }

    private void OnFfmpegStateChanged(object? sender, EventArgs e)
    {
        // Detector raises from a worker thread; marshal to the UI.
        Dispatcher.BeginInvoke(new Action(RefreshFfmpegStatus));
    }

    private void RefreshFfmpegStatus()
    {
        var info = _ffmpegDetector?.CachedInfo;
        if (info is not null)
        {
            FfmpegStatusText.Text = $"Detected — {info.VersionText}";
            FfmpegPathText.Text = info.ExecutablePath;
            FfmpegPathText.Visibility = Visibility.Visible;
            InstallFfmpegButton.Visibility = Visibility.Collapsed;
        }
        else if (_ffmpegDetector is null)
        {
            FfmpegStatusText.Text = "Detection unavailable.";
            FfmpegPathText.Visibility = Visibility.Collapsed;
            InstallFfmpegButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            FfmpegStatusText.Text = "Not installed — needed for YouTube and Stream transcription.";
            FfmpegPathText.Visibility = Visibility.Collapsed;
            InstallFfmpegButton.Visibility = Visibility.Visible;
        }
    }

    private async void InstallFfmpegButton_Click(object sender, RoutedEventArgs e)
    {
        if (_ffmpegDetector is null) return;
        var promptService = (Application.Current as App)?.FfmpegPromptService;
        if (promptService is null)
        {
            MessageBox.Show(
                "FFmpeg install prompt is unavailable in this build.",
                "Whisperheim",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        InstallFfmpegButton.IsEnabled = false;
        try
        {
            await promptService.PromptForInstallAsync(
                "FFmpeg unlocks YouTube and Stream transcription. Install it once and Whisperheim will find it automatically.");
        }
        finally
        {
            InstallFfmpegButton.IsEnabled = true;
            RefreshFfmpegStatus();
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        RefreshFromSettings();
    }

    private void RefreshFromSettings()
    {
        // General.* instance may have been swapped on DiskReload; rebind and redraw.
        DataContext = _settingsService.Current.General;

        // Refresh Ollama endpoint/model (bootstrap fields mirrored into AppSettings).
        OllamaEndpointBox.Text = _settingsService.Current.Ollama.Endpoint;
        var currentModel = _settingsService.Current.Ollama.Model;
        if (!string.IsNullOrEmpty(currentModel))
        {
            // Ensure the combo contains the (possibly newly-loaded) model and is selected.
            if (!OllamaModelCombo.Items.Contains(currentModel))
                OllamaModelCombo.Items.Add(currentModel);
            OllamaModelCombo.SelectedItem = currentModel;
        }

        HighlightActiveTheme();

        // Push the current "keep model loaded" state onto the toggle without
        // relying on its (absent) two-way binding refresh -- General doesn't
        // implement INotifyPropertyChanged, so re-assigning DataContext to the
        // same instance above does not by itself re-evaluate bound values.
        // Mirrors the manual push already used for OllamaEndpointBox/Combo above.
        // Suppressed so setting IsChecked here does not re-fire Checked/Unchecked
        // and re-trigger a save (task infrastructure-n3p8w).
        _suppressKeepModelLoadedToggleEvent = true;
        try
        {
            KeepModelLoadedToggle.IsChecked = _settingsService.Current.General.KeepModelLoaded;
        }
        finally
        {
            _suppressKeepModelLoadedToggleEvent = false;
        }
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        _settingsService.Save();
        _startupService.SetEnabled(_settingsService.Current.General.LaunchAtStartup);
    }

    /// <summary>
    /// Handles the "keep transcription model loaded" toggle (task
    /// infrastructure-n3p8w). Delegates to <c>App.ToggleKeepModelLoaded</c>,
    /// which persists the setting and drives the symmetric load/unload action --
    /// GeneralPage does not own the recognizer lifecycle itself, mirroring how
    /// FFmpeg detection is resolved via <c>Application.Current as App</c> above.
    /// </summary>
    private void KeepModelLoadedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressKeepModelLoadedToggleEvent)
            return;

        var keepLoaded = KeepModelLoadedToggle.IsChecked == true;
        (Application.Current as App)?.ToggleKeepModelLoaded(keepLoaded);
    }

    private void ThemeLight_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("Light");
    private void ThemeDark_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("Dark");
    private void ThemeSystem_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("System");

    private void ApplyTheme(string theme)
    {
        _settingsService.Current.General.Theme = theme;
        _settingsService.Save();

        var appTheme = theme switch
        {
            "Light" => ApplicationTheme.Light,
            "Dark" => ApplicationTheme.Dark,
            _ => ApplicationTheme.Unknown // System
        };

        if (appTheme == ApplicationTheme.Unknown)
        {
            // Follow system theme
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            ApplicationThemeManager.Apply(appTheme);
        }

        HighlightActiveTheme();
    }

    private void UpdateDataPathDisplay()
    {
        var dataPath = _settingsService.DataPathService.DataPath;
        DataPathDisplay.Text = dataPath;
        MachineIdDisplay.Text = _settingsService.DataPathService.MachineId;
    }

    private void BrowseDataPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select data folder for Whisperheim",
            InitialDirectory = _settingsService.DataPathService.DataPath,
        };

        if (dialog.ShowDialog() == true)
        {
            var newPath = dialog.FolderName;

            // Reject install-dir / Velopack root before the writability check.
            // These paths would be wiped on uninstall (and possibly on update)
            // — putting user recordings there would silently lose data.
            if (DataPathService.IsInsideInstallOrLocalAppDataRoot(newPath))
            {
                MessageBox.Show(
                    "This folder lives inside Whisperheim's install directory.\n\n" +
                    $"{newPath}\n\n" +
                    "Storing your data here would cause Windows to delete it when " +
                    "Whisperheim is updated or uninstalled. Please choose a folder " +
                    "outside the install directory — your Documents folder, a cloud-" +
                    "synced folder (Google Drive, OneDrive), or any other location.",
                    "Folder Inside Install Directory",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!DataPathService.ValidatePath(newPath))
            {
                MessageBox.Show(
                    $"The selected folder is not writable:\n\n{newPath}\n\nPlease choose a different folder.",
                    "Invalid Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_settingsService.DataPathService.SetDataPath(newPath))
            {
                UpdateDataPathDisplay();
                MessageBox.Show(
                    "Data folder changed. Please restart Whisperheim for the change to take full effect.",
                    "Restart Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private void ResetDataPath_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.DataPathService.SetDataPath(null);
        UpdateDataPathDisplay();
    }

    // --- Auto-export folders (task main-m6x4v, ADR-0008) ---

    private void UpdateExportFolderDisplays()
    {
        var bootstrap = _settingsService.DataPathService.Bootstrap;

        RecordingsExportFolderDisplay.Text = string.IsNullOrEmpty(bootstrap.RecordingsExportFolder)
            ? "Not set — auto-export disabled"
            : bootstrap.RecordingsExportFolder;

        ImportsExportFolderDisplay.Text = string.IsNullOrEmpty(bootstrap.ImportsExportFolder)
            ? "Not set — auto-export disabled"
            : bootstrap.ImportsExportFolder;
    }

    private void BrowseRecordingsExportFolder_Click(object sender, RoutedEventArgs e)
    {
        var chosen = BrowseForExportFolder(
            "Select export folder for recorded conversations",
            _settingsService.DataPathService.Bootstrap.RecordingsExportFolder);
        if (chosen is null) return;

        _settingsService.DataPathService.Bootstrap.RecordingsExportFolder = chosen;
        _settingsService.DataPathService.Save();
        UpdateExportFolderDisplays();
    }

    private void ClearRecordingsExportFolder_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.DataPathService.Bootstrap.RecordingsExportFolder = null;
        _settingsService.DataPathService.Save();
        UpdateExportFolderDisplays();
    }

    private void BrowseImportsExportFolder_Click(object sender, RoutedEventArgs e)
    {
        var chosen = BrowseForExportFolder(
            "Select export folder for imported voice messages",
            _settingsService.DataPathService.Bootstrap.ImportsExportFolder);
        if (chosen is null) return;

        _settingsService.DataPathService.Bootstrap.ImportsExportFolder = chosen;
        _settingsService.DataPathService.Save();
        UpdateExportFolderDisplays();
    }

    private void ClearImportsExportFolder_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.DataPathService.Bootstrap.ImportsExportFolder = null;
        _settingsService.DataPathService.Save();
        UpdateExportFolderDisplays();
    }

    /// <summary>
    /// Shared folder-picker + writability check for the two export-folder
    /// settings, mirroring <see cref="BrowseDataPath_Click"/>'s
    /// <c>OpenFolderDialog</c> + validate pattern. Returns the chosen path,
    /// or null if the user cancelled or the chosen folder failed validation
    /// (a message box has already been shown in that case).
    /// </summary>
    private string? BrowseForExportFolder(string title, string? initialDirectory)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory,
        };

        if (dialog.ShowDialog() != true)
            return null;

        var newPath = dialog.FolderName;

        if (!DataPathService.ValidatePath(newPath))
        {
            MessageBox.Show(
                $"The selected folder is not writable:\n\n{newPath}\n\nPlease choose a different folder.",
                "Invalid Folder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        return newPath;
    }

    private void HighlightActiveTheme()
    {
        var current = _settingsService.Current.General.Theme;
        var selectedBrush = new SolidColorBrush(Color.FromArgb(0x19, 0x00, 0x5F, 0xAA)); // subtle blue highlight
        var transparentBrush = Brushes.Transparent;

        ThemeLight.Background = current == "Light" ? selectedBrush : transparentBrush;
        ThemeDark.Background = current == "Dark" ? selectedBrush : transparentBrush;
        ThemeSystem.Background = current == "System" ? selectedBrush : transparentBrush;
    }

    // --- Ollama settings ---

    private void InitializeOllamaSettings()
    {
        OllamaEndpointBox.Text = _settingsService.Current.Ollama.Endpoint;

        var currentModel = _settingsService.Current.Ollama.Model;
        if (!string.IsNullOrEmpty(currentModel))
        {
            OllamaModelCombo.Items.Add(currentModel);
            OllamaModelCombo.SelectedItem = currentModel;
        }
    }

    private void OllamaEndpoint_LostFocus(object sender, RoutedEventArgs e)
    {
        var newEndpoint = OllamaEndpointBox.Text?.Trim();
        if (!string.IsNullOrEmpty(newEndpoint))
        {
            _settingsService.Current.Ollama.Endpoint = newEndpoint;
            _settingsService.Save();
        }
    }

    private async void TestOllama_Click(object sender, RoutedEventArgs e)
    {
        // Save endpoint first
        OllamaEndpoint_LostFocus(sender, e);

        OllamaStatusText.Text = "Testing...";
        OllamaStatusText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString("#FF888888"));

        var connected = await _ollamaService.TestConnectionAsync();

        if (connected)
        {
            OllamaStatusText.Text = "Connected";
            OllamaStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FF00AA00"));

            // Auto-refresh models on successful connection
            await RefreshModelsAsync();
        }
        else
        {
            OllamaStatusText.Text = "Not reachable";
            OllamaStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FFE74856"));
        }
    }

    private async void RefreshModels_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModelsAsync();
    }

    private async Task RefreshModelsAsync()
    {
        var models = await _ollamaService.ListLocalModelsAsync();
        var currentModel = _settingsService.Current.Ollama.Model;

        OllamaModelCombo.Items.Clear();
        foreach (var model in models)
            OllamaModelCombo.Items.Add(model);

        if (models.Count == 0)
        {
            OllamaStatusText.Text = "No models found";
            OllamaStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FFE74856"));
            return;
        }

        // Restore previous selection if it still exists
        if (!string.IsNullOrEmpty(currentModel) && models.Contains(currentModel))
            OllamaModelCombo.SelectedItem = currentModel;
        else if (models.Count > 0)
            OllamaModelCombo.SelectedIndex = 0;
    }

    private void OllamaModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OllamaModelCombo.SelectedItem is string model)
        {
            _settingsService.Current.Ollama.Model = model;
            _settingsService.Save();
            Trace.TraceInformation("[GeneralPage] Ollama model set to: {0}", model);
        }
    }
}
