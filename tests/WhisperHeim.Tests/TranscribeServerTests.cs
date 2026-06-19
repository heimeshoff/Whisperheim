using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Http;

namespace WhisperHeim.Tests;

/// <summary>
/// Integration tests for <see cref="TranscribeServer"/> over a real loopback
/// <see cref="HttpListener"/> on an ephemeral port, driven by a fake engine
/// (task main-h7k2p, ADR-0001). Exercises bind/shutdown, the two endpoints, the
/// error contract, and FIFO queue-and-block behaviour end-to-end.
/// </summary>
public class TranscribeServerTests
{
    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private sealed class FakeEngine : ITranscribeEngine
    {
        private readonly Func<TranscribeOutcome> _outcome;
        private int _depth;
        public FakeEngine(Func<TranscribeOutcome> outcome) => _outcome = outcome;

        public bool IsBusy { get; set; }
        public int QueueDepth => _depth;
        public List<Guid> Enqueued { get; } = new();

        public Guid EnqueueFile(string filePath)
        {
            Interlocked.Increment(ref _depth);
            var id = Guid.NewGuid();
            lock (Enqueued) Enqueued.Add(id);
            return id;
        }

        public Task<TranscribeOutcome> WaitForOutcomeAsync(Guid id, CancellationToken ct = default)
        {
            Interlocked.Decrement(ref _depth);
            return Task.FromResult(_outcome());
        }
    }

    [Theory]
    [InlineData(null, 7777)]
    [InlineData("", 7777)]
    [InlineData("garbage", 7777)]
    [InlineData("0", 7777)]
    [InlineData("70000", 7777)]
    [InlineData("8888", 8888)]
    public void ResolvePort_falls_back_to_default_for_invalid_values(string? env, int expected)
        => Assert.Equal(expected, TranscribeServer.ResolvePort(env));

    [Fact]
    public void Start_does_not_throw_when_port_is_in_use_and_returns_false()
    {
        var port = FreePort();
        // Occupy the prefix with a first server.
        var first = new TranscribeServer(MakeHandler(Ok()), port);
        Assert.True(first.Start());

        var second = new TranscribeServer(MakeHandler(Ok()), port);
        var started = second.Start(); // must not throw

        Assert.False(started);

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public async Task Health_endpoint_reports_busy_and_queue_depth()
    {
        var engine = new FakeEngine(() => new TranscribeOutcome(true,
            new FileTranscriptionResult("", TimeSpan.Zero, TimeSpan.Zero, 0, 0, "x"), null))
        { IsBusy = true };
        var port = FreePort();
        using var server = new TranscribeServer(MakeHandler(engine), port);
        Assert.True(server.Start());

        using var client = new HttpClient();
        var resp = await client.GetAsync($"http://127.0.0.1:{port}/health");
        resp.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("ok", json.GetProperty("status").GetString());
        Assert.True(json.GetProperty("busy").GetBoolean());
    }

    [Fact]
    public async Task Transcribe_returns_200_json_over_the_wire()
    {
        var result = new FileTranscriptionResult(
            "the transcript", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), 0.2, 1, "x.ogg");
        var port = FreePort();
        using var server = new TranscribeServer(MakeHandler(new FakeEngine(() =>
            new TranscribeOutcome(true, result, null))), port);
        Assert.True(server.Start());

        using var client = new HttpClient();
        var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
        var resp = await client.PostAsync($"http://127.0.0.1:{port}/transcribe?filename=clip.ogg", content);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("the transcript", json.GetProperty("text").GetString());
        Assert.Equal(10, json.GetProperty("audioDurationSeconds").GetDouble(), 3);
        Assert.Equal(1, json.GetProperty("chunkCount").GetInt32());
    }

    [Fact]
    public async Task Empty_body_returns_400_over_the_wire()
    {
        var port = FreePort();
        using var server = new TranscribeServer(MakeHandler(Ok()), port);
        Assert.True(server.Start());

        using var client = new HttpClient();
        var resp = await client.PostAsync($"http://127.0.0.1:{port}/transcribe",
            new ByteArrayContent(Array.Empty<byte>()));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Engine_failure_returns_500_over_the_wire()
    {
        var port = FreePort();
        using var server = new TranscribeServer(MakeHandler(new FakeEngine(() =>
            new TranscribeOutcome(false, null, "engine exploded"))), port);
        Assert.True(server.Start());

        using var client = new HttpClient();
        var resp = await client.PostAsync($"http://127.0.0.1:{port}/transcribe",
            new ByteArrayContent(new byte[] { 1, 2 }));

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("engine exploded", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Concurrent_requests_both_funnel_through_enqueue_and_succeed()
    {
        // Slow outcome so the two requests genuinely overlap in flight; both must be
        // enqueued (not rejected) and both must return 200 (ADR-0001 queues-and-blocks).
        var engine = new FakeEngine(() =>
        {
            Thread.Sleep(150);
            return new TranscribeOutcome(true,
                new FileTranscriptionResult("ok", TimeSpan.Zero, TimeSpan.Zero, 0, 0, "x"), null);
        });
        var port = FreePort();
        using var server = new TranscribeServer(MakeHandler(engine), port);
        Assert.True(server.Start());

        using var client = new HttpClient();
        var url = $"http://127.0.0.1:{port}/transcribe";
        var t1 = client.PostAsync(url, new ByteArrayContent(new byte[] { 1 }));
        var t2 = client.PostAsync(url, new ByteArrayContent(new byte[] { 2 }));
        var responses = await Task.WhenAll(t1, t2);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(2, engine.Enqueued.Count); // both were enqueued, neither rejected
    }

    [Fact]
    public async Task Server_stops_cleanly_and_port_is_released()
    {
        var port = FreePort();
        var server = new TranscribeServer(MakeHandler(Ok()), port);
        Assert.True(server.Start());
        server.Dispose();

        // Port must be re-bindable after dispose — proves no leaked listener.
        using var second = new TranscribeServer(MakeHandler(Ok()), port);
        Assert.True(second.Start());
    }

    private static FakeEngine Ok()
        => new(() => new TranscribeOutcome(true,
            new FileTranscriptionResult("", TimeSpan.Zero, TimeSpan.Zero, 0, 0, "x"), null));

    private static TranscribeRequestHandler MakeHandler(ITranscribeEngine engine)
        => new(engine);
}
