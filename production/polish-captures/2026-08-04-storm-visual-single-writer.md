# Capture — Storm Map Visual Single-Writer Rewrite

**Date:** 2026-08-04
**System:** Storm map visuals (`StormMapVisualHost`, `StormAdvanceVisualPacer`, `MapViewController` storm section, `StormFrontElement`, `StormPreviewBandElement`)
**Trigger:** System refactor ≥50 lines across multiple system-shape carriers — capture-before-destroy protocol applies.
**Status:** Awaiting user approval before any edit.

---

## Why this refactor

Two distinct player-visible bugs in two days traced to the same structural hole.

**Bug A (2026-08-03, fixed by guard).** Danger-preview band rendered one full strip
ahead of the storm front after an Event beacon resolved. Survived FOUR fix attempts
because each attempt changed *when* things ran, never *who writes what*. Real cause:
`StormAdvanceVisualPacer`'s zero-delta pass-through raised an un-queued tick with
`DurationSeconds == 0`, and `StormMapVisualHost.HandleStormAdvanced` had no matching
guard, so `PlayPreviewGrow(ToX, w, 0)` snapped the band to a position the still-queued
arrival sweep had not reached. Shipped a zero-delta guard; user confirmed fixed.

**Bug B (2026-08-04, open).** Starting a new run after a boss victory paints the
*previous* run's storm over a fresh run's graph: wall ~35-45% into the map and ~9
beacons flagged `wr-beacon--swallowed`, against authored fresh-run values of
`_initialStormCursorX: -0.05` (off the left edge) and `_stormAdvancePerStrip: 0.08`
(one narrow strip). Model restarted correctly — fuel 35/35, counter 12/12, scrap 38
all read fresh. Only the map visuals are stale.

Proven ordering defect behind Bug B:
1. `RunSceneHost.cs:588` — `OnRunStarted` → `StormMapVisualHost.HandleRunStarted` →
   `ScheduleInitialPaint()`, which defers the paint **one frame**.
2. `RunSceneHost.cs:589` — `OnBeaconChanged`, **same frame** →
   `RunSceneOverlayHost.cs:137` → `MapViewController.Bind()` → `RebuildBeacons()`,
   which reapplies `wr-beacon--swallowed` from `_stormSwallowedThroughX` — still
   holding the previous run's high-water mark, because the only reset lives inside
   `ShowStormFrontAt`, which has not run yet.

Root structural cause (TD): the preview band has **two** resync paths
(`PlayPreviewGrow`, `PaintPreview`); the storm front has **zero** after run start.
`ShowStormFrontAt` is called exactly once per run, from `PaintInitialCursorNextFrame`.
Thereafter the apex is written only by `MapViewController.StormAdvanceCoroutine`. Any
divergence between the two elements is therefore permanent rather than self-correcting.

---

## Authored values at risk — MUST be preserved verbatim

### `Assets/Scripts/UI/Elements/StormFrontElement.cs`

| Knob | Value |
|---|---|
| `LayerColors[0]` deepest shadow | `rgba(62, 38, 20, 1.00)` |
| `LayerColors[1]` shadow | `rgba(95, 60, 32, 1.00)` |
| `LayerColors[2]` mid-shadow | `rgba(135, 88, 50, 1.00)` |
| `LayerColors[3]` mid | `rgba(180, 130, 82, 1.00)` |
| `LayerColors[4]` mid-highlight | `rgba(212, 170, 118, 1.00)` |
| `LayerColors[5]` cream highlight | `rgba(238, 210, 158, 0.92)` |
| `LayerOffsets` | `{ -16, -10, -4, 3, 10, 20 }` |
| `LayerBandWidths` | `{ 26, 22, 18, 15, 12, 10 }` |
| `LayerBumpAmplitudes` | `{ 6, 9, 13, 18, 24, 32 }` |
| `LayerPhaseOffsets` | `{ 0.0, 1.37, 2.71, 4.13, 5.51, 6.89 }` |
| `BodyColor` | `rgba(115, 78, 45, 1)` |
| `TrailStreakCount` | `10` |
| `TrailLengthPx` | `130` |
| `TrailSegments` | `14` |
| `TrailColorOuter` | `rgba(215, 172, 118, 0.14)` |
| `TrailColorMid` | `rgba(220, 178, 128, 0.24)` |
| `TrailColorInner` | `rgba(232, 198, 148, 0.48)` |
| `TrailBaseHalfWidthOuter / Mid / Inner` | `10 / 6 / 3` |
| `BumpFrequency` | `0.010` |
| `ChurnSpeed` | `0.35` |
| `SamplesPerHeight` | `64` |

