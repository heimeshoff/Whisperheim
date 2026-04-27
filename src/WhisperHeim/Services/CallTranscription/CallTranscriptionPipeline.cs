using System.Diagnostics;
using System.IO;
using NAudio.Wave;
using WhisperHeim.Services.Audio;
using WhisperHeim.Services.Diarization;
using WhisperHeim.Services.Models;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Orchestrates the end-to-end call transcription pipeline:
/// 1. Load audio from WAV files
/// 2. Diarize using dual-stream (mic + loopback) for speaker attribution
/// 3. Transcribe each speaker segment with Parakeet TDT
/// 4. Assemble into a structured transcript with timestamps and speaker labels
/// 5. Persist to %APPDATA%/WhisperHeim/transcripts/
///
/// Supports progress reporting and cancellation throughout.
/// </summary>
public sealed class CallTranscriptionPipeline : ICallTranscriptionPipeline
{
    private const int ExpectedSampleRate = 16000;

    /// <summary>
    /// RMS threshold below which a chunk is considered silence.
    /// Typical speech RMS is 0.01–0.10; loopback silence is ~0.0001.
    /// sherpa-onnx crashes (access violation) on near-silent chunks because
    /// the segmentation model produces no speech frames.
    /// </summary>
    private const double SilenceRmsThreshold = 0.001;

    /// <summary>
    /// Weight of each pipeline stage in the overall progress calculation.
    /// Loading=5%, Diarizing=30%, Transcribing=55%, Assembling=5%, Saving=5%.
    /// </summary>
    private static readonly (PipelineStage Stage, double Weight)[] StageWeights =
    [
        (PipelineStage.LoadingAudio, 0.05),
        (PipelineStage.Diarizing, 0.30),
        (PipelineStage.Transcribing, 0.55),
        (PipelineStage.Assembling, 0.05),
        (PipelineStage.Saving, 0.05),
    ];

    private readonly ISpeakerDiarizationService _diarization;
    private readonly ITranscriptionService _transcription;
    private readonly ITranscriptStorageService _storage;

    public CallTranscriptionPipeline(
        ISpeakerDiarizationService diarization,
        ITranscriptionService transcription,
        ITranscriptStorageService storage)
    {
        _diarization = diarization;
        _transcription = transcription;
        _storage = storage;
    }

    /// <inheritdoc />
    public async Task<CallTranscript> ProcessAsync(
        CallRecordingSession session,
        IReadOnlyList<string>? remoteSpeakerNames = null,
        string? localSpeakerName = null,
        string? transcriptName = null,
        IProgress<TranscriptionPipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (!_diarization.IsLoaded)
            _diarization.LoadModels();

        if (!_transcription.IsLoaded)
            throw new InvalidOperationException(
                "Transcription model is not loaded. Call LoadModel() first.");

        if (session.EndTimestamp is null)
            throw new InvalidOperationException(
                "Cannot process a recording session that has not ended.");

        var guid = Guid.NewGuid().ToString("N")[..8];
        var transcriptId = $"{session.StartTimestamp:yyyyMMdd_HHmmss}_{guid}";

        // ── Stage 1 & 2: Diarize each stream by reading chunks from disk ─
        // Never load the full audio into memory. Each chunk is read from the
        // WAV file, diarized with a fresh native diarizer, then discarded.
        ReportProgress(progress, PipelineStage.LoadingAudio, 0, "Analyzing audio files...");

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Mic WAV: {0} (exists={1})",
            session.MicWavFilePath, File.Exists(session.MicWavFilePath));
        Trace.TraceInformation(
            "[CallTranscriptionPipeline] System WAV: {0} (exists={1})",
            session.SystemWavFilePath, File.Exists(session.SystemWavFilePath));

        bool micExists = File.Exists(session.MicWavFilePath);
        bool systemExists = File.Exists(session.SystemWavFilePath) &&
            !string.Equals(session.MicWavFilePath, session.SystemWavFilePath, StringComparison.OrdinalIgnoreCase);

