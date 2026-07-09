using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using WhisperHeim.Models;
using WhisperHeim.Services.Analysis;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.CallTranscription;
using WhisperHeim.Services.Export;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Views.Pages;

/// <summary>
/// Page for listing, viewing, searching, and exporting call transcripts.
/// Supports audio playback from transcript segments with visual tracking.
/// </summary>
public partial class TranscriptsPage : UserControl
{
    private readonly ITranscriptStorageService _storageService;
    private readonly TranscriptionQueueService _queueService;
    private readonly ICallRecordingService _recordingService;
    private readonly IFileTranscriptionService _fileTranscriptionService;
    private readonly OllamaService _ollamaService;
    private readonly List<TranscriptListItem> _allItems = new();
    private readonly TranscriptAudioPlayer _audioPlayer = new();
    private readonly DispatcherTimer _copiedIndicatorTimer;
    private readonly DispatcherTimer _recordingDurationTimer;
    private readonly DispatcherTimer _recordingDotPulseTimer;
    private CallTranscript? _selectedTranscript;
    private TranscriptListItem? _selectedListItem;
    private List<SegmentViewModel>? _currentSegmentViewModels;
    // Session dir whose transcript the open drawer is waiting for, so
    // TryAutoOpenTranscriptInDrawer can transition it live when it appears.
    // Display state ("Transcribing" section) is derived from the queue, not this.
    private string? _drawerAutoOpenSessionDir;
    private readonly List<TranscriptGroupViewModel> _groups = new();
    private string? _externalAudioPath; // Set when audio format isn't playable inline

    // Column sorting state
    private string _sortColumn = "Time";
    private bool _sortAscending = false; // Default: Time descending (newest first)

    // Active recording state
    private bool _isActiveRecordingDrawerOpen;
    private List<SpeakerNameItem> _activeRecordingSpeakerNames = new();
    private string _activeRecordingTitle = "";

    // Pending drawer state
    private bool _isPendingDrawerOpen;
    private PendingRecordingItem? _pendingDrawerItem;
    private List<SpeakerNameItem> _pendingDrawerSpeakerNames = new();
    private string _pendingDrawerTitle = "";
    private bool _isSeekBarDragging;

    // Analysis state
    private CancellationTokenSource? _analysisCts;
    private bool _isAnalysisVisible;

