using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Recording;

namespace WhisperHeim.Services.Transcription;

/// <summary>
/// Item stages matching the call transcription pipeline.
/// </summary>
public enum QueueItemStage
{
    Queued,
    Loading,
    Diarizing,
    Transcribing,
    Assembling,
    Completed,
    Failed,
}

/// <summary>
/// Whether the queue item is a call recording or a file transcription.
/// </summary>
public enum QueueItemType
{
    Recording,
    File,
}

/// <summary>
/// A single item in the transcription queue.
/// </summary>
public sealed class TranscriptionQueueItem : INotifyPropertyChanged
{
    private QueueItemStage _stage = QueueItemStage.Queued;
    private double _stagePercent;
    private double _overallPercent;
    private string _stageDescription = string.Empty;
    private string? _errorMessage;
    private DateTimeOffset? _completedAt;
    private string _resultText = string.Empty;
    private string? _warningMessage;

    public TranscriptionQueueItem(
        string title,
        CallRecordingSession session)
    {
        Id = Guid.NewGuid();
        Title = title;
        ItemType = QueueItemType.Recording;
        Session = session;
        EnqueuedAt = DateTimeOffset.Now;
    }

    public TranscriptionQueueItem(
        string title,
        string filePath)
    {
        Id = Guid.NewGuid();
        Title = title;
        ItemType = QueueItemType.File;
        FilePath = filePath;
        EnqueuedAt = DateTimeOffset.Now;
    }

    /// <summary>
    /// Constructor for imported file transcription with a session directory.
    /// </summary>
    public TranscriptionQueueItem(
        string title,
        string filePath,
        string sessionDir)
    {
        Id = Guid.NewGuid();
        Title = title;
        ItemType = QueueItemType.File;
        FilePath = filePath;
        SessionDir = sessionDir;
        EnqueuedAt = DateTimeOffset.Now;
    }

    public Guid Id { get; }
    public string Title { get; }
    public QueueItemType ItemType { get; }
    public CallRecordingSession? Session { get; }
    public string? FilePath { get; }

    /// <summary>
    /// Session directory for imported file transcriptions.
    /// When set, a transcript.json will be saved to this directory after transcription.
    /// </summary>
    public string? SessionDir { get; }

    public DateTimeOffset EnqueuedAt { get; }

    /// <summary>
    /// Number of times this item (or its source session) has failed transcription.
    /// Used to enforce the maximum retry limit.
    /// </summary>
    public int FailureCount { get; set; }

    public QueueItemStage Stage
    {
        get => _stage;
        set { if (_stage != value) { _stage = value; OnPropertyChanged(); } }
    }

    public double StagePercent
    {
        get => _stagePercent;
        set { if (Math.Abs(_stagePercent - value) > 0.01) { _stagePercent = value; OnPropertyChanged(); } }
    }

    public double OverallPercent
    {
        get => _overallPercent;
        set { if (Math.Abs(_overallPercent - value) > 0.01) { _overallPercent = value; OnPropertyChanged(); } }
    }

    public string StageDescription
    {
        get => _stageDescription;
        set { if (_stageDescription != value) { _stageDescription = value; OnPropertyChanged(); } }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { if (_errorMessage != value) { _errorMessage = value; OnPropertyChanged(); } }
    }

