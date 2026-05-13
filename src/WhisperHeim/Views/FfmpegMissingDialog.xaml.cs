using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using WhisperHeim.Services.Ffmpeg;

namespace WhisperHeim.Views;

/// <summary>
/// First-use modal that surfaces when an FFmpeg-dependent feature is invoked
/// but <see cref="FfmpegDetector.CachedInfo"/> is null. Offers three install
/// paths: <c>winget install Gyan.FFmpeg</c>, an "open download page" deep link
/// to gyan.dev, and a "I already installed it" re-detect button.
///
/// <para>Dialog dismisses with <see cref="Result"/> = <c>Installed</c> on
/// successful detection, or <c>Cancelled</c> if the user closes / cancels.</para>
/// </summary>
public partial class FfmpegMissingDialog : Window
{
    private const string DownloadPageUrl = "https://www.gyan.dev/ffmpeg/builds/";
    private const string WingetPackageId = "Gyan.FFmpeg";

    private readonly FfmpegDetector _detector;
    private Process? _wingetProcess;
    private bool _installInFlight;

    /// <summary>
    /// Final outcome of the dialog. Inspect after <see cref="ShowDialog"/>
    /// returns.
    /// </summary>
    public FfmpegPromptResult Result { get; private set; } = FfmpegPromptResult.Cancelled;

    public FfmpegMissingDialog(FfmpegDetector detector, string reason)
    {
        _detector = detector;
        InitializeComponent();
        ReasonText.Text = reason;
        InstallSpinner.Visibility = Visibility.Collapsed;
    }