    public TranscriptsPage(
        ITranscriptStorageService storageService,
        TranscriptionQueueService queueService,
        ICallRecordingService recordingService,
        IFileTranscriptionService fileTranscriptionService,
        OllamaService ollamaService)
    {
        _storageService = storageService;
        _queueService = queueService;
        _recordingService = recordingService;
        _fileTranscriptionService = fileTranscriptionService;
        _ollamaService = ollamaService;
        InitializeComponent();

        _audioPlayer.PositionChanged += OnAudioPositionChanged;
        _audioPlayer.PlaybackStopped += OnAudioPlaybackStopped;

        _copiedIndicatorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _copiedIndicatorTimer.Tick += (_, _) =>
        {
            CopiedIndicator.Visibility = Visibility.Collapsed;
            _copiedIndicatorTimer.Stop();
        };

        // Timer to update the active recording duration display
        _recordingDurationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _recordingDurationTimer.Tick += OnRecordingDurationTick;

        // Timer to pulse the recording dot
        _recordingDotPulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _recordingDotPulseTimer.Tick += OnRecordingDotPulseTick;

        // Subscribe to recording events
        _recordingService.RecordingStarted += OnRecordingStarted;
        _recordingService.RecordingStopped += OnRecordingStopped;

        Loaded += (_, _) =>
        {
            // Refresh the list each time the page becomes visible so stale
            // entries (e.g. deleted while on another tab) are removed.
            LoadTranscriptList();

            // Show active recording card if a recording is in progress
            if (_recordingService.IsRecording)
                ShowActiveRecordingCard();

            // Sync Start/Stop button state
            UpdateRecordingButtonState();
        };

        Unloaded += (_, _) =>
        {
            _audioPlayer.Dispose();
        };

        // Refresh pending items when the queue state changes so the
        // "Engine busy" / "click to transcribe" labels update in real time.
        _queueService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TranscriptionQueueService.ActiveItem))
            {
                Dispatcher.BeginInvoke(LoadPendingSessions);
            }
        };

        // Also refresh when items are added/removed from the queue
        _queueService.Items.CollectionChanged += (_, _) =>
        {
            Dispatcher.BeginInvoke(LoadPendingSessions);
        };

        // Clean up the advisory `transcribing.lock` after takeover items finish.
        _queueService.ItemCompleted += OnQueueItemCompleted;
    }

    /// <summary>
    /// Drops the advisory <c>transcribing.lock</c> when a queue item finishes
    /// (success or failure). Best-effort: failures are logged inside
    /// <see cref="Services.Recording.TranscribingLock.Remove"/>.
    /// </summary>
    private void OnQueueItemCompleted(object? sender, TranscriptionQueueItem item)
    {
        string? sessionDir = item.SessionDir;
        if (sessionDir is null && item.Session is not null)
        {
            sessionDir = Path.GetDirectoryName(item.Session.MicWavFilePath);
        }

        if (sessionDir is not null)
        {
            Services.Recording.TranscribingLock.Remove(sessionDir);
        }
    }

    /// <summary>
    /// Reloads the transcript list from storage.
    /// </summary>
    public void RefreshList() => Dispatcher.Invoke(() =>
    {
        LoadTranscriptList();
        TryAutoOpenTranscriptInDrawer();
    });

    /// <summary>
    /// Shows the "Transcribing..." banner at the top of the page.
    /// </summary>
    public void ShowTranscribingIndicator() =>
        Dispatcher.Invoke(() => TranscribingBanner.Visibility = Visibility.Visible);

    /// <summary>
    /// Hides the "Transcribing..." banner.
    /// </summary>
    public void HideTranscribingIndicator() =>
        Dispatcher.Invoke(() => TranscribingBanner.Visibility = Visibility.Collapsed);

    /// <summary>
    /// Raised when the user clicks a pending recording card to request transcription.
    /// </summary>
    public event EventHandler<CallRecordingSession>? TranscriptionRequested;

    /// <summary>
    /// Raised when the user clicks the re-transcribe button on an existing transcript.
    /// The event carries a session reconstructed from the transcript's audio files.
    /// </summary>
    public event EventHandler<CallRecordingSession>? ReTranscriptionRequested;

    // --- Active Recording ---

    private void OnRecordingStarted(object? sender, CallRecordingSession session)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _activeRecordingTitle = "";
            _activeRecordingSpeakerNames.Clear();
            ShowActiveRecordingCard();
            UpdateRecordingButtonState();
        });
    }

    private void OnRecordingStopped(object? sender, CallRecordingStoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Save the title from the drawer if it's open
            if (_isActiveRecordingDrawerOpen)
            {
                _activeRecordingTitle = TranscriptNameBox.Text?.Trim() ?? "";
            }

            HideActiveRecordingCard();
            UpdateRecordingButtonState();

            if (e.Exception is not null)
            {
                Trace.TraceWarning("[TranscriptsPage] Recording stopped with error: {0}",
                    e.Exception.Message);

                if (_isActiveRecordingDrawerOpen)
                {
                    _isActiveRecordingDrawerOpen = false;
                    CloseDrawer();
                }
                return;
            }

            // Push the drawer's title / speaker edits onto the session BEFORE
            // AutoTranscriptionService (subscribed at Background priority) runs.
            // AutoTranscriptionService reads session.Title and session.RemoteSpeakerNames
            // when it enqueues — the actual Enqueue call lives there now, not here.
            var session = e.Session;

            session.RemoteSpeakerNames = _activeRecordingSpeakerNames
                .Select(i => i.Name ?? "")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            if (!string.IsNullOrWhiteSpace(_activeRecordingTitle))
                session.Title = _activeRecordingTitle;

            // Transition the drawer from recording state to "waiting for transcription" state
            if (_isActiveRecordingDrawerOpen)
            {
                _isActiveRecordingDrawerOpen = false;

                // Hide recording-specific elements
                RecordingIndicatorPanel.Visibility = Visibility.Collapsed;
                DrawerRecordingDuration.Visibility = Visibility.Collapsed;
                RecordingInfoText.Visibility = Visibility.Collapsed;

                // Update header to show completed state
                TranscriptInfo.Visibility = Visibility.Visible;
                TranscriptInfo.Text = "Transcription queued — the drawer will update when complete.";

                // Show the transcript scroll area (empty for now)
                TranscriptScrollViewer.Visibility = Visibility.Visible;

                // Remember the session dir so we can auto-open the transcript when done
                _drawerAutoOpenSessionDir = Path.GetDirectoryName(session.MicWavFilePath);
            }

            // Refresh to show it moved from pending state. AutoTranscriptionService
            // will enqueue at Background priority right after this completes.
            LoadTranscriptList();
        });
    }

    private void ShowActiveRecordingCard()
    {
        ActiveRecordingCard.Visibility = Visibility.Visible;
        UpdateActiveRecordingDuration();
        _recordingDurationTimer.Start();
        _recordingDotPulseTimer.Start();

        // Start the pulsing animation on the recording dot
        var pulseAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.3,
            Duration = TimeSpan.FromMilliseconds(800),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        RecordingDot.BeginAnimation(UIElement.OpacityProperty, pulseAnim);
    }

    private void HideActiveRecordingCard()
    {
        ActiveRecordingCard.Visibility = Visibility.Collapsed;
        _recordingDurationTimer.Stop();
        _recordingDotPulseTimer.Stop();
        RecordingDot.BeginAnimation(UIElement.OpacityProperty, null);

        // Do NOT close the drawer here — the recording-stopped handler
        // will transition the drawer to transcribed state if it's open.
    }

    private void UpdateActiveRecordingDuration()
    {
        if (_recordingService.CurrentSession is not null)
        {
            var duration = _recordingService.CurrentSession.Duration;
            var formatted = CallRecordingService.FormatDuration(duration);
            ActiveRecordingDuration.Text = formatted;

            if (_isActiveRecordingDrawerOpen)
                DrawerRecordingDuration.Text = formatted;
        }
    }

    private void OnRecordingDurationTick(object? sender, EventArgs e)
    {
        UpdateActiveRecordingDuration();
    }

    private void OnRecordingDotPulseTick(object? sender, EventArgs e)
    {
        // The pulse animation is handled by DoubleAnimation on RecordingDot.Opacity
    }

    private void ActiveRecordingCard_Click(object sender, MouseButtonEventArgs e)
    {
        OpenActiveRecordingDrawer();
        e.Handled = true;
    }

    private void OpenActiveRecordingDrawer()
    {
        _isActiveRecordingDrawerOpen = true;
        _isPendingDrawerOpen = false;
        _pendingDrawerItem = null;

        // Use the unified drawer in recording mode
        TranscriptDrawerContent.Visibility = Visibility.Visible;
        QueueTranscriptionPanel.Visibility = Visibility.Collapsed;

        // Show recording-specific elements
        RecordingIndicatorPanel.Visibility = Visibility.Visible;
        DrawerRecordingDuration.Visibility = Visibility.Visible;
        RecordingInfoText.Visibility = Visibility.Visible;

        // Hide transcript-specific elements
        TranscriptInfo.Visibility = Visibility.Collapsed;
        PlaybackPanel.Visibility = Visibility.Collapsed;
        TranscriptScrollViewer.Visibility = Visibility.Collapsed;
        SegmentList.ItemsSource = null;
        PlaceholderText.Visibility = Visibility.Collapsed;
        ActionPanel.Visibility = Visibility.Collapsed;
        AnalysisPanel.Visibility = Visibility.Collapsed;
        ReTranscribeButton.Visibility = Visibility.Collapsed;

        // Populate title
        TranscriptNameBox.IsReadOnly = false;
        TranscriptNameBox.Text = _activeRecordingTitle;

        // Populate speaker names using the shared panel
        _speakerNameItems = _activeRecordingSpeakerNames;
        SpeakerNamesList.ItemsSource = _speakerNameItems;
        SpeakerNamesPanel.Visibility = Visibility.Visible;

        UpdateActiveRecordingDuration();

        DrawerPanel.Visibility = Visibility.Visible;
        AnimateDrawer(open: true);
    }

    private void OpenPendingTranscribingDrawer(PendingRecordingItem item)
    {
        _isActiveRecordingDrawerOpen = false;
        _isPendingDrawerOpen = true;
        _pendingDrawerItem = item;
        _selectedTranscript = null;

        // Use the unified drawer
        TranscriptDrawerContent.Visibility = Visibility.Visible;

        // Hide recording-specific elements
        RecordingIndicatorPanel.Visibility = Visibility.Collapsed;
        DrawerRecordingDuration.Visibility = Visibility.Collapsed;
        RecordingInfoText.Visibility = Visibility.Collapsed;

        // Hide transcript-specific elements (no transcript yet)
        TranscriptScrollViewer.Visibility = Visibility.Collapsed;
        SegmentList.ItemsSource = null;
        PlaceholderText.Visibility = Visibility.Collapsed;
        ActionPanel.Visibility = Visibility.Collapsed;
        AnalysisPanel.Visibility = Visibility.Collapsed;
        ReTranscribeButton.Visibility = Visibility.Collapsed;

        // Show editable title
        TranscriptNameBox.IsReadOnly = false;
        _pendingDrawerTitle = item.Name;
        TranscriptNameBox.Text = _pendingDrawerTitle;

        // Show status info
        TranscriptInfo.Visibility = Visibility.Visible;
        var failures = TranscriptionQueueService.GetSessionFailureCount(item.SessionDir);
        TranscriptInfo.Text = item.IsQueued
            ? "Queued for transcription \u2014 waiting to start."
            : item.IsTranscribing
                ? "Transcription in progress\u2026"
                : failures > 0
                    ? $"Transcription failed {failures} time{(failures != 1 ? "s" : "")} \u2014 click Queue to retry."
                    : "Not yet transcribed \u2014 edit details and queue when ready.";

        // Populate speaker names
        _pendingDrawerSpeakerNames = LoadSpeakerNamesFromSessionDir(item.SessionDir);
        _speakerNameItems = _pendingDrawerSpeakerNames;
        SpeakerNamesList.ItemsSource = _speakerNameItems;
        SpeakerNamesPanel.Visibility = Visibility.Visible;

        // Show/hide queue button based on transcription state
        QueueTranscriptionPanel.Visibility = item.IsTranscribing
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Load audio playback
        StopPlayback();
        _audioPlayer.Close();
        _externalAudioPath = null;
        LoadPendingAudioPlayback(item.SessionDir);

        // Remember the session dir so TryAutoOpenTranscriptInDrawer can transition
        _drawerAutoOpenSessionDir = item.SessionDir;

        DrawerPanel.Visibility = Visibility.Visible;
        AnimateDrawer(open: true);
    }

    private void LoadPendingAudioPlayback(string sessionDir)
    {
        var micPath = Path.Combine(sessionDir, "mic.wav");
        var systemPath = Path.Combine(sessionDir, "system.wav");

        string? audioPath = null;
        if (File.Exists(micPath))
            audioPath = micPath;
        else
        {
            // Imported audio file -- find the first supported file
            var files = Directory.Exists(sessionDir) ? Directory.GetFiles(sessionDir) : Array.Empty<string>();
            audioPath = files.FirstOrDefault(f => _fileTranscriptionService.IsSupported(f));
        }

        if (audioPath is null)
        {
            PlaybackPanel.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            if (File.Exists(micPath) && File.Exists(systemPath))
                _audioPlayer.Open(micPath, systemPath);
            else
                _audioPlayer.Open(audioPath);

            PlaybackPanel.Visibility = Visibility.Visible;
            PlayPauseButton.Content = "Play";
            PlayPauseButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Visible;
            PlaybackPositionText.Visibility = Visibility.Visible;
            OpenExternalButton.Visibility = Visibility.Collapsed;
            AudioFileSizeText.Visibility = Visibility.Collapsed;
            DeleteAudioButton.Visibility = Visibility.Collapsed;
            PlaybackSeekBar.Visibility = Visibility.Visible;
            PlaybackSeekBar.Value = 0;
            UpdatePlaybackPositionText();
        }
        catch (Exception ex)
        {
            Trace.TraceInformation("[TranscriptsPage] Inline playback not supported for pending audio '{0}': {1}", audioPath, ex.Message);
            _externalAudioPath = audioPath;
            PlaybackPanel.Visibility = Visibility.Visible;
            PlayPauseButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Collapsed;
            PlaybackPositionText.Visibility = Visibility.Collapsed;
            PlaybackSeekBar.Visibility = Visibility.Collapsed;
            OpenExternalButton.Visibility = Visibility.Visible;
        }
    }

    private static bool TryLoadPendingName(string sessionDir, out string name)
    {
        name = "";
        var nameFile = Path.Combine(sessionDir, "transcript_name.json");
        if (!File.Exists(nameFile)) return false;

        try
        {
            var json = File.ReadAllText(nameFile);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("name", out var nameElement))
            {
                var val = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    name = val;
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private List<SpeakerNameItem> LoadSpeakerNamesFromSessionDir(string sessionDir)
    {
        // Try to load speaker names from the transcript_name.json file if it exists
        var nameFile = Path.Combine(sessionDir, "transcript_name.json");
        if (File.Exists(nameFile))
        {
            try
            {
                var json = File.ReadAllText(nameFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("speakers", out var speakersElement))
                {
                    return speakersElement.EnumerateArray()
                        .Select(s => new SpeakerNameItem { Name = s.GetString() ?? "" })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[TranscriptsPage] Failed to load speaker names from {0}: {1}", nameFile, ex.Message);
            }
        }
        return new List<SpeakerNameItem>();
    }

    private void SavePendingDrawerMetadata()
    {
        if (_pendingDrawerItem is null) return;

        var sessionDir = _pendingDrawerItem.SessionDir;
        var nameFile = Path.Combine(sessionDir, "transcript_name.json");

        var speakers = _pendingDrawerSpeakerNames
            .Select(i => i.Name ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        try
        {
            var data = new
            {
                name = _pendingDrawerTitle,
                speakers
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(nameFile, json);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[TranscriptsPage] Failed to save pending metadata to {0}: {1}", nameFile, ex.Message);
        }
    }

    /// <summary>
    /// Updates the pipeline progress text shown in the banner (e.g. "Diarizing mic 3/84...").
    /// </summary>
    public void UpdatePipelineProgress(string text)
    {
        Dispatcher.BeginInvoke(() => PipelineProgressText.Text = text);
    }

    /// <summary>
    /// Updates the banner to show how many transcriptions are queued behind the current one.
    /// </summary>
    public void UpdateQueueCount(int queuedCount)
    {
        Dispatcher.Invoke(() =>
        {
            QueueCountText.Text = queuedCount > 0
                ? $" (+{queuedCount} queued)"
                : "";
        });
    }

    // --- Start/Stop Recording + Browse ---

    private void UpdateRecordingButtonState()
    {
        if (_recordingService.IsRecording)
        {
            RecordButtonText.Text = "Stop Recording";
            RecordButtonIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.RecordStop24;
        }
        else
        {
            RecordButtonText.Text = "Start Recording";
            RecordButtonIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Record24;
        }
    }

    private void StartStopRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingService.IsRecording)
        {
            _recordingService.StopRecording();
        }
        else
        {
            _recordingService.StartRecording();
        }

        UpdateRecordingButtonState();
    }

    private void BrowseFiles_Click(object sender, RoutedEventArgs e)
    {
        var supportedExts = string.Join(";", _fileTranscriptionService.SupportedExtensions.Select(ext => $"*{ext}"));
        var dialog = new OpenFileDialog
        {
            Title = "Import audio files",
            Filter = $"Audio files ({supportedExts})|{supportedExts}|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
            return;

        foreach (var filePath in dialog.FileNames)
        {
            if (!_fileTranscriptionService.IsSupported(filePath))
                continue;

            ImportAudioFile(filePath);
        }
    }

    private void ImportAudioFile(string sourceFilePath)
    {
        if (_storageService is not TranscriptStorageService concreteStorage)
        {
            Trace.TraceWarning("[TranscriptsPage] Cannot import file: storage service is not concrete.");
            return;
        }

        var now = DateTimeOffset.Now;
        var sessionDir = concreteStorage.CreateSessionDirectory(now);
        var originalFileName = Path.GetFileName(sourceFilePath);
        var destPath = Path.Combine(sessionDir, originalFileName);

        // Move the file, fall back to copy on failure
        try
        {
            File.Move(sourceFilePath, destPath);
            Trace.TraceInformation("[TranscriptsPage] Moved imported file to {0}", destPath);
        }
        catch (IOException)
        {
            try
            {
                File.Copy(sourceFilePath, destPath, overwrite: false);
                Trace.TraceInformation("[TranscriptsPage] Copied imported file to {0} (move failed, cross-drive?)", destPath);
            }
            catch (Exception ex)
            {
                Trace.TraceError("[TranscriptsPage] Failed to import file '{0}': {1}", sourceFilePath, ex.Message);
                return;
            }
        }

        // Derive a title from the original filename (without extension)
        var title = Path.GetFileNameWithoutExtension(originalFileName);

        // Enqueue for transcription via the queue service (file-based transcription)
        // The queue service ProcessFileItem will be updated to produce a transcript.json
        var queueItem = _queueService.EnqueueFileImport(title, destPath, sessionDir);

        Trace.TraceInformation("[TranscriptsPage] Imported and enqueued '{0}' from '{1}'", title, sourceFilePath);

        // Refresh the list to show the new pending item
        LoadTranscriptList();
    }

    // --- List loading ---

    private void LoadTranscriptList()
    {
        _allItems.Clear();
        var files = _storageService.ListTranscriptFiles();

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var item = new TranscriptListItem(filePath, fileName);
            _allItems.Add(item);
        }

        ApplyFilter();
        LoadPendingSessions();
    }

    private void LoadPendingSessions()
    {
        if (_storageService is not TranscriptStorageService concreteStorage)
        {
            PendingSection.Visibility = Visibility.Collapsed;
            TranscribingSection.Visibility = Visibility.Collapsed;
            OtherMachinePendingSection.Visibility = Visibility.Collapsed;
            return;
        }

        var pendingDirs = concreteStorage.ListPendingSessions();
        LoadOtherMachinePendingSessions(concreteStorage);

        // Separate currently-transcribing items from truly pending ones
        var transcribingItems = new List<PendingRecordingItem>();
        var pendingItems = new List<PendingRecordingItem>();

        // Check actively queued/processing items (not completed or failed)
        var queuedSessionDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var qItem in _queueService.Items)
        {
            if (qItem.Session is not null &&
                qItem.Stage is not (QueueItemStage.Completed or QueueItemStage.Failed))
            {
                var sessionDir = Path.GetDirectoryName(qItem.Session.MicWavFilePath);
                if (sessionDir is not null)
                    queuedSessionDirs.Add(Path.GetFullPath(sessionDir));
            }
        }

        // Also check queued file import items (by session dir)
        foreach (var qItem in _queueService.Items)
        {
            if (qItem.SessionDir is not null &&
                qItem.Stage is not (QueueItemStage.Completed or QueueItemStage.Failed))
            {
                queuedSessionDirs.Add(Path.GetFullPath(qItem.SessionDir));
            }
        }

        // The "Transcribing" state comes from the queue's active item — never
        // from drawer state, which used to mark any clicked pending item as
        // transcribing and never clear it once the queue drained.
        string? activeSessionDir = null;
        if (_queueService.ActiveItem is { } active &&
            active.Stage is not (QueueItemStage.Completed or QueueItemStage.Failed))
        {
            activeSessionDir = active.SessionDir ??
                (active.Session is not null
                    ? Path.GetDirectoryName(active.Session.MicWavFilePath)
                    : null);
        }

        foreach (var dir in pendingDirs)
        {
            var dirName = Path.GetFileName(dir);
            var isCurrentlyTranscribing = activeSessionDir is not null &&
                string.Equals(Path.GetFullPath(dir),
                    Path.GetFullPath(activeSessionDir),
                    StringComparison.OrdinalIgnoreCase);
            var isQueued = queuedSessionDirs.Contains(Path.GetFullPath(dir));

            // Check if a queued item (e.g. file import) has a custom title for this session
            var fullDirPath = Path.GetFullPath(dir);
            var matchingQueueItem = _queueService.Items.FirstOrDefault(q =>
                q.SessionDir is not null &&
                string.Equals(Path.GetFullPath(q.SessionDir), fullDirPath, StringComparison.OrdinalIgnoreCase) &&
                q.Stage is not (QueueItemStage.Completed or QueueItemStage.Failed));

            string name;
            if (matchingQueueItem is not null && !string.IsNullOrWhiteSpace(matchingQueueItem.Title))
            {
                // Use the title from the queue item (e.g. original filename for imports)
                name = matchingQueueItem.Title;
            }
            else if (TryLoadPendingName(dir, out var savedName))
            {
                name = savedName;
            }
            else if (dirName.Length >= 15 &&
                DateTime.TryParseExact(
                    dirName[..15],
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date))
            {
                name = $"Call {date:yyyy-MM-dd HH:mm}";
            }
            else
            {
                name = dirName;
            }

            if (isCurrentlyTranscribing)
            {
                var activeItem = _queueService.ActiveItem;
                var stageText = activeItem is not null
                    ? $"{activeItem.Stage} ({activeItem.OverallPercent}%)"
                    : "Transcribing...";
                transcribingItems.Add(new PendingRecordingItem(dir, name, stageText, true));
            }
            else if (isQueued)
            {
                // Already in queue — show as "Queued" with distinct visual state
                transcribingItems.Add(new PendingRecordingItem(dir, name, "Queued", true, isQueued: true));
            }
            else
            {
                var audioFiles = CountAudioFiles(dir);
                var detail = $"{audioFiles} audio file{(audioFiles != 1 ? "s" : "")}";
                var failures = TranscriptionQueueService.GetSessionFailureCount(dir);
                pendingItems.Add(new PendingRecordingItem(dir, name, detail, false, failureCount: failures));
            }
        }

        // Transcribing section
        if (transcribingItems.Count > 0)
        {
            TranscribingList.ItemsSource = transcribingItems;
            TranscribingSection.Visibility = Visibility.Visible;
        }
        else
        {
            TranscribingSection.Visibility = Visibility.Collapsed;
        }

        // Pending section
        if (pendingItems.Count > 0)
        {
            PendingList.ItemsSource = pendingItems;
            PendingCountText.Text = $"({pendingItems.Count})";
            PendingSection.Visibility = Visibility.Visible;
        }
        else
        {
            PendingSection.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Loads pending sessions originated by other machines (cross-machine sync
    /// via Drive) into the "Other machine" section. Each entry includes a
    /// "Transcribe here" action that takes over the work locally.
    /// </summary>
    private void LoadOtherMachinePendingSessions(TranscriptStorageService concreteStorage)
    {
        var otherDirs = concreteStorage.ListPendingSessionsFromOtherMachines();

        if (otherDirs.Count == 0)
        {
            OtherMachinePendingSection.Visibility = Visibility.Collapsed;
            return;
        }

        var items = new List<OtherMachinePendingItem>(otherDirs.Count);
        foreach (var dir in otherDirs)
        {
            var origin =
                Services.Recording.SessionMetadataStore.ResolveOriginMachineId(dir)
                ?? "unknown";

            var dirName = Path.GetFileName(dir);
            string name;
            if (TryLoadPendingName(dir, out var saved))
            {
                name = saved;
            }
            else if (dirName.Length >= 15 &&
                DateTime.TryParseExact(
                    dirName[..15],
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date))
            {
                name = $"Call {date:yyyy-MM-dd HH:mm}";
            }
            else
            {
                name = dirName;
            }

            var audioFiles = CountAudioFiles(dir);
            var detail = $"{audioFiles} audio file{(audioFiles != 1 ? "s" : "")}";

            var existingLock = Services.Recording.TranscribingLock.TryRead(dir);
            string machineLabel = existingLock is not null
                ? $"{origin} → {existingLock.MachineId}"
                : origin;

            items.Add(new OtherMachinePendingItem(dir, name, detail, machineLabel));
        }

        OtherMachinePendingList.ItemsSource = items;
        OtherMachinePendingCountText.Text = $"({items.Count})";
        OtherMachinePendingSection.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// "Transcribe here" — manual takeover of another machine's pending recording.
    /// Writes an advisory <c>transcribing.lock</c>, enqueues the session in this
    /// machine's transcription queue, and the lock is removed when the queue item
    /// completes or fails (see <see cref="OnQueueItemCompleted"/>).
    /// </summary>
    private void TranscribeHere_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string sessionDir)
            return;
        if (!Directory.Exists(sessionDir))
            return;

        // Resolve origin so we can keep showing it in the queue title.
        var origin =
            Services.Recording.SessionMetadataStore.ResolveOriginMachineId(sessionDir)
            ?? "unknown";

        // Best-effort advisory lock; failures here are non-fatal (see TranscribingLock).
        var myMachineId =
            (_storageService as TranscriptStorageService)?.DataPathService.MachineId
            ?? Environment.MachineName;
        Services.Recording.TranscribingLock.Write(sessionDir, myMachineId);

        // Build the same kind of session object QueueTranscription_Click does.
        var micPath = Path.Combine(sessionDir, "mic.wav");
        var systemPath = Path.Combine(sessionDir, "system.wav");
        var dirName = Path.GetFileName(sessionDir);

        DateTimeOffset startTimestamp;
        if (dirName.Length >= 15 &&
            DateTime.TryParseExact(
                dirName[..15],
                "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date))
        {
            startTimestamp = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
        }
        else
        {
            startTimestamp = DateTimeOffset.UtcNow;
        }

        if (File.Exists(micPath))
        {
            var session = new CallRecordingSession(
                micPath,
                File.Exists(systemPath) ? systemPath : micPath,
                startTimestamp);

            var lastWrite = File.GetLastWriteTimeUtc(micPath);
            session.EndTimestamp = new DateTimeOffset(lastWrite, TimeSpan.Zero);
            session.Title = $"Call {startTimestamp.LocalDateTime:yyyy-MM-dd HH:mm} (from {origin})";

            _queueService.Enqueue(session.Title!, session);
        }
        else
        {
            var audioFile = Directory.GetFiles(sessionDir)
                .FirstOrDefault(f => _fileTranscriptionService.IsSupported(f));
            if (audioFile is null)
            {
                Trace.TraceWarning(
                    "[TranscriptsPage] Other-machine session has no supported audio files: {0}",
                    sessionDir);
                Services.Recording.TranscribingLock.Remove(sessionDir);
                return;
            }

            var title = $"{Path.GetFileNameWithoutExtension(audioFile)} (from {origin})";
            _queueService.EnqueueFileImport(title, audioFile, sessionDir);
        }

        Trace.TraceInformation(
            "[TranscriptsPage] User took over transcription for {0} (origin {1})",
            sessionDir, origin);

        // Refresh now; ItemCompleted will fire later to drop the lock and refresh again.
        LoadPendingSessions();
    }

    /// <summary>
    /// Counts audio files (WAV + supported import formats) in a session directory.
    /// </summary>
    private int CountAudioFiles(string dir)
    {
        var count = 0;
        foreach (var file in Directory.GetFiles(dir))
        {
            var ext = Path.GetExtension(file);
            if (string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase) ||
                _fileTranscriptionService.SupportedExtensions.Contains(ext.ToLowerInvariant()))
            {
                count++;
            }
        }

        return count;
    }

    private void ApplyFilter()
    {
        var searchText = SearchBox?.Text?.Trim() ?? string.Empty;

        IEnumerable<TranscriptListItem> filtered;
        if (string.IsNullOrEmpty(searchText))
        {
            filtered = _allItems;
        }
        else
        {
            filtered = _allItems.Where(i =>
                i.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.SpeakersDisplay.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.TimeDisplay.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.PreviewText.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                i.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Group by date (day)
        var grouped = filtered
            .GroupBy(i => i.GroupKey)
            .OrderByDescending(g => g.Key)
            .ToList();

        // Preserve expanded state from existing groups
        var expandedState = _groups.ToDictionary(g => g.GroupName, g => g.IsExpanded);

        _groups.Clear();
        foreach (var group in grouped)
        {
            var displayName = group.First().GroupDisplayName;
            var isExpanded = expandedState.TryGetValue(displayName, out var was) ? was : true;
            var sortedItems = ApplySortWithinGroup(group.ToList());
            _groups.Add(new TranscriptGroupViewModel(displayName, sortedItems, isExpanded));
        }

        TranscriptGroupList.ItemsSource = null;
        TranscriptGroupList.ItemsSource = _groups;

        var totalCount = filtered.Count();
        EmptyState.Visibility = totalCount == 0
            && PendingSection.Visibility == Visibility.Collapsed
            && ActiveRecordingCard.Visibility == Visibility.Collapsed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private List<TranscriptListItem> ApplySortWithinGroup(List<TranscriptListItem> items)
    {
        IEnumerable<TranscriptListItem> sorted = _sortColumn switch
        {
            "Title" => _sortAscending
                ? items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                : items.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),
            "Speakers" => _sortAscending
                ? items.OrderBy(i => i.SpeakersDisplay, StringComparer.OrdinalIgnoreCase)
                : items.OrderByDescending(i => i.SpeakersDisplay, StringComparer.OrdinalIgnoreCase),
            _ => _sortAscending // "Time" — sort by start time
                ? items.OrderBy(i => i.ParsedDate ?? DateTime.MinValue)
                : items.OrderByDescending(i => i.ParsedDate ?? DateTime.MinValue),
        };
        return sorted.ToList();
    }

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string column) return;

        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = column == "Title"; // Title defaults ascending, others descending
        }

        UpdateSortIndicators();
        ApplyFilter();
    }

    private void UpdateSortIndicators()
    {
        var arrow = _sortAscending ? " \u2191" : " \u2193"; // ↑ or ↓
        TitleSortHeader.Text = "TITLE" + (_sortColumn == "Title" ? arrow : "");
        TimeSortHeader.Text = "TIME" + (_sortColumn == "Time" ? arrow : "");
        SpeakersSortHeader.Text = "SPEAKERS" + (_sortColumn == "Speakers" ? arrow : "");
    }

    // --- Search ---

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();

        if (_selectedTranscript is not null && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            HighlightMatchingSegments(SearchBox.Text.Trim());
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
    }

    // --- Row clicks ---

    private void TranscriptRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TranscriptListItem item })
            return;

        OpenTranscriptDrawer(item);
    }

    private void PendingRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PendingRecordingItem item })
            return;

        // Just open the drawer -- do NOT auto-enqueue for transcription
        OpenPendingTranscribingDrawer(item);
        e.Handled = true;
    }

    // --- Group toggle ---

    private void GroupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle)
            return;

        // The pending / other-machine groups share this handler; route by sender.
        if (ReferenceEquals(toggle, OtherMachinePendingGroupToggle))
        {
            OtherMachinePendingList.Visibility = toggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            OtherMachinePendingGroupChevron.Text = toggle.IsChecked == true ? "\uE70D" : "\uE70E";
            return;
        }

        // Default: pending (mine) group toggle.
        PendingList.Visibility = toggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PendingGroupChevron.Text = toggle.IsChecked == true ? "\uE70D" : "\uE70E";
    }

    private void TranscriptGroupToggle_Click(object sender, RoutedEventArgs e)
    {
        // The binding on IsExpanded handles visibility; just refresh chevron
        if (sender is FrameworkElement { DataContext: TranscriptGroupViewModel group })
        {
            group.OnPropertyChanged(nameof(TranscriptGroupViewModel.ChevronText));
        }
    }

    private void ExpandCollapseAll_Click(object sender, RoutedEventArgs e)
    {
        var allExpanded = _groups.All(g => g.IsExpanded);
        var newState = !allExpanded;
        foreach (var group in _groups)
        {
            group.IsExpanded = newState;
        }
    }

    // --- Drawer ---

    private async void OpenTranscriptDrawer(TranscriptListItem item)
    {
        try
        {
            var transcript = await _storageService.LoadAsync(item.FilePath);
            if (transcript is null)
            {
                return;
            }

            var drawerAlreadyOpen = DrawerPanel.Visibility == Visibility.Visible;

            _isActiveRecordingDrawerOpen = false;
            _isPendingDrawerOpen = false;
            _pendingDrawerItem = null;
            TranscriptDrawerContent.Visibility = Visibility.Visible;
            QueueTranscriptionPanel.Visibility = Visibility.Collapsed;

            // Ensure recording-specific elements are hidden
            RecordingIndicatorPanel.Visibility = Visibility.Collapsed;
            DrawerRecordingDuration.Visibility = Visibility.Collapsed;
            RecordingInfoText.Visibility = Visibility.Collapsed;
            TranscriptScrollViewer.Visibility = Visibility.Visible;
            TranscriptInfo.Visibility = Visibility.Visible;

            if (drawerAlreadyOpen)
            {
                // Crossfade: fade out, swap content, fade in
                var fadeOut = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                };
                fadeOut.Completed += (_, _) =>
                {
                    StopPlayback();
                    _selectedTranscript = transcript;
                    _selectedListItem = item;
                    DisplayTranscript(transcript);

                    var fadeIn = new DoubleAnimation
                    {
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(150),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    };
                    TranscriptDrawerContent.BeginAnimation(OpacityProperty, fadeIn);
                };
                TranscriptDrawerContent.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                _selectedTranscript = transcript;
                _selectedListItem = item;
                DisplayTranscript(transcript);
                DrawerPanel.Visibility = Visibility.Visible;
                AnimateDrawer(open: true);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to load transcript: {0}", ex.Message);
        }
    }

    /// <summary>
    /// If the drawer was left open after a recording session ended, try to auto-open
    /// the completed transcript so the drawer transitions live to the transcribed state.
    /// </summary>
    private void TryAutoOpenTranscriptInDrawer()
    {
        if (_drawerAutoOpenSessionDir is null) return;
        if (DrawerPanel.Visibility != Visibility.Visible) return;
        if (_isActiveRecordingDrawerOpen) return;

        // Find the transcript item that matches the session directory
        var sessionDir = Path.GetFullPath(_drawerAutoOpenSessionDir);
        var matchingItem = _allItems.FirstOrDefault(item =>
        {
            var itemDir = Path.GetDirectoryName(item.FilePath);
            return itemDir is not null &&
                   string.Equals(Path.GetFullPath(itemDir), sessionDir, StringComparison.OrdinalIgnoreCase);
        });

        if (matchingItem is not null)
        {
            _drawerAutoOpenSessionDir = null;
            OpenTranscriptDrawer(matchingItem);
        }
    }

    private void AnimateDrawer(bool open)
    {
        var anim = new DoubleAnimation
        {
            To = open ? 0 : 520,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = open
                ? new CubicEase { EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseIn },
        };

        if (!open)
        {
            anim.Completed += (_, _) =>
            {
                DrawerPanel.Visibility = Visibility.Collapsed;
            };
        }

        DrawerTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void CloseDrawer()
    {
        if (_isActiveRecordingDrawerOpen)
        {
            // Save active recording metadata before closing
            _activeRecordingTitle = TranscriptNameBox.Text?.Trim() ?? "";
            _isActiveRecordingDrawerOpen = false;

            // Update the card subtitle with the title if set
            if (!string.IsNullOrWhiteSpace(_activeRecordingTitle))
                ActiveRecordingTitle.Text = _activeRecordingTitle;
            else
                ActiveRecordingTitle.Text = "Recording in progress...";

            var speakerCount = _activeRecordingSpeakerNames.Count(s => !string.IsNullOrWhiteSpace(s.Name));
            ActiveRecordingSubtitle.Text = speakerCount > 0
                ? $"{speakerCount} speaker{(speakerCount != 1 ? "s" : "")} defined - Click to edit"
                : "Click to edit metadata";
        }

        if (_isPendingDrawerOpen)
        {
            // Save pending drawer metadata before closing
            _pendingDrawerTitle = TranscriptNameBox.Text?.Trim() ?? _pendingDrawerTitle;
            SavePendingDrawerMetadata();
            _isPendingDrawerOpen = false;
            _pendingDrawerItem = null;
        }

        // Hide recording-specific elements
        RecordingIndicatorPanel.Visibility = Visibility.Collapsed;
        DrawerRecordingDuration.Visibility = Visibility.Collapsed;
        RecordingInfoText.Visibility = Visibility.Collapsed;

        StopPlayback();
        _audioPlayer.Close();
        PlaybackPanel.Visibility = Visibility.Collapsed;
        SpeakerNamesPanel.Visibility = Visibility.Collapsed;
        QueueTranscriptionPanel.Visibility = Visibility.Collapsed;
        TranscriptScrollViewer.Visibility = Visibility.Visible;

        _selectedTranscript = null;
        _selectedListItem = null;
        _currentSegmentViewModels = null;
        ActionPanel.Visibility = Visibility.Collapsed;
        SegmentList.ItemsSource = null;

        AnimateDrawer(open: false);
    }

    private void DrawerClose_Click(object sender, RoutedEventArgs e)
    {
        CloseDrawer();
    }


    // --- Transcript display ---

    private void DisplayTranscript(CallTranscript transcript)
    {
        StopPlayback();

        PlaceholderText.Visibility = Visibility.Collapsed;
        ActionPanel.Visibility = Visibility.Visible;

        TranscriptNameBox.IsReadOnly = false;
        TranscriptNameBox.Text = transcript.Name;
        TranscriptInfo.Text = $"Started: {transcript.RecordingStartedUtc.LocalDateTime:HH:mm:ss} | " +
                              $"Duration: {transcript.Duration:hh\\:mm\\:ss} | " +
                              $"Segments: {transcript.Segments.Count}";

        var availableNames = BuildAvailableSpeakerNames();
        var viewModels = transcript.Segments
            .Select(s => new SegmentViewModel(s, transcript, availableNames))
            .ToList();

        _currentSegmentViewModels = viewModels;
        SegmentList.ItemsSource = viewModels;

        ShowSpeakerNamesPanel();

        _externalAudioPath = null;
        var (micPath, sysPath) = transcript.ResolvedSourceAudioPaths;
        var audioPath = micPath ?? sysPath ?? transcript.ResolvedAudioFilePath;
        if (audioPath is not null)
        {
            // Show audio file size
            var totalBytes = GetAudioFileTotalSize(transcript);
            var sizeMb = totalBytes / (1024.0 * 1024.0);
            AudioFileSizeText.Text = $"{sizeMb:F1} MB";
            AudioFileSizeText.Visibility = Visibility.Visible;

            // Enable delete-audio only when transcription segments exist
            var hasSegments = transcript.Segments is { Count: > 0 };
            DeleteAudioButton.IsEnabled = hasSegments;
            DeleteAudioButton.Visibility = Visibility.Visible;
            ReTranscribeButton.Visibility = Visibility.Visible;

            try
            {
                if (micPath is not null && sysPath is not null)
                    _audioPlayer.Open(micPath, sysPath);
                else
                    _audioPlayer.Open(audioPath);
                PlaybackPanel.Visibility = Visibility.Visible;
                PlayPauseButton.Content = "Play";
                PlayPauseButton.Visibility = Visibility.Visible;
                StopButton.Visibility = Visibility.Visible;
                PlaybackPositionText.Visibility = Visibility.Visible;
                OpenExternalButton.Visibility = Visibility.Collapsed;
                PlaybackSeekBar.Visibility = Visibility.Visible;
                PlaybackSeekBar.Value = 0;
                UpdatePlaybackPositionText();
                Trace.TraceInformation("[TranscriptsPage] Audio available for inline playback: {0}", audioPath);
            }
            catch (Exception ex)
            {
                Trace.TraceInformation("[TranscriptsPage] Inline playback not supported for '{0}', using external: {1}", audioPath, ex.Message);
                _externalAudioPath = audioPath;
                PlaybackPanel.Visibility = Visibility.Visible;
                PlayPauseButton.Visibility = Visibility.Collapsed;
                StopButton.Visibility = Visibility.Collapsed;
                PlaybackPositionText.Visibility = Visibility.Collapsed;
                PlaybackSeekBar.Visibility = Visibility.Collapsed;
                OpenExternalButton.Visibility = Visibility.Visible;
            }
        }
        else
        {
            PlaybackPanel.Visibility = Visibility.Collapsed;
            ReTranscribeButton.Visibility = Visibility.Collapsed;
            SetSegmentCursors(Cursors.Arrow);
        }
    }

    private void SetSegmentCursors(Cursor cursor)
    {
        if (_currentSegmentViewModels is null) return;
        foreach (var vm in _currentSegmentViewModels)
            vm.SegmentCursor = cursor;
    }

    // --- Transcript name editing ---

    private async void TranscriptNameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await SaveTranscriptNameAsync();
    }

    private async void TranscriptNameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            await SaveTranscriptNameAsync();

            // Update active recording card title immediately
            if (_isActiveRecordingDrawerOpen)
            {
                var title = TranscriptNameBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    ActiveRecordingTitle.Text = title;
            }

            System.Windows.Input.Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            // Revert to saved name and dismiss focus
            if (_selectedTranscript is not null)
                TranscriptNameBox.Text = _selectedTranscript.Name;
            else if (_isActiveRecordingDrawerOpen)
                TranscriptNameBox.Text = _activeRecordingTitle;
            else if (_isPendingDrawerOpen)
                TranscriptNameBox.Text = _pendingDrawerTitle;

            System.Windows.Input.Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private async Task SaveTranscriptNameAsync()
    {
        var newName = TranscriptNameBox.Text?.Trim();

        // For active recordings, just update the in-memory title
        if (_isActiveRecordingDrawerOpen && _selectedTranscript is null)
        {
            if (!string.IsNullOrEmpty(newName))
                _activeRecordingTitle = newName;
            return;
        }

        // For pending drawer, save to metadata file
        if (_isPendingDrawerOpen && _selectedTranscript is null)
        {
            if (!string.IsNullOrEmpty(newName))
            {
                _pendingDrawerTitle = newName;
                SavePendingDrawerMetadata();
            }
            return;
        }

        if (_selectedTranscript is null) return;

        if (string.IsNullOrEmpty(newName) || newName == _selectedTranscript.Name)
            return;

        _selectedTranscript.Name = newName;

        try
        {
            await _storageService.UpdateAsync(_selectedTranscript);

            if (_selectedListItem is not null)
            {
                _selectedListItem.Name = newName;
                ApplyFilter();
            }

            Trace.TraceInformation("[TranscriptsPage] Renamed transcript to: {0}", newName);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to save transcript name: {0}", ex.Message);
        }
    }

    private void HighlightMatchingSegments(string searchText)
    {
        // Placeholder for segment highlighting
    }

    // --- Delete ---

    private void DrawerDeleteTranscript_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedListItem is null)
            return;

        DeleteTranscriptItem(_selectedListItem);
    }

    private void DeleteTranscriptItem(TranscriptListItem item)
    {
        var displayName = !string.IsNullOrEmpty(item.Name) ? item.Name : item.FileName;
        var dialog = new WhisperHeim.Views.DeleteConfirmationDialog(displayName)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        StopPlayback();
        _audioPlayer.Close();

        try
        {
            if (File.Exists(item.FilePath))
            {
                if (_storageService is TranscriptStorageService concreteStorage)
                {
                    concreteStorage.DeleteSession(item.FilePath);
                    Trace.TraceInformation("[TranscriptsPage] Deleted session for: {0}", item.FilePath);
                }
                else
                {
                    var audioPath = _selectedTranscript?.ResolvedAudioFilePath;
                    if (audioPath is not null && File.Exists(audioPath))
                    {
                        File.Delete(audioPath);
                        Trace.TraceInformation("[TranscriptsPage] Deleted audio: {0}", audioPath);
                    }

                    File.Delete(item.FilePath);
                    Trace.TraceInformation("[TranscriptsPage] Deleted transcript: {0}", item.FilePath);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to delete transcript: {0}", ex.Message);
        }

        _allItems.Remove(item);
        CloseDrawer();
        ApplyFilter();
    }

    private async void DeleteAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null || _selectedListItem is null)
            return;

        var displayName = !string.IsNullOrEmpty(_selectedListItem.Name)
            ? _selectedListItem.Name
            : _selectedListItem.FileName;

        var dialog = new WhisperHeim.Views.DeleteConfirmationDialog(
            displayName, "Delete Audio Files")
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        StopPlayback();
        _audioPlayer.Close();

        try
        {
            // Delete mic.wav and system.wav
            var (micPath, sysPath) = _selectedTranscript.ResolvedSourceAudioPaths;
            if (micPath is not null && File.Exists(micPath))
            {
                File.Delete(micPath);
                Trace.TraceInformation("[TranscriptsPage] Deleted mic audio: {0}", micPath);
            }
            if (sysPath is not null && File.Exists(sysPath))
            {
                File.Delete(sysPath);
                Trace.TraceInformation("[TranscriptsPage] Deleted system audio: {0}", sysPath);
            }

            // Delete combined audio file referenced by audioFilePath
            var combinedPath = _selectedTranscript.ResolvedAudioFilePath;
            if (combinedPath is not null && File.Exists(combinedPath))
            {
                File.Delete(combinedPath);
                Trace.TraceInformation("[TranscriptsPage] Deleted combined audio: {0}", combinedPath);
            }

            // Clear audioFilePath in the transcript and persist
            _selectedTranscript.AudioFilePath = null;
            await _storageService.UpdateAsync(_selectedTranscript);

            // Hide the playback panel, re-transcribe button, and remove hand cursor from segments
            PlaybackPanel.Visibility = Visibility.Collapsed;
            ReTranscribeButton.Visibility = Visibility.Collapsed;
            SetSegmentCursors(Cursors.Arrow);

            Trace.TraceInformation("[TranscriptsPage] Audio files deleted, transcript preserved for: {0}",
                _selectedListItem.FilePath);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to delete audio files: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Calculates the total size of WAV audio files for the given transcript in bytes.
    /// </summary>
    private static long GetAudioFileTotalSize(CallTranscript transcript)
    {
        long totalSize = 0;

        var (micPath, sysPath) = transcript.ResolvedSourceAudioPaths;
        if (micPath is not null && File.Exists(micPath))
            totalSize += new FileInfo(micPath).Length;
        if (sysPath is not null && File.Exists(sysPath))
            totalSize += new FileInfo(sysPath).Length;

        var combinedPath = transcript.ResolvedAudioFilePath;
        if (combinedPath is not null && File.Exists(combinedPath)
            && combinedPath != micPath && combinedPath != sysPath)
        {
            totalSize += new FileInfo(combinedPath).Length;
        }

        return totalSize;
    }

    // --- Audio playback ---

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (!_audioPlayer.IsLoaded)
            return;

        _audioPlayer.TogglePlayPause();
        UpdatePlayPauseButton();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
    }

    private void Segment_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled)
            return;

        // Don't trigger audio playback when clicking on interactive controls (ComboBox, TextBox, etc.)
        if (e.OriginalSource is DependencyObject source && IsInsideInteractiveControl(source))
            return;

        // External playback mode: open with default OS player
        if (_externalAudioPath is not null)
        {
            OpenAudioExternally();
            e.Handled = true;
            return;
        }

        if (!_audioPlayer.IsLoaded)
            return;

        if (sender is FrameworkElement { DataContext: SegmentViewModel vm })
        {
            _audioPlayer.PlayFrom(vm.Segment.StartTime);
            UpdatePlayPauseButton();
            e.Handled = true;
        }
    }

    private static bool IsInsideInteractiveControl(DependencyObject element)
    {
        var current = element;
        while (current != null)
        {
            if (current is ComboBox or TextBox or ToggleButton)
                return true;
            // Stop walking when we reach the segment Border
            if (current is Border { Cursor: var cursor } && cursor == System.Windows.Input.Cursors.Hand)
                break;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void OnAudioPositionChanged(object? sender, TimeSpan position)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdatePlaybackPositionText();
            UpdatePlayingSegmentHighlight(position);
            UpdateSeekBar();
        });
    }

    private void OnAudioPlaybackStopped(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdatePlayPauseButton();
            ClearSegmentHighlights();
        });
    }

    private void StopPlayback()
    {
        _audioPlayer.Stop();
        UpdatePlayPauseButton();
        ClearSegmentHighlights();
        UpdatePlaybackPositionText();
        UpdateSeekBar();
    }

    // --- Seek bar ---

    private void SeekBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isSeekBarDragging = true;
    }

    private void SeekBar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isSeekBarDragging = false;
        if (!_audioPlayer.IsLoaded) return;

        var total = _audioPlayer.TotalDuration.TotalSeconds;
        if (total <= 0) return;

        var seekTo = TimeSpan.FromSeconds(PlaybackSeekBar.Value / 100.0 * total);
        _audioPlayer.Seek(seekTo);
        UpdatePlaybackPositionText();
    }

    private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Only seek during drag -- otherwise the position timer handles updates
        if (!_isSeekBarDragging || !_audioPlayer.IsLoaded) return;

        var total = _audioPlayer.TotalDuration.TotalSeconds;
        if (total <= 0) return;

        var seekTo = TimeSpan.FromSeconds(PlaybackSeekBar.Value / 100.0 * total);
        _audioPlayer.Seek(seekTo);
        UpdatePlaybackPositionText();
    }

    private void UpdateSeekBar()
    {
        if (_isSeekBarDragging || !_audioPlayer.IsLoaded) return;

        var total = _audioPlayer.TotalDuration.TotalSeconds;
        if (total <= 0)
        {
            PlaybackSeekBar.Value = 0;
            return;
        }

        var current = _audioPlayer.CurrentPosition.TotalSeconds;
        PlaybackSeekBar.Value = current / total * 100.0;
    }

    // --- Queue Transcription ---

    private void QueueTranscription_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingDrawerItem is null) return;

        // Save any edited metadata first
        _pendingDrawerTitle = TranscriptNameBox.Text?.Trim() ?? _pendingDrawerTitle;
        SavePendingDrawerMetadata();

        var item = _pendingDrawerItem;
        var micPath = Path.Combine(item.SessionDir, "mic.wav");
        var systemPath = Path.Combine(item.SessionDir, "system.wav");

        if (File.Exists(micPath))
        {
            // Standard call recording session
            var dirName = Path.GetFileName(item.SessionDir);
            DateTimeOffset startTimestamp;
            if (dirName.Length >= 15 &&
                DateTime.TryParseExact(
                    dirName[..15],
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date))
            {
                startTimestamp = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
            }
            else
            {
                // Dir name has no parseable timestamp (e.g. recovered_* sessions) —
                // the mic file's creation time is the closest record of the call start.
                startTimestamp = new DateTimeOffset(File.GetCreationTimeUtc(micPath), TimeSpan.Zero);
            }

            var session = new CallRecordingSession(
                micPath,
                File.Exists(systemPath) ? systemPath : micPath,
                startTimestamp);

            var lastWrite = File.GetLastWriteTimeUtc(micPath);
            session.EndTimestamp = new DateTimeOffset(lastWrite, TimeSpan.Zero);

            // Apply speaker names from the pending drawer
            session.RemoteSpeakerNames = _pendingDrawerSpeakerNames
                .Select(i => i.Name ?? "")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            session.Title = !string.IsNullOrWhiteSpace(_pendingDrawerTitle)
                ? _pendingDrawerTitle
                : item.Name;

            TranscriptionRequested?.Invoke(this, session);
        }
        else
        {
            // Imported audio file
            var audioFile = Directory.GetFiles(item.SessionDir)
                .FirstOrDefault(f => _fileTranscriptionService.IsSupported(f));

            if (audioFile is null)
            {
                Trace.TraceWarning("[TranscriptsPage] Pending session has no supported audio files: {0}", item.SessionDir);
                return;
            }

            var title = !string.IsNullOrWhiteSpace(_pendingDrawerTitle)
                ? _pendingDrawerTitle
                : Path.GetFileNameWithoutExtension(audioFile);

            _queueService.EnqueueFileImport(title, audioFile, item.SessionDir);
        }

        // Update drawer to show queued state
        QueueTranscriptionPanel.Visibility = Visibility.Collapsed;
        TranscriptInfo.Text = "Transcription queued \u2014 the drawer will update when complete.";

        // Refresh the pending list
        LoadPendingSessions();
    }

    private void OpenExternal_Click(object sender, RoutedEventArgs e)
    {
        OpenAudioExternally();
    }

    private void OpenAudioExternally()
    {
        if (_externalAudioPath is null || !File.Exists(_externalAudioPath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(_externalAudioPath) { UseShellExecute = true });
            Trace.TraceInformation("[TranscriptsPage] Opened audio externally: {0}", _externalAudioPath);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[TranscriptsPage] Failed to open audio externally: {0}", ex.Message);
        }
    }

    private void UpdatePlayPauseButton()
    {
        PlayPauseButton.Content = _audioPlayer.IsPlaying ? "Pause" : "Play";
    }

    private void UpdatePlaybackPositionText()
    {
        if (!_audioPlayer.IsLoaded)
        {
            PlaybackPositionText.Text = "00:00 / 00:00";
            return;
        }

        var current = _audioPlayer.CurrentPosition;
        var total = _audioPlayer.TotalDuration;
        PlaybackPositionText.Text =
            $"{current:mm\\:ss} / {total:mm\\:ss}";
    }

    private void UpdatePlayingSegmentHighlight(TimeSpan position)
    {
        if (_currentSegmentViewModels is null)
            return;

        foreach (var vm in _currentSegmentViewModels)
        {
            var isPlaying = position >= vm.Segment.StartTime && position < vm.Segment.EndTime;
            vm.IsCurrentlyPlaying = isPlaying;
        }
    }

    private void ClearSegmentHighlights()
    {
        if (_currentSegmentViewModels is null)
            return;

        foreach (var vm in _currentSegmentViewModels)
            vm.IsCurrentlyPlaying = false;
    }

    // --- Speaker name editing ---

    private void SpeakerLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SegmentViewModel vm })
            return;

        vm.IsPerSegmentEdit = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        vm.BeginEditSpeaker();
        e.Handled = true;
    }

    private void SpeakerComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Prevent click from bubbling to the segment Border (which triggers audio playback)
        e.Handled = true;

        // Let WPF's ComboBox handle the click normally by re-raising via dispatcher
        if (sender is ComboBox comboBox)
        {
            comboBox.Dispatcher.BeginInvoke(() =>
            {
                comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
            }, DispatcherPriority.Input);
        }
    }

    /// <summary>
    /// Set when SelectionChanged commits a speaker edit, so that the subsequent
    /// LostFocus handler knows not to commit again (or cancel).
    /// </summary>
    private bool _speakerSelectionCommitted;

    private async void SpeakerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: SegmentViewModel vm } comboBox)
            return;

        if (!vm.IsEditingSpeaker)
            return;

        // Only process if a selection was actually made from the dropdown
        if (e.AddedItems.Count == 0)
            return;

        var selectedName = e.AddedItems[0]?.ToString();
        if (string.IsNullOrEmpty(selectedName))
            return;

        vm.EditingSpeakerName = selectedName;
        _speakerSelectionCommitted = true;
        await CommitSpeakerEditAsync(vm);
    }

    private void SpeakerEditBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is SegmentViewModel { IsEditingSpeaker: true })
        {
            comboBox.Focus();
            comboBox.IsDropDownOpen = true;
        }
        else if (sender is TextBox textBox && textBox.DataContext is SegmentViewModel { IsEditingSpeaker: true })
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void SpeakerEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SegmentViewModel vm } element)
            return;

        // Defer the commit so that SelectionChanged (which fires after LostFocus
        // when the user clicks a dropdown item) has a chance to process first.
        // If SelectionChanged already committed, the flag prevents a duplicate commit.
        element.Dispatcher.BeginInvoke(async () =>
        {
            if (_speakerSelectionCommitted)
            {
                _speakerSelectionCommitted = false;
                Trace.TraceInformation("[TranscriptsPage] LostFocus skipped – selection already committed");
                return;
            }

            await CommitSpeakerEditAsync(vm);
        }, DispatcherPriority.Background);
    }

    private async void SpeakerEditBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SegmentViewModel vm })
            return;

        if (e.Key == Key.Enter)
        {
            await CommitSpeakerEditAsync(vm);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelEditSpeaker();
            e.Handled = true;
        }
    }

    private async Task CommitSpeakerEditAsync(SegmentViewModel vm)
    {
        if (_selectedTranscript is null || !vm.IsEditingSpeaker)
            return;

        var newName = vm.EditingSpeakerName?.Trim();
        var currentDisplay = vm.DisplaySpeaker;

        if (string.IsNullOrEmpty(newName) || newName == currentDisplay)
        {
            vm.CancelEditSpeaker();
            return;
        }

        if (vm.IsPerSegmentEdit)
        {
            vm.Segment.SpeakerOverride = newName;
            vm.CommitEditSpeaker();

            Trace.TraceInformation(
                "[TranscriptsPage] Per-segment speaker rename: '{0}' -> '{1}'",
                currentDisplay, newName);

            // Offer to apply to all segments with the same original speaker
            var sameSpeakerCount = _currentSegmentViewModels?
                .Count(s => s != vm && s.DisplaySpeaker == currentDisplay) ?? 0;

            if (sameSpeakerCount > 0)
            {
                var result = MessageBox.Show(
                    $"Apply \"{newName}\" to all {sameSpeakerCount + 1} segments currently labelled \"{currentDisplay}\"?",
                    "Apply to all segments",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _selectedTranscript.RenameSpeakerGlobally(vm.Segment.Speaker, newName);

                    if (_currentSegmentViewModels is not null)
                    {
                        foreach (var svm in _currentSegmentViewModels.Where(s => s.Segment.Speaker == vm.Segment.Speaker))
                            svm.RefreshDisplaySpeaker();
                    }

                    Trace.TraceInformation(
                        "[TranscriptsPage] Applied speaker rename to all: '{0}' -> '{1}'",
                        currentDisplay, newName);
                }
            }
        }
        else
        {
            _selectedTranscript.RenameSpeakerGlobally(vm.Segment.Speaker, newName);

            if (_currentSegmentViewModels is not null)
            {
                foreach (var svm in _currentSegmentViewModels.Where(s => s.Segment.Speaker == vm.Segment.Speaker))
                    svm.RefreshDisplaySpeaker();
            }

            vm.CommitEditSpeaker();

            Trace.TraceInformation(
                "[TranscriptsPage] Global speaker rename: '{0}' -> '{1}'",
                vm.Segment.Speaker, newName);
        }

        try
        {
            await _storageService.UpdateAsync(_selectedTranscript);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to save speaker rename: {0}", ex.Message);
        }
    }

    // --- Speaker name list management ---

    private List<SpeakerNameItem> _speakerNameItems = new();

    private void ShowSpeakerNamesPanel()
    {
        if (_selectedTranscript is null)
        {
            SpeakerNamesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _speakerNameItems = (_selectedTranscript.RemoteSpeakerNames ?? new List<string>())
            .Select(n => new SpeakerNameItem { Name = n })
            .ToList();

        SpeakerNamesList.ItemsSource = _speakerNameItems;
        SpeakerNamesPanel.Visibility = Visibility.Visible;
    }

    private void AddSpeaker_Click(object sender, RoutedEventArgs e)
    {
        // Allow adding speakers during active recording, pending drawer, or when viewing a transcript
        if (_selectedTranscript is null && !_isActiveRecordingDrawerOpen && !_isPendingDrawerOpen) return;

        _speakerNameItems.Add(new SpeakerNameItem { Name = "" });
        SpeakerNamesList.ItemsSource = null;
        SpeakerNamesList.ItemsSource = _speakerNameItems;
    }

    private void RemoveSpeaker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SpeakerNameItem item })
            return;

        _speakerNameItems.Remove(item);
        SpeakerNamesList.ItemsSource = null;
        SpeakerNamesList.ItemsSource = _speakerNameItems;

        // Persist to storage if viewing a transcript (not during active recording or pending drawer)
        if (!_isActiveRecordingDrawerOpen && !_isPendingDrawerOpen)
            SaveSpeakerNames();
        else if (_isPendingDrawerOpen)
            SavePendingDrawerMetadata();
    }

    private void SpeakerNameList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && sender is TextBox tb)
        {
            // Force the binding to update, then move focus away to trigger save
            var binding = tb.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
            if (!_isActiveRecordingDrawerOpen && !_isPendingDrawerOpen)
                SaveSpeakerNames();
            else if (_isPendingDrawerOpen)
                SavePendingDrawerMetadata();
            System.Windows.Input.Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            // Dismiss focus without committing
            System.Windows.Input.Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void SpeakerName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_isActiveRecordingDrawerOpen && !_isPendingDrawerOpen)
            SaveSpeakerNames();
        else if (_isPendingDrawerOpen)
            SavePendingDrawerMetadata();
    }

    private async void SaveSpeakerNames()
    {
        if (_selectedTranscript is null) return;

        // Collect renames to propagate to segments
        var renames = _speakerNameItems
            .Where(i => i.PreviousName is not null
                        && !string.IsNullOrWhiteSpace(i.Name)
                        && i.PreviousName != i.Name)
            .Select(i => (OldName: i.PreviousName!, NewName: i.Name!))
            .ToList();

        // Clear previous-name tracking
        foreach (var item in _speakerNameItems)
            item.PreviousName = null;

        _selectedTranscript.RemoteSpeakerNames = _speakerNameItems
            .Select(i => i.Name ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        // Propagate renames: update the speaker name map and refresh segment display names
        foreach (var (oldName, newName) in renames)
        {
            // Find the cluster ID that maps to the old name and update it
            var keysToUpdate = _selectedTranscript.SpeakerNameMap
                .Where(kv => kv.Value == oldName)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keysToUpdate)
                _selectedTranscript.SpeakerNameMap[key] = newName;

            // Also update any per-segment overrides that match the old name
            if (_currentSegmentViewModels is not null)
            {
                foreach (var vm in _currentSegmentViewModels)
                {
                    if (vm.Segment.SpeakerOverride == oldName)
                        vm.Segment.SpeakerOverride = newName;
                }
            }

            Trace.TraceInformation(
                "[TranscriptsPage] Speaker name header rename: '{0}' -> '{1}'",
                oldName, newName);
        }

        try
        {
            await _storageService.UpdateAsync(_selectedTranscript);

            if (_currentSegmentViewModels is not null)
            {
                var names = BuildAvailableSpeakerNames();
                foreach (var vm in _currentSegmentViewModels)
                {
                    vm.AvailableSpeakerNames = names;
                    if (renames.Count > 0)
                        vm.RefreshDisplaySpeaker();
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to save speaker names: {0}", ex.Message);
        }
    }

    private List<string> BuildAvailableSpeakerNames()
    {
        if (_selectedTranscript is null) return new();

        var names = new List<string>();
        if (_selectedTranscript.RemoteSpeakerNames is not null)
            names.AddRange(_selectedTranscript.RemoteSpeakerNames.Where(n => !string.IsNullOrWhiteSpace(n)));

        foreach (var mapped in _selectedTranscript.SpeakerNameMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(mapped) && !names.Contains(mapped))
                names.Add(mapped);
        }

        return names;
    }

    private void ReTranscribe_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        // Capture transcript before CloseDrawer() nulls _selectedTranscript
        var transcript = _selectedTranscript;

        var transcriptDir = transcript.FilePath is not null
            ? Path.GetDirectoryName(transcript.FilePath)
            : null;

        if (transcriptDir is null) return;

        var micPath = Path.Combine(transcriptDir, "mic.wav");
        var systemPath = Path.Combine(transcriptDir, "system.wav");

        // Check if this is an imported single-file session (no mic.wav)
        var hasMicWav = File.Exists(micPath);
        string? importedAudioFile = null;

        if (!hasMicWav)
        {
            // Look for any supported audio file in the session directory
            importedAudioFile = FindImportedAudioFile(transcriptDir);
            if (importedAudioFile is null)
            {
                Trace.TraceWarning("[TranscriptsPage] Cannot re-transcribe: no audio files found in {0}", transcriptDir);
                return;
            }
        }

        // Delete existing transcript
        try
        {
            if (transcript.FilePath is not null && File.Exists(transcript.FilePath))
            {
                File.Delete(transcript.FilePath);
                Trace.TraceInformation("[TranscriptsPage] Deleted existing transcript for re-transcription: {0}",
                    transcript.FilePath);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscriptsPage] Failed to delete transcript for re-transcription: {0}", ex.Message);
        }

        CloseDrawer();

        if (importedAudioFile is not null)
        {
            // Re-transcribe an imported file
            // If multiple speakers are defined, use the call pipeline with diarization
            var speakerNames = (transcript.RemoteSpeakerNames ?? new List<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            if (speakerNames.Count > 1)
            {
                // Feed as system stream for diarization (no mic stream)
                var session = new CallRecordingSession(
                    importedAudioFile, // mic = same file (no separate mic)
                    importedAudioFile, // system = the imported file
                    transcript.RecordingStartedUtc);
                session.EndTimestamp = transcript.RecordingEndedUtc;
                session.RemoteSpeakerNames = speakerNames;
                session.Title = transcript.Name;

                ReTranscriptionRequested?.Invoke(this, session);
            }
            else
            {
                // Single speaker: re-transcribe with flat pipeline
                _queueService.EnqueueFileImport(
                    transcript.Name,
                    importedAudioFile,
                    transcriptDir);
            }
        }
        else
        {
            // Standard call recording re-transcription
            var session = new CallRecordingSession(
                micPath,
                File.Exists(systemPath) ? systemPath : micPath,
                transcript.RecordingStartedUtc);
            session.EndTimestamp = transcript.RecordingEndedUtc;
            session.RemoteSpeakerNames = transcript.RemoteSpeakerNames?.ToList() ?? new List<string>();
            session.Title = transcript.Name;

            ReTranscriptionRequested?.Invoke(this, session);
        }

        LoadTranscriptList();
    }

    /// <summary>
    /// Finds an imported audio file in a session directory (non-wav audio files).
    /// Falls back to the audio file path stored in the transcript if available.
    /// </summary>
    private string? FindImportedAudioFile(string sessionDir)
    {
        // First check if the transcript has a resolved audio path
        if (_selectedTranscript?.ResolvedAudioFilePath is not null)
            return _selectedTranscript.ResolvedAudioFilePath;

        // Look for any supported audio file
        foreach (var file in Directory.GetFiles(sessionDir))
        {
            if (_fileTranscriptionService.IsSupported(file))
                return file;
        }

        return null;
    }

    // --- Export ---

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var markdown = TranscriptMarkdownFormatter.Format(_selectedTranscript);
        System.Windows.Clipboard.SetText(markdown);

        CopiedIndicator.Visibility = Visibility.Visible;
        _copiedIndicatorTimer.Stop();
        _copiedIndicatorTimer.Start();

        Trace.TraceInformation("[TranscriptsPage] Copied transcript (Markdown) to clipboard");
    }

    private void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md",
            FileName = $"transcript_{_selectedTranscript.RecordingStartedUtc:yyyyMMdd_HHmmss}.md",
            Title = "Export Transcript as Markdown"
        };

        if (dialog.ShowDialog() == true)
        {
            var markdown = TranscriptMarkdownFormatter.Format(_selectedTranscript);
            File.WriteAllText(dialog.FileName, markdown);
            Trace.TraceInformation("[TranscriptsPage] Exported Markdown to {0}", dialog.FileName);
        }
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = $"transcript_{_selectedTranscript.RecordingStartedUtc:yyyyMMdd_HHmmss}.json",
            Title = "Export Transcript as JSON"
        };

        if (dialog.ShowDialog() == true)
        {
            var json = FormatAsJson(_selectedTranscript);
            File.WriteAllText(dialog.FileName, json);
            Trace.TraceInformation("[TranscriptsPage] Exported JSON to {0}", dialog.FileName);
        }
    }

    // --- Formatting helpers ---

    private static string FormatAsPlainText(CallTranscript transcript)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Call Transcript - {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration: {transcript.Duration:hh\\:mm\\:ss}");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();

        foreach (var segment in transcript.Segments)
        {
            var speaker = transcript.GetDisplaySpeaker(segment);
            sb.AppendLine($"[{segment.StartTime:hh\\:mm\\:ss}] {speaker}: {segment.Text}");
        }

        return sb.ToString();
    }

    private static string FormatAsJson(CallTranscript transcript)
    {
        var exportData = new
        {
            id = transcript.Id,
            recordingStartedUtc = transcript.RecordingStartedUtc,
            recordingEndedUtc = transcript.RecordingEndedUtc,
            durationSeconds = transcript.Duration.TotalSeconds,
            segments = transcript.Segments.Select(s => new
            {
                speaker = transcript.GetDisplaySpeaker(s),
                originalSpeaker = s.Speaker,
                speakerOverride = s.SpeakerOverride,
                startTime = s.StartTime.TotalSeconds,
                endTime = s.EndTime.TotalSeconds,
                text = s.Text,
                isLocalSpeaker = s.IsLocalSpeaker
            }),
            speakerNameMap = transcript.SpeakerNameMap
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    // --- AI Analysis ---

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTranscript is null) return;

        var templates = _ollamaService.GetTemplates();
        if (templates.Count == 0) return;

        // Build a context menu with available templates
        var menu = new ContextMenu();

        foreach (var template in templates)
        {
            var item = new MenuItem { Header = template.Title, Tag = template };
            item.Click += AnalysisTemplate_Click;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var customItem = new MenuItem { Header = "Custom Prompt..." };
        customItem.Click += CustomAnalysisPrompt_Click;
        menu.Items.Add(customItem);

        if (sender is FrameworkElement fe)
        {
            menu.PlacementTarget = fe;
            menu.IsOpen = true;
        }
    }

    private async void AnalysisTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: AnalysisPromptTemplate template }) return;
        await RunAnalysisAsync(template);
    }

    private async void CustomAnalysisPrompt_Click(object sender, RoutedEventArgs e)
    {
        // Show a simple input dialog for custom prompt
        var dialog = new Window
        {
            Title = "Custom Analysis Prompt",
            Width = 500,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            Background = (Brush)FindResource("ApplicationBackgroundBrush"),
            ResizeMode = ResizeMode.NoResize
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        var label = new TextBlock
        {
            Text = "Enter your analysis prompt. Use {transcript} to insert the transcript text.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = (Brush)FindResource("TextFillColorPrimaryBrush")
        };
        var textBox = new System.Windows.Controls.TextBox
        {
            Height = 150,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Text = "Analyze this transcript and provide insights:\n\n{transcript}"
        };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var okButton = new System.Windows.Controls.Button { Content = "Analyze", Padding = new Thickness(16, 8, 16, 8) };
        var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0) };

        okButton.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        cancelButton.Click += (_, _) => { dialog.DialogResult = false; dialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(label);
        panel.Children.Add(textBox);
        panel.Children.Add(buttonPanel);
        dialog.Content = panel;

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var template = new AnalysisPromptTemplate
            {
                Title = "Custom Analysis",
                Prompt = textBox.Text
            };
            await RunAnalysisAsync(template);
        }
    }

    private async Task RunAnalysisAsync(AnalysisPromptTemplate template)
    {
        if (_selectedTranscript is null) return;

        // Cancel any existing analysis
        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();
        var ct = _analysisCts.Token;

        // Show analysis panel
        AnalysisPanel.Visibility = Visibility.Visible;
        AnalysisTitleText.Text = template.Title;
        AnalysisResultText.Text = "";
        AnalysisStatusText.Text = "Analyzing...";
        AnalysisStatusText.Visibility = Visibility.Visible;
        StopAnalysisButton.Visibility = Visibility.Visible;
        CopyAnalysisButton.Visibility = Visibility.Collapsed;
        _isAnalysisVisible = true;

        var transcriptMarkdown = TranscriptMarkdownFormatter.Format(_selectedTranscript);

        try
        {
            var result = await _ollamaService.AnalyzeAsync(
                template,
                transcriptMarkdown,
                token => Dispatcher.Invoke(() =>
                {
                    AnalysisResultText.Text += token;
                    AnalysisStatusText.Text = "Streaming...";
                    // Auto-scroll to bottom
                    AnalysisScrollViewer.ScrollToEnd();
                }),
                ct);

            if (!ct.IsCancellationRequested)
            {
                AnalysisStatusText.Visibility = Visibility.Collapsed;
                StopAnalysisButton.Visibility = Visibility.Collapsed;
                CopyAnalysisButton.Visibility = Visibility.Visible;
                Trace.TraceInformation("[TranscriptsPage] Analysis complete: {0} ({1} chars)", template.Title, result.Length);
            }
        }
        catch (OperationCanceledException)
        {
            AnalysisStatusText.Text = "Cancelled";
            StopAnalysisButton.Visibility = Visibility.Collapsed;
            Trace.TraceInformation("[TranscriptsPage] Analysis cancelled");
        }
        catch (Exception ex)
        {
            AnalysisStatusText.Text = $"Error: {ex.Message}";
            AnalysisStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FFE74856"));
            StopAnalysisButton.Visibility = Visibility.Collapsed;
            Trace.TraceWarning("[TranscriptsPage] Analysis failed: {0}", ex.Message);
        }
    }

    private void StopAnalysis_Click(object sender, RoutedEventArgs e)
    {
        _analysisCts?.Cancel();
    }

    private void CopyAnalysis_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(AnalysisResultText.Text))
        {
            System.Windows.Clipboard.SetText(AnalysisResultText.Text);
            CopiedIndicator.Visibility = Visibility.Visible;
            _copiedIndicatorTimer.Stop();
            _copiedIndicatorTimer.Start();
        }
    }

    private void CloseAnalysis_Click(object sender, RoutedEventArgs e)
    {
        _analysisCts?.Cancel();
        AnalysisPanel.Visibility = Visibility.Collapsed;
        _isAnalysisVisible = false;
    }
}

