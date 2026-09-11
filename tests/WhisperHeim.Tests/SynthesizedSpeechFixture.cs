using System.Speech.AudioFormat;
using System.Speech.Synthesis;

namespace WhisperHeim.Tests;

/// <summary>
/// Synthesizes a short spoken utterance via the Windows TTS engine ("Microsoft Zira
/// Desktop") directly to 16 kHz mono PCM16 samples, matching the sample rate
/// <see cref="WhisperHeim.Services.Transcription.TranscriptionService"/> expects. No
/// binary WAV fixture needs to be committed to the repo.
/// </summary>
/// <remarks>
/// Shared between infrastructure-anvty's quiet-audio regression test (sherpa-onnx
/// version pin) and main-hh6zw's peak-normalization regression test — both decode
/// this same fixture at decreasing gain and assert non-empty transcription text.
/// </remarks>
public static class SynthesizedSpeechFixture
{
    public const int SampleRate = 16000;

    // Long enough (~18s spoken at the default Zira rate) to match the length of the
    // original offline reproduction that triggered the sherpa-onnx NemoNormalizePerFeature
    // bug (infrastructure-anvty diagnosis) — the bug needs enough mel frames for one to
    // land on the log floor.
    private const string Utterance =
        "The quick brown fox jumps over the lazy dog while the committee reconvenes " +
        "to discuss the quarterly infrastructure budget for the upcoming fiscal year. " +
        "Meanwhile, the engineering team gathered around the whiteboard to review the " +
        "latest deployment metrics and decide whether the new release candidate was " +
        "ready to ship to every customer in the western region by the end of the week.";

    /// <summary>
    /// Synthesizes <see cref="Utterance"/> at 16 kHz mono and scales the resulting
    /// samples by <paramref name="gain"/> (1.0 = unmodified TTS output; values below
    /// 1.0 simulate a quiet/distant microphone recording).
    /// </summary>
    public static float[] SynthesizeSpeechSamples(double gain = 1.0)
    {
        using var synthesizer = new SpeechSynthesizer();
        using var stream = new MemoryStream();

        var format = new SpeechAudioFormatInfo(
            SampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
        synthesizer.SetOutputToAudioStream(stream, format);
        synthesizer.Speak(Utterance);

        var bytes = stream.ToArray();
        var samples = new float[bytes.Length / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var sample16 = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            samples[i] = (float)(sample16 / 32768.0 * gain);
        }

        return samples;
    }
}
