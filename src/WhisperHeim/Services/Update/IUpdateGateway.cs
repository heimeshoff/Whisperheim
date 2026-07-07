namespace WhisperHeim.Services.Update;

/// <summary>
/// UI-free, network-free seam over the Velopack update primitives the
/// <see cref="UpdateService"/> orchestrates (task infrastructure-v8k2m). It
/// carries the "pending update" state internally so a check, its download, and
/// the eventual apply all refer to the same release without the orchestration
/// layer ever touching a Velopack type. The real implementation is
/// <see cref="VelopackUpdateGateway"/>; tests substitute a fake.
/// </summary>
public interface IUpdateGateway
{
    /// <summary>
    /// True only when the app runs from a real Velopack install. Unpacked /
    /// dev runs (<c>dotnet run</c>, raw <c>publish.ps1</c> output) report
    /// <c>false</c>, which makes every update operation a clean no-op and
    /// avoids Velopack's <c>NotInstalledException</c>.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Queries the configured release source for a newer release. Returns the
    /// target version string (e.g. <c>"0.4.0"</c>) when an update is available,
    /// or <c>null</c> when the running build is already current. Stores the
    /// pending release internally for a subsequent
    /// <see cref="DownloadStagedUpdateAsync"/> / <see cref="ApplyStagedUpdateAndRestart"/>.
    /// </summary>
    Task<string?> CheckForUpdatesAsync(CancellationToken ct);

    /// <summary>
    /// Downloads the pending release found by the last
    /// <see cref="CheckForUpdatesAsync"/> into Velopack's package cache, off the
    /// UI thread. The app keeps running on the old version; nothing is swapped
    /// until apply. No-op when no release is pending.
    /// </summary>
    Task DownloadStagedUpdateAsync(IProgress<int>? progress, CancellationToken ct);

    /// <summary>
    /// Applies the staged release and relaunches the app immediately. The only
    /// path that restarts the running process; called solely on explicit user
    /// action. No-op when no release is staged.
    /// </summary>
    void ApplyStagedUpdateAndRestart();
}
