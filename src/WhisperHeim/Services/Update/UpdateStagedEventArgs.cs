using System;

namespace WhisperHeim.Services.Update;

/// <summary>
/// Raised by <see cref="UpdateService"/> when a newer release has been
/// downloaded and staged and is waiting to be applied. Carries the version the
/// footer surfaces as "Update ready: vX.Y".
/// </summary>
public sealed class UpdateStagedEventArgs : EventArgs
{
    public UpdateStagedEventArgs(string version) => Version = version;

    /// <summary>The staged release version (e.g. <c>"0.4.0"</c>), no <c>v</c> prefix.</summary>
    public string Version { get; }
}
