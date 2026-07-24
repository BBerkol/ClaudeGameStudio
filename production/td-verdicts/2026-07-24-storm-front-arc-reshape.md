# TD Verdict — Storm-Front Inverted-Arc Reshape

**Date:** 2026-07-24
**System:** Out-of-fuel V2 storm-front map visual — silhouette reshape
**Files at risk:** `MapViewController.cs`, `MapView.uss` (deletion),
`StormFrontElement.cs` (new), `StormMapVisualHost.cs` (unchanged), storm
event trio (unchanged).

## Context

Slice `e0a1461` (2026-07-24) shipped the sibling `StormMapVisualHost` +
`MapViewController.ShowStormFrontAt` / `PlayStormAdvance` / `HideStormFront`
API driven by the storm event trio. Current visual is a **single 110px
circular red VisualElement** that lerps between beacon positions on the
beacons layer — a puck riding along the beacon lane.

User design change (2026-07-24): the storm front should NOT be a puck. It
should be a **full-map-area silhouette** with an **inverted arc leading
edge** — top and bottom of the map are engulfed farther RIGHT, the middle
(beacon lane) is engulfed farther LEFT, so the "safe channel" reads as a
pinched horizontal band that closes toward the parked player.

## Proposed shape (as briefed)

1. New `StormFrontElement : VisualElement` with `generateVisualContent` +
   `Painter2D` — quadratic bezier bulging LEFT, filled shape covers the
   engulfed area on the map's left.
2. New `_stormLayer` sibling above `_beaconsLayer` in `MapViewController`
   (created at runtime, added to `_beaconsLayer.parent`).
3. `MapViewController` `ShowStormFrontAt` / `PlayStormAdvance` keep their
   index-based signatures — controller internally translates beacon-index
   to normalized-x via `_beaconPositions[index].x`.
4. Serialized `[SerializeField] float _arcConcavityNormalized = 0.15f` on
   `MapViewController` — designer knob for the arc's inward bulge.
5. Delete `.wr-storm-front` USS block (the puck is retired).
6. `StormMapVisualHost` unchanged — the reshape is a MapViewController
   concern.

## Technical Director Review

**Verdict:** APPROVE-WITH-AMENDS

**Answers to design questions**

1. **Painter2D + `generateVisualContent` is correct.** Precedent exists
   (`ConnectionLineElement`); the storm lives on the map canvas — reaching
   for UGUI world-space would breach the ADR-0014 axis-aligned rule (UGUI
   is `Popups` canvas only; map is Toolkit territory).
2. **New `_stormLayer` sibling above `_beaconsLayer`.** Piggybacking on
   `_beaconsLayer` with `BringToFront()` on Show + on every Bind is
   fragile — Bind rebuilds already forget to re-`BringToFront`. Named
   layer makes intent legible and paint order deterministic.
3. **Quadratic bezier, one control point.** Cubic is over-parameterized
   for a single-axis bulge; elliptical `Arc` locks curvature and fights
   aspect-ratio changes. Quadratic with `control = (apexX, midY)` and
   endpoints `(rightEdgeX, 0)` / `(rightEdgeX, height)` gives one knob
   (`apexX`) that maps directly to model state.
4. **Keep `PlayStormAdvance(fromIndex, toIndex, dur)`.** Index→x is a
   MapViewController-internal concern — adding a parallel `...ToApex`
   overload is the bimodal path ADR-0011 forbids.
5. **No `apexY` parameter.** The arc is bounded top/bottom by the
   `_stormLayer` element itself; apex sits at 50% of the element's
   height. If the beacon lane isn't vertically centered, that's a
   Bind-time layer-rect concern, not a per-tick parameter.
6. **Placeholder-then-refine.** One bezier control point is exactly
   the knob playtest will want. Ship the smallest correct shape.

**Three-lens self-audit**

- **Health:** ADR-0011 clean — no bridge, no bimodal API, no vestigial
  USS. Subscription lifecycle unchanged. **AMEND**: on `Bind`, re-parent
  `_stormFront` into `_stormLayer` after the layer clear — mirror
  `EnsurePlayerMarker`'s idempotency so a rebind mid-storm doesn't
  orphan the element.
- **Optimization:** `MarkDirtyRepaint()` on each `SetApex` call = one
  Painter2D repaint per frame during the lerp. Same cadence as today's
  puck (`style.left` write also invalidates). No allocation per frame if
  a `Painter2D` reference is reused. **AMEND**: `generateVisualContent`
  reads `_apexX` off a field, not a closure — closure capture allocates
  on assignment.
- **1.0 survival:** `PlayStormAdvance(fromIndex, toIndex, dur)` survives.
  `StormAdvanceTick` unchanged, host unchanged. Arc curvature becomes
  a serialized field — playtest tuning stays USS-free but not
  code-brittle. **AMEND**: extract `_arcConcavityNormalized = 0.15f`
  on `MapViewController` — you *will* want this knob after first
  playtest.

## Applied AMENDs (implementation notes)

1. `_stormLayer` created at runtime as a sibling of `_beaconsLayer` in
   the same parent, appended AFTER beacons layer (paint order:
   connections < beacons < storm < ...).
2. `.wr-storm-front` USS block deleted (puck retired).
3. `StormFrontElement : VisualElement` with `SetApex(float)` field-write
   + `MarkDirtyRepaint`. No closure — `generateVisualContent` reads the
   field directly.
4. `StormAdvanceCoroutine` lerps normalized-x, calls
   `_stormFront.SetApex(t)`. The `SetStormFrontNormalizedPosition`
   helper is retired.
5. `[SerializeField] private float _arcConcavityNormalized = 0.15f`
   on `MapViewController`; passed to the `StormFrontElement`
   constructor once at `EnsureStormFront`.
6. `_stormLayer` full-width full-height via absolute-inset:0.

## ADR alignment

- ADR-0011 (no bridges) — puck→arc is a clean cut. The old USS block
  is deleted entirely (no vestigial styles). Same `StormMapVisualHost`,
  same `PlayStormAdvance` signature, no bimodal API paths.
- ADR-0014 (UI Toolkit primary) — new element is UI Toolkit
  `VisualElement` with Painter2D, correct stack.
- ADR-0015 (data-flag lagging-dep) — arc concavity knob is a
  serialized designer field on the controller; retunes live without
  code change.
