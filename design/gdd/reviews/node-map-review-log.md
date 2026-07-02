# Node Map GDD — Review Log

Tracks all `/design-review` verdicts, revision rounds, and re-review outcomes for `design/gdd/node-map.md`.

## Review — 2026-07-02 — Verdict: MAJOR REVISION NEEDED (addressed in-session)

**Scope signal:** L (5 upstream systems, 7 formulas, 3 ADR amendments, 1 schema bump)
**Specialists:** game-designer, systems-designer, economy-designer, ux-designer, qa-lead, creative-director (senior)
**Blocking items:** 9 (7 spec-integrity + 1 Pillar 2 decision + 1 economy structural)
**Recommended:** 11
**Prior verdict resolved:** First review of V3 (V1 was Approved 2026-04-21)

### Summary

First adversarial pass on the V3 fuel-as-clock rewrite (retires V1 combat-commit storm cadence + `ChassisStormCadence`, ships chassis-neutral `StormCounter` decremented by `BeaconBaseCost`). Creative-director flagged three load-bearing concerns: (1) Pillar 2 collision — economy-designer's math showed all 3 chassis converge at 5–6 all-combat commits, degrading the chassis-identity signal V1 encoded via cadence; (2) Truck's fuel-verb economy — Rest / Convert / Chopshop all net-negative for Truck at 1.5× drain; (3) V1 vocabulary residue survived the rewrite (`storm cadence pips`, `CombatCommitCounter`, retired multipliers 0.8/1.0/1.3). Plus qa-lead surfaced AC-NM34's false coverage claim (5 of 13 edge cases actually have ACs) and 3 missing invariant ACs.

### User decisions (2026-07-02)

- **Pillar 2**: Accept chassis-neutral cadence + rewrite Pillar 2 mapping to name tank-size (35/50/75) + drain-multiplier (0.7/1.0/1.5) as the identity channel. V1 cadence-based identity contract retired.
- **Truck economy**: Chassis-scale Rest refund + Convert yield + ScrapPerFuelRate. V3 defaults locked: Rest 2/3/5, Convert 4/5/7, ScrapPerFuel 4/4/6.

### Fixes applied in-session

| # | Blocker | Fix |
|---|---|---|
| 1 | I.1/I.3 storm cadence pip vocabulary | Rewrote HUD ASCII, bottom-bar description, I.3 element list around `StormCounter` numeric + ring readout |
| 2 | I.5 screen-reader V1 language | Rewrote text alternative around counter + hover preview |
| 3 | E-6 chassis swap V1 references | Rewrote around `ChassisMultiplier` + `MaxFuel` + `FuelState.Max` clamp |
| 4 | AC-NM36 references retired `CombatCommitCounter` | Rewrote around `StormCounter` + `FuelState` + live-derived `MaxFuel` |
| 5 | F-NM1 Haven formula returns 1 (should be 0) | Added explicit `if B.Type == Haven: return 0` branch + rationale |
| 6 | V&P retrofit V1 multipliers 0.8/1.0/1.3 | Fixed to 0.7/1.0/1.5 + added `MaxFuel` line + `FuelState.Spend` shape |
| 7 | Card Combat retrofit uses V1 `CombatReward` + `CombatCommitCounter` | Rewrote around V3 `CombatReward(Scrap, Choices, Fuel=0)` per ADR-0013 |
| 8 | Pillar 2 mapping V1 in header | Rewrote to name tank + drain as identity channel; chassis-neutral cadence documented as V3 lock |
| 9 | Truck fuel-verb economy hole (structural) | Added AC-NM54b (net-positive verb contract) + V3 fuel-verb retrofit table on Node Encounter F.2 |

### AC coverage additions

- AC-NM34 rewritten with explicit E-1 → E-13 coverage matrix (was false claim of "all 13")
- AC-NM33a (E-2 combat defeat), AC-NM33b (E-4 graph gen failure), AC-NM33c (E-8 consumed reappearance), AC-NM33d (E-9 noise band overlap + E-13 gate stranded)
- AC-NM20d (MaxFuel clamp on `CombatReward.Fuel`), AC-NM20e (Engine-offline surcharge does not touch storm decrement), AC-NM25a (`HostileTiltDelta` sum-zero invariant)

### Deferred to fresh-session re-review

Advisory items (11) held for the re-review pass:
- 7 ambiguous ACs (AC-NM15/50/54/55/56/57/15b wording)
- ux-designer's hover-preview cognitive-load concern (3 hovers required)
- H.1.11 visual connector between cost pill and fuel tank
- 3-second animation × 54–66 beacons pacing cost (skip / hold-to-fast-forward affordance)
- Haven 50% refill inverts Scout chassis fantasy at biome 2 entry (17-fuel start)

### Status

Revised in-session; **pending fresh /design-review pass** in new session for adversarial verification. Systems-index not yet updated to Approved — awaiting re-review verdict.

Companion V3 rewrite capture: `production/polish-captures/2026-07-02-node-map-v3-rewrite.md`
V3 TD verdict: `production/td-verdicts/2026-07-02-v3-fuel-as-clock-architecture.md`
