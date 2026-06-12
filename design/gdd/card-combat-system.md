# Card Combat System

> **Status**: In Design
> **Author**: Bertan Berkol + Claude Code agents
> **Last Updated**: 2026-04-21
> **Implements Pillar**: Pillar 3 (Read to Win), Pillar 1 (Vehicle as Character)

## Overview

The Card Combat System is the turn-by-turn orchestrator of a single combat encounter in Wasteland Run. It owns the combat loop (player turn → enemy turn), the card play pipeline (draw, energy, target, resolve), and the resolution order for status effects, damage, and position. Card Combat is split into three named sub-models — **`CombatLoop`** (the turn orchestrator and event emitter), **`SubsystemState`** (the per-slot HP/plating/status state, which lives on the Vehicle POCO — not inside Combat), and **`PositionState`** (a small component on each Vehicle tracking Behind/Ahead). This split exists because every other combat-adjacent system (Status Effects, Vehicle & Part, Enemy, Combat HUD) needs to read or react to combat state; collapsing everything into one class would create a God Object that every system reaches into. Card Combat does not author cards (Card System does), does not define status rules (Status Effects does), and does not hold subsystem HP (Vehicle & Part does) — it *drives* all three through the turn loop and resolves their outputs into combat effects. The player experience of Card Combat is the moment-to-moment heart of the game: a limited hand, a few energy points, a readable enemy vehicle with five distinct subsystem states, and a turn-clock that rewards playing the *right* card over the *strongest* card. Every other system in Wasteland Run ultimately exists to make this loop richer.

## Player Fantasy

Card Combat should feel like **driving, not deckbuilding**. The player sits in a small cabin looking out at an enemy vehicle — five subsystems, each visibly damaged or intact — and decides, one turn at a time, where to aim. The fantasy is **Slay the Spire's readability layered over FTL's subsystem triage**: every number the player needs to decide their turn is visible on the board, and the core question recurring every turn is not "what's the optimal combo?" but "**which part of this vehicle do I break, and which part of mine do I protect?**" A hand of four cards and three energy is a small, honest budget — the player commits, resolves, watches the enemy telegraph, and commits again. When a Scout player strips the enemy's Weapon slot to Offline and watches the enemy lose its highest-damage card line for the rest of combat, the pleasure is *surgical*, not lucky. When an Assault player rams an already-weakened Frame and ends the combat in one swing, the pleasure is *decisive*, not explosive.

Three fantasy beats define success:

1. **Readability** — a player who has never seen this enemy should be able to beat it on first encounter by reading the board. Nothing important happens off-screen.
2. **Subsystem tension** — every turn presents a meaningful choice about *which* subsystem to attack or repair, and the wrong choice is punished within one or two turns.
3. **Time pressure without panic** — an Enrage mechanic enforces kill pace, but it escalates slowly enough that the player always sees the cliff before they fall off it.

What Card Combat is *not*: a stat-stacking simulator, an RNG gauntlet, or an engine-builder. The player is a driver with a wrench and a shotgun, not a mage with a spell list.

## Detailed Design

### Core Rules

**R1. Three Sub-Models (C1 Scoping Compliance)**

Card Combat is split into three named sub-models. No other system may collapse these into a single entity.

| Sub-Model | Ownership | Responsibility |
|---|---|---|
| **`CombatLoop`** | Card Combat System (this GDD) | Turn orchestrator. Owns turn counter, phase state, current-actor cursor, and the event bus. Emits events; never stores subsystem HP, card effects, or status rules. |
| **`SubsystemState`** | Vehicle & Part System GDD (lives on `Vehicle` POCO as the `Slots[]` array) | Per-slot HP, plating stacks, status instances, damage state. Card Combat reads via `IVehicleView`, writes via `IVehicleMutator`. |
| **`PositionState`** | Card Combat System (this GDD) | Small named component on each `Vehicle`. Holds a single `Position: { Behind, Ahead }` value. Mutated only by `ShiftPositionEffectSO`. |

`CombatLoop` does **not** contain `SubsystemState` or `PositionState` — it holds references to the two combatant `Vehicle` POCOs and reads state through their interfaces. This prevents the God Object risk flagged in systems-index scoping note C1.

**R2. Combat Encounter Lifecycle**

A combat encounter progresses through five phases:

| Phase | Trigger | Actions |
|---|---|---|
| **Setup** | Combat scene start | `CombatLoop` instantiates, registers both `Vehicle` references, draws both opening hands (including Innate — see Card System Keywords), seeds `System.Random` from `runSeed + combatNodeIndex`, sets `TurnCount = 0`, emits `CombatStarted`. |
| **PlayerTurn** | Setup completes OR EnemyTurn ends | Increment `TurnCount`. Reset player energy to `MaxEnergy` (default 3). Draw cards until hand size reaches `HandDrawTarget` (default 4), applying Retain from last turn. Emit `PlayerTurnStarted`. Wait for player `EndTurn` input. |
| **PlayerResolve** | Player clicks End Turn | Apply end-of-turn effects on player (Ethereal auto-discard, non-Retain discard). Tick player-applied statuses (fire-before-tick per Status Effects R3). Emit `PlayerTurnEnded`. |
| **EnemyTurn** | PlayerResolve completes | Execute enemy intent (selected at the *end of the previous enemy turn* and telegraphed to player — see R4). Resolve damage/status against player. Tick enemy-applied statuses (fire-before-tick). Select and telegraph next enemy intent. Emit `EnemyTurnEnded`. |
| **Resolution** | Either combatant reaches `IsDead = true` (Frame Offline — V&P R3) | Stop accepting input. Emit `CombatEnded` with winner/loser. Hand off to Loot & Reward (if player wins) or Run-End flow (if player loses). |

**Turn order**: Player always acts first each round. Player sees the enemy's telegraphed intent before committing cards. This choice is locked to maximize readability (Pillar 3) — it is not a tuning knob.

> **Ambush exception (see R15)**: The `Ambush` encounter type inverts the first-turn actor — enemy resolves its pre-selected intent during Setup before the first `PlayerTurn`. The player still reads the intent telegraph before committing cards (Pillar 3 preserved via node-map preview — see R15). This is the only exception; within any ongoing combat, turn order remains Player → Enemy.

**R3. Card Play Pipeline**

When the player plays a card, `CombatLoop` executes this pipeline in strict order:

