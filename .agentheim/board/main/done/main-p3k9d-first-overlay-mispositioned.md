---
id: main-p3k9d
title: First dictation overlay renders at wrong position (not bottom-center)
status: done
type: bug
context: main
created: 2026-06-28
completed: 2026-06-28
depends_on: []
blocks: []
tags: [ui, overlay, dictation, bug]
related_adrs: []
related_research: []
prior_art: [main-068, main-070, main-012, main-025]
---

## Why
The dictation overlay (the pill) is meant to appear at the **bottom-center** of the
screen every time dictation starts. On the very first dictation after the app launches,
it instead renders near the **top of the screen, ~two-thirds from the left** — a visibly
broken first impression for the app's most-used feature. Every subsequent dictation in
the same session positions correctly, so the bug is specific to the first show.

## What
`DictationOverlayWindow.PositionAtBottomCenter()` computes its coordinates from the WPF
`Width` / `Height` properties:

```
Left = workArea.Left + (workArea.Width - Width) / 2;
Top  = workArea.Bottom - Height - 20;
```

For the auto-sized overlay window, `Width` / `Height` are `NaN` until the window has been
measured and arranged. On the **first** `ShowOverlay()`:

1. `PositionAtBottomCenter()` runs *before* `Show()`, while `Width`/`Height` are still `NaN`
   → the arithmetic yields `NaN`, so WPF falls back to a default placement (the observed
   top / two-thirds-from-left position).
