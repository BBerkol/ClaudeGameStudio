# Cross-GDD Review — Rerun 2 (Closure)

> **Date**: 2026-04-24
> **Scope**: Full 10-GDD MVP set + `design/gdd/game-concept.md` + `design/gdd/game-pillars.md`
> **Mode**: `full` (Phase 2 Consistency + Phase 3 Design Theory, parallel execution)
> **Predecessors**: `gdd-cross-review-2026-04-24.md` (FAIL), `gdd-cross-review-2026-04-24-rerun.md` (FAIL — Stage 0 pre-fix)
> **Closure target**: Stage 1b textual-fix landing (N-1 V&P R13 DELEGATE rewrite, N-2 V&P anti-features amendment, N-3 Enemy System G.1 safe-range expansion)

---

## TL;DR

**Verdict: CONCERNS (PASSABLE) — closes FAIL chain.**

- **Phase 2 Consistency: PASS.** All 4 prior BLOCKERs (BLOCKER-1 Enrage, BLOCKER-2 FuelBurnMultiplier, BLOCKER-3 Silhouette/VisualFamily/ArchetypeFamily triad, BLOCKER-4 CombatsSurvived ownership) and all 6 prior CONCERNs verified closed with file:line citations. Zero registry drift across 44+ canonical constants.
- **Phase 3 Design Theory: CONCERNS (BOUNDED).** 10/10 pillar alignment PASS. W-1 through W-5 remain advisory; W-3 and W-5 materially mitigated by Stage 1a arbitrations. No new warnings, no blockers, no anti-pillar breaks.
- **Phase 4 Scenario Spot-Checks: 5/5 clean.**

**Gate impact**: Systems Design → Technical Setup gate is closable. The cross-GDD consistency check artifact now exists with a non-FAIL verdict.

---

## Scope of Review

| GDD | Path | Last Material Change |
|---|---|---|
| Game Concept | `design/gdd/game-concept.md` | prior |
| Game Pillars | `design/gdd/game-pillars.md` | prior |
| Card System | `design/gdd/card-system.md` | prior |
| Card Combat | `design/gdd/card-combat.md` | prior |
| Enemy System | `design/gdd/enemy-system.md` | 2026-04-24 (N-3 Stage 1b) |
| Fuel System | `design/gdd/fuel-system.md` | prior |
| Loot & Reward | `design/gdd/loot-and-reward.md` | prior |
| Node Encounter | `design/gdd/node-encounter-system.md` | prior |
| Node Map | `design/gdd/node-map.md` | prior |
| Save & Persistence | `design/gdd/save-and-persistence.md` | prior |
| Scrap Economy | `design/gdd/scrap-economy.md` | prior |
| Status Effects | `design/gdd/status-effects.md` | prior |
| Vehicle & Part System | `design/gdd/vehicle-and-part-system.md` | 2026-04-24 (N-1, N-2 Stage 1b) |
| Combat HUD UX | `design/ux/combat-hud.md` | prior |
| Post-Combat Flow | `design/ux/post-combat-flow.md` | prior |

---

## Phase 2 — Cross-GDD Consistency

**Verdict: PASS. The 10-GDD MVP design set is internally consistent with the canonical entity registry and ready for downstream gating (architecture, story-creation, implementation).**

### 2a — BLOCKER Closure Verification

| ID | Description | Closure Evidence | Status |
|---|---|---|---|
| **BLOCKER-1** | Enrage damage model contradiction (V&P flat +N vs. Enemy additive formula) | Enemy System F.4 & H.1.6 state canonical formula `EnrageBonus = DefaultEnrageBaseBonus(=2) + max(0, turn − EffectiveEnrageTurn)`. V&P references Enemy authority, no local formula. Registry `DefaultEnrageBaseBonus: 2` matches both. | **CLOSED** |
| **BLOCKER-2** | FuelBurnMultiplier values disagreed between V&P and Fuel | V&P R2 table + Fuel System C.2 both state Scout 0.8 / Assault 1.0 / HeavyTruck 1.3. Registry entry `FuelBurnMultiplier` matches. | **CLOSED** |
| **BLOCKER-3** | SilhouetteClass / VisualFamily / ArchetypeFamily ownership collision across V&P, Enemy, L&R | V&P R13 rewritten (`vehicle-and-part-system.md:310–338`) as DELEGATE table: Enemy System C.2 owns SilhouetteClass `{Small, Medium, Large, Boss}` + VisualFamily `{Raider, Scavenger, Elite, Boss}`; L&R C.3.1 owns ArchetypeFamily `{Raider, Patcher, PitPacker, Elite, Boss}`. V&P consumes via C# `using` references, never redefines. Enemy System G.1 safe-range updated to include `Boss` (`enemy-system.md:1124`). | **CLOSED** |
| **BLOCKER-4** | CombatsSurvived counter — player-facing vs. internal ambiguity | V&P anti-features bullet amended (`vehicle-and-part-system.md:425`) to specify `InstalledPart.CombatsSurvived` is an internal-only data field consumed by Scrap Economy D.6 (tenure-refund) and Save serialization. Never surfaced to player (Slay-the-Spire readability). Scrap Economy D.6 references match. Save & Persistence schema includes the field. | **CLOSED** |