1. **Cost check** — compare `CardDefinitionSO.EnergyCost` to current energy pool. If insufficient, reject with no state mutation.
2. **Position requirement check** — if `CardDefinitionSO.PositionRequirement` is `RequiresBehind`/`RequiresAhead` and current `PositionState` does not match, reject with no state mutation. `BonusIf*` does NOT gate play.
3. **Target validation** — if `TargetType == EnemySubsystem`, the chosen slot must be in `ValidSubsystemTargets` (or the array is empty = all valid). Reject if invalid.
4. **Deduct energy** — energy pool -= `EnergyCost`.
5. **Build `CardResolutionContext`** — context object passed to all effects. Contains: `PlayerVehicle (IVehicleView/IVehicleMutator)`, `EnemyVehicle (IVehicleView/IVehicleMutator)`, `SelfVehicle` (the card-player's vehicle), `OpponentVehicle` (the other), `TargetSlot` (nullable `SlotType`), `PositionState`, `CombatRng (System.Random)`, `TurnCount`, `SourceCard (ICardData)`.
6. **Effect loop** — for each `ICardEffect` in `SourceCard.Effects` **in list order**:
   - If `effect.Condition` is non-null, call `effect.Condition.IsMet(context)`. If false, skip this effect.
   - Call `effect.Apply(context)`.
   - Card System EC10: effects do **not** short-circuit on Offline targets. If an earlier effect takes a subsystem Offline, subsequent effects still fire against that slot. This GDD honors that rule.
7. **Post-play keyword resolution** — if `Exhaust` keyword set, card → Exhausted. Else card → Discard.
8. **Emit `CardPlayed(card, context)`** — view layer subscribes for animation; Combat HUD updates.

If any step 1–3 rejects, no mutation occurs, no event fires, and UI feedback indicates "invalid play" (specified in Combat HUD UX Spec, not here).

**R4. Enemy Intent Telegraph**

Enemy AI (owned by Enemy System GDD, not this one) selects its next action at the **end of its current turn**, not at the start of the next one. The selected intent is stored on the enemy `Vehicle` as `NextIntent: EnemyIntent` and read by the view layer to display an intent icon (damage, status, defend, etc.) above the enemy.

Rule: **the player always sees the enemy's next action before committing any cards**. No card effect, status tick, or combat event may resolve the player turn before the intent is rendered. This is the Pillar 3 contract.

If the enemy's intent becomes invalid between selection and resolution (e.g., the targeted player slot is now Offline and the intent targeted *that slot specifically*), the enemy falls back to a deterministic retargeting rule defined in the Enemy System GDD. Card Combat does not define retargeting policy — it only provides the `CombatRng` seed and the resolution hooks.

**R5. Damage Resolution Pipeline**

When any damage value is applied to a target subsystem (via `DamageEffectSO`, status tick, or environment), the pipeline runs in this order — inherited from Vehicle & Part F-VP2 and Status Effects F1 — and is reasserted here as the combat-authoritative sequence:

1. **Corrode bonus** — if target slot has `Corroded` status, `damage += CorrodeBonus(Stacks)` (Status Effects F1). *DOT ticks (`DamageSource.Status`) skip this step — DOT is not a card-sourced damage event and Corrode does not apply to it.*
2. **Shield absorption — forks on target slot type** *(canonical authority: V&P F-VP2)*:
   - **Frame path**: `absorbed = min(damage, vehicle.CurrentArmor)`, `vehicle.CurrentArmor -= absorbed`, `damage -= absorbed`.
   - **Non-Frame path (Weapon/Engine/Mobility)**: `absorbed = min(damage, slot.PlatingStacks)`, `slot.PlatingStacks -= absorbed`, `damage -= absorbed`.
   - **DOT exception (both paths)**: `DamageSource.Status` bypasses step 2 entirely. DOT ticks land directly on `slot.Hp`.
   - **Subsystem-strike exception (non-Frame path only)**: if the source `DamageEffectSO` has `BypassPlating = true` (subsystem-strike card family — Armor Piercer successor), step 2 is skipped on the non-Frame path. Subsystem-strike cards cannot target Frame (enforced at R3 step 3 via `ValidSubsystemTargets` restriction), so this exception never interacts with the Frame/Armor path.
3. **Hp deduction** — `Slot.Hp -= damage`, floored at 0.
4. **State transition** — evaluate new `DamageState` (Functional → Degraded → Offline) per V&P R2/F-VP1. If `Slot == Frame` and new state is `Offline`, set `Vehicle.IsDead = true`.
5. **Status trigger fanout** — apply any on-damage status hooks (e.g., Thorns if added later). EA: no triggers.
6. **Emit `SlotStateChanged(slot, oldState, newState)`** — view layer listens (ADR-0001 event-driven update).

Card Combat never bypasses this pipeline. Every damage source — card, status tick, environment — funnels through the same mutator call with a `DamageSource` enum value (`{ Card, Status, Environment }`, closed by V&P R9).

**R6. Status Tick Resolution (Fire-Before-Tick)**

Per Status Effects R3, all status instances on a vehicle resolve in **fire-before-tick** order during the vehicle's own end-of-turn phase:

1. **Fire phase** — for each active `StatusInstance` on this vehicle (iterate in definition order within the `ActiveStatuses` list): apply its per-turn effect (e.g., Burning deals `BurningDamage_per_tick(Stacks)` to the slot it is attached to via `ApplyDamage(slot, amount, DamageSource.Status)` — DOT **bypasses both Corrode (step 1) and shield absorption (step 2)** of F-CC1 and lands directly on `slot.Hp`. Armor is NOT consumed by DOT on Frame).
2. **Tick phase** — after all fires resolve, each `StatusInstance.Duration -= 1`. Instances at `Duration == 0` are removed.

This preserves the design contract that a status dealing damage on its *last* turn still fires before expiring.

**R7. Enrage Timer (Closes Status Effects OQ-SE1)**

Every enemy has an `EnrageTurn: int` field on its definition (owned by Enemy System GDD; Card Combat defines the mechanic). Default EA value: **turn 8**.

| Parameter | Default | Description |
|---|---|---|
| `EnrageTurn` | 8 | Combat turn on which Enrage begins. Counts player turns (`TurnCount` — set in R2 Setup, incremented in PlayerTurn). |
| `EffectiveEnrageBaseBonus` | +2 damage | Damage bonus applied to every enemy damage source on the Enrage activation turn. |
| `EnrageEscalation` | +1 damage per subsequent turn | Each turn after activation, the bonus increases by 1. Turn 8: +2, Turn 9: +3, Turn 10: +4, … |
| `EnrageTelegraphLeadTurns` | 2 | Starting on `(EnrageTurn - EnrageTelegraphLeadTurns)` the enemy shows a visible Enrage warning indicator (art/VFX owned by Combat HUD GDD). Turn 6 is the first warning for default values. |

Enrage is computed at the **`CombatLoop` level**, not on the enemy `Vehicle` — per Status Effects R6 Enrage-decoupling requirement. `CombatLoop` reads `TurnCount` and applies the Enrage modifier to enemy damage output at R5 step 1 (post-Corrode, pre-Plating) as a `DamageSource.Environment` flat add. This ensures Enrage scales independently of status effects and cannot be dispelled.

Enrage does **not** apply to the player. It exists solely to enforce kill pace and prevent Control-family stall strategies (as required by Card System §3, Control family note).

**R8. Redirected Target Selection (Closes Status Effects OQ-SE3)**

When a status effect causes damage to be redirected (Status Effects Redirected status), `CombatLoop` selects the new target slot using this rule:

1. Enumerate slots on the redirect target vehicle where `DamageState != Offline`.
2. If the enumerated set is empty (all slots Offline — should be impossible because Frame Offline = vehicle death), fall back to the original target slot with no redirection.
3. Otherwise, select uniformly at random from the enumerated set using `CombatRng.Next(0, count)` where `CombatRng` is the seeded `System.Random` instance established in R2 Setup.

The redirect selection is fully deterministic given the run seed + combat turn count + redirect sequence index. This makes Redirected reproducible in automated tests (see Section H).

**R9. Hand, Energy, and Draw Constants (EA Defaults)**

| Constant | Default Value | Location |
|---|---|---|
| `HandDrawTarget` | 4 | `CombatRulesSO` (new SO, owned by this GDD) |
| `MaxEnergy` | 3 | `CombatRulesSO` |
| `MaxHandSize` | 10 (overflow cap) | `CombatRulesSO` |
| `StartingEnergy` | 3 | `CombatRulesSO` |

Draw logic: at `PlayerTurn` start, draw cards from deck one at a time until `hand.Count == HandDrawTarget`, OR until deck+discard are both empty. Retain cards count toward the target (a hand of 4 Retain cards triggers zero draws). If hand already exceeds `HandDrawTarget` (multi-Innate + Retain), no cards are drawn.

Hand overflow: if a card effect adds a card to hand that would exceed `MaxHandSize`, the added card is sent directly to the discard pile. This is a hard ceiling to prevent UI pathologies.

`CombatRulesSO` is a single ScriptableObject authored in the project and loaded by `CombatLoop` at Setup. Its specific values are **Tuning Knobs** (Section G).

**R10. Mid-Resolution Deck Zone Mutation (Closes V&P OQ-VP4)**

When a card effect or part scrap causes a card to be added to or removed from a deck zone (deck/hand/discard/exhausted) **during** resolution of another card's effect list, the change takes effect **immediately** but does not retroactively alter the in-flight effect list. Specifically:

1. A `DrawCardsEffectSO` resolved mid-pipeline adds cards to hand *now*; those cards are available for the next card play, not for subsequent effects in the current card.
2. A part scrapped mid-combat (via a future mechanic) removes its granted cards from deck/discard/exhausted *now*; in-hand copies of those cards remain playable this turn but cannot be drawn again.
3. The Card Combat engine does NOT re-sort, re-index, or re-evaluate any in-flight effect list when a deck-zone mutation occurs. Each effect resolves against the zone state as it reads the context; context is not snapshot-frozen.

This rule closes OQ-VP4 with a simple, deterministic contract: **mutations are immediate, but in-flight effect lists run to completion on the effect-list order they started with.**

**R11. Frame=Empty Death Ruling (Closes V&P OQ-VP3)**

Per V&P R3, `IsDead = Frame.DamageState == Offline`. If a combatant enters combat with `Frame` slot empty (no part installed), `DamageState` is undefined — V&P treats it as an illegal configuration. Card Combat enforces this at Setup:

- `CombatLoop.Setup()` validates both `Vehicle` instances. If either has `Frame.Part == null`, Setup throws `InvalidCombatantException` and aborts combat scene load.
- Runtime Empty-Frame mid-combat (e.g., if a future mechanic could scrap the Frame part mid-combat) is disallowed: `IVehicleMutator.RemovePart(Frame)` during an active `CombatLoop` is rejected. Frame parts are non-removable during combat regardless of source.

This closes OQ-VP3: Frame-replacement enforcement is a Chopshop-only concern (already established by V&P R6); Card Combat simply refuses to run without a Frame.

**R12. Stat Round/Floor Convention (Closes V&P OQ-VP5)**

All stat composition (V&P F-VP3) produces `float` intermediate values. When a combat system consumes an int (e.g., a damage bonus), the conversion is **floor** (truncate toward zero):

```
IntConsumerValue = (int)Math.Floor(FloatStatValue)
```

Example: a Scout with EngineBonus `+0.7` contributing to a hypothetical int-consuming effect yields `0`, not `1`.

**No combat formula in EA consumes int from a float stat** — all current formulas (F1, F-VP1..4) use int inputs directly. This rule exists preemptively to close OQ-VP5 with a single project-wide convention.

**R13. CombatResult Schema + Run-Layer Isolation Invariant (retrofit 2026-04-23)**

Card Combat emits a single canonical `CombatResult` payload at `CombatEnded`. This payload is the only data Card Combat exposes to callers; no other run-layer state is readable from within combat, and no run-layer state is writable from within combat.

```csharp
public sealed class CombatResult
{
    public bool   DidPlayerWin;
    public int    TurnCount;                   // final S3 counter value
    public int    DamageDealtToEnemyFrame;     // player's contribution to enemy Frame Hp reduction
    public int    DamageTakenOnPlayerFrame;    // enemy's contribution to player Frame Hp reduction
    public int    CardsPlayed;
    public int    EnergyUnspent;               // sum across all player turns; used by DS feed
    public int    EnrageActivatedTurn;         // S3 counter at which Enrage fired, or -1 if never
    public string EnemyArchetypeId;            // from EnemyDefinitionSO; used for L&R beacon key
    public bool   WasEliteCombat;              // true for EliteCombatHandler, false for CombatHandler
    public bool   WasBoss;                     // true only when (WasEliteCombat && NE handler's isBoss flag)
    public int    CombatNodeIndex;             // echo of Node Map's nodeIndex for RNG/telemetry
}
```

`CombatResult` is consumed by the NE combat handler (CombatHandler / EliteCombatHandler) which forwards it to L&R's `GenerateRewards` and to NE's outcome callback. Card Combat itself does NOT route to L&R — the handler is the intermediary (NE GDD Section C).

**Run-layer isolation — non-interaction invariant with Node Map / Scrap Economy:**

1. Combat code never reads or writes: `CurrentScrap`, `CurrentFuel`, `StormFrontX`, `CombatCommitCounter`, `Phase`, `Reachable`, `BeaconType` (outside the handler setup payload), or any `IScrapEconomy` / `INodeMap*` interface.
2. Combat consumes only these inputs: `runSeed`, `combatNodeIndex`, `Vehicle` state (through `IVehicleMutator` / `IVehicleView` only), `CombatRulesSO`, enemy setup payload.
3. Combat writes only: `Vehicle` state via `IVehicleMutator`, C# events (non-view-aware), `CombatResult`.

**Enforcement — build-time invariant** (per Scrap Economy GDD F.4a):

1. A linter rule (`tools/ci/scrap-combat-isolation-check.cs`) scans every `.cs` file for co-presence of `using Combat.*;` / `namespace Combat.*` **and** any reference to `IScrapEconomy`, `ScrapStateDTO`, `INodeMap*`, or the `ScrapEconomy.*` / `NodeMap.*` namespaces. Co-presence = build failure.
2. Assembly definition (`.asmdef`) boundary: the Combat assembly may not reference the Scrap Economy or Node Map assemblies. This is the primary structural guard; the linter is belt-and-suspenders.
3. Runtime assertion in `IScrapEconomy` implementation constructor: log and hard-assert if the call site is within a Combat-tagged scene. Debug-build only.

**Justification:** Pillar 4 (Scarcity with Agency) has AC-NM55 (separation contract) as its load-bearing structural test. A runtime-only enforcement means a single bad PR can silently degrade the invariant until playtest surfaces it. Build-time catches it at the author's screen.

**R14. EnrageIntentCandidates Contract (Enemy System integration, retrofit 2026-04-23)**

When Enrage activates (R7 — turn counter reaches `EnrageTurn`), the enemy's intent pool may shift from its base intent set to a more aggressive candidate set. The selection is owned by the Enemy System's `IEnemyBrain` implementation; Card Combat provides the hook and the state.

```csharp
public interface IEnemyBrain
{
    // Called during EnemyTurn phase to select next intent
    EnemyIntent SelectNextIntent(EnemyBrainContext context);

    // Called only on the turn Enrage first activates (TurnCount == EnrageTurn)
    // Returns a non-empty array of candidate intents for the Enrage-active state
    // Array must contain at least one intent that is NOT also in the base intent pool
    // (else Enrage has no mechanical effect beyond the damage bonus)
    EnemyIntent[] EnrageIntentCandidates(EnemyBrainContext context);

    // Called by Card Combat EC-CC6 / OQ-CC1 when the selected intent's target goes Offline
    // Returns a replacement intent or a NoOpIntent sentinel (Status Effects retrofit)
    EnemyIntent OnInvalidTarget(EnemyIntent original, EnemyBrainContext context);
}
```

**Rules:**

- The Enrage candidate set must include ≥1 intent that is **not** present in the base pool. Empty-or-duplicate-pool is a content authoring error; SO validator rejects at import.
- Card Combat selects from the candidate set using the combat RNG stream (`runSeed XOR combatNodeIndex`, post-deterministic). Selection is NOT owned by Enemy System — the brain supplies the pool; combat picks.
- On and after the Enrage activation turn, subsequent intent selections continue to use the Enrage candidate set until combat ends. There is no "re-cool" back to the base pool.
- `EnrageIntentCandidates` is called exactly once per combat (on activation turn); the resolved candidate array is cached by `CombatLoop` for the duration.

See Enemy System GDD for per-archetype candidate-set authoring and OQ-CC1 / `NoOpIntent` fallback semantics.

**R15. Encounter Types & Starting Conditions (Closes OQ-CC-NEW-5, Position & Movement retrofit 2026-04-24)**

Every combat encounter has an `EncounterType` that governs starting `PositionState` and first-turn actor. There are exactly two types in EA scope:

| EncounterType | Starting Position (player) | First Actor | Rationale |
|---|---|---|---|
| **Standard** | `Behind` | Player | Default wasteland chase framing — you're running them down. Player always sees enemy intent telegraph before committing (Pillar 3 baseline contract). |
| **Ambush** | `Ahead` | Enemy | Enemy has drop on player — player is ahead of the attacker, must turn and fight or flee. Enemy's pre-selected intent resolves during Setup before first `PlayerTurn`. |

**Encounter authoring**:

- `CombatRulesSO.DefaultEncounterType : EncounterType = Standard` — the per-project default applied when the Node Encounter handler does not override.
- `CombatRulesSO.AmbushStartingPosition : PositionState = Ahead` — locked default; exposed as a tuning knob only for future encounter variants (see Tuning Knobs).
- The Node Encounter handler (`CombatHandler`, `EliteCombatHandler`) passes the chosen `EncounterType` into `CombatLoop.Setup(encounterType, enemy, runSeed, nodeIndex)`. Card Combat does NOT author encounter types — it only honors the payload it receives.

**Pillar 3 readability contract for Ambush**:

The node-map node preview (Node Map GDD) MUST surface the Ambush flag on the node icon — the player sees "Ambush Combat" before committing to the node. The player therefore always knows the encounter type before combat starts; surprise is consented-to, not imposed. Ambush does NOT hide the enemy's first intent — the telegraph is rendered on the enemy vehicle during Setup, immediately before the enemy resolves it. This preserves the "no important information off-screen" guarantee.

**Setup signature amendment**: `CombatLoop.Setup(encounterType: EncounterType, enemy: EnemyDefinitionSO, runSeed: int, nodeIndex: int)`. If `encounterType == Ambush`: after drawing opening hands and seeding RNG, the enemy immediately selects its first intent, `CombatLoop` emits `EnemyTurnStarted`, resolves the intent, then transitions to `PlayerTurn`. If `encounterType == Standard`: standard flow per R2 — transitions directly from Setup to `PlayerTurn`.

**R16. Enemy Intent Position Model (Position & Movement retrofit 2026-04-24)**

Enemy intents carry position-gating metadata mirroring the player card model (R3 step 2). This allows archetype-specific position preferences (bomb-lobber behind-preferred, lancer ahead-preferred) without adding new intent types.

```csharp
public enum IntentPositionRequirement
{
    None,             // intent fires regardless of position
    RequiresAhead,    // enemy must be Ahead (= player Behind) to select/resolve this intent
    RequiresBehind,   // enemy must be Behind (= player Ahead) to select/resolve this intent
    BonusIfAhead,     // intent always selectable; deals BonusDamage when enemy Ahead
    BonusIfBehind     // intent always selectable; deals BonusDamage when enemy Behind
}

public struct EnemyIntent
{
    public IntentType Type;               // Attack, MultiAttack, DebuffAttack, Buff, Plate, PositionShift, etc.
    public int Damage;
    public int Hits;
    public SlotType? TargetSlot;
    public IntentPositionRequirement PositionRequirement;  // NEW (R16)
    public int PositionBonus;                              // NEW (R16) — added to Damage when BonusCondition met
    // ... other existing fields
}
```

**Position reading convention (per S2)**: Enemy position is the logical inverse of `playerVehicle.PositionState.Position`. `RequiresAhead` on an enemy intent means "enemy Ahead" = "player Behind". Card Combat computes this inversion at intent-selection and resolution time; Enemy System authors intents from the enemy's perspective.

**Damage application (mirrors F1 position-bonus rule in Card System GDD)**: If `PositionRequirement == BonusIfAhead` and enemy is Ahead (player Behind), `effectiveDamage = Damage + PositionBonus`. Same for `BonusIfBehind`. If `Requires*` and the condition is not met at selection time, the intent is filtered out of the pool (see R17). The canonical damage formula including this bonus is F-CC5 (Formulas section).

**R17. Enemy AI Selection: Pool-Filter + Reposition Fallback (Position & Movement retrofit 2026-04-24)**

Enemy AI intent selection interacts with position as follows. This rule is Card Combat's contract; per-archetype weights and preferred positions are authored in Enemy System.

**Selection algorithm** (runs at end of EnemyTurn per R4, and at Setup for Ambush per R15):

1. Read enemy's candidate pool from `IEnemyBrain.SelectNextIntent(context)` → `EnemyIntent[]`.
2. **Pool filter**: remove any intent where `PositionRequirement ∈ { RequiresAhead, RequiresBehind }` and the current enemy position does not satisfy it.
3. If filtered pool is non-empty → select from it using the archetype's weight distribution (Enemy System owns weights).
4. **If filtered pool is empty** → enemy selects a synthetic **Reposition intent** (see R18) instead of selecting a base-pool intent. The Reposition intent is not authored in the base pool; it is injected by Card Combat when pool-filter produces an empty set.
5. If the synthetic Reposition intent is also invalid (e.g., archetype has `CanReposition = false` — reserved field, defaults true in EA), fall through to `IEnemyBrain.OnInvalidTarget` → `NoOpIntent` per R14/OQ-CC1. Authoring error: SO validator warns if an archetype has `CanReposition = false` AND contains any `Requires*` intent.

**Archetype preferred position** (authored in Enemy System, consumed by Card Combat):

Each enemy archetype declares `PreferredPosition: { Ahead, Behind, None }`. This does NOT gate selection — it only biases the Reposition fallback's direction when both positions have valid intents available. Not a hard rule; weight system can still produce a Reposition even when at PreferredPosition if that's what the pool says.

**R18. Reposition Intent Resolution (Position & Movement retrofit 2026-04-24)**

When R17 step 4 injects a Reposition intent, its resolution is defined here. Card Combat owns the synthetic intent; Enemy System never authors it.

**Synthetic RepositionIntent**:

```csharp
// Constructed by CombatLoop, not by IEnemyBrain
public static EnemyIntent BuildRepositionIntent(PositionState targetPosition)
{
    return new EnemyIntent {
        Type = IntentType.PositionShift,
        TargetPosition = targetPosition,
        Damage = 0, Hits = 0,
        PositionRequirement = IntentPositionRequirement.None,
        PositionBonus = 0
    };
}
```

**TargetPosition selection** (deterministic, no RNG):

1. Identify the inverse of current enemy position — call this `oppositePosition`.
2. Query the pool for any intent that would be valid at `oppositePosition` (i.e., would not be filtered out by R17 step 2 if enemy were at `oppositePosition`).
3. If yes → `TargetPosition = oppositePosition`. If the filtered pool from step 2 is also empty at `oppositePosition` → Reposition cannot help; fall through to R17 step 5 (NoOpIntent).

**Telegraph**: The intent icon displays a directional shift glyph (Combat HUD UX Spec), not a damage number. Player reads "enemy repositioning to Ahead" as clearly as they read "enemy attacks for 6".

**Cost**: Reposition consumes the enemy's entire turn — no attack, no status, no plate. This mirrors the player's Maneuver family cost model (Card System). The enemy turn emits `EnemyTurnEnded` without damage resolution after applying `ShiftPositionEffectSO` on itself.

**CanReposition: bool (reserved on Enemy archetypes)**: Defaults to `true` in EA. Exists as an archetype flag for potential future archetypes (e.g., stationary turret-style enemies that cannot move). Out of scope for EA content; reserved to avoid breaking the contract later.

### States and Transitions

**S1. CombatLoop Phase State Machine**

| State | Valid Transitions | Trigger |
|---|---|---|
| `NotStarted` | → `Setup` | Combat scene loaded, both `Vehicle` references registered |
| `Setup` | → `PlayerTurn` | Opening hands drawn, RNG seeded, `CombatStarted` emitted |
| `PlayerTurn` | → `PlayerResolve` or `Ended` | Player clicks End Turn, OR `Vehicle.IsDead` becomes true mid-turn → `Ended` |
| `PlayerResolve` | → `EnemyTurn` or `Ended` | End-of-turn effects resolved, status ticks complete. If `EnemyVehicle.IsDead`, skip to `Ended`. |
| `EnemyTurn` | → `PlayerTurn` or `Ended` | Enemy intent resolved, next intent selected and telegraphed. If `PlayerVehicle.IsDead`, skip to `Ended`. |
| `Ended` | (terminal) | `CombatEnded` emitted with winner/loser |

The `Ended` state is entered from any phase when either `Vehicle.IsDead` becomes true. Death-check runs after **every** damage application (R5 step 4) — not just at phase boundaries. This means a mid-pipeline death cuts off further effects in the current card's effect list (see EC-CC3 in Edge Cases).

**S2. PositionState Transitions**

`PositionState.Position` is a two-value enum `{ Behind, Ahead }`. Default at combat Setup: **Behind** (player starts behind the enemy vehicle — Pillar 1 framing: you're chasing them down the wasteland).

| From | To | Trigger |
|---|---|---|
| `(uninitialized)` | `Behind` | Setup with `EncounterType == Standard` — default player-behind chase framing (R15) |
| `(uninitialized)` | `Ahead` | Setup with `EncounterType == Ambush` — player starts Ahead, enemy resolves first intent (R15) |
| `Behind` | `Ahead` | `ShiftPositionEffectSO` resolves with `NewPosition = Ahead` |
| `Ahead` | `Behind` | `ShiftPositionEffectSO` resolves with `NewPosition = Behind` |
| `Behind` | `Behind` | `ShiftPositionEffectSO` resolves with `NewPosition = Behind` (no-op; no event emitted) |
| `Ahead` | `Ahead` | Same (no-op) |

Position is **per-vehicle** conceptually, but in practice: since there are only two combatants in a 1v1 encounter, the "position" is actually relative between them. This GDD treats `PositionState` as a property of the **player `Vehicle`** only; `enemyVehicle.PositionState` is the logical inverse and never independently tracked. Enemy cards/intents that reference position read the player's `PositionState` and invert as needed.

When `PositionState.Position` changes, `CombatLoop` emits `PositionChanged(oldPos, newPos)` for view-layer camera/framing updates.

**S3. Turn Counter**

`TurnCount: int` is a simple monotonic counter owned by `CombatLoop`.

- Starts at `0` in Setup.
- Incremented by `+1` at the start of every `PlayerTurn` phase (not `EnemyTurn` — one "turn" = player+enemy pair).
- Never decrements, never resets within a combat.
- Resets to 0 when a new combat starts (not carried across combats).

Any formula or rule referencing "turn N" (e.g., Enrage in R7) reads this counter.

**S4. Energy State**

Energy is a simple `int` on `CombatLoop` (not on the Vehicle — it's a combat-scoped resource).

- At Setup: `Energy = StartingEnergy` (default 3).
- At PlayerTurn start: `Energy = MaxEnergy` (default 3). Does NOT carry over from previous turn.
- On card play: `Energy -= card.EnergyCost` (R3 step 4).
- On `GainEnergyEffectSO.Apply`: `Energy += Amount`. No upper cap on energy within a single turn (effects can push energy above `MaxEnergy` temporarily).
- At PlayerTurn end: any remaining energy is discarded. No carry-over.

**S5. Deck Zone States (Delegated)**

Card location states (In Deck, In Hand, In Discard, Exhausted) are owned by Card System §States and Transitions. Card Combat does not redefine them — it only orchestrates their transitions via the Card Play Pipeline (R3) and end-of-turn cleanup (R2 PlayerResolve).

### Interactions with Other Systems

| System | What Card Combat Gives | What Card Combat Takes | Interface |
|---|---|---|---|
| **Card System** | Executes the card play pipeline (R3) against `CardDefinitionSO` contracts. Provides `CardResolutionContext` to every effect/condition call. | `CardDefinitionSO`, `CardEffectSO` hierarchy, `EffectConditionSO` hierarchy, keyword behavior contracts (EC1/EC2/EC13/EC14), `TargetType` / `PositionRequirement` enums. | Card Combat imports Card System types; Card System never imports from Card Combat. |
| **Vehicle & Part System** | Mutates `SubsystemState` (Hp, `PlatingStacks`, DamageState) via `IVehicleMutator`. Restores vehicle-level `CurrentArmor` on Frame via `IVehicleMutator.AddArmor(amount)` (called by `RestoreArmorEffectSO` — the Plate Up family effect; overflow above `MaxArmor` is silently discarded per armor-model decision 3). Emits `SlotStateChanged` events. Validates Frame-non-empty at Setup (R11). | `Vehicle` POCO, `SlotType` enum (4 entries — Weapon / Engine / Mobility / Frame), `DamageState` enum, `IVehicleView` (reads, including `MaxArmor` / `CurrentArmor` and `OnMaxArmorChanged` / `OnCurrentArmorChanged` events), `IVehicleMutator` (writes, including `AddArmor`; `MaxArmor` recompute is internal to V&P, fired implicitly via `OnMaxArmorChanged` after `InstallPart` / `RemovePart` / Offline transitions per ADR-0005), `DamageSource` enum (R9). Corrode-before-shield-before-Hp ordering locked in V&P F-VP2 (shield = Plating on non-Frame path, Armor on Frame path). `RestoreArmorEffectSO` is distinct from `RestorePlatingEffectSO` (Armor is vehicle-level; Plating is slot-level). | Card Combat calls through `IVehicleMutator` exclusively. No direct field access. |
| **Status Effect System** | Triggers status ticks at end-of-turn (R6 fire-before-tick). Provides Enrage timer (R7). Provides Redirected RNG (R8). | `StatusType` enum, `StatusInstance` struct, `StatusConditionSO` (for conditional card effects), tick semantics (R3 fire-before-tick). Closes OQ-SE1 (Enrage), OQ-SE3 (Redirected RNG). | Card Combat calls status resolution hooks; Status Effects owns the math. |
| **Enemy System (future)** | Runs enemy intent resolution during `EnemyTurn` phase. Calls enemy AI hooks to select/telegraph intents. Applies enemy damage through the same R5 damage pipeline. | `EnemyIntent` struct (intent type + target + payload), enemy AI decision hooks, per-enemy `EnrageTurn` override. | Card Combat defines `IEnemyBrain { EnemyIntent SelectNextIntent(context) }`. Enemy System implements it. |
| **Save & Persistence System** | Emits `CombatStarted` / `CombatEnded` events with combat state for save-point triggers. Does NOT support mid-combat saves in EA (V&P EC-VP19 conditional). | Run seed (`runSeed`), current combat node index (`combatNodeIndex`). Used as RNG seed inputs. | One-way: Save reads Card Combat events; Card Combat does not read from Save. |
| **Loot & Reward System** | On `CombatEnded` with player win, emits the canonical `CombatResult` payload (see schema below) via the Node Encounter combat handler's outcome callback. L&R consumes the payload to derive Difficulty Score (DS) and beacon key for its reward pipeline. | None (Loot & Reward is a downstream consumer only). | One-way: Card Combat → NE handler → L&R via `CombatResult`. |
| **Combat HUD (UX Spec)** | Emits all state-change events the HUD subscribes to: `CombatStarted`, `PlayerTurnStarted`, `CardPlayed`, `SlotStateChanged`, `StatusApplied`, `PositionChanged`, `EnrageActivated`, `EnrageTelegraphStarted`, `CombatEnded`. View layer is event-subscriber only. | None (HUD is pure consumer; no writes back). | One-way: Card Combat → HUD via C# `event` / `Action` delegates. No `UnityEvent` (per `technical-preferences.md`). |
| **Node Map System** | Combat never reads `Fuel`, `StormFrontX`, or any run-layer state (retrofit 2026-04-23 — non-interaction invariant). Combat uses only `runSeed` and `combatNodeIndex` as seed inputs. Combat does NOT advance the storm or spend Fuel; those are owned by Node Map's commit pipeline steps 4/8 outside the encounter. | `combatNodeIndex: int` + `runSeed: int` | One-way: Node Map → Card Combat (seed input only); Card Combat → NE handler (CombatResult), never directly to Node Map. |
| **Node Encounter System** | Card Combat is driven by the `CombatHandler` and `EliteCombatHandler` (Boss = EliteCombat+isBoss) per NE Section C. The handler owns combat setup (enemy archetype, biome modifiers), invokes Card Combat's combat loop, and consumes `CombatResult` on completion; it then forwards to L&R and emits the NE outcome callback. | `EnemyDefinitionSO`, biome modifier payload, `runSeed`, `nodeIndex`. | Card Combat → NE handler via `CombatResult`; NE handler → Card Combat via `Begin(setup)`. |

**Critical architectural constraint (from ADR-0001 and `technical-preferences.md`):**
- `CombatLoop` is a POCO. No MonoBehaviour holds canonical combat state.
- All events are `event Action<...>` — zero `UnityEvent` usage in combat systems.
- View-layer MonoBehaviours (CombatView, VehicleView, StatusView) subscribe to events at `Awake`/`OnEnable` and unsubscribe at `OnDisable`/`OnDestroy`. Card Combat has no knowledge of the view layer existing.

## Formulas

### F-CC1 — Composite Damage Pipeline

The combat-authoritative damage pipeline (R5). **V&P F-VP2 is the canonical authority** — F-CC1 restates the fork in combat-system terms so Card Combat implementors can read it without cross-referencing V&P. Any conflict between F-CC1 and F-VP2 is resolved in favor of F-VP2.

The pipeline forks at step 2 based on the target slot type. There are also two exceptions to step 2 (DOT and subsystem-strike).

**Non-Frame path (target is Weapon, Engine, or Mobility):**
```
step 1: damage_post_corrode  = base_damage + CorrodeBonus(slot.CorrodedStacks)
                               [CorrodeBonus from Status Effects F1]
step 2: absorbed_by_plating  = min(damage_post_corrode, slot.PlatingStacks)
        slot.PlatingStacks  -= absorbed_by_plating
        damage_after_shield  = damage_post_corrode - absorbed_by_plating
        [Subsystem-strike exception: if effect.BypassPlating == true,
         step 2 is skipped: damage_after_shield = damage_post_corrode,
         slot.PlatingStacks unchanged.]
step 3: slot.Hp              = max(0, slot.Hp - damage_after_shield)
step 4: slot.DamageState     = ComputeDamageState(slot.Hp, slot.MaxHp)   [V&P F-VP1]
```

**Frame path (target is Frame):**
```
step 1: damage_post_corrode  = base_damage + CorrodeBonus(slot.CorrodedStacks)
                               [CorrodeBonus from Status Effects F1]
step 2: absorbed_by_armor    = min(damage_post_corrode, vehicle.CurrentArmor)
        vehicle.CurrentArmor -= absorbed_by_armor
        damage_after_shield  = damage_post_corrode - absorbed_by_armor
step 3: slot.Hp              = max(0, slot.Hp - damage_after_shield)   // slot = Frame
step 4: slot.DamageState     = ComputeDamageState(slot.Hp, slot.MaxHp)   [V&P F-VP1]
```

**DOT exception (both paths):** Burning and all future DOT effects with `DamageSource.Status` bypass step 1 AND step 2 entirely. DOT ticks land directly on `slot.Hp` (the slot the status is attached to), skipping Corrode, PlatingStacks, and Armor absorption. See Status Effects GDD for the tick pipeline.

**Subsystem-strike exception (non-Frame path only):** `DamageEffectSO.BypassPlating = true` skips step 2 on the non-Frame path — the card hits slot.Hp directly after Corrode bonus. Subsystem-strike cards cannot target Frame (R3 step 3 enforces `ValidSubsystemTargets ⊂ {Weapon, Engine, Mobility}` for this card family), so the bypass never interacts with Armor. The `BypassPlating` flag is authored on `DamageEffectSO` by the Card System GDD.

**F-CC1 Variable Table:**

| Symbol | Type | Range | Path | Source |
|---|---|---|---|---|
| `base_damage` | int | 0–40 (EA ceiling with position bonus + Enrage) | Both | R3 step 5 + R7 Enrage additive |
| `slot.CorrodedStacks` | int | 0–10 (Status Effects cap) | Both | Status Effects |
| `CorrodeBonus(x)` | int | = x (Status Effects F1) | Both | Status Effects F1 |
| `slot.PlatingStacks` | int | 0–20 (V&P Tuning Knobs) | Non-Frame | V&P |
| `vehicle.CurrentArmor` | int | 0..MaxArmor | Frame | V&P R1 |
| `vehicle.MaxArmor` | int | 0..unbounded (sum of `ArmorContribution`) | Frame | V&P R1 |
| `slot.Hp` / `slot.MaxHp` | int | 0–24 (V&P Scout/Assault profiles) | Both | V&P |
| `effect.BypassPlating` | bool | { false, true } | Non-Frame | Card System (`DamageEffectSO` flag) |

**Example — Non-Frame path (Scout Weapon slot hit by a 6-damage Assault Focused card, slot has `CorrodedStacks = 2` and `PlatingStacks = 3`):**
- step 1: `damage_post_corrode = 6 + 2 = 8`
- step 2: `absorbed_by_plating = min(8, 3) = 3`, `slot.PlatingStacks = 0`, `damage_after_shield = 5`
- step 3: `slot.Hp = 10 - 5 = 5`
- step 4: Scout Weapon MaxHp = 10 → DegradedThreshold = 5 → `slot.DamageState = Degraded` (per V&P F-VP1)

**Example — Frame path (Scout Frame, `CurrentArmor = 3, MaxArmor = 8`, incoming 5 with no Corrode):**
- step 1: `damage_post_corrode = 5 + 0 = 5`
- step 2: `absorbed_by_armor = min(5, 3) = 3`, `vehicle.CurrentArmor = 0`, `damage_after_shield = 2`
- step 3: `Frame.Hp = 16 - 2 = 14`
- step 4: Scout Frame MaxHp = 16, DegradedThreshold = 8 → `DamageState = Functional` (14 > 8)

**Example — Frame path, Armor stripped (Scout Frame, `CurrentArmor = 0`, incoming 5, no Corrode):**
- step 1: `damage_post_corrode = 5`
- step 2: `absorbed_by_armor = min(5, 0) = 0`, `CurrentArmor` unchanged, `damage_after_shield = 5`
- step 3: `Frame.Hp = 16 - 5 = 11`
- step 4: `DamageState = Functional` (11 > 8)

**Example — Subsystem-strike on non-Frame (6-damage subsystem-strike card, `BypassPlating = true`, target Scout Engine with `PlatingStacks = 4, Hp = 12`, no Corrode):**
- step 1: `damage_post_corrode = 6 + 0 = 6`
- step 2: skipped (`BypassPlating == true`). `PlatingStacks` unchanged at 4, `damage_after_shield = 6`
- step 3: `Engine.Hp = 12 - 6 = 6`
- step 4: Scout Engine MaxHp = 12, DegradedThreshold = 6 → `DamageState = Degraded` (boundary case per V&P F-VP1)

---

### F-CC2 — Enrage Damage Bonus

**Canonical formula owner (BLOCKER-1 arbitration, 2026-04-24).** This section is the single source of truth for the Enrage damage model. Enemy System D.2 applies `EnrageBonus` as an additive term to `ResolvedDamage`; Status Effects R6 cites this formula; the registry entry `enemies.constants.DefaultEnrageBaseBonus` is the default for `EffectiveEnrageBaseBonus`. The deprecated multiplicative form (`ResolvedDamage × EnrageDamageMultiplier = 1.5×`) is replaced. Rationale: additive +1/turn escalation is a countable telegraph — the player can read next-turn damage directly off the intent number, satisfying Pillar 3 (Read to Win).

```
EnrageBonus(turn) = 0,                                          if turn < EnrageTurn
                  = EffectiveEnrageBaseBonus + (turn - EnrageTurn),  if turn >= EnrageTurn
```

**F-CC2 Variable Table:**

| Symbol | Type | Default | Range |
|---|---|---|---|
| `turn` | int | — | `CombatLoop.TurnCount` (1+) |
| `EnrageTurn` | int | 8 | 4–15 (Tuning Knobs). Per-archetype overridable on `EnemyDefinitionSO` (Enemy System D.4). |
| `EffectiveEnrageBaseBonus` | int | 2 | 0–8 (Tuning Knobs). Per-archetype overridable as `EnrageBaseBonusOverride` on `EnemyDefinitionSO` (Enemy System D.4); default is registry `enemies.constants.DefaultEnrageBaseBonus`. |

Output range: `[0, +inf)` in theory; in practice, combats should end within ~5 turns of Enrage activation or the player loses.

**Example — default tuning, turn 10:** `EnrageBonus = 2 + (10 - 8) = 4`. Every enemy damage source on turn 10 deals +4 flat damage (added post-Corrode, pre-Plating per R7).

**Example — turn 7, default tuning:** `EnrageBonus = 0`. No modification.

`EnrageBonus` is applied to enemy `base_damage` before F-CC1 step 1. It is treated as `DamageSource.Environment` so it cannot be dispelled or countered by status-clear cards.

**Escalation is system-wide, not overridable.** The `+1` per-turn escalation term is a global pacing constant. Per-archetype overrides only affect the `EffectiveEnrageBaseBonus` (the flat value on the activation turn). An archetype cannot author a faster or slower escalation curve.

---

### F-CC3 — Redirected Target Selection

```
eligible = [slot for slot in targetVehicle.Slots if slot.DamageState != Offline]
if eligible.length == 0:
    return original_target_slot
index = CombatRng.Next(0, eligible.length)
return eligible[index]
```

**F-CC3 Variable Table:**

| Symbol | Type | Source |
|---|---|---|
| `targetVehicle.Slots` | `Slot[4]` | V&P (4 slots: Weapon, Engine, Mobility, Frame — Armor is NOT a slot) |
| `CombatRng` | `System.Random` seeded at R2 Setup | `System.Random(runSeed ^ combatNodeIndex)` |
| `eligible` | `Slot[]` | filtered subset |
| `index` | int | `[0, eligible.length - 1]` |

**Determinism guarantee**: given identical run seed, combat node index, and redirect sequence (number of prior `Next()` calls on `CombatRng` within this combat), F-CC3 returns the same slot on every playback. This is the contract automated tests exploit in Section H.

**Distribution**: uniform over non-Offline slots. Example — Scout with Weapon Offline, other 3 slots Functional (Engine, Mobility, Frame): each of the 3 remaining slots has 33% probability of being selected. If Weapon is then revived to Degraded, the distribution changes to 25% per slot (all 4 slots eligible) starting from the next redirect.

---

### F-CC4 — Draw Count Per Turn

```
draws = max(0, HandDrawTarget - hand.Count)
actual_draws = min(draws, deck.Count + discard.Count)
```

**F-CC4 Variable Table:**

| Symbol | Type | Default | Notes |
|---|---|---|---|
| `HandDrawTarget` | int | 4 | From `CombatRulesSO`, Tuning Knobs |
| `hand.Count` | int | 0–`MaxHandSize` | Includes Retained cards from previous turn |
| `deck.Count` | int | 0+ | Shuffles from discard on empty (Card System §States) |
| `discard.Count` | int | 0+ | Reshuffled on deck-empty draw attempt |

**Example — opening turn, Scout with 0 Innate cards, 10-card deck:** `hand.Count = 0`, `draws = 4`, `actual_draws = 4`. Draws 4 from a 10-card deck.

**Example — turn 3, Retained 2 cards from turn 2, 0 Innate:** `hand.Count = 2`, `draws = 2`, `actual_draws = 2`. Two more cards drawn to reach hand size of 4.

**Example — hand overflow from Innate + Retain:** starter deck has 3 Innate cards all Retained. At turn 2 start: `hand.Count = 3 + 1 (Retained non-Innate) = 4`. `draws = 0`. No cards drawn — hand already at target.

### F-CC5 — Enemy Damage with Position Bonus (Position & Movement retrofit 2026-04-24)

Mirrors Card System F1 for the enemy-facing damage calculation. Produces the `base_damage` input to F-CC1.

```
variables:
  intent.Damage          (int >= 0)  base damage authored on the intent
  intent.PositionBonus   (int >= 0)  additional damage when BonusCondition met
  intent.PositionRequirement         IntentPositionRequirement enum (R16)
  enemyPosition                      inverse of playerVehicle.PositionState.Position (S2)

formula:
  bonus_active = (PositionRequirement == BonusIfAhead  && enemyPosition == Ahead)
              OR (PositionRequirement == BonusIfBehind && enemyPosition == Behind)

  effective_damage = intent.Damage + (bonus_active ? intent.PositionBonus : 0)
```

**F-CC5 Variable Table:**

| Symbol | Type | Range | Notes |
|---|---|---|---|
| `intent.Damage` | int | 0..99 | Authored on `EnemyIntent` (Enemy System GDD authoring guidelines) |
| `intent.PositionBonus` | int | 0..12 | Authored on `EnemyIntent`; see Tuning Knobs `IntentPositionBonus` |
| `PositionRequirement` | enum | — | `IntentPositionRequirement` (R16): `None`, `RequiresAhead`, `RequiresBehind`, `BonusIfAhead`, `BonusIfBehind` |
| `enemyPosition` | enum | `Ahead`/`Behind` | Computed as inverse of `playerVehicle.PositionState.Position` (S2) |

**Examples:**
- Bomb-lobber `Damage=3, PositionBonus=4, BonusIfBehind`; enemy Behind → `effective_damage = 7`.
- Bomb-lobber `Damage=3, PositionBonus=4, BonusIfBehind`; enemy Ahead → `effective_damage = 3` (bonus missed, intent still fires).
- Lancer `Damage=6, PositionBonus=0, RequiresAhead`; enemy Ahead → `effective_damage = 6` (pool-filter gated earlier by R17; intent never selected when enemy Behind).

`effective_damage` is fed into F-CC1 as `base_damage`. `Requires*` intents are pool-filtered by R17 before resolution — they do not produce zero damage at position failure; they are simply never selected. `BonusIf*` intents deal base `Damage` when the bonus condition fails (no penalty, only bonus missed).

## Edge Cases

### EC-CC1 — Card played deals enough damage to take Frame Offline mid-pipeline

The card has effects `[DamageEffectSO(10), ApplyStatusEffectSO(Burning, 3)]`. The damage takes the enemy's Frame to Offline. Resolution:

1. R5 damage pipeline runs, Frame.Hp → 0, DamageState → Offline, `IsDead = true`.
2. `CombatLoop` detects `IsDead` immediately after R5 step 4 (per S1 note).
3. Phase transitions to `Ended` — **the Burning effect does NOT resolve.** The effect list short-circuits on death only.

This is the one exception to Card System EC10 (effects resolve in full). Death is the terminator. Rationale: applying Burning to a dead vehicle has no observable effect; skipping it avoids view-layer noise (Burning apply VFX on a dying vehicle). Any effect whose target vehicle is dead is silently skipped.

---

### EC-CC2 — Card targets an Offline slot

`DamageEffectSO.Apply` called with `TargetSlot = EnemyVehicle.Slots[Weapon]`, slot is already Offline. The damage still runs through F-CC1:
- Plating on Offline slots is unchanged (V&P states Offline slots retain plating only if explicitly designed — current V&P spec: plating persists independent of DamageState).
- Damage is applied, `slot.Hp` goes further negative (floored at 0), DamageState stays Offline.
- No state transition event emitted (state did not change).

Rationale: "Overkill damage" has no mechanical effect, which is what the player expects. The UI may dim Offline slots as unavailable targets (Combat HUD concern), but the engine does not forbid the play.

---

### EC-CC3 — Card effect list contains an effect that kills the source vehicle

A player card with a self-damage side effect (if added post-EA) takes the player's Frame Offline mid-list. Resolution:

1. The self-damage effect resolves, `PlayerVehicle.IsDead = true`.
2. `CombatLoop` detects death after R5 step 4.
3. All remaining effects in the list are skipped; `CombatLoop` transitions to `Ended` with player loss.

The card is still considered "played" — it enters Discard (or Exhausted) before the death check triggers phase end. This keeps Exhaust/Retain accounting consistent.

---

### EC-CC4 — Multi-effect card with `DrawCardsEffectSO` followed by a condition on the drawn cards

A card with effects `[DrawCardsEffectSO(2), DamageEffectSO(X) with EffectConditionSO "hand has ≥5 cards"]`. Per R10 (mid-resolution mutation), drawn cards are available immediately. The condition is evaluated against the live hand state *at the moment the condition check runs*, so `hand.Count` includes the newly-drawn cards. This is the expected behavior — effect order matters.

---

### EC-CC5 — Player plays a card while they have 0 HP on a non-Frame slot

Valid. Offline non-Frame slots do not end combat (V&P R3 — only Frame Offline kills). The card plays normally. Any effect targeting the Offline slot follows EC-CC2.

---

### EC-CC6 — Enemy intent targets a player slot that goes Offline during the player turn

Player took Weapon to Offline during their turn. Enemy's telegraphed intent was "Deal 5 to Weapon." Per R4, enemy retargeting policy is owned by the Enemy System GDD. Card Combat guarantees:

- The original intent is frozen at telegraph time; it does NOT re-target automatically during the player turn.
- When the enemy turn resolves, if the targeted slot is Offline, `EnemySystem.OnInvalidTarget(context)` is called; the Enemy System returns a new target. Card Combat then applies damage to that new target through the normal R5 pipeline.
- For EA, default enemy fallback: retarget to Frame.

The "telegraph shown → action taken" contract from Pillar 3 still holds: if the enemy retargets, the HUD updates the intent display *before* damage is applied, giving the player one render frame of visual feedback. (Combat HUD GDD owns the feedback spec.)

---

### EC-CC7 — Enrage activates on the same turn a status tick would kill the enemy

Turn 8, default Enrage. Player has applied Burning 5 to enemy Frame. At end of player turn 8 (PlayerResolve phase), Burning ticks on the enemy (fire-before-tick from R6) deal 5 damage to Frame → enemy dead. `CombatLoop` transitions to `Ended` with player win. Enrage never applies that turn because the enemy is dead before its turn begins.

Rationale: Enrage is a property of the enemy's **turn**. Status ticks are a property of the **previous** phase (end-of-turn cleanup). A dead enemy has no turn to Enrage into.

---

### EC-CC8 — Enrage activates on same turn player has Stalled enemy

Turn 8, enemy has Stalled status (skip next turn). Enrage activation happens on the enemy's turn, but the enemy's turn is skipped due to Stalled. Result:

1. Enrage modifier still computes (`EnrageBonus(8) = +2`).
2. Enemy turn is skipped by Stalled — no damage sources fire, so the bonus has no target to apply to.
3. `EnrageBonus` is recomputed next enemy turn as `EnrageBonus(9) = +3`. The bonus continues to escalate by turn count regardless of whether the enemy actually acted.

Rationale: Enrage is a time-pressure mechanic. If Stalled could "waste" an Enrage turn, players could chain Stalls to stall Enrage forever, breaking the anti-stall design intent.

---

### EC-CC9 — Status applied with Duration=0 (Closes Status Effects OQ-SE4 in Card Combat scope)

A theoretical `ApplyStatusEffectSO` with `Duration = 0`: the status instance is added to `ActiveStatuses` and its fire effect resolves **once** during the next end-of-turn tick, then immediately ticks to 0 and is removed. Mechanically equivalent to a one-shot delayed effect. This is a valid pattern — EA does not use it but the engine supports it.

**Offline semantics (OQ-SE4 scope)**: if a status is applied to a slot whose DamageState is Offline:
- The `StatusInstance` is added to the slot's `ActiveStatuses` list normally.
- On end-of-turn tick, the status still fires (e.g., Burning still deals damage to the Offline slot's Hp counter, which is floored at 0).
- The slot's `DamageState` remains Offline regardless of fire effects.
- The status ticks and expires on schedule.

Rationale: statuses on Offline slots are effectively no-ops mechanically (damage can't go below 0, state can't degrade further), but the engine keeps them live for consistency — if the slot is later revived to Degraded, any still-active statuses resume having mechanical effect. This closes OQ-SE4.

---

### EC-CC10 — Two statuses of the same type applied in the same turn

Already owned by Status Effects GDD (stacking rules R2). Card Combat does not redefine; it calls `StatusEffectSystem.Apply(statusType, stacks, duration, targetSlot)` and the Status system handles the merge.

---

### EC-CC11 — Card played on turn 1 applies Stalled to enemy for turn 2

Turn 1 PlayerTurn: player plays `Stall` card. `ApplyStatusEffectSO` adds Stalled(1) to enemy. Turn 1 PlayerResolve: Stalled fires (if applicable — Stalled has no fire effect; it's a skip-turn effect checked at turn start), then ticks: Duration 1 → 0, status expires at PlayerResolve end.

Wait — this is a bug. If Stalled expires at the end of player turn 1, the enemy's turn 1 is never skipped. **Rule**: Stalled's fire-before-tick interaction is special — the "fire" for Stalled is "mark next enemy turn to be skipped." Status Effects GDD R3 specifies this; Card Combat honors it. The skip flag is checked at `EnemyTurn` phase start. So on turn 1: Stalled is applied, its fire effect sets `skip_next_enemy_turn = true`, then ticks and expires. At `EnemyTurn` start the flag is read and the turn is skipped. Flag is consumed by reading.

This is Status Effects territory but the cross-system interaction is documented here so Card Combat implementors don't get confused.

---

### EC-CC12 — Player plays a card with 0 energy cost and no effects

Valid but a design smell. The card plays, emits `CardPlayed`, and goes to discard (or Exhausted). Card Combat does not warn. Card System EC12 already flags cost-0 cards as requiring explicit design justification.

---

### EC-CC13 — Combat ends mid-player-turn via a card that kills the enemy

Player plays a card that deals lethal damage. Enemy Frame goes Offline during R5 step 4. `CombatLoop` immediately detects death after R5 and transitions directly to `Ended` — **skipping PlayerResolve** (no end-of-turn tick on player statuses, no Ethereal discard). Rationale: PlayerResolve is for cleanup before handing to the enemy turn; if the enemy is dead, cleanup is irrelevant. Any remaining effects in the card's effect list are skipped (EC-CC1 rule).

Side effect: player Ethereal cards in hand at kill-moment are NOT auto-discarded. This is correct — the cards will never be relevant again this combat (combat is over). Deck zones are reset at next-combat Setup per Card System states.

---

### EC-CC14 — Player plays a card that causes deck/discard reshuffle mid-effect

Card has effects `[DrawCardsEffectSO(10)]`. Deck has 3 cards, discard has 5. Draw runs: 3 from deck → deck empty. Remaining 7 draws trigger reshuffle: discard (5 cards) shuffles into deck, 5 more drawn, deck empty again, 2 draws fail silently (both deck and discard are empty). Total drawn: 8. This is the Card System EC5 contract.

Rationale: draw continues past reshuffle up to the requested count or until both zones are empty. No exception, no warning.

---

### EC-CC15 — Card targets "AllEnemySubsystems" and some are Offline

`TargetType = AllEnemySubsystems`. Damage pipeline runs for each of the 4 slots in SlotType enum order (Weapon → Engine → Mobility → Frame). Armor is NOT a slot and is NOT an independent AoE target — the Frame path uses the Armor absorption step (F-CC1 Frame path) when the pipeline reaches Frame. Offline slots still receive the damage call per EC-CC2 (Hp floored at 0, no state transition). This matters because Broad Assault cards can still trigger Frame Offline from a slot that's already at Hp 1.

---

### EC-CC16 — ShiftPositionEffectSO applied twice in the same card's effect list

Card has `[ShiftPositionEffectSO(Ahead), ShiftPositionEffectSO(Behind)]`. Both resolve in order. Net result: Behind. Each resolution emits a `PositionChanged` event (or skips emission on no-op per S2). View layer sees two events in sequence and either debounces or animates both — Combat HUD's concern.

---

### EC-CC17 — Player tries to play a card with a position requirement they don't meet

R3 step 2 rejects the play before energy is deducted. No state mutation. UI feedback owned by Combat HUD. `BonusIf*` does NOT gate the play — only `RequiresBehind` / `RequiresAhead` do.

---

### EC-CC18 — Combat started with both combatants having Empty Frame

R11 validates Frame presence at Setup. If either combatant's Frame is null, Setup throws `InvalidCombatantException` and aborts. This should be impossible in normal flow (V&P Chopshop enforces Frame replacement) but the guard is mandatory for robustness.

---

### EC-CC19 — DOT tick on Frame when `CurrentArmor > 0`

A Burning (or any future DOT) status instance attached to Frame fires at end-of-turn. Tick amount is computed per Status Effects F2. Because `DamageSource == Status`, F-CC1 step 1 AND step 2 are both bypassed: `vehicle.CurrentArmor` is NOT consumed, `slot.CorrodedStacks` is NOT added, and `Frame.Hp` drops by the raw tick amount (floored at 0). `OnCurrentArmorChanged` does NOT fire. `DamageState` re-derives from the new `Hp`.

Cross-reference: V&P AC-VP40. Status Effects GDD DOT bypass rule. Armor-model stress-test decision 5.

---

### EC-CC20 — Plate Up overflow when `CurrentArmor == MaxArmor`

`RestoreArmorEffectSO` calls `IVehicleMutator.AddArmor(amount)`. Per V&P R9, `AddArmor` caps `CurrentArmor` at `MaxArmor`; overflow is silently discarded. Playing Plate Up at full Armor spends the energy, the card moves to Discard (or Exhaust per its keyword), and `CurrentArmor` is unchanged. `OnCurrentArmorChanged` does NOT fire (value unchanged). No exception is thrown.

Design implication: playing Plate Up at full Armor is always a tempo loss. The HUD shows `CurrentArmor / MaxArmor` so the player can avoid this (Combat HUD concern).

Cross-reference: V&P AC-VP41. Armor-model stress-test decision 3.

---

### EC-CC21 — Armor fully stripped mid-combat (`CurrentArmor` → 0)

When Frame damage consumes all `CurrentArmor`, `vehicle.CurrentArmor == 0`. Subsequent Frame-targeted hits proceed directly to `Frame.Hp` with no absorption (F-CC1 Frame path: `absorbed_by_armor = min(damage, 0) = 0`). No special state — the pipeline handles it naturally. `OnCurrentArmorChanged` fires for the hit that reduced the value to 0 (payload = 0). Combat continues; vehicle is not dead unless `Frame.Hp` also reaches 0.

HUD must distinguish "`CurrentArmor = 0` (stripped)" from "`MaxArmor = 0` (glass-cannon build)" — mechanically identical but narratively different (armor-model stress-test T-5). Combat HUD concern, not a Card Combat data concern.

---

### EC-CC22 — Part goes Offline mid-combat, reducing `MaxArmor`; `CurrentArmor` clamps immediately

When a part with `ArmorContribution > 0` transitions to Offline during combat (via `ApplyDamage`), V&P EC-VP20 (UQ-1 resolution) specifies this synchronous event order at step 1 of the damage event:

1. `OnSlotDamageStateChanged` fires for the part transition.
2. **Same synchronous step**: `RecalculateMaxArmor()` runs. `MaxArmor` = new sum. If new `MaxArmor < CurrentArmor`: `CurrentArmor = new MaxArmor` (clamp). `OnMaxArmorChanged` fires (payload = new `MaxArmor`). `OnCurrentArmorChanged` fires if clamped (payload = new `CurrentArmor`).
3. Granted cards removed (if transition was to Offline).
4. `IsDead` recomputed.

Card Combat must not read `CurrentArmor` between step 1a and step 1b — C# event synchrony means any subscriber to `OnSlotDamageStateChanged` that reads `CurrentArmor` sees the post-clamp value. This is correct behavior: in-flight cards observe the new `CurrentArmor` after the clamp has occurred.

Example: Enemy has `CurrentArmor = 6, MaxArmor = 8`. Player's card takes the enemy Engine Offline. Engine has `ArmorContribution = 3`. After EC-VP20 step 1: `MaxArmor = 5`, `CurrentArmor = 5` (was 6, now exceeds new cap). `OnMaxArmorChanged(5)` fires, `OnCurrentArmorChanged(5)` fires — both before granted-card removal.

Cross-reference: V&P EC-VP20 (UQ-1), V&P AC-VP38. Armor-model stress-test decision 4.

---

### EC-CC23 — `CurrentArmor` resets to `MaxArmor` at every combat Setup

`CombatLoop.Setup()` calls vehicle state initialization before drawing opening hands. Per V&P R1, `CurrentArmor` is not serialized run state — it always reconstructs from `MaxArmor` at Setup. A vehicle arriving at combat with depleted `CurrentArmor` from a previous run's Chopshop state is not possible: the value is always set to `MaxArmor` at Setup.

Implementation (OQ-CC-NEW-2 closed 2026-04-21): `CombatLoop.Start()` calls `player.ResetArmor()` and `enemy.ResetArmor()` on a dedicated non-mutator `Vehicle.ResetArmor()` method (sets `CurrentArmor = MaxArmor` directly). The `IVehicleMutator` interface does NOT expose a reset — the mutator is damage/restoration only; lifecycle is the combat orchestrator's responsibility.

Cross-reference: V&P R1, V&P AC-VP37. Armor-model stress-test T-1.

---

### EC-CC24 — Enemy AI pool is all `Requires*` and none match current enemy position (Position & Movement retrofit 2026-04-24)

R17 step-2 pool-filter produces an empty set. Card Combat injects the synthetic Reposition intent (R18). Enemy consumes its entire turn repositioning — no damage, no status, no plate — emits `EnemyTurnEnded` without damage resolution. On the next enemy turn, pool-filter re-runs at the new position and the blocked intents become available.

---

### EC-CC25 — Reposition's `TargetPosition` pool is also empty (Position & Movement retrofit 2026-04-24)

R18 step-2 queries whether any intent would be valid at the opposite position. If none exist (an archetype authored with all `RequiresAhead` intents and enemy is Ahead → all valid here, pool is not empty; the failure case is an archetype with all `RequiresAhead` intents but those intents also each target an Offline slot, etc.), Card Combat cannot resolve a valid intent via repositioning. Falls through to `IEnemyBrain.OnInvalidTarget(anyRequiresIntent, context)` → `NoOpIntent` per R14 / OQ-CC1 fallback semantics. SO validator rejects archetypes with all-`Requires*` intents lacking cross-position coverage at import time; runtime guard emits `CombatLog.warning("enemy_pool_unresolvable")` for telemetry.

---

### EC-CC26 — Ambush encounter: enemy's first-selected intent targets a player slot that is already Offline at Setup (Position & Movement retrofit 2026-04-24)

Impossible in EA because V&P R1 / EC-CC23 reset subsystems at Setup (subsystems reconstruct to Functional, `CurrentArmor` resets to `MaxArmor`). No player subsystem can arrive at Setup in an Offline state. If this guarantee is ever relaxed in a future expansion: Ambush first-intent resolution runs through the same `OnInvalidTarget` retarget policy as R4 / OQ-CC1 — no special-casing for the Ambush path.

---

### EC-CC27 — Ambush encounter resolves lethal damage on Setup before player sees a card (Position & Movement retrofit 2026-04-24)

Possible in principle with an over-tuned Ambush archetype. **Design guardrail**: Ambush archetype tuning must not produce more than 50% of player Frame HP on the Setup-resolved first intent. This is enforced as an SO validator warning at import (Enemy System GDD authoring concern), not a runtime guard. If lethal damage does resolve: `CombatEnded(winner=Enemy)` fires immediately through the same mid-setup-death flow as any other lethal hit — no special Ambush-side path. The player still saw the encounter type on the node-map preview before committing (R15 Pillar 3 contract), so consent is preserved even if the combat is one-shot.

---

### EC-CC28 — Pre-selected `Requires*` intent invalidated mid-player-turn by `ShiftPositionEffectSO` (Position & Movement retrofit 2026-04-24)

At the end of a prior enemy turn, the enemy pre-selected an intent with `PositionRequirement = RequiresAhead` (valid at the time). During the following player turn, the player played a card containing `ShiftPositionEffectSO` that flipped the position — the pre-selected intent's requirement is no longer met. At enemy resolution (R4), Card Combat re-checks position gating before executing the intent. If invalid, Card Combat calls `IEnemyBrain.OnInvalidTarget(original, context)` per R14. Enemy System's policy owns the replacement selection (alternate intent / Reposition / `NoOpIntent`). Card Combat does NOT re-run the full R17 pool-filter at resolution time — this is a retarget event handled by the brain's fallback hook; full pool-filter only runs at end-of-turn intent *selection*, not at resolution.

## Dependencies

### Systems This GDD Depends On

| System | What Card Combat Needs From It | When Needed |
|---|---|---|
| **Card System** | `CardDefinitionSO`, `CardEffectSO` hierarchy, `EffectConditionSO` hierarchy, keyword resolution rules (EC1/EC2/EC13/EC14/EC15/EC16), `TargetType` / `PositionRequirement` / `CardFamily` / `CardKeyword` enums | Before Card Combat implementation begins. Card System is Approved. |
| **Vehicle & Part System** | `Vehicle` POCO with `Slot[4]` structure (Weapon, Engine, Mobility, Frame — Armor is NOT a slot) and vehicle-level `MaxArmor` / `CurrentArmor`; `SlotType` / `DamageState` enums; `IVehicleView` / `IVehicleMutator` interfaces (including `AddArmor`, `RecalculateMaxArmor`, `OnMaxArmorChanged`, `OnCurrentArmorChanged`); `DamageSource` enum; F-VP1 damage state thresholds; F-VP2 damage ordering fork (Corrode-before-shield-before-Hp, shield = Plating on non-Frame path and Armor on Frame path); R3 death check; R11 Frame-empty rejection | Before R5 damage pipeline implementation. V&P is Approved (retrofitted 2026-04-21). |
| **Status Effect System** | `StatusType` enum (Stalled/Redirected/Corroded/Burning), `StatusInstance` struct, R3 fire-before-tick order, R2 stacking rules, F1 Corrode bonus, F2 Burning damage, Stalled "skip next turn" contract | Before R6 tick resolution implementation. Status Effects is Approved. |
| **Save & Persistence System** | `runSeed` from `RunState`; `combatNodeIndex` — used as `CombatRng` seed | At `CombatLoop.Setup`. Save & Persistence is Approved. |
| **Node Map System (future)** | `combatNodeIndex: int` per node; win/lose outcome consumer; **node-map preview** surfaces `EncounterType` flag (R15 Pillar 3 contract — Ambush nodes must be labeled before the player commits) | Before first `/prototype combat` integration milestone. Not Started. |
| **Node Encounter System (future)** | `CombatHandler.Begin(setup)` payload carrying `EncounterType: EncounterType` (Standard / Ambush per R15); the handler owns per-node encounter-type selection and passes it into `CombatLoop.Setup()` | Before Ambush encounters ship. Not Started. |
| **Enemy System (future)** | `EnemyIntent` struct (with `PositionRequirement: IntentPositionRequirement` and `PositionBonus: int` fields per R16), `IEnemyBrain` interface implementation, per-archetype `PreferredPosition: PositionState` (R17), reserved `CanReposition: bool` archetype flag (R18), per-enemy `EnrageTurn` field override | Before first end-to-end enemy combat test. Not Started. |

### Systems That Depend On This GDD

| System | What They Need From Card Combat | Notes |
|---|---|---|
| **Card System** | Card Combat executes `CardEffectSO.Apply(CardResolutionContext)` — the `CardResolutionContext` structure is defined by Card Combat but consumed by Card System effect implementations. Any change to the context requires a coordinated update. | Bidirectional at the interface contract; Card System is otherwise upstream. |
| **Status Effect System** | Card Combat owns Enrage (R7 — closes OQ-SE1), Redirected RNG (R8 — closes OQ-SE3), and Offline-slot status semantics (EC-CC9 — closes OQ-SE4). Status Effects GDD should be updated with a reference back to this GDD once approved. | Bidirectional: Status Effects defines the statuses, Card Combat operationalizes Enrage and resolution order. |
| **Enemy System (future)** | `IEnemyBrain` interface, `EnemyIntent` struct, R4 telegraph contract, R6 retargeting hook (`OnInvalidTarget`), `CombatRng` seed access, R16 intent position model (`IntentPositionRequirement` enum + `PositionBonus`), R17 pool-filter + Reposition-fallback selection contract, R18 synthetic `RepositionIntent` ownership (Card Combat injects; Enemy brain never authors), F-CC5 position-bonus damage formula | Enemy AI cannot be authored without this GDD locked. |
| **Combat HUD (UX Spec / future GDD)** | Event bus signatures: `CombatStarted`, `PlayerTurnStarted`, `CardPlayed`, `SlotStateChanged`, `StatusApplied`, `PositionChanged`, `EnrageActivated`, `EnrageTelegraphStarted`, `CombatEnded` | HUD is event-subscriber-only. No inbound writes. |
| **Loot & Reward System (future)** | `CombatEnded` event payload with combat result (win/lose, enemy archetype, duration, damage taken) | Downstream consumer of combat outcome only. |
| **Vehicle & Part System** | Bidirectional: V&P closes Card Combat's OQ-VP3 (Frame-empty handling — R11), OQ-VP4 (mid-resolution mutation — R10), OQ-VP5 (stat floor convention — R12). The V&P GDD should be updated post-approval with references back to this GDD for those closures. | V&P is upstream for data, bidirectional for these three closures. |

### ADRs Referenced

| ADR | Dependency | Impact on Card Combat |
|---|---|---|
| **ADR-0001** (Visual Vehicle Part System) | Accepted 2026-04-21 | Card Combat must emit state-change events (not per-frame polls) for the view layer to drive MaterialPropertyBlock updates. `SlotStateChanged`, `PositionChanged`, `StatusApplied`, `CardPlayed` are the load-bearing events. |

### Downstream Propagation Required Post-Approval

- **Status Effects GDD**: add references to Card Combat R7 (Enrage), R8 (Redirected RNG), EC-CC9 (Offline status semantics) so the closures of OQ-SE1/SE3/SE4 are traceable from that GDD. **Armor retrofit addition**: Status Effects must explicitly state DOT bypasses Armor (cross-link to this GDD's F-CC1 DOT exception and EC-CC19).
- **Vehicle & Part GDD**: add references to Card Combat R10 (mid-resolution mutation), R11 (Frame-empty rejection), R12 (stat floor convention) so the closures of OQ-VP3/VP4/VP5 are traceable. **Armor retrofit addition**: V&P's "Closed in this GDD" entry for T-4 is now bilaterally closed — F-CC1 in this GDD forks on Frame vs non-Frame per F-VP2.
- **Card System GDD**: the part-granted-cards mechanic (V&P R7) still needs propagation. Card Combat honors the mechanic in R10 (mid-resolution mutations) but the Card System data contract for per-instance provenance tracking is still a Forward Dependency. **Armor retrofit addition**: Card System must (a) author `RestoreArmorEffectSO` as a distinct effect type (vehicle-level, calls `AddArmor`); (b) add `bool BypassPlating` to `DamageEffectSO` for the subsystem-strike card family (Armor Piercer successor); (c) enforce `ValidSubsystemTargets ⊂ {Weapon, Engine, Mobility}` on subsystem-strike card definitions.

## Tuning Knobs

All values live on `CombatRulesSO` unless otherwise noted. Designers adjust without code changes.

| Knob | Location | Current Value | Safe Range | Gameplay Effect |
|---|---|---|---|---|
| `HandDrawTarget` | `CombatRulesSO` | 4 | 3–6 | Larger = more options per turn, slower pacing, weaker individual cards. Card System F4 deck-size math is tuned to 4. |
| `MaxEnergy` | `CombatRulesSO` | 3 | 2–5 | Core throughput knob. 2 = very tight budgets, 5 = combo-builder feel. 3 locks us to Slay the Spire-adjacent pacing. |
| `StartingEnergy` | `CombatRulesSO` | 3 | 0–`MaxEnergy` | Usually = `MaxEnergy`. Reducing it creates a ramp-up first turn. |
| `MaxHandSize` | `CombatRulesSO` | 10 | 7–12 | Hard ceiling to prevent UI pathologies. Card System hand layout compresses past 7. |
| `EnrageTurn` (default) | `CombatRulesSO` (per-enemy override on enemy definition) | 8 | 4–15 | Smaller = shorter combats, higher Control-family pressure. Larger = more room for stall strategies. |
| `EffectiveEnrageBaseBonus` | `CombatRulesSO` | +2 | +0 to +5 | Harder floor on how much damage per turn Enrage introduces. +0 = pure escalation. |
| `EnrageEscalation` | `CombatRulesSO` | +1 per turn | +0 to +3 per turn | Steepness of the cliff. +3/turn creates very sharp endgame pressure; +0/turn is just a flat bonus post-activation. |
| `EnrageTelegraphLeadTurns` | `CombatRulesSO` | 2 | 0–5 | How much warning the player gets before Enrage. 0 = surprise (bad — Pillar 3 violation). 5 = early warning at turn 3 for default turn-8 Enrage. |
| `DefaultStartingPosition` | `CombatRulesSO` | `Behind` | `Behind` / `Ahead` | The position the player begins combat in for **Standard** encounters. Behind is Pillar 1 framing; switching changes Scout's opening hand feel. |
| `DefaultEncounterType` | `CombatRulesSO` | `Standard` | `Standard` / `Ambush` | Project-level default applied when Node Encounter handler does not override. Locked to `Standard` in EA; raising the default shifts the overall readability/surprise balance. |
| `AmbushStartingPosition` | `CombatRulesSO` | `Ahead` | `Ahead` (locked) | The position the player starts in for **Ambush** encounters. Exposed as a knob only for future encounter variants; changing this collapses Ambush into Standard. |
| `PreferredPosition` (per archetype) | `EnemyDefinitionSO` (Enemy System) | per archetype | `Ahead` / `Behind` / `None` | Biases Reposition fallback direction when both positions have valid intents (R17). Does not gate selection. Bomb-lobber uses `Behind`; lancer uses `Ahead`; brawler uses `None`. |
| `IntentPositionBonus` (per intent) | `EnemyIntent` (Enemy System authoring) | per intent | 0..12 | Extra damage on `BonusIf*` intents when bonus condition met (F-CC5). 0 = no positional variance; 12 = sharp positional cliff. Tune per archetype's "position identity". |
| `CanReposition` (per archetype) | `EnemyDefinitionSO` (reserved) | `true` | `true` (EA-locked) | Reserved for future stationary archetypes (turret-style enemies). Out of scope for EA content. |
| `CombatRngSeedFormula` | `CombatRulesSO` or code constant | `runSeed XOR combatNodeIndex` | — | Not a tuning value — changing this breaks save compatibility. Locked at ship. |
| Per-enemy `EnrageTurn` override | `EnemyDefinitionSO` (future, owned by Enemy System GDD) | — | 4–15 | Bosses use lower values for pressure; early enemies use default or higher. Per-enemy override is the primary pacing lever once Enemy System GDD is authored. |

### Not Tuning Knobs (Design Constraints)

The following are intentionally excluded from Tuning Knobs — changing them breaks pillars or system contracts:

- **Turn order (player first)** — Pillar 3 readability contract. Locked.
- **Fire-before-tick status order** — Status Effects R3 contract. Locked.
- **Corrode-before-shield-before-Hp damage ordering** — V&P F-VP2 contract. "Shield" means `vehicle.CurrentArmor` on the Frame path and `slot.PlatingStacks` on the non-Frame path. The ordering is locked on both paths. Locked.
- **DOT (DamageSource.Status) bypasses Corrode AND shield absorption** — armor-model stress-test decision 5. DOT ticks land directly on `slot.Hp`. Locked.
- **Subsystem-strike (`DamageEffectSO.BypassPlating = true`) cannot target Frame** — R3 step 3 targeting restriction. Bypass applies to non-Frame `PlatingStacks` only; never interacts with Armor. Locked.
- **Three sub-model split (`CombatLoop` / `SubsystemState` / `PositionState`)** — Systems-index scoping note C1. Locked.
- **`DamageSource` enum values** — V&P R9 contract. Locked.
- **Enrage does not apply to player** — Pillar 3 / fairness contract. Locked.
- **Ambush starting position = `Ahead`** — encounter-identity lock (R15). Ambush at `Behind` collapses to Standard. Locked.
- **Enemy position = logical inverse of player position** — S2 single-source-of-truth contract. Enemy position is never independently tracked; all enemy intent position logic reads `playerVehicle.PositionState.Position` and inverts. Locked.
- **Reposition consumes entire enemy turn** — R18 cost model; mirrors player Maneuver family. Locked.

## Visual/Audio Requirements

### Event → Visual Feedback Contract

Card Combat emits events; the view layer (Combat HUD, VehicleView, StatusView) subscribes. This GDD defines **which events must produce a visible response**; the specific animation/VFX/SFX is owned by the Combat HUD GDD and Audio Direction.

| Event | Required Visible Response | Owner |
|---|---|---|
| `CombatStarted` | Combat scene fade-in, both vehicles present, HUD renders opening state | Combat HUD GDD |
| `PlayerTurnStarted` | HUD highlights player as active; energy resets animate; drawn cards animate into hand | Combat HUD GDD |
| `CardPlayed` | Card-play animation distinct per family (per Card System §Audio); card leaves hand toward target; Exhaust uses distinct exit VFX (Card System §Visual) | Combat HUD GDD + Art Direction |
| `SlotStateChanged` | MaterialPropertyBlock overlay update per ADR-0001 (Functional/Degraded/Offline overlay swap). State-change happens in one frame; no per-frame polling. | ADR-0001 / VehicleView |
| `StatusApplied` | Status icon appears on the affected slot; tick animations on apply/fire/expire | Combat HUD GDD |
| `PositionChanged` | Camera framing shift and/or vehicle reorder animation (per session-memory Combat Scene Layout notes: chase rail, both face right, left=back/right=front, swap animation) | Combat HUD GDD |
| `EnrageTelegraphStarted` | Persistent warning indicator on enemy (icon + color shift) beginning at `EnrageTurn - EnrageTelegraphLeadTurns` | Combat HUD GDD |
| `EnrageActivated` | Distinct Enrage activation flash/VFX on enemy at turn start | Combat HUD GDD |
| `CombatEnded` | Victory/defeat screen transition; enemy visibly defeated or player vehicle visibly stopped | Combat HUD GDD |

### Audio Contract

| Event | Required Audio Behavior | Owner |
|---|---|---|
| `CardPlayed` | Family-specific SFX (Precision/Assault/Control/Repair/Maneuver) — Card System §Audio owns palette | Audio Direction |
| `SlotStateChanged` | Distinct SFX on Functional → Degraded and Degraded → Offline transitions; no SFX on same-state no-ops | Audio Direction |
| `EnrageActivated` | Impactful one-shot cue (drum hit / engine rev); should feel *threatening*, not celebratory | Audio Direction |
| `CombatEnded` | Win vs loss cues clearly distinct | Audio Direction |
| Ambient combat loop | Ambient engine/wasteland loop during combat scene, attenuated during card-play moments | Audio Direction |

### Constraints

- **Event-driven only**. No MonoBehaviour Update() polls combat state. All visual updates fire from C# event subscriptions.
- **No `UnityEvent`** — per `technical-preferences.md`. All events are `event Action<...>` delegates.
- **ADR-0001 honoured** — damage overlay transitions triggered by `SlotStateChanged` events, not polling.

## UI Requirements

### Combat Screen Core Elements

Card Combat defines **what state must be visible**; Combat HUD UX Spec owns layout and interaction design.

| Element | Must Display | Source |
|---|---|---|
| Player vehicle | 4 subsystem slots (Weapon, Engine, Mobility, Frame) with DamageState-appropriate visual (per ADR-0001), per-slot status icons, `PlatingStacks` counter on non-Frame slots, and a vehicle-level `CurrentArmor / MaxArmor` bar attached to the Frame (distinct color band, readable at a glance so the player can see Armor stripping) | V&P + Status Effects |
| Enemy vehicle | Same as player, plus current telegraphed `NextIntent` icon. Enemy `CurrentArmor / MaxArmor` is visible (symmetric — V&P R1 / armor-model decision 2; no fog-of-war on enemy Armor) | V&P + Status Effects + R4 |
| Player hand | Up to `MaxHandSize` cards, full tooltips on hover/select (Card System UI Requirements) | Card System |
| Energy pool | Current energy / `MaxEnergy` (e.g., "2 / 3") | S4 |
| Turn counter | Current `TurnCount`, with Enrage countdown / warning visible starting at `EnrageTurn - EnrageTelegraphLeadTurns` | S3 + R7 |
| Position indicator | Current `PositionState` (Behind / Ahead) with visual metaphor (chase-rail framing) | S2 |
| Deck / discard / exhausted counts | Three counters (or browsable lists on click), always visible | Card System §States |
| End Turn button | Always available during `PlayerTurn` phase; disabled during resolution | R2 |

### Readability Requirements (Pillar 3 Contract)

- **Telegraph always visible before commit**: R4 guarantees the enemy's `NextIntent` renders before the player can play a card. HUD must not hide the intent icon.
- **Subsystem state legible at a glance**: ADR-0001 damage overlay + slot icons must be readable from the standard combat camera zoom without requiring hover.
- **Enrage warning legible**: the `EnrageTelegraphStarted` event must produce a warning indicator that persists for `EnrageTelegraphLeadTurns` turns and is impossible to miss (per R7 defaults: visible starting turn 6 for default turn-8 Enrage).
- **Every damage number must animate from its source to its target slot**: no bulk HP tick-downs. Each damage application from F-CC1 produces a distinct visible "hit" on the target. This is the surgical-feel contract from Section B.

### Gamepad Parity

- All card interactions (select, target, play, end turn) must be fully operable with gamepad alone — per `technical-preferences.md`.
- Card tooltip is mandatory on gamepad select (Card System §UI).
- Targeting UI (R3 step 3) must have clear gamepad cycling between valid targets.

## Acceptance Criteria

All ACs use GIVEN/WHEN/THEN format. Each AC is testable. Automated tests run under NUnit via Unity Test Framework using `System.Random(seed)` for deterministic RNG. Playtest-only ACs are explicitly flagged.

### Turn Loop & Phase Transitions

**AC-CC1** — *GIVEN* a freshly loaded combat scene with two valid Vehicles, *WHEN* `CombatLoop.Setup()` runs, *THEN* `TurnCount == 0`, `Phase == Setup`, `CombatRng` is seeded with `runSeed ^ combatNodeIndex`, both hands are drawn to `HandDrawTarget`, and `CombatStarted` fires exactly once.

**AC-CC2** — *GIVEN* Setup completes, *WHEN* transition fires, *THEN* `Phase` becomes `PlayerTurn`, `TurnCount` increments to 1, `Energy == MaxEnergy`, and `PlayerTurnStarted` fires exactly once.

**AC-CC3** — *GIVEN* `PlayerTurn` with a non-empty hand and sufficient energy, *WHEN* player clicks End Turn, *THEN* phase sequence `PlayerResolve → EnemyTurn → PlayerTurn` executes without manual input, `TurnCount` increments by 1 on the second `PlayerTurn`, and both `PlayerTurnEnded` and `EnemyTurnEnded` fire exactly once.

**AC-CC4** — *GIVEN* both combatants are alive at end of enemy turn, *WHEN* `EnemyTurn` ends, *THEN* next enemy intent is selected and `EnemyIntentTelegraphed` is observable on the enemy Vehicle before `PlayerTurnStarted` fires on the following turn.

### Card Play Pipeline (R3)

**AC-CC5** — *GIVEN* player has 1 energy and attempts to play a card costing 2, *WHEN* `PlayCard(card)` is invoked, *THEN* the play is rejected, energy remains at 1, no effects resolve, and no `CardPlayed` event fires.

**AC-CC6** — *GIVEN* a card with `PositionRequirement = RequiresAhead` and player `PositionState.Position == Behind`, *WHEN* play is attempted, *THEN* play is rejected and no state mutation occurs.

**AC-CC7** — *GIVEN* a card with effects `[DamageEffectSO(5), ApplyStatusEffectSO(Burning, 2)]`, *WHEN* played on a living enemy, *THEN* both effects resolve in list order and a `CardPlayed` event fires exactly once after both effects complete.

**AC-CC8** — *GIVEN* a card with `Exhaust` keyword, *WHEN* play completes normally, *THEN* card is in the Exhausted zone, not Discard.

### Damage Pipeline (F-CC1 / R5)

**AC-CC9** — *GIVEN* enemy Weapon slot with `Hp=10, Plating=3, Corroded=2`, *WHEN* a 6-damage card resolves against it, *THEN* Plating=0, Hp=5, DamageState transitions Functional→Degraded, and `SlotStateChanged(Weapon, Functional, Degraded)` fires exactly once.

**AC-CC10** — *GIVEN* a slot with `Hp=1, Plating=0`, *WHEN* a 99-damage card resolves, *THEN* Hp=0, DamageState=Offline, and if slot==Frame then `Vehicle.IsDead == true`.

**AC-CC11** — *GIVEN* enemy Frame with `Hp=4`, *WHEN* a card with effects `[DamageEffectSO(5), ApplyStatusEffectSO(Burning, 3)]` resolves, *THEN* Frame reaches Offline after effect 1, `CombatLoop` transitions to `Ended` immediately, Burning is NOT applied, and `CombatEnded(winner=Player)` fires exactly once.

### Enrage (F-CC2 / R7)

**AC-CC12** — *GIVEN* default tuning (`EnrageTurn=8, EffectiveEnrageBaseBonus=+2, EnrageEscalation=+1`), *WHEN* enemy deals base 5 damage on turn 7, *THEN* effective damage input to F-CC1 is 5 (Enrage inactive).

**AC-CC13** — *GIVEN* default tuning, *WHEN* enemy deals base 5 damage on turn 8, *THEN* effective damage input to F-CC1 is 7 (5 + 2 Enrage).

**AC-CC14** — *GIVEN* default tuning, *WHEN* enemy deals base 5 damage on turn 12, *THEN* effective damage input to F-CC1 is 11 (5 + 2 + 4 escalation).

**AC-CC15** — *GIVEN* default tuning, *WHEN* `TurnCount == 6`, *THEN* `EnrageTelegraphStarted` has fired exactly once.

**AC-CC16** — *GIVEN* enemy is Stalled on turn 8, *WHEN* turn 8 resolves, *THEN* enemy turn is skipped, no damage fires, and on turn 9 `EnrageBonus(9) == +3` (escalation continues regardless of skip).

### Redirected RNG (F-CC3 / R8)

**AC-CC17** — *GIVEN* a Vehicle with all 4 slots Functional and `new System.Random(seed: 42)`, *WHEN* 10,000 Redirected selections run, *THEN* each of the 4 slots (Weapon, Engine, Mobility, Frame) is selected with frequency 25% ± 1 percentage point.

**AC-CC18** — *GIVEN* a Vehicle with Weapon Offline and 3 slots Functional (Engine, Mobility, Frame), *WHEN* a Redirected selection runs, *THEN* the returned slot is never Weapon and each of the other 3 slots has 33% probability (tested across 10,000 iterations ± 1pp).

**AC-CC19** — *GIVEN* two test runs with identical `runSeed`, `combatNodeIndex`, and redirect-call sequence, *WHEN* both runs execute the same combat turn by turn, *THEN* Redirected selections return identical slots on every matching call (bit-for-bit determinism).

### Status Tick (R6)

**AC-CC20** — *GIVEN* enemy has Burning 3 (Duration 2), *WHEN* end-of-enemy-turn resolves, *THEN* Frame takes Burning damage FIRST, then Burning Duration decrements to 1 (fire-before-tick).

**AC-CC21** — *GIVEN* Burning 5 applied to enemy Frame with `Hp=5, Plating=0`, *WHEN* end-of-player-turn resolves on the turn Burning was applied, *THEN* Frame Hp=0, DamageState=Offline, enemy `IsDead=true`, and `CombatEnded(winner=Player)` fires.

**AC-CC22** — *GIVEN* a status applied to an Offline slot (EC-CC9 / OQ-SE4 closure), *WHEN* end-of-turn tick fires, *THEN* the status's fire effect resolves against the Offline slot (no-op mechanically because Hp is floored at 0), Duration decrements normally, and the slot's DamageState remains Offline.

### Mid-Resolution Mutation (R10)

**AC-CC23** — *GIVEN* a card with effects `[DrawCardsEffectSO(2), DamageEffectSO(X) with Condition "hand.Count >= 5"]`, player starts turn with 3 cards in hand, *WHEN* card resolves, *THEN* 2 cards are drawn first, hand.Count == 5, the condition evaluates true, and damage resolves (in-flight effect list sees updated state).

### Combat RNG Determinism

**AC-CC24** — *GIVEN* two identical run seeds and identical player inputs replayed card-by-card, *WHEN* both runs execute combat 1, *THEN* final state (HP/Plating/Status on every slot of both vehicles, hand/deck/discard/exhausted contents, TurnCount, Energy) is bit-for-bit identical.

### Position State (S2)

**AC-CC25** — *GIVEN* combat Setup with default `DefaultStartingPosition=Behind`, *WHEN* Setup completes, *THEN* `PositionState.Position == Behind` and no `PositionChanged` event has fired.

**AC-CC26** — *GIVEN* `PositionState.Position == Behind`, *WHEN* `ShiftPositionEffectSO(Ahead)` resolves, *THEN* `Position == Ahead`, `PositionChanged(Behind, Ahead)` fires exactly once; a subsequent `ShiftPositionEffectSO(Ahead)` fires NO event (no-op).

### Combat End Conditions

**AC-CC27** — *GIVEN* player Frame reaches Offline mid-enemy-turn, *WHEN* R5 step 4 resolves, *THEN* `CombatLoop` transitions directly to `Ended`, `CombatEnded(winner=Enemy)` fires exactly once, and no subsequent effects or phases resolve.

**AC-CC28** — *GIVEN* player plays a lethal card mid-`PlayerTurn`, *WHEN* enemy dies, *THEN* `CombatLoop` transitions directly to `Ended` — `PlayerResolve` phase is skipped, no end-of-turn status tick runs on the player, and `CombatEnded(winner=Player)` fires exactly once.

### Event Emission Contract

**AC-CC29** — *GIVEN* a complete combat from Setup to `Ended` with 3 card plays, *WHEN* event log is inspected, *THEN* emission order matches: `CombatStarted`, `PlayerTurnStarted`, `CardPlayed×3` (interleaved with `SlotStateChanged` and `StatusApplied`), `PlayerTurnEnded`, `EnemyTurnEnded`, `PlayerTurnStarted`, …, `CombatEnded`.

**AC-CC30** — *GIVEN* the compiled combat assembly, *WHEN* a `grep` scans for `UnityEngine.Events.UnityEvent` references, *THEN* zero matches are found in any Card Combat source file (forbidden-pattern enforcement).

### Code Review Gates (Architectural Compliance)

**AC-CC31** — *GIVEN* a code review of `CombatLoop.cs`, *WHEN* reviewer searches for direct field access to `Vehicle.Slots[*].Hp`, *THEN* zero matches exist — all reads go through `IVehicleView`, all writes through `IVehicleMutator`.

**AC-CC32** — *GIVEN* the project structure, *WHEN* reviewer verifies the three-sub-model split, *THEN* `CombatLoop` class contains no `SubsystemState` fields (references Vehicle via interface only), `PositionState` is a standalone named type on `Vehicle`, and `SubsystemState` lives on `Vehicle.Slots[]` per V&P GDD (three sub-models are disjoint, per C1 scoping note).

### Armor Layer (armor-model retrofit, 2026-04-21)

**AC-CC33** — *GIVEN* enemy Frame with `CurrentArmor = 4, MaxArmor = 8, Hp = 16`, *WHEN* a 6-damage card (no Corrode) resolves against Frame using F-CC1 Frame path, *THEN* `vehicle.CurrentArmor == 0` (4 consumed), `damage_after_shield == 2`, and `Frame.Hp == 14`. Mirrors V&P AC-VP36.

**AC-CC34** — *GIVEN* a non-Frame slot (e.g., Engine) with `PlatingStacks = 2, Hp = 12`, *WHEN* a 5-damage card resolves against it using F-CC1 Non-Frame path, *THEN* `vehicle.CurrentArmor` is unchanged (Armor not involved), `PlatingStacks == 0` (2 consumed), `damage_after_shield == 3`, and `Engine.Hp == 9`.

**AC-CC35** — *GIVEN* enemy Frame with `CurrentArmor = 4` and a Burning status (3 stacks) on Frame, *WHEN* end-of-enemy-turn resolves and the Burning tick fires as `DamageSource.Status`, *THEN* `vehicle.CurrentArmor` remains 4 (unchanged — DOT bypasses Armor), `Frame.Hp` decreases by the Burning tick amount, and `OnCurrentArmorChanged` does NOT fire. Mirrors V&P AC-VP40.

**AC-CC36** — *GIVEN* `RestoreArmorEffectSO` with `amount = 10` resolves when `CurrentArmor = 5, MaxArmor = 8`, *THEN* `CurrentArmor == 8` (hard cap, overflow discarded), `OnCurrentArmorChanged(8)` fires exactly once, and no exception is thrown. Mirrors V&P AC-VP41.

**AC-CC37** — *GIVEN* enemy has `CurrentArmor = 6, MaxArmor = 8`, a player card takes the enemy Engine Offline, and Engine has `ArmorContribution = 3`, *WHEN* `ApplyDamage` resolves, *THEN* per EC-CC22 / EC-VP20 event order: `MaxArmor == 5`, `CurrentArmor == 5` (clamped from 6), `OnMaxArmorChanged(5)` fires, `OnCurrentArmorChanged(5)` fires, and both fire before granted-card removal. Mirrors V&P AC-VP38.

**AC-CC38** — *GIVEN* a fresh combat Setup where player vehicle had `CurrentArmor = 2` at the end of the previous combat (hypothetical persistence check), *WHEN* `CombatLoop.Setup()` runs, *THEN* `CurrentArmor == MaxArmor` (reset), confirming no persistence of Armor state across combat boundaries. Mirrors V&P AC-VP37.

**AC-CC39** — *GIVEN* an enemy vehicle, *WHEN* inspected via `IVehicleView`, *THEN* `MaxArmor >= 0` and `CurrentArmor` is in `[0..MaxArmor]`, confirming enemy vehicles carry the same Armor semantics as the player vehicle. Mirrors V&P AC-VP42.

**AC-CC40** — *GIVEN* a subsystem-strike card (`DamageEffectSO.BypassPlating = true`) with `amount = 6` resolves against a non-Frame slot with `PlatingStacks = 4, Hp = 12` (no Corrode), *WHEN* F-CC1 non-Frame path runs, *THEN* step 2 is skipped, `PlatingStacks` remains 4 (unchanged), `damage_after_shield == 6`, and `slot.Hp == 6`. Closes OQ-CC-NEW-1.

**AC-CC41** — *GIVEN* any subsystem-strike card (`BypassPlating = true`), *WHEN* the player attempts to target Frame at R3 step 3, *THEN* the card's `ValidSubsystemTargets` excludes Frame and the targeting UI refuses to select Frame. If targeting is forced (e.g., via AoE or test bypass), F-CC1 treats the hit as non-Frame-path only — subsystem-strike cannot interact with Armor.

### Position & Encounter ACs (retrofit 2026-04-24, closes OQ-CC-NEW-5)

**AC-CC42** — *GIVEN* `EncounterType == Standard`, *WHEN* `CombatLoop.Setup()` completes, *THEN* `PositionState.Position == Behind`, the first `Phase` transition is → `PlayerTurn`, and no `EnemyTurnStarted` has fired during Setup.

**AC-CC43** — *GIVEN* `EncounterType == Ambush`, *WHEN* `CombatLoop.Setup()` completes, *THEN* `PositionState.Position == Ahead`, the enemy telegraph is rendered on the enemy vehicle, `EnemyTurnStarted` → intent resolution → `EnemyTurnEnded` fire in order during Setup, and `Phase` then transitions to `PlayerTurn`.

**AC-CC44** — *GIVEN* an archetype with intents `{Damage=5 RequiresAhead, Damage=3 RequiresBehind}` and enemy currently Ahead, *WHEN* `SelectNextIntent` runs, *THEN* the R17 filtered pool contains only `Damage=5 RequiresAhead` and that intent is selected.

**AC-CC45** — *GIVEN* an archetype with intents `{Damage=5 RequiresAhead, Damage=3 RequiresAhead}` and enemy currently Behind, *WHEN* `SelectNextIntent` runs, *THEN* the R17 filtered pool is empty, `RepositionIntent(TargetPosition=Ahead)` is injected and telegraphed, the enemy turn ends with zero damage, and `PositionChanged(Behind, Ahead)` fires exactly once on the relevant vehicle.

**AC-CC46** — *GIVEN* intent `{Damage=3, PositionBonus=4, PositionRequirement=BonusIfBehind}` and enemy Behind, *WHEN* the intent resolves, *THEN* F-CC5 outputs `effective_damage == 7` and feeds that value into F-CC1 as `base_damage`.

**AC-CC47** — *GIVEN* the same intent with enemy Ahead, *WHEN* it resolves, *THEN* F-CC5 outputs `effective_damage == 3` (no bonus) AND the intent still fires (Bonus failure does not gate resolution, only the bonus is missed).

**AC-CC48** — *GIVEN* enemy Ahead at end of turn 1 pre-selected `Damage=5 RequiresAhead`; during turn 2 the player plays `ShiftPositionEffectSO(Ahead)` which flips player Behind→Ahead and (by S2 inversion) enemy Ahead→Behind, *WHEN* enemy resolution runs, *THEN* Card Combat calls `IEnemyBrain.OnInvalidTarget(original, context)` exactly once, the returned replacement intent resolves in place of the original, and the original `RequiresAhead` intent never fires.

### Playtest-Only ACs

**AC-CC-PT1** *(playtest)* — In a 20-combat playtest session, the average combat duration is 5–8 turns (anti-stall Enrage pacing).

**AC-CC-PT2** *(playtest)* — When shown a telegraphed enemy intent, 90% of playtesters correctly identify which slot the enemy will hit before playing a card (readability contract / Pillar 3).

## Open Questions

### OQ-CC1 — Enemy retargeting policy on Offline target

**Scope**: R4 / EC-CC6 define the hook (`IEnemyBrain.OnInvalidTarget(context)`) Card Combat calls when a telegraphed intent's target slot becomes Offline between telegraph and resolution. Card Combat does **not** decide the fallback policy.

**EA default locked here**: retarget to Frame (simple, predictable, never-invalid).

**Unresolved**: per-archetype fallback behavior (e.g., should a Hunter archetype prefer the next-lowest-HP slot instead of Frame? should a Brute always Frame-smash regardless?). These are Enemy System GDD concerns.

**Resolves in**: Enemy System GDD (Not Started).

---

### OQ-CC2 — Combat HUD event animation specifics

**Scope**: The Visual/Audio Requirements section defines *which* events must produce visible responses. The *specifics* — damage-number tween curves, intent-icon pulse rate, Enrage warning visual intensity gradient across `EnrageTelegraphLeadTurns`, Exhaust particle shape — are not locked.

**Unresolved**: full animation and timing spec per event. Card Combat owns the event emission contract; the HUD owns the presentation.

**Resolves in**: Combat HUD UX Spec / Combat HUD GDD (Not Started).

---

### OQ-CC3 — Mid-combat save point policy

**Scope**: Card Combat does not support mid-combat saves in EA (consistent with V&P EC-VP19 conditional). Whether saves can be triggered between combats only (node map transitions) or also during loot/reward screens (after `CombatEnded` fires but before node map returns) is a Save & Persistence concern.

**EA default locked here**: `CombatStarted` locks saves; `CombatEnded` unlocks them. Loot/Reward interstitial save-lock status is TBD.

**Resolves in**: Save & Persistence GDD (Approved — needs a small retrofit note on combat boundaries, or a follow-up ADR if the lock semantics get complex).

---

### OQ-CC4 — Combat RNG stream isolation (forward-deferred)

**Scope**: Current design uses a single `System.Random` instance (`CombatRng`) for all combat randomness: Redirected target selection (F-CC3), enemy AI decision rolls, future crit rolls, future loot-rolls-during-combat. Determinism holds as long as the sequence of `Next()` calls is identical across replays.

**Risk**: if a future tooling need (replay scrubber, partial-determinism debugging) wants to isolate Redirected's RNG stream from enemy-AI's RNG stream, a seed-per-domain split (e.g., `RedirectRng`, `EnemyAiRng`, `LootRng`) would be required — and changing the seeding scheme later would break save-replay compatibility.

**Decision deferred**: until the first replay/debug-tooling milestone surfaces a concrete requirement. For EA, a single stream is sufficient and keeps `CombatRulesSO.CombatRngSeedFormula` simple.

**Resolves in**: first replay/debug tooling milestone (post-EA — not blocking).

---

### Forward Dependencies Resolved by the Armor Retrofit (2026-04-21)

- **V&P armor-model stress-test T-4** — F-CC1 now forks on Frame vs non-Frame path with explicit formulas, three worked examples, and matching acceptance criteria (AC-CC33–AC-CC39). V&P's forward dependency on Card Combat for this rule is now satisfied; the V&P "Closed in this GDD" T-4 entry cross-links here.

### New Open Questions Introduced by the Armor Retrofit

#### OQ-CC-NEW-1 — Subsystem-strike (Armor Piercer successor) — does it bypass Plating?

**Resolved 2026-04-21 (user directive)**: YES — subsystem-strike bypasses `PlatingStacks` on the target non-Frame slot. Targeting is restricted to non-Frame slots only (Frame cannot be selected). Corrode still applies (only DOT skips Corrode).

**Implementation**: `DamageEffectSO` gains a `bool BypassPlating` flag (authored on card definitions in Card System GDD). When set to `true`, F-CC1 non-Frame step 2 is skipped. See F-CC1 subsystem-strike exception and AC-CC40 / AC-CC41. Card System GDD retrofit (task 22) must add the flag and the targeting constraint.

**Status**: Closed. No further design decision needed.

---

#### OQ-CC-NEW-2 — `IVehicleMutator` combat-start Armor reset mechanism

**Resolved 2026-04-21 (user directive, Unity refactor scope gate)**: `IVehicleMutator` exposes `AddArmor(int)` **only** (no reset method on the mutator interface). The combat-start reset lives on `CombatLoop` (playing the CombatManager role) and is performed via a non-mutator `Vehicle.ResetArmor()` method — `CombatLoop.Start()` calls `player.ResetArmor()` and `enemy.ResetArmor()` after phase transition to `PlayerTurn`. Rationale: lifecycle concerns (combat-start reset) belong to the combat orchestrator; the mutator interface stays narrow and intention-revealing (damage + restoration only). This is a fourth option not in the original three — neither (A) ResetCurrentArmor on mutator, nor (B) AddArmor(int.MaxValue) abuse, nor (C) event subscription.

**Implementation**: `Vehicle.ResetArmor()` is public but NOT on `IVehicleMutator`. Sets `CurrentArmor = MaxArmor` directly. Callers: `CombatLoop.Start()` only. Tests may call it to seed `CurrentArmor = MaxArmor` for damage-pipeline scenarios.

**Status**: Closed.

---

### New Open Questions Introduced by the Combat Readability Prototype (2026-04-24)

These questions were surfaced by the combat readability prototype (v3) and flagged by the CD-PLAYTEST gate as conditions that must be at minimum *named* before Combat HUD UX spec authoring begins. Each is currently **TBD** — the prototype's 5/5 gauntlet result rested on prototype-authored rules the GDD does not yet specify, and encoding those rules into HUD visual language without a GDD decision would create rework.

See `prototypes/combat/REPORT.md` "If Proceeding (post-conditions)" section for the prototype-side context.

#### OQ-CC-NEW-3 — Enemy Weapon Offline global damage modifier

**Scope**: When an enemy vehicle's Weapon slot reaches Offline, does the enemy's subsequent damage output receive a global modifier (e.g., -50%)? Card Combat currently specifies only that an intent explicitly targeting an Offline slot triggers retargeting (R4 / EC-CC6 / OQ-CC1). It does **not** specify a general damage-output consequence for the attacker when their Weapon slot is destroyed.

**Why this matters**: The combat readability prototype v3 implemented "Weapon Offline → enemy damage × 0.5 globally" to test the encounter-read loop. 4/5 enemies in the playtest gauntlet had their Weapon killed by turn 2-3, and the qualitative win ("targeting felt meaningful") leans heavily on this rule. Without a GDD decision, the HUD spec cannot know whether to render a "Weapon-dead → -50% applied" visual state, and balance will not have a target to tune toward.

**Options to weigh**:
- **A**: Adopt the prototype's "-50% globally while Weapon Offline" as a Card Combat rule. Simple, legible, carries the playtest's qualitative success, but reduces every Weapon-destruction into the same uniform effect.
- **B**: Per-enemy or per-archetype damage modifier (Brute = -25%, Artillery = -75%, etc.) authored on the enemy definition. More identity-per-enemy, more tuning surface.
- **C**: No global modifier; Weapon Offline only blocks intents specifically authored to target Weapon (current GDD reading). Targeting enemy Weapon becomes a much weaker play and the prototype's 4/5 Weapon-kill result will not generalize — the HUD should then NOT highlight Weapon as a distinct targeting priority.
- **D** (chosen): **Symmetric fizzle.** Weapon Offline causes all attack-family intents (Attack, Multi-Attack, Debuff-Attack) to **fizzle** on execution — no damage, no status effects applied, the turn is wasted. This is the enemy-side mirror of the player rule "Weapon Offline → attack cards cannot be played." Neither side has a fuzzy percentage modifier; the effect is a discrete on/off state that matches how the player experiences their own Weapon destruction.

**Resolves in**: Card Combat GDD retrofit OR Enemy System GDD (whichever owns cross-cutting damage modifiers). **Blocks**: Combat HUD UX spec authoring of any Weapon-status visual state.

**Status**: **Resolved (2026-04-24)** — Option D (Symmetric fizzle).

**Resolution**:
- Enemy Weapon Offline → Attack / Multi-Attack / Debuff-Attack intents fizzle (0 damage, no Weaken/status applied, turn wasted).
- Player Weapon Offline → attack cards (`DealsDamage == true`) cannot be played; attempting to play them is rejected in the play-card handler.
- Applies regardless of which side is the attacker. This is the canonical Weapon-slot rule for Card Combat.
- HUD implication: Weapon-status is a binary state (`ALIVE` vs `BROKEN → FIZZLE`). When broken, the intent telegraph for attack-family intents must render with a `FIZZLE` badge so the player can read "this hit is neutralised" at a glance.
- **Does not** introduce a `-50%` damage multiplier anywhere in the game. The v3 prototype's `-50%` rule is deprecated.
- Tuning implication: every enemy's kill-pressure lives primarily in its Weapon subsystem, so Weapon-HP is the dominant "how long can this enemy threaten me" knob.

---

#### OQ-CC-NEW-4 — Distinct mechanical consequences for Engine / Mobility Offline (enemy side)

**Scope**: The Card Combat GDD specifies Frame Offline = combat-end-trigger, but does NOT specify distinct mechanical consequences for other enemy subsystem slots (Engine, Mobility) going Offline. In v3 prototype these were treated as HP pools only (destruction was "just a step toward Frame death via splash").

**Why this matters**: The prototype surfaced a real drift risk: *every enemy in the 5-combat gauntlet had Weapon as the dominant targeting read*, because only Weapon had a destruction consequence. This collapses "Chassis Identity at the encounter layer" (Pillar 2) into "always target Weapon." If Engine Offline and Mobility Offline had distinct effects, different enemies could reward different reads.

**Options to weigh**:
- **A**: Engine Offline = enemy cannot execute Plate intents (loses the defensive play). Mobility Offline = enemy skips every 3rd turn (or fixed skip-pattern). Each destruction is a distinct tactical win.
- **B**: Engine Offline = enemy loses enrage access (cannot transition to Enraged pool). Mobility Offline = enemy's multi-attacks drop to single-hit. Different from A but still distinct per slot.
- **C**: Keep Engine/Mobility as HP pools with no consequences. Accept Weapon-primacy as the designed dominant read. Combat HUD spec should then de-emphasize Engine/Mobility targeting cues.
- **D**: Per-archetype: each enemy archetype authors which subsystem destruction gives what effect, so Chassis Identity shows up at the destruction-consequence layer.
- **E** (chosen): **Symmetric matrix (union of A + B + mirrored player rules).** Enemy and player share the same destruction consequences per subsystem slot. What happens to one side happens identically to the other.

**Resolves in**: Card Combat GDD OR Enemy System GDD. **Blocks**: any HUD visual language that implies per-subsystem tactical significance beyond Frame.

**Status**: **Resolved (2026-04-24)** — Option E (Symmetric matrix).

**Resolution — The Subsystem Consequence Matrix (Canonical)**:

| Slot   | Player Offline                                 | Enemy Offline                                                    |
|--------|------------------------------------------------|------------------------------------------------------------------|
| Weapon | Attack cards cannot be played (see OQ-CC-NEW-3)| Attack / Multi-Attack / Debuff-Attack intents FIZZLE             |
| Engine | Mobility cards cannot be played (see note)     | Plate intents FIZZLE (no armor gained); enrage disabled + un-enrages if already enraged |
| Wheels | Player draws 1 fewer card per turn             | Multi-Attack intents resolve as a single hit (full per-hit dmg)  |
| Frame  | Player dies (combat-end trigger)               | Enemy dies (combat-end trigger)                                  |

**Rule clarifications**:
- "FIZZLE" = the intent executes, consumes the turn, and does nothing. No damage, no status effect, no armor gain. This is intentional — fizzle is legible to the player because the telegraph flipped to `[BROKEN: FIZZLE]` on the prior frame.
- Engine-dead un-enrage: if an enemy is in the Enraged intent pool when its Engine is destroyed, it immediately reverts to the Normal intent pool and its intent cursor resets. It cannot re-enrage while Engine is Offline. Repair of the Engine restores access to enrage at the next trigger check.
- Wheels-dead single-hit: for a Multi-Attack (e.g., MULTI 5×3), the damage per hit is preserved (5) but the hit count becomes 1 (so the MULTI 5×3 resolves as 5 total).
- **Mobility cards [PLACEHOLDER]**: The Card Combat GDD does not yet define a "mobility card" category. The Engine-dead consequence on the player side is specified here but has no enforceable binding until the Card System GDD adds a mobility-tag. Until then, Player Engine Offline only disables the enrage-side consequence (irrelevant for the player) and is otherwise cosmetic. This is a known follow-up.

**HUD implication**:
- Each subsystem slot (Weapon/Engine/Wheels/Frame) has a binary `ALIVE / BROKEN` state that the HUD must render on both sides.
- When an enemy subsystem is BROKEN, the intent telegraph for any intent affected by that subsystem must show the fizzle / single-hit badge.
- When a player subsystem is BROKEN, the card hand must visually disable any card in the affected category (grey out, tooltip explains).

**Tuning implication**: every enemy archetype now has four destruction levers (Weapon, Engine, Wheels, Frame) with real consequences. Archetype identity can be expressed as "which subsystem is the dominant threat to shut down." Example archetype sketches (advisory, not binding):
- *Gunner* chassis: high Weapon HP, low Engine HP → kill Engine to collapse their defense.
- *Tank* chassis: high Engine HP, low Weapon HP → kill Weapon to neutralise offence.
- *Charger* chassis: high Wheels HP, dangerous Multi-Attack intents → kill Wheels to declaw.

---

#### OQ-CC-NEW-5 — Non-player-first encounter types (ambush / surprise)

**Scope**: Tuning Knobs section (line 733) locks turn order as *"player first — Pillar 3 readability contract. Locked."* The combat readability prototype v3 surfaced a design question: does every encounter in the game open with the player's turn, or does an "ambush" encounter type exist where the enemy acts first?

**Why this matters**:
- The prototype's "first-turn Plate is a wasted card" finding is a direct downstream of player-first + full-armor-at-combat-start (EC-CC23). In ambush encounters, turn-1 Plate would be a sensible defensive play.
- Ambush encounters would create meaningful encounter-type variety (*standard* vs *ambush* vs *surprise*) that currently does not exist in any GDD.
- The locked player-first rule is a **pillar-locked decision** (Pillar 3 readability contract), so re-opening it requires deliberate creative-director signoff — this cannot be silently changed.

**Options to weigh**:
- **A**: Keep the player-first rule locked. Accept "first-turn Plate wasted" as a starter-deck / mulligan-layer problem (solve in Card System GDD via starter-deck composition or opening-hand rules).
- **B**: Open a narrow exception: *ambush* encounter type = enemy telegraphs on turn 0, acts on turn 1, player draws and acts on turn 2. Player-first stays the *default*; ambush is a distinct, labeled encounter type surfaced on the node map before entering.
- **C**: Fully fork: encounter type is an axis (Standard / Ambush / Surprise / Standoff) and each type has documented turn-order and telegraph-visibility rules. Heavier scope, more encounter-identity surface.

**Resolves in**: Card Combat GDD (turn order rule) + Node Encounter GDD (encounter-type axis) + pillar review (Pillar 3 readability contract re-evaluation if B or C). **Blocks**: HUD spec can proceed under the locked (A) assumption for EA, but node-map-side visual language for "ambush" encounter pre-warning needs to know the answer before Node UI is authored.

**Status**: **Resolved (2026-04-24) — Option B.** Adopted as the **Ambush** encounter type defined in R15. Player-first remains the default (Standard encounter); Ambush is a labeled, node-map-previewed exception where the enemy resolves a pre-selected, telegraphed first intent during Setup. Pillar 3 readability contract preserved via (a) mandatory node-map pre-warning flag and (b) enemy intent telegraph rendering on the enemy vehicle before damage resolves. Closed by R15 + R16 + R17 + R18 + S2 amendment + F-CC5 + AC-CC42/43/44/45/46/47/48. Downstream impact: HUD spec (task #13) must now render Ambush-state telegraph on enemy vehicle during Setup; Node Map GDD must surface an `EncounterType` flag on node icons; Node Encounter GDD must author the `CombatHandler.Begin(setup)` payload carrying `EncounterType`.
