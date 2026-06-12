# Armor Model Stress-Test — Results & Locked Decisions

**Date**: 2026-04-21
**Status**: Design resolved — ready for GDD retrofit
**Trigger**: Slice 5c playtest revealed Armor-as-separate-slot felt wrong; user proposed Armor-as-layer (peelable protection over Frame Hp).
**Input**: `game-designer` subagent stress-tested the proposed model across 9 edge cases and surfaced 7 hidden gotchas.
**Output**: 6 open questions resolved. Retrofit scope locked.

---

## 1. Model Summary

### Old model (rejected)

Armor was a separate slot alongside Weapon / Engine / Mobility / Frame. It had its own Hp, could be targeted and destroyed, and cards like Plate Up restored it like any other subsystem.

### New model (locked)

- **4 slots**: Weapon, Engine, Mobility, Frame. Armor is no longer a slot.
- **MaxArmor**: a vehicle-level stat computed by summing `ArmorContribution` across all installed parts (Weapon, Engine, Mobility, Frame). *All parts contribute by default* (opt-out).
- **CurrentArmor**: a vehicle-level running total, persists across turns.
- **Armor only protects Frame.** A Frame-targeted hit eats Armor first, then Frame Hp.
- **Non-Frame slots (Weapon/Engine/Mobility)** keep their own `Plating` layer, same as before.
- **Enemies have Armor too** (symmetric MaxArmor / CurrentArmor).
- **Plate Up (Armor-restore)** caps at MaxArmor. Overflow is wasted.
- **When a part goes Offline** mid-combat, MaxArmor drops by that part's ArmorContribution, and CurrentArmor clamps down immediately if above the new cap.
- **Burning (and other DOT)** bypasses Armor entirely; hits Frame/slot directly.
- **Armor Piercer** repurposed as a subsystem-strike card (hits non-Frame slot directly).

---

## 2. Edge Cases — Resolution Table

| # | Edge Case | Status | Decision |
|---|---|---|---|
| EC-1 | Corrode + Armor interaction order | Clear pre-review | Corrode bonus applies → Armor absorbs → Frame Hp (same F-CC1 order, Armor replaces Plating when target is Frame) |
| EC-2 | Chopshop timing for Armor recalc | Clear pre-review | On part re-install, MaxArmor recalculates; CurrentArmor unchanged unless new MaxArmor < CurrentArmor (then clamp) |
| EC-3 | Burning through Armor | **Resolved** | Burning bypasses Armor. Fire is an internal threat. Ticks land directly on Frame (or slot, if Burning lives on a non-Frame slot) |
| EC-4 | MaxArmor distribution (opt-in vs opt-out) | **Resolved** | All parts contribute by default. Every part definition includes `ArmorContribution`. Opt-out model. |
| EC-5 | Plate Up at max / overflow | **Resolved** | Hard cap at MaxArmor. Excess restore is wasted. Tempo trade exists inside the bubble. |
| EC-6 | MaxArmor = 0 glass cannon | Clear pre-review | Legal. All parts could have ArmorContribution=0 in theory. Frame is the only Hp; player takes every hit raw. Build archetype. |
| EC-7 | Offline clamp | **Resolved** | Immediate clamp. Part dies → MaxArmor drops → CurrentArmor clamps down to new MaxArmor in the same moment. |
| EC-8 | Enemy Armor symmetry | **Resolved** | Yes — enemies have Armor. Player must peel before Frame damage lands. Richer combat, more AI burden. |
| EC-9 | Armor Piercer identity | **Resolved** | Repurposed as subsystem-strike (targets non-Frame slot directly). Name may change during Card System retrofit. |

---

## 3. Meta / Balance Risks

### R-1 — Frame-tanking stall pattern

If Armor pool is large and Armor-restore cards are cheap, player can ignore subsystem counterplay — tank hits to Frame, keep topping up Armor. Pillar 4 ("hole in the silhouette IS the emotional event") weakens if the silhouette never shows holes.

**Mitigation**:
- Hard cap on MaxArmor (locked) prevents runaway stacking.
- Burning bypassing Armor (locked) gives enemies a counter to pure Frame-tank builds.
- Armor-restore card energy cost + limited deck slots must stay meaningful — tune during balance pass.
- Subsystem-strike cards (new Armor Piercer role) ensure players still want to attack specific slots.

### R-2 — Per-slot decision space collapse on offense

If every enemy attack that lands on a non-Frame slot still has to contend with Plating, while Frame attacks contend with Armor, damage pipeline forks cleanly. But if all enemy attacks aim at Frame (lazy AI), Armor becomes the only defensive layer anyone cares about.

**Mitigation**: Enemy brains must have slot variety (the existing RotatingBrain is a debug placeholder; real enemy brains telegraph diverse slot targets). Enemy AI becomes load-bearing.

### R-3 — Silhouette impact readability

Armor peeling must feel visually distinct from subsystem damage. Player needs to read "Armor gone, next hit breaks Frame" at a glance.

**Mitigation**: Visual direction task for art-director. Not a design blocker, but a UX risk for the combat HUD.

---

## 4. Things Missed (hidden gotchas)

These surfaced during stress-test — not part of the 6 open questions but need GDD or code attention.

### T-1 — Chopshop re-install exploit

