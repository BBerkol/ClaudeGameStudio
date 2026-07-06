# TD Verdict — BeaconTravelTick surface for multi-consumer per-frame lerp

**Date:** 2026-07-06
**Slice:** V3 Fuel-as-Clock Slice E — travel-tick surface refactor (pre-landing)
**Verdict:** APPROVE — Shape A' (refined option (a))

---

## Context

Slice E Stages 1–3 landed the Run HUD fuel pill on top of the Slice D
`BeaconTravelTick` seam (Shape A++ verdict, 2026-07-05). User eye-tested
the current state and flagged two feel-gaps + one polish addition after
seeing beacon-node hover pills work:

- **Gap A (timing):** tank fuel pill drains over 0.4s at the START of the
  1.5–4s travel window; empties fast, sits static rest of travel. Designer
  wants the drain to unfold across the FULL travel window and land AT
  arrival.
- **Gap C (missing):** destination beacon-node fuel-cost pill number does
  NOT tick down during transit. `BeaconNodeElement.costText` is set once
  at `MapViewController.Bind()` and stays static across travel. Designer
  wants it to count down in sync with the tank, both hitting endpoint at
  arrival.
- **Polish B (additive):** `-1 [fuel canister icon]` micro-widget spawns
  from the tank pill on each integer tick of the drain, animates
  downward + fades. Widget-local, likely doesn't touch tick contract.

## The seam under review

`Assets/Scripts/UI/BeaconTravelTick.cs` — locked by 2026-07-05 Slice D
verdict as three discrete milestones (Depart / Midpoint / Arrive) with
`PreviewedFuelBefore` + `PreviewedFuelAfter` populated on Depart only.
One consumer today: `RunHUDController` (owns its own 0.4s drain coroutine).

## Proposal

**Shape A':** Extend struct + change cadence + retire `Midpoint`.

### 1. Struct extension

```csharp
public readonly struct BeaconTravelTick
{
    public float Progress;                  // 0..1, populated on every tick
    public TravelMilestone Milestone;       // Depart | InFlight | Arrive
    public int PreviewedFuelBefore;         // populated on every tick
    public int PreviewedFuelAfter;          // populated on every tick
    public int PreviewedStormBefore;        // NEW — Slice F pre-population
    public int PreviewedStormAfter;         // NEW — Slice F pre-population
    public int StormAdvanceStrips;          // NEW — Slice F pre-population
}
```

`Midpoint` retires from the `TravelMilestone` enum; `InFlight` replaces
it as the per-frame cadence signal. One-shot enum migration, ADR-0011
exception #1 — no coexistence period.

### 2. Cadence change

`MapViewController.BeaconTravelCoroutine`:
- Fire `Milestone.Depart` once at t=0 with full payload (all six ints)
- **Per-frame yield now fires `Milestone.InFlight`** with populated Progress + Before/After payload
- Fire `Milestone.Arrive` once at t=1.0 AFTER `onComplete()` (Slice D
  invariant preserved — subscribers on Arrive see live post-drain state)

### 3. Storm-field pre-population

Storm-related fields land in this slice even though Slice F is the
consumer. Rationale: `readonly struct` supports additive field growth
without signature churn; adding now costs nothing and prevents a Slice F
reshape (memory `demo_forward_over_infrastructure` + `overall_picture_thinking`).

### 4. Subscribers do their own lerp

Each consumer computes `Mathf.Lerp(before, after, tick.Progress)` in its
tick handler. This is not duplication — the arithmetic IS the primitive,
and each subscriber lerps a different value into a different UI target
(`RunHUDController._fuelLabel.text` vs `BeaconNodeElement._costLabel.text`
vs Polish B canister spawn-threshold detection). A shared helper would
either be too generic to earn keep, or would drag view-target references
into the publisher — both worse than the arithmetic itself.

## Rejected shapes

- **(b) Parallel per-frame event alongside milestones** — doubles
  lifecycle-pairing sites per subscriber (memory
  `subscription_lifecycle_pairing` earned this the hard way on Slice 7a
  CardRewardPicker). Violates ADR-0011 "one canonical surface per
  behavior."
