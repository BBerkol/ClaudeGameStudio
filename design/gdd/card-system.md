# Card System

> **Status**: Approved
> **Author**: Bertan Berkol + Claude Code agents
> **Last Updated**: 2026-04-20
> **Implements Pillar**: Pillar 3 (Read to Win), Pillar 2 (Chassis Identity), Pillar 4 (Scarcity with Agency)

## Overview

The Card System defines the complete data vocabulary for all playable cards in Wasteland Run. It specifies the `CardDefinitionSO` contract — the properties that make a card a card: energy cost, family, rarity tier, effect descriptor, and targeting type. Five families (Precision, Assault, Control, Repair, Maneuver) are the core behavioral taxonomy; each family's mechanical role is defined here and referenced without modification by the Card Combat, Status Effect, Chassis Identity, and Loot & Reward systems. The Card System also owns deck composition rules: how a chassis starter deck is constructed, how post-combat card rewards are drawn and presented, and the rarity distribution that governs card pool depth. The EA card pool spans approximately 75–100 cards per chassis (150–200 total), chassis-biased to reinforce Pillar 2 (Chassis Identity). Card System is a data layer — it defines what cards *are*; Card Combat System defines what happens when they are *played*.

## Player Fantasy

The Card System makes the chassis feel like a driver. Players never experience this system as a data layer — they feel it through the hand they're dealt and the hand they'll never see. Each chassis has a curated, exclusive card pool: Scout players will never encounter a ramming reward; Assault players will never see a pure evasion line. Because of this, the deck that builds over a run is inextricable from the vehicle that built it. A Scout develops a mental model of Scout-ness through repeated card selection: mobility plays, precision shots, surgical positioning over brute force. Switch to Assault and the opening hand *feels different before you've made a single decision* — the rhythm of available verbs has changed. The five families (Precision, Assault, Control, Repair, Maneuver) are the shared alphabet; the per-chassis pool is the dialect. Players don't study the Card System — they *absorb* it, run by run, through card rewards that always feel native to their vehicle.

## Detailed Design

### Core Rules

**1. Card Data Contract**

Every card in Wasteland Run is a `CardDefinitionSO` ScriptableObject asset. This SO is the authoritative definition of a card — all systems that reference cards do so by loading a `CardDefinitionSO`. No card data may be hardcoded.

