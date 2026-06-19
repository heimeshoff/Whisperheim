using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace WhisperHeim.Services.Http;

/// <summary>
/// Transport-agnostic request handler for the STT API (task main-h7k2p, ADR-0001).
/// Owns routing, the temp-file lifecycle, funnelling through the shared engine, and
/// mapping engine outcomes onto the v1 wire contract. <see cref="TranscribeServer"/>
/// is a thin HttpListener adapter around this; all decision logic lives here so it
/// can be unit-tested without sockets or a real ASR engine.
/// </summary>
public sealed class TranscribeRequestHandler
{
    private readonly ITranscribeEngine _engine;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Records already declare camelCase via JsonPropertyName; keep options minimal.
    };

    public TranscribeRequestHandler(ITranscribeEngine engine) => _engine = engine;

    public async Task<TranscribeHttpResponse> HandleAsync(
        TranscribeHttpRequest request, CancellationToken cancellationToken = default)
    {
        // Routing. v1 serves exactly two endpoints (ADR-0001); everything else 404.
        if (request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                return MethodNotAllowed();
            return Health();
        }

        if (request.Path.Equals("/transcribe", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                return MethodNotAllowed();
            return await TranscribeAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return Error(404, "not found");
    }

    private TranscribeHttpResponse Health()
        => TranscribeHttpResponse.Json(200,
            JsonSerializer.Serialize(
                new HealthResponseBody("ok", _engine.IsBusy, _engine.QueueDepth), JsonOptions));

    private async Task<TranscribeHttpResponse> TranscribeAsync(
        TranscribeHttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Body is null || request.Body.Length == 0)
            return Error(400, "audio body is required");

        // Write the body to a temp file, preserving the caller's extension hint so the
        // existing NAudio/ffmpeg decode path can dispatch on it. Default to .ogg (the
        // app's primary recording format) when no hint is given.
        var ext = ResolveExtension(request);
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "whisperheim-transcribe-" + Guid.NewGuid().ToString("N") + ext);

        try
        {
            await File.WriteAllBytesAsync(tempPath, request.Body, cancellationToken)
                .ConfigureAwait(false);

            var id = _engine.EnqueueFile(tempPath);
            var outcome = await _engine.WaitForOutcomeAsync(id, cancellationToken)
                .ConfigureAwait(false);

            if (!outcome.Succeeded)
            {
                var message = outcome.ErrorMessage ?? "transcription failed";
                var status = ClassifyError(message);
                return Error(status, message);
            }

            var result = outcome.Result;
            var body = new TranscribeResponseBody(
                // Raw engine text — empty string for no-speech audio (the UI's
                // "(No speech detected)" sentinel is a UI concern, ADR-0001).
                Text: result?.Text ?? string.Empty,
                AudioDurationSeconds: result?.AudioDuration.TotalSeconds ?? 0,
                TranscriptionDurationSeconds: result?.TranscriptionDuration.TotalSeconds ?? 0,
                RealTimeFactor: result?.RealTimeFactor ?? 0,
                ChunkCount: result?.ChunkCount ?? 0);

            return TranscribeHttpResponse.Json(200, JsonSerializer.Serialize(body, JsonOptions));
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static string ResolveExtension(TranscribeHttpRequest request)
    {
        var hint = request.FilenameQuery;
        if (string.IsNullOrWhiteSpace(hint))
            hint = request.FilenameHeader;

        if (!string.IsNullOrWhiteSpace(hint))
        {
            var ext = Path.GetExtension(hint);
            if (!string.IsNullOrWhiteSpace(ext))
                return ext;
        }

        return ".ogg";
    }

    /// <summary>
    /// Maps an engine error message to an HTTP status. Unsupported/corrupt-format
    /// failures map to 415; everything else is a 500 (ADR-0001).
    /// </summary>
    private static int ClassifyError(string message)
    {
        var m = message.ToLowerInvariant();
        if (m.Contains("not supported") || m.Contains("unsupported") ||
            m.Contains("not a supported"))
        {
            return 415;
        }
        return 500;
    }

    private static TranscribeHttpResponse MethodNotAllowed()
        => Error(405, "method not allowed");

    private static TranscribeHttpResponse Error(int status, string message)
        => TranscribeHttpResponse.Json(status,
            JsonSerializer.Serialize(new ErrorResponseBody(message), JsonOptions));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[TranscribeServer] Could not delete temp file '{0}': {1}", path, ex.Message);
        }
    }
}
