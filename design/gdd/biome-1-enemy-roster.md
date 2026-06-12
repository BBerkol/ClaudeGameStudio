# Biome 1 — Sand Flats Enemy Roster

> **Document type**: Content roster (not system spec).
> **System spec lives in**: `enemy-system.md`.
> This document applies that system to specific enemies and locks the Biome 1 difficulty curve baseline.
>
> **Status**: First draft pending user review. Items requiring explicit user lock are marked `[NEEDS REVIEW]`.
>
> **ADR-0007 mapping (2026-05-18)**: This roster was originally authored against the legacy fixed-4-slot data model (ADR-0001 data contract). Under ADR-0007 (Frame-Driven Variable Slot System), enemies reference a `FrameLayoutSO` via `FrameLayoutId`. The roster has been mapped as follows:
>
> | Archetype | FrameLayoutId | Slot count | Notes |
> |---|---|---|---|
> | Dune Skimmer | `tiny_frame` | 4 | weapon_0, engine_0, mobility_0, hull_0 |
> | Iron Shepherd | `hauler_frame` | 5 | weapon_0, engine_0, mobility_0, hull_0, + 1 reinforced slot TBD in W2 |
> | The Dredge | `dredge_frame` | 10 | incl. armor_chest + armor_back (`ExposureMultiplier = 3.0`, `RedirectsToSlotId = "hull_0"`) |
>
> **Key renames applied in stat blocks**:
> - `BaseSlotHP[Frame]` → `MaxHpOverride[hull_0]` (Frame slot = Hull slot under SlotKind).
> - `MaxArmorContribution[*]` rows are **deprecated** under ADR-0007 (vehicle-level Armor pool removed). For the Dredge, the protection role moves to the two Armor slots on `dredge_frame`. For Skimmer and Shepherd (no Armor slots in their layouts), the old protection budget is folded into higher `MaxHpOverride` values per slot — to be re-tuned in the W2 balance pass.
> - `RetargetPolicy` slot references now use SlotIds (`hull_0`, `engine_0`, etc.) instead of `SlotType` enum values.
> - `Damage(Frame)` in intent tables means `ApplyDamage(slotId="hull_0", …)` under ADR-0007. Intent targeting still resolves through the `RetargetPolicy` — slot labels in tables are reading aids, not the runtime target.
>
> The worked playthrough traces (Section "Worked Combat Trace" under each archetype) preserve the original prose for pacing illustration; numeric specifics will be re-validated during W2 balance work.

---

## Overview

Three enemy archetypes establish the Biome 1 (Sand Flats) difficulty curve and teach the player the core combat language. Together they introduce:

1. **Intent reading** — every enemy telegraphs; learning to read is the price of survival.
2. **Slot prioritization** — the player chooses which threat to neutralize first.
3. **Part-strip-as-suppression** — destroying enemy parts removes intents from the boss's pool, making part destruction both offensive and defensive.

The three are:

| Slot | Name | Family | Silhouette | DifficultyScore | Encounter Role |
|---|---|---|---|---|---|
| Scout | **Dune Skimmer** | Raider | Small | 0.090 | Repeat early-node combat encounter |
| Elite | **Iron Shepherd** | Elite | Medium | 0.257 | Mid-biome dedicated Elite beacon (1 per run) |
| Boss | **The Dredge** | Boss | Large | 0.423 | Biome 1 final encounter (1 per run, mandatory) |

DifficultyScore ascends cleanly (0.09 → 0.26 → 0.42), leaving room above for Biomes 2/3.

---

## Player Fantasy

Biome 1 is the **first 20–35 minutes of every run**. It teaches the language. By the time the player reaches Haven, they should be able to:

- Read an enemy intent telegraph and pick a card response from instinct.
- Recognize that destroying enemy parts is sometimes more valuable than dealing direct damage.
- Triage two simultaneous threats without freezing.
- Understand that boss fights have phases and that earlier work carries into later phases.

The three archetypes pace this learning curve. Dune Skimmers introduce the loop, Iron Shepherds complicate it, The Dredge graduates the player into the language fluency they need for Biome 2.

---

## Difficulty Curve and Encounter Pacing

| Biome 1 Node Depth | Encounter | Notes |
|---|---|---|
| 1–2 | Dune Skimmer (×2–3 across early nodes) | Player meets their first vehicle combat |
| 3–5 | Iron Shepherd (1 mandatory Elite beacon) | Mid-biome wall, dual-threat triage |
| 6–7 | (Standard Combat / Event nodes — not enemy-roster scope) | |
| 8 (final) | The Dredge | Mandatory boss; Haven gate |

**Implied encounter count per run**: 4–6 combat encounters in Biome 1 (3–5 Skimmer + 1 Shepherd + 1 Dredge).

---

