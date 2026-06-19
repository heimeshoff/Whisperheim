using System.Text.Json.Serialization;

namespace WhisperHeim.Services.Http;

/// <summary>
/// A transport-agnostic view of an incoming HTTP request, so the request-handling
/// logic (routing, error mapping, response shaping) can be unit-tested without an
/// actual <c>HttpListener</c>. The <see cref="TranscribeServer"/> adapts a real
/// <c>HttpListenerContext</c> onto this shape.
/// </summary>
public sealed class TranscribeHttpRequest
{
    public required string Method { get; init; }

    /// <summary>Absolute path of the request URL, e.g. "/transcribe".</summary>
    public required string Path { get; init; }

    /// <summary>Optional "filename" query-string value (extension hint for the decoder).</summary>
    public string? FilenameQuery { get; init; }

    /// <summary>Optional X-Filename header value (extension hint for the decoder).</summary>
    public string? FilenameHeader { get; init; }

    /// <summary>Raw request body bytes (the audio).</summary>
    public byte[] Body { get; init; } = Array.Empty<byte>();
}

/// <summary>
/// A transport-agnostic HTTP response produced by <see cref="TranscribeRequestHandler"/>.
/// </summary>
public sealed class TranscribeHttpResponse
{
    public int StatusCode { get; init; }
    public string ContentType { get; init; } = "application/json";
    public string Body { get; init; } = string.Empty;

    public static TranscribeHttpResponse Json(int status, string body)
        => new() { StatusCode = status, ContentType = "application/json", Body = body };
}

/// <summary>
/// 200 OK body for POST /transcribe. Fields map 1:1 onto FileTranscriptionResult
/// (ADR-0001 wire contract). camelCase via JsonPropertyName so serialization is
/// independent of global options.
/// </summary>
public sealed record TranscribeResponseBody(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("audioDurationSeconds")] double AudioDurationSeconds,
    [property: JsonPropertyName("transcriptionDurationSeconds")] double TranscriptionDurationSeconds,
    [property: JsonPropertyName("realTimeFactor")] double RealTimeFactor,
    [property: JsonPropertyName("chunkCount")] int ChunkCount);

/// <summary>GET /health body (ADR-0001).</summary>
public sealed record HealthResponseBody(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("busy")] bool Busy,
    [property: JsonPropertyName("queueDepth")] int QueueDepth);

/// <summary>Error body shape: {"error":"..."} (ADR-0001).</summary>
public sealed record ErrorResponseBody(
    [property: JsonPropertyName("error")] string Error);