### 2b — CONCERN Closure Verification

All 6 prior CONCERNs are closed:

1. **CONCERN-1** — Card System PurgeCost removal: verified no residual references; `ICardRewardGenerator` interface stable.
2. **CONCERN-2** — L&R salvage fallback + BeaconType axis: key `(BiomeIndex, BeaconType, ArchetypeFamily)` used consistently across L&R and Enemy.
3. **CONCERN-3** — V&P FuelCap overflow: V&P R2 clamp rule aligns with Fuel System C.3 intake behavior.
4. **CONCERN-4** — CombatResult schema: Card Combat & Post-Combat Flow agree on fields.
5. **CONCERN-5** — Post-Combat Flow handler-state observer subscription: UX spec matches Save & Persistence handler-active save-block model.
6. **CONCERN-6** — Node Map commit-time Frame sampling: V&P R7/R8 Frame-Degraded gating matches Node Map C.4 commit semantics.

### 2c — Registry Drift Scan

Compared all 44+ canonical constants in `design/registry/entities.yaml` against the corresponding GDD values:

- `DefaultEnrageBaseBonus: 2` ✓ (Enemy System F.4)
- `FuelBurnMultiplier` Scout 0.8 / Assault 1.0 / Truck 1.3 ✓ (V&P R2, Fuel System C.2)
- `SilhouetteClass` `{Small, Medium, Large, Boss}` ✓ (Enemy System C.2, V&P R13 cross-ref, Enemy G.1 safe-range)
- `VisualFamily` `{Raider, Scavenger, Elite, Boss}` ✓ (Enemy System C.2)
- `ArchetypeFamily` `{Raider, Patcher, PitPacker, Elite, Boss}` ✓ (L&R C.3.1)
- `ChassisType` `{Scout, Assault, HeavyTruck}` ✓ (V&P C.1)
- All remaining 38+ entries pass.

**Zero drift. Zero silent overrides. Zero formula disagreements.**

### 2d — Stage 1b Regression Check

| Fix | Target | Regression check | Status |
|---|---|---|---|
| N-1 | V&P R13 (310–338) | Grep `enum SilhouetteClass`, `enum VisualFamily`, `enum ArchetypeFamily` across all GDDs. Only owner docs declare; V&P contains cross-ref table only. | **Clean** |
| N-2 | V&P line 425 anti-features | `CombatsSurvived` internal-purpose note matches Scrap Economy D.6 and Save schema. No player-surfaced references. | **Clean** |
| N-3 | Enemy System G.1 (line 1124) | `{Small, Medium, Large, Boss}` appears in G.1 table, AC-ES2, H.1.8, and registry. Four-way consistent. | **Clean** |

### 2e — Informational Observations (Non-Blocking)

Three INFO items surfaced during Phase 2; none require action before gate closure:

- **INFO-1** — A legacy `HostileTiltDelta` scalar row in the registry is referenced only by Scrap Economy. Consider trimming in a future registry sweep. No consistency impact.
- **INFO-2** — Biome 3 Elite pacing: several enemies in this tier cluster near the upper bound of expected combat length. Forward-watch flag for Balance Pass (also echoed in Phase 3 W-2).
- **INFO-3** — Scrap Economy D.6 tenure-refund uses both `TenureRefundRate` and `CombatsSurvivedRefundCoefficient` as synonyms in prose; pick one at implementation time. Formula itself is unambiguous.

---

## Phase 3 — Game Design Holism

**Verdict: CONCERNS (BOUNDED) — same verdict class as the Stage-0 pre-fix rerun, but with W-3 and W-5 materially mitigated by the Stage 1a arbitrations. No new warnings, no blockers, no anti-pillar breaks.**

### 3a — Pillar Alignment Matrix

| Pillar | Card Sys | Card Combat | Enemy | Fuel | L&R | Node Enc | Node Map | Save | Scrap | Status FX |
|---|---|---|---|---|---|---|---|---|---|---|
| P1 Vehicle as Character | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| P2 Chassis Identity | ✓ | ✓ | — | ✓ | ✓ | — | ✓ | — | ✓ | — |
| P3 Read to Win | ✓ | ✓ | ✓ | ✓ | — | ✓ | ✓ | — | — | ✓ |
| P4 Scarcity with Agency | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ | ✓ |
| P5 Route Reflects Vehicle State | — | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |

**10/10 GDDs PASS pillar alignment.** `—` denotes "not relevant to this pillar," not a miss.

### 3b — Warning Re-Verification (W-1 through W-5)

