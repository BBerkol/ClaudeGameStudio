# Capture — `design/gdd/node-map.md` V3 fuel-as-clock rewrite

**Date:** 2026-07-02
**System:** Node Map GDD (`design/gdd/node-map.md`)
**Trigger:** V3 fuel-as-clock mechanic locked (Path A refuel-driven, base-cost storm decrement, chassis-neutral counter) — retires V1 combat-commit storm cadence.
**User authorization:** "approve" on the section-by-section rewrite plan (2026-07-02 session).
**Companion TD verdict:** `production/td-verdicts/2026-07-02-v3-fuel-as-clock-architecture.md` (CONCERNS — 3 non-blocking items, all resolved in-session as D1–D4).

## Authored values being destroyed (before rewrite)

Enumerated from `git show HEAD:design/gdd/node-map.md` — the values that this rewrite retires or overwrites. Every value is preserved somewhere: either restated in the V3 form, deferred to a knob, or explicitly noted as retired.

### Retired formulas / mechanics

1. **`CombatCommitCounter`** — V1 storm cadence integer state.
   - Retired form: incremented on `Combat`/`EliteCombat` reward-screen close, reset on tick.
   - V3 replacement: `StormCounter` — decrements by `BeaconBaseCost` on *every* commit, chassis-neutral.

2. **`ChassisStormCadence[]`** table — chassis-differentiated combat-count to tick.
   - Retired values: Scout `3`, Assault `2`, Truck `1`.
   - V3 replacement: retired. Chassis identity at map layer = `MaxFuel` tank size + `ChassisMultiplier` fuel-drain multiplier. Storm cadence is chassis-neutral (`StormCounter` = 30 for all).
   - Design-safety property: V1 required "Scout > Assault > Truck ordering." V3 removes the ordering constraint because the chassis-differentiation channel moves to tank size (Scout 35 < Assault 50 < Truck 75 — preserved ordering, different property).

3. **F-NM2 `StormAdvance`** formula — combat-triggered storm tick.
   - Retired signature: `OnCombatRewardClosed() → CombatCommitCounter+=1 → tick-check`.
   - V3 replacement: `F-NM2 StormCounterDecrement(baseCost) → StormCounter -= baseCost → tick-check`.

4. **F-NM7 `ChassisStormCadence`** — the lookup table formula section.
   - Retired entirely. Retired marker paragraph left in place so cross-refs from historical commits still resolve.

### Retuned tuning values

| Knob | V1 value | V3 value | Rationale |
|---|---|---|---|
| `FuelCostBase` | 2 (single knob per commit) | Retired — replaced by `BeaconBaseCost[type]` table | V1 flat base + chassis mul → V3 per-beacon-type table, chassis-neutral |
| `ChassisMultiplier[Scout]` | 0.8 | 0.7 | Widen chassis fantasy split (0.7 / 1.0 / 1.5 vs 0.8 / 1.0 / 1.3) |
| `ChassisMultiplier[Truck]` | 1.3 | 1.5 | Same — bigger drain-per-commit spread |
| `MaxFuel[Scout]` | (undefined in V1 GDD; V&P-owned) | **35** | New V3 knob — tank sizing |
| `MaxFuel[Assault]` | (undefined) | **50** | New |
| `MaxFuel[Truck]` | (undefined) | **75** | New |
| `StormCounterStart` (V3 name) | N/A — V1 used chassis cadence | **30** | New — chassis-neutral |
| `HavenFuelRefillPercent` | N/A | **0.5 (50%)** | New — biome-end refill |
| `FuelCostLateralSurcharge` | 1 (safe range 0–3) | Retired (deferred) | V3 storm counter already prices lateral moves; ship without additive surcharge |
| `EngineOfflineSurcharge` | 1 | 1 (interim) — flag OQ-NM7 for percentage rescale | Flat +1 negligible under V3 base costs (8, 12); rescale deferred |

### New tuning surface added

| Knob | V3 value | Owner |
|---|---|---|
| `BeaconBaseCost[Haven]` | 0 | `BiomeDistributionSO` |
| `BeaconBaseCost[Rest]` | 3 | `BiomeDistributionSO` |
| `BeaconBaseCost[Merchant]` | 4 | `BiomeDistributionSO` |
| `BeaconBaseCost[Chopshop]` | 4 | `BiomeDistributionSO` |
| `BeaconBaseCost[Event]` | 5 | `BiomeDistributionSO` |
| `BeaconBaseCost[Combat]` | 8 | `BiomeDistributionSO` |
| `BeaconBaseCost[EliteCombat]` | 12 (Combat × 1.5) | `BiomeDistributionSO` |
| `CombatReward.Fuel` | Roll site on `IRewardSource.Generate`, salt `0x5257`, draw order LOCKED (Scrap → Fuel) | ADR-0013 V3 amendment |

### Edge cases + acceptance criteria retuned (not deleted)