    public DateTimeOffset? CompletedAt
    {
        get => _completedAt;
        set { if (_completedAt != value) { _completedAt = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Warning message about degraded results (e.g. skipped diarization chunks).
    /// Non-null when the item completed but with issues.
    /// </summary>
    public string? WarningMessage
    {
        get => _warningMessage;
        set { if (_warningMessage != value) { _warningMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWarning)); } }
    }

    /// <summary>
    /// True when the item completed but with warnings (e.g. skipped diarization chunks).
    /// </summary>
    public bool HasWarning => _warningMessage is not null;

    /// <summary>
    /// For file transcriptions: the resulting text after transcription completes.
    /// </summary>
    public string ResultText
    {
        get => _resultText;
        set { if (_resultText != value) { _resultText = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// For file transcriptions: the full engine result (text + duration/RTF/chunkCount
    /// metadata) once transcription completes. Null until the item finishes, and for
    /// recording items. Additive (task main-h7k2p) so API surfaces can return the full
    /// <see cref="FileTranscription.FileTranscriptionResult"/> shape, not just the text.
    /// Unlike <see cref="ResultText"/>, the <c>Text</c> here is the raw engine text — it
    /// is NOT replaced with the UI's "(No speech detected)" sentinel for empty audio.
    /// </summary>
    public FileTranscription.FileTranscriptionResult? Result { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// FIFO transcription queue that processes items sequentially in the background.
/// Replaces both <c>TranscriptionBusyService</c> and the modal <c>TranscriptionProgressDialog</c>.
/// Observable for UI binding.
/// </summary>
public sealed class TranscriptionQueueService : INotifyPropertyChanged
{
    /// <summary>
    /// Maximum number of transcription attempts before a session is marked as permanently failed.
    /// </summary>
    public const int MaxRetries = 3;

    private readonly ICallTranscriptionPipeline _pipeline;
    private readonly IFileTranscriptionService _fileTranscriptionService;
    private readonly ITranscriptStorageService _transcriptStorage;
    private readonly Func<string> _getLocalSpeakerName;
    private TranscriptionQueueItem? _activeItem;
    private bool _isProcessing;
    private bool _isCancelling;
    private CancellationTokenSource? _activeCts;

    // External acquire/release for non-queue callers
    private bool _externalBusy;
    private string _externalBusySource = string.Empty;

    /// <summary>
    /// All items in the queue (waiting, active, and recently completed/failed).
    /// Bound to UI. Must be modified on the dispatcher thread.
    /// </summary>
    public ObservableCollection<TranscriptionQueueItem> Items { get; } = new();

    /// <summary>The currently processing item, or null if idle.</summary>
    public TranscriptionQueueItem? ActiveItem
    {
        get => _activeItem;
        private set
        {
            if (_activeItem != value)
            {
                _activeItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>True when the engine is busy (queue processing or external acquire).</summary>
    public bool IsBusy => _activeItem is not null || _externalBusy;

    /// <summary>True when no transcription is active.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>
    /// Description of the current busy source (for backward compat with file transcription page).
    /// </summary>
    public string BusySource => _activeItem?.Title ?? _externalBusySource;

    /// <summary>Short status string for the collapsed bottom bar.</summary>
    public string StatusText
    {
        get
        {
            if (_activeItem is null)
                return "No active transcriptions";

            var pct = (int)_activeItem.OverallPercent;
            return $"Transcribing \"{_activeItem.Title}\" ({pct}%)";
        }
    }

    /// <summary>
    /// Raised after a queue item completes successfully.
    /// Carries the item so the UI can refresh transcript lists.
    /// </summary>
    public event EventHandler<TranscriptionQueueItem>? ItemCompleted;

    /// <summary>
    /// Raised after a queue item fails.
    /// </summary>
    public event EventHandler<TranscriptionQueueItem>? ItemFailed;

    /// <summary>
    /// Awaits the terminal stage (Completed or Failed) of the queue item with the
    /// given id, returning the item itself. Bridges the event-based queue to a Task
    /// for request/response API callers (task main-h7k2p) without per-handler event
    /// juggling. If the item has already reached a terminal stage when this is called,
    /// it completes immediately. Honours <paramref name="cancellationToken"/>.
    /// </summary>
    public Task<TranscriptionQueueItem> WaitForItemAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<TranscriptionQueueItem>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnCompleted(object? sender, TranscriptionQueueItem item)
        {
            if (item.Id == id) tcs.TrySetResult(item);
        }

        void OnFailed(object? sender, TranscriptionQueueItem item)
        {
            if (item.Id == id) tcs.TrySetResult(item);
        }

        ItemCompleted += OnCompleted;
        ItemFailed += OnFailed;

        CancellationTokenRegistration ctr = default;
        if (cancellationToken.CanBeCanceled)
            ctr = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        // Guard against the race where the item finished between EnqueueFile and the
        // subscription above: if it's already terminal, resolve now.
        var existing = FindItem(id);
        if (existing is not null &&
            existing.Stage is QueueItemStage.Completed or QueueItemStage.Failed)
        {
            tcs.TrySetResult(existing);
        }

        return tcs.Task.ContinueWith(t =>
        {
            ItemCompleted -= OnCompleted;
            ItemFailed -= OnFailed;
            ctr.Dispose();
            return t;
        }, CancellationToken.None,
           TaskContinuationOptions.ExecuteSynchronously,
           TaskScheduler.Default).Unwrap();
    }

    private TranscriptionQueueItem? FindItem(Guid id)
    {
        TranscriptionQueueItem? found = null;
        DispatcherInvoke(() =>
        {
            foreach (var item in Items)
            {
                if (item.Id == id) { found = item; break; }
            }
        });
        return found;
    }

    /// <summary>
    /// Backward-compatible acquire for non-queue callers (e.g. file transcription).
    /// Returns false if the engine is already busy.
    /// </summary>
    public bool TryAcquire(string source)
    {
        lock (this)
        {
            if (IsBusy)
            {
                Trace.TraceWarning(
                    "[TranscriptionQueue] TryAcquire rejected for '{0}' -- engine busy.", source);
                return false;
            }

            _externalBusy = true;
            _externalBusySource = source;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(ActiveItem));

            Trace.TraceInformation(
                "[TranscriptionQueue] Engine acquired externally by '{0}'.", source);
            return true;
        }
    }

    /// <summary>
    /// Releases an external acquire. Safe to call even if not busy.
    /// </summary>
    public void Release()
    {
        lock (this)
        {
            if (!_externalBusy) return;

            Trace.TraceInformation(
                "[TranscriptionQueue] Engine released by '{0}'.", _externalBusySource);

            _externalBusy = false;
            _externalBusySource = string.Empty;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(ActiveItem));
        }

        // Now that the external lock is released, try processing the queue
        ProcessNext();
    }

    public TranscriptionQueueService(
        ICallTranscriptionPipeline pipeline,
        IFileTranscriptionService fileTranscriptionService,
        ITranscriptStorageService transcriptStorage,
        Func<string> getLocalSpeakerName)
    {
        _pipeline = pipeline;
        _fileTranscriptionService = fileTranscriptionService;
        _transcriptStorage = transcriptStorage;
        _getLocalSpeakerName = getLocalSpeakerName;
    }

    /// <summary>
    /// Enqueues a recording session for transcription.
    /// Processing starts automatically if the engine is idle.
    /// </summary>
    public void Enqueue(string title, CallRecordingSession session)
    {
        var item = new TranscriptionQueueItem(title, session);
        DispatcherInvoke(() =>
        {
            Items.Add(item);
            OnPropertyChanged(nameof(StatusText));
        });

        Trace.TraceInformation(
            "[TranscriptionQueue] Enqueued '{0}'. Queue depth: {1}",
            title, Items.Count);

        ProcessNext();
    }

    /// <summary>
    /// Enqueues an audio file for transcription (ephemeral, no transcript.json saved).
    /// Returns the queue item so the caller can track its progress.
    /// </summary>
    public TranscriptionQueueItem EnqueueFile(string filePath)
    {
        var title = System.IO.Path.GetFileName(filePath);
        var item = new TranscriptionQueueItem(title, filePath);
        DispatcherInvoke(() =>
        {
            Items.Add(item);
            OnPropertyChanged(nameof(StatusText));
        });

        Trace.TraceInformation(
            "[TranscriptionQueue] Enqueued file '{0}'. Queue depth: {1}",
            title, Items.Count);

        ProcessNext();
        return item;
    }

    /// <summary>
    /// Enqueues an imported audio file for transcription, producing a transcript.json
    /// in the given session directory when complete.
    /// </summary>
    public TranscriptionQueueItem EnqueueFileImport(string title, string filePath, string sessionDir)
    {
        var item = new TranscriptionQueueItem(title, filePath, sessionDir);
        DispatcherInvoke(() =>
        {
            Items.Add(item);
            OnPropertyChanged(nameof(StatusText));
        });

        Trace.TraceInformation(
            "[TranscriptionQueue] Enqueued file import '{0}' -> {1}. Queue depth: {2}",
            title, sessionDir, Items.Count);

        ProcessNext();
        return item;
    }

    /// <summary>
    /// Removes a queued (not yet active) item from the queue.
    /// </summary>
    public bool Remove(TranscriptionQueueItem item)
    {
        if (item.Stage != QueueItemStage.Queued)
            return false;

        bool removed = false;
        DispatcherInvoke(() => removed = Items.Remove(item));

        if (removed)
        {
            Trace.TraceInformation("[TranscriptionQueue] Removed queued item '{0}'.", item.Title);
            OnPropertyChanged(nameof(StatusText));
        }

        return removed;
    }

    /// <summary>
    /// Cancels the currently active transcription.
    /// </summary>
    public void CancelActive()
    {
        if (_activeCts is not null)
        {
            Trace.TraceInformation("[TranscriptionQueue] Cancelling active item '{0}'.", _activeItem?.Title);
            _isCancelling = true;
            _activeCts.Cancel();

            // Immediately update UI to show cancelling state
            if (_activeItem is not null)
            {
                DispatcherInvoke(() =>
                {
                    _activeItem.StageDescription = "Cancelling...";
                    OnPropertyChanged(nameof(StatusText));
                });
            }
        }
    }

    /// <summary>
    /// Re-enqueues a failed item for retry (appended at the end).
    /// Returns false if the item has exceeded the maximum retry limit.
    /// </summary>
    public bool Retry(TranscriptionQueueItem item)
    {
        if (item.Stage != QueueItemStage.Failed)
            return false;

        var failureCount = GetSessionFailureCount(item);
        if (failureCount >= MaxRetries)
        {
            Trace.TraceWarning(
                "[TranscriptionQueue] Retry rejected for '{0}' — {1} failures reached the limit of {2}.",
                item.Title, failureCount, MaxRetries);
            return false;
        }

        DispatcherInvoke(() => Items.Remove(item));

        // Create a fresh item for the retry, carrying over the failure count
        TranscriptionQueueItem? newItem = null;
        if (item.ItemType == QueueItemType.File && item.FilePath is not null)
        {
            if (item.SessionDir is not null)
                newItem = EnqueueFileImport(item.Title, item.FilePath, item.SessionDir);
            else
                newItem = EnqueueFile(item.FilePath);
        }
        else if (item.Session is not null)
        {
            Enqueue(item.Title, item.Session);
        }

        Trace.TraceInformation(
            "[TranscriptionQueue] Retrying '{0}' (attempt {1}/{2}).",
            item.Title, failureCount + 1, MaxRetries);
        return true;
    }

    /// <summary>
    /// Removes completed or failed items from the list.
    /// </summary>
    public void ClearFinished()
    {
        DispatcherInvoke(() =>
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i].Stage is QueueItemStage.Completed or QueueItemStage.Failed)
                    Items.RemoveAt(i);
            }
        });
    }

    private async void ProcessNext()
    {
        lock (this)
        {
            if (_isProcessing)
            {
                Trace.TraceInformation("[TranscriptionQueue] ProcessNext skipped: already processing.");
                return;
            }

            // Don't start queue processing if an external caller has the lock
            if (_externalBusy)
            {
                Trace.TraceInformation("[TranscriptionQueue] ProcessNext skipped: external busy ({0}).", _externalBusySource);
                return;
            }

            _isProcessing = true;
        }

        // Find the next queued item
        TranscriptionQueueItem? next = null;
        DispatcherInvoke(() =>
        {
            foreach (var item in Items)
            {
                if (item.Stage == QueueItemStage.Queued)
                {
                    next = item;
                    break;
                }
            }
        });

        if (next is null)
        {
            _isProcessing = false;
            return;
        }

        ActiveItem = next;

        while (next is not null)
        {
            await ProcessItem(next);

            // Find next queued item
            next = null;
            DispatcherInvoke(() =>
            {
                foreach (var item in Items)
                {
                    if (item.Stage == QueueItemStage.Queued)
                    {
                        next = item;
                        break;
                    }
                }
            });

            ActiveItem = next;
        }

        _isProcessing = false;
    }

    private async Task ProcessItem(TranscriptionQueueItem item)
    {
        _activeCts = new CancellationTokenSource();
        _isCancelling = false;

        Trace.TraceInformation("[TranscriptionQueue] Processing '{0}' (type={1}).", item.Title, item.ItemType);

        try
        {
            DispatcherInvoke(() =>
            {
                item.Stage = QueueItemStage.Loading;
                item.StageDescription = "Starting...";
            });

            if (item.ItemType == QueueItemType.File)
            {
                await ProcessFileItem(item);
            }
            else
            {
                await ProcessRecordingItem(item);
            }

            Trace.TraceInformation("[TranscriptionQueue] Setting stage to Completed for '{0}'.", item.Title);
            DispatcherInvoke(() =>
            {
                item.Stage = QueueItemStage.Completed;
                item.OverallPercent = 100;
                item.StageDescription = item.WarningMessage is not null
                    ? $"Complete (with warnings)"
                    : "Complete";
                item.CompletedAt = DateTimeOffset.Now;
                Trace.TraceInformation("[TranscriptionQueue] Stage set to Completed on UI thread for '{0}'. Stage={1}", item.Title, item.Stage);
            });

            Trace.TraceInformation("[TranscriptionQueue] Completed '{0}'.", item.Title);
            ItemCompleted?.Invoke(this, item);
        }
        catch (OperationCanceledException)
        {
            DispatcherInvoke(() =>
            {
                item.Stage = QueueItemStage.Failed;
                item.ErrorMessage = "Cancelled";
                item.StageDescription = "Cancelled";
                item.CompletedAt = DateTimeOffset.Now;
            });

            Trace.TraceInformation("[TranscriptionQueue] Cancelled '{0}'.", item.Title);
            ItemFailed?.Invoke(this, item);
        }
        catch (Exception ex)
        {
            var failureCount = IncrementSessionFailureCount(item);
            var retryNote = failureCount >= MaxRetries
                ? $" (failed {failureCount}/{MaxRetries} times — no more retries)"
                : $" (attempt {failureCount}/{MaxRetries})";

            DispatcherInvoke(() =>
            {
                item.Stage = QueueItemStage.Failed;
                item.FailureCount = failureCount;
                item.ErrorMessage = ex.Message + retryNote;
                item.StageDescription = $"Failed: {ex.Message}{retryNote}";
                item.CompletedAt = DateTimeOffset.Now;
            });

            Trace.TraceError("[TranscriptionQueue] Failed '{0}'{1}: {2}",
                item.Title, retryNote, ex.Message);
            ItemFailed?.Invoke(this, item);
        }
        finally
        {
            _activeCts.Dispose();
            _activeCts = null;
        }
    }

    private async Task ProcessRecordingItem(TranscriptionQueueItem item)
    {
        var progress = new Progress<TranscriptionPipelineProgress>(p =>
        {
            DispatcherInvoke(() =>
            {
                if (item.Stage is QueueItemStage.Completed or QueueItemStage.Failed || _isCancelling)
                    return;
                item.Stage = MapStage(p.Stage);
                item.StagePercent = p.StagePercent;
                item.OverallPercent = p.OverallPercent;
                item.StageDescription = p.Description;
                if (p.WarningMessage is not null)
                    item.WarningMessage = p.WarningMessage;
                OnPropertyChanged(nameof(StatusText));
            });
        });

        var localSpeakerName = _getLocalSpeakerName();
        var remoteSpeakerNames = item.Session!.RemoteSpeakerNames;

        await Task.Run(async () =>
            await _pipeline.ProcessAsync(
                item.Session, remoteSpeakerNames, localSpeakerName,
                item.Title, progress, _activeCts!.Token));
    }

    private async Task ProcessFileItem(TranscriptionQueueItem item)
    {
        Trace.TraceInformation("[TranscriptionQueue] ProcessFileItem starting for '{0}'.", item.FilePath);

        var token = _activeCts!.Token;
        var result = await Task.Run(async () =>
        {
            // Progress callback that marshals to UI thread (non-blocking).
            // Guard: don't overwrite terminal stages (Completed/Failed) with stale progress.
            var progress = new Progress<double>(p =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    if (item.Stage is QueueItemStage.Completed or QueueItemStage.Failed || _isCancelling)
                        return;
                    item.Stage = QueueItemStage.Transcribing;
                    item.StagePercent = p * 100;
                    item.OverallPercent = p * 100;
                    item.StageDescription = $"Transcribing ({(int)(p * 100)}%)";
                    OnPropertyChanged(nameof(StatusText));
                });
            });

            return await _fileTranscriptionService.TranscribeFileAsync(
                item.FilePath!, progress, token);
        }, token);