## Archetype 1 — Dune Skimmer

### Identity

A small, single-rider scout bike. Light frame, exposed engine, oversized rear wheel. Reads as a scorpion tail in one frame. Lives at node depths 1–2; the first vehicle the player ever fights.

**One-line role**: The pressure primer. Teaches the player to read the telegraph before it hurts them.

**Player fantasy**: Fighting a Dune Skimmer should feel like the game handing the player a loaded sentence and asking them to finish it. The tell is obvious — a Ram, telegraphed at Frame, eight incoming damage — and the player has exactly enough cards and energy to answer it. Winning the first encounter should produce the thought "I did that," not "I survived that."

**Teaching purpose**: The core read-to-win loop. Its intent pool is shallow, which means the player sees the same telegraph structure 2–3 turns running and learns to plan a turn ahead. Also teaches that blocking is a card play, not a failure.

### Stat Block

| Field | Value |
|---|---|
| ArchetypeFamily | Raider |
| SilhouetteClass | Small |
| FrameLayoutId | `tiny_frame` (4 slots: weapon_0, engine_0, mobility_0, hull_0) |
| RetargetPolicy | `FixedSlot("hull_0")` — Biome 1 teachable default per `enemy-system.md` G.4 |
| MaxHpOverride[weapon_0] | 6 |
| MaxHpOverride[engine_0] | 6 |
| MaxHpOverride[mobility_0] | 8 |
| MaxHpOverride[hull_0] | 12 *(was BaseSlotHP[Frame]=8 + MaxArmorContribution[Frame]=4 folded in; W2 to re-tune)* |
| MaxArmorContribution[*] | **DEPRECATED** under ADR-0007 (no Armor slots in `tiny_frame`; protection budget folded into hull_0 MaxHpOverride above) |
| BaseDamage (Ram reference) | 8 |
| EnrageTurn | null → uses default (turn 8) |
| EnrageBaseBonusOverride | null → uses default (+2 per turn beyond) |

### Intent Pool — Base (turns 1–7)

| Intent | Type | BaseWeight | Damage | PositionRequirement | WeightModifiers |
|---|---|---|---|---|---|
| Ram | Damage(Frame) | 50 | 8 | None | player.Frame.state==Damaged → ×2.0; allPlayerSlotsOffline → ×0.0 |
| Scatter Shot | Damage(Weapon) | 20 | 6 | None | player.Weapon.state==Offline → ×0.0; allPlayerSlotsOffline → ×0.0 |
| Tailgate | Utility(PositionShift) | 30 | — | None | enemy.IsAhead → ×0.0 (pointless if already ahead); allPlayerSlotsOffline → ×0.0 |

Scatter Shot targets the Weapon slot directly — damage routes to Weapon HP, bypasses Frame Armor (per established subsystem-damage rule).

### Intent Pool — Enrage (turn 8+)

No `EnrageIntentCandidates` — damage-only Enrage. Base candidates remain, with flat damage bonus per `enemy-system.md` D.4. Turn 8: +2 damage. Turn 9: +3. Player reads the climb directly off the HUD.

### Visual Identity

Layer 1: uses the shared buggy chassis. Scout identity is conveyed by stat block, intent pool, and HUD bar presentation — not by bespoke per-slot art. A dedicated Skimmer silhouette / per-slot art pass is post-EA scope.

### Sample Turn 1–5 Trace

Player context: Frame 20/20, Armor 3.

| Turn | TurnCount | Brain Roll | Result |
|---|---|---|---|
| 1 | 0 | Ram (P=0.50) | PredictedDamage 8; Armor 3 soaks 3; Frame 20→15. |
| 2 | 2 | Frame Damaged → Ram ×2.0 → Ram (P=0.667) | PredictedDamage 8; Armor partial 2; Frame 15→9. |
| 3 | 3 | Scatter Shot (CDF roll lands in Scatter band) | PredictedDamage 6 → Weapon 15→9 (bypasses Frame Armor). |
| 4 | 4 | Ram (P=0.667, Frame still Damaged) | PredictedDamage 8; Armor 0; Frame 9→2 (critical). |
| 5 | 5 | Ram | PredictedDamage 8; Frame 2→0; **player vehicle dead**. |

Typical Skimmer combat ends turn 4–6. Enrage at turn 8 is a catch-net for dragged-out fights, not a primary weapon.

---

## Archetype 2 — Iron Shepherd

### Identity

A medium armored buggy. Low-slung, wide silhouette with four symmetric corner struts that swell the chassis outward. Reads as a box with teeth. Single appearance per Biome 1 run, at the dedicated Elite beacon (node depths 3–5).

**One-line role**: The mid-biome wall that punishes single-target fixation and forces the player to make a real triage call.