    private async void WingetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_installInFlight) return;

        // Pre-check: is winget present?
        if (!IsWingetAvailable())
        {
            ShowStatus(
                "Windows Package Manager (winget) is not available on this machine. " +
                "Please use the Open download page link instead.");
            return;
        }

        _installInFlight = true;
        WingetButton.IsEnabled = false;
        DownloadPageButton.IsEnabled = false;
        RedetectButton.IsEnabled = false;
        HideStatus();
        HideDetectedCard();
        LogContainer.Visibility = Visibility.Visible;
        LogTextBox.Clear();
        InstallSpinner.Visibility = Visibility.Visible;
        LogHeader.Text = "Running winget install Gyan.FFmpeg...";

        try
        {
            var exit = await RunWingetInstallAsync();
            InstallSpinner.Visibility = Visibility.Collapsed;

            if (exit == 0)
            {
                LogHeader.Text = "winget install completed. Re-detecting FFmpeg...";
                await RedetectAsync(afterWinget: true);
            }
            else
            {
                LogHeader.Text = $"winget exited with code {exit}.";
                ShowStatus(InterpretWingetExitCode(exit));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[FfmpegMissingDialog] winget install failed: {0}", ex.Message);
            InstallSpinner.Visibility = Visibility.Collapsed;
            LogHeader.Text = "winget install failed.";
            AppendLog($"\n[error] {ex.Message}");
            ShowStatus(
                "Failed to launch winget. " +
                "If you have winget installed but it is not on PATH, please use the " +
                "Open download page link to install FFmpeg manually.");
        }
        finally
        {
            _wingetProcess = null;
            _installInFlight = false;
            WingetButton.IsEnabled = true;
            DownloadPageButton.IsEnabled = true;
            RedetectButton.IsEnabled = true;
        }
    }

    private void DownloadPageButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = DownloadPageUrl,
                UseShellExecute = true,
            };
            Process.Start(psi);
            ShowStatus(
                "Opened gyan.dev in your browser. Download the build of your choice, " +
                "install it, then click \"I already installed it\".");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[FfmpegMissingDialog] Open download page failed: {0}", ex.Message);
            ShowStatus($"Could not open browser: {ex.Message}");
        }
    }

    private async void RedetectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_installInFlight) return;
        await RedetectAsync(afterWinget: false);
    }

    private async Task RedetectAsync(bool afterWinget)
    {
        HideStatus();
        var info = await _detector.DetectAsync();
        if (info is not null)
        {
            ShowDetectedCard(info);
            Result = FfmpegPromptResult.Installed;
            // Brief pause so the user can read the success banner, then auto-close.
            await Task.Delay(800);
            CloseWithResult(FfmpegPromptResult.Installed);
        }
        else
        {
            if (afterWinget)
            {
                ShowStatus(
                    "Install reported success but FFmpeg was not found on PATH or in the " +
                    "winget install folder. Open a new terminal and run `ffmpeg -version` " +
                    "to verify; if it works there, try \"I already installed it\" again.");
            }
            else
            {
                ShowStatus(
                    "FFmpeg still not found. Check that ffmpeg.exe is on your PATH, " +
                    "or use the winget install button above.");
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(FfmpegPromptResult.Cancelled);
    }

    private void CloseWithResult(FfmpegPromptResult result)
    {
        Result = result;
        // Make sure we don't try to set a closing-already window.
        if (IsVisible)
            Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // If the user closes via the title-bar X or Esc while winget is still
        // running, kill the child process so it doesn't outlive the dialog.
        if (_wingetProcess is { HasExited: false })
        {
            try { _wingetProcess.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        base.OnClosing(e);
    }

    // ── winget install runner ────────────────────────────────────────

    private async Task<int> RunWingetInstallAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            // -e: exact match; --silent: no winget UI; agreements pre-accepted.
            Arguments =
                $"install -e --id {WingetPackageId} " +
                "--accept-source-agreements --accept-package-agreements " +
                "--silent",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _wingetProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _wingetProcess.OutputDataReceived += (_, ev) =>
        {
            if (ev.Data is null) return;
            Dispatcher.BeginInvoke(() => AppendLog(ev.Data));
        };
        _wingetProcess.ErrorDataReceived += (_, ev) =>
        {
            if (ev.Data is null) return;
            Dispatcher.BeginInvoke(() => AppendLog(ev.Data));
        };

        if (!_wingetProcess.Start())
        {
            throw new InvalidOperationException("Failed to start winget.");
        }

        _wingetProcess.BeginOutputReadLine();
        _wingetProcess.BeginErrorReadLine();

        await _wingetProcess.WaitForExitAsync();
        return _wingetProcess.ExitCode;
    }

    private static bool IsWingetAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            if (!p.WaitForExit(2000))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return false;
            }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string InterpretWingetExitCode(int code)
    {
        // winget exit codes are HRESULT-shaped. A couple are common enough to
        // map explicitly; everything else gets the generic hint.
        return code switch
        {
            // 0x8A150019 — APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE
            unchecked((int)0x8A150019) =>
                "Install failed because another process is using the package. " +
                "Close any open terminals and try again.",
            // 0x8A15002B — ACCESS DENIED — common when the installer wants elevation.
            unchecked((int)0x8A15002B) =>
                "Install was denied. Try running Whisperheim as administrator, " +
                "or install FFmpeg manually via the download page.",
            _ =>
                $"winget exited with code 0x{code:X8}. See the log above for details. " +
                "If the message mentions \"Access denied\", try running Whisperheim as administrator."
        };
    }

    // ── UI helpers ───────────────────────────────────────────────────

    private void AppendLog(string line)
    {
        // Keep the log bounded so a verbose winget install doesn't balloon RAM.
        const int maxChars = 16_000;
        var text = LogTextBox.Text;
        if (text.Length > maxChars)
        {
            text = text[(text.Length - maxChars / 2)..];
        }
        LogTextBox.Text = text + line + "\r\n";
        LogTextBox.ScrollToEnd();
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusBanner.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusBanner.Visibility = Visibility.Collapsed;
    }

    private void ShowDetectedCard(FfmpegInfo info)
    {
        DetectedText.Text = $"FFmpeg detected: {info.VersionText}";
        DetectedCard.Visibility = Visibility.Visible;
    }

    private void HideDetectedCard()
    {
        DetectedCard.Visibility = Visibility.Collapsed;
    }
}
