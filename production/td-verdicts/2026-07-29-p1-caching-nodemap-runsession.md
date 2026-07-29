# P1 Caching: NodeMap.ForwardEdgesFrom + RunSession.IsStrandedForFuel

Companion implementation-gate file for the `whole-game-health-opt-audit.md`
synthesis (2026-07-29). The parent audit document contains the full analysis;
this file satisfies the hook's same-day `## TD Verdict` heading requirement and
records the implementation-level architecture decisions made during the apply
pass.

Parent verdict: `production/td-verdicts/2026-07-29-whole-game-health-opt-audit.md`
(§3 P1-1 and P1-2 are the authoritative sources for what and why.)

## TD Verdict

**ACCEPT** — both changes are pre-approved by the 2026-07-29 whole-game audit.
No new public API surface. No ADR drift. Apply as specified.

### P1-1 — `NodeMap.cs` — ForwardEdgesFrom precompute

Status: **ACCEPT**

Change: Add `int[][] _forwardEdgeCache` field; populate in ctor after edge
validation loop (edges are immutable post-ctor, so build-once is correct for
the full run lifetime). Replace the per-call `new List<int>()` walk with
`return _forwardEdgeCache[beaconIndex]`. Return type stays `IReadOnlyList<int>`;
the concrete backing type changes from `List<int>` to `int[]`, which is
`IReadOnlyList<int>`-compatible and zero-allocation per-call.

Consumer scan: `RunController.ForwardEdgesFrom`, two test files
(`RunSceneHost_Test.cs`, `RunSceneHost_EnqueuesWrite_Test.cs`), and
`RunSession.IsStrandedForFuel`. All iterate only — no `.Add()` call sites
found. Return-type change is signature-compatible; no call-site changes needed.

ADR alignment: Pure optimization, no behavioral change. ADR-0002 (POCO
model, engine-free) and ADR-0003 (determinism) unaffected. ADR-0011: no new
bridge, no parallel storage, no bimodal path.

### P1-2 — `FuelState.cs` + `RunSession.cs` — IsStrandedForFuel cache

Status: **ACCEPT**

**Architecture chosen: Option A** — `event Action OnFuelChanged` on `FuelState`.

Rationale: `ScrapEconomy.TryConvertScrapToFuel`, `TryConvertFuelToScrap`, and
`GrantFuel` all call `FuelState` methods directly (not routed through
`RunSession`). Option B would require threading invalidation through
`IScrapEconomy` and `EventHandler` — wider blast radius, ADR-0011 smell.
Option A: `FuelState` fires `OnFuelChanged` from every mutating method
(`Spend`, `RefillPartial`, `CreditFuel`, `DrainRaw`). `RunSession` subscribes
in ctor and sets `_isStrandedDirty = true` on every fire. The event delegate
is an `Action` (not `UnityEvent`, per ADR-0002 + technical-preferences no-UnityEvent
rule). Delegate lifetime is tied to `RunSession` lifetime — no subscriber leak.
`RestoreFromSnapshot` is save-layer only; it also fires `OnFuelChanged` so a
load-time restore correctly dirties the cache on the first `IsStrandedForFuel`
query after resume.

**Files touched:**
- `Assets/Scripts/Run/NodeMap.cs` (P1-1)
- `Assets/Scripts/Run/FuelState.cs` (P1-2: add `OnFuelChanged` event)
- `Assets/Scripts/Run/RunSession.cs` (P1-2: cache fields + dirty subscription)

**Rest-refill scenario verification:**
Player arrives at Rest node → in-mode repairs (no fuel mutation) → player
confirms Continue → `ResolveRest()` fires `OnRestModelCommitted` → beacon
latched resolved. No fuel mutation here; cache stays valid. If the event
payload includes a fuel grant (e.g. a Windfall-fuel event), `GrantFuel` calls
`CreditFuel` → `OnFuelChanged` fires → `_isStrandedDirty = true`. Next
`IsStrandedForFuel()` call recomputes from fresh `Fuel.Current`. Correct.

Haven refill scenario: `Advance` calls `fuel.RefillPartial(...)` →
`OnFuelChanged` fires → dirty. Correct.

Combat victory fuel reward: `ExitCombat` calls `_controller.State.Fuel.CreditFuel(reward.Fuel)` →
`OnFuelChanged` fires → dirty. Correct.

Convert event (scrap→fuel): `ScrapEconomy.TryConvertScrapToFuel` calls
`fuel.CreditFuel(fuelOut)` → `OnFuelChanged` fires → dirty. Correct.

Convert event (fuel→scrap): `ScrapEconomy.TryConvertFuelToScrap` calls
`fuel.DrainRaw(fuelIn)` → `OnFuelChanged` fires → dirty. Correct.

**ADR alignment:** ADR-0002 (engine-free POCO) — `event Action` is plain C#,
no Unity dependency. ADR-0011 — no bridge, no parallel state. ADR-0003 — no
RNG involved. technical-preferences — `event System.Action`, not `UnityEvent`.
