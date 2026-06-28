using System;
using WhisperHeim.Services.Startup;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Verifies the Win32 working-set trim helper (task infrastructure-w7k9p): it
/// trims the current process working set without throwing, and its failure path
/// is non-fatal. The trim moves cold pages to standby; it does not unload the
/// resident Parakeet recognizer (which lives in native memory the trim cannot
/// free anyway).
/// </summary>
public class WorkingSetTrimmerTests
{
    [Fact]
    public void Trim_NeverThrows_AndReportsOutcome()
    {
        var trimmer = new WorkingSetTrimmer();

        bool result = trimmer.Trim();

        if (OperatingSystem.IsWindows())
        {
            // EmptyWorkingSet on the current process succeeds under normal
            // privileges; the call is the load-bearing assertion that the
            // P/Invoke signature and marshalling are correct.
            Assert.True(result);
        }
        else
        {
            // Non-Windows host: guarded no-op, reported as false, never throws.
            Assert.False(result);
        }
    }

    [Fact]
    public void Trim_IsRepeatable_WithoutThrowing()
    {
        var trimmer = new WorkingSetTrimmer();

        // Trimming twice in a row must stay safe (idle + startup can both fire).
        trimmer.Trim();
        trimmer.Trim();
    }
}
