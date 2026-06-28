using WhisperHeim.Services.Orchestration;
using WhisperHeim.Services.Transcription;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Drives the "warming up" overlay decision seam (task infrastructure-q4t8m,
/// ADR-0005/0006): on release of a held dictation, the overlay shows the
/// pulsing-amber WarmingUp state exactly when transcribe-on-release must await
/// an in-flight model load — i.e. when the recognizer is not yet
/// <see cref="ModelResidencyState.Loaded"/>. The visual pulse and the deferred
/// overlay-hide are WPF/integration concerns verified manually via /deploy; this
/// covers the pure release-time decision (AC1 / AC4).
/// </summary>
public class DictationOverlayWarmUpTests
{
    [Fact]
    public void ShouldWarmUpOnRelease_WhenLoading_IsTrue()
    {
        // Release arrived before the key-down load finished — the worst-case short
        // utterance the warming state must make legible.
        Assert.True(DictationOrchestrator.ShouldWarmUpOnRelease(ModelResidencyState.Loading));
    }

    [Fact]
    public void ShouldWarmUpOnRelease_WhenUnloaded_IsTrue()
    {
        // No load even started yet (e.g. straight after an idle-unload) — still must warm.
        Assert.True(DictationOrchestrator.ShouldWarmUpOnRelease(ModelResidencyState.Unloaded));
    }

    [Fact]
    public void ShouldWarmUpOnRelease_WhenLoaded_IsFalse()
    {
        // Common case: the load finished while the user was speaking — no warming flash.
        Assert.False(DictationOrchestrator.ShouldWarmUpOnRelease(ModelResidencyState.Loaded));
    }

    [Fact]
    public void ShouldWarmUpOnRelease_WhenNoLifecycle_IsFalse()
    {
        // Eager-load builds with no lifecycle manager wired never warm up.
        Assert.False(DictationOrchestrator.ShouldWarmUpOnRelease(null));
    }
}
