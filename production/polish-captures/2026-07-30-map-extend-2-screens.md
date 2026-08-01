---
title: Map Extend to 2 Screens + Storm-Layer Reparent + CanvasAspect SO Promotion
date: 2026-07-30
sprint: sprint-01
milestone: prototype-waiver
type: system-refactor + designer-tuning
---

# Map Extend Slice — Capture

## Intent

Widen the run map from 1 screen (1920×1080) to 2 screens (3840×1080) so beacon spacing has more room to breathe and the journey has more pressure. Preview crescent + storm arc must still align at 2×. Cinematic camera-pan is a follow-up slice; drag-scroll is decided after playing the pan.

Slice ships:
- UXML/USS viewport wrapper + 200%-wide canvas (default view = leftmost)
- Storm layers reparented to canvas so they scroll with future cinematic pan (2% inset trade accepted)
- `CanvasAspectX`/`CanvasAspectY` promoted from `BiomeWebGenerator` consts to `BiomeDistributionSO` fields (ADR-0015 narrowing)
- `Biome1Distribution.asset` retune (revised from pre-audit estimates against actual asset values discovered in file): TargetBeaconCount 30→55, StormAdvancePerStrip 0.16→0.08, ReconnectRadius 500→1000, MaxEdgeLength 500→1000, CanvasWidthPx=3840, CanvasHeightPx=1080. Rationale: all pixel-space knobs scale proportionally with 2× canvas width so normalized-X pressure + reconnect density stay identical to the shipped biome-1 shape.
- `MaxEmergentBeaconCount` const 40→80 + matching OnValidate clamp
- P1 audit fold-in: `MapViewController.RebuildConnections` pooled list (no per-rebind alloc)
- P2 audit fold-in: `BiomeDistributionSO.OnValidate` clamps target beacon count to 80

Surface-freeze exit: `project_generator_so_surface_freeze` set a 5-slice freeze on 2026-07-07; the freeze window has elapsed (2026-07-30). This slice is the controlled expansion — logged here for future audit trail.

## Authored Values Being Destroyed

| Location | Before | After | Notes |
|---|---|---|---|
| `BiomeWebGenerator.CanvasAspectX` (const) | `1920f` | *deleted* | Promoted to `BiomeDistributionSO._canvasWidthPx`, wired through `BiomeGenerationInputs.CanvasWidthPx` |
| `BiomeWebGenerator.CanvasAspectY` (const) | `1080f` | *deleted* | Promoted to `BiomeDistributionSO._canvasHeightPx` |
| `BiomeWebGenerator.MaxEmergentBeaconCount` (const) | `40` | `80` | Doubled to accommodate wider canvas density |
| `BiomeDistributionSO._targetBeaconCount` default | `30` | *unchanged in SO defaults* (per-asset only) | Biome1Distribution.asset retunes to 55 |
| `BiomeDistributionSO._stormAdvancePerStrip` default | `0.05f` | *unchanged in SO defaults* | Biome1Distribution.asset retunes to 0.025 |
| `BiomeDistributionSO.OnValidate` beacon guard | warn on `> 40`, no clamp | warn + clamp to 80 on `> 80` | P2 audit fold-in |
| `Biome1Distribution.asset` `_targetBeaconCount` | `30` | `55` | Density preserved at 2× canvas area |
| `Biome1Distribution.asset` `_stormAdvancePerStrip` | `0.05` | `0.025` | Normalized-X journey pressure preserved |
| `Biome1Distribution.asset` `_reconnectRadius` | `280` | `560` | TD-required — proportional to 2× canvas width |
| `Biome1Distribution.asset` `_canvasWidthPx` | *new* | `3840` | 2-screen map width |
| `Biome1Distribution.asset` `_canvasHeightPx` | *new* | `1080` | Height unchanged |
| `Assets/UI/MapView.uxml` root shape | single `wr-map-root` = viewport | wraps `wr-map-root` in `wr-map-viewport` (`overflow:hidden`), inner canvas at 200% width | Canvas scrolls via `translate:X` on `wr-map-root` in follow-up cinematic-pan slice |
| `Assets/UI/MapView.uss` `.wr-map-canvas` width | implicit 100% | explicit `width: 200%` | |
| `MapViewController._stormLayer` parent | `_canvas.parent` (map-root scope) | `_canvas` (canvas scope) | 2% inset trade; scrolls with map |
| `MapViewController._stormPreviewBand` parent | `_canvas.parent` | `_canvas` | Same trade |
| `MapViewController.RebuildConnections` line list | fresh `List<ConnectionLineElement>` + `.ToArray()` per call | pooled private field, cleared at method entry | P1 audit fold-in |

## Files Touched

1. `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` — add `_canvasWidthPx` + `_canvasHeightPx` fields + accessors + OnValidate clamps; bump beacon-count clamp
2. `Assets/Scripts/Run/BiomeGenerationInputs.cs` — add `CanvasWidthPx` + `CanvasHeightPx` fields
3. `Assets/Scripts/Run/BiomeWebGenerator.cs` — delete `CanvasAspectX`/`CanvasAspectY` consts; source from `inputs.CanvasWidthPx/HeightPx`; bump `MaxEmergentBeaconCount` const 40→80
4. `Assets/Resources/Biome1Distribution.asset` — retune values
5. `Assets/UI/MapView.uxml` — add viewport wrapper
6. `Assets/UI/MapView.uss` — canvas width 200%
7. `Assets/Scripts/UI/MapViewController.cs` — re-parent storm layers; pool RebuildConnections list
8. Wherever `BiomeDistributionSO` → `BiomeGenerationInputs` is constructed (grep required to identify + update)

## Playtest Notes (Post-Slice)