**Player fantasy**: Fighting the Shepherd should feel like realizing the problem is not one thing — it is two things at once. A heavy Ram telegraphed at Engine, plus a Corrode-status attack on Engine that stacks. The player who fixates on stripping the Shepherd's Weapon discovers they just lost two turns of damage while Corrode stacks ticked up. The player who ignores Corrode discovers their Engine attack is now landing twice as hard.

**Teaching purpose**: Slot prioritization under dual pressure. The first time the game asks "which problem do you solve this turn?" Also introduces non-damage intent types that are still dangerous — preparing the player for Biome 2 status-heavy enemies.

### Stat Block

| Field | Value |
|---|---|
| ArchetypeFamily | Elite |
| SilhouetteClass | Medium |
| FrameLayoutId | `hauler_frame` (5 slots: weapon_0, engine_0, mobility_0, hull_0, + 1 reinforced slot TBD W2) |
| RetargetPolicy | `PriorityList("engine_0", "hull_0", "weapon_0", "mobility_0")` — Engine-first targeting teaches the player that Damaged Engine reduces card draw |
| BaseHullHP | 56 |
| BaseSlotHP[Weapon] | 10 |
| BaseSlotHP[Engine] | 14 |
| BaseSlotHP[Mobility] | 14 |
| MaxHpOverride[hull_0] | 28 *(was BaseSlotHP[Frame]=18 + MaxArmorContribution[Frame]=10 folded in; W2 to re-tune)* |
| MaxArmorContribution[*] | **DEPRECATED** under ADR-0007. Original budget (Engine 3, Frame 10) folded into MaxHpOverride[engine_0] and MaxHpOverride[hull_0]. Reinforced "Armor" identity may move to an Armor slot on `hauler_frame` in a post-EA balance pass. |
| BaseDamage (Ram reference) | 10 |
| EnrageTurn | 6 (override) — earlier Enrage raises pacing pressure |
| EnrageBaseBonusOverride | 3 — hits harder on Enrage, armor management matters |

### Intent Pool — Base (turns 1–5)

| Intent | Type | BaseWeight | Damage | ArmorGain | PositionRequirement | WeightModifiers |
|---|---|---|---|---|---|---|
| Ram | Damage(Engine) | 40 | 10 | — | None | player.Engine.state==Damaged → ×2.5; allPlayerSlotsOffline → ×0.0 |
| Armor Rend | Status(Engine, Corrode×1) | 30 | 0 | — | None | player.Engine.state==Offline → ×0.0; allPlayerSlotsOffline → ×0.0 |
| Flank | Damage(Mobility) | 20 | 12 | — | RequiresBehind | allPlayerSlotsOffline → ×0.0 |
| Reinforce | Defend(Self) | 20 | — | 5 | None | enemy.Armor==enemy.MaxArmor → ×0.0 |

> **Locked 2026-05-19.** Armor Rend applies **Corroded×1** to the player's Engine. Verified in `status-effects.md` R1: `Corroded` is Graduated, target-subsystem takes `Stacks` bonus damage per attack, duration-ticked, cap 3 stacks / 3 turns. Per-subsystem instances are permitted (R4), and re-application uses Extend merge — both required for Armor Rend's stacking behavior across turns 1/3/5 in the worked trace.

### Intent Pool — Enrage (turn 6+)

`EnrageIntentCandidates` authored — behavior-shift Enrage. Shepherd commits to damage at turn 6.

| Intent | Type | BaseWeight | Damage | ArmorGain | PositionRequirement | WeightModifiers |
|---|---|---|---|---|---|---|
| Ram | Damage(Engine) | 60 | 10 | — | None | player.Engine.state==Damaged → ×2.0; allPlayerSlotsOffline → ×0.0 |
| Double Ram | Damage(Frame) | 40 | 8 | — | None | allPlayerSlotsOffline → ×0.0 |

Reinforce and Armor Rend removed. EnrageBonus = 3 + max(0, TurnCount − 6). Turn 6: +3. Turn 7: +4. Turn 8: +5.

### Visual Identity

Layer 1: uses the shared buggy chassis — same silhouette as the Skimmer. Elite identity is conveyed by stat block (higher Frame/Engine/Wheels HP + armor_0 pool), intent pool (4 intents incl. Flame Barrage and Reposition), and HUD bar presentation. No bespoke per-slot art, no corner struts, no rotary cannon — those were pre-Layer-1 prescriptions and have been retired. A dedicated Shepherd silhouette / per-slot art pass is post-EA scope.

### Sample Turn 1–6 Trace

Player context: Engine 15/15, Frame 20/20, Armor 6.

