# Cross-GDD Review Report — Re-run after Stage 1a Remediation

**Date**: 2026-04-24 (re-run; original FAILing review: `gdd-cross-review-2026-04-24.md`)
**Scope**: All 10 MVP GDDs + game-concept + systems-index + entity registry (44+ canonical constants)
**Trigger**: User-authorized re-review after completing Stage 1a textual remediation of the 2026-04-24 FAIL verdict (4 BLOCKERs + 4 CONCERNs).
**Method**: Parallel Phase 2 (Consistency) and Phase 3 (Design Theory) subagents; full GDD reads; registry-baseline cross-check; direct file verification of Phase 2's new findings.

---

## TL;DR — Verdict: **FAIL**

| Category | Prior (04-24) | Re-run (04-24 post-Stage-1a) |
|---|---|---|
| Prior BLOCKERs | 4 (all open) | 4 (all CLOSED ✅) |
| Prior CONCERNs | 4 (C1–C4 open) | 4 (all CLOSED ✅) |
| New BLOCKERs | — | **1** (N-1 V&P R13 enum collision) |
| New WARNINGs | — | 2 (N-2 V&P anti-features contradiction; N-3 Enemy System SilhouetteClass table) |
| Design-holism BLOCKERs | — | 0 |
| Design-holism WARNINGs | — | 5 |

The single new BLOCKER (N-1) is the direct cause of the FAIL verdict — V&P's `R13` section was retrofitted on 2026-04-23, one day before the BLOCKER-3 arbitration, and defines its own `SilhouetteClass` and `ArchetypeFamily` enums with member sets that collide with the canonical values owned by Enemy System C.2 and L&R C.3.1. The fix is textual (~15-line rewrite of V&P R13) and the resolution direction is already decided: **delegate to owning GDDs** — delete V&P's enum declarations, cross-reference Enemy System and L&R.

---

## Phase 2: Cross-GDD Consistency

### BLOCKER Verification (2026-04-24 remediation)

| ID | Issue | Status | Evidence |
|----|-------|--------|----------|
| **BLOCKER-1** | Enrage additive model (replace multiplicative) | **CLOSED** ✅ | Card Combat F-CC2 (`card-combat-system.md:498–505`) canonical owner; `EnrageBonus = EffectiveEnrageBaseBonus + max(0, turn − EnrageTurn)`. Enemy System D.2 (`enemy-system.md:344`) applies additively: `ResolvedDamage = PredictedDamage + EnrageBonus`. Status Effects R6 decouples. Registry `entities.yaml:437` records the replacement. No live multiplicative references remain outside backward-compat arbitration notes. |
| **BLOCKER-2** | Fuel multiplier values registry-locked (0.8/1.0/1.3) | **CLOSED** ✅ | V&P R10 (`vehicle-and-part-system.md:229`) reconciled to registry. Downstream consumers (Node Map F.1 / Scrap Economy E.8 / Node Encounter) all align. |
| **BLOCKER-3** | Three-axis enum split (`ArchetypeFamily`/`VisualFamily`/`SilhouetteClass`) | **RE-OPENED via N-1** ❌ | Enemy System C.2 + L&R C.3.1 correctly declare the canonical three-axis split, BUT V&P R13 defines a conflicting parallel pair. See N-1 below. |
| **BLOCKER-4** | `InstalledPart.CombatsSurvived` counter present; PF stale-clause removed | **PARTIALLY RE-OPENED via N-2** ⚠️ | R11 (`vehicle-and-part-system.md:263`) exposes the field; Player Fantasy (line 22) was updated. BUT Anti-Features bullet at line 425 still says `"No 'combats survived' counter per part"` — self-contradiction. |

### CONCERN Verification

| ID | Issue | Status |
|----|-------|--------|
| **C1** (Save bidirectional deps) | CLOSED ✅ — NE subscribe-not-own + HandlerActive save-block documented both sides. |
| **C2** (`IPartCatalog` empty-return) | CLOSED ✅ — V&P R12 explicit "never null" contract (`vehicle-and-part-system.md:306`). |
| **C3** (`TryGrantFuel` overflow) | CLOSED ✅ — V&P R10 explicit `actualGranted = min(amount, FuelCap − CurrentFuel)`; no auto-Scrap conversion. |
| **C4** (PurgeCost sweep) | CLOSED ✅ — `PurgeCost` removed from `CardDefinitionSO`; `GlobalPurgeCost = 30` canonical; no stale non-historical references. |

