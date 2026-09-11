namespace WhisperHeim.Services.Transcription;

/// <summary>
/// Pure peak-normalization transform applied to every buffer before decode
/// (main-hh6zw), as defense in depth against the sherpa-onnx
/// <c>NemoNormalizePerFeature</c> float32-cancellation bug (upstream PR #3857,
/// fixed in 1.13.5): one floor-pinned mel bin can become a +7500 outlier that
/// wrecks the INT8 encoder's dynamic quantization scale and silently decodes a
/// quiet-but-real utterance to an empty string.
/// </summary>
/// <remarks>
/// Applied once, at the single choke point every consumer of the shared engine
/// shares — <see cref="TranscriptionService"/>'s <c>DecodeAudio</c> — so it
/// protects hold-to-talk dictation, the VAD dictation pipeline, the HTTP STT API,
/// file/stream transcription, and call transcription alike (ADR-0006).
/// </remarks>
public static class AudioLevelNormalizer
{
    /// <summary>
    /// The target peak amplitude quiet audio is scaled up to before decode. 0.5 is
    /// the value an offline sweep verified: one 18 s synthesized utterance decoded
    /// at 12 decreasing gain steps produced 6/12 empty results raw on sherpa-onnx
    /// 1.13.3/1.13.4 vs 0/12 peak-normalized to 0.5 (main-hh6zw, and 0/12 on 1.13.8
    /// either way). Kept as a hardcoded constant, not a setting — the sweep's
    /// provenance is the only reason to change it.
    /// </summary>
    public const float TargetPeak = 0.5f;

    /// <summary>
    /// Below this peak amplitude, a buffer is treated as digital silence / noise
    /// floor and left untouched rather than amplified into noise. Matches the
    /// RMS &lt; 1e-4 threshold the overlay's <c>NoMic</c> detection already uses
    /// over a 10-frame window, and the exact-zero buffers NAudio delivers when the
    /// mic is hard-muted.
    /// </summary>
    public const float SilenceFloor = 1e-4f;

    /// <summary>
    /// Returns the largest absolute sample value in <paramref name="samples"/>
    /// (0 for an empty array).
    /// </summary>
    public static float MeasurePeak(float[] samples)
    {
        var peak = 0f;
        foreach (var sample in samples)
        {
            var abs = Math.Abs(sample);
            if (abs > peak)
                peak = abs;
        }

        return peak;
    }

    /// <summary>
    /// Computes the gain <see cref="PeakNormalize"/> would apply for a buffer whose
    /// peak is already known, without re-scanning the samples. Returns 1 (no-op)
    /// when <paramref name="peak"/> is below <paramref name="silenceFloor"/> (never
    /// amplify silence/noise) or at/above <paramref name="targetPeak"/> (never
    /// attenuate).
    /// </summary>
    public static float ComputeGain(
        float peak, float targetPeak = TargetPeak, float silenceFloor = SilenceFloor)
    {
        if (peak < silenceFloor || peak >= targetPeak)
            return 1f;

        return targetPeak / peak;
    }

    /// <summary>
    /// Applies <paramref name="gain"/> to every sample, or returns
    /// <paramref name="samples"/> unchanged when <paramref name="gain"/> is 1 (the
    /// common case: loud audio and silence both skip the allocation/copy).
    /// </summary>
    public static float[] ApplyGain(float[] samples, float gain)
    {
        if (gain == 1f)
            return samples;

        var result = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
            result[i] = samples[i] * gain;

        return result;
    }

    /// <summary>
    /// Scales <paramref name="samples"/> so that the peak absolute value equals
    /// <paramref name="targetPeak"/>, unless the buffer is already louder than that
    /// (never attenuates — loud audio decodes fine today) or quieter than
    /// <paramref name="silenceFloor"/> (never amplifies silence into noise). Output
    /// is finite for every input, including empty arrays, all-zero arrays, and
    /// arrays containing full-scale (&#177;1.0) samples.
    /// </summary>
    public static float[] PeakNormalize(
        float[] samples, float targetPeak = TargetPeak, float silenceFloor = SilenceFloor)
    {
        if (samples.Length == 0)
            return samples;

        var peak = MeasurePeak(samples);
        var gain = ComputeGain(peak, targetPeak, silenceFloor);
        return ApplyGain(samples, gain);
    }
}