Between combats, player could remove a part and re-install it (or swap in a different part). This recalculates MaxArmor. If CurrentArmor persists across combats (open: does it?), a player could theoretically exploit the recalc to inflate Armor.

**Action**: Card Combat GDD must specify: **CurrentArmor resets to MaxArmor at the start of each combat**. No persistence of combat-state Armor across encounters.

### T-2 — Armor repair economy

Where does Armor restoration live — in combat (Plate Up card) or out of combat (Chopshop)?

**Action**: Decide during Card System GDD update. Recommendation: both — Plate Up is in-combat tempo trade, Chopshop repairs happen between combats.

### T-3 — Armor = 0 install UI

If a part contributes 0 Armor, does the UI show "Armor: 0" or hide the stat? Readability question.

**Action**: UI spec (unity-ui-specialist) when combat HUD is built. Not a design blocker.

### T-4 — Corroded-on-Frame ordering nuance

F-CC1 currently says Corrode bonus applies, then Plating absorbs. Under new model, when the target is Frame, Corrode bonus → Armor absorbs → Frame Hp. Must be explicit in the formula doc that Armor substitutes for Plating only when target is Frame.

**Action**: Card Combat F-CC1 fork — write two damage orderings explicitly (Frame path vs non-Frame path).

### T-5 — MaxArmor = 0 at start vs mid-combat

A build that starts with MaxArmor = 0 is a valid glass-cannon. But losing all Armor-contributing parts mid-combat to reach MaxArmor = 0 is a death spiral signal. They're mechanically the same state but narratively different.

**Action**: HUD should show the distinction visually — starting silhouette vs "armor stripped" silhouette. Art-director concern.

### T-6 — RestorePlatingEffectSO cannot be reused

The existing effect SO targets Plating (a per-slot stat). Armor is a vehicle-level stat. They cannot share an implementation.

**Action**: Card System GDD adds `RestoreArmorEffectSO` as a distinct effect type. Plate Up card references the new effect, not the old one.

### T-7 — Enemy AI becomes load-bearing

With symmetric Armor, enemy decisions about which slot to attack carry more meaning. If the enemy always hits Frame, player only needs Armor. If the enemy attacks a non-Frame slot, Armor doesn't protect — that slot's Plating does.

**Action**: Enemy design must encode a mix of slot-targets per archetype. Covered during Enemy Design GDD (future work; noted here so it isn't forgotten).

---

## 5. Retrofit Action Plan

In order:

1. **V&P GDD** — rewrite Armor section:
   - Remove Armor from slot enumeration (now 4 slots)
   - Add `ArmorContribution` stat to part definitions
   - Define vehicle-level MaxArmor / CurrentArmor
   - Document Frame-only Armor protection rule
   - Document Offline clamp rule
   - Document combat-start reset rule (T-1)

2. **Card Combat GDD** — F-CC1 formula fork:
   - Write Frame-target damage path (Corrode bonus → Armor absorbs → Frame Hp)
   - Write non-Frame damage path (Corrode bonus → Plating absorbs → slot Hp) — unchanged from current
   - Update R-series rules that reference Armor slot
   - Update acceptance criteria

3. **Status Effects GDD** — minor amendment:
   - Burning (and all DOT) bypasses Armor
   - Clarify whether DOT hits Frame or the slot it lives on (per EC-3 option chosen: "hits Frame/slot directly" means follows the slot the status is attached to)

4. **Card System GDD** — card updates:
   - New `RestoreArmorEffectSO` effect type (T-6)
   - Plate Up card rewrites against new effect
   - Armor Piercer → subsystem-strike card (new name TBD)
   - Update card family index

5. **Code refactor** — Unity project:
   - `SlotType` enum: remove `Armor`
   - `Vehicle`: add `MaxArmor`, `CurrentArmor`, `RecalculateMaxArmor()`
   - `Slot`: add `ArmorContribution` field
   - `DamagePipeline.Apply`: fork on target slot type (Frame path uses Armor, others use Plating)
   - Update all 33 EditMode tests; add tests for Armor-specific behavior (clamp, reset, DOT bypass)
   - `CombatController` IMGUI: surface CurrentArmor / MaxArmor in vehicle draw

6. **Resume Slice 5d** — hand / deck / discard zones (the original answer to "why can I play 0 energy card unlimited times").

---

## 6. Locked Decisions (quick reference)

| # | Question | Answer |
|---|---|---|
| 1 | MaxArmor source | All parts contribute (opt-out) |
| 2 | Enemy Armor | Symmetric — enemies have Armor too |
| 3 | Plate Up overflow | Hard cap at MaxArmor, overflow wasted |
| 4 | Part goes Offline | CurrentArmor clamps down immediately |
| 5 | Burning vs Armor | Burning bypasses Armor, hits Frame/slot directly |
| 6 | Armor Piercer | Repurposed as subsystem-strike card |

---

## 7. Open items deferred to later phases

- Exact ArmorContribution values per part archetype — balance pass, post-GDD retrofit
- Armor repair economy (Chopshop vs in-combat only) — Card System GDD
- CurrentArmor visualization (silhouette peeling) — art-director + unity-ui-specialist
- Enemy brain slot-target distributions per archetype — Enemy Design GDD (future)
