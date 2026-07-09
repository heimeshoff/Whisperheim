namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Persists and retrieves call transcripts from the local file system.
/// Transcripts are stored in %APPDATA%/WhisperHeim/transcripts/ with date-based naming.
/// </summary>
public interface ITranscriptStorageService
{
    /// <summary>
    /// Gets the root directory where transcripts are stored.
    /// </summary>
    string TranscriptsDirectory { get; }

    /// <summary>
    /// Saves a transcript to persistent storage.
    /// </summary>
    /// <param name="transcript">The transcript to save.</param>
    /// <param name="sessionDir">
    /// The session directory the transcript belongs to, when the caller knows it
    /// (e.g. the call pipeline, which read the session's WAVs from that directory).
    /// When null, the directory is inferred from the transcript's start timestamp —
    /// which fails for directories whose names don't begin with the timestamp
    /// (e.g. <c>recovered_*</c> crash-recovery sessions).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file path where the transcript was saved.</returns>
    Task<string> SaveAsync(CallTranscript transcript, string? sessionDir = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a transcript from a file path.
    /// </summary>
    /// <param name="filePath">Absolute path to the transcript JSON file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded transcript, or null if the file does not exist.</returns>
    Task<CallTranscript?> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing transcript file in-place (re-serializes to its current FilePath).
    /// </summary>
    /// <param name="transcript">The transcript to update (must have a non-null FilePath).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(CallTranscript transcript, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all saved transcript file paths, ordered by date descending (newest first).
    /// </summary>
    IReadOnlyList<string> ListTranscriptFiles();
}