        Trace.TraceInformation("[TranscriptionQueue] ProcessFileItem completed for '{0}': {1} chars.",
            item.FilePath, result.Text?.Length ?? 0);

        var resultText = string.IsNullOrWhiteSpace(result.Text)
            ? "(No speech detected)"
            : result.Text;

        DispatcherInvoke(() =>
        {
            item.ResultText = resultText;
        });

        // Carry the full engine result (raw text + metadata) so API surfaces can
        // return audioDurationSeconds / RTF / chunkCount. Set off the dispatcher:
        // it's a plain additive field with no UI binding.
        item.Result = result;

        // If this is an imported file with a session directory, save a transcript.json
        if (item.SessionDir is not null)
        {
            await SaveFileImportTranscript(item, result);
        }
    }

    /// <summary>
    /// Creates a CallTranscript-compatible transcript.json for an imported file.
    /// </summary>
    private async Task SaveFileImportTranscript(TranscriptionQueueItem item, FileTranscription.FileTranscriptionResult result)
    {
        var now = DateTimeOffset.Now;
        var audioFileName = System.IO.Path.GetFileName(item.FilePath!);

        // Build a single-speaker transcript
        var segments = new List<CallTranscription.TranscriptSegment>();

        if (!string.IsNullOrWhiteSpace(result.Text))
        {
            segments.Add(new CallTranscription.TranscriptSegment
            {
                Speaker = "Speaker",
                StartTime = TimeSpan.Zero,
                EndTime = result.AudioDuration,
                Text = result.Text,
                IsLocalSpeaker = false,
            });
        }

        var transcript = new CallTranscription.CallTranscript
        {
            Id = Guid.NewGuid().ToString(),
            Name = item.Title,
            RecordingStartedUtc = item.EnqueuedAt,
            RecordingEndedUtc = item.EnqueuedAt + result.AudioDuration,
            Segments = segments,
            AudioFilePath = audioFileName,
        };

        // Write transcript.json to the session directory
        var transcriptPath = System.IO.Path.Combine(item.SessionDir!, "transcript.json");
        var json = System.Text.Json.JsonSerializer.Serialize(transcript, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });
        await System.IO.File.WriteAllTextAsync(transcriptPath, json);
        transcript.FilePath = transcriptPath;

        Trace.TraceInformation(
            "[TranscriptionQueue] Saved file import transcript to {0}", transcriptPath);
    }

    private static QueueItemStage MapStage(PipelineStage stage) => stage switch
    {
        PipelineStage.LoadingAudio => QueueItemStage.Loading,
        PipelineStage.Diarizing => QueueItemStage.Diarizing,
        PipelineStage.Transcribing => QueueItemStage.Transcribing,
        PipelineStage.Assembling => QueueItemStage.Assembling,
        PipelineStage.Saving => QueueItemStage.Assembling,
        PipelineStage.Completed => QueueItemStage.Completed,
        _ => QueueItemStage.Loading,
    };

    // ── Persistent failure tracking ────────────────────────────────────

    private const string FailureFileName = "transcription_failures.txt";

    /// <summary>
    /// Gets the session directory for a queue item (for persistent failure tracking).
    /// </summary>
    private static string? GetSessionDir(TranscriptionQueueItem item)
    {
        if (item.SessionDir is not null)
            return item.SessionDir;
        if (item.Session is not null)
        {
            var dir = System.IO.Path.GetDirectoryName(item.Session.MicWavFilePath);
            return dir;
        }
        return null;
    }

    /// <summary>
    /// Reads the persistent failure count for a queue item's session directory.
    /// </summary>
    public static int GetSessionFailureCount(TranscriptionQueueItem item)
    {
        var dir = GetSessionDir(item);
        if (dir is null) return 0;
        return GetSessionFailureCount(dir);
    }

    /// <summary>
    /// Reads the persistent failure count from a session directory.
    /// </summary>
    public static int GetSessionFailureCount(string sessionDir)
    {
        var failurePath = System.IO.Path.Combine(sessionDir, FailureFileName);
        if (!System.IO.File.Exists(failurePath))
            return 0;
        try
        {
            var text = System.IO.File.ReadAllText(failurePath).Trim();
            return int.TryParse(text, out var count) ? count : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Increments and persists the failure count for a queue item's session directory.
    /// Returns the new count.
    /// </summary>
    private static int IncrementSessionFailureCount(TranscriptionQueueItem item)
    {
        var dir = GetSessionDir(item);
        if (dir is null) return 0;

        var current = GetSessionFailureCount(dir);
        var newCount = current + 1;

        try
        {
            var failurePath = System.IO.Path.Combine(dir, FailureFileName);
            System.IO.File.WriteAllText(failurePath, newCount.ToString());
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[TranscriptionQueue] Could not persist failure count: {0}", ex.Message);
        }

        return newCount;
    }

    /// <summary>
    /// Returns true if a session directory has exceeded the maximum retry limit.
    /// </summary>
    public static bool HasExceededRetryLimit(string sessionDir)
    {
        return GetSessionFailureCount(sessionDir) >= MaxRetries;
    }

    private static void DispatcherInvoke(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            else
                dispatcher.BeginInvoke(() =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
        }
        else
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