| Field | Type | Description |
|---|---|---|
| `CardId` | `string` | Stable key for save/serialization. Format: `[chassis]_[family]_[sequence]` (e.g., `scout_precision_007`). Never changes post-ship. |
| `DisplayName` | `string` | Human-readable name. Localization key candidate. Separate from `CardId` so names can change without breaking saves. |
| `Description` | `string` | Templated string using `{param}` tokens so displayed values auto-update when tuning numbers change. Example: `"Deal {damage} damage to target subsystem."` Hardcoded display text is forbidden — it breaks balance iteration. |
| `FlavorText` | `string` | Optional lore text. No gameplay effect. |
| `CardArtKey` | `string` | Addressables key for the card face sprite. |
| `Family` | `CardFamily` enum | `Precision`, `Assault`, `Control`, `Repair`, `Maneuver`. Required for synergy queries and pool filtering. |
| `Rarity` | `CardRarity` enum | `Common`, `Uncommon`, `Rare`. (Legendary reserved, not in EA pool.) |
| `ChassisPool` | `ChassisType` enum | Which chassis can receive this card as a reward. Pools are chassis-exclusive — Scout players never see Assault cards as rewards. |
| `IsStarterCard` | `bool` | If `true`, eligible for inclusion in the fixed starter deck for this chassis. |
| `EnergyCost` | `int` | 0–3. Cost to play. Cost-0 cards must be explicitly justified in design; they are rare. Enforced at SO import: `EnergyCost >= 0`. |
| ~~`PurgeCost`~~ | ~~`int`~~ | **REMOVED 2026-04-23 (Scrap Economy retrofit).** Per Scrap Economy GDD D.1 / D.4, Purge resolves at the flat `GlobalPurgeCost = 30` (tuned globally, not per-card). The field no longer exists on `CardDefinitionSO`; SO authoring tools must not expose it; any legacy asset carrying a `PurgeCost` value is ignored by the SO importer and stripped on re-save. |
| `MerchantPrice` | `int` | 1–100. Scrap the player pays to purchase this card at a Merchant node. Must NOT equal `GlobalPurgeCost` (30) — degenerate parity per Scrap Economy D.4 parity rule (OQ7 resolution). Default value 0 indicates price not yet authored; Scrap Economy GDD owns the pricing formula. Enforced at SO import: `MerchantPrice >= 0` (0 = unset, not free) AND `MerchantPrice != 30` unless 0. |
| `TargetType` | `TargetType` enum | `Self`, `EnemySubsystem`, `AllEnemySubsystems`, `NoTarget`. Drives the targeting UI prompt. |
| `ValidSubsystemTargets` | `SlotType[]` | When `TargetType == EnemySubsystem`: the slots the player may target. Empty = all 4 slots valid. Uses the `SlotType` enum from ADR-0001 (post-Armor-retrofit): `{Weapon, Engine, Mobility, Frame}`. **Subsystem-strike targeting rule:** cards where `DamageEffectSO.BypassPlating == true` must have a non-empty `ValidSubsystemTargets` that **excludes** `Frame` — enforced at SO import. (Empty = all-slots which would allow Frame; Frame cannot be targeted by subsystem-strike because Frame is protected by Armor, not Plating, and subsystem-strike's contract is "bypass non-Frame Plating only." See Card Combat F-CC1.) |
| `PositionRequirement` | `PositionRequirement` enum | `None`, `RequiresBehind`, `RequiresAhead`, `BonusIfBehind`, `BonusIfAhead`. Cards with `Requires*` cannot be played unless the condition is met. Cards with `Bonus*` can always be played but conditional effects activate only when met. |
| `Effects` | `CardEffectSO[]` | Ordered list of effects. Applied in sequence during resolution. May contain 1–N effects. |
| `Keywords` | `CardKeyword` flags | `Exhaust`, `Retain`, `Innate`, `Ethereal`. See Keywords section below. |
| `BaseDamage` | `int` | **Convenience cache — `DamageEffectSO.Amount` is the authoritative source** for both the `{damage}` display token and F1 combat resolution. `BaseDamage` is stored here so formula systems (e.g., mastery scaling) can read it without iterating the `Effects` list. SO import **fails** if `BaseDamage ≠ DamageEffectSO.Amount` on any card that contains a `DamageEffectSO`. Must satisfy `BaseDamage >= 1` on any card with a `DamageEffectSO`. |

> **Post-EA reserved fields — not present in EA `CardDefinitionSO`:** `UpgradedVersion (CardDefinitionSO ref)` and `IsUpgraded (bool)` are reserved for a post-EA card upgrade mechanic. These fields do not exist in EA. No SO authoring tool should expose them. When a card upgrade GDD is authored post-EA, these fields will be added with full mechanic specification (acquisition vector, Scrap cost, copy-limit interaction). See OQ2.

**1a. Description Token Binding**

`{param}` tokens in `Description` strings resolve at display time via a fixed binding table. The token vocabulary is closed — only the tokens listed below are valid. Unbound tokens (e.g., a typo like `{dmg}`) render as the literal string `?` at runtime and emit a compile-time validation warning at SO import.

| Token | Resolves To | Effect Type Required |
|---|---|---|
| `{damage}` | First `DamageEffectSO.Amount` in the `Effects` list | `DamageEffectSO` |
| `{bonus}` | First `DamageEffectSO.PositionBonus` | `DamageEffectSO` |
| `{heal}` | `RepairSubsystemEffectSO.HpRestored` | `RepairSubsystemEffectSO` |
| `{plating}` | `RestorePlatingEffectSO.Amount` | `RestorePlatingEffectSO` |
| `{armor}` | `RestoreArmorEffectSO.Amount` | `RestoreArmorEffectSO` |
| `{draws}` | `DrawCardsEffectSO.Count` | `DrawCardsEffectSO` |
| `{energy}` | `GainEnergyEffectSO.Amount` | `GainEnergyEffectSO` |
| `{stacks}` | `ApplyStatusEffectSO.Stacks` | `ApplyStatusEffectSO` |
| `{duration}` | `ApplyStatusEffectSO.Duration` | `ApplyStatusEffectSO` |
| `{cost}` | `CardDefinitionSO.EnergyCost` | Any |

For cards with multiple effects of the same type, use indexed tokens: `{damage.1}`, `{damage.2}` resolve to the first and second `DamageEffectSO` in the list respectively.

**Display context rule:** In combat (where a `CardResolutionContext` is available), tokens resolve to their current contextual values. In all non-combat display contexts (reward screen, Merchant, Chopshop, deck inspection), tokens resolve to the baseline SO field value — no runtime context is required. The display layer must never assume a live context outside of combat.

---

**2. Card Effect Architecture**

Card effects are defined as `CardEffectSO` ScriptableObject assets. The abstract base class defines three fields shared by all effect types:

- **Target** (`EffectTarget` enum): `Self`, `Opponent`, `TargetSlot`. Identifies what the effect acts upon.
- **Condition** (`EffectConditionSO` reference, nullable): If null, the effect always fires. If assigned, the Card Combat pipeline evaluates `condition.IsMet(context)` before applying. Conditions are their own SO hierarchy: `PositionConditionSO` (tests `Position == Behind | Ahead`), `SlotStateConditionSO` (tests `SlotType` against `DamageState`), `StatusConditionSO` (tests whether a specific status effect is active on a target vehicle — parameters: `StatusType (StatusType enum)`, `TargetVehicle (Self | Opponent)`; required for Control Uncommon+ cards that gate damage behind setup conditions such as "if target is Stalled, deal 8 damage").
- **Effect parameters**: Defined per concrete subclass. All are designer-tunable fields exposed in the Unity Inspector with no code change needed.

**Required EA effect types:**

| SO Type | Parameters | Description |
|---|---|---|
| `DamageEffectSO` | `Amount (int)`, `PositionBonus (int)`, `BypassPlating (bool, default false)` | Deals damage to the targeted subsystem. `PositionBonus` adds to Amount when the card's `PositionRequirement` bonus condition is met. **`BypassPlating` flag (subsystem-strike archetype):** when `true`, Card Combat F-CC1 step 2 (shield absorption) is skipped on the non-Frame target — Plating is NOT consumed and damage lands directly on slot Hp. Cards with `BypassPlating = true` must target a non-Frame slot (enforced via `ValidSubsystemTargets`; see field description and SO import rules). Frame cannot be targeted by subsystem-strike because Armor (not Plating) protects Frame and subsystem-strike's contract is "bypass non-Frame Plating only." Default `false`. Burning/DOT also bypasses shield absorption, but via `DamageSource.Status`, NOT this flag — `BypassPlating` is for card-authored subsystem-strike cards only. |
| `RestorePlatingEffectSO` | `Amount (int)` | Adds plating stacks to a self-slot (Target = Self) or removes from an enemy slot (Target = Opponent). |
| `RestoreArmorEffectSO` | `Amount (int)` | Adds `Amount` to the vehicle's `CurrentArmor` via `IVehicleMutator.AddArmor(int)` (V&P EC-VP20 contract). Result is hard-capped at `MaxArmor`; overflow is wasted (stress-test EC-5 lock). Target is implicit Self — Armor is a vehicle-level stat, not per-slot; there is no "enemy slot" analog. Enforced at SO import: `Amount >= 1`. Cannot reuse `RestorePlatingEffectSO` because Plating is per-slot and Armor is vehicle-level (stress-test T-6). |
| `ApplyStatusEffectSO` | `StatusType (enum)`, `Duration (int)`, `Stacks (int)` | Applies a status effect. `StatusType` enum is owned by the Status Effect System GDD. |
| `RepairSubsystemEffectSO` | `HpRestored (int)`, `CanReviveOffline (bool)` | Restores HP to a self-slot. If `CanReviveOffline` is false, cannot bring an Offline slot back to Degraded. Full revival (Offline → Functional) requires a dedicated high-cost card; not achievable from this effect alone. |
| `DrawCardsEffectSO` | `Count (int)`, `FilterFamily (CardFamily?)` | Draws N cards from the deck. Optional family filter draws only from matching family. **If `FilterFamily` is set and the matching subset in the deck is empty (all cards of that family are in hand, discard, or Exhausted), the draw silently produces nothing — no error, no fallback to full deck.** The player continues with whatever is currently in hand. |
| `GainEnergyEffectSO` | `Amount (int)` | Adds energy to the current turn's pool. |
| `ShiftPositionEffectSO` | `NewPosition (PositionState)` | Changes position state to Behind or Ahead. |

The Card Combat pipeline resolves effects in list order: for each effect, evaluate its condition against the `CardResolutionContext`; if the condition is absent or met, call `effect.Apply(context)`. Conditions may reference `PositionState` or `SubsystemState` only through the context object. Effects may mutate `SubsystemState` only through `IVehicleMutator`. No effect may read from or write to `CombatLoop` internal state directly. (See systems index Scoping Note C1.)

**3. Card Families**

Five families define the behavioral taxonomy. Family roles are universal — they do not change per chassis. Chassis pools lean heavily toward certain families, but the family behavior itself is consistent across all chassis.

| Family | Mechanical Role | Primary Effect Types | Target Type | Chassis Lean |
|---|---|---|---|---|
| **Precision** | Surgical single-subsystem damage. Primary tool for triggering Offline states. Strips Plating from a chosen non-Frame slot (Frame is protected by vehicle-level Armor, not Plating — see Card Combat F-CC1). Many gain bonus damage when `Ahead`. **Subsystem-strike archetype:** Precision is the natural home for cards with `DamageEffectSO.BypassPlating = true` (e.g., **Puncture**) — a subsystem-strike card bypasses the non-Frame Plating layer entirely, landing full damage on slot Hp. Subsystem-strike cards cannot target Frame by contract. | `DamageEffectSO` (restricted `ValidSubsystemTargets`; optionally `BypassPlating = true` for subsystem-strike), `RestorePlatingEffectSO` (enemy) | `EnemySubsystem` (restricted, Frame-exclusive when `BypassPlating = true`) | Scout-heavy |
| **Assault** | Raw hull damage and broad pressure. Two sub-categories: **Broad Assault** hits `AllEnemySubsystems` or `Frame` — no targeting decision required. **Focused Assault** targets a single specified `ValidSubsystemTarget` slot at ~70% of the Broad Assault damage value — adds a read-and-choose targeting decision for Pillar 3. Assault chassis pools lean Broad-heavy at Common, Focused-heavy at Uncommon+. Many Assault cards require or reward being `Behind` (ramming/tailgating). | `DamageEffectSO` (Broad: `AllEnemySubsystems` or `Frame`; Focused: `EnemySubsystem` with non-empty `ValidSubsystemTargets`), `ApplyStatusEffectSO` (vehicle-wide) | `AllEnemySubsystems`, `EnemySubsystem` (Frame only), or `EnemySubsystem` (Focused, specific slot) | Assault-heavy |
| **Control** | Disruption of the enemy's next action. Denies attacks, modifies retargeting, or applies energy-draining status. **Every Control card must include at least one damage-dealing effect (minimum 1 damage).** Pure disruption with zero damage output is not permitted. Common Control cards may deal as little as 1–2 damage alongside the disruption effect; Uncommon+ may gate higher damage behind setup conditions (see `StatusConditionSO`). **Control is a support family — it is not designed as a standalone kill path.** A Control-dominant deck without meaningful Precision or Assault cards will lack the damage output to take subsystems Offline efficiently. Control's primary role is disrupting enemy actions to create windows for Precision and Assault cards to land. The "minimum 1 damage" rule is a per-card floor; it does not prevent high-Rarity Control decks from grinding wins at 1 damage/turn against high-HP enemies. **Forward dependency: the Card Combat System GDD must specify a time-pressure mechanic (e.g., enemy Enrage after N turns, dealing escalating damage to the player) as the system-level kill-rate enforcement. Without an Enrage-equivalent, the "minimum 1 damage" rule is insufficient to prevent Control-dominant stall strategies.** | `ApplyStatusEffectSO` (Stalled, Redirected) always paired with `DamageEffectSO` (minimum Amount 1), effects with `TargetType = NoTarget` | `NoTarget` or `Self` for disruption component; `EnemySubsystem` or `AllEnemySubsystems` for damage component | Mixed (Scout: setup windows; Assault: momentum) |
| **Repair** | Subsystem and hull restoration. Reverses damage, restores Offline → Degraded, adds Plating to self-slots, and restores vehicle-level `CurrentArmor` via `RestoreArmorEffectSO`. The only family where the player targets their own vehicle (self-slots for Plating/Hp restoration; implicit Self for Armor). Armor restoration caps at `MaxArmor` (hard cap; overflow wasted — stress-test EC-5). | `RepairSubsystemEffectSO`, `RestorePlatingEffectSO` (self), `RestoreArmorEffectSO` (self) | `Self` | Balanced (Scout leans Mobility/Engine; Assault leans Weapon/Frame) |
| **Maneuver** | Position manipulation, deck cycling, and energy efficiency. Setup family — improves future turns rather than dealing immediate damage. | `ShiftPositionEffectSO`, `DrawCardsEffectSO`, `GainEnergyEffectSO` | `NoTarget` | Scout-heavy (tactical setup); Assault variant (cost reduction, momentum) |

**4. Card Keywords**

Keywords are machine-readable behavioral flags on the `CardDefinitionSO`. Resolved by Card Combat System.

| Keyword | Effect |
|---|---|
| **Exhaust** | Removed from the deck for the rest of the run after being played. Does not enter the discard pile. |
| **Retain** | Not discarded at end of turn. Stays in hand. Useful for setup cards held across turns. |
| **Innate** | Always appears in the opening hand at the start of **every combat** in the run. Bypasses draw order. Does not consume a draw slot — if the opening hand draws 4 cards, an Innate card is added on top of those 4. Multiple Innate cards all appear; the full opening hand is Innate cards + normal draws. **Maximum 3 Innate cards per deck** — enforced at reward/Merchant offer (Innate cards are filtered from offers if the current deck already contains 3 Innate cards). At the cap of 3 Innate, the opening hand is exactly 3 Innate + 4 normal = 7 cards, which is at the hand compression threshold but does not exceed it. |
| **Ethereal** | Auto-discarded at end of turn if not played. Cannot be Retained. Typically used on powerful cost-0 cards to prevent hoarding. |

**5. Rarity**

Rarity signals both effect complexity and power ceiling:

- **Common**: Single effect, no condition, no position requirement. Core deck vocabulary. The player understands it immediately. Examples: "Deal 4 damage to one subsystem," "Restore 2 plating to a self-slot," "Restore 3 Armor."
- **Uncommon**: One condition OR one synergy hook OR one keyword. Rewards strategic play. Example: "Deal 4 damage to one subsystem. If Degraded, deal 8 instead."
- **Rare**: Multi-effect OR complex condition OR powerful keyword combination. Run-defining cards. Example: "Deal 5 damage to all subsystems. Each Offline subsystem takes double damage."

Per-chassis EA pool distribution:

| Rarity | Count per Pool | % of Pool |
|---|---|---|
| Common | 40–45 | ~50% |
| Uncommon | 25–30 | ~32% |
| Rare | 10–15 | ~15% |

> **Note:** Percentages are approximate ranges derived from the card count ranges above. The residual (~3%) reflects the overlap between the min/max pool size bounds (75–100 cards). These percentages are design targets, not exact values; the F2 rarity draw weights (stored on `ChassisMasteryDefinitionSO`) are the authoritative source of reward probabilities.

**Mastery 1–3 Reward Bias (Early-Run Identity Delivery):** At Mastery 1–3, reward offers apply a **slot-force bias**: Slot 1 is always drawn from the chassis's primary-family pool; Slot 2 runs standard F2 from the full chassis pool. This guarantees exactly one primary-family card per offer and ensures early-run chassis identity is legible before the full pool breadth opens. At Mastery 4+, the bias is disabled — both slots run standard F2.

**Slot-force algorithm (exact):**
1. Identify the **primary-family pool**: all cards in the chassis pool where `Family` matches the chassis's `PrimaryFamily` value (Scout: `Precision` or `Maneuver`; Assault: `Assault`), filtered to cards not at copy limit.
2. Draw **Slot 1** from the primary-family pool using F2 rarity weight normalization. The weights (from `ChassisMasteryDefinitionSO`) are applied only to cards in the primary-family pool.
3. Draw **Slot 2** from the full chassis pool (minus Slot 1's card) using standard F2.
4. **Degenerate pool fallback**: if the primary-family pool is empty (all primary-family cards at copy limit), both slots run standard F2 from the full chassis pool. The bias is silently skipped — no notification to the player.
5. **Exactly 1 eligible card in primary-family pool**: Slot 1 is forced to that one card. Slot 2 runs standard F2 as normal. This is expected late-run behavior as copy limits reduce the eligible pool.

**`PrimaryFamily` data source:** Stored as `PrimaryFamily: CardFamily[]` on `ChassisMasteryDefinitionSO` (Scout: `[Precision, Maneuver]`; Assault: `[Assault]`). The bias flag `PrimaryFamilyBiasEnabled: bool` per mastery tier on the same SO controls whether the slot-force applies. Mastery 1–3 = enabled; Mastery 4+ = disabled.

**Design note — Mastery 4 dual transition:** At Mastery 4, both the primary-family bias and the rarity weights shift simultaneously (85/14/1 → 70/25/5). This is intentional: by Mastery 4, the player has sufficient chassis fluency to benefit from broader pool access and higher-rarity pressure at the same time. These are not staggered.

Reward offer pull weights are mastery-gated. Weights are stored on `ChassisMasteryDefinitionSO` (owned by Vehicle & Part System GDD), not hardcoded:

| Mastery Level | Common Weight | Uncommon Weight | Rare Weight |
|---|---|---|---|
| 1–3 | 85% | 14% | 1% |
| 4–6 | 70% | 25% | 5% |
| 7–10 | 55% | 35% | 10% |

**Rare Discovery Pity Counter:** To ensure new players encounter at least one Rare within a reasonable run, a per-run pity counter is tracked. If the player receives no Rare offer across 8 consecutive combat reward offers, the 9th offer is guaranteed to include one Rare alongside one normal-weighted draw.

**Pity precedence rule:** The pity guarantee governs over the F2 pool exhaustion fallback. When pity fires, the draw first attempts the Rare pool. The F2 tier-degradation path (Rare → Uncommon → Common) does **not** apply to a pity-triggered draw — the two paths are mutually exclusive.

**Empty Rare pool on pity:** If the Rare pool is empty (player already holds the maximum 1 copy of every Rare in the chassis pool), the pity guarantee converts to a Scrap compensation award (amount defined by Scrap Economy GDD) and no card is offered from the Rare tier. **The counter resets to 0 after the Scrap compensation award.** The pity event fired and resolved; the player has exhausted the Rare pool and holds a complete Rare collection.

**Counter reset rules:**
- Resets to 0 on any combat reward offer that includes a Rare card (including offers the player skips — the Rare was generated, the player declined it).
- Resets to 0 after a Scrap compensation pity award (empty Rare pool case).
- Does **not** reset on Merchant Rare purchases — pity tracks combat reward drought only. See below.
- Does not persist between runs.

**Merchant Rare purchases do NOT reset the pity counter.** This is a deliberate design decision: the pity counter exclusively tracks combat reward offer drought. A player who purchases a Rare from a Merchant has exercised their Scrap economy — their combat reward pity protection accumulates independently. A player who buys a Merchant Rare may still trigger the pity counter after 8 dry combat rewards in the same run, potentially acquiring a second Rare. This is intentional: it rewards strategic Scrap spending without neutralizing bad-luck protection.

**Card Copy Limits:** A player's deck may hold a maximum of 3 copies of any Common or Uncommon card and a maximum of 1 copy of any Rare card. Reward offers, Merchant pools, and the Chopshop add-card path (if any) filter out cards that would exceed these limits for the current run.

**6. Deck Composition Rules**

| Rule | Value |
|---|---|
| Starter deck size | 10 cards, fixed per chassis, same every run |
| Minimum deck size | 10 — cannot purge below starter size |
| `MaxDeckSize` (runtime safety ceiling) | 60 — see "MaxDeckSize as runtime safety ceiling" below |
| Cards offered per post-combat reward | 2 |
| Player picks | 1 or 0 (skip) |
| Max copies per card (Common/Uncommon) | 3 copies per card per run |
| Max copies per card (Rare) | 1 copy per card per run |

**No *design-time* maximum deck size.** There is no upper cap that a player-chosen deck-grow path (reward pickup or Merchant purchase) enforces — a player who accepts every reward and never purges will accumulate a large deck, but increasing draw variance is the structural skip incentive. Player-chosen cards never bump against an architectural cap during normal EA play.

**`MaxDeckSize` as runtime safety ceiling (closes R6 Blocker 7 — economy-designer + systems-designer):** the `MaxDeckSize = 60` constant is a *runtime* safety ceiling, NOT a design-cap. It exists for three reasons:
1. **Granted-card composition can outpace player choice.** A future high-slot-count chassis (post-EA Heavy variants, Dredge-style player frames) installing parts that each grant 3 cards (V&P R7 Rare cap) could push the deck past the dilution-as-skip-incentive intent that assumes player-chosen growth. The R6 worst-case projection — 10 slots × 3 cards = 30 granted + 10 starter = 40-card deck — sits comfortably under 60.
2. **Memory and serialization safety.** `VehicleStateDTO` + `CardDeckDTO` round-trip on every save (ADR-0004); a 60-card cap bounds the worst-case DTO size and prevents pathological growth from accidental edge cases (e.g., a future loot-event bug that grants cards without check).
3. **The cap is unreachable under EA-shipping content.** EA player chassis (Small/Medium/Heavy = 4/5/7 slots) granting at most 3 cards per part yields a maximum of 7×3 + 10 = 31 cards, which is half the ceiling. The cap is invisible to players in EA but available for hard-assert in tests and future content.

**Cap behavior on overflow:** when a deck-grow operation would push deck size above `MaxDeckSize`, the operation is **rejected** with the result `DeckGrowResult.CapacityExceeded`. Player-driven sources (reward pick, Merchant purchase) surface a UI message "Deck full — purge a card first." Granted-card install via V&P R5 in EA cannot reach the cap, so no overflow path is exercised in shipping content; the `HardRemoveCards` complement (V&P Decision 16) keeps the cap reachable only by replacing parts, not by adding granted cards beyond replaced ones. A NUnit EditMode test asserts `MaxDeckSize` is unreachable under all EA-shipping chassis × part combinations.

**Skip incentive — draw concentration:** Each card added to the deck dilutes the probability of drawing any specific card when it is needed. A 10-card deck cycling at 4 cards/turn sees each card approximately every 2.5 turns; a 20-card deck every 5 turns; a 30-card deck every 7.5 turns; the runtime-ceiling 60-card deck cycles every 15 turns (intentionally beyond useful play length to confirm the cap is a safety, not a design lever). Players who over-accept rewards will find their key synergy cards increasingly unreliable — the hand will not contain what the situation demands. This is the intended Pillar 4 mechanism. **The skip decision is a probabilistic trade-off: a lean deck draws the right card at the right moment more consistently. There is no explicit Scrap reward for skipping — the reward is deck coherence.** This is the authoritative design decision for no-design-cap deck size. The Card Combat GDD must define the standard hand draw size (assumed 4 cards/turn); if this changes, the coherence math above should be re-verified.

**Chassis Identity Constraint on Starter Decks:** Each chassis starter deck must satisfy minimum family distribution requirements to deliver the Player Fantasy ("the opening hand feels different before a single decision"):
- Scout starter: at minimum 3 Precision cards and 2 Maneuver cards
- Assault starter: at minimum 3 Assault family cards (of which at minimum 2 are Broad Assault) and 2 Precision cards; **at most 1 Maneuver card** (Maneuver is Scout's identity marker — more than 1 Maneuver card in an Assault starter blurs the chassis identity gap)
- Both chassis starters must include at minimum 1 Repair card
- No chassis starter may contain 0 cards from its primary chassis-lean family

The specific card list is authored in `ChassisDefinitionSO` (owned by Vehicle & Part System GDD); these constraints are enforced at SO validation.

Deck grows through exactly three vectors:

1. **Post-combat reward** — 2 cards offered, pick 1 or skip. One offer per Normal and Elite combat. No card offer from bosses (bosses drop parts, keeping reward types distinct).
2. **Chopshop node** — Player may purge 1 card for a Scrap cost (or free per event roll). Purging below 10 cards is forbidden and must be blocked at the game logic level with a clear UI message. **Free Purge Valve:** Each Chopshop node has a seeded 33% chance to waive the Scrap cost for the first purge during that visit. This is determined when the player *enters* the Chopshop node (not at map generation time), using `System.Random` seeded by `runSeed + nodeIndex`. The result is stored as `IsFreeValveApplied: bool` on the node's runtime state object (owned by Save & Persistence System — this is a new dependency, see Dependencies). The result is communicated to the player on entering the Chopshop UI (e.g., "Mechanics are in a good mood — first purge free"). If the game is reloaded mid-session, the persisted `IsFreeValveApplied` value is read from save state — the roll does NOT re-fire on load. The free purge applies to the first purge only; any additional purges during the same visit cost the standard `PurgeCost` Scrap.
3. **Merchant node** — Player may purchase 1 card from an offered pool of 2–3 cards for Scrap.

Expected deck size at run end (no purging, 14 combat nodes, 70% pickup): ~21 cards. Players who accept most rewards and never purge will accumulate larger decks; this is intentional. There is no maximum deck size — see Deck Composition Rules.

The starter deck's specific card list is owned by `ChassisDefinitionSO` in the Vehicle & Part System GDD. Card System owns the rules: starter decks exist, they contain exactly 10 cards, they are fixed per chassis, `IsStarterCard = true` marks a card as eligible for inclusion, and starter decks must satisfy the Chassis Identity Constraint defined in Deck Composition Rules.

---

### States and Transitions

A card occupies one of four locations during a run:

| State | Description | How Entered | How Exited |
|---|---|---|---|
| **In Deck** | Available to be drawn | Start of run (starter cards), added via reward or merchant | Drawn to hand; exhausted (Exhaust keyword); purged at Chopshop |
| **In Hand** | Held by the player this turn | Drawn from deck | Played (→ Discard or Exhausted); discarded at turn end (unless Retain); auto-discarded by Ethereal |
| **In Discard** | Played or discarded this turn | Played from hand (without Exhaust); discarded at turn end | Reshuffled into deck when deck is empty and a draw is attempted |
| **Exhausted** | Removed for the rest of the run | Played with Exhaust keyword | Cannot return — inaccessible until run ends |

Discard pile reshuffles into the deck automatically and instantaneously when the deck is empty and a draw is attempted. There is no shuffle delay. (Card Combat System GDD owns the draw/discard pipeline execution; Card System defines the states.)

---

### Interactions with Other Systems

| System | Interface | Direction |
|---|---|---|
| **Card Combat System** | Reads `CardDefinitionSO` to execute the play pipeline. Calls `effect.Apply(CardResolutionContext)` for each effect in sequence. Evaluates `EffectConditionSO.IsMet(context)` before conditional effects. F-CC1 damage pipeline forks on target slot: Frame path consumes vehicle-level `CurrentArmor`; non-Frame path consumes slot `PlatingStacks`. When `DamageEffectSO.BypassPlating == true`, Card Combat skips F-CC1 step 2 on the non-Frame target (subsystem-strike exception). DOT damage (`DamageSource.Status`) also bypasses step 2 on both paths via a separate exception — Burning uses that path, NOT `BypassPlating`. `RestoreArmorEffectSO.Apply` calls `IVehicleMutator.AddArmor`; no F-CC1 involvement (not a damage effect). | Data source: Card System provides definitions; Combat executes them |
| **Status Effect System** | `ApplyStatusEffectSO` passes `StatusType` and parameters. Status Effect System owns the `StatusType` enum and all resolution logic. Card System does not define what statuses do. | Card System → Status Effects (triggers application only) |
| **Vehicle & Part System** | `DamageEffectSO` and `RepairSubsystemEffectSO` call `IVehicleMutator` for vehicle state writes. `RestoreArmorEffectSO.Apply` calls `IVehicleMutator.AddArmor(int)` — the vehicle-level Armor mutator contract owned by V&P (EC-VP20; cap-at-MaxArmor clamp enforced inside the mutator). `EffectConditionSO` queries `IVehicleView` for slot state reads (4-slot model: Weapon/Engine/Mobility/Frame); `CurrentArmor`/`MaxArmor` read via `IVehicleView` for card-hover previews. `ChassisDefinitionSO` (Vehicle & Part) holds the specific starter deck card list. | Bidirectional: Card System reads vehicle state; mutators write it |
| **Chassis Identity System** | Reads `ChassisPool` field to filter which cards appear in reward pools for the current chassis. Card System defines the field; Chassis Identity reads it. | Card System → Chassis Identity (data only) |
| **Loot & Reward System** | Card System exposes `ICardRewardGenerator` (see below) as the canonical card-offer entry point for L&R. L&R calls `Generate(chassis, mastery, rarePityCounter, drawCount)` and receives a `CardDraft[]` with full tooltip schema; it does not read `CardDefinitionSO` assets directly. Card System owns chassis-pool filtering, mastery-gated rarity weights, and rare-pity counter logic end-to-end. | Card System → Loot & Reward (interface contract) |
| **Scrap Economy System** | **Does NOT read `PurgeCost`** — Purge resolves at flat `GlobalPurgeCost = 30` (Scrap Economy D.1 / D.4; field removed 2026-04-23). Card System enforces `DeckSize > 10` (EC7) as the Scrap Economy purge verb's precondition. Card System reads `MerchantPrice` for Merchant pricing contracts. | Card System → Economy (data only) |
| **Save & Persistence System** | Serializes deck, discard pile, and exhausted card list as `List<string>` of `CardId` values. Deserializes by loading `CardDefinitionSO` assets by key via Addressables. | Bidirectional: Save serializes; Card System provides `CardId` keys and loads assets |

## Formulas

### F1 — Card Damage Output

The damage value a damage-dealing card contributes to the Card Combat pipeline.

```
DamageOutput = BaseDamage + (PositionBonus × PositionConditionMet)
```

**Variables:**
- `BaseDamage` = `DamageEffectSO.Amount` — int, range 1–12 for EA cards
- `PositionBonus` = `DamageEffectSO.PositionBonus` — int, 0 if card has no position requirement. Enforced at SO import: `PositionBonus >= 0` (negative values would produce negative DamageOutput). Recommended design ceiling: 8 (to be cross-referenced against enemy subsystem HP once the Card Combat GDD defines HP ranges).
- `PositionConditionMet` = 1 if player's current position satisfies the card's `PositionRequirement`, else 0

`DamageOutput` is passed to Card Combat System as a resolved int. Card Combat owns all further transformation (plating reduction, subsystem HP deduction). Card System owns only the `DamageOutput` computation.

**Example:**
- Card: `scout_precision_007` — `BaseDamage = 4`, `PositionBonus = 3`, `PositionRequirement = BonusIfAhead`
- Player is Ahead: `DamageOutput = 4 + (3 × 1) = 7`
- Player is Behind: `DamageOutput = 4 + (3 × 0) = 4`

---

### F2 — Rarity Draw Weight Normalization

Card reward offers use weighted random selection from the chassis pool.

```
P(rarity) = W(rarity) / (W(Common) + W(Uncommon) + W(Rare))
```

`W(rarity)` is loaded from `ChassisMasteryDefinitionSO` for the player's current mastery level.

| Mastery Level | W(Common) | W(Uncommon) | W(Rare) | P(Common) | P(Uncommon) | P(Rare) |
|---|---|---|---|---|---|---|
| 1–3 | 85 | 14 | 1 | 85.0% | 14.0% | 1.0% |
| 4–6 | 70 | 25 | 5 | 70.0% | 25.0% | 5.0% |
| 7–10 | 55 | 35 | 10 | 55.0% | 35.0% | 10.0% |

All weights must sum to 100 per mastery tier — enforced at SO import. Individual weights must also satisfy `W(rarity) >= 0` — a weight of 0 makes that rarity unreachable. Weights live on `ChassisMasteryDefinitionSO`; designers adjust without code changes.

**F2 Variable Table:**

| Symbol | Type | Range | Description |
|---|---|---|---|
| `W(rarity)` | int | 0–100 | Weight for this rarity tier, loaded from `ChassisMasteryDefinitionSO` |
| `P(rarity)` | float | [0.0, 1.0] | Normalized probability of drawing this rarity tier |

Output range: `[0.0, 1.0]` — guaranteed by normalization when weights sum to 100.

**Pool Exhaustion Fallback:** The draw pool for each rarity tier is filtered to cards not already at their maximum copy limit in the player's deck (3 copies for Common/Uncommon, 1 copy for Rare). If the weighted draw selects a rarity tier whose filtered pool is empty, the system degrades to the next-lower rarity tier (Rare → Uncommon → Common) and draws from that tier's eligible pool. If all three tiers are fully exhausted (all cards at copy limit), the slot awards Scrap compensation (amount defined by Scrap Economy GDD) instead of offering a card. **Duplicate cards are never offered** — the reward screen always presents cards the player can legally add. The without-replacement constraint (same card cannot appear twice in one 2-card offer) applies through the fallback: the Slot 2 draw excludes the card drawn for Slot 1.

---

### F3 — Two-Card Offer: Probability of At Least One Rare

Each reward offer presents 2 cards drawn **without replacement** (the same card cannot appear twice in one offer). Draws are treated as statistically independent for the purpose of this formula — a valid approximation for pool sizes ≥ 50 cards:

```
P(≥1 Rare in offer) = 1 − (1 − P(Rare))²
```

| Mastery Level | P(Rare) | P(≥1 Rare in offer) |
|---|---|---|
| 1–3 | 1.0% | ~2.0% |
| 4–6 | 5.0% | ~9.75% |
| 7–10 | 10.0% | ~19.0% |

Design target: a mastery-7 player should see a Rare offer roughly 1-in-5 combats. The 10% weight delivers exactly that.

---

### F4 — Expected Deck Size at Run End

```
DeckSize(end) = StarterSize + (CombatRewards × PickupRate) + MerchantPurchases − PurgeCount
```

**F4 Variable Table:**

| Symbol | Type | Range | Description |
|---|---|---|---|
| `StarterSize` | int | 10 (constant) | Fixed per chassis |
| `CombatRewards` | int | TBD — **placeholder: 14** | Count of Normal + Elite combat nodes per run. Owned by Node Map System GDD — not yet confirmed. |
| `PickupRate` | float | 0.0–1.0 (design target: 0.65–0.75) | Fraction of reward offers accepted |
| `MerchantPurchases` | int | 0–2 typical, Scrap-gated | Player-driven variable |
| `PurgeCount` | int | 0 ≤ PurgeCount ≤ (StarterSize + CombatRewards × PickupRate + MerchantPurchases − 10) | Cards purged at Chopshop. **Design-time upper bound only** — this formula uses `PickupRate`, which is a design projection variable, not a runtime value. The actual runtime purge gate is EC7: `CurrentDeckSize − 10` is the maximum purge count at any given moment. F4 uses this bound only to ensure DeckSize(end) ≥ 10 in the projection. |

Output range: `[10, unbounded]` — minimum enforced by EC7 Chopshop floor.

There is no deck maximum — deck size is unbounded above the minimum. F4 is a design-time expected-value projection, not a runtime formula (runtime deck size is a simple card count integer).

> **⚠️ Sequencing constraint:** EA card pool sizing decisions (total card count per chassis, pool composition) **must not be finalized until the Node Map System GDD confirms `CombatRewards`.** The EA pool (75–100 cards per chassis) was sized against an expected ~21-card run endpoint. If `CombatRewards` lands significantly above 14 (larger deck) or below 14 (smaller deck), pool density assumptions must be re-verified.

**Reference projection** (assumes 14 combat nodes, 70% pickup, 1 merchant purchase, 0 purges):
```
DeckSize(end) = 10 + (14 × 0.70) + 1 − 0 = 10 + 9.8 + 1 ≈ 21 cards
```
With active purging (2–4 cards): ~17–19. Node Map node count is the primary lever for final deck size.

> **Dependency flag**: Update `CombatRewards` once the Node Map System GDD confirms per-run combat counts.

---

### F5 — Scrap Cost of Card Purge (retrofit 2026-04-23)

```
PurgeTransactionCost = IsFreeValveApplied ? 0 : GlobalPurgeCost   // GlobalPurgeCost = 30
```

Purging a card at a Chopshop costs the player a flat `GlobalPurgeCost = 30` Scrap (Scrap Economy GDD D.1 / D.4) **regardless of the card's rarity or identity**. Card System no longer authors per-card purge cost; the constant lives in the registry under the Scrap Economy GDD. Card System's role in the transaction is limited to:

1. Supplying the `CardDefinitionSO` reference being purged (for deck-mutation API).
2. Enforcing `DeckSize > 10` (EC7) as a precondition the Scrap Economy purge verb consults.

**Free valve exception:** The actual transaction cost evaluates to 0 Scrap for the first purge of each Chopshop visit when the free-valve rolled true (Scrap Economy D.8). The valve is consumed on first use — second and subsequent purges at the same visit pay the flat 30.

**Legacy:** An earlier version of this GDD specified a per-card `PurgeCost` field on `CardDefinitionSO` with rarity-tier tuning guidance. Both the field and the tuning guidance are **removed** (see Data Contract row + Tuning Knobs). No guidance for per-card purge cost remains because the value is global.

## Edge Cases

### EC1 — Exhaust + Retain Conflict

If a card has both `Exhaust` and `Retain` keywords: **Exhaust fires on play, Retain fires at end-of-turn.** These are different trigger points, so the interaction depends on player action:

- **Card is played:** `Exhaust` fires → card enters Exhausted state. `Retain` does not apply (there is nothing to retain — the card is gone).
- **Card is NOT played by end of turn:** `Retain` fires → card stays in hand. `Exhaust` does not fire (it fires on play, not on discard/retain). The card remains in hand and is available next turn.

Summary: Exhaust removes the card when it is **played**. Retain keeps it in hand when it is **not played**. The two keywords do not conflict — they govern different events.

---

### EC2 — Ethereal + Retain Conflict

`Ethereal` and `Retain` are **mutually exclusive** — a card cannot have both. Enforced at SO import as a validation error. Ethereal represents a hard "use it or lose it" constraint; Retain would completely negate its purpose. If a designer accidentally assigns both, the import pipeline rejects the asset.

---

### EC3 — Keyword Extensibility (Playtest Safety Valve)

The current keyword set (`Exhaust`, `Retain`, `Innate`, `Ethereal`) is a **baseline subject to playtest revision**. The architecture is intentionally designed so any keyword can be removed, replaced, or added in isolation:

- **Remove**: Delete the enum value, strip it from all SO assets, remove the resolution branch in Card Combat. No other keyword or card data changes.
- **Replace**: Swap the enum value and its Card Combat resolution branch. Example: if `Exhaust` is replaced with `DoubleTap` (plays the effect list twice), only the enum definition and the Card Combat resolution pipeline change — `CardDefinitionSO` structure, card pool assets, and all other keywords are untouched.
- **Add**: Add a new `CardKeyword` enum value and implement its resolution branch in Card Combat. Existing cards that don't use it are unaffected.

**No keyword change should require structural changes to `CardDefinitionSO` or card pool SO assets.** This is a hard constraint on the Card Combat implementation.

---

### EC4 — Position Requirement on a NoTarget Card

Valid combination. Example: a Maneuver card with `TargetType = NoTarget` and `PositionRequirement = BonusIfBehind`. The bonus conditional effect fires if position is met; the base effect fires regardless. No conflict — targeting and position requirements are independent fields.

---

### EC5 — Draw Attempt With Empty Deck and Empty Discard

If both deck and discard are empty and a draw is attempted (e.g., from `DrawCardsEffectSO`): the draw silently produces nothing. No error, no crash. This can occur legitimately when all non-Exhausted cards have been drawn in a long combat. The player continues their turn with whatever remains in hand.

---

### EC6 — Purging a Starter Card

Allowed. `IsStarterCard = true` marks eligibility for initial deck construction only — it carries no protection after the run begins. A player may purge any card including starter cards, subject to the 10-card minimum deck size floor.

---

### EC7 — Purge Attempt at Minimum Deck Size

If the deck is at exactly 10 cards and the player attempts a Chopshop purge: **blocked at game logic level.** The Chopshop UI must disable the purge option with an explicit message (e.g., *"Deck cannot drop below 10 cards"*). No Scrap cost is charged for a blocked attempt.

**Why 10 is the floor (not lower):** A deck smaller than 10 cards cycles too rapidly to express the chassis's card vocabulary, undermining Pillar 2 (Chassis Identity). The repetitive hand rhythm of a micro-deck also reduces the "absorb the chassis through play" feeling that is central to Pillar 1 (Vehicle as Character). This is not a tuning knob — it is a design constraint tied to the game's pillars.

---

### EC8 — Cost-0 Ethereal Card Not Played

Auto-discarded at end of turn by standard Ethereal resolution. No special handling needed. The trigger is "not played by end of turn," regardless of energy cost.

---

### EC9 — Card Upgrade Fields (Post-EA Reserved — Not Applicable in EA)

`UpgradedVersion` and `IsUpgraded` do not exist on `CardDefinitionSO` in EA. This edge case is deferred to post-EA. When the upgrade mechanic is designed, the rules for upgrade chains, copy-limit interaction, and SO import validation will be specified in the upgrade GDD. The constraint "no multi-step upgrade chains" is a design intent for the future mechanic.

---

### EC10 — Multi-Effect Card: Earlier Effect Destroys the Target

All effects in the `Effects` list resolve in sequence regardless of intermediate target state. If an earlier `DamageEffectSO` takes a subsystem Offline, subsequent effects still fire against that now-Offline slot. Card System does not short-circuit the list — Card Combat System owns resolution behavior on Offline or already-destroyed targets.

---

### EC11 — ValidSubsystemTargets Empty on an EnemySubsystem Card

An empty `ValidSubsystemTargets` array means **all slots are valid targets**. This is the default for broad Assault cards. Not an error — explicitly intended as unconstrained targeting.

---

### EC12 — Cost-0 Card Without Ethereal

Valid, but must be explicitly justified in design notes on the SO. Cost-0 non-Ethereal cards can be played for free every turn they are drawn and cycle into the deck indefinitely. Any `IsStarterCard = true` card with `EnergyCost = 0` requires explicit lead sign-off before entering the pool.

---

### EC13 — Innate + Exhaust

An Innate card appears in the opening hand of every combat for as long as the card exists in the deck (In Deck, In Hand, or In Discard states). When the Innate card is played and the Exhaust keyword removes it from the run, it enters the Exhausted state. **In all subsequent combats, the Innate property no longer fires for that card** — an Exhausted card cannot appear in any hand, opening or otherwise. This combination is a valid design pattern: a powerful one-time opening action that cannot be hoarded across combats.

---

### EC14 — Innate + Ethereal

A card with both `Innate` and `Ethereal` keywords always appears in the opening hand and follows standard Ethereal resolution: if not played by end of turn, it is auto-discarded into the Discard pile. **This combination is permitted by design** — it creates a "use it in the opening turn or lose it for this combat" pattern. Innate does not override Ethereal; Ethereal does not suppress Innate. The combination is not forbidden at SO import. Designers should use it intentionally: an Innate+Ethereal card is a guaranteed opening hand slot that demands immediate commitment.

---

### EC15 — Innate Card Already In Hand at Combat Start (Retain Interaction)

If an Innate card was Retained from the previous combat and is already In Hand at the start of the new combat, the Innate draw mechanism does **not** produce a second copy. Innate fires only when the card is in the In Deck or In Discard state at combat start. A Retained-Innate card is already where it should be; no additional draw occurs.

---

### EC16 — Innate Deck Cap Enforcement

A player's deck may contain at most 3 Innate cards (see Keywords section). At the cap, Innate-keyword cards are filtered from combat reward offers and Merchant card pools — they are not displayed to the player. If the player has 3 Innate cards and an Innate card would otherwise be added (e.g., via a theoretical future mechanic), the addition is blocked. The cap applies to the total count of Innate-keyword cards in the deck at any one time, regardless of which specific cards they are.

## Dependencies

### Systems This GDD Depends On

| System | What Card System Needs From It | When Needed |
|---|---|---|
| **Vehicle & Part System** | `IVehicleView` and `IVehicleMutator` interfaces; `SlotType` enum; `ChassisDefinitionSO` (starter deck card list); `ChassisMasteryDefinitionSO` (rarity weights) | Before any card effect can read or write vehicle state |
| **Status Effect System** | `StatusType` enum — Card System's `ApplyStatusEffectSO` references it | Before any status-applying cards can be authored |
| **Node Map System** | Combat node count per run — needed to validate F4 deck size projection | Before Node Map GDD is complete, F4 uses a placeholder |
| **Save & Persistence System** | Storage location for `IsFreeValveApplied: bool` on Chopshop node runtime state; deck/discard/exhausted serialization contract | Before Chopshop seeding behavior is implemented |

### Systems That Depend On This GDD

| System | What They Need From Card System | Notes |
|---|---|---|
| **Card Combat System** | `CardDefinitionSO` contract; `CardEffectSO` hierarchy; `EffectConditionSO` hierarchy; keyword resolution rules from EC3 | Card Combat cannot be implemented without this GDD locked |
| **Loot & Reward System** | `ICardRewardGenerator.Generate(chassis, mastery, rarePityCounter, drawCount)` → `CardDraft[]`; `CardDraft` tooltip schema (see below) | Retrofit 2026-04-23: L&R consumes drafts via interface, not SO reads |
| **Save & Persistence System** | `CardId` stable key format; four card location states (EC5) | Serialization schema depends on this |
| **Scrap Economy System** | `DeckSize > 10` precondition on purge (EC7); `MerchantPrice` for purchase pricing | Purge cost is flat `GlobalPurgeCost = 30`; not a per-card field |
| **Chassis Identity System** | `ChassisPool` field for reward pool filtering | Pool filtering is data-only; no logic dependency |

### ICardRewardGenerator Contract (retrofit 2026-04-23 — L&R interface)

```csharp
public interface ICardRewardGenerator
{
    // Canonical card-offer generator. Called by Loot & Reward GDD Section C.3
    // step 6 (card draws) with the run's current pity counter and current deck
    // composition (for copy-limit filtering). Returns exactly `drawCount`
    // drafts, or empty[] ONLY when the chassis-filtered pool is empty
    // (Loot & Reward EC-LR6 handles this via PityScrapAward(40)). Returns
    // never partial — always Length == drawCount or Length == 0.
    //
    // Determinism: seeded from the caller-provided RNG — no global RNG reads.
    // Card System MUST NOT mutate rarePityCounter; L&R owns counter state
    // (writes & persists in LootStateDTO per ADR-0006 Decision §"Pity counter
    // authority").
    CardDraft[] Generate(
        ChassisType   chassis,
        int           mastery,
        int           rarePityCounter,
        int           drawCount,
        IReadOnlyList<string> currentDeckCardIds,
        System.Random rng);
}

public sealed class CardDraft
{
    public string     CardId;          // stable Addressables key
    public string     DisplayName;
    public string     RulesText;       // pre-templated with {damage}, {cost}, {X} resolved
    public CardFamily Family;
    public CardRarity Rarity;
    public int        EnergyCost;
    public string     CardArtKey;      // for L&R compare-panel thumbnail
    public string[]   KeywordBadges;   // ["Exhaust", "Retain", …] — resolved strings
    public int?       MerchantPrice;   // null at reward offers; populated at Merchant
    public int        SelectionHash;   // deterministic hash of draft for telemetry
}
```

**Tooltip schema contract:** `CardDraft.RulesText` is rendered verbatim by Post-Combat Flow UI and Merchant UI; no further templating is required at the presentation layer. `KeywordBadges` is the exhaustive list of keyword chips to display; empty array = no badges. `MerchantPrice` populates at Merchant offers only (null at post-combat rewards and Treasure).

### ADRs Referenced

| ADR | Dependency |
|---|---|
| **ADR-0001** (Visual Vehicle Part System) | `SlotType` enum post-Armor-retrofit (`Weapon`, `Engine`, `Mobility`, `Frame` — Armor removed; see V&P Armor-layer model 2026-04-21) used in `ValidSubsystemTargets` |

## Tuning Knobs

| Knob | Location | Current Value | Safe Range | Gameplay Effect |
|---|---|---|---|---|
| Starter deck size | `ChassisDefinitionSO` | 10 cards | 8–12 | Smaller = more variance early; larger = more consistent opening turns |
| Minimum deck size | Game logic constant | 10 cards | **Not a tuning knob** — see EC7. The 10-card floor is a design constraint tied to Pillars 1 and 2, not an adjustable parameter. Removing this row from tuning consideration. |
| Cards offered per reward | Game logic constant | 2 | 2–3 | 3 offers increases meaningful choice but slows reward pacing |
| Free purge valve rate | Node generation seed | 33% per Chopshop visit | 20–50% | Lower = tighter Scrap economy, less safety net; higher = players can fix mistakes more freely |
| Post-combat pickup rate (design target) | No asset — informs card pool design | 0.65–0.75 | 0.5–0.85 | If actual pickup rate exceeds 0.85, cards may feel too strong to skip; below 0.5, rewards feel weak |
| Rarity weights by mastery tier | `ChassisMasteryDefinitionSO` | See F2 | Common: 50–90; Uncommon: 10–40; Rare: 1–15 (per tier, must sum to 100) | Controls Rare cadence and power curve steepness across a mastery grind |
| EA card pool size per chassis | Card SO assets | 75–100 | 60–120 | Smaller pools increase repeat-seen rate; larger pools dilute specific card hunting |
| ~~PurgeCost by rarity~~ | ~~Per-card `CardDefinitionSO`~~ | — | — | **REMOVED 2026-04-23.** Flat `GlobalPurgeCost = 30` owned by Scrap Economy GDD D.1 / registry. No per-card tuning. |
| BaseDamage range (EA cards) | Per-card `CardDefinitionSO` | 1–12 | 1–15 | Upper bound gates one-shot potential; raise only with corresponding subsystem HP increases |
| RestoreArmorEffectSO.Amount by rarity | Per-card `CardDefinitionSO` | Common 1–2, Uncommon 2–4, Rare 3–5 | Common 1–3, Uncommon 2–5, Rare 3–7 | Governs Plate Up–class cards. Values above guidance trivialize the "Armor peels to reveal Frame" tension central to the locked Armor model (stress-test R-1). Tune against V&P `MaxArmor` range — Armor-restore cards should not make a mid-combat full Armor refill trivially cheap. Hard cap at `MaxArmor`; overflow wasted. |
| Subsystem-strike damage (BypassPlating) | Per-card `DamageEffectSO` | Typically +20–40% above same-cost non-BypassPlating Precision card | — | Subsystem-strike trades Plating penetration for raw damage ceiling — tune so a Plating-heavy target still feels vulnerable to subsystem-strike without making subsystem-strike the default answer to every non-Frame problem. Cross-check against V&P Plating values and Card Combat F-CC1 non-Frame path. |
| EnergyCost range | Per-card `CardDefinitionSO` | 0–3 | 0–4 | Cost-4 cards risk feeling unplayable; cost-0 without Ethereal must be reviewed carefully (EC12) |

## Visual/Audio Requirements

### Card Visual Requirements

| Element | Requirement |
|---|---|
| Card face art | Loaded via Addressables key stored in `CardArtKey`. Must be a Sprite Lit asset (URP Sprite Lit shader) to support plating/damage overlay effects from ADR-0001. |
| Rarity indicator | Distinct visual treatment per rarity tier (Common / Uncommon / Rare) — border colour, glow, or material variant. Exact treatment owned by Art Direction; Card System provides the `Rarity` field as the data source. |
| Family colour coding | Each of the five families has a distinct colour identity used on card frames and keyword badges. Colour palette owned by Art Direction; Card System provides `Family` as the data source. |
| Keyword badges | Each keyword (`Exhaust`, `Retain`, `Innate`, `Ethereal`) has a distinct icon badge displayed on the card face. Per EC3, badge assets must be independently swappable — adding or removing a keyword requires only adding or removing its badge asset, not redesigning the card frame. |
| Description token rendering | `{param}` tokens in `Description` must resolve to their current numeric values at display time. Displayed text must stay in sync when balance tuning changes values. |
| Upgraded card treatment | **Post-EA reserved.** `IsUpgraded` field does not exist in EA. When the upgrade mechanic is added post-EA, upgraded cards will receive a distinct visual treatment (e.g., shimmer border, upgraded badge) — exact treatment to be defined in the upgrade GDD. |
| Exhausted card | Cards leaving the deck via Exhaust animate out distinctly from normal discard (e.g., burn/disintegrate VFX). Owned by VFX/Art; Card System provides the `Exhaust` keyword as the trigger signal. |

### Audio Requirements

| Event | Requirement |
|---|---|
| Card played | One-shot SFX triggered per card play. Varies by family — Precision cards have a different audio identity from Assault cards. Exact palette owned by Audio Direction; Card System provides `Family` as the data source for SFX selection. |
| Card drawn | Subtle draw SFX per card. Must not fatigue over a full combat (low-key, non-repetitive). |
| Card discarded (normal) | Minimal or silent. Discard is a passive state transition, not a player action. |
| Card exhausted | Distinct SFX + VFX combo. Should feel final and impactful — the card is gone for the run. |
| Keyword resolution | `Innate` cards entering the opening hand may have a subtle chime. `Ethereal` auto-discard has a brief dissolve sound. |

## UI Requirements

### Card Display

| Element | Requirement |
|---|---|
| Card tooltip | Hovering a card in hand displays: `DisplayName`, `EnergyCost`, `Family`, `Rarity`, full `Description` with resolved `{param}` tokens, `FlavorText` (if present), and all active keyword badges with one-line explanations. Tooltip must be readable over the combat background without obscuring the hand. |
| Hand layout | Cards fan across the bottom of the screen. Active hover raises and enlarges the hovered card. Maximum visible hand size before overlap compression: 7 cards. At 8+ cards the hand compresses — no card is hidden, but overlap increases. |
| Playable vs. unplayable state | Cards with `EnergyCost` exceeding current energy are visually dimmed. `PositionRequirement = Requires*` cards that cannot currently be played are dimmed with a positional lock indicator. |
| Targeting prompt | When a card with `TargetType = EnemySubsystem` is selected, valid target slots highlight on the enemy vehicle. `ValidSubsystemTargets` drives which slots are highlighted. Invalid slots are visually suppressed. |
| Card reward screen | Presents exactly 2 cards side by side. Each card shows full face art, `DisplayName`, `EnergyCost`, `Family`, `Rarity`, and `Description` (tokens resolved to baseline SO values — no combat context required). **Model B interaction:** three explicit clickable actions — Card A, Card B, Skip. The player must press one of these three buttons; there is no passive close/exit. Skipping is a deliberate action, not a screen dismissal. **Misclick protection on Rare offers:** if the player presses Skip when at least one offered card is Rare, a one-step confirmation prompt appears ("Skip a Rare card? [Confirm / Go Back]"). No confirmation required for Common/Uncommon skips. |
| Chopshop purge UI | Lists all non-exhausted cards in the run (draw pile + discard pile combined — outside of combat, this distinction is meaningless to the player). Purge button disabled when deck is at minimum size (EC7), with a clear inline message. Flat `GlobalPurgeCost = 30` is displayed once at the top of the panel (not per card); the free-valve badge replaces the cost display on the first purge of the visit when the valve is active. |
| Deck inspection | Player can open a full deck view at any time outside of combat showing all cards in deck, discard, and exhausted lists as separate tabs. |

### Keyword State Visualization (Live Hand Layer)

Card badges and glossary tooltips are static. The live hand layer communicates keyword state *while a card is in hand or resolving*. Each keyword requires a distinct visual treatment on the card face at runtime:

| Keyword | State to Communicate | Required Visual Treatment |
|---|---|---|
| **Retain** | This card will stay in hand at end of turn (not discarded) | A persistent highlight or border pulse on the card in hand when the end-of-turn phase begins. Players must be able to see at a glance which cards are Retained before they commit to ending their turn. |
| **Ethereal** | This card will be auto-discarded at end of turn if not played | A visual degradation effect (e.g., fading opacity, dissolve pulse) applied to Ethereal cards in hand when the end-of-turn phase begins — communicating urgency to play now. |
| **Innate** | This card entered the opening hand via the Innate mechanism (not a normal draw) | A brief distinct entry animation at combat start to distinguish Innate-draw cards from normally-drawn cards. After the opening draw, Innate cards look identical to other hand cards. |
| **Exhaust** | This card will leave the run permanently when played | A distinct "leave trail" or dissolve exit animation when an Exhaust card is played — visually separating it from the normal discard animation. The card must not appear to go to the discard pile. |

**Implementation note:** These are behavioral specifications, not final art directions. The exact animation, particle, or color treatment for each state is owned by Art Direction and VFX. What this spec requires is that each keyword has a visually distinct live-state indicator that a player can read without reading tooltip text.

**Gamepad parity:** All four keyword state indicators must be visible on controller without requiring button input — they are passive visual states, not interactive prompts.

---

### Keyword Glossary

All keyword badges must be tappable/hoverable to display a one-line rules summary inline. Glossary text is authored separately from `CardDefinitionSO` — keyword descriptions do not live on individual card assets. Per EC3, adding a new keyword requires adding one glossary entry; no other UI changes.

### Gamepad Card Tooltip Requirement

Card tooltips must be accessible via a dedicated non-hover input on gamepad. On any card-displaying UI (combat hand, reward screen, Chopshop list, deck inspection), selecting a card on gamepad must trigger the same full tooltip display as hovering with a mouse. No card tooltip may be mouse-hover-only. The specific button mapping is owned by the UX Spec (`design/ux/combat-screen.md`); this GDD provides the behavioral contract only.

## Acceptance Criteria

### Data Contract

- [ ] A `CardDefinitionSO` asset can be created via the Unity Inspector (Assets > Create menu) and saved with the following fields populated without C# code: `CardId`, `DisplayName`, `Description`, `Family`, `Rarity`, `ChassisPool`, `IsStarterCard`, `EnergyCost`, `TargetType`, `Keywords`, `Effects` (at least one). SO must serialize and reload without errors. (`PurgeCost` field removed 2026-04-23 — see F5 / Scrap Economy D.4.)
- [ ] `CardId` format `[chassis]_[family]_[sequence]` is enforced at SO import — malformed IDs are rejected with a descriptive error
- [ ] SO import rejects any card with both `Ethereal` and `Retain` keywords set (EC2)
- [ ] SO import rejects rarity weight sets that do not sum to 100 on `ChassisMasteryDefinitionSO`
- [ ] SO import rejects any card where `EnergyCost < 0` or `PositionBonus < 0`
- [ ] SO import strips any legacy `PurgeCost` field value from a re-saved SO and logs a one-time authoring warning; no import failure (retrofit-compat path)
- [ ] SO import rejects any card where `MerchantPrice > 0` AND `MerchantPrice == 30` (degenerate parity against `GlobalPurgeCost`; OQ7 resolution per Scrap Economy D.4). Cards with `MerchantPrice = 0` (unset) are excluded from all Merchant pools at runtime; they pass import but cannot appear for purchase.
- [ ] SO import rejects any Control-family card (`Family == Control`) that does not contain at least one `DamageEffectSO` with `Amount >= 1` in its `Effects` list
- [ ] SO import rejects any card containing a `DamageEffectSO` where `BaseDamage ≠ DamageEffectSO.Amount` or `BaseDamage < 1`
- [ ] SO import rejects any `RestoreArmorEffectSO` where `Amount < 1`
- [ ] SO import rejects any card where a `DamageEffectSO` has `BypassPlating == true` AND `ValidSubsystemTargets` includes `Frame`. (Subsystem-strike cannot target Frame — Frame is protected by Armor, not Plating; the BypassPlating contract is non-Frame-only.)
- [ ] SO import rejects any card where a `DamageEffectSO` has `BypassPlating == true` AND `TargetType == AllEnemySubsystems`. (Subsystem-strike is single-target by contract — broad-target subsystem-strike would defeat the Armor-layer design.)
- [ ] SO import rejects any card where a `DamageEffectSO` has `BypassPlating == true` AND `ValidSubsystemTargets` is empty. (Empty = all-slots which includes Frame, violating the Frame-exclusion rule above.)
- [ ] `Description` tokens (`{param}`) resolve to correct numeric values in the following display contexts: (a) combat hand tooltip, (b) card reward screen, (c) Merchant card display, (d) Chopshop purge list, (e) deck inspection view. When the corresponding SO field value is changed and the SO re-saved, all five contexts reflect the updated value without requiring a scene reload.

### Deck Rules

- [ ] Starter deck for each chassis contains exactly 10 cards, all with `IsStarterCard = true` and matching `ChassisPool`
- [ ] Starter deck for Scout contains at minimum 3 Precision cards and 2 Maneuver cards; starter deck for Assault contains at minimum 3 Assault family cards (of which at minimum 2 are Broad Assault) and 2 Precision cards; starter deck for Assault contains at most 1 Maneuver card
- [ ] Deck cannot be purged below 10 cards — Chopshop purge button is disabled at minimum with an inline message reading "Deck cannot drop below 10 cards" adjacent to the disabled button (EC7)
- [ ] Card reward screen presents exactly 2 cards and exactly 3 interactive elements: [Card A], [Card B], [Skip]. No other dismissal method (Escape key, clicking outside) closes the screen.
- [ ] Selecting Card A or Card B immediately adds that card to the deck and closes the reward screen
- [ ] Pressing Skip when at least one offered card has `Rarity == Rare` triggers a confirmation step: "Skip a Rare card? [Confirm / Go Back]". Pressing Skip when no Rare is offered closes the reward screen without confirmation.
- [ ] A card with `Keywords` containing `Exhaust` and `Retain`: when **played**, enters Exhausted state. Retain does not fire on play — Exhaust removes the card from the run immediately. (EC1 — played case)
- [ ] A card with `Keywords` containing `Exhaust` and `Retain` that is **not played** by end of turn: Retain fires, card stays in hand. Exhaust does not fire (Exhaust fires on play only). The card remains in hand next turn and will enter Exhausted state if subsequently played. (EC1 — unplayed case)
- [ ] A card with `Keywords` containing `Retain` and no `Exhaust`: at end of turn, if the card was not played, it remains in hand. Hand count does not decrease for this card.
- [ ] An Innate card that is already In Hand at combat start (via Retain from prior combat) does not appear twice in the opening hand — Innate draw is skipped for cards already In Hand. (EC15)

### Card Pool Integrity

- [ ] Given a Scout chassis run: reward offers and Merchant card pools contain only cards where `ChassisPool == Scout`. Given an Assault chassis run: the same contexts contain only cards where `ChassisPool == Assault`.
- [ ] Rarity draw weights are verified by simulated draws per mastery tier using `new System.Random(seed: 42)`. Sample size and pass criteria by tier: mastery 1–3: **100,000 draws**, Rare rate **0.7%–1.3%** (±0.3pp — tighter tolerance required because 1% Rare rate makes ±1pp equivalent to ±100% relative error); mastery 4–6: 10,000 draws, Rare rate 4.4%–5.6%; mastery 7–10: 10,000 draws, Rare rate 9.4%–10.6%. **Test must produce identical results on every run (deterministic — seed 42 is fixed).**
- [ ] Pity counter — happy path: in a simulated run using `new System.Random(seed: 42)` where 8 consecutive combat reward offers contain no Rare, the 9th offer includes exactly one card drawn from the Rare pool. Counter resets on any Rare-containing offer (including offers the player skips). Test must produce identical results on every run.
- [ ] Pity counter — empty Rare pool: in a simulated run where the Rare pool is empty and the pity counter reaches 9, the offer awards Scrap compensation (no Rare card offered) and the counter resets to 0. The next 8 offers (if also Rare-free) again trigger pity on the 9th, again awarding Scrap compensation. Test must be deterministic (seed: 42).
- [ ] Pity counter — F2 fallback independence: when pity fires on an empty Rare pool, the system awards Scrap compensation and does NOT degrade to draw an Uncommon card instead. The F2 tier-degradation path is not triggered by pity draws.
- [ ] Primary-family bias: in a simulated Scout run at Mastery 1–3, 10,000 simulated reward offers using `new System.Random(seed: 42)` must show that Slot 1 contains a Precision or Maneuver card in ≥98% of offers where the primary-family pool is non-empty. Slot 2 draws from the full chassis pool (F2 weights). When primary-family pool is fully exhausted (all at copy limit), both slots draw from full chassis pool. Test must be deterministic.
- [ ] Free purge valve: entering a Chopshop node with `runSeed + nodeIndex` seeded via `new System.Random(seed: runSeed + nodeIndex)` produces a deterministic `IsFreeValveApplied` value. Reloading the game mid-session and re-entering the same node produces the same `IsFreeValveApplied` value (reads from persisted save, does not re-roll). Approximately 33% of 10,000 simulated Chopshop entries yield `IsFreeValveApplied = true` (pass range: 30%–36%).
- [ ] Copy limits enforced: a 4th copy of a Common or Uncommon card cannot be added to the deck; a 2nd copy of a Rare cannot be added. Reward and Merchant UIs filter out cards at copy limit for the current run.

### Keyword Extensibility (EC3) — Code Review Gate

> **Note:** The following two criteria verify a structural property of the codebase, not a runtime behavior. They belong in a code review checklist, not an automated test suite. A QA tester verifying EC3 compliance requires access to a Git diff and knowledge of the full file list.

- [ ] **[Code review]** Removing the `Exhaust` keyword requires changes only to: the `CardKeyword` enum, the Card Combat resolution branch, and SO assets — zero structural changes to `CardDefinitionSO` or unrelated card SO assets.
- [ ] **[Code review]** Adding a new keyword requires changes only to: the `CardKeyword` enum, a new Card Combat resolution branch, and a new keyword glossary entry — zero changes to existing card SO assets.
- [ ] **[Runtime proxy]** A `CardDefinitionSO` with a `Keywords` flag value not recognized by the current Card Combat resolution branch does not cause a compile error, a Unity console error, or a missing-component warning at runtime — unknown keywords are silently ignored.

### Card States

- [ ] A card drawn from the deck moves to In Hand state; deck count decreases by 1 and hand count increases by 1
- [ ] A played non-Exhaust card moves from In Hand to In Discard; hand count decreases, discard count increases
- [ ] At end of turn, non-Retain cards in hand move to In Discard
- [ ] When a draw is attempted with an empty deck and non-empty discard: the discard reshuffles into the deck instantaneously; the draw proceeds from the new deck
- [ ] Exhausted cards do not re-enter the deck or discard pile for the remainder of the run
- [ ] Draw attempt with empty deck and empty discard produces no card and no error (EC5)
- [ ] An Innate card appears in the opening hand at the start of combat 1 and again at the start of combat 2 in the same run (EC13 scope: Innate applies every combat while the card exists in the run)
- [ ] An Innate card that is played and Exhausted in combat 1 does not appear in the opening hand in combat 2 (EC13: Exhausted cards cannot be Innate-drawn)

### Formula Verification

- [ ] F1: `DamageOutput` with `PositionConditionMet = 1` equals `BaseDamage + PositionBonus`; with `PositionConditionMet = 0` equals `BaseDamage` only. Boundary: `BaseDamage = 1, PositionBonus = 0` → output = 1 (minimum). `BaseDamage = 12, PositionBonus = 8` → output = 20 (EA ceiling).
- [ ] F3: Simulated 2-card offers at all three mastery tiers using `new System.Random(seed: 42)`. Pass ranges: mastery 1–3: ≥1 Rare in **1.4%–2.6%** of 100,000 offers; mastery 4–6: 8.7%–10.8% of 10,000 offers; mastery 7–10: 17.0%–21.0% of 10,000 offers. Test must produce identical results on every run.
- [ ] F4: In a simulated run with 14 combat nodes, 70% pickup rate, 1 merchant purchase, and 0 purges, the resulting deck contains 20–22 cards.
- [ ] Description token indexed form: a card with two `DamageEffectSO` entries and `{damage.1}` / `{damage.2}` in its `Description` renders both values correctly in all five display contexts (combat tooltip, reward screen, Merchant, Chopshop, deck inspection). Neither token renders as `?`.

### EC Coverage

- [ ] A card with `TargetType == EnemySubsystem` and empty `ValidSubsystemTargets` array highlights all enemy slots in the targeting UI — no slot is suppressed (EC11)
- [ ] A card with `IsStarterCard = true` can be selected for purge at the Chopshop when deck size > 10 — `IsStarterCard` does not prevent purging (EC6)
- [ ] A multi-effect card where the first `DamageEffectSO` takes a target subsystem to Offline still fires all subsequent effects against that now-Offline slot — resolution does not halt at the state transition (EC10)
- [ ] A deck containing 3 Innate cards: combat reward offers and Merchant card pools do not show Innate-keyword cards (filtered out due to Innate cap). A deck containing 2 Innate cards: Innate-keyword cards appear normally in offers. (EC16)
- [ ] EC14: A card with both `Innate` and `Ethereal` keywords always appears in the opening hand (Innate fires) and is auto-discarded at end of turn if not played (Ethereal fires). The two keywords do not suppress each other. This combination is not rejected at SO import.
- [ ] A card with `PositionRequirement == RequiresBehind` cannot be played when the player's position is `Ahead` — the card is visually dimmed and the play action is blocked at the game logic level. A card with `PositionRequirement == RequiresAhead` cannot be played when `Behind`. Cards with `BonusIf*` can always be played regardless of position.

## Open Questions

| # | Question | Owner | Needed By |
|---|---|---|---|
| OQ1 | How many combat nodes per run? F4 uses a placeholder of 14 — must be confirmed by Node Map System GDD. | Node Map System GDD | Before balance pass |
| OQ2 | **RESOLVED** — `UpgradedVersion` and `IsUpgraded` fields have been **removed from the EA `CardDefinitionSO` data contract**. These fields do not exist in EA (see Data Contract section). The card upgrade mechanic is post-EA scope; when a card upgrade GDD is authored post-EA, the fields will be added at that time with full specification. No designer should author or reference these fields for EA card SOs. | — | — |
| OQ3 | `Legendary` rarity: reserved for post-EA. The enum value exists but no Legendary cards are in the EA pool. Confirm this is permanent exclusion (not just deferred weight) to avoid ambiguity in the rarity enum and pool distribution logic. | Game Designer | Before card pool authoring begins |
| OQ4 | Merchant pool definition: does the Merchant offer cards from the full chassis pool or a curated subset? Must differ from combat reward offers to be economically rational — see Deck Composition Rules note on Merchant value proposition. | Loot & Reward System GDD | Before Merchant node design |
| OQ5 | `PurgeCost` for cost-0 cards: the rarity-tier guideline (Common 5–15) may undervalue high-throughput cost-0 cards. Cost-0 non-Ethereal cards should carry elevated PurgeCost relative to their rarity tier. Note: PurgeCost = 0 is now forbidden at SO import (`PurgeCost >= 1` enforced). The open question is whether a custom elevated floor (e.g., 2× rarity-tier minimum) should be defined in the GDD, or left to per-card lead sign-off (EC12). | Economy Designer | Before Chopshop implementation |
| OQ6 | **RESOLVED** — Copy limits confirmed: up to 3 copies of any Common or Uncommon card; max 1 copy of any Rare. Offer generation draws without replacement within a single 2-card offer. Pool exhaustion fallback added to F2. | — | — |
| OQ7 | Merchant card pricing: **PARTIALLY RESOLVED** — `MerchantPrice: int` field added to `CardDefinitionSO` (default 0 = unset). The field exists; the pricing formula (what value each card's `MerchantPrice` should be) is owned by the Scrap Economy GDD. Parity with `PurgeCost` remains forbidden. | Scrap Economy System GDD | Before Merchant node design |
| OQ8 | **RESOLVED** — Pity mechanic confirmed: run pity counter. If no Rare offered in 8 consecutive combat rewards, the 9th offer is guaranteed to include one Rare. Counter resets on any Rare offer. Does not persist across runs. See Rarity section. | — | — |
| OQ9 | **Subsystem-strike + Redirected interaction.** Forward dependency from Status Effects EC-S2. When a vehicle with an active `Redirected` status plays a subsystem-strike card (`BypassPlating = true`) and Redirected's re-roll selects `Frame` as the target, the card's targeting contract says "cannot target Frame" but Redirected has overridden the declared target. Options: **(a)** Redirected re-rolls again, excluding Frame from the pool, until a non-Frame slot is selected (preserves subsystem-strike contract; skews the Redirected RNG). **(b)** Attack is consumed with zero effect — the player loses the card's damage entirely (punishes subsystem-strike + Redirected interaction hard; may feel-bad). **(c)** Attack lands on Frame and bypasses Armor ("Puncture through to Frame" flavor — subsystem-strike becomes armor-bypass when Frame-redirected; this would require a second damage-path carve-out in Card Combat F-CC1). Must resolve before any Puncture-class card SO is authored. | Game Designer + Card Combat GDD | Before Puncture SO authoring |

---

## Armor Retrofit Closure Notes (2026-04-21)

This GDD was amended as part of the Armor-as-layer model change (armor stress-test memo `design/notes/armor-model-stress-test.md`, decisions locked 2026-04-21). The following changes and closures apply:

- **SlotType enum** — `ValidSubsystemTargets` field description and ADR-0001 Dependencies row updated to the post-retrofit 4-slot model `{Weapon, Engine, Mobility, Frame}`. Armor is no longer a slot; it is a vehicle-level stat owned by V&P (`CurrentArmor`/`MaxArmor`).
- **Token binding table** — added `{armor}` → `RestoreArmorEffectSO.Amount`.
- **`DamageEffectSO`** — added `BypassPlating (bool)` field for the subsystem-strike archetype. Default `false`. When `true`, Card Combat F-CC1 step 2 is skipped on the non-Frame target (Plating not consumed).
- **`RestoreArmorEffectSO` (new)** — vehicle-level Armor-restore effect. Calls `IVehicleMutator.AddArmor(int)` (V&P EC-VP20 contract). Hard-capped at `MaxArmor`; overflow wasted. Target = Self only. Cannot reuse `RestorePlatingEffectSO` (T-6 from stress-test).
- **Precision family row** — updated to note "strips Plating from a chosen non-Frame slot" and names Precision as the home for the subsystem-strike archetype (Puncture).
- **Repair family row** — added `RestoreArmorEffectSO (self)` as a supported effect type.
- **Common rarity example** — added "Restore 3 Armor" alongside existing Plating example.
- **Dependencies → Card Combat row** — F-CC1 fork, BypassPlating flag routing, and DOT exception semantics now documented.
- **Dependencies → Vehicle & Part row** — `IVehicleMutator.AddArmor(int)` contract, `CurrentArmor`/`MaxArmor` read path, and 4-slot model referenced.
- **Tuning Knobs** — two new guidance rows: `RestoreArmorEffectSO.Amount` by rarity, and subsystem-strike damage cross-check.
- **Acceptance Criteria — Data Contract** — four new SO import rules: `RestoreArmorEffectSO.Amount >= 1`; `BypassPlating = true` cannot include Frame in `ValidSubsystemTargets`; `BypassPlating = true` cannot be `AllEnemySubsystems`; `BypassPlating = true` cannot have empty `ValidSubsystemTargets`.
- **OQ9 (new, open)** — subsystem-strike + Redirected Frame-re-roll edge case. Three options (a/b/c) on the table; resolution deferred to just before Puncture card SO authoring.

Sections not amended by this retrofit: Overview, Player Fantasy, Card Data Contract (except `ValidSubsystemTargets`), Card Families (except Precision/Repair), Rarity (except one example), Deck Composition Rules, States and Transitions, Formulas F1–F5, EC1–EC16, Visual/Audio, UI Requirements, Dependencies → Status Effects/Chassis Identity/Loot & Reward/Scrap Economy/Save & Persistence rows.

**Card-level retrofit scope (NOT owned here — flows to individual `CardDefinitionSO` assets):**

- Authoring a Puncture card (Precision Uncommon, `BypassPlating = true`, `ValidSubsystemTargets` = `{Weapon, Engine, Mobility}`, Frame excluded).
- Authoring a Plate Up card (Repair Common, `RestoreArmorEffectSO.Amount = 3` or similar).
- Deprecating / removing / relabeling the legacy "Armor Piercer" card SO if one exists. The new card is called **Puncture**.

**Forward dependencies opened by this retrofit (not owned here):**

- Card Combat GDD owns F-CC1 step-2 skip logic for `BypassPlating = true` (already authored in the Card Combat retrofit, 2026-04-21).
- V&P GDD owns `IVehicleMutator.AddArmor(int)` signature and the cap-at-MaxArmor clamp (already authored in the V&P retrofit, 2026-04-21).
- Status Effects GDD owns the Redirected 4-slot pool including Frame (already authored in the Status Effects retrofit, 2026-04-21); the open interaction with subsystem-strike is OQ9 here.
- Game Designer + Card Combat GDD must resolve OQ9 before Puncture SO authoring begins.
