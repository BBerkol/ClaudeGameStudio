# TD Verdict Pointer — Slice C (RunState fuel wiring + save DTO + reward extension)

**Date:** 2026-07-05
**Status:** Pointer / execution wrapper
**Authoritative verdict:** `production/td-verdicts/2026-07-04-v3-fuel-as-clock-architecture.md`

## Purpose

Today-dated pointer verdict satisfying the `td-review-required.sh` hook for Slice C of the
V3 Fuel-as-Clock architecture. Carries forward the 2026-07-04 authoritative TD verdict — no
new decisions here, only execution scope enumeration.

## TD Verdict

**Verdict: ACCEPT** — proceed with Slice C execution per the scope enumerated below. This
pointer verdict carries the authoritative 2026-07-04 V3 Fuel-as-Clock architecture verdict
forward and applies a 2026-07-05 amendment (see "Modifications to existing types" below)
covering the `VehicleDefinitionSO` extension needed to source `TankCapacity` +
`FuelBurnMultiplier` cleanly per ADR-0011 + `feedback_data_flag_lagging_dependency` (SO is
the chassis-authoring surface; hardcoding the values in RunSceneHost would be parallel
storage per ADR-0011 #2). No new architectural decisions — this file exists solely for the
today-dated hook precondition on new public type introductions (`FuelStateDto`,
`FuelStateSerializable`) and the SO field additions (`_tankCapacity`, `_fuelBurnMultiplier`).

## Slice C scope (per 2026-07-04 verdict §"Slice C — RunState wiring + `FuelStateDto` + `FuelStateSerializable` + reward extension")

The "seam lights up" slice — this is where FuelState (Slice A) and BiomeDistributionSO
(Slice B) get wired into RunState, the save layer, and the reward pipeline.

### New public types introduced

- `WastelandRun.Save.Dtos.FuelStateDto` — sibling DTO with
  `SYSTEM_ID = "run.fuel_state"` + `SCHEMA_VERSION = 1`. Shape per 2026-07-04 verdict §Q3
  (Current, Max, StormCounter, StormCounterStart fields).
- `WastelandRun.Save.Adapters.FuelStateSerializable` — adapter following
  `VehicleStateSerializable` pattern exactly (`_liveSource` Func, `LastLoaded` field,
  defense-in-depth type check in `FromDto`).

### Modifications to existing types

- `WastelandRun.Run.RunState` — add `public FuelState Fuel { get; }` (per verdict §Q1).
- `WastelandRun.Run.RunController` — `StartRun` param-thread accepts `FuelState`
  constructed by caller from chassis + biome SO.
- `WastelandRun.CombatView.Data.VehicleDefinitionSO` — TD scope addition
  (2026-07-05): `_tankCapacity : int` (default 35) + `_fuelBurnMultiplier :
  float` (default 0.7f) `SerializeField` fields with `OnValidate` clamps
  (`tank >= 1`, `burn >= 0f` — NOT `>= 0.1f`, a 0-mult chassis is a legit
  future axis) + public getters + `Configure(...)` overload update.
  `Scout.asset` re-baked to (35, 0.7f). ADR-0011-clean end state per
  `feedback_data_flag_lagging_dependency`: authoring surface IS the end
  state, not a TODO. Rationale: SO is already chassis-authoring surface per
  ADR-0012; hardcoding in RunSceneHost = parallel storage (ADR-0011 #2).
- `WastelandRun.CombatView.CombatDataInitializer` — if it calls
  `VehicleDefinitionSO.Configure(...)`, pass through new fields or defaults
  silently zero on re-bake.
- `WastelandRun.Run.CombatReward` — additive `int Fuel = 0` field
  (per verdict §Q5, ADR-0013-composition-compatible).
- `WastelandRun.Run.FlatScrapRewardSource.Generate` — reserves seam by explicitly setting
  `Fuel = 0` (unchanged M1 behavior; draw-order lock — scrap draws before fuel draws
  inside `Generate` implementations, enforced by test).
- `WastelandRun.Run.RunSession.Advance` — reads
  `BiomeDistributionSO.BeaconFuelCosts[(int)beacon.Type]` × chassis multiplier and calls
  `state.Fuel.Spend(baseCost, chassisMultiplier)` at commit time (before `EnterCombat`
  fires). Haven beacons additionally call `state.Fuel.RefillPartial(havenPercent)` +
  `state.Fuel.ResetStormOnHaven()`.
- `WastelandRun.Run.RunSession.ExitCombat` — credits
  `state.Fuel.CreditFuel(reward.Fuel)`.
- Save layer registration — `SaveBootstrap.RegisterAll` (or equivalent) picks up
  `FuelStateSerializable`. `RunSceneHost.BeginRunFromLoaded` rehydrate branch
  applies `loadedFuelState` when non-null.

### Tests to ship

- `RunSession_Advance_DecrementsFuel_test` — Combat beacon at cost 8 with chassis 1.0 →
  Fuel.Current decreases by 8.
- `RunSession_Advance_ResetsStormOnHaven_test` — Haven beacon → StormCounter =
  StormCounterStart, Fuel.Current = ceil(Max × HavenFuelRefillPercent) clamped to Max.
- `RunSession_Fuel_PersistsAcrossSaveLoad_test` — mid-run FuelState round-trips through
  DTO with Current, Max, StormCounter, StormCounterStart intact.
- `FuelStateDto_SchemaRegistry_Unique_test` — ADR-0004 registry uniqueness sweep picks
  up new SYSTEM_ID.
- `RewardSource_ScrapBeforeFuel_test` — fixed-seed roll returns hand-picked (scrap, fuel)
  tuple; test flips if any `IRewardSource.Generate` implementation reorders draws.

### Explicit non-goals (deferred to later slices)

- No animation seam / 3s ticker — Slice D.
- No faucet card wiring / storm strip UI — Slice E.
- No UI hookup — model-layer plumbing only.
- `FlatScrapRewardSource.Fuel` stays 0 for M1; actual faucets land in Slice E.

## Files planned to touch (grounded in 2026-07-04 verdict §"Files read to ground this verdict")

- `Assets/Scripts/Run/RunState.cs`
- `Assets/Scripts/Run/RunSession.cs`
- `Assets/Scripts/Run/RunController.cs`
- `Assets/Scripts/Run/CombatReward.cs`
- `Assets/Scripts/Run/FlatScrapRewardSource.cs`
- `Assets/Scripts/Save/Dtos/FuelStateDto.cs` (new)
- `Assets/Scripts/Save/Adapters/FuelStateSerializable.cs` (new)
- `Assets/Scripts/Save/SaveBootstrap.cs` (or equivalent registration site)
- `Assets/Scripts/Save/link.xml` — IL2CPP preservation grant
- `Assets/Scripts/CombatView/RunSceneHost.cs` — rehydrate branch
- `Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs` — TD scope addition
- `Assets/Scripts/CombatView/CombatDataInitializer.cs` — Configure(...) pass-through
- `Assets/Resources/CombatData/Chassis/Scout.asset` — re-bake with tank=35, burn=0.7f
- `Assets/Tests/EditMode/Run/*` — new test files above

## ADR contract references

- **ADR-0002** — engine-free `WastelandRun.Run` assembly preserved (no `UnityEngine.`
  references leak into RunState / RunSession / FuelState).
- **ADR-0003** — deterministic RNG: draw-order lock enforced by test.
- **ADR-0004** — distributed schema registry: new SYSTEM_ID unique; standalone group
  (NOT joined to `run.session_core`); asymmetric exhaustion (missing FuelState → fresh
  tank on load-fail, run continues).
- **ADR-0011** — no bridges: additive `CombatReward.Fuel = 0` is composition, not
  bimodal path.
- **ADR-0013** — CombatReward extends additively; `IRewardSource` stays scrap+fuel
  (one axis) — NOT split into `IFuelRewardSource` (see 2026-07-04 verdict §Q5 rationale).

## Success criteria (verdict §"Success criteria")

- Save round-trip test writes `Fuel.Current=42, StormCounter=17`, corrupts everything
  else, and fuel comes back intact from standalone group.
- `RewardSource_ScrapBeforeFuel_test` fails if any developer swaps draw order inside a
  `Generate` implementation.
- Zero UnityEngine references in new FuelStateDto / FuelStateSerializable test files
  (they can Unity-reference — POCO discipline is FuelState-side; DTOs live under Save).

## Estimate

Per 2026-07-04 verdict: ~2 focused sessions.

---

*Filed by executor (following 2026-07-04 TD verdict). No new architectural decisions —
this pointer exists solely for the today-dated hook precondition on new public type
introductions (`FuelStateDto`, `FuelStateSerializable`).*
