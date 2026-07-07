using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WhisperHeim.Services.Update;

/// <summary>
/// Notify-only in-app updater (task infrastructure-v8k2m). Drives the
/// Velopack lifecycle one stage at a time — <em>check</em> →
/// <em>silent background download</em> → <em>stage + notify</em> — and
/// deliberately stops short of applying: a staged update is surfaced in the
/// status footer and only ever applied when the user explicitly clicks
/// "Restart &amp; update now" (<see cref="RestartToUpdate"/>) or, passively, by
/// Velopack's default auto-apply-on-next-launch (which we never disable).
/// </summary>
/// <remarks>
/// <para>
/// All Velopack/network contact is behind <see cref="IUpdateGateway"/>, so this
/// class is UI-free and unit-testable. Every check is guarded on
/// <see cref="IUpdateGateway.IsInstalled"/> so dev/unpacked runs are a clean
/// no-op (no <c>NotInstalledException</c>, no bogus footer indicator), and every
/// check/download is wrapped so transient failures (offline, GitHub down or
/// rate-limited, partial download) are logged and swallowed — they never crash
/// the app or nag the user; the next poll simply retries.
/// </para>
/// <para>
/// Polling is gentle: an initial check shortly after startup plus a multi-hour
/// timer (<see cref="DefaultPollInterval"/>), because unauthenticated GitHub
/// REST is capped at 60 requests/hour/IP.
/// </para>
/// </remarks>
public sealed class UpdateService : IDisposable
{
    /// <summary>
    /// Multi-hour poll cadence. Unauthenticated GitHub is 60 req/hr/IP; a check
    /// is a handful of calls, so every 6 h is comfortably within budget while
    /// still noticing a same-day release.
    /// </summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromHours(6);

    /// <summary>Short grace delay before the first check so it never competes with first-frame boot.</summary>
    private static readonly TimeSpan InitialCheckDelay = TimeSpan.FromSeconds(30);

    private readonly IUpdateGateway _gateway;

    // Serialises overlapping checks (startup check vs. a timer tick) so a single
    // pass downloads/stages at a time.
    private readonly SemaphoreSlim _checkGate = new(1, 1);

    private Timer? _pollTimer;
    private CancellationTokenSource? _cts;
    private string? _stagedVersion;

    public UpdateService(IUpdateGateway gateway)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        _gateway = gateway;
    }

    /// <summary>
    /// The version of the downloaded-and-staged update waiting to be applied
    /// (e.g. <c>"0.4.0"</c>), or <c>null</c> when nothing is staged. The footer
    /// renders this as "Update ready: vX.Y".
    /// </summary>
    public string? StagedVersion => _stagedVersion;

    /// <summary>Raised once, on the staging of a newer release, with its version.</summary>
    public event EventHandler<UpdateStagedEventArgs>? UpdateStaged;

    /// <summary>
    /// Begins update polling: an initial check after a short grace delay, then a
    /// recurring check every <see cref="DefaultPollInterval"/>. All checks run
    /// off the caller's thread (timer thread-pool callbacks). Idempotent-ish:
    /// call once at startup. No-op-safe when the app is not installed (the check
    /// itself guards on <see cref="IUpdateGateway.IsInstalled"/>).
    /// </summary>
    public void Start(TimeSpan? pollInterval = null)
    {
        if (_pollTimer is not null) return; // already started
        _cts = new CancellationTokenSource();
        var interval = pollInterval ?? DefaultPollInterval;
        _pollTimer = new Timer(
            _ => _ = CheckAndStageAsync(_cts.Token),
            state: null,
            dueTime: InitialCheckDelay,
            period: interval);
    }

    /// <summary>
    /// One check → silent-download → stage → notify pass. Safe to call directly
    /// (the timer does). Never throws: a no-op when not installed, when already
    /// current, or when the staged version is unchanged; transient failures are
    /// logged and swallowed.
    /// </summary>
    public async Task CheckAndStageAsync(CancellationToken ct = default)
    {
        if (!_gateway.IsInstalled) return; // dev/unpacked guard — clean no-op

        if (!await _checkGate.WaitAsync(0, ct).ConfigureAwait(false))
            return; // a check is already in flight; let it finish

        try
        {
            var available = await _gateway.CheckForUpdatesAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(available))
                return; // already current

            if (string.Equals(available, _stagedVersion, StringComparison.Ordinal))
                return; // this version is already staged and waiting — idempotent

            // Silent background download. The app keeps running on the old
            // version; nothing is swapped. Notify-only: we do NOT apply here.
            await _gateway.DownloadStagedUpdateAsync(progress: null, ct).ConfigureAwait(false);

            _stagedVersion = available;
            UpdateStaged?.Invoke(this, new UpdateStagedEventArgs(available));
            Trace.TraceInformation("[UpdateService] Update staged and ready: v{0}", available);
        }
        catch (OperationCanceledException)
        {
            // Shutdown / cancellation — nothing to surface.
        }
        catch (Exception ex)
        {
            // Offline, GitHub unreachable/rate-limited, partial download, etc.
            // Swallow and let the next poll retry; never crash or nag.
            Trace.TraceWarning("[UpdateService] Update check/download failed (will retry): {0}", ex.Message);
        }
        finally
        {
            _checkGate.Release();
        }
    }

    /// <summary>
    /// Explicit "Restart &amp; update now": applies the staged update and
    /// relaunches. The only path that restarts the running process. No-op when
    /// nothing is staged.
    /// </summary>
    public void RestartToUpdate()
    {
        if (_stagedVersion is null) return;
        Trace.TraceInformation("[UpdateService] User requested restart to apply v{0}.", _stagedVersion);
        _gateway.ApplyStagedUpdateAndRestart();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _pollTimer?.Dispose();
        _pollTimer = null;
        _cts?.Dispose();
        _checkGate.Dispose();
    }
}
