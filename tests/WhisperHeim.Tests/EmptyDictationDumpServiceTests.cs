using System.IO;
using NAudio.Wave;
using WhisperHeim.Services.Diagnostics;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Verifies <see cref="EmptyDictationDumpService"/> against real temp directories
/// (its whole purpose is filesystem I/O) using an injected root directory and
/// clock, per task main-ma9j8's acceptance criteria.
/// </summary>
public class EmptyDictationDumpServiceTests : IDisposable
{
    private readonly string _testRoot;

    public EmptyDictationDumpServiceTests()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "WhisperHeimTests",
            "diag_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
            else if (File.Exists(_testRoot))
                File.Delete(_testRoot);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static float[] MakeSamples(int count, float value = 0.1f)
    {
        var samples = new float[count];
        for (int i = 0; i < count; i++) samples[i] = value;
        return samples;
    }

    [Fact]
    public void TryDump_WritesValidRiffWaveWithMatchingSampleCount()
    {
        var service = new EmptyDictationDumpService(_testRoot, () => new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc));
        var samples = MakeSamples(1600); // 0.1s at 16kHz

        var result = service.TryDump(samples, sampleRate: 16000);

        Assert.True(result.Success);
        Assert.False(result.Disabled);
        Assert.Null(result.Error);
        Assert.NotNull(result.Path);
        Assert.True(File.Exists(result.Path));

        using var reader = new WaveFileReader(result.Path);
        Assert.Equal(16000, reader.WaveFormat.SampleRate);
        Assert.Equal(1, reader.WaveFormat.Channels);
        Assert.Equal(16, reader.WaveFormat.BitsPerSample);
        Assert.Equal(WaveFormatEncoding.Pcm, reader.WaveFormat.Encoding);

        var actualSampleCount = reader.Length / reader.BlockAlign;
        Assert.Equal(samples.Length, actualSampleCount);
    }

    [Fact]
    public void TryDump_WritesUnderGivenDirectory_NeverElsewhere()
    {
        var service = new EmptyDictationDumpService(_testRoot, () => DateTime.UtcNow);

        var result = service.TryDump(MakeSamples(100));

        Assert.True(result.Success);
        Assert.StartsWith(_testRoot, result.Path!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDump_RingCap_KeepsOnlyTenNewestAfterElevenDumps()
    {
        var start = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        var tick = 0;
        var service = new EmptyDictationDumpService(_testRoot, () => start.AddSeconds(tick));

        string? firstPath = null;
        for (int i = 0; i < 11; i++)
        {
            tick = i;
            var result = service.TryDump(MakeSamples(100));
            Assert.True(result.Success);
            if (i == 0) firstPath = result.Path;
        }

        var remaining = Directory.GetFiles(_testRoot, "empty-dictation-*.wav");
        Assert.Equal(10, remaining.Length);
        Assert.False(File.Exists(firstPath), "the oldest dump should have been evicted by the ring cap");
    }

    [Fact]
    public void TryDump_DisabledViaEnvVar_SkipsWriteAndReportsDisabled()
    {
        Environment.SetEnvironmentVariable("WHISPERHEIM_DISABLE_DIAG_DUMP", "1");
        try
        {
            var service = new EmptyDictationDumpService(_testRoot, () => DateTime.UtcNow);

            var result = service.TryDump(MakeSamples(100));

            Assert.False(result.Success);
            Assert.True(result.Disabled);
            Assert.Null(result.Path);
            Assert.False(Directory.Exists(_testRoot) && Directory.GetFiles(_testRoot).Length > 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WHISPERHEIM_DISABLE_DIAG_DUMP", null);
        }
    }

    [Fact]
    public void TryDump_IoFailure_IsCaughtAndReportedRatherThanThrown()
    {
        // Occupy the target directory's path with a plain file so
        // Directory.CreateDirectory inside TryDump throws.
        Directory.CreateDirectory(Path.GetDirectoryName(_testRoot)!);
        File.WriteAllText(_testRoot, "occupied");

        var service = new EmptyDictationDumpService(_testRoot, () => DateTime.UtcNow);

        var result = service.TryDump(MakeSamples(100));

        Assert.False(result.Success);
        Assert.False(result.Disabled);
        Assert.NotNull(result.Error);
    }
}
