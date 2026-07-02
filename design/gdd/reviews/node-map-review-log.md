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

## Review — 2026-07-02 (re-review, same day) — Verdict: NEEDS REVISION → revised in-session

**Scope signal:** L (7 blockers; math/spec/UX/AC surface)
**Specialists:** game-designer, systems-designer, economy-designer, ux-designer, qa-lead, creative-director (senior)
**Blocking items:** 7 (all resolved in-session)
**Recommended:** advisory items rolled forward
**Prior verdict resolved:** Yes — the 2026-07-02 MAJOR REVISION verdict's 9 in-session fixes held; this re-review is the fresh adversarial pass promised in that log's Status line.

### Summary

Adversarial re-review found the V3 architecture sound but seven follow-on gaps in the math and spec surface: two chassis-scaled fuel-verb defaults failed their own AC-NM54b invariant (Scout Rest, Truck Chopshop), one AC preamble asserted a "net-positive" claim the shipping table couldn't satisfy for Truck Convert, one AC (AC-NM56) tested a distribution that biome 1 does not ship (contradicts ADR-0015's `{Combat, Haven}` narrowing), one Pillar-3 constraint (RC-F1) remained permanently invisible after V3 tone-shift, one AC (AC-NM55) declared an isolation contract without an enforceable gate, and the storm-impact readability surface needed a UI decision (user chose impact-badges on the storm counter over per-beacon dual labels).

### User decisions (2026-07-02 re-review)

