namespace WhisperHeim.Services.FileTranscription;

/// <summary>
/// Transcribes audio files by decoding to PCM and feeding to the underlying
/// transcription engine. OGG, MP3, M4A and WAV decode natively; any other format
/// is attempted via FFmpeg as a last resort.
/// </summary>
public interface IFileTranscriptionService
{
    /// <summary>
    /// Natively-known audio extensions (lowercase, with leading dot). This is a
    /// file-picker DISPLAY HINT only, not an exhaustive truth source — other
    /// formats may still transcribe via FFmpeg. Use <see cref="IsSupported"/> to
    /// decide acceptance.
    /// </summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /// <summary>
    /// Returns true if the file is worth attempting to transcribe — i.e. it has
    /// an extension. The decoder is the authority on what can actually be decoded.
    /// </summary>
    bool IsSupported(string filePath);

    /// <summary>
    /// Transcribes the audio file at the given path.
    /// Long files are automatically chunked at silence boundaries.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result with text and metadata.</returns>
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    /// <exception cref="InvalidOperationException">If the file is corrupt or cannot be decoded.</exception>
    Task<FileTranscriptionResult> TranscribeFileAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
