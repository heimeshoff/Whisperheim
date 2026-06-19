using System.IO;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Ffmpeg;

namespace WhisperHeim.Tests;

/// <summary>
/// Unit tests for the FFmpeg fallback decode path (main-r7n2k). These do NOT run
/// a real FFmpeg binary — they exercise the routing and error-classification:
/// that an unknown extension routes to the FFmpeg fallback, that a missing FFmpeg
/// produces a distinct <see cref="FfmpegRequiredException"/> naming FFmpeg, and
/// that an undecodable file produces the distinct "corrupt or not audio" message.
/// </summary>
public class AudioFileDecoderTests
{
    /// <summary>
    /// A wired detector that reports no FFmpeg (DetectAsync was never run, so
    /// CachedInfo is null) forces the "FFmpeg required" branch for an unknown
    /// extension — without invoking any external process.
    /// </summary>
    [Fact]
    public void Unknown_extension_with_no_ffmpeg_throws_distinct_ffmpeg_required_error()
    {
        var emptyDetector = new FfmpegDetector(); // CachedInfo == null
        AudioFileDecoder.SetDetector(emptyDetector);
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(),
                "whisperheim-decodertest-" + Guid.NewGuid().ToString("N") + ".opus");
            File.WriteAllBytes(tempPath, new byte[] { 1, 2, 3, 4 });
            try
            {
                var ex = Assert.Throws<FfmpegRequiredException>(
                    () => AudioFileDecoder.Decode(tempPath));

                // Distinct, FFmpeg-naming message — separate from the corrupt case.
                Assert.Contains("FFmpeg", ex.Message);
                Assert.Contains("requires", ex.Message);
                Assert.DoesNotContain("corrupt", ex.Message);
            }
            finally
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
        }
        finally
        {
            // Reset the global so other tests aren't affected (static seam).
            AudioFileDecoder.SetDetector(null!);
        }
    }

    /// <summary>
    /// A natively-handled extension fed garbage bytes is undecodable. NAudio
    /// throws, and the decoder wraps it as the distinct "appears corrupt or is not
    /// audio" message — NOT the FFmpeg-required message. No external process.
    /// </summary>
    [Fact]
    public void Corrupt_native_file_throws_distinct_corrupt_error()
    {
        var tempPath = Path.Combine(Path.GetTempPath(),
            "whisperheim-decodertest-" + Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(tempPath, new byte[] { 0xFF, 0x00, 0x13, 0x37, 0x42 });
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => AudioFileDecoder.Decode(tempPath));

            Assert.Contains("corrupt or is not audio", ex.Message);
            // Must be distinct from the FFmpeg-missing case.
            Assert.IsNotType<FfmpegRequiredException>(ex);
            Assert.DoesNotContain("requires FFmpeg", ex.Message);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* ignore */ }
        }
    }
}
