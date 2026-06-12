# Cross-GDD Review — 2026-04-24

**Scope**: All 10 MVP-tier GDDs + game concept + pillars
**Review mode**: full
**Run by**: `/review-all-gdds` skill (parallel consistency + design-theory passes)
**Verdict**: **FAIL** — 4 unique BLOCKERS must be resolved before Systems Design gate can close.

---

## Executive Summary

10/10 MVP GDDs are individually Approved and retrofit-propagated (per `production/session-state/active.md`). This review is the first cross-GDD consistency + design-theory pass run against the complete set.

Two parallel agent passes (consistency, design-theory) independently returned FAIL. Findings deduplicate to **4 blockers + 13 concerns**. Most blockers are single-line or single-section textual corrections against the authoritative registry; only blocker #1 (Enrage) and blocker #3 (ArchetypeFamily) require creative/design arbitration.

---

## Inputs

| Input | Source | Notes |
|---|---|---|
| Pillars | `design/gdd/game-concept.md` §2 | 5 pillars, 6 anti-pillars |
| Systems index | `design/gdd/systems-index.md` | 10/10 MVP Approved |
| Entity registry | `design/registry/entities.yaml` | 44 canonical constants — authoritative baseline |
| GDDs (all full-read) | `design/gdd/*.md` | card-system, card-combat-system, enemy-system, status-effects, vehicle-and-part-system, scrap-economy, save-persistence, node-map, node-encounter, loot-reward |

---

## Blockers (4 unique)

### 🔴 BLOCKER-1 — Enrage damage model contradiction

| GDD | Rule | Location |
|---|---|---|
| Card Combat | `EnrageBonus = +2 + (turn - EnrageTurn)` — additive flat bonus, escalates +1/turn | AC-CC12/13/14, Interactions |
| Enemy System | Enrage = `DamageMultiplier ×1.5` applied to base intent damage — multiplicative, flat | D.2 resolution pipeline, Tuning Knobs |
| Registry | `DefaultEnrageDamageMultiplier = 1.5` (multiplicative form wins the authority tie) | `enemies.constants.DefaultEnrageDamageMultiplier` |

**Impact**: AC-CC12/13/14 simulation will fail against Enemy System's D.2 pipeline — two different formulas applied to the same intent resolution. First implementation pass will hit the contradiction immediately.

**Resolution owner**: `creative-director` (pacing/fantasy call — additive escalation creates a building-dread curve; multiplicative is a flat tilt). Registry's `×1.5` value should be treated as placeholder pending the arbitration.

---

### 🔴 BLOCKER-2 — V&P R10 Fuel multipliers wrong

| GDD | Value | Location |
|---|---|---|
| V&P R10 | `Scout 0.75 / Assault 1.0 / Truck 1.5` | `vehicle-and-part-system.md` line 229 |
| Registry | `FuelBurnMultiplier.{Scout:0.8, Assault:1.0, Truck:1.3}` | `entities.yaml` `vehicle.constants.FuelBurnMultiplier` |
| Node Map F-NM1 | `0.8 / 1.0 / 1.3` | `node-map.md` F-NM1 |
| Node Map retrofit note | "V&P retrofit spec: 0.8/1.0/1.3" | `node-map.md` line 1133 |

**Impact**: V&P is the nominal owner of the multiplier but ships with different numbers than registry + downstream consumer. Node Map already authored *the correction* for V&P at line 1133 — V&P R10 simply never received the edit.

**Resolution**: Pure textual correction on V&P R10. No design debate. Update V&P R10 line 229 to `Scout 0.8 / Assault 1.0 / Truck 1.3` matching registry.

---

### 🔴 BLOCKER-3 — ArchetypeFamily enum divergence

| GDD | Enum members | Purpose |
|---|---|---|
| Enemy System | `{Raider, Scavenger, Elite, Boss}` (4 values) | Art contract — silhouette family / visual archetype |
| Loot & Reward | `{Raider, Patcher, PitPacker, Elite, Boss}` (5 values) | Gameplay lookup axis — drop-table keying |

