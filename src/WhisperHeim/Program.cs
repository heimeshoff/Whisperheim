using System;
using System.Diagnostics;
using System.IO;
using Velopack;

namespace WhisperHeim;

/// <summary>
/// Custom WPF entry point that hosts <see cref="VelopackApp"/> before WPF
/// initializes. Velopack requires this pattern so its install / update /
/// uninstall / first-run hooks can intercept the process *before* any WPF
/// window is constructed (the hooks themselves run with a 15&#x202F;s timeout
/// and forbid UI -- see <c>OnFirstRun</c> below).
/// </summary>
/// <remarks>
/// Because we use this custom entry point, App.xaml is registered as a
/// <c>&lt;Page&gt;</c> (not <c>ApplicationDefinition</c>) in the csproj and
/// <c>&lt;StartupObject&gt;</c> points at this class. <c>vpk pack</c> will
/// emit a warning that <c>VelopackApp.Run()</c> is not in the entry-point
/// assembly -- that is expected with custom Main and documented by Velopack.
/// </remarks>
public static class Program
{
    /// <summary>
    /// Set by <see cref="VelopackApp.OnFirstRun"/> when this process is the
    /// very first launch after a Velopack install. <see cref="App.OnStartup"/>
    /// reads it (alongside the <c>VELOPACK_FIRSTRUN</c> environment variable
    /// Velopack also sets) and decides whether the first-run model download
    /// dialog should be surfaced. The dialog itself is *not* shown from the
    /// hook -- Velopack hooks have a 15&#x202F;s timeout and explicitly forbid
    /// UI. Task 108 owns the UI; this flag is the handoff.
    /// </summary>
    public static bool IsFirstRun { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            VelopackApp.Build()
                .OnFirstRun(_ =>
                {
                    // NO UI HERE. Hook has a 15 s timeout and Velopack
                    // explicitly forbids window construction. Just flip the
                    // flag; App.OnStartup consumes it once WPF is alive.
                    IsFirstRun = true;
                })
                .OnBeforeUninstallFastCallback(_ =>
                {
                    // Drop a small "where is my data" note on the user's
                    // desktop right before Velopack wipes the install
                    // directory. Hook has a 30 s timeout and Velopack
                    // forbids UI -- this is plain file IO only.
                    //
                    // We do NOT touch user data here (recordings, settings,
                    // models all stay in %APPDATA%\WhisperHeim\ which
                    // Velopack preserves). The note exists so a user who
                    // uninstalled by accident can find their recordings.
                    TryWriteUninstallDataNote();
                })
                .Run();
        }
        catch (Exception ex)
        {
            // VelopackApp.Run() handles its own --veloapp-* CLI verbs and
            // exits the process when invoked by the installer/updater. A
            // failure here in the regular launch path is non-fatal: log it
            // and continue into WPF so the user still gets the app.
            Trace.TraceWarning("[Program] VelopackApp.Run() failed: {0}", ex);
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    /// <summary>
    /// Writes a single plain-text note to the user's desktop with the
    /// location of preserved user data. Called from the
    /// <see cref="VelopackApp.OnBeforeUninstallFastCallback"/> hook, which
    /// has a 30&#x202F;s timeout and forbids UI. We do not read the
    /// configured DataPath (that would mean spinning up
    /// <see cref="Services.Settings.DataPathService"/> here, which is
    /// heavier than a hook should be) -- we point the user at
    /// <c>%APPDATA%\WhisperHeim\</c> (the default + bootstrap location)
    /// and let them follow <c>bootstrap.json</c> from there if they
    /// configured a custom path. Best-effort: any failure is swallowed
    /// so uninstall never aborts because of this note.
    /// </summary>
    private static void TryWriteUninstallDataNote()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
                return;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var roamingRoot = Path.Combine(appData, "WhisperHeim");

            var notePath = Path.Combine(desktop, "WhisperHeim-data-location.txt");
            var contents =
                "Thanks for trying Whisperheim." + Environment.NewLine +
                Environment.NewLine +
                "Your recordings, transcripts, and settings have NOT been deleted." + Environment.NewLine +
                "They live in:" + Environment.NewLine +
                "  " + roamingRoot + Environment.NewLine +
                Environment.NewLine +
                "If you configured a custom Data Folder in Settings, your recordings" + Environment.NewLine +
                "live there instead -- the path is recorded in:" + Environment.NewLine +
                "  " + Path.Combine(roamingRoot, "bootstrap.json") + Environment.NewLine +
                Environment.NewLine +
                "AI models (~800 MB) are also kept under the folder above so a" + Environment.NewLine +
                "reinstall does not have to re-download them." + Environment.NewLine +
                Environment.NewLine +
                "Delete that folder manually for a fully clean removal." + Environment.NewLine +
                Environment.NewLine +
                "If Whisperheim served you well, a star on GitHub would make our day:" + Environment.NewLine +
                "  https://github.com/heimeshoff/WhisperHeim" + Environment.NewLine;

            File.WriteAllText(notePath, contents);
        }
        catch
        {
            // Hook failures must never block uninstall. Swallow silently.
        }
    }
}
