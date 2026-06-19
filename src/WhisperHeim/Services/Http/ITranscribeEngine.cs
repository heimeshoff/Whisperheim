using WhisperHeim.Services.FileTranscription;

namespace WhisperHeim.Services.Http;

/// <summary>
/// The slice of <c>TranscriptionQueueService</c> the transcribe HTTP handler needs.
/// Extracted as an interface (task main-h7k2p) so <see cref="TranscribeRequestHandler"/>
/// is unit-testable against a fake without the real ASR engine, and so the handler
/// stays decoupled from queue internals.
/// </summary>
public interface ITranscribeEngine
{
    /// <summary>True while a transcription is in flight (engine busy).</summary>
    bool IsBusy { get; }

    /// <summary>Number of items currently in the queue (waiting + active).</summary>
    int QueueDepth { get; }

    /// <summary>
    /// Enqueues an audio file for transcription and returns its id. The request funnels
    /// through the single shared engine; a request arriving while another is in flight
    /// queues behind it (FIFO) rather than being rejected.
    /// </summary>
    Guid EnqueueFile(string filePath);

    /// <summary>
    /// Awaits the terminal outcome of the enqueued item identified by <paramref name="id"/>.
    /// </summary>
    Task<TranscribeOutcome> WaitForOutcomeAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Terminal outcome of a queued transcription, decoupled from the queue item type.
/// </summary>
public sealed record TranscribeOutcome(
    bool Succeeded,
    FileTranscriptionResult? Result,
    string? ErrorMessage);
