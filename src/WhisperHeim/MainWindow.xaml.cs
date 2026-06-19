using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;
using WhisperHeim.Services.Hotkey;
using WhisperHeim.Services.Input;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Settings;
using WhisperHeim.Services.Templates;
using WhisperHeim.Services.Analysis;
using WhisperHeim.Services.Streams;
using WhisperHeim.Services.Tray;
using WhisperHeim.Converters;
using WhisperHeim.Views.Pages;
using Wpf.Ui.Controls;

namespace WhisperHeim;

/// <summary>
/// Settings window — navigation, page hosting, and queue UI. Constructed
/// lazily on first user request (tray-icon click or non-minimized startup).
///
/// <para>
/// The tray icon, global hotkeys, dictation orchestrator, dictation overlay,
/// and the call-recording → transcription-queue plumbing all live in App and
/// run independently of this window. As of task 106, MainWindow is no longer
/// constructed at startup when the user has Start Minimized enabled — which
/// removed the rare empty-window flash that the previous off-screen Show/Hide
/// dance was trying (and intermittently failing) to suppress.
/// </para>
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly SettingsService _settingsService;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ModelManagerService _modelManager;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IInputSimulator _inputSimulator;
    private readonly ITemplateService _templateService;

    private readonly ICallRecordingService _callRecordingService;
    private readonly ICallTranscriptionPipeline _callTranscriptionPipeline;
    private readonly CallRecordingHotkeyService _callRecordingHotkeyService;

    private readonly ITranscriptStorageService _transcriptStorageService;
    private readonly IFileTranscriptionService _fileTranscriptionService;
    private readonly IHighQualityLoopbackService _highQualityLoopbackService;
    private readonly IHighQualityRecorderService _highQualityRecorderService;
    private readonly DataPathService _dataPathService;
    private readonly TranscriptionQueueService _transcriptionQueueService;
    private readonly OllamaService _ollamaService;
    private readonly StreamTranscriptionService _streamTranscriptionService;
    private readonly StreamStorageService _streamStorageService;
    private readonly Services.Http.TranscribeServer? _transcribeServer;

    // STT status footer health-dot brushes (Utterheim palette).
    private static readonly Brush SttGreenBrush = Freeze(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly Brush SttAmberBrush = Freeze(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush SttGreyBrush = Freeze(Color.FromRgb(0x9C, 0xA3, 0xAF));

    private static Brush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    // Cache pages so they are not recreated on every navigation
    private readonly Dictionary<string, object> _pageCache = new();

    // Sidebar collapsed state
    private bool _isSidebarCollapsed;
    private const double SidebarExpandedWidth = 200;
    private const double SidebarCollapsedWidth = 64;

    public MainWindow(
        SettingsService settingsService,
        IAudioCaptureService audioCaptureService,
        ModelManagerService modelManager,
        ITranscriptionService transcriptionService,
        IInputSimulator inputSimulator,
        IFileTranscriptionService fileTranscriptionService,
        ITemplateService templateService,
        ICallRecordingService callRecordingService,
        ICallTranscriptionPipeline callTranscriptionPipeline,
        CallRecordingHotkeyService callRecordingHotkeyService,
        ITranscriptStorageService transcriptStorageService,
        IHighQualityLoopbackService highQualityLoopbackService,
        IHighQualityRecorderService highQualityRecorderService,
        DataPathService dataPathService,
        TranscriptionQueueService transcriptionQueueService,
        OllamaService ollamaService,
        StreamTranscriptionService streamTranscriptionService,
        StreamStorageService streamStorageService,
        Services.Http.TranscribeServer? transcribeServer = null)
    {
        _settingsService = settingsService;
        _audioCaptureService = audioCaptureService;
        _modelManager = modelManager;
        _transcriptionService = transcriptionService;
        _inputSimulator = inputSimulator;
        _fileTranscriptionService = fileTranscriptionService;
        _templateService = templateService;
        _callRecordingService = callRecordingService;
        _callTranscriptionPipeline = callTranscriptionPipeline;
        _callRecordingHotkeyService = callRecordingHotkeyService;
        _transcriptStorageService = transcriptStorageService;
        _highQualityLoopbackService = highQualityLoopbackService;
        _highQualityRecorderService = highQualityRecorderService;
        _dataPathService = dataPathService;
        _transcriptionQueueService = transcriptionQueueService;
        _ollamaService = ollamaService;
        _streamTranscriptionService = streamTranscriptionService;
        _streamStorageService = streamStorageService;
        _transcribeServer = transcribeServer;

        InitializeComponent();

        // Wire up the transcription queue bottom bar
        TranscriptionBar.Initialize(_transcriptionQueueService);
        _transcriptionQueueService.ItemCompleted += OnTranscriptionItemCompleted;
        _transcriptionQueueService.ItemFailed += OnTranscriptionItemFailed;

        // STT API status footer: show the loopback endpoint + a live health dot.
        // The server runs in-process, so "is it listening?" is known directly
        // (no self-poll); busy/idle rides the queue's existing PropertyChanged.
        _transcriptionQueueService.PropertyChanged += OnQueueStatusChangedForFooter;
        UpdateSttStatusFooter();

        // Restore saved window position/size or center on screen
        RestoreWindowPosition();

        // Restore sidebar collapsed state from settings
        if (_settingsService.Current.Window.SidebarCollapsed)
        {
            ApplySidebarCollapsedState(collapsed: true, animate: false);
        }

        // Load the initial page now that InitializeComponent has set up PageContent
        NavigateTo("Dictation");

        // The window/taskbar icon is the two-tone logo
        Icon = TrayIcons.CreateTwoToneLogoIcon();
    }

    private void OnQueueStatusChangedForFooter(object? sender, PropertyChangedEventArgs e)
    {
        // IsBusy is derived from ActiveItem, which raises these property changes.
        Dispatcher.BeginInvoke(UpdateSttStatusFooter);
    }

    /// <summary>
    /// Reflects the STT API state in the always-visible status footer: green dot +
    /// endpoint when listening and idle, amber while a transcription is in flight,
    /// grey "offline" when the server never bound (e.g. port already in use).
    /// </summary>
    private void UpdateSttStatusFooter()
    {
        if (_transcribeServer is not { IsRunning: true } server)
        {
            SttHealthDot.Fill = SttGreyBrush;
            SttEndpointText.Text = "offline";
            SttStateText.Text = string.Empty;
            return;
        }

        SttEndpointText.Text = $"http://127.0.0.1:{server.Port}";

        if (_transcriptionQueueService.IsBusy)
        {
            SttHealthDot.Fill = SttAmberBrush;
            SttStateText.Text = "· busy";
        }
        else
        {
            SttHealthDot.Fill = SttGreenBrush;
            SttStateText.Text = "· idle";
        }
    }

    /// <summary>
    /// Shows this settings window. Called from App.ShowSettingsWindow().
    /// </summary>
    public void ShowWindow()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        ShowActivated = true;
        ShowInTaskbar = true;
        Visibility = Visibility.Visible;
        Show();
        Activate();
        Topmost = true;
        Topmost = false;
    }

    /// <summary>
    /// Persists the current window position to settings. Called from App.OnExit
    /// so the position survives even when the user exits while the window is
    /// still visible.
    /// </summary>
    public void SaveOnExit()
    {
        if (IsLoaded)
            SaveWindowPosition();
    }

    private TranscriptsPage GetOrCreateTranscriptsPage()
    {
        if (_pageCache.TryGetValue("Recordings", out var cached) && cached is TranscriptsPage page)
            return page;

        page = new TranscriptsPage(_transcriptStorageService, _transcriptionQueueService, _callRecordingService, _fileTranscriptionService, _ollamaService);
        page.TranscriptionRequested += OnPendingTranscriptionRequested;
        page.ReTranscriptionRequested += OnPendingTranscriptionRequested;
        _pageCache["Recordings"] = page;
        return page;
    }

    private void OnPendingTranscriptionRequested(object? sender, CallRecordingSession session)
    {
        EnqueueTranscription(session);
    }

    private void EnqueueTranscription(CallRecordingSession session)
    {
        // Use the session's title if set (e.g. from re-transcription or pending drawer),
        // otherwise derive from the session directory name (e.g. "2026-03-25_14-30-00")
        var title = session.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            var sessionDir = Path.GetDirectoryName(session.MicWavFilePath);
            title = Path.GetFileName(sessionDir) ?? "Recording";
        }
        _transcriptionQueueService.Enqueue(title, session);
    }

    private void OnTranscriptionItemCompleted(object? sender, TranscriptionQueueItem item)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            // Only refresh the Recordings page if it has actually been opened.
            if (_pageCache.TryGetValue("Recordings", out var cached) && cached is TranscriptsPage page)
                page.RefreshList();
        });
    }

    private void OnTranscriptionItemFailed(object? sender, TranscriptionQueueItem item)
    {
        // Refresh pending list so cancelled/failed recordings move back to pending
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            if (_pageCache.TryGetValue("Recordings", out var cached) && cached is TranscriptsPage page)
                page.RefreshList();
        });
    }

    /// <summary>
    /// Intercept window closing to hide to tray instead of actually closing.
    /// Real exit is driven by App (tray menu "Exit" → Application.Shutdown).
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        // Save window position/size whenever the window is closed or hidden
        SaveWindowPosition();

        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;

        base.OnClosing(e);
    }

    private bool _suppressNavSelectionSync;

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard: SelectionChanged fires during InitializeComponent before controls are ready
        if (PageContent is null) return;

        if (_suppressNavSelectionSync) return;

        if (NavList.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            // Clear bottom list selection when main list is selected
            _suppressNavSelectionSync = true;
            NavBottomList.SelectedIndex = -1;
            _suppressNavSelectionSync = false;

            NavigateTo(tag);
        }
    }

    private void NavBottomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageContent is null) return;

        if (_suppressNavSelectionSync) return;

        if (NavBottomList.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            // Clear main list selection when bottom list is selected
            _suppressNavSelectionSync = true;
            NavList.SelectedIndex = -1;
            _suppressNavSelectionSync = false;

            NavigateTo(tag);
        }
    }

    private void NavigateTo(string pageName)
    {
        if (!_pageCache.TryGetValue(pageName, out var page))
        {
            page = pageName switch
            {
                "Dictation" => new DictationPage(_settingsService, _audioCaptureService, _templateService),
                "Recordings" => GetOrCreateTranscriptsPage(),
                "Streams" => new StreamsPage(_streamTranscriptionService, _streamStorageService),
                "Settings" => new GeneralPage(_settingsService, _ollamaService),
                "About" => new AboutPage(_modelManager),
                _ => null
            };

            if (page is not null)
            {
                _pageCache[pageName] = page;
            }
        }

        if (page is not null)
        {
            PageContent.Content = page;
        }
    }

    // ── Sidebar collapse/expand ────────────────────────────────────────

    private void BrandingHeader_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ApplySidebarCollapsedState(!_isSidebarCollapsed, animate: true);

        // Persist the state
        _settingsService.Current.Window.SidebarCollapsed = _isSidebarCollapsed;
        _settingsService.Save();
    }

    /// <summary>
    /// Applies the sidebar collapsed or expanded state, optionally with animation.
    /// </summary>
    private void ApplySidebarCollapsedState(bool collapsed, bool animate)
    {
        _isSidebarCollapsed = collapsed;

        var targetWidth = collapsed ? SidebarCollapsedWidth : SidebarExpandedWidth;
        var labelVisibility = collapsed ? Visibility.Collapsed : Visibility.Visible;

        // Show/hide text labels and adjust branding layout
        BrandingTitle.Visibility = labelVisibility;
        BrandingLogo.Margin = collapsed ? new Thickness(0) : new Thickness(0, 0, 10, 0);
        BrandingHeader.Margin = collapsed ? new Thickness(4, 12, 4, 24) : new Thickness(16, 12, 16, 24);
        BrandingHeader.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        NavLabelDictation.Visibility = labelVisibility;
        NavLabelRecordings.Visibility = labelVisibility;
        NavLabelStreams.Visibility = labelVisibility;
        NavLabelSettings.Visibility = labelVisibility;
        NavLabelAbout.Visibility = labelVisibility;

        // Adjust icon margins when collapsed (center the icons)
        var iconMargin = collapsed ? new Thickness(0) : new Thickness(0, 0, 10, 0);
        foreach (var item in NavList.Items.OfType<ListBoxItem>().Concat(NavBottomList.Items.OfType<ListBoxItem>()))
        {
            if (item.Content is StackPanel sp && sp.Children[0] is Wpf.Ui.Controls.SymbolIcon icon)
            {
                icon.Margin = iconMargin;
            }
        }

        // Adjust nav panel margin for collapsed mode (center content)
        NavPanel.Margin = collapsed
            ? new Thickness(4, 0, 4, 12)
            : new Thickness(12, 0, 4, 12);

        if (animate)
        {
            var animation = new GridLengthAnimation
            {
                From = new GridLength(collapsed ? SidebarExpandedWidth : SidebarCollapsedWidth),
                To = new GridLength(targetWidth),
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);
        }
        else
        {
            SidebarColumn.Width = new GridLength(targetWidth);
        }
    }

    // ── Window position persistence ─────────────────────────────────────

    private const double DefaultWidth = 1200;
    private const double DefaultHeight = 800;

    /// <summary>
    /// Restores saved window position/size from settings, or centers on screen at default size.
    /// If saved position is off-screen (e.g., monitor disconnected), falls back to centered.
    /// </summary>
    private void RestoreWindowPosition()
    {
        var ws = _settingsService.Current.Window;

        if (ws.Left.HasValue && ws.Top.HasValue && ws.Width.HasValue && ws.Height.HasValue)
        {
            var savedRect = new System.Windows.Rect(ws.Left.Value, ws.Top.Value, ws.Width.Value, ws.Height.Value);

            if (IsRectOnScreen(savedRect))
            {
                Left = ws.Left.Value;
                Top = ws.Top.Value;
                Width = ws.Width.Value;
                Height = ws.Height.Value;

                if (ws.IsMaximized)
                {
                    WindowState = WindowState.Maximized;
                }

                Trace.TraceInformation("[MainWindow] Restored window position: {0},{1} {2}x{3} maximized={4}",
                    ws.Left, ws.Top, ws.Width, ws.Height, ws.IsMaximized);
                return;
            }

            Trace.TraceInformation("[MainWindow] Saved window position is off-screen, centering on primary monitor.");
        }

        // First launch or off-screen: center on primary screen at default size
        CenterOnPrimaryScreen();
    }

    /// <summary>
    /// Centers the window on the primary screen's work area.
    /// </summary>
    private void CenterOnPrimaryScreen()
    {
        var workArea = SystemParameters.WorkArea;
        Width = DefaultWidth;
        Height = DefaultHeight;
        Left = workArea.Left + (workArea.Width - DefaultWidth) / 2;
        Top = workArea.Top + (workArea.Height - DefaultHeight) / 2;
    }

    /// <summary>
    /// Checks if at least 100x100 pixels of the given rectangle are visible on any monitor.
    /// Uses Win32 EnumDisplayMonitors to enumerate all connected monitors.
    /// </summary>
    private static bool IsRectOnScreen(System.Windows.Rect rect)
    {
        const double minVisiblePixels = 100;
        bool isOnScreen = false;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var workArea = new System.Windows.Rect(
                    monitorInfo.rcWork.left,
                    monitorInfo.rcWork.top,
                    monitorInfo.rcWork.right - monitorInfo.rcWork.left,
                    monitorInfo.rcWork.bottom - monitorInfo.rcWork.top);

                var intersection = System.Windows.Rect.Intersect(rect, workArea);
                if (!intersection.IsEmpty &&
                    intersection.Width >= minVisiblePixels &&
                    intersection.Height >= minVisiblePixels)
                {
                    isOnScreen = true;
                }
            }
            return true; // continue enumeration
        }, IntPtr.Zero);

        return isOnScreen;
    }

    // ── Win32 interop for multi-monitor detection ───────────────────────

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// Saves the current window position, size, and maximized state to settings.
    /// </summary>
    private void SaveWindowPosition()
    {
        var ws = _settingsService.Current.Window;
        ws.IsMaximized = WindowState == WindowState.Maximized;

        // Save the restore bounds (normal position) even when maximized,
        // so we can restore to the correct position when un-maximizing.
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new System.Windows.Rect(Left, Top, Width, Height);

        ws.Left = bounds.Left;
        ws.Top = bounds.Top;
        ws.Width = bounds.Width;
        ws.Height = bounds.Height;

        _settingsService.Save();

        Trace.TraceInformation("[MainWindow] Saved window position: {0},{1} {2}x{3} maximized={4}",
            ws.Left, ws.Top, ws.Width, ws.Height, ws.IsMaximized);
    }
}