| Turn | TurnCount | Brain Roll | Result |
|---|---|---|---|
| 1 | 0 | Flank position-filtered (enemy Ahead). Pool: Ram 40, ArmorRend 30, Reinforce 20. ArmorRend (P=0.333). | Engine gains Corrode×1. |
| 2 | 2 | Engine Damaged (Corroded). Ram ×2.5 = 100. Reinforce filtered (Armor at cap). Pool: Ram 100, ArmorRend 30. Ram (P=0.769). | PredictedDamage 10; Armor 6 soaks 6; Engine direct hit 4 (subsystem path); Engine 15→11. |
| 3 | 3 | Ram 100, ArmorRend 30. ArmorRend (high roll). | Engine Corrode×2. |
| 4 | 4 | Ram (high probability) | PredictedDamage 10; partial Armor 3; Engine 11→4 (critical). |
| 5 | 5 | ArmorRend (last base-pool turn) | Engine Corrode×3. Pressure cliff approaches. |
| 6 | 6 | **Enrage** → draw from EnrageIntentCandidates. Ram (P=0.60). EnrageBonus +3. | PredictedDamage 10 → ResolvedDamage 13. Player reads +3 spike from HUD. |

---

## Archetype 3 — The Dredge (Boss)

### Identity

A heavy freight truck. Raised forward cab, flatbed rear with a roof-mounted minigun, lateral sponson rams that push the silhouette wider than the wheelbase. Visible six-wheel configuration (two front steering, four rear drive in dual-tire). Reads as a slow wall with spikes on both ends. Final encounter of Biome 1, mandatory.

**One-line role**: Biome 1's capstone. A two-phase freight truck that teaches the player that part-stripping is not just offense — it is a tool for disabling enemy capabilities, and that fights have second gears.

**Player fantasy**: The first time the combat screen feels genuinely dangerous. Phase 1 is a siege: the Dredge grinds forward telegraphing simple heavy attacks while taunting, and the player chips at specific weapons and weakens parts. Phase 2 shifts the tone from siege to chase: the minigun spins up, the boss starts throwing chained javelins, and the player is suddenly racing the clock. Strips made in Phase 1 carry forward — the cannon you broke is still broken — giving good play an asymmetric reward in Phase 2.

**Teaching purpose**: The part-strip-to-disable loop, explicitly. Phase awareness. The lesson that taking damage is sometimes the correct trade — a player who tries to block every hit in Phase 1 will never strip the minigun before Phase 2 arrives.

**Core design principle**: The Dredge has **no repairs and no armor regeneration**. It can only get weaker. Every part the player breaks stays broken. The fight is a slow erosion of the boss's capabilities.

### Stat Block

| Field | Value |
|---|---|
| ArchetypeFamily | Boss |
| SilhouetteClass | Large (locked 2026-05-19 — uses existing `{Small, Medium, Large}` enum per `enemy-system.md` C.2; no enum extension required) |
| FrameLayoutId | `dredge_frame` (10 slots, includes armor_chest + armor_back with `ExposureMultiplier = 3.0` redirecting to hull_0 — see V&P R_FL.3) |
| RetargetPolicy | `PriorityList("hull_0", "weapon_0", "engine_0", "mobility_0")` — Hull-first cascade through Weapon→Engine if hull_0 Offline; ensures pressure at all times. Note: Armor slots (armor_chest, armor_back) are not in the targeting list — they intercept hull-bound damage via the R_ARM Armor slot mechanic until destroyed. |
| BaseHullHP | 90 |
| BaseSlotHP[Weapon] | 18 |
| BaseSlotHP[Engine] | 22 |
| BaseSlotHP[Mobility] | 22 |
| MaxHpOverride[hull_0] | 28 *(original BaseSlotHP[Frame]; legacy MaxArmorContribution budget now lives in the two Armor slots below — NOT folded into hull_0)* |
| MaxHpOverride[armor_chest] | TBD W2 balance pass *(Armor slot — `Kind == Armor`, `ExposureMultiplier = 3.0`, `RedirectsToSlotId = "hull_0"`. Initial guidance: tune so an unupgraded Scout needs ~2–3 focused hits to break — see V&P Tuning Knobs / Armor HP guidance.)* |
| MaxHpOverride[armor_back] | TBD W2 balance pass *(Armor slot — same mechanic as armor_chest; protects against rear-position attackers.)* |
| MaxArmorContribution[*] | **DEPRECATED** under ADR-0007. Legacy budget (Mobility 5, Frame 14) is replaced by the two Armor slots on `dredge_frame` — this is the Dredge's signature mechanic post-ADR-0007. |
| BaseDamage (Ram reference) | 14 |
| EnrageTurn | 8 (default) — Enrage is secondary pressure axis to phase transition |
| EnrageBaseBonusOverride | 4 |
| Phase2Trigger | `HpPercent ≤ 0.60` `[NEW BrainRulesetSO field — see W2 ADR]` |

### Phase 1 Intent Pool (HP > 60%) — "the testing phase"