2. The post-`Show()` reposition is **guarded by `_hasBeenLoaded`**, which is still `false`
   on the first show (it's only set to `true` inside `OnLoaded`), so the bad position is
   never corrected for that first appearance.

On later shows the window already carries the corrected `Left`/`Top` from a prior pass and
`_hasBeenLoaded` is `true`, so positioning looks right — masking the first-show defect.

Fix direction (for the worker to confirm): position from `ActualWidth`/`ActualHeight` (or
defer positioning until after layout/`Show()` when real dimensions exist), and ensure the
first show gets the same post-`Show()` reposition the `_hasBeenLoaded` branch already does
for subsequent shows. Keep the existing `OnLoaded` reposition that guards against
`SetClickThrough()` moving the window.

Relevant code: `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs`
— `PositionAtBottomCenter()` (~line 410), `ShowOverlay()` (~line 248), `OnLoaded()` (~line 95).

## Acceptance criteria
- [ ] The **first** dictation overlay after app start renders at bottom-center (same position
      as all subsequent overlays — horizontally centered, ~20px above the work-area bottom).
- [ ] No regression for subsequent shows or when the work area / screen resolution changes
      between shows.
- [ ] No visible flash or jump of the overlay from a wrong position to the correct one on
      first show (it should appear at bottom-center, not slide into place).
- [ ] Behavior holds on the primary display; multi-monitor placement is unchanged from
      current behavior (this fix is not scoped to add multi-monitor follow-the-cursor logic).

## Notes
- Prior art: `main-068` (pill overlay positioning), `main-070` (fix pill overlay
  visualization), `main-012` (original dictation overlay), `main-025` (overlay mic-state
  visualization). The doc comment on `ShowOverlay()` still says "at the last clicked
  position" but the implementation has since moved to fixed bottom-center — worth correcting
  that stale comment while in the file.
- The recent `infrastructure-q4t8m` "warming-up" work also touches overlay show/hide timing
  (deferred-hide). This bug is orthogonal (positioning, not state), but the worker should be
  aware of the show/hide lifecycle when testing.

## Outcome
Root cause corrected during work: the window is **not** auto-sized — the XAML has carried
explicit `Width="100" Height="40"` since March, so `Width`/`Height` were never `NaN`. The real
defect was *ordering + DPI context*: `PositionAtBottomCenter()` ran **before** `Show()`, so the
coordinates were set without an HWND (no monitor/DPI resolution), and the only post-`Show()`
reposition was gated behind `_hasBeenLoaded`, which is still `false` on the first show. First
show therefore kept the pre-`Show()` (DPI-unaware) placement; later shows hit the
`_hasBeenLoaded` branch and looked correct.

Fix in `Views/DictationOverlayWindow.xaml.cs`:
- `ShowOverlay()` now calls `Show()` **first**, then `PositionAtBottomCenter()` unconditionally.
  The window's `Opacity` starts at 0 (XAML) and the fade-in begins only after positioning, so
  it is never visible at the wrong spot — no flash/jump (AC3).
- `PositionAtBottomCenter()` now prefers `ActualWidth`/`ActualHeight` (valid post-`Show()`),
  falling back to `Width`/`Height`.
- Removed the now-dead `_hasBeenLoaded` field and its first-show guard; kept the `OnLoaded`
  reposition that compensates for `SetClickThrough()` adding `WS_EX_TOOLWINDOW`.
- Extracted the placement geometry into pure `internal static ComputeBottomCenter(Rect, w, h, margin)`
  and pinned it with 3 xUnit tests (`tests/WhisperHeim.Tests/DictationOverlayPositionTests.cs`):
  primary, offset, and DPI-scaled work areas. The `Show()`/DPI lifecycle itself is a WPF
  integration concern verified manually via `/deploy`, mirroring the `DictationOverlayWarmUpTests`
  seam convention.
- Corrected three stale "last clicked position" / "global mouse hook" doc comments to say
  bottom-center.

AC2 (no regression on subsequent shows / work-area change) and AC4 (multi-monitor unchanged):
the post-`Show()` reposition runs on every show and reads the current `SystemParameters.WorkArea`;
multi-monitor follow-cursor logic was neither present nor added.

Full suite green: 169/169.

Key files:
- `src/WhisperHeim/Views/DictationOverlayWindow.xaml.cs`
- `tests/WhisperHeim.Tests/DictationOverlayPositionTests.cs` (new)

## Follow-up fix (2026-06-28) — the first-show bug was still live on a scaled ultrawide

The original fix above did **not** actually resolve the reported symptom on the maintainer's hardware (single 57" Samsung Odyssey G95NC super-ultrawide, 7680×2160 native, **125% scaling**, so a 6144×1728 DIP desktop). The first dictation overlay after launch still rendered top-right; every subsequent one was correct. Root-caused live via a Win32 `GetWindowRect` poller (the app is `SYSTEM_AWARE`, no DPI manifest):

- **The position is governed solely by WPF `Left`/`Top`, not by any post-`Show()` Win32 placement.** A detour that tried to place the pill in physical pixels via `SetWindowPos` was *always overridden* by WPF's `Left`/`Top` reconciliation (the window snapped back to the DIP coordinate on every show) — confirmed and abandoned.
- **The defect is a first-`Show()` layout/DPI settling glitch.** The poller captured the very first show inflating to a transient `4608×1240 @ (307,307)` then resting at `(4611,13)` — top, right. The *second* and all later shows rest at the correct `(3022,1620)`. So the window only mis-settles the first time its HWND is realized.

**Fix shipped:** `DictationOverlayWindow.PrewarmFirstShow()` runs that throwaway first-`Show()` once at startup, invisibly (Opacity 0, `ShowInTaskbar`/`ShowActivated` false), hiding it after one dispatcher cycle so the settling completes. `App.InitializeOverlay` calls it right after constructing the overlay. The first *real* dictation is then already a "second show" and lands bottom-center. The Win32 `SetWindowPos` detour was reverted — positioning stays the existing DIP `PositionAtBottomCenter`.

**Verified live (2026-06-28):** poller caught the pre-warm absorbing the `(4611,13)` glitch at startup (invisible), then the user's first real dictation at the correct `(3022,1620)`; maintainer confirmed "it works now." Full suite green: 169/169.

**Known residual (not fixed here, separate concern):** because the app is `SYSTEM_AWARE` rather than Per-Monitor-V2, the pill renders at 100×40 *physical* (unscaled) instead of 125×50, and `(3022,1620)` is the DIP-derived center rather than the true physical center (~3778). The maintainer accepted this position. A proper Per-Monitor-V2 DPI manifest would make size + position exact and consistent, but it is an app-wide change (affects every window) and was deliberately not bundled into this targeted fix.
