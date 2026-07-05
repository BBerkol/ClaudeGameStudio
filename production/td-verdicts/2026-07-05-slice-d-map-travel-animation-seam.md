# Slice D — Map Travel Animation Seam (Shape A++ verdict)

**Slice:** V3 Fuel-as-Clock Slice D — MapViewController animation seam
**Date:** 2026-07-05
**Verdict:** APPROVE — Shape A++ (wiring seam + minimal visual affordance + 1.0-shape payloads)

---

## Context

V3 Fuel-as-Clock Slice C (RunState fuel wiring + DTO) landed. Slice D introduces
the map-travel animation seam so a beacon click no longer commits the fuel
model synchronously — instead it plays a 3s animation and commits at the end,
giving Slice E's fuel/storm/pill/delta-arrow widgets a window to display the
preview values before the model actually mutates.

Full V3 architecture rationale: `production/td-verdicts/2026-07-04-v3-fuel-as-clock-architecture.md`.

## TD Verdict

**APPROVE Shape A++.** Two-pass consultation:
- Pass 1 (Shape A vs Shape B): APPROVE Shape A + `.traveling` USS affordance (Shape A+).
- Pass 2 (health/optimization/1.0-shape re-audit per new mandatory self-audit routine): three deltas surfaced, Shape A+ becomes Shape A++.

### Pass 2 deltas (Shape A → A++)

**Codebase health**
- Extract `FuelState.ComputeDrain(baseCost, mult)` private helper — both `Spend` and `PreviewSpend` delegate. Kills drift risk on future storm/chassis modifiers.
- Extract `FuelState.ComputeRefill(max, pct)` private helper — both `RefillPartial` and `PreviewRefill` delegate. Same reasoning.
- `onComplete` callback in `RunSceneOverlayHost` guards `this` and `_host` for scene-teardown races (Alt+F4 during animation).
- Animation coroutine lives on `MapViewController` (map-scoped playback) — not a separate `BeaconTravelAnimator` component (premature abstraction).

**Optimization**
- Milestone ticks, NOT per-frame. `OnBeaconTravelTick` fires 3 times over 3s: Depart (t=0), Midpoint (t=1.5s), Arrive (t=3s). Fuel is integer-valued — per-frame ticks can't animate integer decrements honestly. Slice E widgets that want continuous curves compute their own interpolation off `Progress`.
- Coroutine allocation (`yield return WaitForSeconds`) negligible for one-shot 3s; not worth `Awaitable` migration.
- USS class toggle scope is element-local — no full-panel style recompute.

**1.0-shape survival**
- Verb takes `readonly struct BeaconTravelPreview` instead of positional `(spendPreview, stormPreview)` args. Slice E extending fields (destination pill styling, pre/post fuel snapshots) does NOT reshape the verb signature.
- `BeaconTravelPreview` carries both spend-shape (`FuelSpendResult Spend`) and Haven-shape (`int HavenRefill`, `bool StormResets`) fields so Slice E widgets branch on `Destination` without needing two separate verbs.
- `OnBeaconTravelTick(BeaconTravelTick tick)` where tick = `{ Progress, Milestone∈{Depart,Midpoint,Arrive} }`. Widgets read live model state (`FuelState`, storm state) — tick is a signal, not a data envelope. Payload never drifts against stale snapshots.
- `.traveling` USS class survives 1.0 as "beacon under active travel" state — not a stopgap that Slice E pills replace (pills are a different element).
- Cancellation policy: reject mid-animation clicks (via `IsTraveling` gate). Correct for 1.0; 3s window is short enough that misclick correction is post-1.0 polish concern.

## Files touched

**New files:**
- `Assets/Scripts/Run/BeaconTravelPreview.cs` — POCO readonly struct, WastelandRun.Run namespace.
- `Assets/Scripts/UI/BeaconTravelTick.cs` — payload struct + `TravelMilestone` enum, WastelandRun.UI namespace.

**Modified files:**
- `Assets/Scripts/Run/FuelState.cs` — add `PreviewSpend`, `PreviewRefill`, private `ComputeDrain` + `ComputeRefill` helpers; `Spend`/`RefillPartial` delegate to helpers.
- `Assets/Scripts/Run/RunSession.cs` — add `PreviewBeaconArrival(int toIndex) → BeaconTravelPreview` verb.
- `Assets/Scripts/UI/MapViewController.cs` — add `PlayBeaconTravelAnimation(from, to, preview, onComplete)` coroutine + `IsTraveling` gate + `event Action<BeaconTravelTick> OnBeaconTravelTick`; toggles `.traveling` USS class on destination `BeaconNodeElement`.
- `Assets/Scripts/CombatView/RunSceneOverlayHost.cs` — refactor `HandleBeaconClicked` to precompute preview, call verb, defer commit to `onComplete` callback.

**New tests:**
- `Assets/Tests/EditMode/Run/FuelState_PreviewSpend_Test.cs` — PreviewSpend purity + storm-preview symmetry with Spend.
- `Assets/Tests/EditMode/Run/RunSession_PreviewBeaconArrival_Test.cs` — non-Haven vs Haven preview shapes, no state mutation.

## ADR / memory drift check

- **ADR-0002** (engine-free Run POCO): `BeaconTravelPreview` is a `readonly struct` in `WastelandRun.Run` with no engine dep. Clean.
- **ADR-0011** (no bridges): `PreviewSpend` / `PreviewRefill` are canonical sibling verbs, not adapter shims. `ComputeDrain` / `ComputeRefill` are private single-source helpers, not parallel storage. Clean.
- **ADR-0014** (UI Toolkit primary, no UnityEvent): animation event uses `System.Action<BeaconTravelTick>`, not `UnityEvent`. Clean.
- **ADR-0015** (lagging-dep data flag): `OnBeaconTravelTick` ships with no subscribers today; Slice E widgets are the intended subscribers. Textbook lagging-dep pattern — the event surface is the end-state wire.
- **Memory `demo_forward_over_infrastructure`** (build 1.0 shape directly): `BeaconTravelPreview` struct is deliberately over-shaped for today's zero widgets so Slice E doesn't reshape it. Correct application of 1.0-forward discipline.
- **Memory `uitoolkit_subscription_lifecycle` / `subscription_lifecycle_pairing`**: no new subscription pairs added — `OnBeaconTravelTick` will be subscribed from Slice E under the Bind/OnDestroy pattern per the existing MapViewController lifecycle.
- **Memory `feedback_td_three_lens_self_audit`** (baked in this session): this verdict IS the output of the mandatory self-audit; second pass surfaced the three deltas above.

## Cancellation / re-entry

- `IsTraveling` gate on `MapViewController` rejects re-entrant `PlayBeaconTravelAnimation` calls; `RunSceneOverlayHost.HandleBeaconClicked` also short-circuits when `_mapView.IsTraveling` is true so BeaconNodeElement click events during animation are dropped silently.
- Scene teardown mid-animation: Unity auto-stops the coroutine on GameObject destroy. `onComplete` never fires — correct behavior (no state to commit). Defensive `if (this == null || _host == null) return;` guard in the callback body for scene-reload races.

## Success criteria

- FuelState.PreviewSpend does not mutate Current or StormCounter (locked by test).
- Advance does NOT fire synchronously on beacon click — fires after ~3s from `onComplete` (locked by test).
- Destination beacon carries `.traveling` USS class between click and commit (playtest evidence).
- Alt+F4 mid-animation reloads to pre-click state (playtest evidence — model wasn't committed).
- Re-entry attempts during animation are rejected silently (locked by test).

## Estimated effort

~1 session (arithmetic changes are small; animation seam is a 3s coroutine).
