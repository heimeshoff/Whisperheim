using System.Diagnostics;
using System.Windows;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Diarization;
using WhisperHeim.Services.Dictation;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Orchestration;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Startup;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Templates;
using WhisperHeim.Services.Transcription;
using WhisperHeim.Services.Tray;
using WhisperHeim.Services.Analysis;
using WhisperHeim.Services.Streams;
using WhisperHeim.Views;
using Wpf.Ui.Appearance;

namespace WhisperHeim;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly DataPathService _dataPathService = new();
    private SettingsService? _settingsService;
    private readonly AudioCaptureService _audioCaptureService = new();
    private readonly ModelManagerService _modelManager = new();
    private bool _isShowingError;

    // ── Long-lived services that used to live on MainWindow ────────────
    // All of these are constructed eagerly in StartupCore so that hotkeys,
    // tray icon, call-recording → auto-transcription, and dictation overlay
    // all work even when the user has Start Minimized enabled and no
    // window has ever been opened.

    private TranscriptionService? _transcriptionService;
    private InputSimulator? _inputSimulator;
    private FileTranscriptionService? _fileTranscriptionService;
    private TemplateService? _templateService;
    private CallRecordingService? _callRecordingService;
    private TranscriptStorageService? _transcriptStorageService;
    private SpeakerDiarizationService? _speakerDiarizationService;
    private CallTranscriptionPipeline? _callTranscriptionPipeline;
    private CallRecordingHotkeyService? _callRecordingHotkeyService;
    private TranscriptionQueueService? _transcriptionQueueService;
    private HighQualityLoopbackService? _highQualityLoopbackService;
    private HighQualityRecorderService? _highQualityRecorderService;
    private OllamaService? _ollamaService;
    private StreamStorageService? _streamStorageService;
    private StreamTranscriptionService? _streamTranscriptionService;
    private AutoTranscriptionService? _autoTranscriptionService;

    private GlobalHotkeyService? _hotkeyService;
    private DictationOrchestrator? _orchestrator;
    private DictationOverlayWindow? _overlayWindow;
    private TrayIconHost? _trayIconHost;

    // Lazy-constructed settings window. Created on first open (tray click,
    // menu item, or non-minimized startup).
    private MainWindow? _settingsWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Run as headless diarization worker if launched with --diarize-worker.
        // This must happen before any WPF initialization.
        if (e.Args.Length > 0 && e.Args[0] == "--diarize-worker")
        {
            Services.Diarization.DiarizationWorker.Run(e.Args);
            Shutdown(0);
            return;
        }

        // Global exception handler for diagnostics -- guarded against re-entrance
        // to prevent cascading MessageBox dialogs when multiple exceptions fire
        // (e.g. COM/MediaFoundation errors during audio decode).
        DispatcherUnhandledException += (_, args) =>
        {
            System.Diagnostics.Trace.TraceError("[App] Unhandled UI exception: {0}", args.Exception);
            args.Handled = true;

            if (_isShowingError)
                return;

            _isShowingError = true;
            try
            {
                MessageBox.Show(
                    $"WhisperHeim encountered an error:\n\n{args.Exception}",
                    "WhisperHeim Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isShowingError = false;
            }
        };

        // Prevent unobserved task exceptions (e.g. from parallel diarization) from crashing the app
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            System.Diagnostics.Trace.TraceError("[App] Unobserved task exception: {0}", args.Exception);
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            System.Diagnostics.Trace.TraceError(
                "[App] Unhandled domain exception (IsTerminating={0}): {1}\nStackTrace: {2}",
                args.IsTerminating, ex?.Message, ex?.StackTrace ?? "(no stack trace)");

            if (_isShowingError)
                return;

            _isShowingError = true;
            try
            {
                MessageBox.Show(
                    $"WhisperHeim fatal error:\n\n{ex}",
                    "WhisperHeim Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isShowingError = false;
            }
        };

        Exit += OnAppExit;

        try
        {
            StartupCore(e);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("[App] Startup failed: {0}", ex);
            MessageBox.Show(
                $"WhisperHeim failed to start:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "WhisperHeim Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void StartupCore(StartupEventArgs e)
    {

        // Load bootstrap config (data path pointer + machine-local settings)
        _dataPathService.Load();

        // Enable trace output to a log file for diagnostics
        var logPath = _dataPathService.LogPath;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
        Trace.Listeners.Add(new TextWriterTraceListener(logPath) { TraceOutputOptions = TraceOptions.DateTime });
        Trace.AutoFlush = true;
        Trace.TraceInformation("[App] WhisperHeim starting...");
        Trace.TraceInformation("[App] Data path: {0}", _dataPathService.DataPath);

        // Run migration from old flat structure to new per-session structure
        _dataPathService.MigrateIfNeeded();

        // Recover any orphaned in-flight WAV recordings left behind by a
        // crash / hard-kill / failed atomic move on a prior run. These are
        // moved into RecordingsPath under a `recovered_` prefix so the
        // transcripts page surfaces them as pending sessions. Best-effort;
        // never blocks startup.
        try
        {
            var recovered = RecordingFileStager.SweepOrphans(
                _dataPathService.RecordingStagingPath,
                _dataPathService.RecordingsPath);
            if (recovered > 0)
            {
                Trace.TraceInformation(
                    "[App] Startup recovery sweep restored {0} orphaned recording session(s).",
                    recovered);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[App] Startup recovery sweep failed: {0}", ex.Message);
        }

        // Initialize path-dependent static services
        ModelManagerService.Initialize(_dataPathService);
        HighQualityLoopbackService.Initialize(_dataPathService);

        // Load settings (creates file with defaults on first run)
        _settingsService = new SettingsService(_dataPathService);
        _settingsService.Load();

        // Apply the persisted theme so the UI matches the user's last choice
        var savedTheme = _settingsService.Current.General.Theme;
        if (savedTheme == "System")
        {
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            var appTheme = savedTheme == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(appTheme);
        }

        // If auto-start is enabled, refresh the registry entry so the exe path
        // stays current (handles updates that move the executable).
        var startupService = new StartupService();
        startupService.RefreshIfEnabled();

        // Check if AI models need downloading (first run)
        if (!_modelManager.AreAllModelsReady())
        {
            bool success = ModelDownloadDialog.ShowAndDownload(_modelManager);
            if (!success)
            {
                // User cancelled or download failed -- exit gracefully
                Shutdown();
                return;
            }
        }

        // ── Services (all lifecycle-independent of MainWindow) ─────────
        _transcriptionService = new TranscriptionService();
        _transcriptionService.LoadModel();
        _inputSimulator = new InputSimulator();
        _fileTranscriptionService = new FileTranscriptionService(_transcriptionService);
        _templateService = new TemplateService(_settingsService);

        _callRecordingService = new CallRecordingService(_dataPathService);
        _transcriptStorageService = new TranscriptStorageService(_dataPathService);
        _speakerDiarizationService = new SpeakerDiarizationService();
        _callTranscriptionPipeline = new CallTranscriptionPipeline(
            _speakerDiarizationService, _transcriptionService, _transcriptStorageService);
        _callRecordingHotkeyService = new CallRecordingHotkeyService(_callRecordingService);

        _transcriptionQueueService = new TranscriptionQueueService(
            _callTranscriptionPipeline,
            _fileTranscriptionService,
            _transcriptStorageService,
            () => _settingsService!.Current.General.DefaultSpeakerName);

        _highQualityLoopbackService = new HighQualityLoopbackService();
        _highQualityRecorderService = new HighQualityRecorderService(_dataPathService);
        _ollamaService = new OllamaService(_settingsService);

        _streamStorageService = new StreamStorageService(_dataPathService);
        _streamTranscriptionService = new StreamTranscriptionService(
            _transcriptionService, _streamStorageService);

        // Headless auto-transcription: enqueues each completed recording even
        // when no UI page is open to observe the recording-stopped event.
        _autoTranscriptionService = new AutoTranscriptionService(
            _callRecordingService, _transcriptionQueueService);

        // ── Tray icon (registers a hidden host window as Application.MainWindow) ──
        _trayIconHost = new TrayIconHost(
            _callRecordingService,
            onShowSettingsRequested: ShowSettingsWindow,
            onExitRequested: RequestExit);

        // ── Hotkeys + dictation orchestrator + overlay ─────────────────
        SetupHotkeysAndOrchestration();

        // Either show the settings window now, or stay in the tray.
        var startMinimized = _settingsService.Current.General.StartMinimized;
        if (startMinimized)
        {
            Trace.TraceInformation("[App] Start minimized -- MainWindow construction deferred.");
        }
        else
        {
            ShowSettingsWindow();
        }
    }

    private void SetupHotkeysAndOrchestration()
    {
        _hotkeyService = new GlobalHotkeyService();

        // Register the global dictation hotkey (low-level keyboard hook, no HWND needed)
        bool registered = _hotkeyService.Register();
        if (!registered)
        {
            Trace.TraceWarning(
                "[App] Failed to register global dictation hotkey. " +
                "Another application may own the combination.");
        }

        // Wire up the hold-to-talk orchestrator (with template support via Alt modifier)
        _orchestrator = new DictationOrchestrator(
            _hotkeyService,
            _audioCaptureService,
            _transcriptionService!,
            _inputSimulator!,
            OnDictationStateChanged,
            _templateService!,
            _settingsService!);

        _orchestrator.AudioAmplitudeChanged += OnAudioAmplitudeChanged;
        _orchestrator.PipelineError += OnPipelineError;
        _orchestrator.TemplateNoMatch += spokenText =>
            ToastWindow.Show($"No template match for: \"{spokenText}\"");

        _orchestrator.Start();

        // Initialize the dictation overlay if enabled in settings
        InitializeOverlay();

        Trace.TraceInformation(
            "[App] Orchestrator started. Dictation hotkey registered: {0}",
            registered);

        // Register call recording hotkey: Ctrl+Win+R
        var callHotkey = new HotkeyRegistration(
            ModifierKeys.Control | ModifierKeys.Win,
            VirtualKey: 0x52); // 'R' key
        bool callHkRegistered = _callRecordingHotkeyService!.Register(callHotkey);
        Trace.TraceInformation(
            "[App] Call recording hotkey registered: {0}", callHkRegistered);
    }

    /// <summary>
    /// Called by the orchestrator (on the UI thread) when dictation starts/stops.
    /// Updates both the tray icon state machine and shows/hides the dictation overlay.
    /// </summary>
    private void OnDictationStateChanged(bool isActive)
    {
        _trayIconHost?.OnDictationStateChanged(isActive);

        if (isActive)
            _overlayWindow?.ShowOverlay();
        else
            _overlayWindow?.HideOverlay();
    }

    private void InitializeOverlay()
    {
        var overlaySettings = _settingsService!.Current.Overlay;
        if (!overlaySettings.Enabled)
        {
            Trace.TraceInformation("[App] Overlay disabled in settings.");
            return;
        }

        _overlayWindow = new DictationOverlayWindow();
        _overlayWindow.ApplySettings(overlaySettings);

        Trace.TraceInformation("[App] Overlay initialized (pill mode, follows last click).");
    }

    private void OnAudioAmplitudeChanged(double rmsAmplitude)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            if (_overlayWindow is null) return;
            _overlayWindow.SetMicState(OverlayMicState.Speaking);
            _overlayWindow.UpdateAmplitude(rmsAmplitude);
        });
    }

    private void OnPipelineError(Exception ex)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _overlayWindow?.SetMicState(OverlayMicState.Error);
            Trace.TraceError("[App] Pipeline error reflected in overlay: {0}", ex.Message);
        });
    }

    /// <summary>
    /// Shows the settings window, constructing it lazily on first call.
    /// Wired to the tray icon left-click and "Settings" menu item.
    /// </summary>
    public void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            Trace.TraceInformation("[App] Showing MainWindow lazily on first request.");
            _settingsWindow = new MainWindow(
                _settingsService!,
                _audioCaptureService,
                _modelManager,
                _transcriptionService!,
                _inputSimulator!,
                _fileTranscriptionService!,
                _templateService!,
                _callRecordingService!,
                _callTranscriptionPipeline!,
                _callRecordingHotkeyService!,
                _transcriptStorageService!,
                _highQualityLoopbackService!,
                _highQualityRecorderService!,
                _dataPathService,
                _transcriptionQueueService!,
                _ollamaService!,
                _streamTranscriptionService!,
                _streamStorageService!);
        }

        _settingsWindow.ShowWindow();
    }

    /// <summary>
    /// Called by the tray "Exit" menu item to shut down the app.
    /// MainWindow's OnClosing always hides-to-tray; only this path actually exits.
    /// </summary>
    private static void RequestExit()
    {
        Application.Current.Shutdown();
    }

    private void OnAppExit(object sender, ExitEventArgs e)
    {
        Trace.TraceInformation("[App] Application exiting...");

        // If the settings window was opened, persist its position/size.
        _settingsWindow?.SaveOnExit();

        _overlayWindow?.Close();
        _orchestrator?.Dispose();
        _hotkeyService?.Dispose();
        _callRecordingHotkeyService?.Dispose();
        _autoTranscriptionService?.Dispose();
        (_callRecordingService as IDisposable)?.Dispose();
        _trayIconHost?.Dispose();
        _settingsService?.Dispose();
    }
}