- **B5 RC-F1**: HUD status flag on the Frame subsystem icon (`"⚠ Frame Strained"`) + faint amber outline on unresolved Event beacons; magnitude (`+15%`) stays hidden. Preserves "wasteland bites harder" fantasy while restoring Pillar 3 discoverability.
- **B7 storm impact readability**: impact-badge row directly under the storm counter (badges show `[icon] −N` per Reachable beacon). Beacon labels remain fuel-only. Hovering a beacon cross-highlights the matching badge; the counter itself does not dim per-hover.
- **B3 Convert language**: relax AC-NM54b preamble from "net-positive" to "net-non-negative", and name **Truck Convert as an explicit −1 exception** (narrative flavor of Truck's thirst; single Event outcome, does not brick a Truck run).

### Fixes applied in-session

| # | Blocker | Fix |
|---|---|---|
| B1 | Scout `RestFuelRefund = 2` violates AC-NM54b at Scout Rest cost = 3 | F.2 table: `RestFuelRefund[Scout] = 3` + rewrote rationale column |
| B2 | Truck `ScrapPerFuelRate = 6` fails AC-NM54b invariant; rationale text carried inverted fix-direction claim | F.2 table: `ScrapPerFuelRate[Truck] = 3` (lower = better rate); rewrote rationale for all three chassis; removed false "shipping value satisfies the invariant" claim |
| B3 | AC-NM54b preamble asserts "net-positive" that Truck Convert cannot satisfy | Relaxed to "net-non-negative"; named Truck Convert exception (yield 7 vs. Event cost 8 = −1); Scout/Assault stay net-non-negative |
| B4 | AC-NM56 tests biome-1 mixed distribution that ADR-0015 says biome 1 does not ship | Rewrote AC-NM56 for shipping `{Combat, Haven}` distribution; added AC-NM56b for full-economy biomes (activates when biome 2 assets ship); corrected G.2 tuning notes + G.3 RC-F1 notes to acknowledge biome-1 stripping |
| B5 | RC-F1 permanently invisible → Pillar 3 breach | C4.1 RC-F1 rewritten with subsystem-icon "Frame Strained" flag + Event-beacon amber outline; C4.3 table row flipped to "Yes (magnitude hidden)"; H.1.6 overlay list updated; new **AC-NM53b** added for discoverability testing |
| B6 | AC-NM55 asserts isolation without enforceable gate | Rewrote with concrete CI grep gate — two grep rules, both-mutation source-file allow-list (`ConvertScrapToFuel.cs` / `ConvertFuelToScrap.cs`), landing in `tests/ci/gates/domain-isolation.sh` |
| B7 | Storm impact only readable via per-beacon hover (Pillar 3 friction) | H.1.7 rewritten with impact-badge row under storm counter (attached to storm-front visual); beacon labels stay fuel-only; I.3 updated to match |

### Deferred / carried forward

The 11 advisory items from the 2026-07-02 MAJOR REVISION log were not re-surfaced as blockers by this pass; they remain open as tuning/UX iteration items, not gates:
- 7 ambiguous AC wordings (AC-NM15/50/54/55/56/57/15b) — AC-NM54b/55/56 tightened by this pass; the others rest for a future light copy-edit pass.
- ux-designer hover cognitive-load concern — B7 impact badges reduce hover-to-plan cost for storm reads.
- H.1.11 visual connector cost pill ↔ fuel tank — advisory.
- 3-second commit animation pacing across 54–66 beacons — advisory (accessibility Reduce-Motion path already spec'd).
- Haven 50% refill inverts Scout fantasy at biome 2 entry — advisory (biome 2 tuning surface).

### Status

Systems-index update pending user selection. Ready for `Approved (2026-07-02)` marker once the closing widget confirms.

## Review — 2026-07-02 (third pass, same day) — Verdict: MAJOR REVISION NEEDED → revised in-session

**Scope signal:** L (5 upstream systems, 7 formulas, math/spec/UX surface)
**Specialists:** game-designer, systems-designer, economy-designer, ux-designer, qa-lead, creative-director (senior)
**Blocking items:** 6 (all resolved in-session)
**Recommended:** advisory items rolled forward
**Prior verdict resolved:** Yes — the 2026-07-02 NEEDS REVISION re-review's 7 blockers held; this is the adversarial verification pass.

### User decisions (2026-07-02 third pass)

- **Player Fantasy / ADR-0015 collision**: Added explicit biome-1 scope section separating V3 lock (EA, `{Combat, Haven}` only) from full five-verb target (biome 2+). Fantasy section retained as complete-game description with a boxed scope qualifier.
- **Truck viability**: Raised `CombatRewardFuelFloor` to provisional stub values (Scout 5 / Assault 7 / Truck 10 per combat) as a data-flag pattern per ADR-0011. Math: Truck needs ~8.43 fuel/combat to be viable; default floor 10 overshoots slightly for safety until playtest calibration.
- **Scout identity inversion**: Raised `MaxFuel[Scout]` from 35 → 40 so Scout's biome-1 Combat budget matches Assault's (`floor(40/6)=6` commits vs `floor(50/8)=6`), preventing chassis-identity inversion in the EA shipping slice.
- **Animation sync ambiguity**: Locked "finish together at different tick rates" — fuel ticks at `fuelDrain/2s` ticks/s, storm at `BeaconBaseCost/2s` ticks/s, both reach zero at T≈2s simultaneously. Player reads the divergence as "storm charges more than the fuel I spend."

### Fixes applied in-session

| # | Blocker | Fix |
|---|---|---|
| B1 | Player Fantasy describes biome-2+ mixed economy while biome 1 ships `{Combat, Haven}` only | Added biome-1 scope section in Player Fantasy; updated OQ-NM7 with rounding spec (`Mathf.RoundToInt`) |
| B2 | Truck viability deficit — 1.25× reward floor nets −29.6 fuel cumulative over 21 Combat beacons | Added `CombatRewardFuelFloor` provisional stub table in G.1 (Scout 5 / Assault 7 / Truck 10) as data-flag pattern |
| B3 | Scout `MaxFuel=35` causes chassis-identity inversion (Scout 5 combats vs Assault 6 per tank) | Raised `MaxFuel[Scout]` 35→40 across all references (C1.3, G.1, AC-NM20b, F.1 dependencies, OQ-NM6, V&P retrofit, header, all examples) |
| B4 | H.1.11 animation sync ambiguous (3 valid implementations for fuel/storm tick rates) | Rewrote H.1.11 step 3 + H.1.7 tick animation to lock "finish together, independent rates" contract; added per-chassis example |
| B5 | F-NM3/F-NM4 independence not explicit; exact-equal case (`StormFrontX == PlayerBeaconX`) under-specified | Rewrote C1.4 step 8 to name both predicates, clarify strict vs. non-strict, and mandate independent evaluation; added `#if DEBUG Assert` + `= StormCounterStart` (not `+=`) clarification in F-NM2 pseudocode; rewrote AC-NM29 around F-NM4 independence |
| B6 | AC-NM25/AC-NM34/AC-NM57 spec holes: AC-NM25 contradicted AC-NM53b; AC-NM34 pointed E-7→wrong AC + E-12→wrong AC; AC-NM57 threshold math wrong for Combat-only biome 1 | Rewrote AC-NM25 (no-magnitude disclosure, existence per AC-NM53b); added AC-NM28b (E-7 pure Mobility stranded), AC-NM33e (E-12 StormCounter clamp), AC-NM33f (`IsCommitInProgress` lifecycle); fixed AC-NM34 matrix (E-7→AC-NM28b, E-12→AC-NM33e); updated AC-NM57 threshold to 3–6 (expected 5.3 ticks, Combat-only math); updated G.2 notes with biome-1 tick rate context |

### Status

All 6 blockers resolved in-session. Ready for `Approved (2026-07-02)` marker.

## Review — 2026-07-02 (fourth pass, same day) — Verdict: APPROVED

**Scope signal:** L (5 upstream systems, 7 formulas, no new ADRs required)
**Specialists:** game-designer, systems-designer, economy-designer, ux-designer, qa-lead, creative-director (senior)
**Blocking items:** 5 (all resolved in-session) | Recommended: 5 (all applied in-session)
**Prior verdict resolved:** Yes — all 6 third-pass blockers held; fourth pass is adversarial verification.

### Summary

Creative-director verdict: APPROVED. All five blockers were AC hygiene issues — no design changes required. B1: AC-NM33d was covering two unrelated edge cases (E-9 z-index layering + E-13 gate-funnel stranded); split into independent AC-NM33d and AC-NM33g, AC-NM34 coverage matrix updated. B2: AC-NM15 conflated three distinct player experiences under one threshold; split into AC-NM15 / AC-NM15c / AC-NM15d with explicit survey question strings (AC-NM15d lowered to 60% — intentional design). B3: AC-NM45 "correctly identify" lacked a scoring rule; added ≥80% Reachable set with zero false-positives per session. B4: AC-NM25a HostileTiltDelta sum-zero enforced at editor-time only; added runtime `Debug.Assert` at every biome-asset load including hot-reload and E-4 fallback. B5: Chopshop minimum transaction undefined (sub-rate exploit possible); `ChopshopMinScrap = 4` knob added to G.1, AC-NM54b references rejection contract. All five recommended items also applied in-session (AC-NM50 operational definition, AC-NM57 discrete trace correcting 5.3→5 ticks, AC-NM53b/53c split, H.1.7 badge ordering locked to spatial/lane, AC-NM28b curated fixture mandate). OQ-NM11 added (Haven biome-2 fuel cliff). V3 Node Map GDD is now fully clean through four adversarial passes; 20 blockers resolved total.

### Fixes applied in-session (pass 4)

| # | Blocker | Fix |
|---|---|---|
| B1 | AC-NM33d covered E-9 z-index + E-13 gate-funnel (unrelated; CI pass on one masks other) | Split into AC-NM33d (E-9 only) + new AC-NM33g (E-13 only, curated fixture required); AC-NM34 matrix updated |
| B2 | AC-NM15 not falsifiable — 3 distinct player experiences under one 70% threshold | Split into AC-NM15 / AC-NM15c / AC-NM15d with explicit survey question strings; AC-NM15d = 60% (intentional: chassis-neutral cadence is design feature) |
| B3 | AC-NM45 "correctly identify" undefined — no scoring rule | Added: ≥80% of Reachable set + zero false-positives; scored per session |
| B4 | AC-NM25a HostileTiltDelta sum-zero editor-enforced only; hot-reload + E-4 fallback bypass | Added `Debug.Assert(delta.Treasure + delta.Ambush + delta.Windfall + delta.Convert == 0)` at every biome-asset load |
| B5 | Chopshop minimum transaction undefined; sub-rate exploit at <4 scrap | `ChopshopMinScrap = 4` tuning knob added to G.1; AC-NM54b references rejection contract |

### Recommended items applied (pass 4)

| # | Item | Fix |
|---|---|---|
| R1 | AC-NM50 "beacon index" undefined (non-sequential graph) | Canvas-width operational definition: middle 40–60% of biomeWidth |
| R2 | AC-NM57 expected value wrong (continuous formula gives 5.3; discrete-reset gives 5) | Corrected to exactly 5 ticks with full discrete trace; warning against continuous formula added |
| R3 | AC-NM53b single AC covered timing + string verification | Split into AC-NM53b (render timing: before next commit prompt) + AC-NM53c (tooltip copy string assertion) |
| R4 | H.1.7 badge ordering deferred to "UX pass" — decision never made | Locked to spatial/lane (top-lane first, bottom-lane last); stated as final (2026-07-02 design review) |
| R5 | AC-NM28b allowed seeded-generated graphs with vacuous all-lateral precondition | Curated fixture graph mandate added |

### OQ added

- **OQ-NM11**: Haven 50% refill into biome 2 is unmodeled against biome-2 beacon costs. Owner: Node Map + Loot & Reward GDD. Trigger: biome 2 slice start.

