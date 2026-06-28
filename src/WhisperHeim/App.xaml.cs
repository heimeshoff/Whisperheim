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
using WhisperHeim.Services.Ffmpeg;
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
    private readonly Services.Startup.StartupMemoryCompactor _startupMemoryCompactor = new();
    private bool _isShowingError;

    // ── Long-lived services that used to live on MainWindow ────────────
    // All of these are constructed eagerly in StartupCore so that hotkeys,
    // tray icon, call-recording → auto-transcription, and dictation overlay
    // all work even when the user has Start Minimized enabled and no
    // window has ever been opened.

    /// <summary>
    /// True when this WPF process is the first launch after a Velopack
    /// install. Populated in <see cref="OnStartup"/> from the
    /// <see cref="Program.IsFirstRun"/> flag (set by Velopack's
    /// <c>OnFirstRun</c> hook in Program.cs) and the <c>VELOPACK_FIRSTRUN</c>
    /// environment variable Velopack also sets. Task 108 consumes this to
    /// decide whether to surface the first-run model download dialog;
    /// for now it is just recorded.
    /// </summary>
    public bool IsFirstRun { get; private set; }

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
    private FfmpegDetector? _ffmpegDetector;
    private FfmpegPromptService? _ffmpegPromptService;
    private Services.Http.TranscribeServer? _transcribeServer;

    /// <summary>
    /// Process-wide FFmpeg detector. Populated in <see cref="StartupCore"/>
    /// before any UI is shown. Exposed publicly so UI bindings (e.g.
    /// <c>GeneralPage</c>'s FFmpeg status card) can subscribe to
    /// <see cref="FfmpegDetector.StateChanged"/> without taking a constructor
    /// dependency through MainWindow.
    /// </summary>
    public FfmpegDetector? FfmpegDetector => _ffmpegDetector;

    /// <summary>
    /// UI-agnostic seam used by Services-tier code to surface the install
    /// modal on the WPF dispatcher when FFmpeg is missing.
    /// </summary>
    public IFfmpegPromptService? FfmpegPromptService => _ffmpegPromptService;

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

        // Capture Velopack first-run signal. Two sources are consulted:
        //   1. Program.IsFirstRun -- flipped by VelopackApp's OnFirstRun hook
        //      in Program.cs (runs before WPF, no UI allowed there).
        //   2. VELOPACK_FIRSTRUN env var -- Velopack sets this for the first
        //      launch after install. Belt-and-braces in case the hook didn't
        //      fire (e.g. running unpacked / dotnet run with the env var set
        //      manually for testing).
        // Task 108 will use this to gate the first-run model download dialog.
        IsFirstRun = Program.IsFirstRun
            || string.Equals(
                Environment.GetEnvironmentVariable("VELOPACK_FIRSTRUN"),
                "true",
                StringComparison.OrdinalIgnoreCase);

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
                    $"Whisperheim encountered an error:\n\n{args.Exception}",
                    "Whisperheim Error",
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
                    $"Whisperheim fatal error:\n\n{ex}",
                    "Whisperheim Fatal Error",
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
                $"Whisperheim failed to start:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Whisperheim Startup Error",
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
        Trace.TraceInformation("[App] Machine id: {0}", _dataPathService.MachineId);

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

        // First-run model download dialog (Task 108).
        //
        // Surface a modal FirstRunSetupWindow when either:
        //   - this is the first launch after a Velopack install
        //     (Program.IsFirstRun / VELOPACK_FIRSTRUN), OR
        //   - any required model file is missing on disk.
        //
        // After Task 109 lands and Silero VAD + Pyannote Seg are bundled
        // into the publish output, GetMissingRequiredModels() naturally
        // omits them and the dialog shrinks to only show Parakeet for new
        // users -- no code change needed here.
        //
        // UX choice: when "start minimized" is on AND a model is missing we
        // still show the dialog. The alternative (silently boot to tray with
        // no models) leaves dictation broken until the user thinks to open
        // settings. Showing the modal is the only way to surface the
        // dependency. The dialog itself takes care of *not* appearing when
        // models are already present (next condition).
        var missingRequired = _modelManager.GetMissingRequiredModels();
        if (IsFirstRun || missingRequired.Count > 0)
        {
            if (missingRequired.Count == 0)
            {
                // First-run but nothing to download (e.g. dev environment
                // re-using an existing models folder). Persist the manifest
                // so the next launch skips this branch entirely.
                try { _modelManager.WriteManifest(); }
                catch (Exception ex)
                {
                    Trace.TraceWarning("[App] WriteManifest failed: {0}", ex.Message);
                }
            }
            else
            {
                Trace.TraceInformation(
                    "[App] First-run setup: {0} missing required model(s) -- showing dialog.",
                    missingRequired.Count);
                var setup = Views.FirstRunSetupWindow.ShowAndRun(_modelManager, missingRequired);

                if (setup.UserSkipped)
                {
                    // User chose "Skip for now". Continue boot; if they
                    // attempt to dictate without the model the existing
                    // lazy-download path (ModelDownloadDialog) will fire.
                    Trace.TraceInformation(
                        "[App] First-run setup: user skipped. Lazy-download fallback armed.");
                }
                else if (!setup.AllModelsReady)
                {
                    // Closed without skip AND not complete: treat the same
                    // as skip rather than shutting down. The user can still
                    // configure / read transcripts; dictation will trigger
                    // the lazy-download fallback.
                    Trace.TraceInformation(
                        "[App] First-run setup: dialog closed before completion. Continuing.");
                }
            }
        }

        // ── Services (all lifecycle-independent of MainWindow) ─────────
        _transcriptionService = new TranscriptionService();
        // If the user skipped first-run setup, the Parakeet model may be
        // absent. Load lazily/best-effort so the rest of the app (tray icon,
        // settings UI, transcripts viewer) still boots; the lazy-download
        // fallback (ModelDownloadDialog) will fire when dictation is
        // attempted and the model is still missing.
        try
        {
            _transcriptionService.LoadModel();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[App] TranscriptionService.LoadModel failed (likely missing model after Skip): {0}",
                ex.Message);
        }
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

        // STT API (task main-h7k2p, ADR-0001): expose the shared engine to first-party
        // local tooling (Claude) over loopback HTTP. Funnels through the same
        // TranscriptionQueueService as the UI. A bind failure is logged inside Start()
        // and is non-fatal — the tray app must still run if the API port is taken.
        var transcribeEngine = new Services.Http.QueueTranscribeEngine(_transcriptionQueueService);
        var transcribeHandler = new Services.Http.TranscribeRequestHandler(transcribeEngine);
        _transcribeServer = new Services.Http.TranscribeServer(transcribeHandler);
        _transcribeServer.Start();

        _highQualityLoopbackService = new HighQualityLoopbackService();
        _highQualityRecorderService = new HighQualityRecorderService(_dataPathService);
        _ollamaService = new OllamaService(_settingsService);

        // FFmpeg detection + install-prompt seam. The detector is a process-wide
        // singleton; the prompt service marshals to the WPF dispatcher to show
        // the modal. Background services (StreamTranscriptionService,
        // AudioFileDecoder) take only the IFfmpegPromptService abstraction so
        // they stay UI-agnostic.
        _ffmpegDetector = new FfmpegDetector();
        _ffmpegPromptService = new FfmpegPromptService(_ffmpegDetector);
        // AudioFileDecoder uses a static accessor (it's invoked from many call
        // sites including pure-static decode paths). Wire it once at startup
        // so the OGG fast-path uses the detector's resolved absolute path.
        Services.FileTranscription.AudioFileDecoder.SetDetector(_ffmpegDetector);
        // Kick off detection on a background thread once everything else has
        // booted; fire-and-forget. UI binders (e.g. General page) subscribe to
        // FfmpegDetector.StateChanged and react when the detection completes
        // or flips after a user install.
        _ = Task.Run(async () =>
        {
            try
            {
                var info = await _ffmpegDetector.DetectAsync();
                Trace.TraceInformation(
                    "[App] FFmpeg detection: {0}",
                    info is null ? "not found" : $"found ({info.VersionText}) at {info.ExecutablePath}");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[App] FFmpeg detection failed: {0}", ex.Message);
            }
        });

        _streamStorageService = new StreamStorageService(_dataPathService);
        _streamTranscriptionService = new StreamTranscriptionService(
            _transcriptionService, _streamStorageService, _ffmpegDetector, _ffmpegPromptService);

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

        // Post-startup memory housekeeping (infrastructure-g3n5t). Model load +
        // WPF init leave Large Object Heap slack that the default (non-compacting)
        // LOH never reclaims. Run a single compacting gen-2 collection on a
        // thread-pool thread after a short delay so it doesn't compete with
        // first-frame rendering or the user's first Ctrl+Win dictation.
        // Fire-and-forget; the call never faults. The working-set trim
        // (infrastructure-w7k9p) will append onto this same delayed hook.
        if (Environment.GetEnvironmentVariable("WHISPERHEIM_DISABLE_STARTUP_GC") != "1")
        {
            _ = _startupMemoryCompactor.ScheduleAsync(TimeSpan.FromSeconds(5));
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
                _streamStorageService!,
                _transcribeServer);
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

        _transcribeServer?.Dispose();
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
