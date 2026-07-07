using WhisperHeim.Services.AppInfo;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Covers the UI-free resolve / format / dev-fallback logic of the single
/// shared app-version provider (task infrastructure-p4w7n). The three XAML
/// bindings that consume <see cref="AppVersionProvider.Display"/> are verified
/// by code-reading per the repo's documented lack of WPF UI-test infra.
/// </summary>
public class AppVersionProviderTests
{
    [Fact]
    public void InstalledVersion_IsPrefixedWithV()
    {
        // A build packed from the v0.3.1 tag exposes "0.3.1" via Velopack's
        // installed-version metadata; the provider prefixes it with "v".
        var provider = new AppVersionProvider(() => "0.3.1");

        Assert.Equal("v0.3.1", provider.DisplayVersion);
    }

    [Fact]
    public void NullInstalledVersion_FallsBackToDev()
    {
        // Unpacked / dotnet run: VelopackLocator reports no installed version.
        // Must read "dev" — never a stale "v1.0"/"v1.0.0".
        var provider = new AppVersionProvider(() => null);

        Assert.Equal("dev", provider.DisplayVersion);
    }

    [Fact]
    public void BlankInstalledVersion_FallsBackToDev()
    {
        var provider = new AppVersionProvider(() => "   ");

        Assert.Equal("dev", provider.DisplayVersion);
    }

    [Fact]
    public void DisplayVersion_ReadsSourceOnce_AndCachesForTheProcess()
    {
        // The installed version cannot change within a process run, so the
        // (potentially I/O-bound) Velopack read happens at most once.
        var calls = 0;
        var provider = new AppVersionProvider(() =>
        {
            calls++;
            return "0.3.1";
        });

        _ = provider.DisplayVersion;
        _ = provider.DisplayVersion;

        Assert.Equal(1, calls);
    }
}