- **(c) Retire milestones entirely, fold everything per-frame** —
  Slice F's storm-strip advance triggered by
  `FuelSpendResult.StormAdvanceStrips` doesn't lerp, it SNAPS at Arrive
  (or per D1 spec: mid-travel if counter hits 0). Discrete milestone
  transitions are the natural home for that snap event; forcing storm
  subscribers to poll `Progress` and detect threshold crossings turns a
  signal into a shape they have to reconstruct. Also, retiring
  `TravelMilestone` is destructive scope creep on a Shape A++ verdict
  that landed 24 hours ago; not a one-shot migration.
- **(d) Coroutine-owned tweener registration** —
  `_mapView.AnimateValue(from, to, curve, subscriber)`. Puts a generic
  animation-engine surface on `MapViewController`, which per its docstring
  is a "map view's binding lifecycle" MonoBehaviour. Charter creep.
  Doesn't solve Slice F's discrete-snap needs. ADR-0011 flavor: "just
  build the seam Slice F actually needs" beats "build a general animation
  facility."

## Migration cost

### `RunHUDController` (~-40 / +15 lines net)

- Retire `_drainCoroutine`, `StartDrainTween`, `StopDrainTween`,
  `DrainTweenCoroutine` — the whole tween-owns-its-own-timer machinery.
- Replace `HandleBeaconTravelTick` switch: `Depart` still starts USS
  class toggle + resets `_lastShownFuel`; `InFlight` becomes the lerp
  handler running `Mathf.Lerp(tick.Before, tick.After, tick.Progress)` on
  each call; `Arrive` snaps to live `FuelState.Current`, toggles USS
  class off.
- Keep `_drainCurve` as `SerializeField` (feel knob preserved); evaluate
  against `tick.Progress` in the InFlight handler.
- Add `_lastShownFuel` int guard: skip `_fuelLabel.text` reassignment
  when rounded value unchanged. Turns "~240 string allocs per travel"
  into "~FuelDrained allocs." Same edge triggers Polish B canister spawn.
- Feel benefit: drain duration now inherits from
  `MarkerSpeedNormalizedPerSecond` (edge-length aware) — a distant Combat
  node draining 8 fuel earns the 3s drain vs. an adjacent 2-cost hop
  draining across 1.5s.

### `BeaconNodeElement` (small addition)

- `MapViewController` sets destination flag on the pooled element at
  `PlayBeaconTravelAnimation` entry (`SetTravelDestination(preview,
  fuelBefore)` method or equivalent).
- Element subscribes to parent's `OnBeaconTravelTick` via same
  `RebuildBeacons` wire/unwire block that already carries
  `OnClicked`/`OnHoverEnter`/`OnHoverExit` (one line per side; memory
  `subscription_lifecycle_pairing` satisfied via `_beaconPool` lifetime).
- On InFlight tick, if `_isTravelDestination`: lerp `_costLabel.text`
  from `preview.Spend.FuelDrained → 0` (or `preview.HavenRefill → 0`).
- On Arrive: clear destination flag.
- Same `_lastShownCost` guard mirrors the RunHUDController allocation
  discipline.

### `BeaconTravelTick_Test`

Existing 5 tests (Stage 1) assert ctor shapes. Update or replace to cover
the extended struct + new field defaults. Test count may hold or grow;
maintain green baseline (800/0/1 today).

### Slice D verdict (2026-07-05)

Add a two-line amendment footnote at the bottom pointing to this
verdict. **Do NOT edit the verdict body** — that's revisionism. Amendment
format:

```markdown
**Amendment 2026-07-06:** Slice E extended tick cadence from 3 discrete
milestones to per-frame InFlight — see
`production/td-verdicts/2026-07-06-travel-tick-surface.md`.
```

## Three-lens self-audit

### Lens 1 — Codebase health

