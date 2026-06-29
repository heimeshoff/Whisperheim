namespace WhisperHeim.Services.AppInfo;

/// <summary>
/// Single source of truth for the version string shown in the UI
/// (task infrastructure-p4w7n). Surfaces an already-formatted, ready-to-bind
/// value so every surface (Dictation, Settings, About — and any future page)
/// renders an identical, correctly-prefixed string and none re-derives it.
/// </summary>
public interface IAppVersionProvider
{
    /// <summary>
    /// The version to display, e.g. <c>"v0.3.1"</c> on an installed
    /// (Velopack-packed) build, or <c>"dev"</c> when run unpacked.
    /// </summary>
    string DisplayVersion { get; }
}
