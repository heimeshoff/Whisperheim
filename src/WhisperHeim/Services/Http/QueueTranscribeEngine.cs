using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.Http;

/// <summary>
/// Adapts <see cref="TranscriptionQueueService"/> onto <see cref="ITranscribeEngine"/>
/// for the STT API (task main-h7k2p). Keeps the queue service free of HTTP concerns:
/// it funnels every request through <see cref="TranscriptionQueueService.EnqueueFile"/>
/// (FIFO, queues-and-blocks — never rejects when busy, per ADR-0001) and bridges
/// completion via <see cref="TranscriptionQueueService.WaitForItemAsync"/>.
/// </summary>
public sealed class QueueTranscribeEngine : ITranscribeEngine
{
    private readonly TranscriptionQueueService _queue;

    public QueueTranscribeEngine(TranscriptionQueueService queue) => _queue = queue;

    public bool IsBusy => _queue.IsBusy;

    public int QueueDepth => _queue.Items.Count;

    public Guid EnqueueFile(string filePath) => _queue.EnqueueFile(filePath).Id;

    public async Task<TranscribeOutcome> WaitForOutcomeAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _queue.WaitForItemAsync(id, cancellationToken).ConfigureAwait(false);

        if (item.Stage == QueueItemStage.Completed)
        {
            // item.Result carries the raw engine text + metadata (additive field set in
            // ProcessFileItem). It should be non-null for a completed file item, but be
            // defensive: a missing result is still a "success with empty text".
            return new TranscribeOutcome(true, item.Result, null);
        }

        // Failed (or cancelled): surface the queue's error message.
        return new TranscribeOutcome(false, null, item.ErrorMessage ?? "transcription failed");
    }
}
