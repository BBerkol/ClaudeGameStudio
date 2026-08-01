# Storm Move-Preview Overlay — 2026-07-29 (P2 map polish)

## Slice intent

Render a caution-hatched map band showing the canvas region the storm will
engulf on its next strip-advance. Locks in the player's understanding of
the doom-sweep loop: the storm counter tells them WHEN, the band tells them
WHERE. Fulfils memory `project_storm_move_preview` (user request 2026-07-29).

## Files touched (additive)

- `Assets/UI/MapView.uxml` — one new element (`#storm-preview-band`) as
  sibling of `beacons-layer` inside `wr-map-canvas`. Above beacons in
  document order so the hatch reads AS AN OVERLAY on chips inside the
  zone. Starts `.is-hidden`.
- `Assets/UI/MapView.uss` — `.wr-storm-preview-band` rule + `.is-hidden`
  companion. Solid translucent yellow tint (alpha 0.16) with strong 3px
  yellow left+right borders for the caution-tape delimiters, `position:
  absolute`, `top: 0; bottom: 0`. Initially authored with a
  `repeating-linear-gradient` hatch — rejected by UI Toolkit 6.3's USS
  parser at import time (`Unknown function`) AND threw `Invalid value
  for image texture Dimension` at runtime. Recorded as feedback memory
  `feedback_uss_no_repeating_gradient` for future guardrails.
- `Assets/Scripts/UI/MapViewController.cs` — `_stormPreviewBand` field,
  `_lastPreview{Left,Width}Pct` change-detect cache, resolved in
  `ResolveLayersIfPossible`, new public `UpdateStormPreview(cursorX,
  bandWidthNormalized)`, hidden by `SetInputLocked(true)` during the
  storm cinematic.
- `Assets/Scripts/CombatView/RunSceneHost.cs` — new
  `ActiveStormAdvancePerStrip` accessor so the visual host doesn't need
  to grab `_biomeDistribution` directly.
- `Assets/Scripts/CombatView/StormMapVisualHost.cs` — `PaintPreview`
  helper. Initial paint fires after `ShowStormFrontAt`; post-advance
  repaint fires from `ReleaseInputLockAfter` so the band reappears at
  the new cursor location once the cinematic clears.

## Nothing destroyed

Purely additive. No pre-authored asset values, prefab connections,
SerializeFields, or existing scripts were removed. Change-detect cache
ensures no per-frame DOM churn on Bind re-fires (single write on cursor
move, no writes on no-op refresh).

## Technical Director Review

**TD verdict (2026-07-29):** APPROVE with fixes (all addressed above).

- **Health.** Reads clean against the existing storm-cinematic surface —
  same public entry-point pattern as `ShowStormFrontAt` +
  `PlayStormAdvance`. The `SetInputLocked` hide + post-release repaint
  keeps the band and the moving arc from fighting each other.
- **Optimization.** Change-detect cache `(lastLeftPct, lastWidthPct)`
  skips DOM writes on no-op re-fires — Bind can call at will without
  churning style dirty flags. Single UXML element, single USS rule, no
  Painter2D, no coroutine.
- **1.0 survival.** Width sourced from `BiomeDistributionSO.StormAdvancePerStrip`
  so Biome 2/3 can retune without controller changes. Per-beacon storm
  weighting (ADR-0015 lagging-dep) will refine the semantic later; the
  band shows "one strip's worth of doom" today, which is the honest
  floor when weights aren't yet in play.

**Applied fixes from TD review:**
- Preview band hidden during cinematic + auto-repainted after
  `ReleaseInputLockAfter` (avoids fighting the arc/vignette).
- Sentinel-NaN cache prevents first-frame false-hits.
- Off-canvas cursor (CursorX ≥ 1 or width clamps to 0) snaps to hidden
  instead of leaving a stale strip on the right edge.

## unity-ui-specialist verdict

APPROVE. Concrete fix landed: `Length` assignment to `style.left/width`
uses the existing local convention (implicit conversion, matches
`SetMarkerNormalizedPosition` at MapViewController.cs:427).
- `repeating-linear-gradient` syntax confirmed valid in UI Toolkit 6.3.
- `picking-mode="Ignore"` in UXML is idiomatic.
- Two-float change-detect cache is Unity-runtime-binding-idiomatic.

## User-side verify

1. Enter a run → map shows the caution band starting at the current
   storm-cursor X, spanning one `StormAdvancePerStrip` width to the right.
2. Travel to a beacon that triggers a strip advance → cinematic plays
   with the band hidden → after the input-lock releases, the band
   re-appears at the new cursor location.
3. Travel to a beacon that does NOT trigger a strip → band stays where
   it was (cursor didn't move).
4. Near-end-of-run when the cursor is nearly at the right edge → band
   truncates cleanly at the canvas edge (no overflow past the vignette).