The Dredge belittles the player while throwing simple heavy attacks. No defensive intents, no repairs. The pool is small and predictable on purpose — Phase 1 is the player's window to plan strips.

| Intent | Type | BaseWeight | Damage | PositionRequirement | WeightModifiers |
|---|---|---|---|---|---|
| Ram | Damage(Frame) | 55 | 10 | None | player.Frame.state==Damaged → ×1.5; enemy.Engine.state==Offline → ×0.0; allPlayerSlotsOffline → ×0.0 |
| Sweep | Damage(Weapon) | 25 | 7 | None | enemy.Weapon.state==Offline → ×0.0; allPlayerSlotsOffline → ×0.0 |
| Taunt | Setup(Status, Marked×1) | 20 | 0 | None | applies `Marked×1` to player Frame; next Frame-targeting Dredge intent deals +3 bonus damage |

> **Locked 2026-05-19 — Option A.** Taunt applies a `Marked` status to player Frame: 1-turn marker, next Frame-targeting Dredge intent deals +3 bonus damage. Mechanically meaningful (Pillar 3 read-payoff loop), distinct from Corrode (no stacking across turns), and respects telegraph cadence. **Implementation note:** `Marked` is not yet declared in `status-effects.md` — must be added before Dredge ships. Recommended shape: Binary, Refresh merge per R4; turn-start handler sets a `vehicle.FrameMarkedNextHit = true` flag that the damage pipeline checks at Frame-damage resolution and adds +3 to incoming damage (one-shot consumption — flag clears after the first Frame-damage application). This is a parallel addition to the Stunned addition this same session.

### Phase 2 Intent Pool (HP ≤ 60%) — "the chase"

The minigun spins up. The boss commits to aggression. New intents enter the pool; Phase 1 simple attacks remain available as fallback.

| Intent | Type | BaseWeight | Damage | PositionRequirement | WeightModifiers |
|---|---|---|---|---|---|
| Shred | Damage(Weapon + Frame, dual-target) | 35 | 12 to subsystem + 12 to Frame | RequiresAhead | enemy.Weapon.state==Offline → ×0.0 (no minigun = no Shred); allPlayerSlotsOffline → ×0.0 |
| Javelin Hook | Damage(Frame) + Status(Stunned×1) | 25 | 8 + applies Stunned×1 (Layer 1 simple) | RequiresAhead | enemy.Engine.state==Offline → ×0.0 (javelin powered by engine hydraulics); allPlayerSlotsOffline → ×0.0 |
| Spike Flail | Damage(Frame) | 25 | 10 | RequiresBehind | enemy.Mobility.state==Offline → ×0.0 (flail mounted on rear mobility assembly); allPlayerSlotsOffline → ×0.0 |
| Bulldoze | Damage(Frame) | 15 | 14 | None | enemy.Frame.state==Offline → ×0.0; allPlayerSlotsOffline → ×0.0 |

> Bulldoze is the position-agnostic fallback intent that keeps the pool active when the player is mid-reposition (per `card-combat-system.md` R18 reposition-fallback path).

**Layer 1 simplification**: The Javelin Hook → Stunned → Bulldoze combo is the closest Layer 1 expression of the user's "javelin + chains + big attack" vision. The full vision (Tethered status, Cut Chain card injection, Cut Chain + Reposition handbrake-dodge) is **Layer 2 deferred** — see `## Layer 2 — Dredge Signature Mechanics (Deferred)` below.

### Phase Transition (HP ≤ 60%)

1. Boss enters Phase 2 state. Brain swaps `BaseCandidates` for the Phase 2 pool above.
2. Visual transformation fires (see Visual Transformation section below).
3. Player accumulated strips from Phase 1 carry forward in full — any slot already Offline stays Offline, and its gated Phase 2 intents stay disabled.
4. **Enrage and Phase Transition are independent axes.** Enrage still fires at turn 8 per default; if combat extends, EnrageBonus stacks on top of Phase 2 damage. A long fight can simultaneously be in Phase 2 AND Enraged.

### Strip-Disables-Intent Map

| Player Strips | Slot State | Disabled Intents | Phase |
|---|---|---|---|
| Boss Weapon (minigun) → Offline | Weapon Offline | Shred | Phase 2 |
| Boss Engine → Offline | Engine Offline | Javelin Hook (per recommended slot binding above) | Phase 2 |
| Boss Mobility → Offline | Mobility Offline | Spike Flail | Phase 2 |
| Boss Frame → Offline | Frame Offline | Boss is dead (Frame Offline = vehicle dead per V&P R3) | — |

Bulldoze, Ram, Sweep, and Taunt remain active regardless of strips — the Dredge can always throw something at the player. Strips suppress *signature* attacks, not all attacks.

### Visual Identity — Phase 1