/// <summary>
/// View model for a group of transcripts in the list.
/// </summary>
internal sealed class TranscriptGroupViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;

    public TranscriptGroupViewModel(string groupName, List<TranscriptListItem> items, bool isExpanded = true)
    {
        GroupName = groupName;
        Items = items;
        _isExpanded = isExpanded;

        // Pre-compute distinct, sorted remote speaker names across all items in the group
        var speakers = items
            .Where(i => !string.IsNullOrWhiteSpace(i.SpeakersDisplay))
            .SelectMany(i => i.SpeakersDisplay.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        SpeakersSummary = speakers.Count > 0 ? " — " + string.Join(", ", speakers) : "";
    }

    public string GroupName { get; }
    public List<TranscriptListItem> Items { get; }
    public string CountDisplay => $"({Items.Count})";

    /// <summary>Distinct remote speaker names from all items, shown when collapsed.</summary>
    public string SpeakersSummary { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChevronText));
            }
        }
    }

    /// <summary>Down chevron when expanded, right chevron when collapsed.</summary>
    public string ChevronText => IsExpanded ? "\uE70D" : "\uE70E";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for a transcript list item.
/// </summary>
internal sealed class TranscriptListItem
{
    public TranscriptListItem(string filePath, string fileName)
    {
        FilePath = filePath;
        FileName = fileName;

        // Try to parse the date from either format:
        // Legacy: transcript_YYYYMMDD_HHmmss.json → fileName = "transcript_YYYYMMDD_HHmmss"
        // New:    YYYYMMDD_HHmmss/transcript.json → fileName = "transcript", date in parent dir name
        DateTime? date = null;

        if (fileName.Length >= 25 &&
            DateTime.TryParseExact(
                fileName.Substring(11, 15),
                "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var legacyDate))
        {
            date = legacyDate;
        }
        else
        {
            // New format: try parsing the session directory name (parent folder)
            var parentDir = Path.GetFileName(Path.GetDirectoryName(filePath));
            if (parentDir is not null &&
                DateTime.TryParseExact(
                    parentDir,
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var sessionDate))
            {
                date = sessionDate;
            }
        }

        // Read transcript data from the file
        try
        {
            var json = File.ReadAllText(filePath);
            var transcript = JsonSerializer.Deserialize<CallTranscript>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (transcript is not null)
            {
                // If we still don't have a date, use RecordingStartedUtc from the transcript
                date ??= transcript.RecordingStartedUtc.LocalDateTime;

                Name = !string.IsNullOrEmpty(transcript.Name)
                    ? transcript.Name
                    : $"Call {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm}";

                ParsedDuration = transcript.Duration;

                // Build compact duration string: "1h 12m" or "45m" or "0m"
                var totalMinutes = (int)transcript.Duration.TotalMinutes;
                var hours = totalMinutes / 60;
                var minutes = totalMinutes % 60;
                var compactDuration = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";

                // TimeDisplay: "HH:mm \u2013 Xh Ym" using the start time of day
                if (date.HasValue)
                    TimeDisplay = $"{date.Value:HH:mm} \u2013 {compactDuration}";
                else
                    TimeDisplay = compactDuration;

                // SpeakersDisplay: comma-separated remote speaker names
                SpeakersDisplay = transcript.RemoteSpeakerNames is not null
                    ? string.Join(", ", transcript.RemoteSpeakerNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                    : "";

                var firstSegment = transcript.Segments.FirstOrDefault();
                PreviewText = firstSegment?.Text ?? "(empty)";
                if (PreviewText.Length > 50)
                    PreviewText = PreviewText[..50] + "...";
            }
        }
        catch
        {
            TimeDisplay = "";
            SpeakersDisplay = "";
            PreviewText = "(unable to read)";
        }

        // Set date-derived properties
        if (date.HasValue)
        {
            ParsedDate = date.Value;
            GroupKey = date.Value.ToString("yyyy-MM-dd");
            GroupDisplayName = date.Value.ToString("MMMM dd, yyyy").ToUpperInvariant();
        }
        else
        {
            GroupKey = "unknown";
            GroupDisplayName = "OTHER";
        }
    }

    public string FilePath { get; }
    public string FileName { get; }
    public string Name { get; set; } = "";
    public string TimeDisplay { get; } = "";
    public string SpeakersDisplay { get; } = "";
    public string PreviewText { get; } = "";
    public DateTime? ParsedDate { get; }
    public TimeSpan ParsedDuration { get; }
    public string GroupKey { get; }
    public string GroupDisplayName { get; }
}

/// <summary>
/// View model for a single transcript segment in the viewer.
/// Supports inline editing of speaker names with INotifyPropertyChanged.
/// </summary>
internal sealed class SegmentViewModel : INotifyPropertyChanged
{
    private static readonly Brush LocalSpeakerColor = new SolidColorBrush(Color.FromRgb(86, 156, 214));   // Blue
    private static readonly Brush RemoteSpeakerColor = new SolidColorBrush(Color.FromRgb(206, 145, 120)); // Orange
    private static readonly Brush LocalBackground = new SolidColorBrush(Color.FromArgb(20, 86, 156, 214));
    private static readonly Brush RemoteBackground = new SolidColorBrush(Color.FromArgb(20, 206, 145, 120));
    private static readonly Brush PlayingHighlight = new SolidColorBrush(Color.FromArgb(50, 255, 200, 50)); // Yellow highlight

    private readonly CallTranscript _transcript;
    private readonly Brush _defaultBackground;
    private string _displaySpeaker;
    private bool _isEditingSpeaker;
    private string _editingSpeakerName = "";
    private bool _isCurrentlyPlaying;

    static SegmentViewModel()
    {
        LocalSpeakerColor.Freeze();
        RemoteSpeakerColor.Freeze();
        LocalBackground.Freeze();
        RemoteBackground.Freeze();
        PlayingHighlight.Freeze();
    }

    public SegmentViewModel(TranscriptSegment segment, CallTranscript transcript, List<string>? availableSpeakerNames = null)
    {
        Segment = segment;
        _transcript = transcript;
        _displaySpeaker = transcript.GetDisplaySpeaker(segment);
        _availableSpeakerNames = availableSpeakerNames ?? new();
        Text = segment.Text;
        TimestampDisplay = $"{segment.StartTime:hh\\:mm\\:ss}";
        SpeakerColor = segment.IsLocalSpeaker ? LocalSpeakerColor : RemoteSpeakerColor;
        _defaultBackground = segment.IsLocalSpeaker ? LocalBackground : RemoteBackground;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TranscriptSegment Segment { get; }

    public string DisplaySpeaker
    {
        get => _displaySpeaker;
        private set { _displaySpeaker = value; OnPropertyChanged(); }
    }

    public string Text { get; }
    public string TimestampDisplay { get; }
    public Brush SpeakerColor { get; }

    public Brush CurrentBackground => _isCurrentlyPlaying ? PlayingHighlight : _defaultBackground;

    private Cursor _segmentCursor = Cursors.Hand;
    public Cursor SegmentCursor
    {
        get => _segmentCursor;
        set { _segmentCursor = value; OnPropertyChanged(); }
    }

    public bool IsCurrentlyPlaying
    {
        get => _isCurrentlyPlaying;
        set
        {
            if (_isCurrentlyPlaying != value)
            {
                _isCurrentlyPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentBackground));
            }
        }
    }

    public bool IsEditingSpeaker
    {
        get => _isEditingSpeaker;
        private set { _isEditingSpeaker = value; OnPropertyChanged(); }
    }

    public string EditingSpeakerName
    {
        get => _editingSpeakerName;
        set { _editingSpeakerName = value; OnPropertyChanged(); }
    }

    public bool IsPerSegmentEdit { get; set; }

    private List<string> _availableSpeakerNames;

    public List<string> AvailableSpeakerNames
    {
        get => _availableSpeakerNames;
        set { _availableSpeakerNames = value; OnPropertyChanged(); }
    }

    public void BeginEditSpeaker()
    {
        EditingSpeakerName = DisplaySpeaker;
        IsEditingSpeaker = true;
    }

    public void CommitEditSpeaker()
    {
        IsEditingSpeaker = false;
        RefreshDisplaySpeaker();
    }

    public void CancelEditSpeaker()
    {
        IsEditingSpeaker = false;
    }

    public void RefreshDisplaySpeaker()
    {
        DisplaySpeaker = _transcript.GetDisplaySpeaker(Segment);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for a pending recording session (WAV files without transcript).
/// </summary>
internal sealed class PendingRecordingItem
{
    public PendingRecordingItem(string sessionDir, string name, string detail, bool isTranscribing, bool isQueued = false, int failureCount = 0)
    {
        SessionDir = sessionDir;
        Name = name;
        Detail = failureCount > 0
            ? $"Failed {failureCount}x \u2014 {detail}"
            : detail;
        IsTranscribing = isTranscribing;
        IsQueued = isQueued;
        FailureCount = failureCount;
    }

    public string SessionDir { get; }
    public string Name { get; }
    public string Detail { get; }
    public bool IsTranscribing { get; }
    /// <summary>True when item is in the queue but not yet actively transcribing.</summary>
    public bool IsQueued { get; }
    /// <summary>Number of times transcription has failed for this session.</summary>
    public int FailureCount { get; }
    /// <summary>True when transcription has failed at least once.</summary>
    public bool HasFailed => FailureCount > 0;
}

/// <summary>
/// View model for a pending recording that originated on a different machine
/// (cross-machine sync). Shows the originating machine and offers a
/// "Transcribe here" action that takes over the work locally.
/// </summary>
internal sealed class OtherMachinePendingItem
{
    public OtherMachinePendingItem(string sessionDir, string name, string detail, string machineLabel)
    {
        SessionDir = sessionDir;
        Name = name;
        Detail = detail;
        MachineLabel = machineLabel;
    }

    public string SessionDir { get; }
    public string Name { get; }
    public string Detail { get; }
    /// <summary>Originating machine id (and possible takeover indicator).</summary>
    public string MachineLabel { get; }
}

/// <summary>
/// Editable speaker name item for the speaker names list.
/// </summary>
internal sealed class SpeakerNameItem : INotifyPropertyChanged
{
    private string _name = "";

    public string Name
    {
        get => _name;
        set
        {
            PreviousName ??= _name;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>Tracks the name before the most recent edit (set once per focus cycle, cleared after save).</summary>
    public string? PreviousName { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