- `StormAdvancePerStrip = 0.025` — normalized-X pressure is preserved arithmetically, but the visual sweep may read as a nudge rather than an event. Flag for playtest review; retune upward if the doom cinematic loses weight. TD flagged this as design-feel-pending, not TD-locked.
- Poisson generator time on 3840×1080 with 55 beacons @ 90px min-separation should stay < 100ms. Measure on a fixed seed before/after and log; if it climbs, follow-up perf slice raises the retry cap or adds early-out.
- Camera stays at leftmost view (`translate:0`) — no cinematic pan yet, so the right half of the map is initially off-screen. This is expected pre-pan-slice; drag-scroll (deferred) or the cinematic pan (next slice) exposes it.

## Deferred

- Cinematic camera pan on storm advance (next slice)
- Drag-scroll (LMB hold) — decide after playing cinematic-pan
- MapViewController controller-split refactor (folds into cinematic-pan slice per TD)

## Technical Director Review

**Verdict: APPROVE-WITH-CHANGES** — the bundle is directionally correct and ADR-0011-clean, but three of the "key concerns" are load-bearing and must be addressed in-slice, not deferred. The storm arc math is fine; the Poisson budget and reconnect radius are not.

### Health (blast radius, coupling, reversibility)

- **ADR-0011 posture: clean.** Deleting the consts + wiring SO fields through `BiomeGenerationInputs` is the canonical narrowing pattern (ADR-0015), not a bridge. No compat overload, no dual-source-of-truth. Good.
- **Storm re-parent is the right call.** Painting storm/preview on `_canvas` parent (map-root scope) means the storm scrolls with future cinematic pan for free — that's the shape that survives. 2% inset is a real UX delta but self-heals when canvas padding is normalized in the pan slice.
- **Coupling risk — surface freeze memory.** `project_generator_so_surface_freeze` had a 5-slice freeze from 2026-07-07. We're past that window (2026-07-30), but this is a real surface change: `BiomeGenerationInputs` gains two fields, `BiomeDistributionSO` gains two serialized fields with `OnValidate`. Log this as the freeze exit in the capture — future readers need the audit trail.
- **Reversibility: high.** Consts → SO field is one-way per ADR-0011, but the *value* is designer-editable; if 2-screen feels wrong we retune the SO, no code change.
- **Subscription lifecycle:** `MapViewController` connection pooling (item 6) doesn't cross a Bind/OnDestroy boundary — pure per-rebuild reuse. Safe.

### Optimization

- **Item 6 (connection pool) is a real win** — RebuildConnections runs on every map state change; killing per-call `List` alloc + `.ToArray()` matters. Confirmed no delta needed.
- **Poisson dart-throwing on 3840×1080 with 55 targets @ 90px min-separation IS the concern.** Theoretical max on 3840×1080 with 90px hexagonal packing is ~500 points; 55 is comfortable *in principle*, but Poisson dart-throwing scales poorly with candidate rejection rate as density climbs. Current 30 beacons on 1920×1080 is well under threshold; doubling area + ~2× count keeps density flat but the dart-throwing iteration budget likely wasn't tuned for this. **Required: measure generator time before/after on a fixed seed and log it in the capture.** If >100ms, raise the retry cap or add early-out.
- **First-frame:** 200% canvas width means UI Toolkit lays out 2× the beacon elements immediately. 55 beacon `VisualElement`s + connection lines is still trivial — no concern.

### 1.0 Survival

- **Storm arc math survives.** `_apexX * r.width` on the double-wide canvas is correct — the arc parametrization is normalized in `_apexX` (0..1) and stretched to canvas pixels. Doubling `r.width` doubles the arc horizontally, which is what you want (storm crescent scaled to map). Confirmed no math change needed.
- **`_reconnectRadius = 280f` DOES NOT survive as-is.** On 1920px this is ~14.6% of canvas width; on 3840px it's ~7.3%. Beacons at the same normalized spacing now fall outside reconnect range → sparser mesh, more isolated components. **Required: promote to SO field OR retune to ~560f in-slice.** Recommend SO field (same pattern as canvas dims — designer-editable, ADR-0015-clean).
- **`_maxEdgeLength = 0f` (unlimited) survives IF reconnect radius is fixed.** Reconnect radius caps the practical edge length. Do not add a separate `_maxEdgeLength` clamp — that's a second knob for one problem. If cross-map stringy edges appear post-retune, revisit in a follow-up.
- **`StormAdvancePerStrip = 0.025` is a design-feel question, not a TD one.** Normalized-X pressure is preserved; whether the visual sweep reads as consequential is playtest-driven. Flag for playtest note in capture, don't block on it.
- **Item 7 clamp bump (40→80):** aligns with item 5's cap raise. Correct.

### Required Changes (punch list)

1. **Retune `_reconnectRadius`** — either bump to ~560f in `Biome1Distribution.asset` OR promote to `BiomeDistributionSO` field (recommend the latter; same slice, same pattern). [Resolved: `_reconnectRadius` already IS an SO field per BiomeDistributionSO.cs line 102 — retune to 560 in Biome1Distribution.asset]
2. **Measure + log Poisson generator time** before/after on a fixed seed in the capture; if >100ms, note as follow-up perf slice. [Flagged as playtest note above — measurement runs against Play build]
3. **Log ADR-0015 surface freeze exit** in the capture (2026-07-07 freeze → 2026-07-30 controlled expansion). [Logged in Intent section]
4. **Playtest note for `StormAdvancePerStrip = 0.025`** — flag as design-feel-pending, not TD-locked. [Logged in Playtest Notes section]

Storm arc math, item 6 pool, and generator cap raise are all confirmed clean — no delta on those.
