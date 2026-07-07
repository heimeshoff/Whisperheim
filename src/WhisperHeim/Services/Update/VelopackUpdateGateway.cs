using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace WhisperHeim.Services.Update;

/// <summary>
/// The real <see cref="IUpdateGateway"/> over Velopack 0.0.1298 against the
/// public WhisperHeim GitHub Releases feed (task infrastructure-v8k2m). Wraps a
/// single <see cref="UpdateManager"/> built on a <see cref="GithubSource"/> and
/// holds the pending <see cref="UpdateInfo"/> between check, download, and apply.
/// </summary>
/// <remarks>
/// <para>
/// Source: <c>GithubSource(repoUrl, accessToken: null, prerelease: false)</c> —
/// a <c>null</c> token because the repo is public, and <c>prerelease: false</c>
/// to track the single default stable channel the release pipeline publishes.
/// Detection is automatic: <see cref="UpdateManager.CheckForUpdatesAsync"/>
/// compares the SemVer baked into the published <c>RELEASES</c>/nupkg manifest
/// (the <c>--packVersion</c> from the <c>v*</c> tag) against the installed pack
/// version — no pipeline change required.
/// </para>
/// <para>
/// API surface verified by reflection against the restored
/// <c>Velopack 0.0.1298</c> assembly (not the 1.2.0 docs): the apply/restart
/// overload takes a <see cref="VelopackAsset"/> (there is no
/// <c>UpdateInfo</c> extension overload in this build), so we pass
/// <see cref="UpdateInfo.TargetFullRelease"/> explicitly. Auto-apply-on-next-launch
/// is Velopack's default and is intentionally left enabled (no
/// <c>SetAutoApplyOnStartup</c> call), so an un-clicked staged update installs on
/// the next launch. User data under <c>%APPDATA%</c> (recordings, settings,
/// ONNX models, FFmpeg) lives outside the install dir and is untouched by the
/// install-dir swap.
/// </para>
/// </remarks>
public sealed class VelopackUpdateGateway : IUpdateGateway
{
    /// <summary>The public release feed the runtime reads (mirrors the pipeline's upload target).</summary>
    public const string RepositoryUrl = "https://github.com/heimeshoff/WhisperHeim";

    private readonly UpdateManager _manager;
    private UpdateInfo? _pending;

    public VelopackUpdateGateway()
        : this(new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false)))
    {
    }

    internal VelopackUpdateGateway(UpdateManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        _manager = manager;
    }

    /// <inheritdoc />
    public bool IsInstalled => _manager.IsInstalled;

    /// <inheritdoc />
    public async Task<string?> CheckForUpdatesAsync(CancellationToken ct)
    {
        // CheckForUpdatesAsync has no CancellationToken overload in 0.0.1298;
        // honour cancellation at the boundary instead.
        ct.ThrowIfCancellationRequested();
        _pending = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        return _pending?.TargetFullRelease?.Version?.ToString();
    }

    /// <inheritdoc />
    public Task DownloadStagedUpdateAsync(IProgress<int>? progress, CancellationToken ct)
    {
        if (_pending is null) return Task.CompletedTask;
        var report = progress is null ? (Action<int>?)null : progress.Report;
        return _manager.DownloadUpdatesAsync(_pending, report, ct);
    }

    /// <inheritdoc />
    public void ApplyStagedUpdateAndRestart()
    {
        if (_pending is null) return;
        // VelopackAsset overload (no UpdateInfo extension exists at 0.0.1298).
        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
    }
}
