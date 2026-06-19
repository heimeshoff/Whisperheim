// whisperheim-transcribe CLI — thin wrapper over POST /transcribe (ADR-0001).
// Mirrors Utterheim.Cli's shape: WHISPERHEIM_ENDPOINT override and 0/1/2/3 exit
// codes. Two deliberate differences from utterheim-speak: it POSTs the file's
// raw audio bytes (not a JSON envelope), and prints the response's `text` field.
//
//   whisperheim-transcribe path\to\audio.ogg
//
// Exit codes: 0 success / 1 usage or file error / 2 HTTP non-success /
// 3 cannot reach endpoint.
using System.Net.Http.Headers;
using WhisperHeim.Cli;

internal static class Program
{
    private const string ToolName = "whisperheim-transcribe";

    public static async Task<int> Main(string[] args)
    {
        var path = CliCore.ParseArgs(args);
        if (path is null)
        {
            Console.WriteLine(CliCore.UsageText());
            return 1;
        }

        // Validate the file before any network call (acceptance criterion).
        byte[] audioBytes;
        try
        {
            if (!File.Exists(path))
            {
                await Console.Error.WriteLineAsync($"{ToolName}: file not found: {path}");
                return 1;
            }
            audioBytes = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"{ToolName}: cannot read file: {path} ({ex.Message})");
            return 1;
        }

        var endpoint = CliCore.ResolveEndpoint(Environment.GetEnvironmentVariable(CliCore.EndpointEnvVar));
        var url = CliCore.BuildTranscribeUrl(endpoint, path);
        var fileName = Path.GetFileName(path);

        // Transcription blocks until the engine finishes; long files need a
        // generous timeout. The 5 s default in the speak reference is far too
        // short for STT — wait indefinitely instead.
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        try
        {
            using var content = new ByteArrayContent(audioBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            // Belt-and-suspenders extension hint alongside the ?filename= query.
            content.Headers.Add("X-Filename", fileName);

            using var response = await http.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                await Console.Error.WriteLineAsync(
                    $"{ToolName}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(errorBody))
                    await Console.Error.WriteLineAsync(errorBody);
                return 2;
            }

            var json = await response.Content.ReadAsStringAsync();
            var text = CliCore.ExtractText(json);
            if (text is null)
            {
                await Console.Error.WriteLineAsync(
                    $"{ToolName}: response had no 'text' field: {json}");
                return 2;
            }

            // Print only the transcript. Empty/no-speech audio prints a blank line.
            Console.WriteLine(text);
            return 0;
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync(
                $"{ToolName}: cannot reach {url} — is WhisperHeim running? ({ex.Message})");
            return 3;
        }
        catch (TaskCanceledException ex)
        {
            await Console.Error.WriteLineAsync(
                $"{ToolName}: request timed out talking to {url} ({ex.Message})");
            return 3;
        }
    }
}
