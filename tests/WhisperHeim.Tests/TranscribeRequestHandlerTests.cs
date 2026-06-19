using System.IO;
using System.Text;
using System.Text.Json;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Http;

namespace WhisperHeim.Tests;

/// <summary>
/// Unit tests for <see cref="TranscribeRequestHandler"/> — the transport-agnostic core
/// of the STT API (task main-h7k2p, ADR-0001). The handler funnels POST /transcribe
/// through a fake engine, maps outcomes onto the wire contract, and manages the temp
/// file lifecycle. No real HttpListener or ASR engine involved.
/// </summary>
public class TranscribeRequestHandlerTests
{
    /// <summary>
    /// A fake engine that records the temp path it was handed and returns a
    /// preconfigured outcome. Captures whether the temp file still existed at
    /// enqueue time so tests can assert cleanup happens AFTER the outcome.
    /// </summary>
    private sealed class FakeEngine : ITranscribeEngine
    {
        private readonly TranscribeOutcome _outcome;
        public FakeEngine(TranscribeOutcome outcome) => _outcome = outcome;

        public bool IsBusy { get; set; }
        public int QueueDepth { get; set; }

        public string? LastEnqueuedPath { get; private set; }
        public byte[]? FileBytesAtEnqueue { get; private set; }

        public Guid EnqueueFile(string filePath)
        {
            LastEnqueuedPath = filePath;
            FileBytesAtEnqueue = File.Exists(filePath) ? File.ReadAllBytes(filePath) : null;
            return Guid.NewGuid();
        }

