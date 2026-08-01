# TD Verdict — Storm Preview Arc Crescent (2026-07-29)

> **Superseded (in part) 2026-07-31** by
> `production/td-verdicts/2026-07-31-storm-cost-single-source-of-truth.md`.
> Amendment 1's Route-D two-field decision (per-choice storm cost lives on
> `EventPayloadDefinitionSO._perChoiceStormCost[]` mirror) is RETIRED — the
> payload-side mirror was never populated on shipped assets, silently
> broke storm decrement on event commit, and violated ADR-0011 parallel
> storage. Storm cost now lives ONLY on `DialogueChoiceSO._stormCost`,
> projected through `IDialogueChoiceData.StormCost`. The visual work in
> this verdict (crescent Painter2D + StormArcMath extraction) stands.

## Slice intent

Replace the initial storm-preview solid-fill USS rectangle with a
`Painter2D`-based crescent element that mirrors the storm-front's arc
curvature exactly. Fill is translucent red (brighter than
`StormFrontElement`'s "already consumed" dark red), no stroke, no
border. Parents to the map root (not the canvas) so it hits flush with
the screen left/right edges, matching the `_stormLayer` /
`_stormVignette` precedent.

## Files touched

- **NEW** `Assets/Scripts/UI/Elements/StormPreviewBandElement.cs` —
  sealed VisualElement with `Painter2D` generateVisualContent, paints
  the crescent between two quadratic-bezier arcs (trailing = current
  cursor, leading = cursor + `StormAdvancePerStrip`). No stroke, no
  border, pickingMode Ignore.
- **NEW** `Assets/Scripts/UI/Elements/StormArcMath.cs` — public static
  helper (`ControlX(apexPx, concavityPx)`) shared by
  `StormFrontElement` + `StormPreviewBandElement` so a future arc-shape
  retune propagates to both elements without drift. Addresses TD
  amendment: two callers today → extract now before a third lands.
- **MOD** `Assets/Scripts/UI/Elements/StormFrontElement.cs` — one-line
  swap to call `StormArcMath.ControlX` instead of inlined
  `apexPx - concavityPx` at both the fill + stroke paths.
- **MOD** `Assets/Scripts/UI/MapViewController.cs` — swap
  `_stormPreviewBand` field from `VisualElement` to
  `StormPreviewBandElement`, delete NaN change-detect cache, add
  `EnsureStormPreviewBand()` (parents to `_canvas.parent`), rewrite
  `UpdateStormPreview` to compute trailing + leading apex X in
  canvas-space, remap both through `RemapCanvasXToStormLayerX`, and
  call `SetRange`. `SetInputLocked(true)` hides via
  `style.display = None`. `ResolveLayersIfPossible` no longer resolves
  a UXML element.
- **MOD** `Assets/UI/MapView.uxml` — `#storm-preview-band` element
  already removed.
- **MOD** `Assets/UI/MapView.uss` — `.wr-storm-preview-band` rule
  already removed.

## Technical Director Review

**Verdict: APPROVE with one amendment (already incorporated).**

### Lens 1 — Codebase Health
- ADR-0011 clean: old UXML/USS rules and NaN sentinel deleted in the
  same commit; no bridge left behind.
- Subscription lifecycle: `EnsureStormPreviewBand()` is lazy-create
  matching `EnsureStormFront`; hide-during-cinematic uses same
  visibility toggle path (style.display), not SetActive/RemoveFromHierarchy.
- **Amendment (incorporated):** extract shared `StormArcMath.ControlX`
  helper. Two elements would otherwise duplicate apex/concavity math;
  ADR-0011 two-caller rule says extract before the third caller lands.

### Lens 2 — Optimization
- Skip-repaint guard on unchanged `(cursor, stripWidth)` — Painter2D
  MarkDirtyRepaint triggers full mesh rebuild; guard is correct.
- Zero per-frame allocation — Painter2D reuses internal buffers, bezier
  calls are struct-based.
- Cadence is milestone-shaped (per-beacon-advance), not per-frame.

### Lens 3 — 1.0-Shape Survival
- `UpdateStormPreview(cursorX, stripWidth)` signature survives 1.0 —
  matches storm counter model.
- Future dashed/animated caution outline: one-line addition to
  Painter2D paint call (no signature churn).
- ADR-0015 lagging-dep: `StormAdvancePerStrip` scalar is the right
  1.0 shape; per-beacon-weight future absorbs into leading-arc
  computation without touching this element.

Ship.
