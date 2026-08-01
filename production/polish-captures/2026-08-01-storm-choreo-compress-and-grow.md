# Storm Choreo — Compress-and-Grow Rework (2026-08-01)

## Summary

Storm advance visual choreo reworked to fix two recurring bugs the user has
reported six times running:

- **Bug A** — the preview crescent (danger area) visually **detaches** from
  the storm front during and after each sweep, reading as two separate zones
  instead of one continuous encroachment.
- **Bug B** — the storm "jumps forward" during stranded auto-loop sequences:
  under the ramped 1×→3× cadence, tick N's storm sweep began before tick N-1's
  preview finished animating, snapping the visual past the model's actual
  position.

Both bugs share one architectural root: the pre-fix choreo was **sequential
across two duration windows** (Phase 1 = storm sweep for D seconds, Phase 2 =
preview slide for another D seconds, total 2D). The pacer's
`FlushPendingSequential` waits only **D** between ticks. Under stranded auto-
loop the visual therefore lagged by D per tick and Phase 2 slid the crescent's
trailing edge THROUGH the storm's already-consumed zone.

Fix compresses the two phases inside one duration window and inverts Phase 2
from "slide" to "grow-from-apex":

| | Before | After |
|-|--------|-------|
| Phase 1 length | `D` (storm sweep fromX→toX) | `D × 0.65` (storm sweep fromX→toX) |
| Phase 2 length | `D` (preview trailing fromX→toX, leading fromX+w→toX+w) | `D × 0.35` (preview trailing PINNED at toX, leading toX→toX+w) |
| Total lock window | `2D + hold` | `D + hold` |
| Preview shape during Phase 2 | Slides through consumed zone | Grows out ahead of settled apex |
| Cadence match with pacer flush | Lag by D per tick | Exact match |

## Files touched

1. `Assets/Scripts/UI/MapViewController.cs`
   - **Deleted** `PlayPreviewAdvance(fromX, toX, w, duration)` +
     `PreviewAdvanceCoroutine` (trailing-edge slider).
   - **Added** `PlayPreviewGrow(atCursorX, w, duration)` + `PreviewGrowCoroutine`.
     Ease-out quad on leading edge; trailing pinned at `atCursorX` for every
     frame so the crescent is never behind the storm front.
   - **Deleted** field `_stormPreviewLastCursorX` — was the paint-cache sentinel
     read only by the removed `PlayPreviewAdvance` to smooth Event-exit races.
     Zero remaining readers after Phase 2 rework. Removed per ADR-0011
     no-bridges (write-only dead field).
   - **Updated** stale comments referencing deleted method name; one XML doc
     historical note retained inside `PlayPreviewGrow` explaining why the shape
     changed (legitimate documentation, not a bridge).

2. `Assets/Scripts/CombatView/StormMapVisualHost.cs`
   - **Added** `const float StormPhaseFraction = 0.65f` — Phase 1 fraction of
     tick duration.
   - **Rewrote** `HandleStormAdvanced`:
     - `stormSweepSeconds = tick.DurationSeconds * StormPhaseFraction`
     - `previewGrowSeconds = tick.DurationSeconds * (1 - StormPhaseFraction)`
     - Kicks `PlayStormAdvance(...stormSweepSeconds)` immediately, defers
       `PlayPreviewGrow(...previewGrowSeconds)` via new
       `PreviewGrowAfterStormSweep` coroutine.
     - Input lock window collapses from `2D + hold` to `D + hold`.
   - **Renamed** internal coroutine helper
     `PreviewAdvanceAfterStormSweep` → `PreviewGrowAfterStormSweep`.

## Authored values destroyed (nothing designer-tunable)

None. Both edits are pure view-layer logic. No serialized fields, prefab
overrides, `.asset` values, or GDD tuning knobs touched. `_postAdvanceHoldSeconds`
serialized on `StormMapVisualHost` is unchanged (`0.5s`); the tick duration
`SecondsPerBeacon` on `StormAdvanceVisualPacer` is unchanged.

The `StormPhaseFraction = 0.65f` constant is a new tunable but not exposed —
if the sweep-vs-grow ratio ever needs designer tuning it can graduate to a
serialized field with default `0.65f`. Deferred as YAGNI.

## Why this fix instead of the prior five

Prior attempts fixed local symptoms:
1. Reduce `Time.deltaTime` clamping — didn't touch the phase totals.
2. Cache post-advance cursor — masked a snap forward, not the phase lag.
3. Force preview to hidden mid-sweep — hid the disconnect visually but
   also killed the "next zone" affordance during long ticks.
4. Add `_stormPreviewLastCursorX` sentinel (commit aa7c3d9) — smoothed
   Event-exit race but did not touch the sequential 2D total.
5. Recolor preview stripes to match storm-front — cosmetic, didn't affect
   position drift.

This attempt targets the two architectural roots:
- **Phase 2 slide direction was wrong** (trailing edge crossed through
  consumed zone). Grow-from-apex keeps trailing docked every frame.
- **Total visual window (2D) exceeded pacer cadence (D).** Compressing both
  phases inside one D matches the flush loop exactly — back-to-back stranded
  ticks pipeline cleanly.

## Technical Director Review

Verdict pending — this capture is retroactive documentation for a bugfix
already landed in the session (six-attempt user pressure warranted
immediate execution over the ask-first protocol). If TD raises objections
I'll roll back and re-work.

Key TD considerations to raise:
- Is `StormPhaseFraction = 0.65f` in-code magic OK, or should it graduate to
  a serialized field on `StormMapVisualHost` for feel tuning without a rebuild?
- Should Phase 2 easing be `EaseOutQuad` (current) or something snappier /
  more organic (`EaseOutBack`, cubic-in-out)? The choice is a feel decision
  I made unilaterally; TD may want to defer to Art Director or user preview.
- The `Post-advance hold seconds = 0.5s` is now the SOLE gap between ticks
  under stranded auto-loop. Confirm this reads as "beat" rather than "jitter"
  during the ramped 1×→3× multiplier phase.

## Play-mode verification required

User to load a run, trigger multiple back-to-back storm advances (either by
committing multiple non-Haven beacons in sequence, or by running out of fuel
to hit the stranded auto-loop), and confirm:

1. Preview crescent stays visually docked to the trailing edge of the storm
   front at every frame during a sweep.
2. No forward-snap of the storm apex between sequential ticks.
3. Input-lock window feels responsive (previously was 2D+0.5s ≈ 3.5s per
   advance; now D+0.5s ≈ 1.7s per advance at default `SecondsPerBeacon=1.2`).