### New Consistency Issues

#### 🔴 N-1 — V&P R13 defines conflicting enum values that violate BLOCKER-3

- **File**: `design/gdd/vehicle-and-part-system.md:310–338`
- **Content**:
  ```csharp
  public enum SilhouetteClass { Lean, Balanced, Heavy, Asymmetric }  // shape axis (4)
  public enum ArchetypeFamily {
      Scout, Assault, Hauler,      // player-aligned
      Raider, Skirmisher, Hunter,  // enemy-aligned
      Boss
  }  // 7 mixed members
  ```
- **Canonical values** (post-BLOCKER-3 arbitration, 2026-04-24):
  - `SilhouetteClass = {Small, Medium, Large, Boss}` (4 size values) — owned by Enemy System C.2
  - `VisualFamily = {Raider, Scavenger, Elite, Boss}` (4 art axis values) — owned by Enemy System C.2 (V&P does not declare this)
  - `ArchetypeFamily = {Raider, Patcher, PitPacker, Elite, Boss}` (5 gameplay values) — owned by L&R C.3.1
- **Impact**: V&P R13 claims "V&P owns these fields ... and mirrors them to `EnemyDefinitionSO`." If V&P's enum definitions bind, Enemy System cannot independently own `SilhouetteClass` or `VisualFamily` with its own member set, and L&R's reward-table `(biome, beacon, family)` SO keys silently break (V&P `ArchetypeFamily` has 7 members; L&R keys assume 5).
- **Required fix (user-approved direction: DELEGATE)**: Rewrite V&P R13 to delete the enum declarations entirely. Replace with a cross-reference paragraph pointing to the canonical owners (Enemy System C.2 for `SilhouetteClass` + `VisualFamily`; L&R C.3.1 for `ArchetypeFamily`). V&P may expose the fields on `ChassisDefinitionSO` only for player-vehicle silhouette-grid compliance — but it MUST NOT redefine the enum types.

#### ⚠️ N-2 — V&P Anti-Features list contradicts R11

- **File**: `design/gdd/vehicle-and-part-system.md:425`
- **Content**: Anti-Features bullet `"No 'combats survived' counter per part"`
- **Conflict**: Direct self-contradiction with R11 line 263 (`public int CombatsSurvived { get; }`) and with the BLOCKER-4-revised Player Fantasy paragraph (line 22) that explicitly documents the internal counter for Scrap Economy tenure refund.
- **Required fix**: Amend the bullet to `"No player-surfaced 'combats survived' display per part"` to reconcile with R11's internal bookkeeping, OR delete the bullet outright.

#### ⚠️ N-3 — Enemy System `SilhouetteClass` table missing `Boss`

- **File**: `design/gdd/enemy-system.md:1124`
- **Content**: G.1 knob table lists `SilhouetteClass` values as `{Small, Medium, Large}` (3 members).
- **Conflict**: Inconsistent with line 1185 (describes "four `SilhouetteClass` values"), AC-ES2 (asserts `{Small, Medium, Large, Boss}` — 4 members), and L&R C.3.1 (uses the 4-value canonical set). Registry entry confirms 4-value canonical.
- **Required fix**: Update line 1124 knob table value set to `{Small, Medium, Large, Boss}`.

### Dependency Bidirectionality

All 15 GDD dependency edges are bidirectional ✅ except one broken edge surfaced by N-1:

- **V&P ↔ Enemy System** (art-contract enum) — V&P R13's competing enum definitions break the contract with Enemy System C.2. Closes when N-1 is fixed.

### Formula Compatibility

