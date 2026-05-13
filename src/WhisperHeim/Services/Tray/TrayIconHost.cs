using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WhisperHeim.Services.Recording;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray.Controls;

namespace WhisperHeim.Services.Tray;

/// <summary>
/// Owns the system-tray icon (a <see cref="NotifyIcon"/>) and its menu for the
/// process lifetime, independently of the main settings window.
///
/// <para>
/// Historically, <c>&lt;tray:NotifyIcon&gt;</c> was declared inside
/// <c>MainWindow.xaml</c>. To make the icon appear, the app had to call
/// <c>MainWindow.Show()</c> at startup — which is what produced the intermittent
/// empty-window flash on start-minimized launches. The fix (task 106) is to
/// hoist the tray icon out of MainWindow's visual tree entirely so MainWindow
/// can be constructed lazily on first user request.
/// </para>
///
/// <para>
/// <c>Wpf.Ui.Tray.TrayManager.Register</c> calls
/// <c>PresentationSource.FromVisual(Application.Current.MainWindow)</c> and
/// silently fails if it returns null. <c>WindowInteropHelper.EnsureHandle()</c>
/// alone is NOT enough — the <c>HwndSource</c> ↔ visual binding is only
/// established by <c>Window.Show()</c>. So this host <em>does</em> call
/// <c>Show()</c> on a layered, fully transparent, 1x1, off-screen window
/// (<c>AllowsTransparency=true</c> + <c>Opacity=0</c> + transparent
/// background) — DWM composites per-pixel alpha, nothing is ever painted to
/// the desktop. The window is then left shown for the life of the process;
/// calling <c>Hide()</c> after <c>Show()</c> would tear down the
/// PresentationSource and break the tray hook. The app uses
/// <c>ShutdownMode="OnExplicitShutdown"</c> so a perpetually-shown MainWindow
/// does not affect exit semantics.
/// </para>
/// <para>
/// The real settings window is a separate <c>MainWindow</c> instance that
/// opens lazily on first user request (tray click / "Settings" menu).
/// </para>
/// </summary>
public sealed class TrayIconHost : IDisposable
{
    private readonly ICallRecordingService _callRecordingService;
    private readonly Action _onShowSettingsRequested;
    private readonly Action _onExitRequested;

    // Tray icon images (idle, dictation, call-recording)
    private readonly ImageSource _idleIcon;
    private readonly ImageSource _recordingIcon;
    private readonly ImageSource _callRecordingIcon;

    private readonly Window _hiddenHostWindow;
    private readonly NotifyIcon _notifyIcon;
    private readonly Wpf.Ui.Controls.MenuItem _callRecordingMenuItem;
    private bool _disposed;

    public TrayIconHost(
        ICallRecordingService callRecordingService,
        Action onShowSettingsRequested,
        Action onExitRequested)
    {
        _callRecordingService = callRecordingService;
        _onShowSettingsRequested = onShowSettingsRequested;
        _onExitRequested = onExitRequested;

        // Generate the three icon states up-front.
        _idleIcon = CreateTwoToneTrayIcon();
        _recordingIcon = CreateMicrophoneIcon(new SolidColorBrush(Color.FromRgb(0x44, 0xCC, 0x44)));
        _callRecordingIcon = CreateMicrophoneIcon(Brushes.Orange);

        // Build a layered, transparent 1x1 off-screen host window. This will
        // be Show()n once and left shown for the life of the process.
        // AllowsTransparency=true + Opacity=0 + transparent background means
        // DWM composites the window with zero alpha across every pixel -- the
        // user never sees anything, even though the window is technically
        // "visible". WindowStyle.None removes chrome (required for
        // AllowsTransparency). ShowActivated=false prevents stealing focus at
        // logon. ShowInTaskbar=false keeps it out of the taskbar / Alt-Tab.
        _hiddenHostWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Opacity = 0.0,
            ShowInTaskbar = false,
            ShowActivated = false,
            SizeToContent = SizeToContent.Manual,
            Width = 1,
            Height = 1,
            Left = -32000,
            Top = -32000,
            Title = "WhisperHeim Tray Host",
        };

