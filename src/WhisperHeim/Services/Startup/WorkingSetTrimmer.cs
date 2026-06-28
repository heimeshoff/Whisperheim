using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WhisperHeim.Services.Startup;

/// <summary>
/// Voluntarily releases the process working set so cold, committed-but-untouched
/// pages move to the OS standby list, dropping the resident-set number Task
/// Manager reports (task infrastructure-w7k9p).
///
/// <para>
/// This does <em>not</em> unload anything: the INT8 Parakeet recognizer (~640 MB)
/// stays committed, and the hot decode pages simply re-fault back in on the next
/// Ctrl+Win dictation, usually imperceptibly. It trades a slightly colder first
/// access after idle for a lower reported footprint — that trade-off is the whole
/// point. Committed (private) memory stays roughly flat; only the working set drops.
/// </para>
///
/// <para>
/// Windows-only and fully failure-isolated: on a non-Windows host it is a no-op,
/// and a failed P/Invoke is logged and swallowed so memory housekeeping can never
/// destabilize the running app. It pairs with <see cref="StartupMemoryCompactor"/>
/// as the "trim" half of "compact, then trim" (ADR-0003).
/// </para>
/// </summary>
public sealed class WorkingSetTrimmer
{
    // EmptyWorkingSet removes as many pages as possible from the process working
    // set, leaving them on the standby list (faulted back in on next touch).
    // Exported by psapi.dll; the kernel32 K32EmptyWorkingSet forwarder is the
    // same call.
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    /// <summary>
    /// Trims the current process's working set. Best-effort and non-fatal:
    /// returns <c>false</c> (after logging) instead of throwing on any failure or
    /// on a non-Windows host.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the trim P/Invoke succeeded; <c>false</c> if it was skipped
    /// (non-Windows) or failed (logged).
    /// </returns>
    public bool Trim()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        return TrimWindows();
    }

    [SupportedOSPlatform("windows")]
    private static bool TrimWindows()
    {
        try
        {
            // Process.Handle for the current process is the pseudo-handle; safe to
            // pass and never needs closing.
            bool ok = EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            if (!ok)
            {
                Trace.TraceWarning(
                    "[WorkingSetTrimmer] EmptyWorkingSet returned false (Win32 error {0}); continuing.",
                    Marshal.GetLastWin32Error());
            }

            return ok;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[WorkingSetTrimmer] Working-set trim failed: {0}; continuing.",
                ex.Message);
            return false;
        }
    }
}
