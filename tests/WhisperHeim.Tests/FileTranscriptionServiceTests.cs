using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Tests;

/// <summary>
/// Unit tests for <see cref="FileTranscriptionService"/>'s acceptance gate
/// (main-r7n2k): IsSupported is now permissive — it accepts any file with an
/// extension and lets the decoder be the authority, instead of rejecting
/// anything outside a fixed {.wav,.mp3,.m4a,.ogg} allowlist.
/// </summary>
public class FileTranscriptionServiceTests
{
    private sealed class StubTranscriptionService : ITranscriptionService
    {
        public bool IsLoaded => true;
        public void LoadModel() { }
        public void Dispose() { }
        public Task<TranscriptionResult> TranscribeAsync(
            float[] samples, int sampleRate = 16000, CancellationToken ct = default)
            => Task.FromResult(new TranscriptionResult(string.Empty, TimeSpan.Zero, TimeSpan.Zero, 0));
    }

    private static FileTranscriptionService NewService()
        => new(new StubTranscriptionService());

    [Theory]
    [InlineData("voice.opus")]   // not in the old allowlist — must now be accepted
    [InlineData("clip.flac")]
    [InlineData("song.aac")]
    [InlineData("recording.ogg")] // native formats still accepted
    [InlineData("a.wav")]
    public void IsSupported_accepts_any_file_with_an_extension(string fileName)
    {
        Assert.True(NewService().IsSupported(fileName));
    }

    [Fact]
    public void IsSupported_rejects_a_file_with_no_extension()
    {
        Assert.False(NewService().IsSupported("README"));
    }

    [Fact]
    public void SupportedExtensions_remains_a_display_hint_of_native_formats()
    {
        // It is no longer the truth source for acceptance, but the file picker
        // still shows it. Native formats stay listed.
        var exts = NewService().SupportedExtensions;
        Assert.Contains(".ogg", exts);
        Assert.Contains(".wav", exts);
    }
}
