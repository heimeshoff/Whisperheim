using System.IO;
using System.Text;

namespace WhisperHeim.Services.Export;

/// <summary>
/// Derives a Windows-safe base filename (no extension) from a recording's
/// display name, for use by <see cref="TranscriptAutoExportService"/> when it
/// writes <c>&lt;recording name&gt;.md</c> into a configured export folder
/// (task main-m6x4v, ADR-0008).
/// </summary>
public static class ExportFileName
{
    /// <summary>
    /// Windows-reserved device names. Blocked regardless of any extension
    /// (<c>CON.txt</c> is just as invalid as <c>CON</c>).
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Explicit Windows-invalid filename characters, on top of whatever
    /// <see cref="Path.GetInvalidFileNameChars"/> reports for the current
    /// platform (belt-and-braces: the task spec calls these out by name).
    /// </summary>
    private static readonly char[] ExtraInvalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

    private const int MaxLength = 120;

    /// <summary>
    /// Sanitizes <paramref name="name"/> into a Windows-safe base filename
    /// (no extension). Steps, in order:
    /// <list type="number">
    /// <item>Replace every invalid character with <c>_</c>.</item>
    /// <item>Trim trailing dots and spaces.</item>
    /// <item>If the result (ignoring any extension) is a reserved device name,
    /// prefix with <c>_</c>.</item>
    /// <item>Truncate to 120 characters (re-trimming any dot/space the cut
    /// reintroduces at the boundary).</item>
    /// <item>If empty after all of the above, fall back to
    /// <c>transcript_{recordingStartedUtc:yyyyMMdd_HHmmss}</c>.</item>
    /// </list>
    /// </summary>
    public static string Sanitize(string? name, DateTimeOffset recordingStartedUtc)
    {
        var input = name ?? string.Empty;

        // 1. Replace invalid characters.
        var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
        foreach (var c in ExtraInvalidChars)
            invalidChars.Add(c);

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
            sb.Append(invalidChars.Contains(c) ? '_' : c);
        var result = sb.ToString();

        // 2. Trim trailing dots and spaces.
        result = result.TrimEnd('.', ' ');

        // 3. Reserved device name check, ignoring any extension.
        var baseName = Path.GetFileNameWithoutExtension(result);
        if (ReservedNames.Contains(baseName))
            result = "_" + result;

        // 4. Truncate to the length cap.
        if (result.Length > MaxLength)
            result = result[..MaxLength];
        // The cut can land right after a dot/space; re-trim so the result
        // stays a clean Windows-safe name.
        result = result.TrimEnd('.', ' ');

        // 5. Fall back to a timestamp-derived name if nothing survived.
        if (string.IsNullOrEmpty(result))
            result = $"transcript_{recordingStartedUtc:yyyyMMdd_HHmmss}";

        return result;
    }
}