**Impact**: Any shared identifier named `ArchetypeFamily` cannot serve both purposes. Code will collide on the type name; one system's authoring will silently not work for the other.

**Resolution owner**: `creative-director` + `game-designer` joint call. Two viable fixes:
- **(a) Split axes**: Rename Enemy System's to `SilhouetteClass` (already the Combat HUD's carry-forward language from prototype conclusion), keep L&R's `ArchetypeFamily` as the gameplay-facing axis.
- **(b) Unify**: `{Raider, Patcher, PitPacker, Scavenger, Elite, Boss}` (6 values) — matches both visual and gameplay needs, but forces Enemy System to add 2 members it never used.

Recommend (a) — the split is already implicit in the Combat HUD carry-forward language.

---

### 🔴 BLOCKER-4 — V&P Player Fantasy contradicts V&P R11

| Claim | Location |
|---|---|
| "No per-part history, acquisition timestamps, or combats-survived counters are tracked" | V&P Player Fantasy line 22 |
| `public int CombatsSurvived { get; }` on InstalledPart | V&P R11 line 263 |

**Downstream consumers of `CombatsSurvived`**:
- Scrap Economy D.6 — tenure-refund formula reads it
- Save & Persistence — persists it in InstalledPartDTO

**Resolution**: Pure prose fix on V&P Player Fantasy. Retrofit (2026-04-23) added `CombatsSurvived` to R11 to close Scrap Economy's tenure-refund dependency; the Player Fantasy was not updated in the same pass. Remove the "no combats-survived counters" clause from line 22.

---

## Concerns (13)

### ⚠️ Consistency concerns (from Phase 2)

| # | Issue | GDDs involved | Recommended action |
|---|---|---|---|
| C1 | Save bidirectional dependency table stale vs. final handler-state subscribe model | Save, Node Encounter | Regenerate Save Dependencies table from current NE handler contract |
| C2 | V&P `IPartCatalog` empty-return contract unspecified (fallback to whitelist? no-op?) | V&P, Loot & Reward | Add explicit empty-return clause to V&P R12 |
| C3 | V&P `TryGrantFuel` overflow path — specify return value and Scrap conversion semantics | V&P, Scrap Economy, Node Encounter | V&P R10 add overflow clause |
| C4 | Card System PurgeCost removal — sweep all GDDs for any remaining PurgeCost prose | Card System, L&R, Scrap Economy | Grep pass + remove stragglers |
| C5 | Card Combat R13 CombatResult schema — verify every consumer reads the same field names | Card Combat, NE, Save | Build-time invariant test to verify |
| C6 | `PartRarityWeight.{60/30/10}` not explicitly registered in entities.yaml | Loot & Reward | Add to registry or confirm it's intentionally implicit |

### ⚠️ Design-theory concerns (from Phase 3)

| # | Issue | Risk |
|---|---|---|
| D1 | Progression loop competition — 3 parallel value meters (Scrap, Fuel, Parts) without a clear primary | Player ambiguity about what to optimize |
| D2 | Attention budget — 9 simultaneously active elements during Card Combat (beyond the 3-4 comfort ceiling) | Cognitive overload — mitigated by HUD clarity, but risk stands |
| D3 | Biome 3 Elite + Enrage envelope — `1.9 × 1.75 × (enrage)` compounding on a single node is the hardest state combination | Spike may feel unfair without telegraphs |
| D4 | Truck chassis power fantasy risk — `TruckRewardMultiplier = 1.25` + higher HP + higher Frame HP threatens pillar #4 (Scarcity with Agency) | Truck may become dominant choice rather than a trade-off |
| D5 | Ambush + Frame-Offline combined — survivability on first combat action if player starts with damaged Frame | Near-unwinnable start state without a mitigation rule |
| D6 | Boss pity counter ambiguity — does pity apply to Boss node, and does hitting it reset the counter? | Card System AC + NE need to agree |
| D7 | "Convert" verb framing register split — Events treat it as narrative flavor, Scrap Economy treats it as mechanical primitive | Tone inconsistency between handlers |