All formula compatibility checks pass ✅:
- Additive EnrageBonus propagates through Card Combat F-CC2 → Enemy System D.2 → HUD contract correctly.
- Scrap Economy D.6 tenure refund consumes V&P R11 `CombatsSurvived` correctly.
- L&R reward-table key `(BiomeIndex, BeaconType, ArchetypeFamily)` computes to 3×5×5 = 75 pre-prune (matches L&R stated "≈60 pruned") **if** `ArchetypeFamily` has 5 members as L&R declares. V&P's 7-member redefinition would silently inflate to 3×5×7 = 105 — flagged under N-1 impact.
- `EdgeFuelCost = BaseCost × ChassisMultiplier` uses registry-canonical 0.8/1.0/1.3 across V&P, Node Map, Scrap Economy.

---

## Phase 3: Game Design Holism

### Pillar Verification (post-BLOCKER)

| Pillar | Status | Notes |
|--------|--------|-------|
| 1. Vehicle as Character | **PASS** ✅ | Persistent vehicle state, tenure decay, storm cadence tied to chassis. |
| 2. Chassis Identity | **CONCERN** ⚠️ | Chassis differentiation scattered across 6 GDDs; Chassis Identity System GDD deferred to VS. See WARNING-1. |
| 3. Read to Win | **PASS** ✅ | Additive Enrage model (F-CC2) is countable +1/turn; HUD contract preserves PredictedDamage/ResolvedDamage divergence only for Enrage; 2-turn telegraph. Pillar 3 structurally enforced. |
| 4. Scarcity with Agency | **PASS** ✅ | Three-domain separation (Scrap/Fuel/Energy) enforced via build-time linter; six agency verbs; pity counter prevents RNG starvation. |
| 5. Route Reflects Vehicle State | **PASS** ✅ | 4 Route Constraints (RC-W1/E1/M1/F1) cover the subsystem-state → route-shape mapping. |

### Progression Loop Analysis

Three parallel loops: combat (per-turn), run (per-node), meta (per-run). The **run loop** dominates — 15-25 node decisions per run carry highest stakes. Combat is tactical-frequent; meta is deferred (Truck + Haven post-MVP). Nested cleanly; build-time linter prevents combat-state bleeding into run layer. No competing dominance.

### Player Attention Budget

9 simultaneously-active systems during combat turn (hand/energy, intent, 4+4 subsystems, statuses, position, armor/plating, enrage), only 3 require active per-turn decisions. Borderline on UX density — Frame-Degraded + Ambush + Burning stacks + Enrage telegraph can crowd HUD. Design-level OK; UX-level flag for `unity-ui-specialist` at VS. See WARNING-4.

### Dominant Strategy Watch

- Chassis dominance: none detected; potential Truck overperformance flag for balance sim.
- Rarity pricing: internally consistent; mastery-driven rarity shift prevents dominance.
- Convert verb (4:1 / Event 3:1): not exploitable given Event scarcity.
- Combat-avoidance: borderline theoretical exploit; blocked in practice by grid topology + Fuel drain. See WARNING-3.
- Slot-force bias (pity counter): works as intended.

### Economy Map

- **Scrap**: closed loop *except* Haven terminal sink (forward dependency). See WARNING-2.
- **Fuel**: closed loop with intentional silent overflow drop.
- **Hull HP**: no hoarding pressure.
- **Cards**: drops scale with mastery; pity prevents drought.
- **Parts**: tenure decay creates attachment economy.

### Difficulty Curve Compatibility

Biome 3 stacking: HP 3.325× base, damage 2.24× base, reward ~2.8× base. Gap is **intentional pressure** matching the ~10% success-rate design target. Internally consistent.

### Design-Holism Issues

🔴 **BLOCKING**: None.

⚠️ **WARNING-1 — Chassis Identity Validation Gap (Pillar 2)**
Chassis differentiation is scattered across 6 GDDs; `systems-index.md` B3 defers the Chassis Identity System GDD to VS. Risk: Scout vs. Assault may feel numerically different but thematically similar in first Prototype playtest. Truck is unvalidated in MVP. **Recommendation**: author a short chassis-identity quick-spec before playtest, OR accept as explicit Prototype limitation.

⚠️ **WARNING-2 — Terminal Scrap Sink Absent (Pillar 4)**
Haven terminal sink marked FORWARD DEPENDENCY in `scrap-economy.md`. Late-run Scrap hoarding (200+) possible in final 3 nodes with no sink. Undermines Pillar 4 climax. **Recommendation**: Haven Encounter quick-spec before VS, OR interim end-of-run Scrap sink documented in Scrap Economy.