        // Validate and attempt repair of WAV files before processing.
        // Mic failure is fatal (nothing to transcribe). System failure is not —
        // an empty/corrupt loopback file (e.g. nothing captured during the call)
        // shouldn't block transcribing the mic side of the conversation.
        if (micExists)
            ValidateAndRepairWav(session.MicWavFilePath, "mic");
        if (systemExists)
        {
            try
            {
                ValidateAndRepairWav(session.SystemWavFilePath, "system");
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] System WAV unusable, continuing with mic-only: {0}",
                    ex.Message);
                systemExists = false;
            }
        }

        double micDurationSeconds = micExists ? GetWavDuration(session.MicWavFilePath) : 0;
        double systemDurationSeconds = systemExists ? GetWavDuration(session.SystemWavFilePath) : 0;

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Audio durations: mic={0:F1}s, system={1:F1}s",
            micDurationSeconds, systemDurationSeconds);

        // Resolve local speaker label
        var localSpeakerLabel = string.IsNullOrWhiteSpace(localSpeakerName) ? "You" : localSpeakerName;

        // Determine numSpeakers for loopback from remote speaker name list
        int loopbackNumSpeakers = remoteSpeakerNames is { Count: > 0 }
            ? remoteSpeakerNames.Count
            : -1; // auto-detect

        // -- Mic stream: VAD-only, never diarize (single known speaker) --
        DiarizationResult? micDiarization = null;
        if (micExists && micDurationSeconds > 0.5)
        {
            ReportProgress(progress, PipelineStage.Diarizing, 0, "Processing microphone (VAD speech detection)...");

            // The mic always has exactly one speaker (the local user).
            // Use Silero VAD to detect actual speech boundaries instead of
            // fixed-length chunks or expensive diarization.
            var micSegments = await Task.Run(
                () => DetectSpeechSegmentsWithVad(session.MicWavFilePath, micDurationSeconds),
                cancellationToken);

            micDiarization = new DiarizationResult(
                micSegments, SpeakerCount: 1,
                AudioDuration: TimeSpan.FromSeconds(micDurationSeconds),
                ProcessingDuration: TimeSpan.Zero);

            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Mic: VAD detected {0} speech segments ({1:F1}s total audio)",
                micSegments.Count, micDurationSeconds);
        }

        // -- Diarize system stream --
        // When there's only one remote speaker (or none), diarization is unnecessary
        // and error-prone — use lightweight VAD instead, just like the mic stream.
        // Only run full diarization when multiple remote speakers need distinguishing.
        DiarizationResult? systemDiarization = null;
        string? diarizationWarning = null;
        if (systemExists && systemDurationSeconds > 0.5)
        {
            if (loopbackNumSpeakers > 1)
            {
                ReportProgress(progress, PipelineStage.Diarizing, 50, "Diarizing system audio...");

                var (sysResult, skippedChunks, totalChunks) = await DiarizeFromFileAsync(
                    session.SystemWavFilePath, numSpeakers: loopbackNumSpeakers,
                    dp =>
                    {
                        ReportProgress(progress, PipelineStage.Diarizing, 50 + dp.Percent * 0.5,
                            $"Diarizing system — chunk {dp.ProcessedChunks}/{dp.TotalChunks}");
                    },
                    cancellationToken);
                systemDiarization = sysResult;

                if (skippedChunks > 0)
                {
                    // Each chunk step covers ~110s (120s chunk - 10s overlap)
                    int skippedMinutes = skippedChunks * 110 / 60;
                    diarizationWarning =
                        $"{skippedChunks}/{totalChunks} diarization chunks failed — " +
                        $"~{skippedMinutes}min of system audio may be missing from the transcript.";
                    Trace.TraceWarning(
                        "[CallTranscriptionPipeline] {0}", diarizationWarning);
                }

                Trace.TraceInformation(
                    "[CallTranscriptionPipeline] System diarization: {0} segments",
                    systemDiarization.Segments.Count);
            }
            else
            {
                // Single remote speaker (or auto-detect): use VAD like the mic stream
                ReportProgress(progress, PipelineStage.Diarizing, 50, "Processing system audio (VAD speech detection)...");

                var sysSegments = await Task.Run(
                    () => DetectSpeechSegmentsWithVad(session.SystemWavFilePath, systemDurationSeconds),
                    cancellationToken);

                systemDiarization = new DiarizationResult(
                    sysSegments, SpeakerCount: 1,
                    AudioDuration: TimeSpan.FromSeconds(systemDurationSeconds),
                    ProcessingDuration: TimeSpan.Zero);

                Trace.TraceInformation(
                    "[CallTranscriptionPipeline] System: VAD detected {0} speech segments ({1:F1}s total audio)",
                    sysSegments.Count, systemDurationSeconds);
            }
        }

        // -- Build attributed segments --
        var diarizedSegments = new List<AttributedDiarizationSegment>();

        if (micDiarization is not null)
        {
            foreach (var seg in micDiarization.Segments)
                diarizedSegments.Add(new AttributedDiarizationSegment(
                    SpeakerId: 0, seg.StartTime, seg.EndTime, SpeakerSource.Microphone));
        }

        if (systemDiarization is not null)
        {
            foreach (var seg in systemDiarization.Segments)
                diarizedSegments.Add(new AttributedDiarizationSegment(
                    SpeakerId: seg.SpeakerId + 1, seg.StartTime, seg.EndTime, SpeakerSource.Loopback));
        }

        // ── Clock drift correction ────────────────────────────────────
        // WASAPI mic and loopback use different hardware clocks (ADC vs DAC)
        // that drift ~0.1% (1ms/s). For a 30-min recording this is ~1.8s,
        // enough to reorder segments. Fix: scale loopback timestamps so both
        // streams share the mic's time base.
        if (micExists && systemExists && micDurationSeconds > 0.5 && systemDurationSeconds > 0.5)
        {
            double driftCorrection = micDurationSeconds / systemDurationSeconds;
            double driftSeconds = Math.Abs(micDurationSeconds - systemDurationSeconds);

            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Clock drift correction: factor={0:F6}, " +
                "drift={1:F3}s over {2:F1}s recording (mic={3:F3}s, loopback={4:F3}s)",
                driftCorrection, driftSeconds, micDurationSeconds,
                micDurationSeconds, systemDurationSeconds);

            // Scale loopback segment timestamps to mic's time base
            for (int i = 0; i < diarizedSegments.Count; i++)
            {
                var seg = diarizedSegments[i];
                if (seg.Source == SpeakerSource.Loopback)
                {
                    var correctedStart = TimeSpan.FromSeconds(seg.StartTime.TotalSeconds * driftCorrection);
                    var correctedEnd = TimeSpan.FromSeconds(seg.EndTime.TotalSeconds * driftCorrection);
                    diarizedSegments[i] = seg with { StartTime = correctedStart, EndTime = correctedEnd };
                }
            }
        }

        // Fallback: if neither stream had data, nothing to do
        if (diarizedSegments.Count == 0 && micDiarization is null && systemDiarization is null)
        {
            Trace.TraceWarning("[CallTranscriptionPipeline] No audio data to process.");
        }

        diarizedSegments.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        ReportProgress(progress, PipelineStage.Diarizing, 100,
            diarizationWarning is not null
                ? $"Diarization complete: {diarizedSegments.Count} segments found. WARNING: {diarizationWarning}"
                : $"Diarization complete: {diarizedSegments.Count} segments found.",
            warningMessage: diarizationWarning);

        for (int i = 0; i < diarizedSegments.Count; i++)
        {
            var ds = diarizedSegments[i];
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Diarization segment {0}: speaker={1}, source={2}, " +
                "start={3:F2}s, end={4:F2}s, duration={5:F2}s",
                i, ds.SpeakerId, ds.Source,
                ds.StartTime.TotalSeconds, ds.EndTime.TotalSeconds,
                (ds.EndTime - ds.StartTime).TotalSeconds);
        }

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Diarization complete: {0} segments",
            diarizedSegments.Count);

        // ── Stage 3: Transcribe each segment ────────────────────────────
        // Extract audio segments directly from WAV files on disk to avoid
        // holding the full audio in memory.
        ReportProgress(progress, PipelineStage.Transcribing, 0, "Transcribing segments...");

        var transcriptSegments = new List<TranscriptSegment>();
        int totalSegments = diarizedSegments.Count;

        for (int i = 0; i < totalSegments; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var seg = diarizedSegments[i];
            double segPercent = totalSegments > 0
                ? (double)i / totalSegments * 100.0
                : 0;

            var speakerLabel = GetSpeakerLabel(seg, localSpeakerLabel, remoteSpeakerNames);
            ReportProgress(progress, PipelineStage.Transcribing, segPercent,
                $"Transcribing segment {i + 1}/{totalSegments} ({speakerLabel})...");

            // Read only the segment's time range from the WAV file
            var wavPath = seg.Source == SpeakerSource.Microphone
                ? session.MicWavFilePath
                : session.SystemWavFilePath;

            float[] segmentSamples = await Task.Run(
                () => LoadWavSegment(wavPath, seg.StartTime, seg.EndTime), cancellationToken);

            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Segment {0}: {1} samples ({2:F2}s) from {3}",
                i, segmentSamples.Length,
                (double)segmentSamples.Length / ExpectedSampleRate,
                seg.Source);

            if (segmentSamples.Length == 0)
                continue;

            var result = await _transcription.TranscribeAsync(
                segmentSamples, ExpectedSampleRate, cancellationToken);

            var text = result.Text.Trim();
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Segment {0} transcribed: \"{1}\"", i, text);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            transcriptSegments.Add(new TranscriptSegment
            {
                Speaker = speakerLabel,
                StartTime = seg.StartTime,
                EndTime = seg.EndTime,
                Text = text,
                IsLocalSpeaker = seg.Source == SpeakerSource.Microphone,
            });
        }

        ReportProgress(progress, PipelineStage.Transcribing, 100,
            $"Transcription complete: {transcriptSegments.Count} segments with text.");

        // ── Stage 4: Assemble ───────────────────────────────────────────
        ReportProgress(progress, PipelineStage.Assembling, 0, "Assembling transcript...");

        // Merge consecutive segments from the same speaker
        var mergedSegments = MergeConsecutiveSpeakerSegments(transcriptSegments);

        var transcript = new CallTranscript
        {
            Id = transcriptId,
            Name = !string.IsNullOrWhiteSpace(transcriptName)
                ? transcriptName
                : $"Call {session.StartTimestamp.LocalDateTime:yyyy-MM-dd HH:mm}",
            RecordingStartedUtc = session.StartTimestamp,
            RecordingEndedUtc = session.EndTimestamp.Value,
            Segments = mergedSegments,
            RemoteSpeakerNames = remoteSpeakerNames?.ToList() ?? new List<string>(),
        };

        ReportProgress(progress, PipelineStage.Assembling, 100, "Transcript assembled.");

        // ── Stage 5: Save ───────────────────────────────────────────────
        ReportProgress(progress, PipelineStage.Saving, 0, "Saving transcript...");

        // Save the transcript first to get the file path, then preserve audio alongside it
        var filePath = await _storage.SaveAsync(transcript, cancellationToken);

        // Audio is played back by mixing mic.wav + system.wav in real-time — no merged file needed

        ReportProgress(progress, PipelineStage.Saving, 100, $"Transcript saved to {filePath}");

        sw.Stop();

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Pipeline complete in {0:F1}s — " +
            "{1} segments, saved to {2}",
            sw.Elapsed.TotalSeconds, transcript.Segments.Count, filePath);

        ReportProgress(progress, PipelineStage.Completed, 100, "Processing complete.");

        return transcript;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a WAV file and returns its samples as 16kHz mono float32.
    /// </summary>
    private static float[] LoadWavSamples(string wavFilePath)
    {
        if (!File.Exists(wavFilePath))
        {
            Trace.TraceWarning(
                "[CallTranscriptionPipeline] WAV file not found: {0}", wavFilePath);
            return [];
        }

        using var reader = new AudioFileReader(wavFilePath);

        // Estimate output sample count to pre-allocate (avoids List doubling OOM on long recordings)
        long estimatedSamples = (long)(reader.TotalTime.TotalSeconds * ExpectedSampleRate) + ExpectedSampleRate;

        // If the file is already 16kHz mono float, read directly
        // Otherwise NAudio's AudioFileReader handles conversion to float
        var sampleProvider = reader.ToSampleProvider();

        // Resample if needed
        if (reader.WaveFormat.SampleRate != ExpectedSampleRate ||
            reader.WaveFormat.Channels != 1)
        {
            // Use WdlResamplingSampleProvider for high-quality resampling
            var mono = reader.WaveFormat.Channels > 1
                ? new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider)
                : sampleProvider;

            var resampler = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(
                (ISampleProvider)mono, ExpectedSampleRate);

            return ReadAllSamples(resampler, estimatedSamples);
        }

        return ReadAllSamples(sampleProvider, estimatedSamples);
    }

    /// <summary>
    /// Reads all samples from a sample provider into a float array.
    /// Pre-allocates based on expected duration to avoid repeated List doubling.
    /// </summary>
    private static float[] ReadAllSamples(ISampleProvider provider, long estimatedSampleCount = 0)
    {
        var buffer = new float[ExpectedSampleRate]; // 1 second read buffer

        // Pre-allocate to avoid repeated array doubling for long recordings
        int capacity = estimatedSampleCount > 0
            ? (int)Math.Min(estimatedSampleCount, int.MaxValue)
            : ExpectedSampleRate * 60; // default 1 minute
        var allSamples = new List<float>(capacity);

        int samplesRead;
        while ((samplesRead = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            allSamples.AddRange(buffer.AsSpan(0, samplesRead).ToArray());
        }

        return allSamples.ToArray();
    }

    /// <summary>
    /// Runs diarization for a chunk in a separate child process.
    /// If the child crashes (native access violation), returns null instead of killing the app.
    /// </summary>
    private static async Task<DiarizationResult?> DiarizeChunkOutOfProcessAsync(
        float[] samples,
        int numSpeakers,
        CancellationToken cancellationToken,
        float? threshold = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"wh_diarize_{Guid.NewGuid():N}.raw");

        try
        {
            // Write raw float bytes to temp file
            var bytes = new byte[samples.Length * 4];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            await File.WriteAllBytesAsync(tempFile, bytes, cancellationToken);

            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot determine process path.");

            var segModel = Models.ModelManagerService.PyannoteSegmentationModelPath;
            var embModel = Models.ModelManagerService.SpeakerEmbeddingModelPath;

            using var proc = new System.Diagnostics.Process();
            var thresholdArg = threshold.HasValue
                ? $" --threshold {threshold.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : "";

            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--diarize-worker --samples \"{tempFile}\" " +
                            $"--segmentation \"{segModel}\" " +
                            $"--embedding \"{embModel}\" " +
                            $"--num-speakers {numSpeakers}{thresholdArg}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            proc.Start();

            // Kill the child process immediately if the user cancels
            using var cancelReg = cancellationToken.Register(() =>
            {
                Trace.TraceInformation("[CallTranscriptionPipeline] Cancel requested — killing diarization child process.");
                try { proc.Kill(entireProcessTree: true); } catch { }
            });

            // Timeout: 2 minutes per chunk, linked to user cancel token
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(2));

            // Read stdout/stderr with the linked token so they cancel too
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = proc.StandardError.ReadToEndAsync(timeoutCts.Token);

            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Kill the child on timeout or user cancel
                try { proc.Kill(entireProcessTree: true); } catch { }

                if (cancellationToken.IsCancellationRequested)
                {
                    Trace.TraceInformation("[CallTranscriptionPipeline] Diarization cancelled by user.");
                    throw;
                }

                Trace.TraceWarning("[CallTranscriptionPipeline] Diarization child process timed out.");
                return null;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] Diarization child process exited with code {0}. stderr: {1}",
                    proc.ExitCode, stderr.Length > 500 ? stderr[..500] : stderr);
                return null;
            }

            // Parse JSON output
            var dtos = System.Text.Json.JsonSerializer.Deserialize<
                Diarization.DiarizationWorker.DiarizationSegmentDto[]>(stdout);

            if (dtos is null)
                return new DiarizationResult([], 0, TimeSpan.Zero, TimeSpan.Zero);

            var segments = new List<Diarization.DiarizationSegment>();
            var speakerIds = new HashSet<int>();

            foreach (var dto in dtos)
            {
                var start = TimeSpan.FromSeconds(dto.Start);
                var end = TimeSpan.FromSeconds(dto.End);
                if (end <= start) continue;
                segments.Add(new Diarization.DiarizationSegment(dto.Speaker, start, end));
                speakerIds.Add(dto.Speaker);
            }

            return new DiarizationResult(
                segments, speakerIds.Count,
                TimeSpan.FromSeconds((double)samples.Length / ExpectedSampleRate),
                TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceError(
                "[CallTranscriptionPipeline] Out-of-process diarization error: {0}", ex.Message);
            return null;
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// Computes the root-mean-square (RMS) energy of an audio buffer.
    /// Used to detect silence before sending to native diarization.
    /// </summary>
    private static double ComputeRms(float[] samples)
    {
        if (samples.Length == 0)
            return 0.0;

        double sumSquares = 0;
        for (int i = 0; i < samples.Length; i++)
            sumSquares += (double)samples[i] * samples[i];

        return Math.Sqrt(sumSquares / samples.Length);
    }

    /// <summary>
    /// Retries a failed diarization chunk by splitting it into two halves.
    /// Smaller chunks may avoid native crashes triggered by specific audio patterns.
    /// Returns a merged result, or null if both halves fail.
    /// </summary>
    private static async Task<DiarizationResult?> RetryChunkAsHalvesAsync(
        float[] samples,
        int numSpeakers,
        int chunkLabel,
        int totalChunks,
        double parentOffsetSec,
        CancellationToken cancellationToken)
    {
        int mid = samples.Length / 2;
        var firstHalf = samples[..mid];
        var secondHalf = samples[mid..];
        var segments = new List<DiarizationSegment>();

        // Try first half
        if (ComputeRms(firstHalf) >= SilenceRmsThreshold)
        {
            var r1 = await DiarizeChunkOutOfProcessAsync(firstHalf, numSpeakers, cancellationToken);
            if (r1 is not null)
            {
                segments.AddRange(r1.Segments);
                Trace.TraceInformation(
                    "[CallTranscriptionPipeline] Chunk {0}/{1} first half succeeded — {2} segments.",
                    chunkLabel, totalChunks, r1.Segments.Count);
            }
            else
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] Chunk {0}/{1} first half also failed.",
                    chunkLabel, totalChunks);
            }
        }

        // Try second half (offset segments by half-chunk duration)
        if (ComputeRms(secondHalf) >= SilenceRmsThreshold)
        {
            var r2 = await DiarizeChunkOutOfProcessAsync(secondHalf, numSpeakers, cancellationToken);
            if (r2 is not null)
            {
                double halfOffsetSec = (double)mid / ExpectedSampleRate;
                foreach (var seg in r2.Segments)
                {
                    segments.Add(new DiarizationSegment(
                        seg.SpeakerId,
                        seg.StartTime + TimeSpan.FromSeconds(halfOffsetSec),
                        seg.EndTime + TimeSpan.FromSeconds(halfOffsetSec)));
                }

                Trace.TraceInformation(
                    "[CallTranscriptionPipeline] Chunk {0}/{1} second half succeeded — {2} segments.",
                    chunkLabel, totalChunks, r2.Segments.Count);
            }
            else
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] Chunk {0}/{1} second half also failed.",
                    chunkLabel, totalChunks);
            }
        }

        if (segments.Count == 0)
            return null;

        var speakerIds = new HashSet<int>(segments.Select(s => s.SpeakerId));
        return new DiarizationResult(
            segments, speakerIds.Count,
            TimeSpan.FromSeconds((double)samples.Length / ExpectedSampleRate),
            TimeSpan.Zero);
    }

    /// <summary>
    /// Validates a WAV file and attempts repair if the header is corrupted.
    /// Throws if the file is invalid and cannot be repaired.
    /// </summary>
    private static void ValidateAndRepairWav(string wavFilePath, string streamLabel)
    {
        var error = WavFileValidator.Validate(wavFilePath);
        if (error is null)
            return; // file is valid

        Trace.TraceWarning(
            "[CallTranscriptionPipeline] {0} WAV validation failed: {1}. Attempting repair...",
            streamLabel, error);

        if (WavFileValidator.TryRepair(wavFilePath, out var repairError))
        {
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] {0} WAV repaired successfully.", streamLabel);
        }
        else
        {
            throw new InvalidOperationException(
                $"WAV file '{streamLabel}' is corrupted and could not be repaired: {repairError ?? error}");
        }
    }

    /// <summary>
    /// Gets the total duration of a WAV file without loading it into memory.
    /// </summary>
    private static double GetWavDuration(string wavFilePath)
    {
        using var reader = new AudioFileReader(wavFilePath);
        return reader.TotalTime.TotalSeconds;
    }

    /// <summary>
    /// Diarizes a WAV file by reading it in chunks from disk.
    /// Never holds more than one chunk (~120s) in memory at a time.
    /// Each chunk gets a fresh native diarizer to prevent memory accumulation.
    /// </summary>
    private async Task<(DiarizationResult Result, int SkippedChunks, int TotalChunks)> DiarizeFromFileAsync(
        string wavFilePath,
        int numSpeakers,
        Action<DiarizationProgress> onProgress,
        CancellationToken cancellationToken)
    {
        const int chunkSeconds = 120;
        const int overlapSeconds = 10;

        double totalSeconds = GetWavDuration(wavFilePath);

        // For short files (under 2 minutes), load and diarize in one chunk
        if (totalSeconds <= 120)
        {
            var samples = await Task.Run(() => LoadWavSamples(wavFilePath), cancellationToken);
            var result = await DiarizeChunkOutOfProcessAsync(samples, numSpeakers, cancellationToken);
            var singleChunkResult = result ?? new DiarizationResult([], 0, TimeSpan.FromSeconds(totalSeconds), TimeSpan.Zero);
            return (singleChunkResult, SkippedChunks: result is null ? 1 : 0, TotalChunks: 1);
        }

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Diarizing {0:F0}s from file in {1}s chunks (numSpeakers={2})",
            totalSeconds, chunkSeconds, numSpeakers);

        var sw = Stopwatch.StartNew();
        var chunkResults = new List<(double OffsetSeconds, DiarizationResult Result)>();

        int stepSeconds = chunkSeconds - overlapSeconds;
        int totalChunks = (int)Math.Ceiling((totalSeconds - overlapSeconds) / stepSeconds);
        int chunkIndex = 0;
        int skippedChunks = 0;

        for (double offsetSec = 0; offsetSec < totalSeconds; offsetSec += stepSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double lengthSec = Math.Min(chunkSeconds, totalSeconds - offsetSec);
            if (lengthSec < 1.0)
                break;

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Diarizing chunk {0}/{1} (offset={2:F1}s, length={3:F1}s) " +
                "[memory: managed={4:F0}MB, working={5:F0}MB, private={6:F0}MB]",
                chunkIndex + 1, totalChunks, offsetSec, lengthSec,
                GC.GetTotalMemory(false) / 1048576.0,
                proc.WorkingSet64 / 1048576.0,
                proc.PrivateMemorySize64 / 1048576.0);

            // Read just this chunk from the WAV file
            var chunkSamples = await Task.Run(
                () => LoadWavSegment(wavFilePath, TimeSpan.FromSeconds(offsetSec),
                    TimeSpan.FromSeconds(offsetSec + lengthSec)),
                cancellationToken);

            if (chunkSamples.Length < ExpectedSampleRate)
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] Chunk {0}/{1} (offset={2:F1}s) too short ({3} samples) — skipping.",
                    chunkIndex + 1, totalChunks, offsetSec, chunkSamples.Length);
                skippedChunks++;
                chunkIndex++;
                continue;
            }

            // Skip chunks that are mostly silence — sherpa-onnx's native diarization
            // crashes (access violation) on low-energy audio because the segmentation
            // model produces no speech frames, leading to empty embeddings and a null
            // pointer during clustering.
            double rms = ComputeRms(chunkSamples);
            if (rms < SilenceRmsThreshold)
            {
                Trace.TraceInformation(
                    "[CallTranscriptionPipeline] Chunk {0}/{1} (offset={2:F1}s) is silence (RMS={3:F6}) — skipping diarization.",
                    chunkIndex + 1, totalChunks, offsetSec, rms);
                chunkIndex++;
                continue;
            }

            // Run diarization in a child process so native crashes (access violations
            // in sherpa-onnx) don't kill the main application. If the child crashes,
            // retry once with two half-size sub-chunks before giving up.
            var chunkResult = await DiarizeChunkOutOfProcessAsync(
                chunkSamples, numSpeakers, cancellationToken);

            if (chunkResult is null)
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] Diarization failed for chunk {0}/{1} " +
                    "(offset={2:F1}s, RMS={3:F4}) — retrying as two half-chunks.",
                    chunkIndex + 1, totalChunks, offsetSec, rms);

                chunkResult = await RetryChunkAsHalvesAsync(
                    chunkSamples, numSpeakers, chunkIndex + 1, totalChunks, offsetSec,
                    cancellationToken);
            }

            if (chunkResult is null)
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] Diarization failed for chunk {0}/{1} " +
                    "(offset={2:F1}s) after retry — skipping.",
                    chunkIndex + 1, totalChunks, offsetSec);
                skippedChunks++;
                chunkIndex++;
                continue;
            }

            // Release chunk and nudge GC to reclaim managed memory between chunks
            chunkSamples = null;
            GC.Collect(0, GCCollectionMode.Default, blocking: false);

            proc.Refresh();
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Chunk {0}/{1} done — {2} segments " +
                "[memory: managed={3:F0}MB, working={4:F0}MB, private={5:F0}MB]",
                chunkIndex + 1, totalChunks, chunkResult.Segments.Count,
                GC.GetTotalMemory(false) / 1048576.0,
                proc.WorkingSet64 / 1048576.0,
                proc.PrivateMemorySize64 / 1048576.0);

            // Report progress
            onProgress(new DiarizationProgress
            {
                ProcessedChunks = chunkIndex + 1,
                TotalChunks = totalChunks,
            });

            // Filter segments to the non-overlapping zone of this chunk
            double keepFrom = offsetSec == 0 ? 0 : overlapSeconds / 2.0;
            double keepTo = (offsetSec + stepSeconds >= totalSeconds)
                ? double.MaxValue
                : (double)stepSeconds + overlapSeconds / 2.0;

            var filteredSegments = new List<DiarizationSegment>();
            var filteredSpeakerIds = new HashSet<int>();

            foreach (var seg in chunkResult.Segments)
            {
                double midpoint = (seg.StartTime.TotalSeconds + seg.EndTime.TotalSeconds) / 2.0;
                if (midpoint >= keepFrom && midpoint < keepTo)
                {
                    filteredSegments.Add(seg);
                    filteredSpeakerIds.Add(seg.SpeakerId);
                }
            }

            chunkResults.Add((offsetSec, new DiarizationResult(
                filteredSegments, filteredSpeakerIds.Count,
                chunkResult.AudioDuration, chunkResult.ProcessingDuration)));

            chunkIndex++;
        }

        sw.Stop();

        // Use cross-chunk speaker consistency remapping for multi-speaker audio
        var allSegments = numSpeakers > 1
            ? RemapSpeakerIdsAcrossChunks(chunkResults, numSpeakers)
            : chunkResults.SelectMany(cr =>
                cr.Result.Segments.Select(seg => new DiarizationSegment(
                    seg.SpeakerId,
                    seg.StartTime + TimeSpan.FromSeconds(cr.OffsetSeconds),
                    seg.EndTime + TimeSpan.FromSeconds(cr.OffsetSeconds)))).ToList();

        allSegments.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        var speakerIds = new HashSet<int>(allSegments.Select(s => s.SpeakerId));

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] File diarization complete: {0:F1}s in {1:F0}ms — {2} speakers, {3} segments ({4} chunks, {5} skipped)",
            totalSeconds, sw.Elapsed.TotalMilliseconds, speakerIds.Count, allSegments.Count, chunkIndex, skippedChunks);

        return (new DiarizationResult(
            allSegments, speakerIds.Count,
            TimeSpan.FromSeconds(totalSeconds), sw.Elapsed), skippedChunks, totalChunks);
    }

    /// <summary>
    /// Loads a time range from a WAV file, resampling to 16kHz mono.
    /// Only reads the necessary portion from disk — does not load the full file.
    /// </summary>
    private static float[] LoadWavSegment(string wavFilePath, TimeSpan startTime, TimeSpan endTime)
    {
        if (!File.Exists(wavFilePath))
            return [];

        using var reader = new AudioFileReader(wavFilePath);

        // Seek to start position (AudioFileReader works in bytes for the underlying stream)
        if (startTime > TimeSpan.Zero && startTime < reader.TotalTime)
        {
            reader.CurrentTime = startTime;
        }

        var sampleProvider = reader.ToSampleProvider();

        // Resample/downmix if needed
        ISampleProvider provider;
        if (reader.WaveFormat.SampleRate != ExpectedSampleRate || reader.WaveFormat.Channels != 1)
        {
            var mono = reader.WaveFormat.Channels > 1
                ? new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider)
                : sampleProvider;
            provider = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(
                (ISampleProvider)mono, ExpectedSampleRate);
        }
        else
        {
            provider = sampleProvider;
        }

        var duration = endTime - startTime;
        int expectedSamples = (int)(duration.TotalSeconds * ExpectedSampleRate) + ExpectedSampleRate;
        var result = new List<float>(expectedSamples);
        var buffer = new float[ExpectedSampleRate]; // 1 second read buffer
        int maxSamples = (int)(duration.TotalSeconds * ExpectedSampleRate) + 1;

        int totalRead = 0;
        int samplesRead;
        while (totalRead < maxSamples &&
               (samplesRead = provider.Read(buffer, 0, Math.Min(buffer.Length, maxSamples - totalRead))) > 0)
        {
            result.AddRange(buffer.AsSpan(0, samplesRead).ToArray());
            totalRead += samplesRead;
        }

        return result.ToArray();
    }

    /// <summary>
    /// Generates a human-readable speaker label from an attributed diarization segment.
    /// Mic source uses the configured local speaker name (default "You").
    /// Loopback source uses remote speaker names if available, otherwise "Speaker N".
    /// </summary>
    private static string GetSpeakerLabel(
        AttributedDiarizationSegment segment,
        string localSpeakerLabel,
        IReadOnlyList<string>? remoteSpeakerNames)
    {
        if (segment.Source == SpeakerSource.Microphone)
            return localSpeakerLabel;

        // Loopback speakers: SpeakerId is 1-based (0 is reserved for mic).
        // Map to remote speaker names list (0-indexed).
        int remoteIndex = segment.SpeakerId - 1;
        if (remoteSpeakerNames is { Count: > 0 } && remoteIndex >= 0 && remoteIndex < remoteSpeakerNames.Count)
        {
            var name = remoteSpeakerNames[remoteIndex];
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return segment.SpeakerId == 1
            ? "Other"
            : $"Speaker {segment.SpeakerId}";
    }

    /// <summary>
    /// Merges consecutive transcript segments from the same speaker into a single segment.
    /// This produces a cleaner transcript by combining fragments.
    /// </summary>
    private static IReadOnlyList<TranscriptSegment> MergeConsecutiveSpeakerSegments(
        List<TranscriptSegment> segments)
    {
        if (segments.Count == 0)
            return [];

        var merged = new List<TranscriptSegment>();
        var current = segments[0];

        for (int i = 1; i < segments.Count; i++)
        {
            var next = segments[i];

            // Merge if same speaker and gap is less than 2 seconds
            if (next.Speaker == current.Speaker &&
                (next.StartTime - current.EndTime).TotalSeconds < 2.0)
            {
                current = new TranscriptSegment
                {
                    Speaker = current.Speaker,
                    StartTime = current.StartTime,
                    EndTime = next.EndTime,
                    Text = current.Text + " " + next.Text,
                    IsLocalSpeaker = current.IsLocalSpeaker,
                };
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }

    /// <summary>
    /// Uses Silero VAD to detect speech boundaries in a WAV file.
    /// Returns segments attributed to speaker 0 (the local user).
    /// This avoids expensive diarization for the mic stream which always
    /// has a single known speaker.
    /// </summary>
    private static IReadOnlyList<DiarizationSegment> DetectSpeechSegmentsWithVad(
        string wavFilePath, double totalDurationSeconds)
    {
        var segments = new List<DiarizationSegment>();

        var vadModelPath = ModelManagerService.SileroVadModelPath;
        if (!File.Exists(vadModelPath))
        {
            Trace.TraceWarning(
                "[CallTranscriptionPipeline] Silero VAD model not found at {0}, falling back to single segment.",
                vadModelPath);
            // Fallback: return the entire audio as one segment
            segments.Add(new DiarizationSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(totalDurationSeconds)));
            return segments;
        }

        var settings = new VadSettings
        {
            MinSpeechDurationMs = 300,
            MinSilenceDurationMs = 600,
        };

        // Use the sherpa-onnx VAD directly for offline batch processing
        // to collect completed speech segments with their timestamps
        var vadConfig = new SherpaOnnx.VadModelConfig();
        vadConfig.SileroVad.Model = vadModelPath;
        vadConfig.SileroVad.Threshold = settings.SpeechThreshold;
        vadConfig.SileroVad.MinSilenceDuration = settings.MinSilenceDurationMs / 1000f;
        vadConfig.SileroVad.MinSpeechDuration = settings.MinSpeechDurationMs / 1000f;
        vadConfig.SileroVad.MaxSpeechDuration = 120f; // Cap at 2 minutes per segment
        vadConfig.SileroVad.WindowSize = settings.ChunkSamples;
        vadConfig.SampleRate = ExpectedSampleRate;
        vadConfig.NumThreads = 1;

        using var vadDetector = new SherpaOnnx.VoiceActivityDetector(vadConfig, bufferSizeInSeconds: 60);

        // Read audio in chunks to avoid loading entire file
        using var reader = new AudioFileReader(wavFilePath);
        var sampleProvider = reader.ToSampleProvider();

        ISampleProvider provider;
        if (reader.WaveFormat.SampleRate != ExpectedSampleRate || reader.WaveFormat.Channels != 1)
        {
            var mono = reader.WaveFormat.Channels > 1
                ? new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider)
                : sampleProvider;
            provider = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(
                (ISampleProvider)mono, ExpectedSampleRate);
        }
        else
        {
            provider = sampleProvider;
        }

        int windowSize = settings.ChunkSamples;
        var buffer = new float[ExpectedSampleRate]; // 1 second read buffer
        var pending = new List<float>();
        int samplesRead;

        while ((samplesRead = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            pending.AddRange(buffer.AsSpan(0, samplesRead).ToArray());

            while (pending.Count >= windowSize)
            {
                var chunk = new float[windowSize];
                pending.CopyTo(0, chunk, 0, windowSize);
                pending.RemoveRange(0, windowSize);

                vadDetector.AcceptWaveform(chunk);

                while (!vadDetector.IsEmpty())
                {
                    var seg = vadDetector.Front();
                    vadDetector.Pop();

                    var startTime = TimeSpan.FromSeconds((double)seg.Start / ExpectedSampleRate);
                    var endTime = startTime + TimeSpan.FromSeconds((double)seg.Samples.Length / ExpectedSampleRate);

                    // Skip very short segments (< 0.3s) that are likely noise
                    if ((endTime - startTime).TotalSeconds >= 0.3)
                    {
                        segments.Add(new DiarizationSegment(
                            SpeakerId: 0, StartTime: startTime, EndTime: endTime));
                    }
                }
            }
        }

        // Flush remaining audio
        vadDetector.Flush();
        while (!vadDetector.IsEmpty())
        {
            var seg = vadDetector.Front();
            vadDetector.Pop();

            var startTime = TimeSpan.FromSeconds((double)seg.Start / ExpectedSampleRate);
            var endTime = startTime + TimeSpan.FromSeconds((double)seg.Samples.Length / ExpectedSampleRate);

            if ((endTime - startTime).TotalSeconds >= 0.3)
            {
                segments.Add(new DiarizationSegment(
                    SpeakerId: 0, StartTime: startTime, EndTime: endTime));
            }
        }

        // If VAD found no segments (e.g., very quiet audio), fall back to single segment
        if (segments.Count == 0 && totalDurationSeconds > 0.5)
        {
            Trace.TraceWarning(
                "[CallTranscriptionPipeline] VAD detected no speech in mic audio, falling back to single segment.");
            segments.Add(new DiarizationSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(totalDurationSeconds)));
        }

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] VAD: {0} speech segments detected in {1:F1}s mic audio",
            segments.Count, totalDurationSeconds);

        return segments;
    }

    /// <summary>
    /// Remaps speaker IDs across chunks to ensure cross-chunk consistency.
    /// For group calls (numSpeakers > 1), speaker IDs assigned independently per chunk
    /// may not correspond. This method uses a simple first-appearance ordering heuristic:
    /// within each chunk, speakers are renumbered in order of first appearance,
    /// which provides basic consistency when speakers take turns.
    /// </summary>
    private static List<DiarizationSegment> RemapSpeakerIdsAcrossChunks(
        List<(double OffsetSeconds, DiarizationResult Result)> chunkResults,
        int numSpeakers)
    {
        // For single-speaker streams, no remapping needed
        if (numSpeakers == 1)
        {
            var segments = new List<DiarizationSegment>();
            foreach (var (offset, result) in chunkResults)
            {
                foreach (var seg in result.Segments)
                {
                    segments.Add(new DiarizationSegment(
                        SpeakerId: 0,
                        StartTime: seg.StartTime + TimeSpan.FromSeconds(offset),
                        EndTime: seg.EndTime + TimeSpan.FromSeconds(offset)));
                }
            }
            return segments;
        }

        // For multi-speaker: renumber speakers by order of first appearance within each chunk.
        // This assumes speakers tend to appear in the same order across chunks.
        var allSegments = new List<DiarizationSegment>();

        foreach (var (offset, result) in chunkResults)
        {
            // Build a mapping from original speaker ID to remapped ID (order of first appearance)
            var remapping = new Dictionary<int, int>();
            int nextId = 0;

            // Sort segments by start time to determine order of first appearance
            var sortedSegments = result.Segments.OrderBy(s => s.StartTime).ToList();

            foreach (var seg in sortedSegments)
            {
                if (!remapping.ContainsKey(seg.SpeakerId))
                {
                    remapping[seg.SpeakerId] = nextId++;
                }
            }

            foreach (var seg in sortedSegments)
            {
                allSegments.Add(new DiarizationSegment(
                    SpeakerId: remapping[seg.SpeakerId],
                    StartTime: seg.StartTime + TimeSpan.FromSeconds(offset),
                    EndTime: seg.EndTime + TimeSpan.FromSeconds(offset)));
            }
        }

        return allSegments;
    }

    /// <summary>
    /// Reports pipeline progress with overall percentage calculation.
    /// </summary>
    private static void ReportProgress(
        IProgress<TranscriptionPipelineProgress>? progress,
        PipelineStage stage,
        double stagePercent,
        string description,
        string? warningMessage = null)
    {
        if (progress is null)
            return;

        double overallPercent = CalculateOverallPercent(stage, stagePercent);

        progress.Report(new TranscriptionPipelineProgress
        {
            Stage = stage,
            StagePercent = stagePercent,
            OverallPercent = overallPercent,
            Description = description,
            WarningMessage = warningMessage,
        });
    }

    /// <summary>
    /// Calculates the overall pipeline progress percentage based on the current stage
    /// and the progress within that stage.
    /// </summary>
    private static double CalculateOverallPercent(PipelineStage stage, double stagePercent)
    {
        if (stage == PipelineStage.Completed)
            return 100.0;

        double cumulativeBefore = 0;
        double currentWeight = 0;

        foreach (var (s, weight) in StageWeights)
        {
            if (s == stage)
            {
                currentWeight = weight;
                break;
            }
            cumulativeBefore += weight;
        }

        return Math.Min(100.0,
            (cumulativeBefore + currentWeight * (stagePercent / 100.0)) * 100.0);
    }
}
