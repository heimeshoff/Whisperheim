using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WhisperHeim.Models;

namespace WhisperHeim.Views;

/// <summary>
/// A pill-shaped, always-on-top, click-through overlay window that shows animated
/// frequency bars during active dictation. Appears at the bottom-center of the
/// primary screen. Uses WS_EX_TRANSPARENT and WS_EX_NOACTIVATE to avoid stealing
/// focus or blocking mouse clicks.
///
/// Supports five visual states (see <see cref="OverlayMicState"/>):
///   Idle      -> grey border, grey bars with gentle movement
///   Speaking  -> blue border, orange bars driven by RMS amplitude
///   NoMic     -> grey border, grey static bars
///   WarmingUp -> amber border, amber bars breathing in sync (~1 s), RMS ignored
///   Error     -> solid red fill
/// </summary>
public partial class DictationOverlayWindow : Window
{
    // ── Win32 constants ──────────────────────────────────────────────────
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    // Muted mic detection
    private const int MutedFrameThreshold = 10; // consecutive zero-RMS frames to consider mic muted

    // ── P/Invoke declarations ────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    // ── Brand colors ─────────────────────────────────────────────────────
    private static readonly Color BlueBorderColor = (Color)ColorConverter.ConvertFromString("#FF25abfe");
    private static readonly Color OrangeBarColor = (Color)ColorConverter.ConvertFromString("#FFff8b00");
    // Warming-up amber: a yellower warm tone, deliberately distinct from the
    // Speaking-orange (#FFff8b00) so the two busy states never read alike.
    private static readonly Color AmberBarColor = (Color)ColorConverter.ConvertFromString("#FFFFC107");
    private static readonly Color GreyColor = Color.FromRgb(0x99, 0x99, 0x99);
    private static readonly Color RedColor = Color.FromRgb(0xEE, 0x33, 0x33);

    private static readonly Duration ColorTransitionDuration = new(TimeSpan.FromMilliseconds(300));

    // ── Bar configuration ────────────────────────────────────────────────
    private const int BarCount = 12;
    private const double BarGap = 2.0;
    private const double MinBarHeightFraction = 0.05; // minimum bar height as fraction of canvas height

    // Warming-up pulse: all bars breathe in sync on this period, ignoring RMS.
    private const double WarmingPulsePeriodMs = 1000.0;

    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly Random _random = new();

    // ── Animation state ──────────────────────────────────────────────────
    private Storyboard? _fadeIn;
    private Storyboard? _fadeOut;
    private DispatcherTimer? _barAnimationTimer;

    private bool _isVisible;
    private OverlayMicState _currentState = OverlayMicState.Idle;

    // Smoothed RMS value for amplitude-driven animation
    private double _smoothedRms;
    private const double RmsSmoothingFactor = 0.3;

    // ── Muted mic detection ─────────────────────────────────────────────
    private int _consecutiveZeroFrames;

    /// <summary>
    /// The maximum opacity the overlay will reach (set from settings).
    /// </summary>
    private double MaxOpacity { get; set; } = 0.85;

    public DictationOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        SetClickThrough();
        // Reposition after SetClickThrough() — adding WS_EX_TOOLWINDOW can
        // cause Windows to move the window on first show.
        PositionAtBottomCenter();
        InitializeBars();

        _fadeIn = (Storyboard)FindResource("FadeIn");
        _fadeOut = (Storyboard)FindResource("FadeOut");

