using System.Diagnostics;
using System.IO;
using SherpaOnnx;
using WhisperHeim.Services.Models;

namespace WhisperHeim.Services.Transcription;

/// <summary>
/// Transcribes audio segments using Parakeet TDT 0.6B via sherpa-onnx's OfflineRecognizer.
/// All transcription work runs on a background thread to avoid blocking the audio pipeline.
/// </summary>
public sealed class TranscriptionService : ITranscriptionService
{
    private OfflineRecognizer? _recognizer;
    private readonly object _lock = new();
    private bool _disposed;

    /// <inheritdoc />
    public bool IsLoaded => _recognizer is not null;

    /// <inheritdoc />
    public void LoadModel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_recognizer is not null)
            return;

        var encoderPath = ModelManagerService.ParakeetEncoderPath;
        var decoderPath = ModelManagerService.ParakeetDecoderPath;
        var joinerPath = ModelManagerService.ParakeetJoinerPath;
        var tokensPath = ModelManagerService.ParakeetTokensPath;

        ValidateModelFile(encoderPath, "encoder");
        ValidateModelFile(decoderPath, "decoder");
        ValidateModelFile(joinerPath, "joiner");
        ValidateModelFile(tokensPath, "tokens");

        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;

        config.ModelConfig.Transducer.Encoder = encoderPath;
        config.ModelConfig.Transducer.Decoder = decoderPath;
        config.ModelConfig.Transducer.Joiner = joinerPath;
        config.ModelConfig.Tokens = tokensPath;
        config.ModelConfig.NumThreads = Math.Min(Environment.ProcessorCount, 2);
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.Debug = 0;

        config.DecodingMethod = "greedy_search";

        try
        {
            _recognizer = new OfflineRecognizer(config);
            Trace.TraceInformation(
                "[TranscriptionService] Parakeet TDT 0.6B model loaded successfully " +
                "(threads={0}, provider={1})",
                config.ModelConfig.NumThreads,
                config.ModelConfig.Provider);
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[TranscriptionService] Failed to load Parakeet model: {0}", ex.Message);
            throw new InvalidOperationException(
                $"Failed to initialize the speech recognition model: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        float[] samples,
        int sampleRate = 16000,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_recognizer is null)
            throw new InvalidOperationException(
                "Model is not loaded. Call LoadModel() before transcribing.");

        if (samples.Length == 0)
            return new TranscriptionResult(string.Empty, TimeSpan.Zero, TimeSpan.Zero, 0);

        var audioDuration = TimeSpan.FromSeconds((double)samples.Length / sampleRate);

        // Run the actual transcription on a background thread so we don't block the caller.
        var result = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DecodeAudio(samples, sampleRate);
        }, cancellationToken);

        var transcriptionDuration = result.Elapsed;
        var realTimeFactor = audioDuration.TotalSeconds > 0
            ? transcriptionDuration.TotalSeconds / audioDuration.TotalSeconds
            : 0;

        Trace.TraceInformation(
            "[TranscriptionService] Transcribed {0:F2}s audio in {1:F0}ms (RTF={2:F3}): \"{3}\"",
            audioDuration.TotalSeconds,
            transcriptionDuration.TotalMilliseconds,
            realTimeFactor,
            result.Text);

        return new TranscriptionResult(
            result.Text,
            audioDuration,
            transcriptionDuration,
            realTimeFactor);
    }

    /// <summary>
    /// Performs the actual decode using sherpa-onnx OfflineRecognizer.
    /// This method is thread-safe via locking.
    /// </summary>
    private (string Text, TimeSpan Elapsed) DecodeAudio(float[] samples, int sampleRate)
    {
        var sw = Stopwatch.StartNew();

        lock (_lock)
        {
            using var stream = _recognizer!.CreateStream();
            stream.AcceptWaveform(sampleRate, samples);
            _recognizer.Decode(stream);

            var result = stream.Result;
            sw.Stop();

            var text = (result.Text ?? string.Empty).Trim();
            return (text, sw.Elapsed);
        }
    }

    private static void ValidateModelFile(string path, string name)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Model file not found: {name} at '{path}'. " +
                "Ensure models have been downloaded via the Model Manager.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _recognizer?.Dispose();
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[TranscriptionService] Error disposing recognizer: {0}", ex.Message);
        }

        _recognizer = null;
    }
}
