using System.Diagnostics;
using System.IO;
using System.Text;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.FileTranscription;

/// <summary>
/// Transcribes audio files by decoding them to 16kHz mono PCM and feeding
/// chunks to the underlying <see cref="ITranscriptionService"/>.
/// OGG, M4A, MP3, and WAV decode natively; any other format is attempted via
/// FFmpeg as a last resort. Long files are automatically chunked at silence
/// boundaries.
/// </summary>
public sealed class FileTranscriptionService : IFileTranscriptionService
{
    // Natively-known formats. NOT an exhaustive truth source any more: it is a
    // file-picker DISPLAY HINT only. Any other extension is offered to FFmpeg as a
    // last-resort transcode (main-r7n2k); the decoder is the authority on what can
    // actually be decoded.
    private static readonly HashSet<string> SupportedExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ogg", ".mp3", ".m4a", ".wav"
    };

    private readonly ITranscriptionService _transcriptionService;

    public FileTranscriptionService(ITranscriptionService transcriptionService)
    {
        ArgumentNullException.ThrowIfNull(transcriptionService);
        _transcriptionService = transcriptionService;
    }

    /// <inheritdoc />
    public IReadOnlySet<string> SupportedExtensions => SupportedExts;

    /// <inheritdoc />
    public bool IsSupported(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        // Permissive: accept any file that has an extension. The decoder is the
        // authority — natively-readable formats decode directly, anything else is
        // attempted via FFmpeg (main-r7n2k). This stops the UI silently dropping
        // e.g. .opus before decode is ever tried.
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext);
    }

    /// <inheritdoc />
    public async Task<FileTranscriptionResult> TranscribeFileAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Validate file exists
        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                $"Audio file not found: '{filePath}'", filePath);

        // No fixed-allowlist rejection here. The decoder decides what it can read:
        // native formats decode directly, anything else is attempted via FFmpeg,
        // and a missing FFmpeg surfaces as a distinct error (main-r7n2k).

        // Ensure model is loaded
        if (!_transcriptionService.IsLoaded)
            _transcriptionService.LoadModel();

        var totalStopwatch = Stopwatch.StartNew();
        progress?.Report(0.0);

        Trace.TraceInformation(
            "[FileTranscriptionService] Starting transcription of '{0}'",
            Path.GetFileName(filePath));

        // Decode audio to 16kHz mono float32 PCM
        cancellationToken.ThrowIfCancellationRequested();
        Trace.TraceInformation(
            "[FileTranscriptionService] Decoding audio file '{0}'...",
            Path.GetFileName(filePath));
        var (samples, sampleRate) = await Task.Run(
            () => AudioFileDecoder.Decode(filePath, cancellationToken), cancellationToken);
        Trace.TraceInformation(
            "[FileTranscriptionService] Decoded '{0}': {1} samples",
            Path.GetFileName(filePath), samples.Length);

        if (samples.Length == 0)
        {
            totalStopwatch.Stop();
            return new FileTranscriptionResult(
                string.Empty, TimeSpan.Zero, totalStopwatch.Elapsed, 0, 0, filePath);
        }

        var audioDuration = TimeSpan.FromSeconds((double)samples.Length / sampleRate);
        progress?.Report(0.1); // 10% for decoding

        // Chunk at silence boundaries for long files
        cancellationToken.ThrowIfCancellationRequested();
        var chunks = await Task.Run(
            () => SilenceChunker.ChunkAtSilence(samples, sampleRate), cancellationToken);

        Trace.TraceInformation(
            "[FileTranscriptionService] Audio split into {0} chunk(s), total duration: {1:F1}s",
            chunks.Count, audioDuration.TotalSeconds);

        // Transcribe each chunk
        var textBuilder = new StringBuilder();
        double progressPerChunk = chunks.Count > 0 ? 0.9 / chunks.Count : 0.9;

        for (int i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkResult = await _transcriptionService.TranscribeAsync(
                chunks[i], sampleRate, cancellationToken);

            if (!string.IsNullOrWhiteSpace(chunkResult.Text))
            {
                if (textBuilder.Length > 0)
                    textBuilder.Append(' ');
                textBuilder.Append(chunkResult.Text);
            }

            progress?.Report(0.1 + (i + 1) * progressPerChunk);

            Trace.TraceInformation(
                "[FileTranscriptionService] Chunk {0}/{1} transcribed: \"{2}\"",
                i + 1, chunks.Count,
                Truncate(chunkResult.Text, 80));
        }

        totalStopwatch.Stop();

        var fullText = textBuilder.ToString().Trim();
        var rtf = audioDuration.TotalSeconds > 0
            ? totalStopwatch.Elapsed.TotalSeconds / audioDuration.TotalSeconds
            : 0;

        Trace.TraceInformation(
            "[FileTranscriptionService] Completed '{0}': {1:F1}s audio in {2:F1}s (RTF={3:F3}), {4} chunks",
            Path.GetFileName(filePath),
            audioDuration.TotalSeconds,
            totalStopwatch.Elapsed.TotalSeconds,
            rtf,
            chunks.Count);

        progress?.Report(1.0);

        return new FileTranscriptionResult(
            fullText,
            audioDuration,
            totalStopwatch.Elapsed,
            rtf,
            chunks.Count,
            filePath);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength), "...");
}