        public Task<TranscribeOutcome> WaitForOutcomeAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_outcome);
    }

    private static TranscribeHttpRequest Post(byte[] body, string? filenameQuery = null, string? filenameHeader = null)
        => new()
        {
            Method = "POST",
            Path = "/transcribe",
            Body = body,
            FilenameQuery = filenameQuery,
            FilenameHeader = filenameHeader,
        };

    [Fact]
    public async Task Transcribe_returns_200_with_full_result_metadata()
    {
        var result = new FileTranscriptionResult(
            Text: "hello world",
            AudioDuration: TimeSpan.FromSeconds(42.13),
            TranscriptionDuration: TimeSpan.FromSeconds(7.05),
            RealTimeFactor: 0.17,
            ChunkCount: 3,
            SourceFilePath: "x.ogg");
        var engine = new FakeEngine(new TranscribeOutcome(true, result, null));
        var handler = new TranscribeRequestHandler(engine);

        var resp = await handler.HandleAsync(Post(new byte[] { 1, 2, 3 }, filenameQuery: "x.ogg"));

        Assert.Equal(200, resp.StatusCode);
        var json = JsonDocument.Parse(resp.Body).RootElement;
        Assert.Equal("hello world", json.GetProperty("text").GetString());
        Assert.Equal(42.13, json.GetProperty("audioDurationSeconds").GetDouble(), 3);
        Assert.Equal(7.05, json.GetProperty("transcriptionDurationSeconds").GetDouble(), 3);
        Assert.Equal(0.17, json.GetProperty("realTimeFactor").GetDouble(), 3);
        Assert.Equal(3, json.GetProperty("chunkCount").GetInt32());
    }

    [Fact]
    public async Task Transcribe_writes_body_to_temp_file_preserving_extension()
    {
        var engine = new FakeEngine(new TranscribeOutcome(
            true, EmptyResult(), null));
        var handler = new TranscribeRequestHandler(engine);
        var body = new byte[] { 9, 8, 7, 6 };

        await handler.HandleAsync(Post(body, filenameQuery: "clip.mp3"));

        Assert.NotNull(engine.LastEnqueuedPath);
        Assert.Equal(".mp3", Path.GetExtension(engine.LastEnqueuedPath!));
        Assert.Equal(body, engine.FileBytesAtEnqueue);
    }

    [Fact]
    public async Task Transcribe_prefers_X_Filename_header_when_no_query()
    {
        var engine = new FakeEngine(new TranscribeOutcome(true, EmptyResult(), null));
        var handler = new TranscribeRequestHandler(engine);

        await handler.HandleAsync(Post(new byte[] { 1 }, filenameHeader: "voice.m4a"));

        Assert.Equal(".m4a", Path.GetExtension(engine.LastEnqueuedPath!));
    }

    [Fact]
    public async Task Empty_body_returns_400()
    {
        var engine = new FakeEngine(new TranscribeOutcome(true, EmptyResult(), null));
        var handler = new TranscribeRequestHandler(engine);

        var resp = await handler.HandleAsync(Post(Array.Empty<byte>()));

        Assert.Equal(400, resp.StatusCode);
        Assert.Equal("audio body is required",
            JsonDocument.Parse(resp.Body).RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task No_speech_audio_returns_200_with_empty_string_not_sentinel()
    {
        var engine = new FakeEngine(new TranscribeOutcome(true, EmptyResult(), null));
        var handler = new TranscribeRequestHandler(engine);

        var resp = await handler.HandleAsync(Post(new byte[] { 1, 2 }));

        Assert.Equal(200, resp.StatusCode);
        Assert.Equal("", JsonDocument.Parse(resp.Body).RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Engine_failure_returns_500_with_error_message()
    {
        var engine = new FakeEngine(new TranscribeOutcome(false, null, "decode blew up"));
        var handler = new TranscribeRequestHandler(engine);

        var resp = await handler.HandleAsync(Post(new byte[] { 1, 2 }));

        Assert.Equal(500, resp.StatusCode);
        Assert.Equal("decode blew up",
            JsonDocument.Parse(resp.Body).RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Unsupported_format_returns_415()
    {
        var engine = new FakeEngine(new TranscribeOutcome(
            false, null, "The file format '.xyz' is not supported."));
        var handler = new TranscribeRequestHandler(engine);

        var resp = await handler.HandleAsync(Post(new byte[] { 1, 2 }, filenameQuery: "f.xyz"));

        Assert.Equal(415, resp.StatusCode);
    }

    [Fact]
    public async Task Temp_file_is_cleaned_up_after_success()
    {
        var engine = new FakeEngine(new TranscribeOutcome(true, EmptyResult(), null));
        var handler = new TranscribeRequestHandler(engine);

        await handler.HandleAsync(Post(new byte[] { 1, 2 }, filenameQuery: "a.wav"));

        // The engine saw the file during enqueue; after HandleAsync returns it must be gone.
        Assert.False(File.Exists(engine.LastEnqueuedPath!), "temp file should be deleted after the request");
    }

    [Fact]
    public async Task Temp_file_is_cleaned_up_after_failure()
    {
        var engine = new FakeEngine(new TranscribeOutcome(false, null, "boom"));
        var handler = new TranscribeRequestHandler(engine);

        await handler.HandleAsync(Post(new byte[] { 1, 2 }, filenameQuery: "a.wav"));

        Assert.False(File.Exists(engine.LastEnqueuedPath!), "temp file should be deleted even on failure");
    }

    [Fact]
    public async Task Health_returns_status_busy_and_queue_depth()
    {
        var engine = new FakeEngine(new TranscribeOutcome(true, EmptyResult(), null))
        {
            IsBusy = true,
            QueueDepth = 2,
        };
        var handler = new TranscribeRequestHandler(engine);

        var resp = await handler.HandleAsync(new TranscribeHttpRequest { Method = "GET", Path = "/health" });

        Assert.Equal(200, resp.StatusCode);
        var json = JsonDocument.Parse(resp.Body).RootElement;
        Assert.Equal("ok", json.GetProperty("status").GetString());
        Assert.True(json.GetProperty("busy").GetBoolean());
        Assert.Equal(2, json.GetProperty("queueDepth").GetInt32());
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var engine = new FakeEngine(new TranscribeOutcome(true, EmptyResult(), null));
        var handler = new TranscribeRequestHandler(engine);

        var resp = await handler.HandleAsync(new TranscribeHttpRequest { Method = "GET", Path = "/nope" });

        Assert.Equal(404, resp.StatusCode);
    }

    [Fact]
    public async Task Wrong_method_on_transcribe_returns_405()
    {
        var engine = new FakeEngine(new TranscribeOutcome(true, EmptyResult(), null));
        var handler = new TranscribeRequestHandler(engine);

        var resp = await handler.HandleAsync(new TranscribeHttpRequest { Method = "GET", Path = "/transcribe" });

        Assert.Equal(405, resp.StatusCode);
    }

    private static FileTranscriptionResult EmptyResult()
        => new("", TimeSpan.Zero, TimeSpan.Zero, 0, 0, "x");
}
