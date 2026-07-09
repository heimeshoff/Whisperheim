using WhisperHeim.Services.Export;

namespace WhisperHeim.Tests;

/// <summary>
/// Table-driven coverage of every branch of <see cref="ExportFileName.Sanitize"/>'s
/// rule (task main-m6x4v): invalid characters, trailing dots/spaces, reserved
/// device names, the 120-char length cap, and the empty-after-sanitize fallback.
/// </summary>
public class ExportFileNameTests
{
    private static readonly DateTimeOffset Fallback =
        new(2026, 7, 9, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Sanitize_LeavesOrdinaryNameUnchanged()
    {
        Assert.Equal("Call with Alice", ExportFileName.Sanitize("Call with Alice", Fallback));
    }

    [Theory]
    [InlineData("a<b>c", "a_b_c")]
    [InlineData("a:b", "a_b")]
    [InlineData("\"quoted\"", "_quoted_")]
    [InlineData("a/b\\c", "a_b_c")]
    [InlineData("pipe|question?star*", "pipe_question_star_")]
    public void Sanitize_ReplacesInvalidCharsWithUnderscore(string input, string expected)
    {
        Assert.Equal(expected, ExportFileName.Sanitize(input, Fallback));
    }

    [Theory]
    [InlineData("trailing dots...", "trailing dots")]
    [InlineData("trailing spaces   ", "trailing spaces")]
    [InlineData("mixed. . ", "mixed")]
    public void Sanitize_TrimsTrailingDotsAndSpaces(string input, string expected)
    {
        Assert.Equal(expected, ExportFileName.Sanitize(input, Fallback));
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("con", "_con")]
    [InlineData("PRN", "_PRN")]
    [InlineData("AUX", "_AUX")]
    [InlineData("NUL", "_NUL")]
    [InlineData("COM1", "_COM1")]
    [InlineData("COM9", "_COM9")]
    [InlineData("LPT1", "_LPT1")]
    [InlineData("LPT9", "_LPT9")]
    public void Sanitize_PrefixesReservedDeviceNames(string input, string expected)
    {
        Assert.Equal(expected, ExportFileName.Sanitize(input, Fallback));
    }

    [Fact]
    public void Sanitize_ReservedNameCheckIgnoresExtension()
    {
        // "CON.important" is just as reserved as bare "CON" on Windows.
        Assert.Equal("_CON.important", ExportFileName.Sanitize("CON.important", Fallback));
    }

    [Fact]
    public void Sanitize_DoesNotFlagNonReservedNamesContainingReservedSubstrings()
    {
        // "CONference" is not the reserved name "CON" — must not be prefixed.
        Assert.Equal("CONference", ExportFileName.Sanitize("CONference", Fallback));
    }

    [Fact]
    public void Sanitize_TruncatesTo120Characters()
    {
        var input = new string('a', 200);
        var result = ExportFileName.Sanitize(input, Fallback);

        Assert.Equal(120, result.Length);
        Assert.Equal(new string('a', 120), result);
    }

    [Fact]
    public void Sanitize_ReTrimsDanglingSeparatorLeftByTruncation()
    {
        // Character at index 119 (the 120th character) is a dot, which the
        // truncation-to-120 cut would otherwise leave dangling at the end.
        var input = new string('a', 119) + "." + new string('b', 50);
        var result = ExportFileName.Sanitize(input, Fallback);

        Assert.Equal(119, result.Length);
        Assert.False(result.EndsWith('.'));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData(" . . . ")]
    public void Sanitize_FallsBackToTimestampName_WhenEmptyAfterSanitizing(string input)
    {
        var result = ExportFileName.Sanitize(input, Fallback);

        Assert.Equal("transcript_20260709_143000", result);
    }

    [Fact]
    public void Sanitize_TreatsNullNameAsEmpty()
    {
        var result = ExportFileName.Sanitize(null, Fallback);

        Assert.Equal("transcript_20260709_143000", result);
    }
}
