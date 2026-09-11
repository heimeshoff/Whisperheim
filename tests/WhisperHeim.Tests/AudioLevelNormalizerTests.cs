using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Tests;

/// <summary>
/// Unit tests for the pure <see cref="AudioLevelNormalizer"/> transform
/// (main-hh6zw). See <see cref="TranscriptionService"/>'s <c>DecodeAudio</c> for
/// the single choke point where this is applied to every consumer of the shared
/// engine (ADR-0006).
/// </summary>
public class AudioLevelNormalizerTests
{
    [Fact]
    public void PeakNormalize_EmptyArray_ReturnsEmptyArray()
    {
        var result = AudioLevelNormalizer.PeakNormalize(Array.Empty<float>());

        Assert.Empty(result);
        Assert.All(result, v => Assert.False(float.IsNaN(v) || float.IsInfinity(v)));
    }

    [Fact]
    public void PeakNormalize_AllZeroArray_ReturnsUnchanged()
    {
        var samples = new float[16000]; // 1s of digital silence

        var result = AudioLevelNormalizer.PeakNormalize(samples);

        Assert.Equal(samples, result);
        Assert.All(result, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void PeakNormalize_PeakBelowSilenceFloor_ReturnsUnchanged()
    {
        // Peak of 5e-5 is below the default 1e-4 silence floor.
        var samples = new float[] { 0f, 5e-5f, -3e-5f, 0f };

        var result = AudioLevelNormalizer.PeakNormalize(samples);

        Assert.Equal(samples, result);
    }

    [Fact]
    public void PeakNormalize_PeakAboveTargetPeak_ReturnsUnchanged()
    {
        // Peak of 0.8 is already above the default 0.5 target peak.
        var samples = new float[] { 0.1f, -0.8f, 0.3f };

        var result = AudioLevelNormalizer.PeakNormalize(samples);

        Assert.Equal(samples, result);
    }

    [Fact]
    public void PeakNormalize_QuietSignal_ScalesPeakToTargetWithinTolerance()
    {
        var samples = new float[] { 0.01f, -0.02f, 0.015f, -0.008f };

        var result = AudioLevelNormalizer.PeakNormalize(samples);

        var resultPeak = result.Max(v => Math.Abs(v));
        Assert.Equal(AudioLevelNormalizer.TargetPeak, resultPeak, 1e-6);
    }

    [Fact]
    public void PeakNormalize_InputContainingFullScaleValues_ProducesFiniteOutput()
    {
        var samples = new float[] { 1.0f, -1.0f, 0f, 0.5f, -0.5f };

        var result = AudioLevelNormalizer.PeakNormalize(samples);

        Assert.All(result, v => Assert.False(float.IsNaN(v) || float.IsInfinity(v)));
    }

    [Fact]
    public void ComputeGain_QuietPeak_ReturnsExpectedGain()
    {
        var gain = AudioLevelNormalizer.ComputeGain(0.01f);

        Assert.Equal(AudioLevelNormalizer.TargetPeak / 0.01f, gain, 3);
    }

    [Fact]
    public void ComputeGain_LoudOrSilentPeak_ReturnsUnityGain()
    {
        Assert.Equal(1f, AudioLevelNormalizer.ComputeGain(0.8f));
        Assert.Equal(1f, AudioLevelNormalizer.ComputeGain(0f));
    }

    [Fact]
    public void MeasurePeak_ReturnsMaxAbsoluteSample()
    {
        var samples = new float[] { 0.1f, -0.6f, 0.3f, -0.2f };

        Assert.Equal(0.6f, AudioLevelNormalizer.MeasurePeak(samples));
    }
}
