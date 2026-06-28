using System.Windows;
using WhisperHeim.Views;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Drives the overlay placement seam (task main-p3k9d): the pill must rest at the
/// bottom-center of the work area on every show — including the very first one after
/// launch, which previously rendered top / two-thirds-from-left because positioning
/// ran before the window had an HWND (and was DPI-resolved) and the post-Show reposition
/// was gated behind a "has been loaded" flag.
///
/// The Show()/DPI/lifecycle plumbing is a WPF-integration concern verified manually via
/// /deploy; this pins the pure geometry that <see cref="DictationOverlayWindow.PositionAtBottomCenter"/>
/// delegates to, so the centering math can never silently regress.
/// </summary>
public class DictationOverlayPositionTests
{
    [Fact]
    public void ComputeBottomCenter_CentersHorizontallyOnPrimaryWorkArea()
    {
        // 1920x1080, 48px bottom taskbar -> work area (0,0,1920,1032).
        var (left, top) = DictationOverlayWindow.ComputeBottomCenter(
            new Rect(0, 0, 1920, 1032), width: 100, height: 40, bottomMargin: 20);

        Assert.Equal(910, left);   // (1920 - 100) / 2
        Assert.Equal(972, top);    // 1032 - 40 - 20
    }

    [Fact]
    public void ComputeBottomCenter_HonorsOffsetWorkArea()
    {
        // Left/top taskbar or non-origin work area: offsets must carry through.
        var (left, top) = DictationOverlayWindow.ComputeBottomCenter(
            new Rect(100, 50, 1820, 1000), width: 100, height: 40, bottomMargin: 20);

        Assert.Equal(960, left);   // 100 + (1820 - 100) / 2
        Assert.Equal(990, top);    // (50 + 1000) - 40 - 20
    }

    [Fact]
    public void ComputeBottomCenter_WorksOnScaledWorkAreaInDips()
    {
        // 1920px physical at 150% scale -> 1280 DIP wide work area. The math is the
        // same in DIP space; the bug was never the math but where/when it ran.
        var (left, top) = DictationOverlayWindow.ComputeBottomCenter(
            new Rect(0, 0, 1280, 688), width: 100, height: 40, bottomMargin: 20);

        Assert.Equal(590, left);   // (1280 - 100) / 2
        Assert.Equal(628, top);    // 688 - 40 - 20
    }
}
