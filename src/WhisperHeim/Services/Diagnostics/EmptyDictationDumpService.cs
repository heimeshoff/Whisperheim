using System.Diagnostics;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace WhisperHeim.Services.Diagnostics;

/// <summary>
/// Best-effort dumper for the raw samples of a dictation that decoded to an empty
/// transcript (task main-ma9j8): writes a 16 kHz mono 16-bit PCM WAV into a
/// machine-local diagnostics folder so the next lost dictation is reproducible
/// offline, and keeps only the <see cref="RingSize"/> most recent dumps.
///
/// Standalone and injectable (root directory + clock + disabled-predicate) so it
/// is unit-testable without touching the real <c>%LOCALAPPDATA%</c> folder or the
/// real system clock -- mirrors <see cref="Transcription.ModelLifecycleManager"/>'s
/// injected-clock / injected-predicate shape.
/// </summary>
public sealed class EmptyDictationDumpService
{
    /// <summary>Number of most-recent dump files kept; older ones are deleted on each write.</summary>
    public const int RingSize = 10;

    private const string FilePrefix = "empty-dictation-";
    private const string FileNameTimestampFormat = "yyyyMMdd-HHmmss";

    private readonly string _directory;
    private readonly Func<DateTime> _utcNow;
    private readonly Func<bool> _isDisabled;

    /// <summary>Result of a dump attempt.</summary>
    /// <param name="Success">True if a WAV file was written.</param>
    /// <param name="Disabled">True if the dump was skipped because it is disabled (env var).</param>
    /// <param name="Path">The written file's full path, when <paramref name="Success"/> is true.</param>
    /// <param name="Error">The exception that caused the write to fail, if any.</param>
    public sealed record DumpResult(bool Success, bool Disabled, string? Path, Exception? Error);

    /// <param name="directory">
    /// The diagnostics folder to write into. Defaults to
    /// <see cref="Settings.DataPathService.DiagnosticsPath"/> (<c>%LOCALAPPDATA%\WhisperHeim\diagnostics\</c>).
    /// </param>
    /// <param name="utcNow">Clock source used for the dump filename's timestamp; defaults to <see cref="DateTime.UtcNow"/>.</param>
    /// <param name="isDisabled">
    /// Predicate gating the write. Defaults to reading the
    /// <c>WHISPERHEIM_DISABLE_DIAG_DUMP</c> environment variable (mirroring
    /// <c>WHISPERHEIM_DISABLE_STARTUP_GC</c>): <c>"1"</c> disables the WAV write only,
    /// never the caller's warning/info trace line.
    /// </param>
    public EmptyDictationDumpService(
        string? directory = null,
        Func<DateTime>? utcNow = null,
        Func<bool>? isDisabled = null)
    {
        _directory = directory ?? Path.Combine(Settings.DataPathService.LocalAppDataRoot, "diagnostics");
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _isDisabled = isDisabled ?? (() => Environment.GetEnvironmentVariable("WHISPERHEIM_DISABLE_DIAG_DUMP") == "1");
    }

    /// <summary>
    /// Writes <paramref name="samples"/> as a 16-bit PCM WAV, then enforces the
    /// ring cap. Best-effort: any I/O failure is caught, logged at Warning, and
    /// reported via <see cref="DumpResult.Error"/> rather than thrown -- callers
    /// must never let a dump failure change what gets typed.
    /// </summary>
    public DumpResult TryDump(float[] samples, int sampleRate = 16000)
    {
        if (_isDisabled())
            return new DumpResult(false, true, null, null);

        try
        {
            Directory.CreateDirectory(_directory);
            var fileName = $"{FilePrefix}{_utcNow().ToString(FileNameTimestampFormat)}.wav";
            var path = Path.Combine(_directory, fileName);
            WriteWav(path, samples, sampleRate);
            EnforceRingCap();
            return new DumpResult(true, false, path, null);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[EmptyDictationDumpService] Failed to write empty-dictation dump: {0}", ex.Message);
            return new DumpResult(false, false, null, ex);
        }
    }

    /// <summary>
    /// Converts float32 [-1, 1] samples to 16-bit PCM and writes them as a mono
    /// WAV -- the inverse of <see cref="Audio.AudioCaptureService"/>'s
    /// <c>pcm16 / 32768f</c> capture-side conversion.
    /// </summary>
    private static void WriteWav(string path, float[] samples, int sampleRate)
    {
        var format = new WaveFormat(sampleRate, 16, 1);
        using var writer = new WaveFileWriter(path, format);

        var buffer = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var f = samples[i];
            if (f > 1f) f = 1f;
            else if (f < -1f) f = -1f;

            short pcm = (short)(f * 32767f);
            buffer[i * 2] = (byte)(pcm & 0xFF);
            buffer[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        writer.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Deletes all but the <see cref="RingSize"/> newest dump files. The
    /// <c>yyyyMMdd-HHmmss</c> filename format sorts chronologically as plain
    /// strings, so an ordinal descending sort is enough to find the newest.
    /// </summary>
    private void EnforceRingCap()
    {
        try
        {
            var stale = Directory.GetFiles(_directory, $"{FilePrefix}*.wav")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Skip(RingSize);

            foreach (var file in stale)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(
                        "[EmptyDictationDumpService] Failed to delete stale dump {0}: {1}", file, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[EmptyDictationDumpService] Failed to enforce diagnostics ring cap: {0}", ex.Message);
        }
    }
}
