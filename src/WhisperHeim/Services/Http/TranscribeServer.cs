using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace WhisperHeim.Services.Http;

/// <summary>
/// Loopback-only HTTP/1.1 server exposing the STT engine to first-party local tooling
/// (Claude), per ADR-0001 / task main-h7k2p. BCL-only: built on
/// <see cref="HttpListener"/>, no ASP.NET Core / Generic Host. Binds the literal
/// <c>127.0.0.1</c> prefix (avoids the http.sys URL-ACL wrinkle that non-localhost
/// prefixes hit), default port 7777 (override via WHISPERHEIM_TRANSCRIBE_PORT).
///
/// Synchronous block-and-return: each request enqueues onto the shared
/// <see cref="Transcription.TranscriptionQueueService"/> and holds the connection open
/// until the item reaches a terminal stage. The accept loop runs on a background thread.
/// </summary>
public sealed class TranscribeServer : IDisposable
{
    /// <summary>Default loopback port (ADR-0001).</summary>
    public const int DefaultPort = 7777;

    /// <summary>Environment variable that overrides the default port.</summary>
    public const string PortEnvVar = "WHISPERHEIM_TRANSCRIBE_PORT";

    private readonly TranscribeRequestHandler _handler;
    private readonly int _port;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Thread? _acceptThread;
    private bool _started;
    private bool _disposed;

    public TranscribeServer(TranscribeRequestHandler handler, int? port = null)
    {
        _handler = handler;
        _port = port ?? ResolvePort(Environment.GetEnvironmentVariable(PortEnvVar));
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
    }

    /// <summary>The TCP port the server is bound to.</summary>
    public int Port => _port;

    /// <summary>True once the accept loop is running.</summary>
    public bool IsRunning => _started;

    /// <summary>
    /// Resolves the listen port from the env-var value: a valid 1..65535 integer wins,
    /// anything else (null, empty, garbage, out of range) falls back to the default.
    /// Pure so it can be unit-tested.
    /// </summary>
    public static int ResolvePort(string? envValue)
    {
        if (int.TryParse(envValue, out var p) && p is > 0 and <= 65535)
            return p;
        return DefaultPort;
    }

    /// <summary>
    /// Starts the listener and accept loop. A bind failure (e.g. port in use) is logged
    /// via <see cref="Trace.TraceError"/> and swallowed — startup must NOT crash if the
    /// API can't bind (ADR-0001). Returns true if the server started, false otherwise.
    /// </summary>
    public bool Start()
    {
        if (_started) return true;
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[TranscribeServer] Could not bind http://127.0.0.1:{0}/ — STT API disabled. {1}",
                _port, ex.Message);
            return false;
        }

        _started = true;
        _acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "TranscribeServer-Accept",
        };
        _acceptThread.Start();

        Trace.TraceInformation("[TranscribeServer] Listening on http://127.0.0.1:{0}/", _port);
        return true;
    }

    private void AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = _listener.GetContext();
            }
            catch (Exception) when (_cts.IsCancellationRequested || !_listener.IsListening)
            {
                break; // shutting down
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[TranscribeServer] Accept failed: {0}", ex.Message);
                continue;
            }

            // Handle each request on its own thread so a long transcription does not
            // block the accept loop. Ordering across requests is preserved by the
            // single-engine FIFO queue, not by the transport.
            _ = Task.Run(() => HandleContextAsync(context));
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context)
    {
        try
        {
            var request = await ReadRequestAsync(context.Request).ConfigureAwait(false);
            var response = await _handler.HandleAsync(request, _cts.Token).ConfigureAwait(false);
            await WriteResponseAsync(context.Response, response).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[TranscribeServer] Request handling failed: {0}", ex.Message);
            TryWriteFallback500(context.Response);
        }
        finally
        {
            try { context.Response.OutputStream.Close(); } catch { /* best-effort */ }
        }
    }

    private static async Task<TranscribeHttpRequest> ReadRequestAsync(HttpListenerRequest req)
    {
        byte[] body = Array.Empty<byte>();
        if (req.HasEntityBody)
        {
            using var ms = new MemoryStream();
            await req.InputStream.CopyToAsync(ms).ConfigureAwait(false);
            body = ms.ToArray();
        }

        return new TranscribeHttpRequest
        {
            Method = req.HttpMethod,
            Path = req.Url?.AbsolutePath ?? "/",
            FilenameQuery = req.QueryString["filename"],
            FilenameHeader = req.Headers["X-Filename"],
            Body = body,
        };
    }

    private static async Task WriteResponseAsync(HttpListenerResponse res, TranscribeHttpResponse response)
    {
        res.StatusCode = response.StatusCode;
        res.ContentType = response.ContentType;
        var bytes = Encoding.UTF8.GetBytes(response.Body);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static void TryWriteFallback500(HttpListenerResponse res)
    {
        try
        {
            res.StatusCode = 500;
            res.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes("{\"error\":\"internal server error\"}");
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes);
        }
        catch { /* best-effort */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        try { _listener.Stop(); } catch { /* ignore */ }
        try { _listener.Close(); } catch { /* ignore */ }

        // GetContext unblocks on Stop(); give the thread a moment to exit.
        try { _acceptThread?.Join(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }

        _cts.Dispose();
        _started = false;
        Trace.TraceInformation("[TranscribeServer] Stopped.");
    }
}
