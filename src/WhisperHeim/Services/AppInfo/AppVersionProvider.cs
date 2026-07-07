using System;
using System.Diagnostics;
using Velopack.Locators;

namespace WhisperHeim.Services.AppInfo;

/// <summary>
/// Reads the running build's version from Velopack's installed-version
/// metadata (the <c>--packVersion</c> = <c>v*</c> git tag the build was packed
/// from) and formats it for display (task infrastructure-p4w7n).
/// </summary>
/// <remarks>
/// <para>
/// Source-of-truth decision (refine, 2026-06-29): the version is read from
/// <see cref="VelopackLocator"/> — <em>not</em> from an
/// <c>UpdateManager</c> (which would need a <c>GithubSource</c> and couple a
/// pure version <em>display</em> to the updater / network), and <em>not</em>
/// from the assembly informational version. Because the release pipeline is
/// deliberately left unchanged, the assembly version stays the default
/// <c>1.0.0</c> forever, so an assembly fallback tier could only ever surface a
/// misleading <c>v1.0.0</c>. The honest logic is therefore two tiers, not three:
/// <c>installed → "v" + Velopack version; else → "dev"</c>.
/// </para>
/// <para>
/// The Velopack lookup runs at most once per process (the installed version
/// cannot change while the app runs) and is cached behind a <see cref="Lazy{T}"/>.
/// </para>
/// </remarks>
public sealed class AppVersionProvider : IAppVersionProvider
{
    private readonly Lazy<string> _displayVersion;

    /// <summary>
    /// Creates a provider over an injectable installed-version reader. The
    /// reader returns the raw installed version string (e.g. <c>"0.3.1"</c>)
    /// or <c>null</c>/blank when the app is not installed (unpacked / dev).
    /// </summary>
    public AppVersionProvider(Func<string?> readInstalledVersion)
    {
        ArgumentNullException.ThrowIfNull(readInstalledVersion);
        _displayVersion = new Lazy<string>(() => Format(readInstalledVersion()));
    }

    /// <inheritdoc />
    public string DisplayVersion => _displayVersion.Value;

    /// <summary>
    /// Formats a raw installed-version string into the displayed value:
    /// <c>"v" + version</c> when present, <c>"dev"</c> when null/blank.
    /// </summary>
    internal static string Format(string? installedVersion) =>
        string.IsNullOrWhiteSpace(installedVersion)
            ? "dev"
            : "v" + installedVersion.Trim();

    /// <summary>
    /// The process-wide provider backed by the real Velopack read. Every UI
    /// surface binds to this single instance so the displayed version can't
    /// drift between pages.
    /// </summary>
    public static IAppVersionProvider Default { get; } =
        new AppVersionProvider(ReadVelopackInstalledVersion);

    /// <summary>
    /// XAML-bindable convenience over <see cref="Default"/>'s
    /// <see cref="IAppVersionProvider.DisplayVersion"/>, consumed via
    /// <c>{x:Static}</c> by the Dictation, Settings, and About pages.
    /// </summary>
    public static string Display => Default.DisplayVersion;

    /// <summary>
    /// Reads the locally installed version through <see cref="VelopackLocator"/>
    /// with no update source and no network. Returns <c>null</c> when the app
    /// is not installed (unpacked runs), which the formatter maps to <c>"dev"</c>.
    /// </summary>
    private static string? ReadVelopackInstalledVersion()
    {
        try
        {
            var locator = VelopackLocator.CreateDefaultForPlatform(null);
            return locator?.CurrentlyInstalledVersion?.ToString();
        }
        catch (Exception ex)
        {
            // Unpacked / dev environments can throw while resolving the
            // locator. A version display must never crash the app — fall back.
            Trace.TraceInformation(
                "[AppVersionProvider] No installed Velopack version ({0}); using dev fallback.",
                ex.Message);
            return null;
        }
    }
}