- **ADR-0011 drift: none.** `Midpoint → InFlight` in the same commit as
  cadence change is a one-shot enum migration (exception #1). No parallel
  storage. Subscribers' local `Mathf.Lerp` is the primitive, not
  duplication. Confirmed clean.
- **Subscription lifecycle:** `RunHUDController` already pairs
  Bind ↔ OnDestroy. `BeaconNodeElement` gains a subscription to its
  parent's event; lifetime managed by `_beaconPool.Add/Remove` in the
  existing wire/unwire block. Documented pairing. Clean.
- **Single-responsibility:** `MapViewController` gains no new
  responsibility — already firing the tick event. Increased cadence lives
  inside its existing coroutine.
- **Teardown races:** coroutine auto-stops on GameObject destroy (Unity
  standard). Per-frame tick cadence adds no new race surface.

### Lens 2 — Optimization

- **Cadence:** per-frame is correct — the pill LERPs an integer over
  1.5–4s = 90–240 ticks per travel at 60fps. `Mathf.Lerp` + `RoundToInt`
  is cheap; the real cost is `$"{shown}/{max}"` string interpolation per
  tick. Guarded by `_lastShownFuel` edge detection → ~FuelDrained
  allocs per travel (single digits).
- **USS class toggle:** `wr-fuel-pill--draining` toggles twice per travel
  (Depart on, Arrive off). Element-scoped. Same as today.
- **Delegate/closure alloc:** `readonly struct BeaconTravelTick` fires by
  value — no boxing. `yield return null` is the current shape. Zero new
  alloc.

### Lens 3 — 1.0-shape survival

- **Payload shape survives:** the 7-field struct is the 1.0 shape.
  Future Slice F (storm strip) and Slice G (auto-storm-advance on
  stranded — off this seam) don't force additions.
- **Enum survives:** `TravelMilestone { Depart, InFlight, Arrive }`.
  `Midpoint` retires cleanly (single subscriber path in current code,
  `default: break;`). If D1 spec's "mid-travel storm advance" needs a
  discrete signal at 1.0, add `Milestone.StormAdvance` then — additive
  enum growth is not a bridge.
- **Stopgap risk: none.** No placeholder USS classes or `-1` sentinels
  being introduced — in fact **removing** the `-1` sentinel that lived
  on the Midpoint/Arrive constructor by populating fields on every tick.
  Cleanup delta, not new debt.
- **Cancellation / re-entry:** `IsTraveling` gate stays. No 1.0 UX
  polish lock-in introduced.

## ADR / memory drift flags

- **ADR-0002** (engine-free Run POCO): `BeaconTravelTick` remains a
  UI-namespace payload struct; `MapViewController` still gets fuel
  values ferried at click time from the host. Clean.
- **ADR-0011** (no bridges): retiring `Midpoint` in the same commit that
  introduces `InFlight` is a one-shot enum migration (exception #1). No
  coexistence period. `-1` sentinel goes away entirely rather than
  "sentinel path + populated path." Clean if executed in a single slice.
- **ADR-0014** (no `UnityEvent`): `Action<BeaconTravelTick>` unchanged.
  Clean.
- **Memory `demo_forward_over_infrastructure`:** this verdict
  pre-populates storm fields for Slice F. Textbook application.
- **Memory `subscription_lifecycle_pairing`:** `BeaconNodeElement` gains
  a subscription; lifetime is `_beaconPool.Add/Remove` sibling to
  existing wire/unwire block. Documented pairing.

## Success criteria

- Tank pill numeric drains monotonically from `fuelBefore` to `fuelAfter`
  across the FULL travel window (1.5–4s), not the first 0.4s.
- Destination beacon-node cost pill ticks down from
  `preview.Spend.FuelDrained` (or `preview.HavenRefill`) to `0` in sync
  with the tank, both hitting endpoint at Arrive.
- `_lastShownFuel` guard: no `_fuelLabel.text` reassignment per frame
  when rounded integer value unchanged. Expect ≤ `FuelDrained + 2`
  assignments per travel (profiler verifiable).
- Slice F storm-strip subscriber (when it lands) needs zero changes to
  `BeaconTravelTick` or `MapViewController` — verified by inspection at
  Slice F time.
- Test baseline holds: 800 passed / 0 failed / 1 skipped after EditMode
  sweep, plus test count delta from `BeaconTravelTick_Test` updates.

## Files touched (per implementation)

- `Assets/Scripts/UI/BeaconTravelTick.cs` — struct extension + enum edit
- `Assets/Scripts/UI/MapViewController.cs` — coroutine per-frame tick +
  storm payload wire
- `Assets/Scripts/CombatView/RunHUDController.cs` — retire own coroutine,
  replace with handler-lerp + `_lastShownFuel` guard
- `Assets/Scripts/UI/BeaconNodeElement.cs` — destination-subscription +
  cost label lerp + `_lastShownCost` guard
- `Assets/Tests/EditMode/UI/BeaconTravelTick_Test.cs` — extend test
  coverage
- `Assets/UI/RunHUD.uxml` — Polish B canister emitter placeholder element
- `Assets/UI/RunHUD.uss` — Polish B canister emitter animation classes
- `production/td-verdicts/2026-07-05-slice-d-map-travel-animation-seam.md`
  — two-line amendment footnote (no body edit)
