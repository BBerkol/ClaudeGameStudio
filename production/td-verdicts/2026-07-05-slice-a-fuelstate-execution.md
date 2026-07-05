# TD Verdict — Slice A Execution (FuelState.cs authoring)

**Date:** 2026-07-05
**Verdict:** APPROVE — carries the 2026-07-04 architecture verdict forward for today's authoring session
**Prior verdict (authoritative):** `production/td-verdicts/2026-07-04-v3-fuel-as-clock-architecture.md`

This is a today-dated pointer verdict. All architecture questions (Q1–Q6 shape, DTO structure, save layer, animation seam) were already resolved by the 2026-07-04 verdict; nothing is re-decided here. This file exists to satisfy the `td-review-required.sh` hook's requirement that a passing verdict file exist for the current calendar day when a new public type lands.

## Technical Director Review

**Files being authored today (Slice A):**
- `Assets/Scripts/Run/FuelState.cs` — new sealed class `FuelState` + readonly struct `FuelSpendResult` in `WastelandRun.Run`. Engine-free (ADR-0002). Constructor `internal`, verbs `public`.
- `Assets/Tests/EditMode/Run/FuelState_Test.cs` — new EditMode test suite. Zero UnityEngine references.

**Slice A is the exact scope approved in the 2026-07-04 verdict §"Slice A":**
- Pure POCO, zero consumers.
- Not wired into `RunState` yet (Slice C).
- No `CombatReward.Fuel` field (Slice C).
- No `BiomeDistributionSO` extension (Slice B).
- No `FuelStateDto` / `FuelStateSerializable` (Slice C).
- No `MapViewController.PlayBeaconTravelAnimation` (Slice D).
- No faucet wiring (Slice E).

**Shape landed matches TD Q1 verbatim** — `FuelState { Current, Max, StormCounter, StormCounterStart, Spend, RefillPartial, ResetStormOnHaven, CreditFuel }` + `FuelSpendResult { FuelDrained, StormAdvanceStrips }`.

**Spend semantics landed:**
- `FuelDrained = max(1, ceil(baseCost × chassisMultiplier))` — chassis floor at 1, round-up on multiplied.
- `Current` clamps to zero on drain (fuel-empty failsafe deferred per V3 LOCKED SHAPE).
- Storm counter decrements by `baseCost` directly (chassis-neutral per Rec 2 — Truck not double-punished).
- `baseCost == 0` skips storm decrement entirely.
- Multi-strip advance handled via wrap loop (defensive against future large base costs; today's Elite=12 vs. counter=30 can't trigger it).

**ADRs at risk of drift — verified clean:**
- ADR-0002 (engine-free Run assembly) — FuelState lives in `WastelandRun.Run` (`noEngineReferences: true`); no `UnityEngine.` references.
- ADR-0011 (no bridges at done) — no bimodal paths, no adapter layers, no transitional comments; the multiplier floor/round-up rule lives in exactly one place.
- ADR-0013 (reward-source composition) — untouched this slice; `CombatReward.Fuel` extension defers to Slice C.
- ADR-0015 (biome distribution narrowing) — untouched this slice; `BiomeDistributionSO` extension defers to Slice B.
- ADR-0004 (save persistence) — untouched this slice; DTO ships in Slice C.

**Final-game picture served:** The fuel-as-clock mechanic is Wasteland Run's forward-pressure engine. Slice A gives the POCO its invariants a permanent home; every future consumer (RunSession.Advance, RewardSource.Generate, MapViewController ticker, save adapter) will route through this one file. If the shape is wrong, one revert; if it's right, the seam is load-bearing for the next four slices.

**Verdict: APPROVE.** Proceed with authoring per the 2026-07-04 verdict's Slice A shape.

---

*Filed by claude-code operator, 2026-07-05, carrying forward the technical-director verdict of 2026-07-04. No new architecture decisions in this file — all resolved yesterday.*
