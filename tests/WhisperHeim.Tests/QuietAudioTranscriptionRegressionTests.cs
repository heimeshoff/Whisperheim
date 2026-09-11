using WhisperHeim.Services.Models;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Tests;

/// <summary>
/// Regression coverage for infrastructure-anvty: sherpa-onnx &lt;= 1.13.4's
/// NemoNormalizePerFeature has a float32 catastrophic-cancellation bug (upstream PR
/// #3857, fixed in 1.13.5) that silently decodes quiet/attenuated audio to an empty
/// string instead of a partial transcript. Exercises the real
/// <see cref="TranscriptionService"/> against a synthesized utterance at decreasing
/// gain and asserts every step still produces non-empty text.
/// </summary>
/// <remarks>
/// Requires the real ~640 MB Parakeet model files on disk (not committed to the
/// repo); skips with a clear reason when they are absent, e.g. on a machine that
/// has not run the Model Manager download yet.
/// </remarks>
public class QuietAudioTranscriptionRegressionTests
{
    private static bool ModelFilesPresent =>
        File.Exists(ModelManagerService.ParakeetEncoderPath) &&
        File.Exists(ModelManagerService.ParakeetDecoderPath) &&
        File.Exists(ModelManagerService.ParakeetJoinerPath) &&
        File.Exists(ModelManagerService.ParakeetTokensPath);

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.1)]
    [InlineData(0.05)]
    [InlineData(0.02)]
    [InlineData(0.01)]
    public async Task TranscribeAsync_SynthesizedUtterance_YieldsNonEmptyText_AtGain(double gain)
    {
        if (!ModelFilesPresent)
        {
            // No SkippableFact-style dynamic skip is available on xunit 2.9.2 without
            // an extra package; log the reason and pass trivially rather than fail a
            // machine that has not downloaded the ~640 MB Parakeet model yet.
            Console.WriteLine(
                "SKIPPED: Parakeet model files not found at " +
                $"'{ModelManagerService.ParakeetEncoderPath}'. Run the Model Manager " +
                "download to exercise this regression test.");
            return;
        }

        var samples = SynthesizedSpeechFixture.SynthesizeSpeechSamples(gain);

        using var transcriptionService = new TranscriptionService();
        var result = await transcriptionService.TranscribeAsync(
            samples, SynthesizedSpeechFixture.SampleRate);

        Assert.False(
            string.IsNullOrWhiteSpace(result.Text),
            $"Expected non-empty transcription at gain={gain}, got empty text " +
            "(the sherpa-onnx NemoNormalizePerFeature regression — see infrastructure-anvty).");
    }
}