### `Assets/Scripts/UI/Elements/StormPreviewBandElement.cs`

| Knob | Value |
|---|---|
| `StripeColor` | `rgba(240, 40, 40, 1)` |
| `StripeSpacingPx` | `22` |
| `StripeLineWidthPx` | `4` |
| `SamplesPerHeight` (local const) | `128` |

### `Assets/Scripts/CombatView/StormMapVisualHost.cs`

| Knob | Value |
|---|---|
| `StormPhaseFraction` | `0.65` |
| `_postAdvanceHoldSeconds` | `0.5` (C# default; not overridden on Run.prefab) |

### Serialized on `Assets/Prefabs/Run/Run.prefab` — DESIGNER OVERRIDE

| Knob | Value | Note |
|---|---|---|
| `MapViewController._arcConcavityNormalized` | **`0.05`** | Overrides the C# field default of `0.04`. Must survive. |
| `StormMapVisualHost._host` / `._mapView` | wired refs | Re-wire if the component's field set changes. |

### `Assets/Resources/Run/StormEngulfmentTuning.asset`

| Knob | Value |
|---|---|
| `_secondsPerBeacon` | `3` (fallback in code is `1.5`) |
| `_strandedPeakSpeedMultiplier` | absent → default `3` |
| `_strandedRampSeconds` | absent → default `8` |

### `Assets/Resources/.../Biome1Distribution.asset`

| Knob | Value |
|---|---|
| `_initialStormCursorX` | `-0.05` |
| `_stormAdvancePerStrip` | `0.08` |
| `_stormCounterStart` | `12` |

### USS / layout

- `_stormLayer` and `_stormPreviewBand`: `left: -3%`, `right: -3%`, both direct
  children of `_canvas`. (In-code comments claiming `-2%/+2%` are **stale** — verified
  against source; do not propagate the comment's numbers.)
- Beacon swallow class: `wr-beacon--swallowed`.

---

## Proposed change

Per TD verdict below:

1. Add pure `readonly struct StormPaintFrame { FrontX; BandTrailingX; BandLeadingX; }`
   and `static StormPaintFrame Evaluate(fromX, toX, bandWidth, t01)` — no Unity types,
   fully EditMode-testable.
2. Add `MapViewController.ApplyStormPaint(in StormPaintFrame)` as the **sole** caller of
   `_stormFront.SetApex` and `_stormPreviewBand.SetRange`. Collapse `ShowStormFrontAt`,
   `UpdateStormPreview`, `PlayPreviewGrow`, `PlayStormAdvance` into it.
3. One coroutine, on `MapViewController` (owns the elements, so lifetimes match),
   driving `t01` 0→1 over D. Input-lock release becomes its tail.
4. Rest / run-start state is `Evaluate(cursorX, cursorX, w, 1f)` — same function as the
   animation, so they cannot structurally diverge. This is what closes Bug B.
5. Reset `_stormSwallowedThroughX` and reapply swallow classes inside `ApplyStormPaint`,
   so `RebuildBeacons` can never reapply a stale high-water mark.
6. `StormMapVisualHost` reduces to a translator; keeps no coroutines.
7. Expose `public StormPaintFrame CurrentStormPaint { get; private set; }` — the
   permanent observability seam four eyes-only sessions lacked.
8. Fix `MapViewController.OnDisable` — currently stops `_fadeCoroutine` but not
   `_stormAdvanceCoroutine` / `_previewAdvanceCoroutine`, and nulls neither.
9. Delete the pacer's zero-delta pass-through branch **only if** the HUD counter
   refresh keeps working; the HUD genuinely needs the immediate raise (2026-07-31 fix).
   Default position: keep the pass-through, keep the map-side guard.

**EditMode invariant to assert:** `FrontX == BandTrailingX` at every `t01`.

### Explicitly out of scope

- The Painter2D wall itself (EA prototype; 1.0 target is sprite-composite or shader per
  `project_storm_visual_1_0_target`). `ApplyStormPaint` is the seam that replacement
  plugs into.
- The cosmetic docking gap: `StormFrontElement` draws its front layer at `apex + 20px`
  with ±32px Perlin churn and wispy alpha while the band docks a crisp opaque 4px stroke
  at raw `apex`, and the storm layer paints on top. Separate slice.
- `StormFrontElement`'s 30 Hz `schedule.Execute(...).Every(33)` churn tick repainting
  while the map is hidden. Logged, not fixed here.

---

## Technical Director Review

**Verdict: REJECT (current shape, including the uncommitted `tick.ToX` fix).**

> The problem is not that four paths write positions. It is that **only one of the two
> elements has a resync path.** `UpdateStormPreview` (band) has two authoritative
> writers: `PlayPreviewGrow`'s coroutine, and `PaintPreview` at the end of every
> input-lock window. `ShowStormFrontAt` (front) has exactly one production caller:
> `StormMapVisualHost.PaintInitialCursorNextFrame`, which runs once per run start.
> After that the apex is written only by `MapViewController.StormAdvanceCoroutine`. If
> that coroutine is interrupted, aborted, or never completes, the apex freezes and
> nothing ever corrects it, while the band unconditionally lands at its correct
> endpoint. Result: band correct, front stale-behind.
>
> Every fix so far changed **when** things run. Fix #4 in fact *strengthened the band's
> resync* while leaving the front with zero resync — it can only widen the gap it is
> trying to close. This is a fix that cannot work in principle, which is consistent with
> the observed recurrence rate.
>
> **Sequencing: rewrite now, do not instrument first.** The readout you want is a
> byproduct of the correct shape (`CurrentStormPaint` is one line inside
> `ApplyStormPaint`), so "instrument first" means writing probes into four call sites
> you are about to delete. The diagnosis does not need more data — the resync-path
> asymmetry is provable from the call graph as it stands.
>
> **Revert the uncommitted `ReleaseInputLockAfter(delay, finalVisualX)` diff** — it is
> throwaway scaffolding the rewrite deletes.

**Additional TD findings folded into scope:**

- The pass-through's justifying comment (`StormAdvanceVisualPacer.cs:227-232`) claims
  "nothing fires `OnBeaconChanged` / `OnRewardClaimed` after a same-beacon resolution."
  **False** — `RunSceneHost.NotifyEventResolved` (913-920), `NotifyRestResolved` (888),
  `NotifyMerchantResolved` (941) and `NotifyChopshopResolved` (969) all resolve and then
  fire `OnBeaconChanged`. ADR-0011 forbidden pattern #3 (bimodal path) justified by a
  premise that does not hold.
- Three components, two GameObject lifetimes, one animation: `_flushCoroutine` on the
  pacer and `_inputLockCoroutine` on the host live on Run.prefab root (survive anything
  the map does); `_stormAdvanceCoroutine` / `_previewAdvanceCoroutine` live on the
  MapView GameObject (do not). Whenever those lifetimes diverge for one frame, the front
  freezes and the band resyncs — the exact failure signature.
- `MapViewController.Hide()` toggles UIDocument `display`, not `SetActive`, so cached
  `VisualElement` pointers are safe here and `feedback_uidocument_setactive_reclone` does
  not bite. But while hidden `resolvedStyle.width == 0`, so any paint issued while hidden
  is stored and not rendered.

**Three-lens self-audit (TD):** ADR-0011 drift confirmed (bimodal pass-through +
transitional comment block documenting a false premise). `StormMapVisualHost`'s
OnEnable/OnDisable pairing is **correct** — it sits on Run.prefab root, never
SetActive-cycled. Moving the coroutine to `MapViewController` *reduces* the host's
surface. `StormPaintFrame` must be a `readonly struct` so the 1.0 sprite-composite /
shader wall can add fields (churn phase, opacity ramp) without a signature break.

---

## Verification plan

1. EditMode: `Evaluate` invariants — `t=0` front == `fromX` and band trailing == `fromX`;
   `t=1` both == `toX`; **front == band trailing at every t**.
2. EditMode: run-start state — `Evaluate(cursor, cursor, w, 1f)` yields a band of exactly
   `_stormAdvancePerStrip` width with trailing at the cursor.
3. PlayMode, Bug A regression: Event beacon → storm-cost choice → resolve → band stays
   docked through the sweep; no strip-ahead jump; no spurious input lock.
4. PlayMode, Bug B regression: beat boss → New Run → storm wall off the left edge, zero
   beacons flagged swallowed, danger band one strip (8%) wide.
5. `CurrentStormPaint` never shows front and band trailing differing by more than float
   epsilon outside an active sweep.

## Rollback

Single commit, revertible. Pre-change HEAD: `506de2d`. Working tree also carries the
uncommitted Chopshop/storm bug bundle + widget removal — commit or stash those first so
the rewrite lands isolated.
