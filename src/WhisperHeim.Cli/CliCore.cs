// Pure, side-effect-free helpers for whisperheim-transcribe.
// Factored out of Program so arg parsing, endpoint resolution, URL building, and
// response parsing are unit-testable without a live server (against the ADR-0001
// wire contract).
using System.Text.Json;

namespace WhisperHeim.Cli;

internal static class CliCore
{
    public const string DefaultEndpoint = "http://127.0.0.1:7777";
    public const string EndpointEnvVar = "WHISPERHEIM_ENDPOINT";

    /// <summary>
    /// Parses argv. Returns the audio file path on success, or null when usage
    /// should be printed (no path, too many positionals, or --help/-h).
    /// </summary>
    public static string? ParseArgs(string[] args)
    {
        string? path = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                case "/?":
                    return null;
                default:
                    // First (and only) positional argument is the audio file path.
                    if (path is not null) return null; // a second positional is a usage error
                    path = args[i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(path)) return null;
        return path;
    }

    /// <summary>
    /// Resolves the base endpoint from the env var, falling back to the default.
    /// </summary>
    public static string ResolveEndpoint(string? envValue) =>
        string.IsNullOrWhiteSpace(envValue) ? DefaultEndpoint : envValue!;

    /// <summary>
    /// Builds the full /transcribe URL with the filename hint so the server can
    /// preserve the extension for the decoder (ADR-0001).
    /// </summary>
    public static string BuildTranscribeUrl(string endpoint, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var encoded = Uri.EscapeDataString(fileName);
        return $"{endpoint.TrimEnd('/')}/transcribe?filename={encoded}";
    }

    /// <summary>
    /// Extracts the "text" field from a /transcribe JSON response body. Returns
    /// the (possibly empty) transcript, or null if the body has no text field.
    /// </summary>
    public static string? ExtractText(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("text", out var textElement)
            && textElement.ValueKind == JsonValueKind.String)
        {
            return textElement.GetString() ?? string.Empty;
        }
        return null;
    }

    public static string UsageText() =>
        string.Join(Environment.NewLine,
            "Usage: whisperheim-transcribe <path-to-audio-file>",
            "",
            "Reads the audio file's bytes, POSTs them to WhisperHeim's STT API,",
            "and prints the transcript to stdout.",
            "",
            "Arguments:",
            "  <path-to-audio-file>  Audio file to transcribe (OGG/MP3/M4A/WAV/...)",
            "",
            "Options:",
            "  --help, -h            Show this message",
            "",
            "Environment:",
            $"  {EndpointEnvVar}  Override base URL (default: {DefaultEndpoint})",
            "",
            "Exit codes:",
            "  0  success",
            "  1  usage / arg error (no path, --help, missing or unreadable file)",
            "  2  HTTP non-success (server returned an error)",
            "  3  cannot reach the endpoint (is WhisperHeim running?)");
}