        // Designate the hidden window as the application MainWindow so that
        // TrayManager.GetParentSource() returns our HwndSource.
        Application.Current.MainWindow = _hiddenHostWindow;

        _notifyIcon = new NotifyIcon
        {
            FocusOnLeftClick = false,
            MenuOnRightClick = true,
            TooltipText = "Whisperheim",
            Icon = _idleIcon,
        };

        // Build the context menu (mirrors what used to live in MainWindow.xaml).
        var contextMenu = new ContextMenu();

        _callRecordingMenuItem = new Wpf.Ui.Controls.MenuItem
        {
            Header = "Start Call Recording",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Record24 },
        };
        _callRecordingMenuItem.Click += (_, _) => _callRecordingService.ToggleRecording();
        contextMenu.Items.Add(_callRecordingMenuItem);
        contextMenu.Items.Add(new Separator());

        var settingsMenuItem = new Wpf.Ui.Controls.MenuItem
        {
            Header = "Settings",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
        };
        settingsMenuItem.Click += (_, _) => _onShowSettingsRequested();
        contextMenu.Items.Add(settingsMenuItem);
        contextMenu.Items.Add(new Separator());

        var exitMenuItem = new Wpf.Ui.Controls.MenuItem
        {
            Header = "Exit",
            Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowExit20 },
        };
        exitMenuItem.Click += (_, _) => _onExitRequested();
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon.Menu = contextMenu;
        _notifyIcon.LeftClick += (_, _) => _onShowSettingsRequested();

        // Show the host window. Required: TrayManager.Register reads
        // PresentationSource.FromVisual(Application.Current.MainWindow), which
        // is null until Show() establishes the visual ↔ HwndSource binding.
        // Do NOT call Hide() afterwards -- that tears down the source and
        // breaks the tray hook. The window is layered/transparent so showing
        // it is invisible.
        _hiddenHostWindow.Show();

        // Register with the Windows shell.
        _notifyIcon.Register();

        if (!_notifyIcon.IsRegistered)
        {
            Trace.TraceError(
                "[TrayIconHost] NotifyIcon.Register() failed -- tray icon will not appear. " +
                "MainWindow={0}, PresentationSource={1}",
                Application.Current.MainWindow?.GetType().Name ?? "(null)",
                PresentationSource.FromVisual(_hiddenHostWindow)?.GetType().Name ?? "(null)");
        }

        // Wire up call-recording state-machine event handlers.
        _callRecordingService.RecordingStarted += OnCallRecordingStarted;
        _callRecordingService.RecordingStopped += OnCallRecordingStopped;
        _callRecordingService.DurationUpdated += OnCallRecordingDurationUpdated;
        _callRecordingService.StreamFailed += OnCallRecordingStreamFailed;

        Trace.TraceInformation("[TrayIconHost] Tray icon registered (IsRegistered={0}).", _notifyIcon.IsRegistered);
    }

    /// <summary>
    /// Called by the dictation orchestrator when push-to-talk starts or stops.
    /// Updates the tray icon and tooltip to reflect the current state, taking
    /// into account whether a call recording is also in progress.
    /// </summary>
    public void OnDictationStateChanged(bool isActive)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            if (isActive)
            {
                _notifyIcon.Icon = _recordingIcon;
                _notifyIcon.TooltipText = "Whisperheim - Recording...";
            }
            else if (_callRecordingService.IsRecording)
            {
                _notifyIcon.Icon = _callRecordingIcon;
                var duration = _callRecordingService.CurrentSession?.Duration ?? TimeSpan.Zero;
                _notifyIcon.TooltipText = $"Whisperheim - Recording call ({CallRecordingService.FormatDuration(duration)})";
            }
            else
            {
                _notifyIcon.Icon = _idleIcon;
                _notifyIcon.TooltipText = "Whisperheim";
            }

            Trace.TraceInformation("[TrayIconHost] Dictation state changed. Active: {0}", isActive);
        });
    }

    private void OnCallRecordingStarted(object? sender, CallRecordingSession session)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _notifyIcon.Icon = _callRecordingIcon;
            _notifyIcon.TooltipText = "Whisperheim - Recording call (00:00)";
            _callRecordingMenuItem.Header = "Stop Call Recording (00:00)";
            Trace.TraceInformation("[TrayIconHost] Call recording started.");
        });
    }

    private void OnCallRecordingStopped(object? sender, CallRecordingStoppedEventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _notifyIcon.Icon = _idleIcon;
            _notifyIcon.TooltipText = "Whisperheim";
            _callRecordingMenuItem.Header = "Start Call Recording";

            if (e.Exception is not null)
            {
                Trace.TraceWarning(
                    "[TrayIconHost] Call recording stopped with error: {0}",
                    e.Exception.Message);
            }
            else
            {
                Trace.TraceInformation("[TrayIconHost] Call recording stopped.");
            }
        });
    }

    private void OnCallRecordingDurationUpdated(object? sender, TimeSpan duration)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            var formatted = CallRecordingService.FormatDuration(duration);
            _callRecordingMenuItem.Header = $"Stop Call Recording ({formatted})";
            _notifyIcon.TooltipText = $"Whisperheim - Recording call ({formatted})";
        });
    }

    private void OnCallRecordingStreamFailed(object? sender, StreamFailedEventArgs e)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            Trace.TraceWarning(
                "[TrayIconHost] Call recording stream failed: {0} - {1}",
                e.Stream, e.Exception?.Message);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _callRecordingService.RecordingStarted -= OnCallRecordingStarted;
        _callRecordingService.RecordingStopped -= OnCallRecordingStopped;
        _callRecordingService.DurationUpdated -= OnCallRecordingDurationUpdated;
        _callRecordingService.StreamFailed -= OnCallRecordingStreamFailed;

        _notifyIcon.Dispose();
        _hiddenHostWindow.Close();
    }

    // ── Icon factories (moved verbatim from MainWindow) ─────────────────

    /// <summary>
    /// Renders a microphone glyph from Segoe Fluent Icons into a BitmapSource
    /// suitable for use as a tray icon.
    /// </summary>
    private static ImageSource CreateMicrophoneIcon(Brush foreground)
    {
        const int size = 32;
        // U+E720 = Microphone glyph in Segoe Fluent Icons / Segoe MDL2 Assets
        const string microphoneGlyph = "";

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            var typeface = new Typeface(
                new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            var text = new FormattedText(
                microphoneGlyph,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                24,
                foreground,
                96);

            var x = (size - text.Width) / 2;
            var y = (size - text.Height) / 2;
            ctx.DrawText(text, new System.Windows.Point(x, y));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Creates a two-tone (blue head + orange stand) tray icon using the custom mic paths.
    /// </summary>
    private static ImageSource CreateTwoToneTrayIcon()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            const double pathH = 20.5;
            const double pathX = 6.0;
            const double pathY = 2.0;
            const double pathW = 12.0;
            double scale = (size - 4) / pathH;
            double offsetX = (size - pathW * scale) / 2 - pathX * scale;
            double offsetY = (size - pathH * scale) / 2 - pathY * scale;

            var blueBrush = new SolidColorBrush(Color.FromRgb(0x25, 0xab, 0xfe));
            var orangeBrush = new SolidColorBrush(Color.FromRgb(0xff, 0x8b, 0x00));

            var headGeometry = Geometry.Parse("M12,2 C9.79,2 8,3.79 8,6 L8,12 C8,14.21 9.79,16 12,16 C14.21,16 16,14.21 16,12 L16,6 C16,3.79 14.21,2 12,2 Z");
            var standGeometry = Geometry.Parse("M6,11 L6,12 C6,15.31 8.69,18 12,18 C15.31,18 18,15.31 18,12 L18,11 L16.5,11 L16.5,12 C16.5,14.49 14.49,16.5 12,16.5 C9.51,16.5 7.5,14.49 7.5,12 L7.5,11 Z M11.25,18.5 L11.25,21 L8.5,21 L8.5,22.5 L15.5,22.5 L15.5,21 L12.75,21 L12.75,18.5 Z");

            ctx.PushTransform(new TranslateTransform(offsetX, offsetY));
            ctx.PushTransform(new ScaleTransform(scale, scale));
            ctx.DrawGeometry(blueBrush, null, headGeometry);
            ctx.DrawGeometry(orangeBrush, null, standGeometry);
            ctx.Pop();
            ctx.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