        // Timer for animating bars (~30 fps)
        _barAnimationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _barAnimationTimer.Tick += OnBarAnimationTick;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _barAnimationTimer?.Stop();
    }

    // ── Bar initialization ───────────────────────────────────────────────

    private void InitializeBars()
    {
        BarsCanvas.Children.Clear();
        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Rectangle
            {
                Fill = new SolidColorBrush(GreyColor),
                RadiusX = 1.5,
                RadiusY = 1.5
            };
            _bars[i] = bar;
            BarsCanvas.Children.Add(bar);
        }

        // Layout bars when the wrapping Grid gets sized (Canvas alone reports 0x0)
        BarsGrid.SizeChanged += OnCanvasSizeChanged;

        // Grid may already be sized by the time Loaded fires — layout immediately
        if (BarsGrid.ActualWidth > 0 && BarsGrid.ActualHeight > 0)
        {
            BarsCanvas.Width = BarsGrid.ActualWidth;
            BarsCanvas.Height = BarsGrid.ActualHeight;
            LayoutBars();
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Propagate the Grid's actual size to the Canvas so it has real dimensions
        BarsCanvas.Width = BarsGrid.ActualWidth;
        BarsCanvas.Height = BarsGrid.ActualHeight;
        LayoutBars();
    }

    private void LayoutBars()
    {
        double canvasWidth = BarsCanvas.Width;
        double canvasHeight = BarsCanvas.Height;
        if (double.IsNaN(canvasWidth) || double.IsNaN(canvasHeight)) return;
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        double totalGaps = (BarCount - 1) * BarGap;
        double barWidth = (canvasWidth - totalGaps) / BarCount;
        if (barWidth < 1) barWidth = 1;

        for (int i = 0; i < BarCount; i++)
        {
            var bar = _bars[i];
            double x = i * (barWidth + BarGap);
            double barHeight = canvasHeight * MinBarHeightFraction;

            bar.Width = barWidth;
            bar.Height = barHeight;
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, (canvasHeight - barHeight) / 2);
        }
    }

    // ── Bar animation tick ───────────────────────────────────────────────

    private void OnBarAnimationTick(object? sender, EventArgs e)
    {
        double canvasHeight = BarsCanvas.Height;
        if (double.IsNaN(canvasHeight) || canvasHeight <= 0) return;

        for (int i = 0; i < BarCount; i++)
        {
            double targetHeight;

            if (_currentState == OverlayMicState.WarmingUp)
            {
                // Synchronized breathing pulse on a ~1 s cosine cycle, identical for
                // every bar and independent of RMS — unmistakably "busy", not frozen.
                double phase = (Environment.TickCount64 % (long)WarmingPulsePeriodMs) / WarmingPulsePeriodMs;
                double pulse = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * phase); // 0..1
                double heightFraction = MinBarHeightFraction + (1.0 - MinBarHeightFraction) * pulse;
                targetHeight = canvasHeight * heightFraction;
            }
            else if (_currentState == OverlayMicState.Speaking || _currentState == OverlayMicState.Idle)
            {
                double amplitude = Math.Max(_smoothedRms, 0.0001);

                // Aggressive normalization: even soft speech (~0.003 RMS) drives strong bars.
                // log10(0.0001)=-4, log10(0.003)≈-2.5 → map [-4, -1] to [0, 1],
                // then apply pow(0.4) curve to boost low values further.
                double normalized = Math.Clamp((Math.Log10(amplitude) + 4.0) / 3.0, 0.0, 1.0);
                normalized = Math.Pow(normalized, 0.4);

                // Per-bar random variation for spectrum analyzer effect
                double randomFactor = 0.3 + _random.NextDouble() * 0.7;
                double heightFraction = MinBarHeightFraction + (1.0 - MinBarHeightFraction) * normalized * randomFactor;

                targetHeight = canvasHeight * heightFraction;
            }
            else
            {
                // NoMic or Error: static minimal bars
                targetHeight = canvasHeight * MinBarHeightFraction;
            }

            var bar = _bars[i];
            // Smooth transition: lerp toward target
            double current = bar.Height;
            double lerped = current + 0.35 * (targetHeight - current);
            bar.Height = lerped;
            Canvas.SetTop(bar, (canvasHeight - lerped) / 2);
        }
    }

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>
    /// Applies the overlay settings (opacity).
    /// Position is fixed bottom-center, size is fixed for the pill.
    /// </summary>
    public void ApplySettings(OverlaySettings settings)
    {
        MaxOpacity = settings.Opacity;
    }

    /// <summary>
    /// Shows the overlay at the bottom-center of the primary screen with a fade-in animation.
    /// </summary>
    public void ShowOverlay()
    {
        if (_isVisible) return;
        _isVisible = true;

        // Show() first so the window has an HWND (DPI-resolved against its monitor) and a
        // completed layout pass before we place it — only then is bottom-center positioning
        // accurate. Positioning *before* Show() was the first-show bug: coordinates were set
        // without a DPI context and the post-Show reposition was gated behind a flag that was
        // still false on the first show. The window's Opacity starts at 0 (see XAML) and the
        // fade-in begins after positioning, so it is never visible at the wrong spot — no flash.
        Show();
        PositionAtBottomCenter();

        if (_fadeIn != null)
        {
            var anim = (DoubleAnimation)_fadeIn.Children[0];
            anim.To = MaxOpacity;
        }

        _fadeIn?.Begin(this);
        _barAnimationTimer?.Start();

        // Start in Speaking state (blue/orange) — mic is active during dictation
        _currentState = OverlayMicState.NoMic; // sentinel so transition is applied
        SetMicState(OverlayMicState.Speaking);

        Trace.TraceInformation("[DictationOverlay] Shown at ({0}, {1}).", Left, Top);
    }

    /// <summary>
    /// Realizes the window and runs its first-<see cref="Window.Show"/> layout/DPI settling pass
    /// once at startup — invisibly (Opacity stays 0 from XAML) — then hides it again.
    /// <para>
    /// The very first Show() of a WPF window goes through a messy layout/DPI pass: on a scaled
    /// display (e.g. 125%) the window briefly inflates and resolves at the wrong physical position
    /// (measured: it rests top-right on the first show, then every show after lands correctly at
    /// bottom-center). Doing that throwaway first show here means the first *real* dictation overlay
    /// is already a "second show" and lands correctly. No flash: ShowInTaskbar/ShowActivated are
    /// false and Opacity is 0 throughout. Call once after construction.
    /// </para>
    /// </summary>
    public void PrewarmFirstShow()
    {
        if (_isVisible) return; // never warm over a real dictation
        Show();
        // Keep the window realized for one dispatcher cycle so the first-show layout/DPI pass
        // actually completes before we hide it — a synchronous Show()+Hide() can hide before the
        // settling finishes, leaving the next show still "first-show"-like.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!_isVisible) Hide();
        }));
        Trace.TraceInformation("[DictationOverlay] Pre-warmed first show (settling DPI/layout).");
    }

    /// <summary>
    /// Hides the overlay with a fade-out animation.
    /// </summary>
    public void HideOverlay()
    {
        if (!_isVisible) return;
        _isVisible = false;
        _currentState = OverlayMicState.Idle;
        _smoothedRms = 0;
        _consecutiveZeroFrames = 0;

        _barAnimationTimer?.Stop();

        if (_fadeOut != null)
        {
            _fadeOut.Completed -= OnFadeOutCompleted;
            _fadeOut.Completed += OnFadeOutCompleted;
            _fadeOut.Begin(this);
        }
        else
        {
            Hide();
        }

        Trace.TraceInformation("[DictationOverlay] Hiding.");
    }

    /// <summary>
    /// Sets the visual state of the overlay.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetMicState(OverlayMicState newState)
    {
        if (!_isVisible) return;
        if (_currentState == newState) return;

        var previousState = _currentState;
        _currentState = newState;

        switch (newState)
        {
            case OverlayMicState.Idle:
                AnimateBorderColor(GreyColor);
                SetBarColor(GreyColor);
                PillBorder.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x2D, 0x2D, 0x2D));
                _smoothedRms = 0;
                break;

            case OverlayMicState.Speaking:
                AnimateBorderColor(BlueBorderColor);
                SetBarColor(OrangeBarColor);
                PillBorder.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x2D, 0x2D, 0x2D));
                break;

            case OverlayMicState.NoMic:
                AnimateBorderColor(GreyColor);
                SetBarColor(GreyColor);
                PillBorder.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x2D, 0x2D, 0x2D));
                _smoothedRms = 0;
                break;

            case OverlayMicState.WarmingUp:
                AnimateBorderColor(AmberBarColor);
                SetBarColor(AmberBarColor);
                PillBorder.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x2D, 0x2D, 0x2D));
                _smoothedRms = 0;
                break;

            case OverlayMicState.Error:
                AnimateBorderColor(RedColor);
                SetBarColor(RedColor);
                PillBorder.Background = new SolidColorBrush(RedColor);
                _smoothedRms = 0;
                break;
        }

        Trace.TraceInformation("[DictationOverlay] State: {0} -> {1}", previousState, newState);
    }

    /// <summary>
    /// Updates the bar heights based on real-time RMS audio amplitude.
    /// Must be called on the UI thread. Only effective in Speaking state.
    /// </summary>
    public void UpdateAmplitude(double rmsAmplitude)
    {
        if (!_isVisible) return;

        rmsAmplitude = Math.Clamp(rmsAmplitude, 0.0, 1.0);

        // Detect muted mic: sustained zero RMS means OS-level mute
        if (rmsAmplitude < 0.0001)
        {
            _consecutiveZeroFrames++;
            if (_consecutiveZeroFrames >= MutedFrameThreshold)
            {
                SetMicState(OverlayMicState.NoMic);
                _smoothedRms = 0;
                return;
            }
        }
        else
        {
            if (_consecutiveZeroFrames >= MutedFrameThreshold)
            {
                // Mic was muted, now it's back — restore Speaking state
                SetMicState(OverlayMicState.Speaking);
            }
            _consecutiveZeroFrames = 0;
        }

        _smoothedRms = _smoothedRms + RmsSmoothingFactor * (rmsAmplitude - _smoothedRms);
    }

    /// <summary>
    /// Convenience: Switches to the Speaking state.
    /// </summary>
    public void NotifySpeechActivity()
    {
        SetMicState(OverlayMicState.Speaking);
    }

    /// <summary>
    /// Convenience: Switches back to the Idle state.
    /// </summary>
    public void NotifySpeechPause()
    {
        SetMicState(OverlayMicState.Idle);
    }

    // ── Positioning ──────────────────────────────────────────────────────

    // Gap between the pill's bottom edge and the work-area bottom.
    private const double BottomMargin = 20.0;

    /// <summary>
    /// Positions the pill overlay at the bottom-center of the primary screen.
    /// Prefers the realized layout size (<see cref="FrameworkElement.ActualWidth"/>/
    /// <see cref="FrameworkElement.ActualHeight"/>), which is only valid once the window
    /// has been shown and laid out; falls back to the declared <see cref="FrameworkElement.Width"/>/
    /// <see cref="FrameworkElement.Height"/> before that first layout pass.
    /// </summary>
    private void PositionAtBottomCenter()
    {
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : Height;

        var (left, top) = ComputeBottomCenter(SystemParameters.WorkArea, width, height, BottomMargin);
        Left = left;
        Top = top;
    }

    /// <summary>
    /// Pure geometry: the top-left point that horizontally centers a
    /// <paramref name="width"/>×<paramref name="height"/> overlay within
    /// <paramref name="workArea"/> and rests it <paramref name="bottomMargin"/> px above
    /// the work-area bottom. Extracted from <see cref="PositionAtBottomCenter"/> so the
    /// placement math is unit-testable without a live WPF window — the Show()/DPI/lifecycle
    /// plumbing that makes the *first* show land correctly is verified manually via /deploy.
    /// </summary>
    internal static (double Left, double Top) ComputeBottomCenter(
        Rect workArea, double width, double height, double bottomMargin)
    {
        double left = workArea.Left + (workArea.Width - width) / 2.0;
        double top = workArea.Bottom - height - bottomMargin;
        return (left, top);
    }

    // ── Color helpers ────────────────────────────────────────────────────

    private void AnimateBorderColor(Color targetColor)
    {
        var anim = new ColorAnimation(targetColor, ColorTransitionDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        PillBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    private void SetBarColor(Color color)
    {
        var brush = new SolidColorBrush(color);
        for (int i = 0; i < BarCount; i++)
        {
            _bars[i].Fill = brush;
        }
    }

    // ── Fade-out callback ────────────────────────────────────────────────

    private void OnFadeOutCompleted(object? sender, EventArgs e)
    {
        _fadeOut!.Completed -= OnFadeOutCompleted;
        Hide();
        Opacity = 0;
    }

    // ── Click-through ────────────────────────────────────────────────────

    private void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

}