| ID | Summary | Post-Stage-1b status | Carry-forward |
|---|---|---|---|
| **W-1** | Truck chassis reward loop: `BossFlatScrap × TruckRewardMultiplier(1.5)` may over-reward late run | Bounded. Advisory only — playable values; surface at Balance Pass with live telemetry. | Balance Pass |
| **W-2** | Biome 3 Elite combat length exceeds target pacing window for some archetypes | Bounded. Advisory only — not a rules conflict; a curve-tuning concern. | Balance Pass |
| **W-3** | Silhouette-to-identity mapping risk (P2 Chassis Identity drift) | **Materially mitigated** by Stage 1a three-axis arbitration (SilhouetteClass for art/size, VisualFamily for enemy family, ArchetypeFamily for gameplay/loot). Player chassis identity now routed via `ChassisType`, decoupled from enemy axes. | Deprioritized |
| **W-4** | Haven scrap sink under-defined; endgame Scrap may over-accumulate | Bounded. Scope-deferred to Haven Ending GDD; no MVP blocker. | Haven Ending GDD |
| **W-5** | Chassis Identity (P2) requires VS-level validation, not static-analysis proof | **Materially mitigated** by decoupling of player-ChassisType from enemy silhouette/family axes. Formal validation still VS-gate work. | VS gate |

### 3c — New Informational Items

Three Phase 3 INFO items not requiring action before gate closure:

- **I-1 Enrage-skip HUD messaging.** Enemies with `EnrageTurn = ∞` (skip Enrage) are represented consistently in rules but could benefit from a HUD affordance pass when combat HUD is implemented. Carry to combat-hud implementation, not design.
- **I-2 BossFlatScrap × TruckRewardMultiplier.** Multiplicative stack is legal per formulas, but economy designers should verify boss reward distributions per chassis in Balance Pass. Echoes W-1 from the Truck side.
- **I-3 EnrageTelegraphLeadTurns per-archetype.** Currently one global constant; consider per-archetype tuning in Balance Pass if Elite vs Raider Enrage feels samey.

### 3d — Anti-Pillar Violation Scan

No GDD violates an anti-pillar. The P2 Chassis Identity pillar — the highest-risk axis during the BLOCKER-3 arbitration — is now reinforced rather than threatened by the Stage 1a + 1b changes.

---

## Phase 4 — Cross-System Scenario Walkthrough

5/5 scenarios clean:

| # | Scenario | Systems touched | Result |
|---|---|---|---|
| 1 | Boss encounter with Enrage at Biome 3 | Enemy, Card Combat, L&R, Fuel | Damage formulas additive and bounded; reward computation respects chassis multiplier; Fuel payout gated. |
| 2 | Frame-Degraded → Event → Ambush chain | V&P, Node Encounter, Card Combat, Save | Frame-Degraded flag sampled at commit time; handler-active save-block engages; ambush escalation uses correct archetype. |
| 3 | Mobility offline → Chopshop node | V&P, Node Encounter, Scrap Economy, L&R | `MobilityOffline` flag routes to recovery node; salvage fallback executes; scrap credited via D.6. |
| 4 | Pity-fire on empty Rare pool | Card System, L&R | Pity precedence activates; slot-force bias deferred; no softlock, no negative rewards. |
| 5 | Save-block during handler-active | Save & Persistence, Node Encounter, Post-Combat Flow | Handler-active observer blocks save; resume state matches pre-handler snapshot; no data loss. |

---

## Required Actions

**None blocking.** Systems Design gate can close.

### Carry-Forwards

| Item | Destination | Owner | Priority |
|---|---|---|---|
| W-1 Truck Reward Loop (BossFlatScrap × 1.5) | Balance Pass | economy-designer | Medium |
| W-2 Biome 3 Elite Combat Length | Balance Pass | systems-designer | Medium |
| W-4 Haven Scrap Sink | Haven Ending GDD (post-MVP design work) | game-designer | Deferred |
| W-5 Chassis Identity (P2) runtime validation | VS gate playtest | qa-lead + creative-director | VS gate |
| W-3 Silhouette-to-identity drift | Deprioritized — revisit only if playtest signals emerge | — | Low |
| I-1 Enrage-skip HUD affordance | Combat HUD implementation | unity-ui-specialist | With HUD story |
| I-2 BossFlatScrap × TruckRewardMultiplier interaction | Balance Pass telemetry | economy-designer | Bundle with W-1 |
| I-3 Per-archetype `EnrageTelegraphLeadTurns` | Balance Pass tuning | systems-designer | Nice-to-have |

### Systems-Index Update

Mark all MVP-tier GDDs as **Design-Complete** in `design/gdd/systems-index.md`. This closes the prerequisite for the Systems Design → Technical Setup gate.

---

## Verdict Chain

| Review | Date | Verdict |
|---|---|---|
| `gdd-cross-review-2026-04-24.md` | 2026-04-24 | FAIL (4 BLOCKERs, 6 CONCERNs) |
| `gdd-cross-review-2026-04-24-rerun.md` | 2026-04-24 | FAIL (Stage 0 pre-fix snapshot) |
| **`gdd-cross-review-2026-04-24-rerun2.md`** (this) | 2026-04-24 | **CONCERNS (PASSABLE) — FAIL chain closed** |

---

## Next Action

Run `/gate-check pre-production` — wait, Systems Design → Technical Setup is the relevant gate. Resolve: `/gate-check` (auto-detect will propose Systems Design → Technical Setup transition). The cross-GDD review artifact required by that gate is now on disk with a non-FAIL verdict.