⚠️ **WARNING-3 — Combat-Avoidance Edge**
Storm advances only on Combat/Elite reward-screen close. Theoretical stall strategy via Treasure/Event/Rest chains; mitigated by Fuel drain + `PartDropCooldownNodes = 3` + reward gating, not structurally blocked. **Recommendation**: monitor playtest; add minimum-combat-count gate per biome transition if exploitable.

⚠️ **WARNING-4 — Attention Budget Borderline (9 Systems)**
Combat turn tracks 9 simultaneously-active systems; 3 require active decision. High visual density possible in compound states (Frame-Degraded + Ambush + Burning + Enrage telegraph). **Recommendation**: UX concern for `unity-ui-specialist` at VS.

⚠️ **WARNING-5 — Enrage Reachability at Biome 3**
Additive Enrage (F-CC2) starts at `EnrageTurn = 8` with 2-turn telegraph lead. If biome-3 elite combats end by turn 8-10 (assumed, not documented), Enrage may never fully escalate — making the telegraph a warning for damage that doesn't arrive. **Recommendation**: during balance pass, validate average combat length hits the Enrage zone OR lower `EnrageTurn` for biome 3 elites.

---

## Phase 4: Cross-System Scenario Spot-Check

Five key multi-system scenarios walked; no new issues beyond those already surfaced:

1. **Boss kill at level cap** — L&R `BossFlatScrap = 30` substitute fires correctly; no double-reward risk.
2. **Elite combat → tenure-decayed refund → Chopshop install** — R11 `CombatsSurvived` increment → Scrap Economy D.6 `TenureMultiplier` → `InstallBaseCost[Rare] = 50`. Clean pipeline. (Dependent on N-2 fix — anti-features contradiction must clear.)
3. **Event Convert during Frame Degraded** — `HostileTiltDelta.Convert = 0` correctly isolates Convert from Frame state; rate remains favorable 3:1. No race.
4. **Pity fire on Rare-empty pool** — Card System pity counter → L&R substitutes `PityScrapAward = 40`. No double-dip.
5. **Mid-encounter quit → reload** — NE subscribe-not-own handler model; `HandlerActive` save-block prevents autosave; handler re-fires clean on reload. C1 contract verified.

---

## GDDs Flagged for Revision

| GDD | Reason | Type | Priority |
|-----|--------|------|----------|
| `vehicle-and-part-system.md` | N-1 R13 enum collision; N-2 anti-features self-contradiction | Consistency | **Blocking** |
| `enemy-system.md` | N-3 G.1 knob table `SilhouetteClass` missing `Boss` | Consistency | Warning |

---

## Verdict: **FAIL**

**Single pivot issue**: N-1 (V&P R13 enum collision) re-opens BLOCKER-3. All other retrofits held cleanly.

**Required actions before re-run can PASS**:
1. **V&P R13 rewrite** (`vehicle-and-part-system.md:310–338`): delete enum declarations; cross-reference Enemy System C.2 (`SilhouetteClass` + `VisualFamily`) and L&R C.3.1 (`ArchetypeFamily`) as canonical owners. User-approved direction: **DELEGATE**.
2. **V&P anti-features** (`vehicle-and-part-system.md:425`): amend or delete `"No 'combats survived' counter per part"` bullet.
3. **Enemy System G.1 table** (`enemy-system.md:1124`): update `SilhouetteClass` value set to `{Small, Medium, Large, Boss}`.

Stage 1b textual fixes are ~30 minutes of work; no design arbitration required — all three fixes apply already-canonical content to stale prose. Once complete, re-run `/review-all-gdds` should close cleanly to PASS or CONCERNS (design-holism warnings carry forward as VS playtest watchpoints).

### Design-holism warnings carried forward (advisory, non-blocking)

W-1 Chassis Identity Validation Gap · W-2 Terminal Scrap Sink Absent · W-3 Combat-Avoidance Edge · W-4 9-System Attention Budget · W-5 Enrage Reachability at Biome 3