- **Color anchor**: Layered hybrid. Iron oxide `#8B3A2A` dominant base (50%, Raider grammar); tarnished steel `#7A7872` on structural frames and cab (30%, Elite grammar); bleached sand `#D4B896` on sand-caked undercarriage and lower flanks (20%, Sand Flats material). Reads as a vehicle assembled from everything it has killed.
- **Scale**: ~1.6× player height, ~2.2× player width.
- **Silhouette read**: Flatbed heavy truck. Raised forward cab. Roof-mounted rotating minigun visible on cab roof. Lateral sponson rams flank the cab. Six-wheel dual-rear configuration. Reads as a slow wall with spikes on both ends.

| Slot | Visual |
|---|---|
| Weapon | **Roof-mounted minigun** (revised from art-director's rear ram-launcher per user direction). Multi-barrel rotary assembly, six visible barrels in cluster. Sits on a pivot mount on the cab roof. Barrels are static in idle state, spin up visibly during Shred telegraph. |
| Engine | Twin armored engine blisters, one each side of cab, asymmetrically sized (left 20% larger — salvaged-mismatch visual tell). Each blister has three exhaust ports. |
| Mobility | Six wheels — two front steering (smaller), four rear drive in dual-tire configuration. The **spiked metal ball flail** trails from the rear axle on a chain, dragging in idle — visible behind the truck silhouette. |
| Frame | Reinforced cab. Flat roof, horizontal slit viewport (no curved glass). Three overlapping steel plate layers at cab corners. Front-bumper ram horns angled 30° down. |

**Slot state visuals (Phase 1)**: Online = full saturation. Damaged = visible cracks/dents on the corresponding part, faint soot trails. Offline = part visibly broken — minigun barrels frozen mid-rotation and drooped, engine blister split at seam with caps on exhaust ports, spike-flail chain severed and ball dragging on ground.

### Visual Transformation (Phase 2)

Triggered at HP ≤ 60%:

1. **Plate shed**: Three of the cab's outer plate layers blow off as a 16-particle debris burst (iron-colored sprites, each plate tumbling). Cab silhouette narrows by ~15% at the top.
2. **Exposed core revealed**: New geometry becomes visible behind the cab — a rectangular reactor block previously hidden by plates. Pulses amber → ember (`#E8B23A` → `#FF6A00`, 1.2s cycle). Brightest point on the sprite.
3. **Color desaturation**: Iron oxide field desaturates 20%, making the emissive core the visual focal point.
4. **Minigun deployment**: Minigun barrels lock into firing position; barrel-cluster visibly lower and forward-canted, ready to spin. (In Phase 1 the cluster sat in idle rest; Phase 2 it sits combat-ready.)
5. **Transition dust burst**: 400ms screen-edge dust burst (24 particles max), matching Enrage activation budget. This is mid-combat, NOT a death cascade — do not reuse the death tilt animation.

> **Locked 2026-05-19.** `#FF6A00` adopted as a new H.1.2 family color anchor (Phase 2 emissive core). The amber→ember gradient `#E8B23A → #FF6A00` is the Dredge's Phase 2 signature color cue. Add to the H.1.2 anchor list on the next art-bible pass; no art-director re-review required for this single-anchor addition.

**Enrage visual (Phase 1 or 2)**: Standard red rim + multiply tint per H.1.5. Additionally — exhaust port flames intensify, minigun barrels spin even in idle (Phase 2 only).

**Death cascade**: Standard 3-stage cascade scaled for Boss SilhouetteClass per H.1.9 (12 idle, 16 enrage, 20 death minimum frames). Cab fully collapses. Final dust column is wide and long-lasting.

### Sample Turn Trace — Phase 1 → Phase 2

Player context: Frame 20/20, Weapon 15/15, Engine 15/15, Mobility 15/15, Armor 5. Player position: Ahead (Dredge is Ahead of player on the chase rail).

**Phase 1 (Dredge HP > 60%, Dredge HP starts at 90):**

| Turn | Action | Notes |
|---|---|---|
| 1 | Dredge: Ram (P=0.55) → 10 damage; Armor 5 soaks 5; Frame 20→15. | Player blocks next turn or commits to strip. |
| 2 | Dredge: Taunt → applies Marked×1 to player Frame (next Frame-targeted hit gains +3 damage; consumed on use). Player plays 2 cards on Dredge Weapon slot, dealing 10 → Dredge Weapon 18→8. | Minigun still online, but cracking. |
| 3 | Dredge: Ram (Marked × Frame → +3) = 13 damage; Armor 3; Frame 15→5 (critical). Player plays Block + 1 damage card on Weapon → Weapon 8→2. | Player committed to strip, eating Frame damage. |
| 4 | Dredge: Sweep targets Weapon → 7 damage; player Weapon 15→8. Player plays cards on Dredge Weapon → Weapon 2→0 = **Offline**. | **Strip 1 achieved. Shred disabled for Phase 2.** |
| 5 | Dredge: Ram → 10. Armor 5 → Frame 5→0 = **player vehicle dead**. | OR: extended trace if player healed/blocked. Dredge HP from accumulated player damage: 90 − ~25 = 65. Phase transition not yet triggered. |

**Phase 2 trigger (Dredge HP drops to ≤ 54, i.e. ≤ 60% of 90):**

| Turn | Action | Notes |
|---|---|---|
| 6 | **Transition fires**. Dredge sheds cab plates, exposes core. Phase 2 intent pool active. Dredge: Shred (P=0 because Weapon Offline → filtered) → Bulldoze (now highest weight) = 14 damage Frame. | Strip 1 paid off — Shred suppressed forever. |
| 7 | Dredge: Javelin Hook → 8 damage + Stunned×1; next turn, the player's first card-play attempt is consumed (card discarded, energy fully refunded — per `status-effects.md` R1 Stunned definition). | Boss continues building pressure. |
| 8 | **Enrage** activates. Dredge: Bulldoze = 14 + 4 = 18 damage. | Phase 2 + Enrage stacking pressure. Player must close before turn 10–11. |

---

## Layer 2 — Dredge Signature Mechanics (Deferred)

These mechanics are part of the user's full vision for The Dredge but are **deferred until Layer 1 boss ships and validates the SO architecture**. Each requires architecture work beyond what `enemy-system.md` currently supports and should be specified as its own ADR or extension before implementation.

### Vulnerable Areas

Two visual locations on the boss chassis with the following properties:

- **No HP of their own** — not slots in the 4-slot system.
- **×3 damage multiplier** on hits that land in the VA zone.
- **Damage redirects to Frame** — Armor soaks first, then Frame HP.
- Likely tied to specific cards or aimed attacks (player chooses target zone).

Architecture impact: New hit-target concept beyond the 4 slots. Affects card targeting UI and damage pipeline.

### Card Injection — "Cut Chain"

When the Dredge lands a Javelin Hook (Phase 2), 3 "Cut Chain" cards are added to the player's deck (likely shuffled into draw pile).

- "Cut Chain" by itself does minor damage or no effect.
- "Cut Chain" + any Reposition card played same turn → player handbrake-dodges to behind the Dredge, evading the queued Bulldoze and entering the Spike Flail zone.

Architecture impact: Boss action mutates player deck mid-combat (new pattern). Card combo recognition system (multi-card play interaction).

### Tethered Status

A status effect applied to player on Javelin Hook hit. While Tethered:

- Player cannot play Reposition cards (the chain prevents it).
- Tether breaks when "Cut Chain" is played, when Dredge's Engine goes Offline, or after N turns.

Architecture impact: New status type with card-play-disable semantics. Extends `status-effects.md`.

### Production Order (Layer 2)

1. Vulnerable Areas (foundational visual + damage extension)
2. Tethered Status (small, fits existing status pattern)
3. Card Injection (medium, new mechanic)
4. Card combo recognition (largest, depends on injection)

---

## Difficulty Calibration

Per `enemy-system.md` D.5 DifficultyScore formula:

| Component | Dune Skimmer | Iron Shepherd | The Dredge |
|---|---|---|---|
| BaseHullHP | 28 | 56 | 90 |
| norm(HP) | 0.080 | 0.360 | 0.700 |
| AvgDPT | 5.20 | 5.82 | 9.20 |
| norm(DPT) | 0.057 | 0.087 | 0.248 |
| IntentCount (active pool) | 3 | 4 | 4 (Phase 1) / 4 (Phase 2) |
| norm(IC) | 0.286 | 0.429 | 0.429 |
| EnrageSeverity | 0 | 4 | 4 |
| norm(ES) | 0.000 | 0.333 | 0.333 |
| **DifficultyScore** | **0.090** | **0.257** | **0.423** |

Monotonic ascent with clean spacing. Scout sits in the floor band (DS < 0.40 → DS_FLOOR_BONUS = 4 Scrap). Elite also floor band. Boss crosses 0.40 — enters linear DSBonus scaling, reward scales with risk.

> Note: Dredge DPT calculation uses Phase 1 pool only for parity with the other archetypes. Phase 2 effective DPT is materially higher; full Phase-aware DifficultyScore is a candidate D.5 extension. Flag as Layer 2 telemetry-gated.

---

## Dependencies

- `enemy-system.md` — system spec for `EnemyDefinitionSO`, `BrainRuleset`, intent resolution, slot states, retarget policy, enrage formula.
- `vehicle-and-part-system.md` — variable slot vehicle model (ADR-0007), slot HP/Armor slot mechanic, R1 (`IsDead = StructuralHp == 0`), R_FL (FrameLayoutSO contract), R_ARM (Armor slot damage behavior).
- `docs/architecture/adr-0007-frame-driven-variable-slot-system.md` — data contract; supersedes ADR-0001 data contract. ADR-0001 visual contract still authoritative.
- `card-combat-system.md` — combat phase loop, position system, reposition-fallback R18, telegraph contract, damage pipeline.
- `status-effects.md` — Corroded (Iron Shepherd's Armor Rend, verified R1, Graduated damage-amp), Stunned (Dredge's Javelin Hook, added 2026-05-19 R1, Graduated card-play-skip counter), Marked (Dredge's Taunt, added 2026-05-19 R1, Binary one-shot Frame-damage amplifier +3 via F3).
- `node-encounter.md` — encounter placement (Skimmer ×N in Combat nodes, Shepherd at Elite beacon, Dredge at biome-final boss node).
- `loot-reward.md` — drop tables tied to DifficultyScore (Skimmer floor band → +4 Scrap bonus; Boss → linear DSBonus).

---

## Open Decisions Consolidated

W1 gate-closure pass completed 2026-05-19. All actionable items below are Locked or Deferred.

1. **Iron Shepherd — Corroded status**: ✅ **Locked 2026-05-19.** Verified Corroded exists in `status-effects.md` R1 line 34 (Graduated, damage-amp on Armor-soak interaction). No replacement needed; Armor Rend ships as spec'd.
2. **Dredge — Taunt mechanical effect**: ✅ **Locked 2026-05-19 (Option A).** Marked status — 1-turn marker, next Frame-targeted hit gains +3 damage, one-shot consumption. Note: Marked status itself must be added to `status-effects.md` before The Dredge ships (see item 8).
3. **Dredge — Javelin Hook slot binding**: ✅ **Locked 2026-05-19.** Engine slot binding (differentiates from Shred's Weapon binding). Damage scales `×0.0` when Engine state == Offline (Dredge's own Engine, not the player's).
4. **Dredge — Stunned status**: ✅ **Locked 2026-05-19.** Stunned added to `status-effects.md` R1 this session (Graduated, `Stunned×N` ⇒ next N card-play attempts consumed without resolving — card discarded, energy refunded; counter resets at end of turn).
5. **Dredge — SilhouetteClass enum value**: ✅ **Locked 2026-05-19.** `Large`. The enum stays `{Small, Medium, Large}` — no `Boss` extension. Boss-ness is conveyed by ArchetypeFamily + encounter placement, not by silhouette.
6. **Dredge — Phase 2 visual core color**: ✅ **Locked 2026-05-19.** `#FF6A00` adopted as a new H.1.2 family color anchor. Gradient `#E8B23A → #FF6A00` is the Dredge Phase 2 emissive signature.
7. **Dredge — Boss DifficultyScore calculation**: ⏸ **Deferred to telemetry.** Currently uses Phase 1 pool only. Phase-aware DifficultyScore is a D.5 candidate extension — revisit after live telemetry on Phase 2 reach rates.
8. **Marked status definition (follow-up)**: ✅ **Locked 2026-05-19.** Marked added to `status-effects.md` R1 as Binary class, Refresh merge, turn-start no-op, one-shot consumption on first Frame-targeted damage event (any source), `MarkedBonus = 3` flat (constant, R5). F3 formula, EC-S17/S18/S19/S20, Tuning Knob row, icon (crosshair / tarnished steel grey), VFX/audio rows, and AC-SE43–AC-SE50 all added in the same pass.

---

## Acceptance Criteria

A Biome 1 run is considered to use this roster correctly when:

1. Dune Skimmer can be encountered 2–3 times across nodes 1–2 in any single run.
2. Iron Shepherd appears exactly once at the Biome 1 Elite beacon.
3. The Dredge appears exactly once at the Biome 1 final node and is mandatory.
4. All three archetypes load as `EnemyDefinitionSO` assets (no hardcoded enemy data).
5. The Dredge's Phase 2 transition fires at HP ≤ 60% and not before.
6. Stripping the Dredge's Weapon (minigun) suppresses Shred from the Phase 2 intent pool for the remainder of the encounter.
7. Stripping the Dredge's Mobility (rear flail assembly) suppresses Spike Flail.
8. Stripping the Dredge's Engine suppresses Javelin Hook (per current `[NEEDS REVIEW]` binding decision).
9. The Dredge has no intent that heals it, repairs its parts, or restores its armor.
10. DifficultyScore values match the calibration table within ±0.01 (rounding tolerance).
11. Layer 2 mechanics (Vulnerable Areas, Card Injection, Tethered status) are absent from Layer 1 build but are documented as deferred work, not removed from the design.