---

## Scenario Walkthroughs

Three cross-system scenarios walked; all flagged at least one issue covered in the blockers/concerns above. Summary preserved inline with blockers — no independent scenario-only findings.

1. **Enrage-turn resolution** — exposes BLOCKER-1 immediately on first enraged intent.
2. **Ambush start with pre-damaged Frame** — exposes D5; recommends a first-turn survivability floor.
3. **Boss node with L&R + Scrap pity + Card reward** — exposes D6 + C6.

---

## Chain-of-Verification

| Question | Answer |
|---|---|
| Did I mark any BLOCKER as CONCERN to soften the verdict? | No — blockers 1/2/3/4 are hard contradictions, not judgment calls. |
| Did both passes agree on the verdict? | Yes — both returned FAIL independently. |
| Could any concern be elevated? | D5 (Ambush + Frame-Offline) is close — floor depends on whether implementation adds a survivability rule. Keeping as concern pending that decision. |
| Did I verify against registry before declaring conflict? | Yes — registry was handed to Phase 2 agent as tiebreaker baseline. |
| Are any PASS items actually weaker? | Registry coverage of all 4 blockers is verified; L&R `PartRarityWeight` registry gap is flagged as C6. |

Chain-of-Verification: 5 questions — verdict **unchanged — FAIL**.

---

## Remediation Path

### Stage 1a — Textual corrections (minutes)
- **BLOCKER-2**: Edit `vehicle-and-part-system.md` R10 line 229 → `0.8 / 1.0 / 1.3`
- **BLOCKER-4**: Edit `vehicle-and-part-system.md` Player Fantasy line 22 → remove "no combats-survived counters" clause
- **C1**: Regenerate Save Dependencies table against current NE handler-state subscribe model
- **C2**: Add empty-return clause to V&P R12 `IPartCatalog`
- **C3**: Add overflow clause to V&P R10 `TryGrantFuel`
- **C4**: Grep + remove PurgeCost stragglers
- **C6**: Add `PartRarityWeight` entry to `entities.yaml` or note as intentionally implicit

### Stage 1b — Design arbitration (single session with creative-director)
- **BLOCKER-1**: Enrage damage model — additive escalation vs multiplicative flat. Registry has `×1.5` as placeholder; arbitrate and update BOTH Card Combat + Enemy System to match.
- **BLOCKER-3**: ArchetypeFamily — recommend split to `SilhouetteClass` (Enemy/art) + `ArchetypeFamily` (L&R/gameplay). Confirm.

### Stage 1c — Re-run cross-review
After Stage 1a+1b, re-run `/review-all-gdds` (or partial: `since-last-review`) to confirm closure. A PASS or CONCERNS verdict closes Systems Design gate.

### Stage 2 onward
Gate-check report's remediation plan takes over: `/create-architecture`, Foundation ADRs, `/ux-review combat-hud.md`, test scaffolding, CI.

---

## Next Decision Point

Two parallel tracks can run after this report:

- **Track A — Fast textual pass**: Work through Stage 1a (6 edits). No arbitration needed.
- **Track B — Creative arbitration session**: Spawn creative-director for blockers 1 + 3. Output feeds Stage 1a's Card Combat + Enemy System + L&R edits.

Recommend Track B first — Track A's final state depends on blocker 1's resolution (the Enrage formula sits in Card Combat AC-CC12/13/14 + Enemy System D.2 + registry).

---

*Report written 2026-04-24 by `/review-all-gdds` skill. Authored by two parallel general-purpose agents (consistency + design-theory) with the main session synthesizing.*