- **E-12** `CombatCommitCounter` corruption at save-load → renamed to `StormCounter` corruption; clamp behavior parallel.
- **AC-NM10 / AC-NM11 / AC-NM12** rewritten around `StormCounter` decrement instead of `CombatCommitCounter` cadence.
- **AC-NM15** Pillar-2 playtest survey retuned: no more "Scout feels breath-room; Truck feels chased" cadence contract → V3 contract is "chassis-different *tank drain*, same storm cadence."
- **AC-NM50** Pillar-2 Reachable-set-size test preserved (chassis still shapes map via drain, not cadence).
- **AC-NM54** Truck compensation contract preserved (Loot & Reward Truck value uplift 1.25×).

### Player Fantasy edits

- Pillar 2 paragraph reframed: "smaller tick, not immunity."
- Storm paragraph reframed: no longer "the wasteland reclaiming the road on combat-ticks" → "storm counter closes with every commit, tank drain and storm decrement are the same beat felt twice."

### New sections added

- **H.1.11 Commit Travel Animation (V3)** — 3-second vehicle traversal with synced 1-by-1 tickers on fuel tank + node cost pill + storm counter; mid-travel storm advance if counter → 0; combat starts after settle. Anchors D1 decision from V3 lock session.
- **OQ-NM7** — `EngineOfflineSurcharge` percentage rescale trigger flagged.

## Technical Director Review

Full verdict at `production/td-verdicts/2026-07-02-v3-fuel-as-clock-architecture.md`.

**Verdict:** CONCERNS (3 non-blocking items resolved in-session as D1–D4)

**Q1 — Fuel state location:** APPROVE Option A (single `FuelState` POCO on `RunState`). Combat should not see fuel (CD Cond 2). Migration ~150 LOC + tests.

**Q2 — `CombatReward.Fuel`:** APPROVE as ADR-0013 clean additive. `public sealed record CombatReward(int Scrap, CardOffer Choices, int Fuel = 0)`. Roll site: extend `IRewardSource.Generate → (int Scrap, int Fuel)`. Deterministic seed salt `0x5257`; draw order LOCKED (scrap first, fuel second) — CI-checked with known-seed unit test. Migration ~200 LOC + tests.

**Q3 — `BiomeDistributionSO` extension:** PARTIAL APPROVE.
- `int[] BeaconFuelCosts` by BeaconType → APPROVE
- `int StormCounterStart` → APPROVE
- `float HavenFuelRefillPercent` → APPROVE
- `float BiomeStartingFuelModifier` → DEFER (YAGNI at biome 1)
- `float[] depthCostModifier` per strip → REJECT (grammar retired by ADR-0015 Block 2)

Migration ~30 LOC + asset re-serialize.

**Q4 — Save shape:** APPROVE Option (a) additive fields on `RunStateDto` (FuelCurrent, FuelMax, StormCounter) with SCHEMA_VERSION++. `MaxFuel` **live-derived** from Chassis on FromDto (post-EA balance patches take effect on existing saves). `StormCounterStart` / `HavenFuelRefillPercent` / beacon cost table NOT persisted — live-read from biome SO on resume (ADR-0003 Rule 3).

**Q5 — CD Condition 3 ("Storm does NOT advance during combat"):** STRICT-COMPATIBLE. Storm decrement + advance fire at commit pipeline step 5 (before `EnterCombat`). Wording risk on animation timing resolved by D1 (3-sec travel with synced tickers, mid-travel storm advance visible).

**Top 3 Risks (all resolved in-session):**
1. Fuel roll seed ordering (Q2) → D4 accepted: draw order LOCKED (scrap → fuel).
2. `FuelState.Max` snapshot vs. live-derive (Q1/Q4) → D3 recommendation applied: live-derive.
3. `depthCostModifier` rejection is soft → D2 accepted rejection.

**ADR delta on implementation:**
- ADR-0002: add one line to "systems composed under RunState" list (`FuelState`).
- ADR-0013: amendment for additive `CombatReward.Fuel` + `IRewardSource.Generate` return-shape bump.
- ADR-0015: amendment paragraph documenting fuel/storm tuning on `BiomeDistributionSO`.
- ADR-0004: `RunStateDto` `SCHEMA_VERSION` bump (standard).

## Approval sequence

- User: "3 1 2 yes" → step 3 (commits) done, step 1 (TD consult) done, step 2 (GDD rewrite) in flight (this file).
- User: "confirm" on retuned numbers (2026-07-02) → tanks 35/50/75, base costs 3/4/4/5/8/12, counter 30.
- User: "approve" on section-by-section rewrite plan (2026-07-02) → this rewrite is executing under that approval.

## Post-review addendum (2026-07-02 late session)

Adversarial `/design-review` pass on the V3 GDD returned MAJOR REVISION NEEDED — 9 blockers surfaced (7 vocabulary/formula/AC + Pillar 2 header collision + Truck fuel-verb economy hole). Blockers addressed in-session with two user design decisions:

- **Pillar 2**: accept chassis-neutral cadence, rewrite Pillar 2 mapping to name tank-size + drain-multiplier as the identity channel (V1 cadence-based identity contract retired).
- **Truck economy**: chassis-scale Rest / Convert / Chopshop values (Rest 2/3/5, Convert 4/5/7, ScrapPerFuel 4/4/6) via retrofit contract on Node Encounter GDD.

Round-trip audit captured in the review log at `design/gdd/reviews/node-map-review-log.md`. GDD status: revised in-session, pending fresh-session re-review pass for adversarial verification.
