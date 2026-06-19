using WhisperHeim.Cli;

namespace WhisperHeim.Cli.Tests;

public class CliCoreTests
{
    // --- ParseArgs ---

    [Fact]
    public void ParseArgs_returns_path_for_single_positional()
    {
        Assert.Equal(@"C:\audio\clip.ogg", CliCore.ParseArgs(new[] { @"C:\audio\clip.ogg" }));
    }

    [Fact]
    public void ParseArgs_returns_null_for_no_args()
    {
        Assert.Null(CliCore.ParseArgs(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void ParseArgs_returns_null_for_help_flags(string flag)
    {
        Assert.Null(CliCore.ParseArgs(new[] { flag }));
    }

    [Fact]
    public void ParseArgs_returns_null_for_second_positional()
    {
        Assert.Null(CliCore.ParseArgs(new[] { "a.ogg", "b.ogg" }));
    }

    // --- ResolveEndpoint ---

    [Fact]
    public void ResolveEndpoint_uses_default_when_env_missing()
    {
        Assert.Equal("http://127.0.0.1:7777", CliCore.ResolveEndpoint(null));
        Assert.Equal("http://127.0.0.1:7777", CliCore.ResolveEndpoint(""));
        Assert.Equal("http://127.0.0.1:7777", CliCore.ResolveEndpoint("   "));
    }

    [Fact]
    public void ResolveEndpoint_uses_env_when_set()
    {
        Assert.Equal("http://localhost:9000", CliCore.ResolveEndpoint("http://localhost:9000"));
    }

    // --- BuildTranscribeUrl ---

    [Fact]
    public void BuildTranscribeUrl_appends_transcribe_with_filename_hint()
    {
        var url = CliCore.BuildTranscribeUrl("http://127.0.0.1:7777", @"C:\audio\clip.ogg");
        Assert.Equal("http://127.0.0.1:7777/transcribe?filename=clip.ogg", url);
    }

    [Fact]
    public void BuildTranscribeUrl_trims_trailing_slash_on_endpoint()
    {
        var url = CliCore.BuildTranscribeUrl("http://127.0.0.1:7777/", @"C:\audio\clip.ogg");
        Assert.Equal("http://127.0.0.1:7777/transcribe?filename=clip.ogg", url);
    }

    [Fact]
    public void BuildTranscribeUrl_url_encodes_spaces_in_filename()
    {
        var url = CliCore.BuildTranscribeUrl("http://127.0.0.1:7777", @"C:\audio\voice message.m4a");
        Assert.Equal("http://127.0.0.1:7777/transcribe?filename=voice%20message.m4a", url);
    }

    // --- ExtractText ---

    [Fact]
    public void ExtractText_returns_transcript_for_valid_response()
    {
        var json = "{\"text\":\"hello world\",\"audioDurationSeconds\":42.13,\"chunkCount\":3}";
        Assert.Equal("hello world", CliCore.ExtractText(json));
    }

    [Fact]
    public void ExtractText_returns_empty_string_for_no_speech_audio()
    {
        var json = "{\"text\":\"\",\"audioDurationSeconds\":1.0,\"chunkCount\":0}";
        Assert.Equal("", CliCore.ExtractText(json));
    }

    [Fact]
    public void ExtractText_returns_null_when_text_field_absent()
    {
        var json = "{\"error\":\"something broke\"}";
        Assert.Null(CliCore.ExtractText(json));
    }
}
