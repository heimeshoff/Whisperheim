using System;
using System.Threading;
using System.Threading.Tasks;
using WhisperHeim.Services.Update;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Covers the UI-free notify-only update orchestration (task infrastructure-v8k2m):
/// the dev-machine guard, the check → silent-download → stage → notify pipeline,
/// the never-auto-restart invariant, the explicit restart path, transient-failure
/// swallowing, and idempotent re-checks. The footer wiring in MainWindow is
/// verified by code-reading per the repo's documented lack of WPF UI-test infra.
/// </summary>
public class UpdateServiceTests
{
    /// <summary>
    /// Programmable <see cref="IUpdateGateway"/> double that records calls so
    /// the orchestration can be asserted without Velopack or a network.
    /// </summary>
    private sealed class FakeGateway : IUpdateGateway
    {
        public bool IsInstalled { get; set; } = true;
        public string? AvailableVersion { get; set; }
        public Exception? CheckThrows { get; set; }
        public Exception? DownloadThrows { get; set; }

        public int CheckCalls { get; private set; }
        public int DownloadCalls { get; private set; }
        public int ApplyCalls { get; private set; }

        public Task<string?> CheckForUpdatesAsync(CancellationToken ct)
        {
            CheckCalls++;
            if (CheckThrows is not null) throw CheckThrows;
            return Task.FromResult(AvailableVersion);
        }

        public Task DownloadStagedUpdateAsync(IProgress<int>? progress, CancellationToken ct)
        {
            DownloadCalls++;
            if (DownloadThrows is not null) throw DownloadThrows;
            return Task.CompletedTask;
        }

        public void ApplyStagedUpdateAndRestart() => ApplyCalls++;
    }

    [Fact]
    public async Task NotInstalled_IsACleanNoOp()
    {
        var gateway = new FakeGateway { IsInstalled = false, AvailableVersion = "0.4.0" };
        var service = new UpdateService(gateway);
        var raised = false;
        service.UpdateStaged += (_, _) => raised = true;

        await service.CheckAndStageAsync();

        Assert.Equal(0, gateway.CheckCalls);
        Assert.Equal(0, gateway.DownloadCalls);
        Assert.Null(service.StagedVersion);
        Assert.False(raised);
    }

    [Fact]
    public async Task UpdateAvailable_DownloadsSilently_StagesAndNotifies()
    {
        var gateway = new FakeGateway { AvailableVersion = "0.4.0" };
        var service = new UpdateService(gateway);
        string? notified = null;
        service.UpdateStaged += (_, e) => notified = e.Version;

        await service.CheckAndStageAsync();

        Assert.Equal(1, gateway.DownloadCalls);
        Assert.Equal("0.4.0", service.StagedVersion);
        Assert.Equal("0.4.0", notified);
    }

    [Fact]
    public async Task NoNewerRelease_StagesNothing()
    {
        var gateway = new FakeGateway { AvailableVersion = null };
        var service = new UpdateService(gateway);
        var raised = false;
        service.UpdateStaged += (_, _) => raised = true;

        await service.CheckAndStageAsync();

        Assert.Equal(0, gateway.DownloadCalls);
        Assert.Null(service.StagedVersion);
        Assert.False(raised);
    }

    [Fact]
    public async Task DetectingAnUpdate_NeverRestarts()
    {
        // Notify-only: finding/downloading an update must never apply or restart.
        var gateway = new FakeGateway { AvailableVersion = "0.4.0" };
        var service = new UpdateService(gateway);

        await service.CheckAndStageAsync();

        Assert.Equal(0, gateway.ApplyCalls);
    }

    [Fact]
    public async Task CheckFailure_IsSwallowed_AndNeverCrashes()
    {
        var gateway = new FakeGateway { CheckThrows = new InvalidOperationException("github down") };
        var service = new UpdateService(gateway);

        await service.CheckAndStageAsync(); // must not throw

        Assert.Null(service.StagedVersion);
    }

    [Fact]
    public async Task DownloadFailure_IsSwallowed_AndLeavesNothingStaged()
    {
        var gateway = new FakeGateway
        {
            AvailableVersion = "0.4.0",
            DownloadThrows = new TimeoutException("partial download"),
        };
        var service = new UpdateService(gateway);
        var raised = false;
        service.UpdateStaged += (_, _) => raised = true;

        await service.CheckAndStageAsync(); // must not throw

        Assert.Null(service.StagedVersion);
        Assert.False(raised);
    }

    [Fact]
    public void RestartToUpdate_WhenStaged_AppliesAndRestarts()
    {
        var gateway = new FakeGateway { AvailableVersion = "0.4.0" };
        var service = new UpdateService(gateway);
        service.CheckAndStageAsync().GetAwaiter().GetResult();

        service.RestartToUpdate();

        Assert.Equal(1, gateway.ApplyCalls);
    }

    [Fact]
    public void RestartToUpdate_WhenNothingStaged_IsANoOp()
    {
        var gateway = new FakeGateway();
        var service = new UpdateService(gateway);

        service.RestartToUpdate();

        Assert.Equal(0, gateway.ApplyCalls);
    }

    [Fact]
    public async Task RepeatedCheck_ForTheSameStagedVersion_DoesNotRedownloadOrRenotify()
    {
        var gateway = new FakeGateway { AvailableVersion = "0.4.0" };
        var service = new UpdateService(gateway);
        var notifications = 0;
        service.UpdateStaged += (_, _) => notifications++;

        await service.CheckAndStageAsync();
        await service.CheckAndStageAsync();

        Assert.Equal(1, gateway.DownloadCalls);
        Assert.Equal(1, notifications);
    }
}
