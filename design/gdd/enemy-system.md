# Enemy System

> **Status**: In Design
> **Author**: user + game-designer + systems-designer + ai-programmer + art-director + qa-lead
> **Last Updated**: 2026-04-23
> **Implements Pillar**: Pillar 3 (Read to Win — PRIMARY, intent data model is the information channel); Pillar 2 (Chassis Identity — secondary, enemy variety prevents same-strategy-wins-all); Pillar 5 (Route Reflects Vehicle State — secondary, per-biome enemy archetypes reward different vehicle states)

## Overview

The Enemy System defines the antagonists of Wasteland Run: every non-player vehicle, raider, and wasteland machine the player fights. Structurally it is the data layer behind combat's second chair — a library of `EnemyDefinitionSO` archetypes (stat blocks, subsystem loadouts, intent decision rules, per-enemy `EnrageTurn` overrides, reward metadata) plus the live `IEnemyBrain` implementations that select the next `EnemyIntent` at the end of each enemy turn and handle the `OnInvalidTarget` retargeting when a telegraphed slot goes Offline mid-player-turn. It owns the per-biome enemy pool (`IEnemyPool`) that Node Encounter consumes when populating a Combat or EliteCombat beacon, and the archetype metadata that Loot & Reward reads when scaling post-combat payouts. Enemies are Vehicles — the same POCO, the same `IVehicleView`/`IVehicleMutator` surface, the same four slots (Weapon / Engine / Mobility / Frame), the same symmetric Armor layer. There is no privileged enemy code path; an enemy card plays through `IVehicleMutator` exactly like a player card does.

In the player's seat the system isn't archetypes — it's the **tell above the vehicle in front of you**. Every turn the Enemy System publishes `NextIntent` before the player commits a card, and what the player reads there (damage, status, self-heal, defend, position shift, target slot) is the primary information channel of Pillar 3 (Read to Win). A losing run is supposed to end on "I misread what it was going to do," not on "there was nothing I could do." So the system's job cuts both ways: it must produce enemies *legible enough* to read on first encounter (Pillar 3) and *varied enough* that the same deck, same targeting plan, same routing habit doesn't clear all three biomes (the MVP surface of Pillar 2 and the seed for Pillar 5's per-biome archetype assignments). Without the Enemy System there is no combat; without distinct enemy archetypes there is no *run*, only a longer fight.

## Player Fantasy

The tell flickers above the wreck in front of you. *Ram. Fourteen. Frame.* The rig rocks on its springs; its front end drops half an inch as the engine builds pressure. Your Frame is already cracked from the last exchange. You have a mid-weight Block in hand and the Engine Overcharge you've been hoarding since Biome 1 — the one you were going to save for the Haven gate. You burn the Overcharge. The Block soaks. Your Frame survives at 3 HP. You did not win that turn. You *saw* that turn coming and paid for it on your terms.

That is the fantasy the Enemy System exists to deliver: **foresight under pressure.** Not surprise — every enemy telegraphs, and if the player is ever surprised, we failed. Not reflex — the game is turn-based and the turn is as long as the player needs it to be. Not puzzle-solving in the single-correct-answer sense — the tell tells you what's coming, never what you should do about it. The answer lives with the player. The enemy's job is to *ask the question clearly enough that the player can answer it wrong and know it was their own answer.* A losing run ends on "I misread what the raider was going to do," or "I read it and couldn't afford the block." Never on "there was nothing I could do."

Read-to-win is the spine. Variety is the bracing that keeps it from collapsing into pattern-recognition-once-and-done — the Sand Flats raider opens with a Ram feint; the Junk Mesa scavenger builds stacks before committing; the Haven-approach elite chains Enrage off overheated Engine slots. A run clears three biomes only if the player *keeps reading*, not if they memorize one opener. This is how the Enemy System serves Chassis Identity without ever asking which chassis you picked: the question changes often enough that the same deck, same targeting plan, same routing habit cannot answer all of them.

And when the raider's Engine finally glows red — the same overheat your own rig shows when you've overcharged two turns in a row — you recognize the state, because you have *been* in that state. The rig in front of you has four slots, like yours. Frame-only armor, like yours. An engine that runs hot when pushed. It was built in the same wasteland from the same scrap, and it breaks the same way. Read it like you read yourself.

## Detailed Design

### Core Rules

**C.1 — Enemy = Vehicle.** An enemy is a Vehicle in every structural sense. Identical `VehicleState` POCO, four slots (Weapon / Engine / Mobility / Frame), vehicle-level MaxArmor from part contributions, Frame-only Armor protection, the same `IVehicleView` / `IVehicleMutator` surface. No privileged enemy code path. Rules out: enemy-only HP pools, enemy-side damage resolution, enemy-exclusive slot types, any mutation that bypasses `IVehicleMutator`.

**C.2 — Archetype Model (`EnemyDefinitionSO`).** Single authoritative data asset per archetype. Authors exactly:

- Stat block (MaxArmor contribution per part, base HP per slot, `EnrageTurn` override)
- Subsystem loadout (`PartDefinitionSO` references per slot)
- `BrainRuleset` reference (weighted intent candidates + context-conditional weight modifiers; see C.5)
- `RetargetPolicy` enum field (default `Frame`; see C.6)
- `ArchetypeTags` (string[], e.g. `["Raider","Biome1","Elite"]`) + `DifficultyScore` (float)
- **Art-contract fields (mandatory)**: `SilhouetteClass` enum (Small / Medium / Large), `VisualFamily` enum (Raider / Scavenger / Elite / Boss). These do not drive gameplay — they exist so Section H can specify per-class sprite budgets and per-family biome skinning without a breaking data migration later. **Name note (cross-review arbitration 2026-04-24, BLOCKER-3)**: this field was previously `ArchetypeFamily`. Renamed to disambiguate from Loot & Reward's gameplay-lookup `ArchetypeFamily` axis (see next bullet).
- **Gameplay-axis field (mandatory)**: `ArchetypeFamily` enum (Raider / Patcher / PitPacker / Elite / Boss). Owned by Loot & Reward; Enemy System SOs populate this field to key drop-table lookups + AI behavior family. Separate from `VisualFamily` so that two enemies with the same silhouette read (e.g., both `VisualFamily = Raider`) can have different gameplay roles (e.g., `ArchetypeFamily = Patcher` vs `ArchetypeFamily = Raider`). SO validator (see G.4) must reject any enemy where either field is null. See F.1 (Dependencies) for the L&R bidirectional link.
- **Position-axis fields (Card Combat R17 retrofit 2026-04-24)**: `PreferredPosition` enum (`Ahead` / `Behind` / `None`). Informs the Reposition-fallback direction choice — biases, does not gate. Bomb-lobber archetypes use `Behind`; lancer archetypes use `Ahead`; brawler archetypes use `None`. Full authoring guidance in E.N.
- **Reserved field**: `CanReposition: bool` (default `true`). Reserved for future stationary archetypes (turret-style enemies). Out of scope for EA content — all EA archetypes MUST leave this `true`. SO validator warns if `false` is combined with any `RequiresAhead`/`RequiresBehind` intent.

Does NOT author: runtime instance state (lives in `VehicleState`), biome pool assignments (live on `BiomePoolSO`), cross-enemy balance constants (live on `CombatConfig`).

**C.3 — Intent Data Model.**

```csharp
public struct EnemyIntent
{
    public IntentType     Type;          // Damage | Status | Heal | Defend | PositionShift | Utility
    public TargetType     Target;        // PlayerSlot | Self | None
    public SlotId         TargetSlot;    // Weapon | Engine | Mobility | Frame | None (required when Target == PlayerSlot)

    // HUD-facing predictions; not evaluated during resolution.
    public int            PredictedDamage;
    public int            PredictedStatusStacks;
    public StatusEffectSO StatusRef;

    // Payload — interpret per IntentType:
    //   Damage        -> PredictedDamage (raw, pre-Armor)
    //   Status        -> StatusRef + PredictedStatusStacks
    //   Heal          -> HealAmount (Target = Self)
    //   Defend        -> ArmorGain (Target = Self)
    //   PositionShift -> TargetPosition (synthetic RepositionIntent only — see R18)
    //   Utility       -> free-form; HUD uses intent name from BrainRuleset label
    public int            ArmorGain;
    public int            HealAmount;

    // Position model (Card Combat R16 retrofit 2026-04-24)
    public IntentPositionRequirement PositionRequirement;  // None | RequiresAhead | RequiresBehind | BonusIfAhead | BonusIfBehind
    public int                       PositionBonus;        // added to PredictedDamage when BonusCondition met (Card Combat F-CC5)
    public PositionState?            TargetPosition;       // populated only on synthetic RepositionIntent (Card Combat R18)
}
```

`PredictedDamage` and `PredictedStatusStacks` are HUD/Loot-scaling data only. Actual resolution runs through `IVehicleMutator` — no field on `EnemyIntent` is evaluated during damage math.

**C.3.1 — Position Fields (Card Combat R16 retrofit 2026-04-24).** `PositionRequirement` mirrors `CardDefinitionSO.PositionRequirement` (Card System) with two added axes (`BonusIfAhead`, `BonusIfBehind`). Enemy position is the logical inverse of `playerVehicle.PositionState.Position` (Card Combat S2) — Enemy System never stores enemy position as an independent value. `Requires*` intents are filtered out of the candidate pool when the condition fails (Card Combat R17 pool-filter); they are never "attempted and missed" — if Requires fails, the intent is simply not selected. `BonusIf*` intents always fire at base `PredictedDamage`; the bonus is added per Card Combat F-CC5 when the bonus condition is met. Authoring constraint: `PositionBonus` MUST be `0` when `PositionRequirement ∈ {None, RequiresAhead, RequiresBehind}` — bonus is only meaningful on `BonusIf*`. SO import validator rejects mismatched combinations.

`TargetPosition` is populated only on the synthetic `RepositionIntent` (Card Combat R18), which Card Combat injects when the pool-filter produces an empty set. Enemy System / `BrainRuleset` **never authors** `RepositionIntent` — it is not in any authored candidate list. From the brain's perspective, Reposition is an invisible fallback handled entirely by the loop.

**C.4 — Telegraph Contract.** `NextIntent` MUST be non-null on the enemy `VehicleState` at the moment the player turn begins. Combat Loop enforces as a hard invariant — null triggers `InvalidCombatStateException` and halts; no silent default. Brain is invoked for turn-1 intent during combat setup (before first player turn), then at the end of each subsequent enemy turn. The player never faces an enemy with no visible intent.

**C.5 — Intent Selection.** Brain selects via weighted decision rules in the `BrainRuleset` asset referenced on `EnemyDefinitionSO`. `BrainRuleset` is pure data: ordered intent candidates, each with a base weight and a set of context-conditional multipliers. `IEnemyBrain.SelectNextIntent(CombatContext)` is a pure function — receives both vehicle states, `TurnCount`, `EnrageActive`, the seeded `CombatRng` (inside `CombatContext`), and status stacks; returns an `EnemyIntent` and mutates no external state. Weighted draw uses `CombatRng` (deterministic, replayable). Weight modifiers evaluate top-to-bottom; conditions reference `CombatContext` fields only — no reflection, no global state.

**Position-axis filtering (Card Combat R17 retrofit 2026-04-24).** Before the weighted draw evaluates `WeightModifier` conditions, the candidate list is **pool-filtered** to remove any intent where `PositionRequirement ∈ {RequiresAhead, RequiresBehind}` and the current enemy position does not satisfy the requirement. The filtered pool is what the weighted draw sees. If the filtered pool is empty, Card Combat (not the brain) injects the synthetic `RepositionIntent` per Card Combat R18 — the brain never authors or returns Reposition directly. If the filtered pool is empty AND the archetype has `CanReposition == false` (future stationary archetypes only), `OnInvalidTarget` is called and may return `NoOpIntent` per C.6 / EC2 degenerate-pool handling.

**C.6 — Retargeting Rule (closes OQ-CC1).** When the player renders the telegraphed `TargetSlot` Offline before the enemy's turn resolves, Combat Loop calls `IEnemyBrain.OnInvalidTarget(CombatContext)` exactly once. Default policy (all archetypes that don't override): retarget to Frame. Frame is the default because it is always present (never part-dependent) and aligns with the wasteland-scavenging aesthetic — attackers go for the chassis. Per-archetype override via `EnemyDefinitionSO.RetargetPolicy`, which may name any `SlotId` or a runtime priority list evaluated against `CombatContext`. A retarget call may resolve only to a slot that is Online or Damaged — never Offline. If every slot is Offline, `OnInvalidTarget` returns the `NoOpIntent` sentinel (`Type = Utility, Target = None`) and logs a warning; full degenerate-slot spec in Section E.

**Position-invalidation retarget (Card Combat EC-CC28 retrofit 2026-04-24).** If a pre-selected `Requires*` intent was valid at end-of-turn selection but invalid at resolution time (player played `ShiftPositionEffectSO` during their turn that flipped enemy position), Card Combat calls `OnInvalidTarget(CombatContext)` following the same retarget path as an Offline `TargetSlot`. Brain returns a replacement intent per the archetype's `RetargetPolicy`, or `NoOpIntent` if no valid replacement exists. Card Combat does NOT re-run the full R17 pool-filter at resolution time — the retarget hook is the single replacement path. This keeps `OnInvalidTarget` as the one authoritative "intent became invalid" hook, regardless of whether the cause is Offline slot or flipped position.

**C.7 — Enrage Rule.** At the start of the enemy's turn, if `TurnCount >= (EnemyDefinitionSO.EnrageTurn ?? DefaultEnrageTurn)` (default 8), `EnrageActive` is set on that enemy for the remainder of combat. Cannot be dispelled by any status or card. `Stalled` does not pause `TurnCount` — Enrage advances regardless of whether the enemy acted. Damage / behavior math: Section D. Stored as a boolean `EnrageActive` on the runtime combat record, not on `VehicleState`.

**C.8 — Status Effects Parity.** Enemies apply and receive status effects through the identical `ApplyStatusEffectSO` pipeline used for the player. Duration cap `RemainingDuration = 3` applies symmetrically. Fire-before-tick ordering on the enemy's turn mirrors the player's turn. Zero enemy-side duplicate code.

**C.9 — Mutator Parity.** All enemy-produced effects — damage to player slots, status on player, self-heal, self-Armor gain, position shift — route through `IVehicleMutator`. An enemy "playing a card" is not metaphorical: brain resolves the selected intent by constructing equivalent effect parameters and invoking `IVehicleMutator` on the target vehicle (player or self), identically to player card resolution via `DamageEffectSO` / `HealEffectSO`. Enforces symmetric Armor math (AC-CC33–39), symmetric `BypassPlating`, symmetric DOT bypass.

**C.10 — Per-Biome Pool Rule.** `IEnemyPool.GetEnemyFor(int biomeIndex, BeaconType beaconType, System.Random runSeed)` returns a single `EnemyDefinitionSO`. Pool composition is authored on `BiomePoolSO` data assets, one per biome. `IEnemyPool` performs a seeded weighted draw using `runSeed`; no authorship decisions at runtime. **MVP default: archetypes are biome-exclusive** — each `EnemyDefinitionSO` appears in exactly one biome's `BiomePoolSO`. Silhouette and material read *are* the biome's visual vocabulary. Post-MVP upgrade path (shared archetypes + render-time biome motif layer) is deferred; the `BiomePoolSO` shape must not preclude it, but the MVP pipeline has no motif-injection system. Node Encounter owns when to call; Enemy System owns what can be returned. `BiomePoolSO` assets in `assets/data/enemies/pools/` are content, not this GDD's authoring surface.

**C.11 — Reward Metadata Rule.** On combat end, Enemy System populates `CombatEndedPayload` on the `OnCombatEnded` event for Loot & Reward:

```csharp
public record CombatEndedPayload(
    string                ArchetypeId,
    IReadOnlyList<string> ArchetypeTags,
    float                 DifficultyScore,
    int                   BiomeIndex,
    bool                  WasElite,
    bool                  EnrageReached,          // true if EnrageActive was ever set this combat
    int                   TurnsToKill,            // TurnCount at combat end
    bool                  PlayerVehicleCritical   // true if any player slot ended Offline
);
```

Enemy System guarantees payload accuracy. Loot & Reward is solely responsible for translating payload into reward magnitudes. Enemy System has zero knowledge of reward tables or currencies.

**C.12 — No Content Roster in This GDD.** This GDD defines the framework — data contracts, selection rules, behavioral invariants. Specific stat values, `BrainRuleset` weights, `DifficultyScore` assignments, biome pool memberships live in data assets and are balanced via the vertical-slice balance sim (Section D). If a rule in this GDD conflicts with a value in a data asset, this GDD is authoritative.

### States and Transitions

**Enemy Turn Lifecycle States**

| State | Entered When | Exited When | Brain Calls | Notes |
|---|---|---|---|---|
| **Spawned** | `EnemyDefinitionSO` instantiated by `IEnemyPool`; VehicleState hydrated, slots Online, HP at max | Immediately, when bootstrap sequence runs | None | Transient — one init frame. No `NextIntent` yet; telegraph contract not yet active. |
| **SelectingFirstIntent** | From Spawned during combat setup, before player's turn 1 | To **Telegraphed** once brain returns non-null intent | `IEnemyBrain.SelectNextIntent(CombatContext)` | `TurnCount = 0`. Loop asserts non-null return before advancing. |
| **Telegraphed** | `NextIntent` published; player's turn is active | Branch at enemy turn start: Stalled active → **Stalled_Skipping**; else `TargetSlot` Offline → **Retargeting**; else → **Resolving** | None (brain idle during player turn) | `NextIntent` visible in Combat HUD — primary information channel of Pillar 3. Persists across the full player turn. |
| **Retargeting** | Enemy turn begins; `NextIntent.TargetSlot` is Offline | To **Resolving** after exactly one `OnInvalidTarget` call | `IEnemyBrain.OnInvalidTarget(CombatContext)` | Fires exactly once per enemy turn. Brain may re-target a different slot or switch intent type entirely. Degenerate all-Offline case returns `NoOpIntent`; Section E owns full spec. |
| **Resolving** | Enemy turn: intent passes through `IVehicleMutator` | To **SelectingNextIntent** after effect chain + status ticks complete; to **EndOfCombat** if HP ≤ 0 detected | None during execution; `IVehicleMutator` is the executor | All damage/status/position effects route through the same mutator pipeline as player cards. Enrage damage modifiers (if `EnrageActive`) applied here per Section D. |
| **SelectingNextIntent** | End of Resolving or Stalled_Skipping, after current turn's chain closes | To **Telegraphed** once brain returns next intent; to **EndOfCombat** if HP ≤ 0 detected before brain call | `IEnemyBrain.SelectNextIntent(CombatContext)` | `TurnCount` increments here before the brain call — Enrage threshold check happens at this moment. Telegraph Contract: MUST NOT exit to player turn until `NextIntent` is non-null. |
| **Stalled_Skipping** | Enemy turn begins; Stalled active (≥1 charge remaining); charge consumed | To **SelectingNextIntent** | None — intent resolution is skipped | `NextIntent` was already published last turn (player still saw the tell). Intent does NOT play through mutator. Stalled charge = 1 consumed. `TurnCount` advances normally; Enrage can activate on a skipped turn. |
| **EndOfCombat** | HP ≤ 0 on either vehicle, detected during any state | Terminal — no exits | None | Emits `OnCombatEnded(CombatEndedPayload)` exactly once. Combat Loop tears down the enemy instance. |

**Enrage Sub-State**

> Enrage math and per-archetype behavior tuning live in Section D (Formulas) and Section G (Tuning Knobs). This table specifies state semantics only.

| Flag | Activation | Deactivation | Effect (high-level) | Interactions |
|---|---|---|---|---|
| **EnrageActive** | `TurnCount >= (EnemyDefinitionSO.EnrageTurn ?? DefaultEnrageTurn)`; evaluated at top of SelectingNextIntent after `TurnCount` increments | Never — permanent for combat duration | Modifies damage output and/or action pattern per archetype (Section D) | Not blocked by Stalled (TurnCount still increments during Stalled_Skipping). Not a status effect — cannot be dispelled, not subject to the 3-turn cap. |

**Transition Rules**

1. **Single exit or explicit branch.** Spawned → SelectingFirstIntent (unconditional). Telegraphed branches three ways at enemy-turn start: Stalled → Stalled_Skipping; else Offline target → Retargeting; else → Resolving. Resolving → SelectingNextIntent (nominal) or EndOfCombat (HP ≤ 0). SelectingNextIntent → Telegraphed (nominal) or EndOfCombat (HP ≤ 0 before brain call). Stalled_Skipping → SelectingNextIntent (unconditional). EndOfCombat is terminal.
2. **Telegraph invariant.** Any state that hands control back to the player MUST have `NextIntent` non-null before the handoff. Exactly two such handoffs: SelectingFirstIntent → Telegraphed (bootstrap), SelectingNextIntent → Telegraphed (end of each enemy turn). A null return from `SelectNextIntent` is a programming error, not a recoverable state.
3. **Stalled interaction.** Enemy turn with Stalled active collapses to Telegraphed → Stalled_Skipping → SelectingNextIntent. Resolving is bypassed — the telegraphed intent does not execute. Stalled charge consumed = 1. Brain still called in SelectingNextIntent. `TurnCount` advances; Enrage threshold may flip during a Stalled turn.
4. **Retargeting fires exactly once per enemy turn.** On `NextIntent.TargetSlot` Offline at turn start, Combat Loop calls `OnInvalidTarget` once. Replacement proceeds directly to Resolving without a second validity check. Two consecutive checks are forbidden; a replacement that happens to target Offline is handled by Section E's degenerate-slot handler.
5. **All-slots-Offline degenerate edge.** If `OnInvalidTarget` has no valid slot, brain returns `NoOpIntent` sentinel. State machine still transitions Retargeting → Resolving; Resolving receives NoOp and produces no effect. Full spec in Section E (includes HUD update question).
6. **EndOfCombat is terminal.** Entered from Resolving or SelectingNextIntent. If a future status-on-tick effect causes HP loss during Stalled_Skipping or Retargeting, the HP ≤ 0 check still fires there and EndOfCombat is entered immediately. `OnCombatEnded` emits exactly once.
7. **Determinism.** All brain calls receive `CombatContext` carrying the seeded `CombatRng` (`System.Random` with explicit run seed). Brain draws randomness exclusively from `CombatRng`; `UnityEngine.Random` is forbidden. Same seed + same input state → identical transitions across reloads. Hard contract.

### Interactions with Other Systems

**Dependency Matrix**

| System | Direction | Interface(s) | Data Flow | Ownership Boundary |
|---|---|---|---|---|
| **Card Combat** | ↔ | `IEnemyBrain.SelectNextIntent`, `IEnemyBrain.OnInvalidTarget` | Loop passes `CombatContext` (vehicle states, TurnCount, EnrageActive, CombatRng, status stacks, **enemy position per Card Combat S2 inversion**). Brain returns `EnemyIntent`. Loop validates non-null before player turn. **Loop pool-filters candidates on `IntentPositionRequirement` before the weighted draw (Card Combat R17); on empty post-filter pool, Loop authors a synthetic `RepositionIntent` directly (Card Combat R18) — brain is not re-invoked.** | Card Combat owns loop + invariants + encounter type + pool filter + synthetic `RepositionIntent` injection. Enemy System owns decision pipeline, position-axis authoring (`PreferredPosition`, `CanReposition`, `IntentPositionRequirement`, `PositionBonus`), and retarget policy. Brain NEVER drives resolution, authors `RepositionIntent`, or reads its own position — produces candidate data only. |
| **Vehicle & Part** | reads/writes | `IVehicleView` (read), `IVehicleMutator` (write via loop) | Brain reads player and self state via `IVehicleView`. ALL enemy effects route through `IVehicleMutator`. No privileged write path. | V&P owns state mutation. Enemy System owns intent selection. Brain never touches `VehicleState` directly. |
| **Status Effects** | reads / affected by | `ApplyStatusEffectSO`, status stacks on `CombatContext` | Brain reads incoming status from `CombatContext`. Enemy applies status via the same pipeline. Stalled: brain still called, Loop enforces skip. Enrage counter increments regardless of Stalled. | Status Effects owns tick order + duration rules. Enemy System owns how status state influences brain weights. |
| **Card System** | reads | `DamageEffectSO`, `TargetType` enum | Enemy damage values are data-authored on `DamageEffectSO` referenced by intent rules. `TargetType.EnemySubsystem` rules apply in reverse (player slots as targets). | Card System owns damage authority. Enemy System references those data assets; no enemy-side damage duplicate. |
| **Loot & Reward** *(undesigned)* | writes (event) | `event Action<CombatEndedPayload> OnCombatEnded` | On combat end, payload emitted with archetype identity + combat outcome fields. L&R subscribes and scales rewards independently. | Enemy System owns payload composition + emission. L&R owns reward translation. Enemy System has zero knowledge of reward tables. |
| **Node Encounter** *(undesigned)* | writes (implements) | `IEnemyPool.GetEnemyFor(biomeIndex, beaconType, runSeed)` | Node Encounter calls at beacon population. Returns weighted `EnemyDefinitionSO`. | Node Encounter owns when/why enemy is selected. Enemy System owns pool composition + weighting. `BeaconType` is owned by Node Map GDD. |
| **Combat HUD UX Spec** *(undesigned)* | writes (data model) | `IVehicleView.NextIntent` (HUD reads); `OnIntentTelegraphed`, `OnIntentRetargeted`, `OnEnrageActivated` events | HUD reads intent to render the tell (icon, damage, target-slot indicator). | Enemy System owns intent data model + event emission. HUD UX Spec owns icon mapping, visual treatment, display language. HUD must not reach into brain internals. |
| **Biome** *(undesigned)* | writes (authors) | `BiomePoolSO` ScriptableObjects | Enemy System authors pool assets keyed by `biomeIndex`. Biome references during strip progression. | Biome owns `biomeIndex` progression + strip gating. Enemy System owns pool composition within each index. |

**Provisional Contract Specs**

```csharp
// Owned by this GDD (Enemy System). Consumed by Loot & Reward.
public record CombatEndedPayload(
    string                ArchetypeId,
    IReadOnlyList<string> ArchetypeTags,
    float                 DifficultyScore,
    int                   BiomeIndex,
    bool                  WasElite,
    bool                  EnrageReached,
    int                   TurnsToKill,
    bool                  PlayerVehicleCritical
);

// Owned by this GDD. Called by Node Encounter at beacon population time.
// BeaconType enum is owned by Node Map GDD — Enemy System takes a read-only dependency.
public interface IEnemyPool
{
    EnemyDefinitionSO GetEnemyFor(int biomeIndex, BeaconType beaconType, System.Random runSeed);
}

// Owned by this GDD. Card Combat holds the call sites; contract definition lives here.
// Implementations are plain C# classes — no MonoBehaviour. Stateless per call:
// all decision state must live in CombatContext, not in the brain instance.
public interface IEnemyBrain
{
    EnemyIntent SelectNextIntent(CombatContext context);   // non-null required
    EnemyIntent OnInvalidTarget(CombatContext context);    // retargeting hook; NoOpIntent sentinel allowed
}

// Owned by this GDD. All emissions use C# event Action<T>. UnityEvent is forbidden.
public interface IEnemyEventSource
{
    event Action<EnemyIntent>               OnIntentTelegraphed;
    event Action<EnemyIntent, EnemyIntent>  OnIntentRetargeted;     // (old, new)
    event Action<VehicleState>              OnEnrageActivated;
    event Action<CombatEndedPayload>        OnCombatEnded;
}
```

**Interface ownership summary:**

- `IEnemyBrain`, `IEnemyPool`, `IEnemyEventSource`, `CombatEndedPayload`, `EnemyIntent`, `EnemyDefinitionSO`, `BrainRuleset`, `BiomePoolSO` — **Enemy System GDD (this document)**
- `CombatContext`, `CombatRng` — **Card Combat GDD**
- `IVehicleView`, `IVehicleMutator`, `VehicleState`, `SlotId` — **V&P GDD**
- `DamageEffectSO`, `TargetType` — **Card System GDD**
- `ApplyStatusEffectSO`, `StatusEffectSO` — **Status Effects GDD**
- `BeaconType` — **Node Map GDD**

**📌 Provisional Flags**

- **📌 Loot & Reward** — Enemy System emits `OnCombatEnded(CombatEndedPayload)`. L&R GDD must subscribe and define scaling against payload fields. Payload shape is a hard contract; changes require breaking-change notice. Provisional default if undesigned: emit to a no-op subscriber, no rewards granted until wired.
- **📌 Node Encounter** — Enemy System implements `IEnemyPool`. Node Encounter GDD must define `BeaconType` and call `GetEnemyFor` at beacon population. Provisional default: `DefaultEnemyPool` returns hardcoded `EnemyDefinitionSO` for biome 0 regardless of beacon type, so combat runs in isolation.
- **📌 Combat HUD UX Spec** — Must define the **Intent Icon Language**: one distinct icon + color per `IntentType`, numeric damage treatment, target-slot indicator mapping `SlotId` to on-vehicle position. **Hard constraint: the intent tell MUST be a primary HUD element** — reserved screen zone, minimum read target size, contrast guarantee against the vehicle sprite behind it, z-order that cannot be occluded by particles or status stacks. A 12px tooltip above a 200px sprite fails Pillar 3 before a card is played. Provisional default: HUD displays `IntentType.ToString()` + damage int until icon language is authored.
- **📌 Biome** — Enemy System authors `BiomePoolSO` assets keyed by `biomeIndex`. Biome GDD must ratify the `biomeIndex` progression (strip 1 → N → gate → next biome). Provisional default: all archetypes set to `biomeIndex = 0`; pool selection ignores biome until Biome GDD ratifies the scheme.

**Determinism Clause**

All `IEnemyBrain` implementations MUST be pure functions of their inputs. `SelectNextIntent` and `OnInvalidTarget` derive every decision exclusively from the `CombatContext` passed at call time and from the `CombatRng` (`System.Random` with explicit run seed) inside it. `UnityEngine.Random`, `Time.time`, `Time.frameCount`, and all other Unity global state are forbidden in any brain or decision-weight code path. No brain instance may hold mutable decision state between turns — if carry-forward is required (e.g., two-turn wind-up patterns), it must be modeled as fields on `CombatContext` so the full decision snapshot is reproducible. Invariant: identical `CombatContext` + identical seed → identical `EnemyIntent`, every time. This is a hard requirement for the replay and debug tooling specified in Card Combat OQ-CC4.

## Formulas

> **HUD Promise**: `EnemyIntent.PredictedDamage` is a deterministic pre-computed value — what the player reads on the telegraph is what the Armor layer receives, every time. There is no hidden RNG in damage magnitude. All randomness lives in intent selection (which brain candidate is drawn each turn) and in the Armor layer's own resolution rules as defined by Vehicle & Part (V&P AC-CC33–39). The player is never surprised by a number that wasn't shown.

### D.1 — Enemy HP and Armor Scaling

#### D.1.1 — Hull HP

```
HullHP = BaseHullHP × BiomeHPScalar(biomeIndex) × EliteFactor
```

Where:

```
EliteFactor = EliteHPScalar   if beaconType == EliteCombat
EliteFactor = 1.0             otherwise
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `BaseHullHP` | int | 20–120 | Authored on `EnemyDefinitionSO`. Sum of all four slot base HP values. |
| `BiomeHPScalar` | float | 1.0–2.0 | Biome index multiplier (see anchor table below). Tunable per Section G. |
| `biomeIndex` | int | 1–3 | Active biome at time of pool selection. |
| `EliteHPScalar` | float | 1.0–2.0 | Applied when beacon type is `EliteCombat`. Default: 1.75. Tunable per Section G. |
| `EliteFactor` | float | 1.0–2.0 | Resolved from `EliteHPScalar` or 1.0. |
| `HullHP` | int | 20–420 | Final vehicle HP after scaling. Not stored on `EnemyDefinitionSO` — computed at spawn time and set on `VehicleState`. |

**Biome HP scalar anchor defaults:**

| biomeIndex | BiomeHPScalar | Design Intent |
|---|---|---|
| 1 (Sand Flats) | 1.0 | Even match with a fresh Scout chassis. Non-elite enemy should not require optimal play to survive. |
| 2 (Junk Mesa) | 1.4 | Noticeable step up. A run arriving here with no repairs will feel the difference. |
| 3 (Haven Approach) | 1.9 | Pressures a mid-run upgraded vehicle. Non-elite enemies should demand resource commitment. |

*All scalar values are tuning knobs. See Section G for safe ranges.*

**Output range:** Clamped to minimum 1. Uncapped above — authoring error if `HullHP` exceeds 500 for a non-boss archetype.

**Worked example — Sand Flats "Patch Rider" raider, non-elite:**

```
BaseHullHP = 40
BiomeHPScalar(1) = 1.0
EliteFactor = 1.0

HullHP = 40 × 1.0 × 1.0 = 40
```

Same archetype as an elite in Biome 2:

```
HullHP = 40 × 1.4 × 1.75 = 98
```

#### D.1.2 — Per-Slot HP

```
SlotHP[s] = BaseSlotHP[s] × BiomeHPScalar(biomeIndex) × EliteFactor
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `s` | enum | {Weapon, Engine, Mobility, Frame} | Slot identity. |
| `BaseSlotHP[s]` | int | 4–40 | Authored on `EnemyDefinitionSO` per slot. Four values sum to `BaseHullHP`. |
| `BiomeHPScalar` | float | 1.0–2.0 | Same scalar as D.1.1 — not per-slot. |
| `EliteFactor` | float | 1.0–2.0 | Same factor as D.1.1. |
| `SlotHP[s]` | int | 4–200 | Final per-slot HP applied to `VehicleState.Slots[s]` at spawn. |

**Design note:** The four `BaseSlotHP` values are authored independently per archetype. Fragile-fronted archetypes (Weapon slot low) create predictable early kill targets. Fortress archetypes (all slots even) demand sustained pressure. The invariant `ΣBaseSlotHP[s] == BaseHullHP` is enforced by an SO import validator.

**Worked example — Patch Rider (10 HP each slot, non-elite, Biome 1):**

```
SlotHP[Weapon]   = 10 × 1.0 × 1.0 = 10
SlotHP[Engine]   = 10 × 1.0 × 1.0 = 10
SlotHP[Mobility] = 10 × 1.0 × 1.0 = 10
SlotHP[Frame]    = 10 × 1.0 × 1.0 = 10
```

#### D.1.3 — MaxArmor

MaxArmor is **not scaled at runtime**. It is authored directly on `EnemyDefinitionSO` as the sum of per-part MaxArmor contributions, exactly as the player's MaxArmor is derived from installed parts. Biome and elite scaling affect HP only. This matches the V&P symmetric Armor model: Armor is a property of the vehicle's installed parts, not a level-gated bonus.

Armor resolution (current Armor absorbed per hit, overflow, Bypass flags) is fully governed by V&P rules (AC-CC33–39) and is not redefined here.

**Worked example — Patch Rider:**

```
Frame part MaxArmor contribution: 8
MaxArmor = 8   (authored; identical at Biome 1 and Biome 3)
```

### D.2 — Enemy Damage Scaling

```
BaseDamageScaled = BaseDamage(archetype, intentRef)
                   × BiomeDamageScalar(biomeIndex)
                   × EliteDamageFactor

PredictedDamage  = BaseDamageScaled          ← written to EnemyIntent at brain call time

ResolvedDamage   = PredictedDamage + EnrageBonus
                   → passed to IVehicleMutator (Armor layer applied inside mutator)
```

Where:

```
EnrageBonus = EffectiveEnrageBaseBonus + max(0, TurnCount - EffectiveEnrageTurn)   if EnrageActive == true
EnrageBonus = 0                                                                    otherwise

EliteDamageFactor = EliteDamageScalar   if beaconType == EliteCombat
EliteDamageFactor = 1.0                 otherwise
```

**Formula ownership (arbitration 2026-04-24, BLOCKER-1):** The `EnrageBonus` formula is owned by Card Combat F-CC (additive escalation). Enemy System D.2 applies it as a post-scaling additive term. The old multiplicative form (`ResolvedDamage = PredictedDamage × EnrageDamageMultiplier`) is deprecated; registry constant `DefaultEnrageDamageMultiplier` has been replaced by `DefaultEnrageBaseBonus = 2` (see registry `enemies.constants.DefaultEnrageBaseBonus`). Rationale: additive +1/turn escalation is a countable telegraph — the player can read next-turn damage directly off the intent number, satisfying Pillar 3 (Read to Win).

| Symbol | Type | Range | Description |
|---|---|---|---|
| `BaseDamage(archetype, intentRef)` | int | 4–30 | Authored on the intent candidate within `BrainRuleset`. |
| `BiomeDamageScalar(biomeIndex)` | float | 1.0–1.7 | Biome-indexed multiplier. |
| `EliteDamageScalar` | float | 1.0–1.75 | Applied for `EliteCombat` beacons. Default: 1.4. Lower than HP scalar — elite fights are longer, not overwhelmingly spikey. |
| `EliteDamageFactor` | float | 1.0–1.75 | Resolved from `EliteDamageScalar` or 1.0. |
| `PredictedDamage` | int | 4–90 | Raw damage written into `EnemyIntent`. Armor-naive — player reads this number on the telegraph. |
| `EffectiveEnrageBaseBonus` | int | 0–8 | Per-archetype base bonus (see D.4). Default 2 (from `DefaultEnrageBaseBonus`). |
| `EnrageBonus` | int | 0–unbounded | Additive bonus resolved from `EffectiveEnrageBaseBonus + max(0, TurnCount - EffectiveEnrageTurn)` when `EnrageActive == true`, else 0. |
| `ResolvedDamage` | int | 0–unbounded | Damage value passed to `IVehicleMutator`. Armor soak happens inside mutator — Frame takes `max(0, ResolvedDamage - currentArmor)`. |

**Biome damage scalar anchor defaults:**

| biomeIndex | BiomeDamageScalar | Design Intent |
|---|---|---|
| 1 (Sand Flats) | 1.0 | Damage a fresh Scout can survive with reactive card play. |
| 2 (Junk Mesa) | 1.3 | Demands triage decisions. Ignoring damage for two turns becomes costly. |
| 3 (Haven Approach) | 1.6 | A single unblocked Ram intent from a non-elite should threaten a slot. |

**HUD promise enforcement:** `PredictedDamage` is computed once at brain call time (end of previous enemy turn or combat setup) and frozen on `EnemyIntent`. Enrage activation that occurs *between* the brain call and the Resolving state does not retroactively update `PredictedDamage` — instead, `EnrageBonus` is added at Resolving time. The HUD displays `PredictedDamage` (pre-Enrage, pre-Armor); the actual hit is `PredictedDamage + EnrageBonus` minus Armor. This is the only permitted divergence between telegraphed and resolved values, and it is always in one direction: resolved ≥ telegraphed. Designs where resolved can be *lower* than telegraphed are forbidden (they would make the HUD misleading in the optimistic direction). **HUD contract:** if Enrage activates on the same turn as an outstanding telegraph, the Combat HUD MUST update the displayed damage to `PredictedDamage + EnrageBonus` before the player's next input — owned by the Combat HUD UX Spec.

**Worked example — Patch Rider Ram intent, non-elite, Biome 1, turn 2 (no Enrage):**

```
BaseDamage(PatchRider, Ram)  = 12
BiomeDamageScalar(1)         = 1.0
EliteDamageFactor            = 1.0

PredictedDamage = 12 × 1.0 × 1.0 = 12
EnrageBonus = 0   (EnrageActive false; TurnCount 2 < EnrageTurn 8)

ResolvedDamage = 12 + 0 = 12
  → IVehicleMutator: Frame MaxArmor 8, current Armor 6
  → Frame takes max(0, 12 - 6) = 6 HP damage
```

Same intent at turn 9 (Enrage active; default EnrageTurn = 8, DefaultEnrageBaseBonus = 2):

```
PredictedDamage = 12   (was computed at turn 8 end, before Enrage resolved)
EnrageBonus     = 2 + max(0, 9 - 8) = 2 + 1 = 3
HUD updates to show 12 + 3 = 15 on Enrage activation (Combat HUD UX Spec contract)
ResolvedDamage = 12 + 3 = 15
  → Frame takes max(0, 15 - 6) = 9 HP damage

Turn 10: EnrageBonus = 2 + 2 = 4 → ResolvedDamage = 16. Turn 11: EnrageBonus = 5 → 17.
Escalation is +1/turn after Enrage activates (Card Combat F-CC).
```

### D.3 — Intent Selection Weighted Draw

```
FinalWeight(c) = BaseWeight(c) × Π{ Multiplier(m) | Condition(m) == true, m ∈ Modifiers(c) }

P(c) = FinalWeight(c) / Σ{ FinalWeight(c') | FinalWeight(c') > 0, c' ∈ Candidates }
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `c` | candidate ref | — | One intent candidate in the `BrainRuleset` ordered list. |
| `BaseWeight(c)` | float | 0.0–100.0 | Authored on the candidate. A value of 0 means the candidate is always excluded. |
| `Modifiers(c)` | list | 0–N entries | Ordered list of `WeightModifier { Condition, Multiplier }` on the candidate. |
| `Condition(m)` | bool | true/false | Evaluated against `CombatContext`. No reflection, no global state. |
| `Multiplier(m)` | float | 0.0–10.0 | Multiplicative modifier when `Condition(m)` is true. 0 suppresses the candidate regardless of base weight. |
| `FinalWeight(c)` | float | 0.0–unbounded | Product of base weight and all true-condition multipliers. |
| `P(c)` | float | 0.0–1.0 | Normalized probability for candidate `c` in the valid candidate pool. |

**Step 0 pre-filter (Card Combat R17 retrofit 2026-04-24):** BEFORE `FinalWeight` evaluation, the candidate list is first **position-filtered**: remove any candidate where `PositionRequirement ∈ {RequiresAhead, RequiresBehind}` and the current enemy position does not satisfy it. Weight modifiers then evaluate on the position-filtered list only.

**Filter rule:** Candidates with `FinalWeight ≤ 0` are removed from the pool before normalization. They do not contribute to the denominator.

**Empty result after Step 0 (Card Combat R18 retrofit 2026-04-24):** If the position-filter leaves zero candidates, Card Combat injects the synthetic `RepositionIntent` — this is NOT an EC2 authoring error; it is the designed Reposition-fallback path. EC2 applies only when the position-filtered list is non-empty but every `FinalWeight ≤ 0`.

**Edge case — all candidates filtered:** If every candidate surviving Step 0 resolves to `FinalWeight ≤ 0`, no valid pool exists. Brain logs a warning (`"BrainRuleset malformed: all candidates have FinalWeight ≤ 0 for archetype {id}"`) and returns `NoOpIntent` sentinel. This is an authoring error. Combat continues; the enemy skips its turn without telegraphing an intent change (the existing telegraph persists unchanged in the HUD). Section E owns the full degenerate-state spec.

**Seeded draw procedure:**

1. Sort valid candidates into a stable ordered list (preserve authoring order for determinism).
2. Compute cumulative distribution: `CDF[i] = Σ{ P(c[0..i]) }`.
3. Draw `r = CombatRng.NextDouble()` (0.0 inclusive, 1.0 exclusive).
4. Select first candidate `c[i]` where `CDF[i] > r`.

Identical seed + identical `CombatContext` → identical draw every time.

**Worked example — Patch Rider at TurnCount 2, player Frame Damaged, player Engine Offline:**

3-candidate ruleset:

| Candidate | BaseWeight | Condition | Multiplier | FinalWeight |
|---|---|---|---|---|
| Ram (Damage, Frame target) | 40 | player.Frame.state == Damaged | 2.0 | 40 × 2.0 = **80** |
| Swerve (PositionShift) | 20 | player.Engine.state == Offline | 0.0 | 20 × 0.0 = **0** (filtered) |
| Fortify (Defend, Self) | 30 | (no modifiers true) | — | **30** |

Filtered pool: {Ram: 80, Fortify: 30}. Sum = 110.

```
P(Ram)     = 80  / 110 = 0.727
P(Fortify) = 30  / 110 = 0.273

CDF: [0.727, 1.000]

CombatRng.NextDouble() → r = 0.541
  r < 0.727 → select Ram

Selected: Ram intent. PredictedDamage = 12 (pre-Enrage). TargetSlot = Frame.
```

Swerve was suppressed entirely (Multiplier 0.0 on the Engine-Offline condition reflects that a position shift is pointless when the player can't drive). The designer is modeling tactical opportunism: when Frame is cracked, press it.

### D.4 — Enrage Damage Bonus and Behavior Shift

```
EnrageActive = (TurnCount >= EffectiveEnrageTurn)

EffectiveEnrageTurn = EnemyDefinitionSO.EnrageTurn    if field is authored
                    = DefaultEnrageTurn (= 8)          otherwise

EffectiveEnrageBaseBonus = EnemyDefinitionSO.EnrageBaseBonusOverride   if field is authored
                         = DefaultEnrageBaseBonus (= 2)                 otherwise

EnrageBonus = EffectiveEnrageBaseBonus + max(0, TurnCount - EffectiveEnrageTurn)   if EnrageActive
            = 0                                                                     otherwise
```

**Formula ownership:** this additive formula is owned by Card Combat F-CC (cross-review arbitration 2026-04-24, BLOCKER-1). Enemy System D.2 applies it in the damage pipeline; Enemy System D.4 documents its behavior and the authoring surface.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `TurnCount` | int | 0–unbounded | Full turns elapsed. Increments at the start of `SelectingNextIntent`, including Stalled turns. |
| `EffectiveEnrageTurn` | int | 1–20 | The turn at which Enrage activates for this archetype. |
| `DefaultEnrageTurn` | int | 8 (constant) | Global default. Tunable via `CombatConfig`. See Section G. |
| `EffectiveEnrageBaseBonus` | int | 0–8 | Per-archetype base bonus added on the Enrage-activation turn. Default 2. |
| `DefaultEnrageBaseBonus` | int | 2 (constant) | Global default. Tunable via `CombatConfig`. Replaces deprecated `DefaultEnrageDamageMultiplier`. |
| `EnrageBonus` | int | 0–unbounded | Flat damage added to `ResolvedDamage` while Enrage is active. Escalates +1 per turn past `EffectiveEnrageTurn`. |
| `EnrageActive` | bool | — | Boolean flag on the runtime combat record. Set permanently once threshold crossed; never cleared. |

**Per-archetype override:** `EnemyDefinitionSO` exposes an optional `int? EnrageBaseBonusOverride` field. If unset (null), the brain uses the global `CombatConfig.DefaultEnrageBaseBonus` (2). Most archetypes do not author this field. Archetypes that should spike harder (boss-tier, Haven Approach elites) set a higher value (e.g., 4). Archetypes that Enrage for behavior shift only (new intent list, no extra damage) may set it to 0. The +1/turn escalation term is not overridable — it is a system-wide pacing constant.

**Enrage behavior shift — secondary candidate list:**

When `EnrageActive` is true, `IEnemyBrain.SelectNextIntent` checks whether the archetype's `BrainRuleset` has a non-empty `EnrageIntentCandidates` list. If it does, that list **replaces** the base candidate list entirely for the weighted draw (D.3 formula applies identically on the replacement list). If `EnrageIntentCandidates` is absent or empty, the base candidate list is used with `EnrageBonus` added to all resulting damage via D.2.

Enrage has two distinct expression modes:

- **Damage-only Enrage** (no `EnrageIntentCandidates`): same behavior, hits harder and escalates. Simpler to author.
- **Behavior-shift Enrage** (has `EnrageIntentCandidates`): different action repertoire at high turns. Used when the archetype "going berserk" should feel qualitatively different. Additive bonus still applies on top of the new candidates' damage.

**Stalled interaction:** `TurnCount` advances during `Stalled_Skipping`. Enrage may therefore activate on a Stalled turn. However, `EnrageBonus` applies only during `Resolving` — a Stalled turn produces no damage event, so the bonus has no effect that turn. `EnrageActive` is still set; the HUD must reflect Enrage state even on a skipped turn (Combat HUD UX Spec contract). The +1/turn escalation term continues to advance during Stalled turns (it keys off `TurnCount`, not off resolved damage events).

**Worked example — Patch Rider, default `EnrageBaseBonusOverride = null` → EffectiveEnrageBaseBonus = 2, has `EnrageIntentCandidates`:**

Base candidates at turns 1–7: {Ram 40, Swerve 20, Fortify 30} (see D.3).

`EnrageIntentCandidates` at turn 8+: {Ram 70, DoubleRam 30} — retreat and fortify are gone; the Patch Rider commits entirely to aggression.

At turn 9 (`TurnCount = 9 >= EffectiveEnrageTurn = 8`, `EnrageActive = true`):

```
Draw from EnrageIntentCandidates:
  Ram:       BaseWeight 70, no modifiers → FinalWeight 70
  DoubleRam: BaseWeight 30, no modifiers → FinalWeight 30
  Sum = 100

P(Ram)       = 0.70
P(DoubleRam) = 0.30

r = CombatRng.NextDouble() → 0.82 → select DoubleRam

DoubleRam: BaseDamage 18
PredictedDamage = 18 × 1.0 × 1.0 = 18   (Biome 1 non-elite, computed before this turn)
EnrageBonus    = 2 + max(0, 9 - 8) = 3
ResolvedDamage = 18 + 3 = 21            (EnrageBonus added per D.2)
  → IVehicleMutator: Frame current Armor 4 (depleted from prior turns)
  → Frame takes max(0, 21 - 4) = 17 HP damage

Turn 10 same intent:  EnrageBonus = 4 → ResolvedDamage = 22 → 18 HP
Turn 11 same intent:  EnrageBonus = 5 → ResolvedDamage = 23 → 19 HP
```

Seventeen HP to Frame on a slot that started at 10 HP: the slot went Offline turns ago. The target redirects per `RetargetPolicy` (D.6). See D.7 for the full turn-9 trace. The escalating +1/turn after Enrage is the player's visible "close this fight" pressure — countable directly off the intent readout.

### D.5 — DifficultyScore Computation

`DifficultyScore` is a float authored on `EnemyDefinitionSO`. It is an archetype-intrinsic property — it does not incorporate biome or elite scaling. Loot & Reward receives both `DifficultyScore` and `BiomeIndex` (plus `WasElite`) in `CombatEndedPayload` and applies its own reward scaling on top.

```
DifficultyScore =   w_HP  × norm(BaseHullHP,      HP_min,      HP_max)
                  + w_DPT × norm(AvgDPT,           DPT_min,     DPT_max)
                  + w_IC  × norm(IntentCount,       IC_min,      IC_max)
                  + w_ES  × norm(EnrageSeverity,    ES_min,      ES_max)
```

Where:

```
norm(x, lo, hi) = clamp((x - lo) / (hi - lo), 0.0, 1.0)

AvgDPT = Σ{ P(c) × BaseDamage(c) | c ∈ BaseCandidates }
         (probability-weighted average base damage across all base intent candidates)

EnrageSeverity = max(0, DefaultEnrageTurn - EffectiveEnrageTurn)
               + max(0, EffectiveEnrageBaseBonus - DefaultEnrageBaseBonus) × 2
         (archetypes that Enrage earlier and hit harder score higher; purely additive under the Card Combat F-CC model)
```

**Weight defaults:**

| Weight | Default | What it captures |
|---|---|---|
| `w_HP` | 0.30 | Raw survivability — how long the enemy lives |
| `w_DPT` | 0.40 | Pressure output — how fast it hurts the player |
| `w_IC` | 0.15 | Legibility tax — more candidates = harder to predict |
| `w_ES` | 0.15 | Threat acceleration — early Enrage + elevated base bonus = scarier |

**Anchor band defaults (tunable via Section G):**

| Component | lo | hi | Notes |
|---|---|---|---|
| `BaseHullHP` | 20 | 120 | Biome-1 weakest to Biome-3 elite baseline |
| `AvgDPT` | 4 | 25 | Single low-damage utility to sustained high-damage Ram spam |
| `IntentCount` | 1 | 8 | A single-intent brain scores 0; an 8-candidate brain scores 1 |
| `EnrageSeverity` | 0 | 12 | `(8 - 3) + (4 - 2) × 2 = 9` for early-Enrage aggressive with base bonus 4; `0` for Enrage-off archetypes and default archetypes |

| Symbol | Type | Range | Description |
|---|---|---|---|
| `BaseHullHP` | int | 20–120 | Authored stat, same as D.1.1 baseline. |
| `AvgDPT` | float | 4–25 | Probability-weighted average base damage over base intent candidates. |
| `IntentCount` | int | 1–8 | Number of distinct candidates in `BrainRuleset.BaseCandidates`. |
| `EnrageSeverity` | float | 0–12 | `max(0, DefaultEnrageTurn - EffectiveEnrageTurn) + max(0, EffectiveEnrageBaseBonus - DefaultEnrageBaseBonus) × 2`. Enrage-disabled archetypes (sentinel `EffectiveEnrageTurn == null`) score 0 by definition. |
| `DifficultyScore` | float | 0.0–1.0 | Normalized composite score. 0 = trivially easy; 1 = maximally threatening within anchor band. |

**Output range:** Clamped to [0.0, 1.0]. An archetype whose stats exceed the anchor band in every dimension scores 1.0 — a signal to the author that the anchor band needs revisiting.

**Worked example — Patch Rider:**

```
BaseHullHP     = 40    → norm(40, 20, 120) = (40-20)/(120-20) = 20/100 = 0.20
AvgDPT         = P(Ram)×12 + P(Swerve)×0 + P(Fortify)×0
                 Unweighted prior (equal conditions):
                   Ram: 40/(40+20+30) = 0.444
                   Swerve: 20/90 = 0.222
                   Fortify: 30/90 = 0.333
                 AvgDPT = 0.444×12 + 0.222×0 + 0.333×0 = 5.33
                 norm(5.33, 4, 25) = (5.33-4)/(25-4) = 1.33/21 = 0.063

IntentCount    = 3     → norm(3, 1, 8) = (3-1)/(8-1) = 2/7 = 0.286

EnrageSeverity = max(0, 8 - 8) + max(0, 2 - 2) × 2 = 0 + 0 = 0.0
                 → norm(0, 0, 12) = 0.0

DifficultyScore = 0.30×0.20 + 0.40×0.063 + 0.15×0.286 + 0.15×0.0
               = 0.060 + 0.025 + 0.043 + 0.000
               = 0.128
```

A score of ~0.13 positions the Patch Rider as a light introductory threat — appropriate for a Biome-1 non-elite archetype. Loot & Reward will scale the reward accordingly.

### D.6 — Retarget Policy Priority Resolution

Governed by `EnemyDefinitionSO.RetargetPolicy`. Default for all archetypes is `Frame` (a `FixedSlot(Frame)` shorthand). Per-archetype overrides are one of three policy types:

**FixedSlot(slot)**

- If `slot` is `Online` or `Damaged`: target it. Proceed to Resolving.
- If `slot` is `Offline`: return `NoOpIntent`. Log warning. Resolving receives NoOp — no effect.

**PriorityList(slot1, slot2, ..., slotN)**

- Walk the list in authoring order.
- First slot with state `Online` or `Damaged` wins. Proceed to Resolving with new target.
- If no slot in the list is Online or Damaged (all are `Offline`): return `NoOpIntent`. Log warning.

**ContextualRule(conditionList)**

- Evaluate conditions in authoring order against `CombatContext`.
- First condition that evaluates true: its associated `SlotId` becomes the target.
- If no condition matches: fall back to `DefaultRetargetPolicy` (`Frame`).
  - If Frame is also `Offline`: return `NoOpIntent`. Log warning.

**NoOpIntent sentinel:** Returned by any policy branch that cannot resolve to a valid slot. `Type = Utility`, `Target = None`, no payload. State machine still executes Retargeting → Resolving; Resolving produces no effect on either vehicle. Full degenerate-slot spec in Section E.

**Worked example — "Weapon-hunter" elite, PriorityList(Weapon, Engine, Frame, Mobility):**

**Scenario A** — Player Weapon is `Offline`, Engine is `Damaged`:

```
Walk list:
  Weapon → Offline → skip
  Engine → Damaged → VALID

Target: Engine. Proceed to Resolving with TargetSlot = Engine.
```

**Scenario B** — All four player slots are `Offline`:

```
Walk list:
  Weapon   → Offline → skip
  Engine   → Offline → skip
  Frame    → Offline → skip
  Mobility → Offline → skip
  List exhausted. No valid target.

Return: NoOpIntent. Log: "RetargetPolicy(PriorityList) found no valid slot for archetype weapon-hunter-elite"
Resolving: no effect.
```

**Design note:** `Offline` means a slot has reached 0 HP — it is structurally destroyed for this combat. `Damaged` means it has taken damage but is still functional. Both are valid targets. Only `Offline` is excluded because an attack on a destroyed slot is nonsensical (and V&P's mutator would ignore it anyway).

### D.7 — Worked Example: Patch Rider Across Turns 1–9

**Archetype: "Patch Rider" (Sand Flats raider)**
`BaseHullHP = 40`, 4 slots at 10 HP each, `MaxArmor = 8` (Frame part contribution), `BaseDamage(Ram) = 12`, 3 intent candidates (Ram/Swerve/Fortify), `EnrageTurn = 8`, `RetargetPolicy = FixedSlot(Frame)` (default). Biome 1, non-elite.

Player vehicle (for context): Scout chassis, Frame 18/20 HP, Engine 15/15 HP, current Armor = 6. Player has played defensively, Armor not yet depleted.

**Turn 1** (TurnCount = 0 at brain call during SelectingFirstIntent; increments to 1 at next SelectingNextIntent)

`CombatContext` snapshot: TurnCount 0, EnrageActive false, player all slots Online, Armor 6.

Weight calc (no conditions true for player-state modifiers at combat start):
- Ram: 40 × 1.0 = 40, Swerve: 20 × 1.0 = 20, Fortify: 30 × 1.0 = 30. Sum = 90.
- P(Ram) = 0.444, P(Swerve) = 0.222, P(Fortify) = 0.333.

`r = CombatRng.NextDouble() → 0.31` → Ram selected (CDF: Ram 0.444 covers 0.31).

`EnemyIntent`: Ram, TargetSlot = Frame, PredictedDamage = 12. Telegraphed.

Player turn. Player plays Engine Overcharge. Player does not block.

Enemy turn Resolving: EnrageActive false, EnrageBonus 0. ResolvedDamage = 12 + 0 = 12. IVehicleMutator: Armor 6 → Frame takes 6 HP damage. Frame: 18 → 12 HP. Armor resets per V&P rules.

**Turn 2** (TurnCount incremented to 2 at SelectingNextIntent)

`CombatContext`: TurnCount 2, EnrageActive false, player Frame Damaged (12/20), Engine Online, Armor 6.

Frame Damaged condition fires on Ram modifier (×2.0):
- Ram: 40 × 2.0 = 80, Swerve: 20 × 0.0 = 0 (filtered — Engine not Offline), Fortify: 30 × 1.0 = 30. Sum = 110.

`r → 0.54` → Ram (CDF: Ram 0.727 > 0.54).

Intent: Ram, Frame, PredictedDamage = 12. Telegraphed.

Player blocks with mid-weight Block card. Block absorbs 8 (card effect via IVehicleMutator).

Enemy Resolving: ResolvedDamage = 12. After Block absorption (8), remaining = 4. Armor 0 at this point (Block consumed it). Frame takes 4 HP. Frame: 12 → 8 HP.

**Turn 3** (TurnCount = 3)

`CombatContext`: Frame Damaged (8/20), Armor 4 (partial replenish).

Same weight calc as turn 2: Ram 80, Fortify 30, sum 110. `r → 0.78` → Fortify selected (CDF: Ram ends at 0.727; 0.78 > 0.727 → Fortify).

Intent: Fortify (Defend, Self), ArmorGain = 4. Telegraphed.

Enemy Resolving: IVehicleMutator called on enemy vehicle. Enemy Armor: 8 → min(MaxArmor, 8+4) = 8 (already at cap). No overflow; Fortify intent is useful only when Armor has been depleted. Player reads this as a "wasted" Fortify — signal that the enemy's Armor is full, not worth burning through this turn.

**Turn 4** (TurnCount = 4)

`CombatContext`: player Frame Damaged (8/20), Armor 4. Enemy Armor at cap (8).

`r → 0.41` → Ram (P(Ram) = 0.727, 0.41 < 0.727).

Intent: Ram, Frame, PredictedDamage = 12. Telegraphed.

Player passes (no block available). ResolvedDamage = 12. Armor 4 soaks 4; Frame takes 8. Frame: 8 → 0 HP. Frame is now Offline.

**Turn 5** (TurnCount = 5)

`CombatContext`: player Frame Offline, Armor 0.

Ram modifier: Frame Damaged condition is false (Frame is Offline, not Damaged). Ram weight back to base 40. Swerve: Engine Online, condition false, weight 20. Fortify: 30.

`r → 0.19` → Ram (CDF: Ram 0.444 > 0.19). TargetSlot = Frame (authored on Ram candidate).

`OnInvalidTarget` fires immediately (Frame is Offline before Resolving). Policy: FixedSlot(Frame). Frame is Offline. Return `NoOpIntent`. Log warning.

Resolving: NoOpIntent, no effect. Enemy skips damage this turn. Player breathes — Frame is gone but Armor layer has protected the rest.

**Turn 6** (TurnCount = 6) — Retarget scenario: player knocks the telegraphed TargetSlot Offline mid-player-turn

Brain call at start of turn 6 SelectingNextIntent. `r → 0.55` → Fortify. Intent: Fortify, Self, ArmorGain = 4. Telegraphed. (No retargeting scenario here — Fortify targets Self, always valid.)

Now: mid-scenario insert illustrating the retarget event. Suppose at TurnCount 6, brain had selected Ram targeting Weapon slot. Player plays Dismantle card on enemy Weapon slot during player turn, reducing it to 0 HP (Offline).

Enemy turn begins. `NextIntent.TargetSlot = Weapon`, state = Offline. Combat Loop calls `OnInvalidTarget(CombatContext)`. Policy: FixedSlot(Frame). But the Patch Rider's RetargetPolicy targets the *player's* Frame. Player Frame is Offline (0 HP from turn 4). Return NoOpIntent. Log warning.

`OnIntentRetargeted(old: Ram/Weapon, new: NoOpIntent)` event fires for the HUD. Resolving: no effect. TurnCount increments to 7.

**Turn 7** (TurnCount = 7)

Normal turn. `r → 0.62` → Fortify. Enemy self-buffs Armor. Player has two turns before Enrage. No damage event.

**Turn 8** (TurnCount = 8 at brain call — Enrage threshold check)

At start of SelectingNextIntent: `TurnCount = 8 >= EffectiveEnrageTurn = 8`. `EnrageActive = true`.

`OnEnrageActivated(VehicleState)` event fires. HUD updates.

Brain draws from `EnrageIntentCandidates`: {Ram 70, DoubleRam 30}. `r → 0.23` → Ram (P(Ram) = 0.70 > 0.23).

Intent: Ram, TargetSlot = Frame (authored on Ram candidate; coincidentally the same slot as default retarget policy). Before Resolving, `NextIntent.TargetSlot` = Frame, which is Offline. `OnInvalidTarget` fires. FixedSlot(Frame) → Offline → NoOpIntent.

Resolving: no effect. The Patch Rider is enraged but has nowhere to hit — player's Frame destruction is working against the enemy's fixed targeting. Player Frame Offline = de facto damage immunity through this archetype's default policy.

HUD should communicate this: the Enrage flame icon is visible, but the intent resolves to a strike-out (NoOp indicator).

**Turn 9** (TurnCount = 9, `EnrageActive = true` already set)

Player has repaired Engine to Online this turn; Mobility remains Online.

Brain draws from `EnrageIntentCandidates`. `r → 0.82` → DoubleRam (P(DoubleRam) = 0.30; CDF: Ram 0.70, DoubleRam 1.00; 0.82 > 0.70 → DoubleRam).

DoubleRam: BaseDamage = 18, TargetSlot = Frame (authored on intent candidate).

`OnInvalidTarget`: Frame Offline → FixedSlot(Frame) → NoOpIntent. No effect.

**Design observation for authors:** The Patch Rider's `RetargetPolicy = FixedSlot(Frame)` means that once the player destroys their own Frame slot (at turn 4 here), the Patch Rider becomes permanently toothless via its damage intents. This is intentional for a Biome-1 introductory archetype — it teaches the "sacrifice Frame to neutralize raider" strategy. **Biome-2 and Biome-3 archetypes MUST use `PriorityList` or `ContextualRule` policies to prevent this trivialization** (authoring rule; see Section G).

---

### D.8 — Position Bonus Damage (Card Combat F-CC5 cross-reference, retrofit 2026-04-24)

Enemy `PredictedDamage` authored per D.2 is the **base** damage; Card Combat F-CC5 adds `PositionBonus` when the `BonusIf*` condition is met at resolution. The authoritative formula lives in Card Combat F-CC5 — this subsection exists so brain authors understand that `PredictedDamage` is **pre-bonus base damage**, not effective post-bonus damage.

```
effective_damage = intent.PredictedDamage + (bonus_active ? intent.PositionBonus : 0)
  where bonus_active = (PositionRequirement == BonusIfAhead  && enemyPosition == Ahead)
                    OR (PositionRequirement == BonusIfBehind && enemyPosition == Behind)
```

**HUD telegraph implication**: the HUD reads `PredictedDamage + ConditionalPositionBonus` (HUD rendering concern, not brain concern) so the player sees the real number they will take. The brain does not pre-compute this — it authors `PredictedDamage` and `PositionBonus` separately; the HUD and damage pipeline combine them.

**Balance sim implication**: D.5 DifficultyScore uses `PredictedDamage` as the base for DPT calculations. If an archetype relies heavily on `PositionBonus`, its DifficultyScore under-represents its real threat. Balance sim spec (post-retrofit): DPT weight should use `PredictedDamage + (0.5 × PositionBonus)` as an expected-value approximation (assumes ~50% of the archetype's turns are in its preferred position across a combat). Formalize this in G when the balance sim picks it up.

---

> **Cross-reference:** All constants with default values in this section are tunable via Section G (Tuning Knobs). Anchor defaults (`BiomeHPScalar`, `BiomeDamageScalar`, `EliteHPScalar`, `EliteDamageScalar`, `DefaultEnrageTurn`, `DefaultEnrageBaseBonus`, `DifficultyScore` weight coefficients and anchor bands) are authoring starting points validated against the vertical-slice balance simulation. Any recalibration from playtest telemetry amends Section G first; this section reflects formula shape only, not final production values.

## Edge Cases

The edge cases below cover every degenerate state the enemy state machine can enter; each is fully specified so an engineer can implement a deterministic outcome and a QA engineer can verify it without ambiguity.

### EC1 — All Player Slots Offline When Damage Intent Telegraphed

**Trigger.** At the start of the enemy turn, the enemy's `NextIntent` has `Type = Damage` and `Target = PlayerSlot`, and every player slot (`Weapon`, `Engine`, `Mobility`, `Frame`) is `Offline`. The `TargetSlot` named on the intent is therefore `Offline`, which routes the state machine from `Telegraphed` to `Retargeting` per transition rule 1. `OnInvalidTarget` is called. The `RetargetPolicy` (regardless of type — `FixedSlot`, `PriorityList`, or `ContextualRule`) finds no slot with state `Online` or `Damaged` and returns `NoOpIntent`.

**State behavior.** `Telegraphed` → `Retargeting` (entry condition: `NextIntent.TargetSlot` is `Offline`). `IEnemyBrain.OnInvalidTarget(CombatContext)` called exactly once. Policy exhausts all candidates; all are `Offline`. Brain returns `NoOpIntent` (full field contract in EC4). `OnIntentRetargeted(old: original intent, new: NoOpIntent)` fires. State machine continues: `Retargeting` → `Resolving`. `Resolving` receives `NoOpIntent`; `IVehicleMutator` is not called (no target, no payload). `Resolving` → `SelectingNextIntent`. `TurnCount` increments. `EnrageActive` threshold check runs. Brain called for next intent via `SelectNextIntent`. The `allPlayerSlotsOffline` weight-modifier condition is now `true` in `CombatContext` — `BrainRulesets` MUST author a weight modifier for this condition (see Authoring Rules Carried to Section G). Brain returns the next `EnemyIntent` — which may again be `NoOpIntent` if the ruleset's `allPlayerSlotsOffline` modifier suppresses all candidates to `FinalWeight ≤ 0` (see EC2). `SelectingNextIntent` → `Telegraphed`.

**Player outcome.** The HUD's intent tell updates to the neutral "—" indicator (full `NoOpIntent` HUD field contract in EC4). The enemy visibly does nothing this turn. If the player's vehicle has all slots `Offline`, the combat state that triggered this edge case also triggers the player-defeat path via `EndOfCombat` — in practice, this edge case is encountered only when the player-defeat check is evaluated in the same pass and the player's vehicle is already at `HullHP ≤ 0`. If `HullHP > 0` but all slots are `Offline` (see EC6 for that symmetric scenario), the enemy still resolves NoOp and play continues, which is the correct designed behavior.

**Designer intent.** An all-`Offline` player vehicle with `HullHP > 0` is an extremely degenerate state (V&P symmetric rules govern when it can occur), but the enemy system must not crash or produce undefined behavior if it happens. The NoOp path ensures combat continues cleanly until the HP-loss path terminates it.

**Detection.** Unit test `EnemyBrain_AllPlayerSlotsOffline_RetargetReturnsNoOp`: construct a `CombatContext` with all four player slots set to `Offline`; call `OnInvalidTarget` on a brain with `PriorityList(Weapon, Engine, Frame, Mobility)` policy; assert return value is `NoOpIntent` with all fields matching EC4's contract. Log assertion: `"RetargetPolicy(...) found no valid slot"` present in test output.

> **Carry to Section G:** `BrainRuleset` authors MUST provide a weight modifier that handles `allPlayerSlotsOffline == true`. The modifier may suppress damage intents (multiplier 0.0) to avoid repeated pointless NoOp cycles, or authors may accept NoOp as the intentional outcome. This is an authoring responsibility, not an engine invariant.

### EC2 — All BrainRuleset Candidates Filtered (Every FinalWeight ≤ 0)

**Trigger.** `IEnemyBrain.SelectNextIntent(CombatContext)` is called (from either `SelectingFirstIntent` or `SelectingNextIntent`). After evaluating all `WeightModifier` conditions against `CombatContext`, every candidate in the active list (base candidates if `EnrageActive == false`, `EnrageIntentCandidates` if `EnrageActive == true` and the list is non-empty) resolves to `FinalWeight ≤ 0`. Per D.3's filter rule, no valid pool exists.

**State behavior.** Brain logs warning: `"BrainRuleset malformed: all candidates have FinalWeight ≤ 0 for archetype {ArchetypeId}"`. Brain returns `NoOpIntent` sentinel (full field contract in EC4). If this occurred during `SelectingFirstIntent`: the Combat Loop receives `NoOpIntent`, sets it as `NextIntent`, emits `OnIntentTelegraphed(NoOpIntent)`, and advances to `Telegraphed`. If this occurred during `SelectingNextIntent`: same path — `NoOpIntent` becomes the new `NextIntent`, `OnIntentTelegraphed` fires, loop advances to `Telegraphed`. The `NextIntent` is non-null in both cases, so Telegraph Contract (C.4) is satisfied. Combat continues; the enemy takes no action on its next turn (the NoOp intent routes through `Resolving` as specified in EC4's state behavior). This is an authoring error — it should be caught by the SO import validator before it reaches runtime.

**Player outcome.** HUD displays the "—" neutral tell. The enemy telegraphs nothing actionable. From the player's perspective this is indistinguishable from a deliberate Utility/NoOp intent — which is correct, since the player has no obligation to know that an authoring error occurred.

**Designer intent.** A malformed ruleset must not crash combat or leave `NextIntent` null (which would violate the Telegraph Contract and trigger `InvalidCombatStateException`). The NoOp sentinel is the safest non-destructive fallback. The warning gives engineering a log trace to catch the authoring error in QA.

**Detection.** Unit test `EnemyBrain_AllCandidatesFiltered_ReturnsNoOpWithWarning`: author a `BrainRuleset` where every candidate has a condition that is always true with `Multiplier = 0.0`; call `SelectNextIntent`; assert return is `NoOpIntent` and Unity log contains the expected warning string. SO import validator check (carry to Section J): reject any `EnemyDefinitionSO` where every candidate in `BrainRuleset.BaseCandidates` has `BaseWeight == 0` — this is the only static detectable form; zero-weight-via-modifier requires runtime context.

### EC3 — Stalled Active and Enrage Activates on the Same Turn

**Trigger.** At the start of the enemy turn: Stalled status is active on the enemy with `RemainingCharges ≥ 1`, AND the Enrage threshold check that runs at `SelectingNextIntent` (after `TurnCount` increments) would place `TurnCount >= EffectiveEnrageTurn`. Concretely: the `Stalled_Skipping` state is entered this turn, and `TurnCount` after increment satisfies the Enrage condition.

**State behavior.** Exact ordering is mandatory:

1. Enemy turn begins. Stalled active. State enters `Stalled_Skipping`. One Stalled charge consumed from `RemainingDuration` (Status Effects pipeline, symmetric with player per C.8).
2. Intent resolution is bypassed. `IVehicleMutator` is not called. The telegraphed `NextIntent` from last turn persists on `VehicleState` — it did not execute.
3. State transitions `Stalled_Skipping` → `SelectingNextIntent`.
4. `TurnCount` increments at `SelectingNextIntent` entry.
5. Enrage threshold check: `TurnCount >= EffectiveEnrageTurn`. If true: `EnrageActive = true`. `OnEnrageActivated(VehicleState)` event fires. HUD updates to show Enrage state (Combat HUD UX Spec contract — this GDD owns the event; HUD owns the visual).
6. `IEnemyBrain.SelectNextIntent(CombatContext)` called. `CombatContext` at this call has `EnrageActive = true`. Brain consults `EnrageIntentCandidates` if non-empty (per D.4). The selected intent is telegraphed for the *next* player turn — it does NOT execute this turn.
7. `SelectingNextIntent` → `Telegraphed`. Player's next turn begins with the Enrage-selected intent visible in the HUD.

`EnrageBonus` has no effect on the Stalled turn because `Resolving` was bypassed — no damage event occurred. The bonus will apply when the next non-Stalled `Resolving` state is reached (and will reflect the correct `TurnCount` at that point, per F-CC's additive +1/turn escalation).

**Player outcome.** The enemy's icon shows the Enrage indicator mid-turn (on step 5). The previously telegraphed intent (which did not resolve) is replaced in the HUD by the new Enrage-selected intent at step 7. The player sees: enemy skipped its attack, and it is now enraged with a new, potentially more dangerous tell. This is a high-information turn for the player — they get one free turn of observation after Enrage activates.

**Designer intent.** Stalled is a tempo tool the player uses to delay enemy damage. It does not suppress Enrage — a player who stalls an enemy near its Enrage threshold is buying one skipped attack, not preventing the behavior escalation. `TurnCount` advances regardless (C.7 invariant) so Enrage remains on its authored schedule.

**Detection.** Unit test `EnemyLifecycle_StalledAndEnrage_SameTurn_OrderingCorrect`: set up a combat context with `TurnCount` at `EffectiveEnrageTurn - 1`, Stalled active with 1 charge. Run one enemy turn. Assert sequence: `Stalled_Skipping` entered, `SelectingNextIntent` entered, `TurnCount` equals `EffectiveEnrageTurn`, `EnrageActive == true`, `OnEnrageActivated` fired, `SelectNextIntent` called with `EnrageActive = true` in context. Assert `IVehicleMutator` was NOT called during this turn.

### EC4 — NoOpIntent HUD Data Contract

**Trigger.** Any code path that returns the `NoOpIntent` sentinel: D.3 all-candidates-filtered, C.6 all-slots-`Offline` retarget, D.6 any policy that exhausts all candidates. The sentinel must carry a fully specified field set so the HUD (and any subscriber to `OnIntentTelegraphed` or `OnIntentRetargeted`) can render without null-checking individual fields or switching on undefined combinations.

**State behavior.** The `NoOpIntent` sentinel is an `EnemyIntent` struct with the following exact field values — no other values are valid for this sentinel:

| Field | Value | Rationale |
|---|---|---|
| `Type` | `IntentType.Utility` | The only `IntentType` with no mandatory payload — HUD treats Utility as free-form display. |
| `Target` | `TargetType.None` | No target exists; HUD must not attempt to highlight any player or enemy slot. |
| `TargetSlot` | `SlotId.None` | Explicit sentinel value; HUD must not render a slot indicator. |
| `PredictedDamage` | `0` | No damage payload. |
| `PredictedStatusStacks` | `0` | No status payload. |
| `StatusRef` | `null` | No status effect reference. |
| `ArmorGain` | `0` | No defend payload. |
| `HealAmount` | `0` | No heal payload. |

HUD contract (this GDD owns the data; Combat HUD UX Spec owns the visual): when `NextIntent.Type == Utility && NextIntent.Target == None`, the HUD renders a neutral "—" tell in the intent zone. The exact icon, color, and animation treatment are owned by Combat HUD UX Spec. This GDD mandates only that the HUD has sufficient data to render a non-null, non-error state without reading any field that could be null or zero in a misleading way.

**Player outcome.** The intent zone in the combat HUD shows the "—" neutral indicator. No slot highlight is drawn on either vehicle. No numeric damage, stack count, or status icon is displayed. The player reads this as "the enemy is doing nothing this turn" — which is always accurate when `NoOpIntent` is emitted.

**Designer intent.** A fully specified sentinel eliminates conditional null checks and ambiguous zero-states scattered across HUD rendering code. The HUD has exactly one rendering branch for "no intent": `Type == Utility && Target == None`. All other `Utility` intents (legitimate authored Utility behaviors) must have `Target != None` to distinguish them from the sentinel — this is an authoring constraint, not an engine invariant.

**Detection.** Unit test `NoOpIntent_FieldContract_AllFieldsCorrect`: instantiate `EnemyIntent` via the `NoOpIntent` factory/constant; assert every field matches the table above, including `StatusRef == null`. Integration test `HUD_NoOpIntent_RendersNeutralTell`: inject `NoOpIntent` into the HUD's intent display path; assert the neutral "—" element is visible and no slot highlight is active.

### EC5 — Status DOT Tick Kills Enemy During Stalled_Skipping or Retargeting

**Trigger.** A damage-over-time status effect (e.g., Ignite or Bleed) ticks at its designated point in the turn order while the enemy state machine is in `Stalled_Skipping` or `Retargeting`. The tick reduces the enemy's `HullHP` to `≤ 0`.

**State behavior.** The HP ≤ 0 check MUST be evaluated immediately after every damage event from any source — including status ticks — in every state. This is not a check reserved for `Resolving`. When HP ≤ 0 is detected:

- Current state exits immediately regardless of which state it is (`Stalled_Skipping` or `Retargeting`).
- State machine enters `EndOfCombat` directly, bypassing `Resolving`, `SelectingNextIntent`, and any pending `OnInvalidTarget` call.
- `OnCombatEnded(CombatEndedPayload)` fires exactly once with the following field values:
  - `TurnsToKill`: `TurnCount` at the moment `EndOfCombat` is entered (includes any increments that occurred this turn).
  - `EnrageReached`: the value of `EnrageActive` at the moment of death — reflects whether Enrage was ever set during this combat, which may be `true` even if the killing blow came from a DOT on a Stalled turn.
  - `PlayerVehicleCritical`: `true` if any player slot ended in `Offline` state at the moment of payload construction; `false` otherwise. Sourced from `IVehicleView` on the player vehicle at `EndOfCombat` entry.
  - All other `CombatEndedPayload` fields (`ArchetypeId`, `ArchetypeTags`, `DifficultyScore`, `BiomeIndex`, `WasElite`) populated from `EnemyDefinitionSO` as normal.
- Combat Loop tears down the enemy instance. No further state transitions occur.

**Player outcome.** The player sees the enemy vehicle's HP reaching zero during what would have been the enemy's turn. The combat end sequence (death animation, reward screen, or whatever the Combat HUD UX Spec defines) fires. The cause (DOT kill during a Stalled or Retargeting state) is transparent to the player — the outcome is identical to a normal kill.

**Designer intent.** Death from any source must terminate combat immediately. Allowing a dead enemy to continue resolving a Retarget or skipping a Stalled turn would produce logically invalid states (an enemy acting after it died). The DOT tick path is symmetric with the player DOT tick path per C.8 status parity.

**Detection.** Unit test `EnemyLifecycle_DotKill_DuringStalledSkipping_EntersEndOfCombat`: set enemy HP to 1, apply an Ignite status with 1 remaining stack, activate Stalled. Run one enemy turn. Assert: `Stalled_Skipping` entered, DOT tick fires, enemy HP reaches 0, `EndOfCombat` entered, `OnCombatEnded` fires with `TurnsToKill` matching `TurnCount`, `Resolving` was NOT entered. Parallel test for `Retargeting` state: same setup, enemy HP 1, Bleed active, enemy turn begins with `TargetSlot` Offline.

### EC6 — Enemy Frame Destroyed While Hull HP > 0 / All Enemy Slots Offline While Hull HP > 0

**Trigger (Frame destroyed).** A player card or status effect reduces the enemy's `Frame` slot to `SlotHP[Frame] ≤ 0` (Offline) while the enemy's `HullHP > 0`. Frame slot enters `Offline` state via `IVehicleMutator`. `HullHP` is not automatically set to 0 — slot destruction and vehicle death are separate events governed by V&P rules (C.1 symmetry).

**Trigger (all slots Offline).** A sequence of player actions drives all four enemy slots (`Weapon`, `Engine`, `Mobility`, `Frame`) to `Offline` while `HullHP > 0`. This is the symmetric counterpart to the all-player-slots-Offline condition addressed in EC1.

**State behavior.** V&P rules (symmetric per C.1 and C.9) govern what happens to the vehicle state when a slot reaches 0 HP — this GDD does not redefine those rules. From the Enemy System's perspective:

- The enemy remains a valid `IVehicleView` target for status ticks and player cards until `HullHP ≤ 0`.
- The enemy brain still executes its turn. If `BrainRulesets` are carelessly authored to hard-require an enemy slot being `Online` (e.g., a weight modifier condition `enemy.Weapon.state == Online` with `Multiplier = 0.0` on every candidate), the brain may produce `NoOpIntent` on every subsequent turn — legal but non-functional. See authoring rule below.
- `RetargetPolicy` and intent selection continue to execute normally each turn. An enemy with all slots `Offline` can still select and telegraph `Damage` intents that target player slots — those intents route through `Resolving` via `IVehicleMutator` identically to normal intent resolution. The enemy's slot state does not gate its ability to produce and execute intents (no privileged enemy code path per C.1).
- If the enemy selects a `Damage` or `Status` intent that implicitly draws from an authored slot's effect reference (e.g., the Weapon slot's `DamageEffectSO`), and that slot is `Offline`, the behavior is governed by EC9 (null effect reference handling) if the reference is missing, or by V&P mutator rules if the reference is present but the slot is destroyed.
- `EndOfCombat` is NOT entered until `HullHP ≤ 0`. A vehicle with all slots `Offline` and positive `HullHP` is alive by the rules.

**Player outcome.** The player may observe an enemy whose chassis indicator shows all slots destroyed but the vehicle is still mobile (positive HP bar). The enemy continues to telegraph and potentially execute intents. This state is edge-case rare in practice — destroying all four enemy slots without reducing `HullHP` to zero requires specifically targeting slot HP without contributing to hull damage, which V&P rules govern. It teaches the player that slot destruction and killing are distinct.

**Designer intent.** Symmetric treatment with the player is the non-negotiable constraint (C.1). The Enemy System does not introduce an enemy-exclusive "all slots down = instant death" shortcut because doing so would create an asymmetric code path. V&P owns that decision. The authoring risk (brains that become non-functional when enemy slots go Offline) is a Section G authoring responsibility, not an engine invariant.

**Detection.** Unit test `EnemyLifecycle_AllEnemySlotsOffline_HullPositive_BrainStillCalled`: set all enemy slot HP to 0 (Offline) via `IVehicleMutator`; set `HullHP = 10`; run one enemy turn; assert `SelectNextIntent` was called, `EndOfCombat` was NOT entered, state returned to `Telegraphed`. Playtest scenario: author a test enemy archetype with all slots at 1 HP, player deck containing four single-target slot-damage cards; verify the enemy remains interactive after all slots destroyed.

> **Carry to Section G:** `BrainRulesets` MUST NOT hard-require enemy slot `Online` conditions that suppress all candidates to `FinalWeight ≤ 0` when enemy slots are destroyed. An enemy whose brain collapses to perpetual NoOp after a slot is destroyed is an authored defect, not a system invariant. Every `BrainRuleset` must have at least one candidate that remains valid regardless of enemy slot state — typically a `FixedSlot(Frame)` damage intent or a Utility candidate with no slot-dependent conditions.

### EC7 — Brain Returns Null on First-Turn Bootstrap

**Trigger.** `IEnemyBrain.SelectNextIntent(CombatContext)` is called during `SelectingFirstIntent` (the bootstrap call before player turn 1) and returns `null`. This is a programming error in the brain implementation — the `IEnemyBrain` contract mandates a non-null return from `SelectNextIntent` (C.4 Telegraph Contract).

**State behavior.** The Combat Loop evaluates the return value from `SelectNextIntent` before advancing state. `null` is detected. `InvalidCombatStateException` is thrown with message: `"IEnemyBrain.SelectNextIntent returned null for archetype {ArchetypeId} at TurnCount {TurnCount}. Brain implementations must always return a non-null EnemyIntent."` Combat halts. The state machine does NOT advance to `Telegraphed`. No `OnIntentTelegraphed` event fires. The exception propagates to the Combat Loop's error boundary — session recovery behavior (return to map, save corruption handling) is owned by Card Combat GDD, not this GDD.

This is not a recoverable state. There is no silent default, no fallback NoOp substitution, no warning-and-continue path. A null return from `SelectNextIntent` is categorically different from a `NoOpIntent` sentinel — `NoOpIntent` is a valid authored outcome; `null` is a contract violation that indicates a broken implementation.

Prevention is owned by two gates:

1. **SO import validator** (carry to Section J): reject any `EnemyDefinitionSO` that has a null `BrainRuleset` reference, OR whose `BrainRuleset` has zero candidates with `BaseWeight > 0` in `BaseCandidates`. A ruleset where every candidate has `BaseWeight = 0` guarantees that `SelectNextIntent` will filter all candidates to `FinalWeight ≤ 0` and return `NoOpIntent` — which is recoverable (EC2) — but a null `BrainRuleset` reference means the brain cannot be instantiated at all, which triggers the null-return path. These are distinct failure modes.
2. **Unit tests on every `IEnemyBrain` implementation**: assert that `SelectNextIntent` never returns null for any valid `CombatContext`, including extreme degenerate contexts (all slots Offline on both vehicles, `TurnCount = 999`, `EnrageActive = true`).

**Player outcome.** Combat terminates abnormally. The player does not see a death screen or a reward screen — the session error boundary governs the recovery experience. This outcome should be impossible in production if both prevention gates are active.

**Designer intent.** A null telegraph is an invisible enemy — the player cannot read what cannot be shown. The Telegraph Contract (C.4) is a Pillar 3 invariant; violating it by silently substituting a default would mask a broken brain implementation and allow incorrect behavior to ship. A hard exception surfaces the error immediately in development.

**Detection.** Unit test `EnemyBrain_NullReturn_ThrowsInvalidCombatStateException`: mock an `IEnemyBrain` implementation whose `SelectNextIntent` returns `null`; invoke the Combat Loop's bootstrap sequence; assert `InvalidCombatStateException` is thrown with the expected message. SO import validator integration test: attempt to import an `EnemyDefinitionSO` with `BrainRuleset = null`; assert import is rejected with an error in the Unity console.

### EC8 — Stalled Expired and Enrage Activates Same Turn (Non-Interaction)

**Trigger.** On an enemy turn, Stalled is NOT active at turn start (the status cleared on a prior turn; `RemainingDuration = 0`). Additionally, after `TurnCount` increments in `SelectingNextIntent`, `TurnCount >= EffectiveEnrageTurn` is true for the first time. Both conditions are true in the same turn. This edge case is distinct from EC3: in EC3, Stalled is active and the enemy skips its turn (`Stalled_Skipping`). In EC8, Stalled has already expired — the enemy resolves normally and Enrage activates at `SelectingNextIntent`.

**State behavior.** Exact ordering — no ambiguity:

1. Enemy turn begins. Stalled is NOT active. No `Stalled_Skipping`. State proceeds: `Telegraphed` → branch check → no Stalled → `TargetSlot` check → (Offline?) → Retargeting or → `Resolving` normally.
2. Intent resolves through `IVehicleMutator`. Full damage/status/effect chain executes.
3. `Resolving` → `SelectingNextIntent`.
4. `TurnCount` increments.
5. Enrage threshold check: `TurnCount >= EffectiveEnrageTurn`. If true for the first time: `EnrageActive = true`. `OnEnrageActivated(VehicleState)` fires.
6. `SelectNextIntent(CombatContext)` called with `EnrageActive = true`. If `EnrageIntentCandidates` is non-empty, brain draws from it. New intent telegraphed.
7. `SelectingNextIntent` → `Telegraphed`.

There is no ordering ambiguity because Stalled expiration and Enrage activation occupy different points in the state machine: Stalled is evaluated at the top of the enemy turn (before `Resolving`); Enrage is evaluated at `SelectingNextIntent` (after `Resolving`). They cannot conflict.

**Player outcome.** The enemy resolves its telegraphed intent normally (it was not stalled this turn). Then it telegraphs a new, Enrage-selected intent for the next turn. The Enrage indicator appears in the HUD at step 5. The player sees: the enemy attacked, and it is now enraged. This is the intended "turn-pressure escalation" moment — the player had full information about the attack and can now read the new Enrage-state tell.

**Designer intent.** Documenting this as a NON-interaction is necessary because the superficially similar EC3 (Stalled active + Enrage activates) is a different path with different outcomes. Authors and QA engineers must not conflate the two. The key distinction: in EC3 the attack is skipped; in EC8 the attack resolves, and then Enrage activates.

**Detection.** Unit test `EnemyLifecycle_StalledExpired_EnrageActivatesSameTurn_AttackResolves`: set up a context where Stalled has `RemainingDuration = 0` (already cleared) and `TurnCount = EffectiveEnrageTurn - 1`. Run one enemy turn. Assert: `Stalled_Skipping` NOT entered, `IVehicleMutator` called during `Resolving`, `TurnCount` increments to `EffectiveEnrageTurn`, `EnrageActive == true`, `OnEnrageActivated` fired, `SelectNextIntent` called with `EnrageActive = true`.

### EC9 — DamageEffectSO (or Any Authored Effect Reference) Is Null on Selected Candidate

**Trigger.** `IEnemyBrain.SelectNextIntent` returns an `EnemyIntent` where the selected candidate references a `DamageEffectSO` (or equivalent effect asset reference) that is null at resolution time. This occurs during `Resolving` when `IVehicleMutator` is invoked with the intent's effect payload. This is an authoring error — every intent candidate with `Type = Damage` must reference a non-null `DamageEffectSO`.

**State behavior.** `IVehicleMutator` receives the intent payload and evaluates the effect reference. Null effect reference: `IVehicleMutator` rejects the call. This is not a crash — `IVehicleMutator` returns without applying any mutation and logs an error: `"IVehicleMutator: null effect reference on intent candidate '{candidateLabel}' for archetype {ArchetypeId}. Intent resolved as NoOp."` The enemy turn continues: `Resolving` completes with no mutation applied (equivalent to `NoOpIntent` resolution). `Resolving` → `SelectingNextIntent` as normal. `TurnCount` increments. Brain called for next intent.

This is NOT a `NoOpIntent` sentinel path — the intent that reached `Resolving` was a `Damage` intent with a non-null `EnemyIntent` struct. The NoOp behavior happens at the mutator level, not at the brain level. The distinction matters for log tracing: the error surfaces in the mutator layer, not in the brain layer.

Prevention is owned by the SO import validator (carry to Section J): at `EnemyDefinitionSO` import time, validate that every intent candidate in `BrainRuleset.BaseCandidates` and `BrainRuleset.EnrageIntentCandidates` that has `IntentType != Utility` carries a non-null effect reference appropriate to its type (`DamageEffectSO` for `Damage`, `StatusEffectSO` reference for `Status`, etc.).

**Player outcome.** The enemy's turn resolves visually as though nothing happened — no hit animation, no damage number, no status indicator. The player observes the enemy telegraphed a `Damage` intent but produced no effect. The HUD does not update to show "—" because the intent that was telegraphed was a legitimate `Damage` intent, not a `NoOpIntent`. This discrepancy (telegraphed damage, no resolution) is a visible artifact of the authoring error. In production, the SO import validator prevents this from reaching players.

**Designer intent.** `IVehicleMutator` must not crash on a null effect reference because other systems (player card resolution) also route through it, and a crash in the mutator would terminate the session. The error-log-and-continue pattern surfaces the defect in QA without destroying the session.

**Detection.** Unit test `IVehicleMutator_NullEffectRef_LogsErrorAndSkips`: invoke `IVehicleMutator` with a `Damage` intent carrying a null `DamageEffectSO`; assert no mutation applied to target vehicle, error log contains the expected message, no exception thrown. SO import validator test: attempt to import an `EnemyDefinitionSO` with a `BrainRuleset` candidate whose `DamageEffectSO` field is unassigned; assert import rejected.

### EC10 — Brain Returns a Still-Invalid Target from OnInvalidTarget

**Trigger.** `IEnemyBrain.OnInvalidTarget(CombatContext)` is called (state: `Retargeting`, entry condition: `NextIntent.TargetSlot` is `Offline`). The brain's `RetargetPolicy` is a `PriorityList` or `ContextualRule` that, due to an authoring oversight, names only slots that are currently `Offline` and does not include a fallback that is always available. The policy resolves and returns an `EnemyIntent` whose `TargetSlot` is still `Offline`. Per transition rule 4, `OnInvalidTarget` fires exactly once per enemy turn — a second call is forbidden.

**State behavior.** `Retargeting` → `Resolving` (the transition is unconditional after `OnInvalidTarget` returns — per transition rule 4). The replacement intent from `OnInvalidTarget` — which has an `Offline` `TargetSlot` — is passed to `IVehicleMutator`. `IVehicleMutator` evaluates the target slot: state is `Offline`. `IVehicleMutator` rejects the target (an `Offline` slot is not a valid mutation target — V&P rules govern this). `IVehicleMutator` logs an error: `"IVehicleMutator: target slot {SlotId} is Offline for archetype {ArchetypeId}. Mutation rejected."` No mutation applied. `Resolving` completes as NoOp. `Resolving` → `SelectingNextIntent`. Turn ends without effect.

The one-call-per-turn invariant (transition rule 4) is what prevents an infinite loop: the system does NOT re-enter `Retargeting` to call `OnInvalidTarget` a second time. The mutator rejection is the terminal fallback.

Authoring-side guard (carry to Section G): every `PriorityList` retarget policy MUST include `Frame` as its final entry. Frame is the only slot guaranteed to always be present as a part of the vehicle definition — it cannot be absent from the vehicle structure, only `Offline` in degenerate states. Including `Frame` last provides a final fallback: if Frame is also `Offline`, the policy returns `NoOpIntent` as specified in D.6. A `PriorityList` without `Frame` as a terminal entry is an authoring error detectable by the SO import validator.

**Player outcome.** The enemy turn resolves as though the enemy did nothing. The HUD shows the original intent (which was retargeted mid-turn) replaced by whatever `OnInvalidTarget` returned — which may be a `Damage` intent whose slot indicator points to an `Offline` slot before the mutator rejects it. This is a visible artifact of the authoring error (the intent tell points to a destroyed slot and then nothing happens). In production, SO import validator and the authoring rule prevent this.

**Designer intent.** The one-call-per-turn invariant is a stability guarantee: no matter how broken the retarget policy, the combat loop never re-enters `Retargeting` and cannot infinite-loop. The mutator layer is the final rejection wall. Authoring discipline (Frame as terminal fallback) prevents this case from ever reaching the mutator rejection path in a correctly authored ruleset.

**Detection.** Unit test `EnemyBrain_OnInvalidTarget_StillOffline_MutatorRejectsNoInfiniteLoop`: set all player slots to `Offline`; author a `PriorityList(Weapon, Engine, Mobility)` policy (no Frame fallback); call `OnInvalidTarget`; assert: `OnInvalidTarget` called exactly once, `Resolving` entered, `IVehicleMutator` called once and rejected (error log present), `SelectingNextIntent` entered, `Retargeting` NOT re-entered. SO import validator test: attempt to import an `EnemyDefinitionSO` with a `PriorityList` retarget policy that does not include `Frame`; assert import rejected with authoring error.

### E.N — Archetype Position Identity (Card Combat R17 retrofit 2026-04-24)

Archetype authoring guidelines for position identity. These are non-binding tuning examples — content authors may deviate with design-lead signoff.

| Archetype class | `PreferredPosition` | Typical intent mix | Position feel |
|---|---|---|---|
| **Bomb-lobber** (e.g., Junk Mortar) | `Behind` | Majority `BonusIfBehind` attacks + one `RequiresBehind` heavy-hitter; low-weight utility that works at either position. | Enemy wants to stay Behind; will Reposition rather than attack from Ahead. Player strategy: mirror and stay Behind (match), or force Ahead (denies damage). |
| **Lancer** (e.g., Spear Rider) | `Ahead` | Majority `BonusIfAhead` attacks + one `RequiresAhead` charge; weaker behind-side fallback. | Enemy wants to stay Ahead; aggressive forward plays. Player strategy: mirror Ahead (accept damage race), or force Behind (neutralizes lancer). |
| **Brawler** (e.g., Scrap Brute) | `None` | Position-agnostic attacks (all `PositionRequirement = None`); may include `BonusIfAhead` Ram intent. | Enemy doesn't care about position. Player cannot cheese by repositioning — brawlers are an answer-shape check, not a positional puzzle. |
| **Specialist** (rare; future) | `Behind` or `Ahead` | Mixed; may combine `Requires*` utility intents with position-agnostic damage. | Case-by-case; authored per encounter. |

**Authoring constraint (SO validator, C.5 pool-filter invariant)**: An archetype MUST NOT have a candidate list whose `Requires*` intents cumulatively cover only one position without any cross-position fallback (where "fallback" = `None` or `BonusIf*` intent selectable at the opposing position). Without a fallback, the enemy becomes softlocked after a single Reposition fails. Validator check: at least one candidate must be selectable at each position (either `PositionRequirement = None`, `BonusIf*`, or `Requires*` matching that position). Violating archetypes are rejected at SO import with a clear error.

**Authoring constraint (bonus-only meaningful on BonusIf* )**: `PositionBonus` MUST be `0` on candidates where `PositionRequirement ∈ {None, RequiresAhead, RequiresBehind}`. Validator check runs at SO import.

**Authoring recommendation (bomb-lobber/lancer balance)**: `PositionBonus` values should scale so that `PredictedDamage + PositionBonus` at preferred position is in range `(1.3–1.8) × PredictedDamage` — enough to make position read meaningfully, not so much that off-position is trivial. Formal range: `PositionBonus ∈ [0.3 × PredictedDamage, 0.8 × PredictedDamage]`, rounded to int, for `BonusIf*` intents.

### Edge Case Coverage Summary

| EC # | Category | Seeded By | Detection Method |
|---|---|---|---|
| EC1 | Degenerate target state — all player slots Offline | C.6 retarget rule; D.6 policy resolution; Transition rule 1 | Unit test (brain + retarget policy with all-Offline context); log assertion |
| EC2 | Malformed BrainRuleset — all candidates filtered | D.3 filter rule; D.3 edge-case note | Unit test (`SelectNextIntent` with zero-weight candidates); SO import validator check |
| EC3 | Stalled + Enrage co-activation (Stalled skips, Enrage activates at `SelectingNextIntent`) | C.7 Enrage rule; Transition rule 3; D.4 Stalled interaction note | Unit test (lifecycle ordering assertion); event sequence assertion |
| EC4 | NoOpIntent HUD data contract — full field specification | C.3 intent data model; C.6 sentinel definition; D.6 sentinel definition | Unit test (struct field assertion); integration test (HUD render path with NoOpIntent) |
| EC5 | Status DOT kills enemy during `Stalled_Skipping` or `Retargeting` | C.8 status parity; Transition rule 6; C.11 payload spec | Unit test (`Stalled_Skipping` DOT kill + `Retargeting` DOT kill); event field assertion |
| EC6 | All enemy slots Offline while Hull HP > 0 | C.1 Enemy = Vehicle symmetry; C.9 mutator parity | Unit test (brain called after all slots Offline, `EndOfCombat` not entered); playtest scenario |
| EC7 | Brain returns null on first-turn bootstrap | C.4 Telegraph Contract; Transition rule 2 | Unit test (`InvalidCombatStateException` thrown); SO import validator (null `BrainRuleset` rejected) |
| EC8 | Stalled expired + Enrage activates same turn (Stalled NOT active — non-interaction) | C.7 Enrage rule; D.4; Transition rules 1 and 3 distinction | Unit test (attack resolves, Enrage activates at `SelectingNextIntent`, no `Stalled_Skipping`) |
| EC9 | Null `DamageEffectSO` on selected intent candidate | C.9 mutator parity; D.3 candidate structure | Unit test (`IVehicleMutator` null-ref rejection + error log); SO import validator (null effect ref rejected) |
| EC10 | `OnInvalidTarget` returns still-invalid (Offline) target | C.6 one-call-per-turn invariant; Transition rule 4; D.6 policy spec | Unit test (one `OnInvalidTarget` call, mutator rejection, no re-entry to `Retargeting`); SO import validator (no-Frame `PriorityList` rejected) |

### Authoring Rules Carried to Section G

The following authoring rules were discovered while specifying edge cases in this section. Each must appear in Section G (Tuning Knobs and Authoring Guide) as an explicit authoring constraint with the stated rationale. They are NOT engine invariants — the engine handles violations gracefully (via NoOp, error log, or `InvalidCombatStateException`) — but correct authored content should never exercise those fallbacks in production.

1. **`allPlayerSlotsOffline` weight modifier (EC1).** Every `BrainRuleset` MUST author a weight modifier condition for `allPlayerSlotsOffline == true`. The modifier may suppress damage intents (multiplier 0.0) to avoid repeated NoOp cycles, or it may be left as-is to accept NoOp as the designed outcome when no valid player targets exist. Omitting this modifier does not break the engine, but produces an enemy that telegraphs damage intents it can never resolve — a legibility failure for the player.

2. **BrainRulesets must not hard-require enemy slot Online conditions (EC6).** No `BrainRuleset` may be authored such that all candidates are suppressed by conditions that evaluate false when enemy slots go Offline. Every ruleset must retain at least one candidate that is valid regardless of the enemy's own slot state — typically a `FixedSlot(Frame)` damage intent or an unconditional Utility candidate.

3. **`PriorityList` retarget policies must include `Frame` as the terminal entry (EC10).** `Frame` is the only slot guaranteed structurally present on every vehicle. Including it last in any `PriorityList` ensures the policy degrades to `NoOpIntent` (via D.6 Frame-Offline handling) rather than reaching `IVehicleMutator` with a guaranteed-`Offline` target.

4. **SO import validator: non-null `BrainRuleset` reference required (EC7).** Any `EnemyDefinitionSO` with a null `BrainRuleset` field must be rejected at import time. The validator must also reject any `BrainRuleset` where `BaseCandidates` contains zero entries with `BaseWeight > 0` (carry to Section J acceptance criteria).

5. **SO import validator: non-null effect references required on all non-Utility candidates (EC9).** Every intent candidate in `BrainRuleset.BaseCandidates` and `BrainRuleset.EnrageIntentCandidates` with `IntentType != Utility` must carry a non-null, valid asset reference for its payload type (`DamageEffectSO` for `Damage`, `StatusEffectSO` for `Status`). Candidates with null refs must be rejected at import time (carry to Section J acceptance criteria).

## Dependencies

### F.1 — Upstream (Hard Dependencies)

Systems this GDD reads from. Each row names the contract consumed and the authority GDD that owns it.

| System | Contract Consumed | Authority | Data Flow |
|---|---|---|---|
| Card Combat | `CombatContext` (vehicle states, `TurnCount`, `EnrageActive`, `CombatRng`, status stacks); `InvalidCombatStateException`; Combat Loop's state machine calls | Card Combat GDD | Brain receives `CombatContext` on every `SelectNextIntent` / `OnInvalidTarget` call. Exceptions propagate to loop. |
| Vehicle & Part | `IVehicleView` (read), `IVehicleMutator` (write); `VehicleState` POCO; `SlotId` enum; Armor resolution rules (AC-CC33–39); symmetric Frame-only Armor | V&P GDD | All enemy state read/write routes through these interfaces. No privileged path. |
| Status Effects | `ApplyStatusEffectSO`, `StatusEffectSO`, tick ordering, duration cap (3), Stalled semantics | Status Effects GDD | Enemy applies/receives status through the identical pipeline. Stalled consumed during `Stalled_Skipping` state. |
| Card System | `DamageEffectSO`, `TargetType` enum, damage resolution math | Card System GDD | Enemy intent candidates reference `DamageEffectSO` for damage payloads. `TargetType.EnemySubsystem` symmetric inverse: player slots as target. |
| Node Map | `BeaconType` enum | Node Map GDD | Read-only consumer. `IEnemyPool.GetEnemyFor` accepts `BeaconType` to key pool selection (`Combat` vs. `EliteCombat`). |

Hard contract: if any upstream interface changes shape, Enemy System must be re-verified against it. No in-house duplicate implementations exist.

### F.2 — Downstream (Consumers)

Systems that read from this GDD. Each row names the contract owned here and the provisional default if the consumer GDD is undesigned at implementation time. Provisional defaults mirror the 📌 flags in Section C.

| System | Contract Exposed | Status | Provisional Default |
|---|---|---|---|
| Loot & Reward | `event Action<CombatEndedPayload> OnCombatEnded` | Undesigned | Emit to no-op subscriber; no rewards granted until L&R wired. Payload shape is a hard contract — changes require breaking-change notice. |
| Node Encounter | `IEnemyPool.GetEnemyFor(biomeIndex, BeaconType, System.Random runSeed)` | Undesigned | `DefaultEnemyPool` returns hardcoded `EnemyDefinitionSO` for `biomeIndex = 0` regardless of beacon type, so combat runs in isolation. |
| Combat HUD UX Spec | `IVehicleView.NextIntent`; `OnIntentTelegraphed`, `OnIntentRetargeted`, `OnEnrageActivated` events; intent tell as primary HUD element (hard constraint) | Undesigned | HUD displays `IntentType.ToString()` + `PredictedDamage` int in a debug zone until the icon language is authored. |
| Biome | `BiomePoolSO` ScriptableObjects keyed by `biomeIndex` | Undesigned | All archetypes set to `biomeIndex = 0`; pool selection ignores biome until Biome GDD ratifies the strip-progression scheme. |

### F.3 — Back-Reference Updates Required

Other GDDs must be amended to reference this GDD's contracts. These retrofits are queued post-Phase-5 and execute when the relevant GDD is next touched or during its implementation.

**Hard retrofits (existing GDDs):**

- **V&P GDD** — Add explicit note: `IVehicleView` and `IVehicleMutator` apply symmetrically to enemy vehicles; no enemy-exclusive interface. `VehicleState` POCO is identical for player and enemy. Armor resolution (AC-CC33–39) applies symmetrically.
- **Card Combat GDD** — Add reference to C.6 (this GDD) as the closure of OQ-CC1 (enemy retargeting policy per-archetype). Confirm `CombatContext` exposes `allPlayerSlotsOffline` boolean derivable from `IVehicleView` or compute-on-construct (Card Combat's choice; Enemy System weight modifiers depend on it being present). Confirm `CombatRng` is inside `CombatContext` (not passed separately). **Card Combat R15–R18 + F-CC5 closed by this GDD's C.2 / C.3 / C.5 / C.6 / D.3 / D.8 / E.N retrofits (2026-04-24 Position & Movement propagation).** Enemy System authoritatively owns: `EnemyDefinitionSO.PreferredPosition`, `EnemyDefinitionSO.CanReposition` (reserved), `EnemyIntent.PositionRequirement`, `EnemyIntent.PositionBonus`, `EnemyIntent.TargetPosition` (for reserved archetype-authored Reposition), and the archetype position-identity authoring rules + SO import validator constraints (E.N). Card Combat authoritatively owns: encounter types (R15 Standard / Ambush + `CombatRulesSO.DefaultEncounterType` / `AmbushStartingPosition`), pool-filter invariant before the weighted draw (R17), synthetic `RepositionIntent` injection when the pool is empty (R18 — the brain never authors Reposition), enemy position inversion of player `PositionState.Position` (S2), and the F-CC5 position-bonus damage formula applied at damage-resolution time.
- **Status Effects GDD** — Add note confirming C.8 symmetric parity: Stalled consumption on `Stalled_Skipping`; DOT ticks fire in every enemy state and may trigger `EndOfCombat` per EC5. No enemy-side duplicate code.
- **Card System GDD** — Confirm `DamageEffectSO` and `TargetType` enum are authored for cross-vehicle consumption. Enemy candidates reference the same asset type; no enemy-only damage asset exists.
- **Node Map GDD** — Confirm `BeaconType` enum is owned by Node Map, consumed read-only by Enemy System via `IEnemyPool`. If Node Map adds new `BeaconType` values, the `BiomePoolSO` authoring surface must extend to accept them.

**Soft retrofits (undesigned GDDs — to be satisfied on authoring):**

- **Loot & Reward GDD** — Must subscribe to `OnCombatEnded(CombatEndedPayload)` and define reward scaling over the payload's eight fields. `DifficultyScore` is the primary reward scalar per D.5. `PlayerVehicleCritical` is available for close-call reward boosts if L&R wants one.
- **Node Encounter GDD** — Must call `IEnemyPool.GetEnemyFor(biomeIndex, beaconType, runSeed)` at beacon population time. `biomeIndex` sourcing is Node Map's concern; `beaconType` is Node Encounter's decision (Combat vs. EliteCombat selection).
- **Combat HUD UX Spec** — Must honor the primary-HUD-element hard constraint from C's 📌 Combat HUD flag: reserved screen zone, minimum read target size, contrast guarantee, z-order un-occludable. Must define the Intent Icon Language (per-`IntentType` icon + color + target-slot indicator). Must render Enrage state visually on `OnEnrageActivated`, and must render the `NoOpIntent` neutral "—" tell per EC4.
- **Biome GDD** — Must ratify `biomeIndex` progression (strip 1 → N → gate → next biome). Must declare whether per-biome visual-motif layering is available post-MVP (C.10 deferred motif system).

### F.4 — Data Asset Authoring

Enemy System authors the following ScriptableObject asset types. Paths are prescriptive.

| Asset Type | Path | Purpose | Cross-References |
|---|---|---|---|
| `EnemyDefinitionSO` | `assets/data/enemies/archetypes/` | One per archetype. Stat block, subsystem loadout, `BrainRuleset` ref, `RetargetPolicy`, `ArchetypeTags`, `DifficultyScore`, art-contract fields (`SilhouetteClass`, `VisualFamily`). | References `BrainRuleset`; references `PartDefinitionSO` (V&P) for subsystem loadout. |
| `BrainRuleset` | `assets/data/enemies/rulesets/` | Weighted intent candidates + context modifiers; `BaseCandidates` and `EnrageIntentCandidates` lists. May be shared across archetypes when behavior overlaps. | References `DamageEffectSO` (Card System), `StatusEffectSO` (Status Effects). |
| `BiomePoolSO` | `assets/data/enemies/pools/` | One per biome. Weighted `EnemyDefinitionSO` references per `BeaconType`. | References `EnemyDefinitionSO` (this GDD); consumed by Node Encounter via `IEnemyPool`. |

No status effect, damage effect, or part definition assets are authored here — those belong to Status Effects, Card System, and V&P respectively.

**SO Import Validator** (carry to Section J): The validator responsible for rejecting malformed `EnemyDefinitionSO` / `BrainRuleset` assets at import time (per EC2, EC7, EC9, EC10) lives alongside these asset directories. Implementation is an engineering concern owned by `unity-specialist` during the `/architecture-decision` pass for the enemy data pipeline. Validator rules are enumerated as authoring constraints in Section E's "Authoring Rules Carried to Section G" and will be formalized as acceptance criteria in Section J.

## Tuning Knobs

Every tunable surface in the Enemy System, with default, safe range, and the gameplay axis it controls. Values live on `CombatConfig` (global) or on the relevant ScriptableObject asset (per-archetype / per-ruleset). Changes outside safe ranges require a tuning memo.

### G.1 — Global Combat Constants (on `CombatConfig`)

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `DefaultEnrageTurn` | 8 | 5–12 | Pacing of every combat that reaches late turns. Below 5 = frantic; above 12 = combat runs can stall. |
| `DefaultEnrageBaseBonus` | 2 | 0–8 | Flat bonus added to `ResolvedDamage` on the Enrage-activation turn. Per F-CC, this grows by +1 each subsequent turn (escalation is not tunable — it is a system-wide pacing constant). 0 = Enrage is behavior-only; 8 = immediately lethal on activation. |
| `BiomeHPScalar[1]` (Sand Flats) | 1.0 | 0.8–1.2 | Biome 1 baseline. 1.0 is the anchor — every other scalar is defined relative to it. |
| `BiomeHPScalar[2]` (Junk Mesa) | 1.4 | 1.2–1.6 | Mid-biome step-up. Below 1.2 = no felt progression; above 1.6 = un-upgraded vehicles wall out. |
| `BiomeHPScalar[3]` (Haven Approach) | 1.9 | 1.6–2.2 | Late-biome pressure. Below 1.6 = Haven approach trivial; above 2.2 = requires optimal build. |
| `BiomeDamageScalar[1]` | 1.0 | 0.8–1.2 | Biome 1 baseline damage. |
| `BiomeDamageScalar[2]` | 1.3 | 1.1–1.5 | Mid-biome damage step. |
| `BiomeDamageScalar[3]` | 1.6 | 1.4–1.8 | Haven-approach damage. 1.8 = a single unblocked hit threatens a Frame slot. |
| `EliteHPScalar` | 1.75 | 1.3–2.0 | Elite combat length. Higher = longer elite fights (more decision points, more attrition). |
| `EliteDamageScalar` | 1.4 | 1.2–1.6 | Elite damage spike. Lower than HP scalar intentionally — elite fights should be long, not explosive. |
| `DifficultyScore.HPWeight` | 0.30 | 0.20–0.40 | How much HP contributes to the DifficultyScore that scales rewards. |
| `DifficultyScore.DPTWeight` | 0.40 | 0.30–0.50 | How much average damage-per-turn contributes. Highest weight because damage directly threatens the player. |
| `DifficultyScore.IntentCountWeight` | 0.15 | 0.10–0.20 | How much the archetype's candidate-list size contributes (complexity proxy). |
| `DifficultyScore.EnrageSeverityWeight` | 0.15 | 0.10–0.20 | How much the Enrage post-activation damage premium contributes. |

Weight constraint: `DifficultyScore` weights must sum to 1.0 (enforced by `CombatConfig` validator). Changing any weight without offsetting the others invalidates the DifficultyScore anchor bands in D.5.

### G.2 — Per-Archetype Knobs (on `EnemyDefinitionSO`)

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `BaseHullHP` | — (authored) | 20–120 | Archetype durability pre-biome-scaling. Sum of all four `BaseSlotHP[s]`. |
| `BaseSlotHP[Weapon]` | — | 4–40 | Weapon-slot durability. Low values create fragile-fronted archetypes. |
| `BaseSlotHP[Engine]` | — | 4–40 | Engine-slot durability. |
| `BaseSlotHP[Mobility]` | — | 4–40 | Mobility-slot durability. |
| `BaseSlotHP[Frame]` | — | 4–40 | Frame-slot durability. Frame is the Armor-bearer; higher values make Armor-strip strategies costly. |
| `MaxArmorContribution[part]` | — | 0–15 per part | Sum of per-part Armor contributions. 0 = no-Armor archetype (rare, intentional). |
| `EnrageTurn` override | null (uses `DefaultEnrageTurn` = 8) | 4–14 | Per-archetype Enrage timing. Elites and Haven archetypes often set to 6–7; trash archetypes rarely override. |
| `EnrageBaseBonusOverride` | null (uses `DefaultEnrageBaseBonus` = 2) | 0–8 | Per-archetype override of the flat Enrage damage bonus. 0 = behavior-shift-only Enrage. 8 = boss-tier spike. Most archetypes leave null. The +1/turn escalation term is system-wide and NOT overridable. |
| `DifficultyScore` | — (computed per D.5) | 0.0–1.0 | Reward scalar. Authored values are suggested; automated recomputation from stat block + ruleset is expected pre-release. |
| `RetargetPolicy` | `FixedSlot(Frame)` | — | See G.4 authoring rule #3. `FixedSlot(Frame)` is a Biome 1 teachable-moment default; Biome 2/3 archetypes MUST override. |
| `SilhouetteClass` | — | {Small, Medium, Large, Boss} | Art-contract field. No gameplay effect. Section H defines per-class sprite budgets. Canonical value set matches AC-ES2 and H.1.8. |
| `VisualFamily` | — | {Raider, Scavenger, Elite, Boss} | Art-contract field. No gameplay effect. Section H defines per-family biome skinning. |
| `ArchetypeTags` | — | string[], arbitrary | Propagated to `CombatEndedPayload` for Loot & Reward filtering. |
| `PreferredPosition` | `None` | {`Ahead`, `Behind`, `None`} | Card Combat R17 bias axis. Informs Reposition-fallback direction choice (Card Combat R18); does NOT gate intent selection. Bomb-lobber archetypes `Behind`, lancer archetypes `Ahead`, brawler archetypes `None`. E.N defines archetype class → preferred position mapping. |
| `CanReposition` (reserved) | `true` | `true` (EA-locked) | Reserved for post-MVP stationary archetypes (turret-style). All EA archetypes MUST leave `true`. SO import validator warns if `false` is combined with any `RequiresAhead` / `RequiresBehind` intent candidate (they would deadlock). |

### G.3 — Per-Ruleset Knobs (on `BrainRuleset` and its candidates / modifiers)

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| Candidate `BaseWeight` | — (authored) | 0.0–100.0 | Raw probability weight before context modifiers. 0 = always excluded (authored as a filter). |
| Candidate `BaseDamage` (on Damage candidates) | — | 4–30 | Raw telegraphed damage before biome/elite/Enrage scaling. |
| Candidate `PredictedStatusStacks` (on Status candidates) | — | 1–3 | Number of status stacks applied. Capped by Status Effects `RemainingDuration = 3` symmetrically. |
| Candidate `HealAmount` (on Heal candidates) | — | 4–20 | Self-heal magnitude. High values (>20) create un-killable archetypes; reserve for boss behaviors. |
| Candidate `ArmorGain` (on Defend candidates) | — | 3–10 | Self-Armor gain. Capped by vehicle MaxArmor (V&P rule). |
| `WeightModifier.Multiplier` | — | 0.0–10.0 | Context-conditional probability multiplier. 0.0 suppresses candidate entirely; 10.0 is a hard-preference spike (rare). |
| `EnrageIntentCandidates` (list) | empty | 0–6 candidates | Replacement candidate list when `EnrageActive`. Empty = damage-only Enrage. Non-empty = behavior-shift Enrage. |
| Candidate `PositionRequirement` (on any intent) | `None` | {`None`, `RequiresAhead`, `RequiresBehind`, `BonusIfAhead`, `BonusIfBehind`} | Card Combat R16 / R17. `None` = candidate always eligible. `Requires*` = Card Combat pool-filter excludes this candidate when enemy position doesn't match (R17); if the filtered pool becomes empty, Loop injects synthetic `RepositionIntent` (R18). `BonusIf*` = candidate stays in the pool; `PositionBonus` applies at resolution per F-CC5. |
| Candidate `PositionBonus` (on Damage candidates) | 0 | 0..12 | Card Combat F-CC5. Additive damage applied ONLY if `PositionRequirement` is `BonusIfAhead` / `BonusIfBehind` AND enemy position matches at resolution. Ignored on non-Damage intents and on `None` / `Requires*` requirements. HUD renders `PredictedDamage + conditional(PositionBonus)` per D.8. Balance sim uses `PredictedDamage + (0.5 × PositionBonus)` as DPT expected-value weight. |

### G.4 — Authoring Constraints

These rules are NOT engine invariants — the engine degrades gracefully if violated (NoOp, error log, `InvalidCombatStateException`). They are authorship requirements. The SO import validator catches some; the rest are caught at QA via the unit tests named in Section E.

1. **`allPlayerSlotsOffline` weight modifier required on every `BrainRuleset` (EC1).** Modifier may suppress damage intents (multiplier 0.0) or may accept NoOp — authorial choice. Missing modifier = enemy telegraphs damage intents it can never resolve, a legibility failure for the player. *Detection: unit test sampling all `BrainRulesets`.*

2. **`BrainRulesets` must not hard-require enemy slot `Online` conditions (EC6).** Every ruleset must retain at least one candidate valid regardless of enemy slot state — typically `FixedSlot(Frame)` damage or unconditional Utility. *Detection: unit test + playtest scenario with all-slots-Offline enemy.*

3. **Biome 2 and Biome 3 archetypes MUST override `RetargetPolicy` with `PriorityList` or `ContextualRule` (seeded in Section D).** `FixedSlot(Frame)` is the Biome 1 default that teaches the retarget concept; using it for Biome 2/3 archetypes creates trivializable enemies (player offlines Frame and the enemy becomes a NoOp generator). *Detection: SO import validator — reject `BiomePoolSO` entries at `biomeIndex > 1` whose `EnemyDefinitionSO.RetargetPolicy == FixedSlot(Frame)`.*

4. **`PriorityList` retarget policies must include `Frame` as the terminal entry (EC10).** `Frame` is the only structurally-always-present slot; terminal-Frame ensures the policy degrades to `NoOpIntent` cleanly rather than reaching the mutator with a guaranteed-Offline target. *Detection: SO import validator.*

5. **`EnemyDefinitionSO.BrainRuleset` must be non-null; `BrainRuleset.BaseCandidates` must contain at least one candidate with `BaseWeight > 0` (EC7).** *Detection: SO import validator.*

6. **Non-Utility intent candidates must carry non-null effect references (EC9).** Every `Damage` candidate needs a non-null `DamageEffectSO`; every `Status` candidate needs a non-null `StatusEffectSO` reference. *Detection: SO import validator.*

7. **Cross-position coverage (E.N).** Every `BrainRuleset` must retain at least one candidate with `PositionRequirement == None` (unconditional) OR must carry candidates spanning both `RequiresAhead` and `RequiresBehind`. Authoring only `RequiresAhead` (or only `RequiresBehind`) causes Card Combat's pool filter to empty whenever the enemy is in the non-matching position, forcing a synthetic `RepositionIntent` *every single turn* the enemy is mispositioned — a legibility failure. *Detection: SO import validator — reject rulesets whose candidate set has no `None` entry AND lacks at least one `RequiresAhead` + one `RequiresBehind`.*

8. **`PositionBonus > 0` only on `BonusIf*` candidates (E.N).** A non-zero `PositionBonus` on a `None` / `RequiresAhead` / `RequiresBehind` candidate is nonsensical: unconditional damage belongs in `PredictedDamage`, and `Requires*` candidates are already gated by position so a conditional bonus on top is double-gating. *Detection: SO import validator — reject candidates where `PositionBonus > 0 && PositionRequirement != BonusIfAhead && PositionRequirement != BonusIfBehind`.*

9. **`CanReposition == false` with any `Requires*` candidate deadlocks (C.2 reserved field).** If the archetype is immobile AND the pool filter can empty, Card Combat's R18 Reposition fallback is impossible — the archetype would loop on NoOp indefinitely when mispositioned. Reserved field; no EA archetype exercises this, but the validator warns early. *Detection: SO import validator.*

### G.5 — Tuning Telemetry Plan

Knob defaults in G.1 are educated guesses that will need real-run data to calibrate. Post-Early-Access telemetry should capture the following metrics (full telemetry infrastructure is owned by a future Analytics GDD):

- **TurnsToKill distribution per archetype.** Identifies HP values too high (slow combat) or too low (trivialized archetype).
- **Enrage activation rate per combat.** The percentage of combats that reach `EnrageActive = true`. Target: 15–30% (Enrage should be a real threat, not typical).
- **HUD-Promise violation count.** The count of `ResolvedDamage` values > `PredictedDamage` that were NOT caused by Enrage activation mid-turn. Target: 0 (any non-zero is a bug).
- **`NoOpIntent` emission rate per archetype.** High rates indicate brain authoring defects (EC1, EC2, EC6) that the engine degraded gracefully but that players are experiencing.
- **`RetargetPolicy` trigger rate per archetype.** How often `OnInvalidTarget` fires. Informs whether retarget complexity is actually exercised or whether the design is over-engineering.
- **DifficultyScore vs. actual run-outcome correlation.** Regression check: is the DifficultyScore formula a good predictor of "player-felt difficulty" (clear rate, TurnsToKill, close-call frequency)? Calibrates the four weights in G.1.

## Visual/Audio Requirements

The enemy's on-screen body and its sound are the primary channels through which Pillar 3 (Read to Win) and Pillar 2 (Chassis Identity) reach the player. This section is **hard constraint** territory: silhouette, intent icon, enrage cue, death cascade, and their audio counterparts are not decoration — they are the combat UI. The numbers below are the minimum legibility contract; archetype production may exceed them but must not fall below.

### H.1 Visual Requirements

#### H.1.1 Silhouette Language

Enemies resolve as one of four `SilhouetteClass` values. Sprite canvases, backed by archetype concept art:

| Class  | Canvas (px) | Purpose                                          |
|--------|-------------|--------------------------------------------------|
| Small  | 160 × 80    | Scavenger scouts, dune bikes; hit-and-run feel.  |
| Medium | 240 × 140   | Raider baseline, Elite patrol; core combat mass. |
| Large  | 320 × 200   | Heavy Raider, Elite champion; screen-anchor.     |
| Boss   | 400 × 260   | Act-end encounters only; visual event.           |

**Legibility tests (MANDATORY before art lock):**

1. **20%-scale test.** Render the silhouette at 20% of canvas in pure black against white. A playtester must name the `SilhouetteClass` and family (Raider / Scavenger / Elite / Boss) from silhouette alone within 2 seconds.
2. **Cross-class differentiation.** Any two archetypes sharing a `SilhouetteClass` must be distinguishable at full scale by silhouette outline (weapon protrusion, stance tilt, frame profile) — never only by color or texture.

#### H.1.2 Family Visual Vocabulary

| Family    | Grammar                                                    | Primary Color Anchor       |
|-----------|------------------------------------------------------------|----------------------------|
| Raider    | Weapon-heavy protrusion; asymmetric, aggressive stance.    | Iron oxide `#8B3A2A`       |
| Scavenger | Patchy 3-zone material (salvaged panels over bare frame).  | Bleached sand `#D4B896`    |
| Elite     | Symmetric, clean perimeter; reinforced seams.              | Tarnished steel `#7A7872`  |
| Boss      | Oversize; combines thorny (Raider) + blocky (Elite).       | Hybrid; per-boss signature |

Family readability must survive 20%-scale test (H.1.1.1). Archetype-level differentiation is additive on top of family grammar.

#### H.1.3 Biome-Exclusive Material Language

Per C.10 (biome-exclusive MVP rule), each archetype ships with **one** biome tint. The tint is baked into the sprite — no runtime shader re-tint.

| Biome          | Material Motif                          | Accent Palette                       |
|----------------|-----------------------------------------|--------------------------------------|
| Sand Flats     | Iron oxide 65-75% surface coverage.     | Amber wash `#C98A3A` for highlights. |
| Junk Mesa      | Green-black corrosion at panel seams.   | Corrosion `#8AAB2C` bloom at joints. |
| Haven Approach | Cold-concrete blue shadow overlay.      | Overlay `#4A6880` on shadow side.    |

#### H.1.4 Intent Icon Language

The intent icon is the single most-read pixel cluster on the enemy side of the HUD. Every archetype emits an icon + target-slot label every player turn (Combat HUD primary-element hard constraint, C📌-1).

- **Canvas:** minimum 48 × 48 px, rendered above the enemy silhouette in the intent zone (see Combat HUD UX Spec for exact placement).
- **Per-`IntentType` glyph + color:**

  | Type          | Glyph            | Color                      |
  |---------------|------------------|----------------------------|
  | Damage        | Red triangle ▲   | Raider red `#B03030`       |
  | Status        | Amber hexagon ⬡  | Sulfur `#E8B23A`           |
  | Heal          | Green plus +     | Verdigris `#4AA868`        |
  | Defend        | Steel arc        | Tarnished steel `#7A7872`  |
  | PositionShift | Blue diamond ◇   | Signal blue `#3A78B0`      |
  | Utility       | Sand gear ⚙      | Sand `#C8B890`             |
  | NoOpIntent    | Grey em-dash —   | Ash `#6A6660` (no bg fill) |

- **Target-slot label.** Below the glyph, the intent carries a short label ("WEAPON" / "ENGINE" / "MOBILITY" / "FRAME" / "SELF" / "—") in ash-white `#E8E0D4`, 12px monospace. NoOpIntent shows `—`, no slot.
- **Target-slot echo pulse.** When the player hovers over (or gamepad-focuses) the enemy intent, a 300ms ember-orange `#E8630A` pulse fires on the **player's** predicted-target slot. This is the Foresight kicker — the player's own chassis tells them where the hit is coming.

#### H.1.5 Enrage Visual Activation

When `EnrageActive` transitions false→true (per C.6 / D.4 / EC4):

1. **Activation burst:** a 200ms red `#B03030` flash floods the intent zone at 60% opacity.
2. **Persistent rim:** 2px red `#B03030` outline added around the enemy sprite for the remainder of combat.
3. **Persistent tint:** the enemy sprite shifts to a `#2A1414` multiply overlay at 30% opacity. Reads as "blood under iron."

Enrage is **permanent** (C.6) — the rim and tint do not pulse, do not fade. They are a static state change.

#### H.1.6 Retarget Transition

When `OnInvalidTarget` fires (C.5 / D.6), the intent icon rebinds to a new target-slot label. The transition is 350ms and runs in 3 phases:

| Phase           | Duration  | Behavior                                                                                   |
|-----------------|-----------|--------------------------------------------------------------------------------------------|
| Fade-out        | 0-120ms   | Old slot label fades to 0%; glyph remains.                                                 |
| Crossfade sweep | 120-280ms | Directional arrow wipes from old slot position to new slot position in ember `#E8630A`.    |
| Pulse-in        | 280-350ms | New slot label fades in + single ember-orange echo pulse on the new player-chassis slot.   |

The retargeting transition must never overlap with intent resolution — R4 (C.4) guarantees the player sees the rebound intent at player-turn-start.

#### H.1.7 Slot State Legibility

Enemies render slot states with the **same visual vocabulary as the player** (Pillar 3 symmetry):

| State    | Rendering                                                              |
|----------|------------------------------------------------------------------------|
| Online   | Slot icon at full saturation, green `#4AA868` status dot.              |
| Damaged  | Slot icon at 80% saturation, amber `#E8B23A` status dot.               |
| Offline  | Slot icon desaturated to 30%, red `#B03030` X overlay, no status dot.  |

**Frame Armor rendering:** Armor is shown as a **shield glyph + integer** (e.g., "⛊ 12"), NOT as a bar. Bar rendering is reserved for HP. This mirrors the player-side convention from V&P.

#### H.1.8 Death Cascade

When `EndOfCombat` triggers via enemy zero-HP (C.8):

| Time       | Behavior                                                                            |
|------------|-------------------------------------------------------------------------------------|
| 0-200ms    | Shadow beneath sprite shrinks to 0 (weight-leaving-body).                           |
| 200-500ms  | Sprite tilts 5° forward (direction of travel), brightness -20%.                     |
| 500-700ms  | Dust VFX spawns at sprite base (16-particle dust puff, sand/corrosion per biome).   |
| 700ms      | Sprite alpha=0, intent icon removed.                                                |

**Close-call variant.** If the killing blow reduced player HP to ≤20% of max within the same resolution window, the cascade is identical but the enemy sprite renders at +10% desaturation from 0ms. This is a quiet "you almost died" tell.

#### H.1.9 Asset Production Budget

Per-archetype frame counts (minimum — may exceed for hero archetypes):

| SilhouetteClass | Idle  | Intent tell  | Hit reaction | Enrage activation | Death    |
|-----------------|-------|--------------|--------------|-------------------|----------|
| Small           | 4 fr  | 3 fr × type  | 2 fr         | 6 fr              | 6 fr     |
| Medium          | 6 fr  | 4 fr × type  | 3 fr         | 8 fr              | 8 fr     |
| Large           | 8 fr  | 6 fr × type  | 4 fr         | 10 fr             | 10 fr    |
| Boss            | 12 fr | 8 fr × type  | 6 fr         | 16 fr             | 20 fr    |

**VFX budgets (per runtime instance, hard cap):**

- Damage intent zone: max 20 particles.
- Enrage activation burst: max 24 particles.
- Death dust puff: max 16 particles.
- Status-effect overlay on slot: max 8 particles.

**Z-ordering rule:** intent-zone VFX render **above** the enemy sprite but **below** the intent icon. No z-clipping into the chassis.

**Sheet ceiling:** 512 × 512 px per sprite sheet. Archetypes needing more frames split across multiple sheets rather than expanding canvas.

### H.2 Audio Requirements

Audio is the second read channel. Where the intent icon delivers *what* is coming, the intent telegraph audio delivers *urgency* and *family* — it answers "how worried should I be" before the player's eyes have finished parsing the glyph. Mix targets in this section are relationships, not absolute session dBFS.

#### H.2.1 Intent Telegraph Audio

Every intent that telegraphs in the player turn emits a telegraph stinger at the enemy's spatial position.

- **Pan:** hard right (R = 0.8-1.0). Enemies sit on the right of the chase rail; audio matches the visual stage.
- **Level target:** -3 dBFS on the stinger bus.
- **Per-`IntentType` sound:**

  | Type          | Motif                                             | Duration    |
  |---------------|---------------------------------------------------|-------------|
  | Damage        | Hydraulic hiss-clunk (pressure build + hard stop).| 0.35s       |
  | Status        | Chemical hiss (glass-on-metal, corrosion).        | 0.5s        |
  | Heal          | Intake valves + fluid rush.                       | 0.45s       |
  | Defend        | Steel clang + lock-in ratchet.                    | 0.6s        |
  | PositionShift | Gear snap + tire friction.                        | 0.55s       |
  | Utility       | Single relay click.                               | 0.2s        |
  | NoOpIntent    | Silence, or 0.1s static crackle (designer pick).  | 0 or 0.1s   |

#### H.2.2 Family Sonic Identity

Beneath the telegraph, each enemy emits a continuous engine/presence layer at -8 dBFS (8 dB under the telegraph). The layer runs from spawn to death.

| Family    | Engine Profile                                         |
|-----------|--------------------------------------------------------|
| Raider    | Square-wave growl, 80-120 Hz fundamental.              |
| Scavenger | 2-stroke rattle, 140-180 Hz, irregular cadence.        |
| Elite     | Turbine hum, 200-250 Hz, steady.                       |
| Boss      | Diesel throb, 40-80 Hz, with 0.3-0.5 Hz LFO pulse.     |

#### H.2.3 SilhouetteClass Weighting

Mass is audible. The family layer is filtered per `SilhouetteClass`:

| Class  | Filter                  | Level    | Sub-bass (< 60 Hz) |
|--------|-------------------------|----------|--------------------|
| Small  | High-pass @ 120 Hz      | -12 dBFS | Rejected           |
| Medium | High-pass @ 80 Hz       | -6 dBFS  | Rejected           |
| Large  | No high-pass            | 0 dBFS   | Allowed            |
| Boss   | No high-pass            | 0 dBFS   | Required           |

Sub-bass below 60 Hz is reserved for Large and Boss — this keeps Small/Medium enemies from muddying the mix.

#### H.2.4 Biome Sonic Vocabulary

Per C.10, biome tint is baked into the archetype's asset, not mixed at runtime. There is **no runtime biome-tint bus** — if a Sand Flats archetype appears, its audio ships with Sand Flats tinting already rendered.

| Biome          | Tint                                                       |
|----------------|------------------------------------------------------------|
| Sand Flats     | Dry aluminum resonance on impacts; minimal reverb.         |
| Junk Mesa      | Chemical drip (off-resonance, dissonant intervals).        |
| Haven Approach | Cold structural hits; -10 dBFS on all hits (muffled feel). |

#### H.2.5 Enrage Audio Activation

When `EnrageActive` transitions false→true:

1. **Activation one-shot:** 0.8-1.2s cinematic hit at -2 dBFS. Ducks the full combat bed by -6 dB for the duration.
2. **Persistent high-RPM layer:** -8 dBFS, runs under the family layer for remainder of combat. Frequency-gated per biome (Sand Flats = bright, Haven Approach = muffled).

The activation one-shot is the single loudest enemy-side audio event in a non-boss combat; it is the mix's way of saying *this changed*.

#### H.2.6 Retarget Stinger

When `OnInvalidTarget` fires:

- Duration 0.2-0.4s, 1-3 kHz band-limited noise stinger with pitch rise.
- Layers **over** the telegraph — does **not** duck the telegraph.
- Mirrors the visual directional sweep (H.1.6) in stereo pan from old-slot side to new-slot side within the right channel field.

#### H.2.7 Death Cascade Audio

Per `SilhouetteClass`, aligned to the visual cascade (H.1.8):

| Class  | Total duration | Structure                                                        |
|--------|----------------|------------------------------------------------------------------|
| Small  | 0.8s           | Snap + dust settle.                                              |
| Medium | 1.2s           | Impact + metal groan + dust.                                     |
| Large  | 1.8s           | Impact + secondary collapse + sustained groan + dust.            |
| Boss   | 2.5-3.0s       | 5-stage cascade: impact → vent hiss → frame collapse → engine death → ambient silence hold. |

**Close-call variant.** If triggered by a player close-call kill (see H.1.8 close-call rule), the death cascade shifts: +2-3 dB high-frequency brightness, and the tail extends by +300ms. This is the audio side of the "you almost died" moment.

#### H.2.8 Slot State Event Audio

One-shots fire on slot state transitions (aligned to the player-side vocabulary):

| Event              | Motif                                    | Band         | Duration | Level   |
|--------------------|------------------------------------------|--------------|----------|---------|
| Online → Damaged   | Flex-groan (stressed metal).             | 200-500 Hz   | 0.4s     | -6 dBFS |
| Damaged → Offline  | Seizing failure (grinding stop).         | 2-4 kHz      | 0.5s     | -4 dBFS |
| Offline → Online   | *No sound.* (MVP: no on-combat repair.)  | —            | —        | —       |

**Simultaneous event rule.** If two slot events fire on the same tick (e.g., two slots go Offline from a single resolution), the second event is queued with a **50 ms offset**. This prevents cancellation from stacked identical sounds.

#### H.2.9 Mix Priority, Voice Budget, Close-Call Mix State

**Priority order (highest first):**

1. Enrage activation one-shot.
2. Intent telegraph stinger (current turn).
3. Resolution impacts (slot hit, armor crack).
4. Slot state event (Damaged/Offline).
5. Retarget stinger.
6. Family engine layer.

When priority tiers compete for voice slots, lower tiers are ducked -6 dB or dropped.

**Enemy-side voice ceiling:** 8 simultaneous voices. If exceeded, drop by priority.

**Close-call mix state.** When player HP ≤ 20% of max, the mix shifts: +2 dB on 2-6 kHz (brightness, "heart-in-throat") and -3 dB on <80 Hz (pulled sub-bass) for the remainder of combat. Restores at `EndOfCombat`.

**FMOD integration (when middleware adopted).** Priority order above maps directly to FMOD event priority integers (1=highest). Voice ceiling maps to the enemy bus voice limit. Middleware is **not mandated** for MVP — the priority/ceiling model is engine-agnostic.

## UI Requirements

> **Scope.** This section specifies the wire-level layout, data bindings, input model, and interaction contracts for the enemy-owned zones of the Combat HUD. Exact animation curves, transition easing, and VFX timing are deferred to the Combat HUD UX Spec (OQ-CC2). Mechanic rules, damage formulas, and intent selection are owned by Sections C–H of this GDD and are not redefined here — this section only specifies how that data surfaces to the player visually and interactively.

### I.1 HUD Zones (Enemy Side)

All placement is expressed in screen-relative terms using a 16:9 reference (1920×1080). Enemy-side zones occupy the right hemisphere of the combat HUD, consistent with the scene framing rule: player vehicle on left, enemy vehicle on right.

| Zone | Placement | Always Visible? | Contents |
|---|---|---|---|
| **Enemy Chassis Zone** | Right 45% of screen width; vertically centered on the chassis sprite, occupying the upper 70% of screen height | Yes | Enemy vehicle sprite canvas (per H.1.1 size class); four slot state overlays (H.1.7); Frame Armor readout (shield glyph + integer, bottom-left of chassis area); death cascade anchor |
| **Intent Zone** | Top-right corner; inset 24px from screen edge; spans approximately top 22% of screen height and right 20% of screen width | Yes — never hidden, never occluded (C📌-1) | Intent glyph (48×48px min, H.1.4); target-slot label below glyph; predicted-value readout (damage int, status stacks, armor gain, or "—" for NoOp) |
| **Name / Family Plate** | Directly above the enemy chassis sprite, horizontally centered on the sprite canvas; occupies a thin strip roughly 5% of screen height | Yes | Archetype name (e.g., "Patch Rider"); family tag (e.g., "Raider · Biome 1") |
| **HP Readout** | Bottom edge of chassis zone, horizontally spanning the chassis sprite width; approximately bottom 8% of screen height | Yes | Numeric HP bar: current HP / max HP as integer pair; bar fill left-to-right |
| **Enrage Indicator** | Inset into the top-left corner of the chassis sprite canvas; overlaps sprite edge | Only when `EnrageActive == true` | 2px red `#B03030` rim (H.1.5); amber "ENRAGE" text label in 12px monospace; Enrage warning state (turns-until-Enrage counter) shown 2 turns before activation — proposed: "ENRAGE IN N" in amber `#E8B23A`, designer review |
| **Slot State Overlay Panel** | Flush-right strip along the left edge of the chassis sprite canvas; four slot icons stacked vertically | Yes | Four slot icons (Weapon / Engine / Mobility / Frame) each with state rendering per H.1.7; Frame Armor sub-readout attached below Frame icon |

**Occlusion priority.** The Intent Zone has the highest z-order among enemy-side elements. Particle effects, status stacks, and the Enrage rim must all render behind the intent glyph and target-slot label. The HUD must never occlude the intent tell with any VFX or overlay.

### I.2 Data Bindings

The HUD is a **read-only view** of game state. No UI element writes back to any state object. All bindings are one-directional: model → view, mediated by the C# event bus defined in `IEnemyEventSource` and `IVehicleView`.

| UI Element | Source Field / Event | Source Type | Notes |
|---|---|---|---|
| Enemy sprite render | `EnemyDefinitionSO.SilhouetteClass`, `VisualFamily` | `EnemyDefinitionSO` | Sprite resolved at spawn; biome tint baked per H.1.3. |
| Intent glyph icon | `NextIntent.Type` → glyph lookup table (H.1.4) | `EnemyIntent` via `IVehicleView.NextIntent` | Lookup is a static mapping; no runtime logic in the view. |
| Intent target-slot label | `NextIntent.TargetSlot` → label string ("WEAPON" / "ENGINE" / "MOBILITY" / "FRAME" / "SELF" / "—") | `EnemyIntent` via `IVehicleView.NextIntent` | NoOpIntent always yields "—" per EC4. |
| Predicted damage readout | `NextIntent.PredictedDamage` | `EnemyIntent` | Displayed as integer. Pre-Armor. Enrage-updated per D.2 HUD contract — refreshes when `OnEnrageActivated` fires. |
| Predicted status stacks | `NextIntent.PredictedStatusStacks`, `NextIntent.StatusRef.DisplayName` | `EnemyIntent` | Only rendered when `IntentType == Status`. |
| Predicted armor gain | `NextIntent.ArmorGain` | `EnemyIntent` | Only rendered when `IntentType == Defend`. |
| Slot state (Weapon / Engine / Mobility / Frame) | `VehicleState.Slots[s].DamageState` | `IVehicleView` via `SlotStateChanged` event | Four independent bindings; updates on each `SlotStateChanged` event emission. |
| Frame Armor readout | `VehicleState.CurrentArmor` | `IVehicleView` via `OnCurrentArmorChanged` | Rendered as "⛊ {int}". Hidden when `CurrentArmor == 0` — proposed: show empty shield at 40% opacity for legibility continuity, designer review. |
| HP bar current fill | `VehicleState.HullHP` (derived from sum of slot HP) | `IVehicleView` | Bar updates on any `SlotStateChanged` that reduces HP. |
| HP numeric label | `VehicleState.HullHP` / `VehicleState.MaxHullHP` | `IVehicleView` | Integer pair; MaxHullHP is set at spawn and does not change. |
| Archetype name | `EnemyDefinitionSO.ArchetypeId` → display name lookup | `EnemyDefinitionSO` | Display name is a separate authored field from the code ID — proposed: `EnemyDefinitionSO.DisplayName: string`, designer review. |
| Family tag | `EnemyDefinitionSO.VisualFamily`, `BiomeIndex` | `EnemyDefinitionSO`, `CombatEndedPayload.BiomeIndex` | Format: "{Family} · Biome {N}". |
| Enrage indicator (active) | `EnrageActive` flag on combat record via `OnEnrageActivated` event | `IEnemyEventSource.OnEnrageActivated` | Rim and tint applied permanently on first event. |
| Enrage warning (pre-activation) | `TurnCount` vs. `EffectiveEnrageTurn` — derived: `TurnsUntilEnrage = EffectiveEnrageTurn - TurnCount` | `CombatContext` (Card Combat R7: `EnrageTelegraphLeadTurns = 2`) | Warn when `TurnsUntilEnrage <= 2` and `EnrageActive == false`. Proposed: display "ENRAGE IN {N}" in amber `#E8B23A`. |
| Intent retarget animation trigger | `IEnemyEventSource.OnIntentRetargeted(old, new)` | Event | Drives the 3-phase H.1.6 transition in the intent zone; old and new intents supplied by event payload. |

**View write-back rule.** No handler wired to any of these events may call a method on `IVehicleMutator`, `IEnemyBrain`, or any game-state object. Event subscribers are pure view-update functions.

### I.3 Information Hierarchy (Read Order)

Pillar 3 ("Read to Win") requires the player to extract the four key combat facts — **What / Where / How much / When** — within two seconds of a new intent being telegraphed.

**Designed eye-path:**

| Read Order | Element | Answers | Design Justification |
|---|---|---|---|
| 1st read | Intent glyph (top-right, 48×48px min) | **What** is coming — Damage / Status / Defend / etc. | Largest icon, highest contrast, fixed position. Peripheral vision registers it before focus arrives. |
| 2nd read | Target-slot label below glyph | **Where** it lands — FRAME / ENGINE / WEAPON / MOBILITY | Immediately beneath the glyph; single short word, monospace, ash-white on dark field. |
| 3rd read | Predicted-value readout (damage int, stacks, armor delta) | **How much** it will hurt | Numeric; positioned directly adjacent to the label. Integer is the terminal fixation point. |
| Ambient / implicit | Slot state overlays on enemy chassis | **When** to press — enemy slot health informs when to prioritize targeting it | Not a sequential read; absorbed over time from the left edge of the chassis zone. Informs medium-term strategy, not this-turn reaction. |

The Enrage warning ("ENRAGE IN N") acts as a **turn counter** — it adds a fourth ambient read that answers **When does the pressure escalate**. It does not interrupt the primary 1st–3rd eye-path because it sits inside the chassis zone, not the intent zone.

**Two-second target.** A player who has never seen a given archetype should complete reads 1–3 in under two seconds on first encounter. This is achievable because reads 1 and 2 are a single fixation (glyph + label are spatially adjacent), and read 3 is an integer adjacent to read 2. Three words and a number. The intent zone is reserved screen space that never moves between archetypes; the player builds a fixed scan habit after one combat.

### I.4 Input and Focus Model

**Keyboard / Mouse:**

| Interaction | Behavior | Rationale |
|---|---|---|
| Mouse hovers over enemy chassis or intent zone | Ember-orange `#E8630A` pulse fires on the player's predicted-target slot (H.1.4 echo pulse, 300ms); intent zone text brightens to full opacity if it was dimmed | Foresight feedback loop: hovering the threat highlights where your vehicle will be hit. No state change — purely visual. |
| Mouse click on enemy chassis or intent zone | No action (MVP). Click does not select, target, or interact. | Enemies are not directly targeted by card play in Wasteland Run (Card Combat R3: target validation is player-slot or enemy-slot per `ValidSubsystemTargets`; the enemy as a whole is never a click target). Clicking an untargetable entity with no feedback trains incorrect expectations. |
| Mouse leaves enemy zone | Echo pulse fades; intent zone returns to default opacity | Clean reset; no lingering state. |

**No hover-only interactions.** Per `technical-preferences.md`, every interaction that surfaces information must have a gamepad equivalent. The hover echo pulse is mirrored by gamepad focus (see below), so it is not hover-only.

**Gamepad:**

| Interaction | Proposed Binding | Behavior |
|---|---|---|
| Focus enemy entity | Right shoulder button (R1 / RB) — **proposed, designer review** | Cycles focus to enemy chassis (single enemy in MVP; multi-enemy spec in I.7). Focused enemy receives identical echo-pulse behavior as hover. |
| Inspect enemy (tooltip) | Hold South face button (cross / A) for 400ms while enemy is focused | Opens inspect tooltip (I.5). Matches common gamepad "hold to inspect" convention (ref: Hades, Slay the Spire console port). |
| Dismiss tooltip | Release South face button or press East face button (circle / B) | Tooltip closes; returns to default view. |

**Rationale for R1/RB cycle binding.** Card Combat already uses face buttons and triggers for card play and end-turn. The shoulder buttons are the natural navigation layer for entity inspection — they do not conflict with combat actions. The player can inspect the enemy without disrupting in-progress card selection. *Proposed — verify against Card Combat input map before implementation.*

**Accessibility hook.** The intent zone must expose a machine-readable text description of the current `NextIntent` via the UI accessibility tree (Unity UI Toolkit accessibility API or UGUI accessibility layer — implementation owned by `unity-ui-specialist`). This GDD specifies only that the hook must exist and the string format:

**Format:** `"{ArchetypeDisplayName}: {IntentVerb} {SlotLabel} for {Value}{Unit}, telegraphed turn {TurnCount}"`

**Examples:**
- `"Patch Rider: strike Frame for 12 damage, telegraphed turn 2"`
- `"Scrap Vulture: apply 2 Burning to Engine, telegraphed turn 4"`
- `"Dune Warden: defend self for 6 armor, telegraphed turn 1"`
- `"Patch Rider: no action, telegraphed turn 5"` (NoOpIntent)

The accessibility description must update whenever `OnIntentTelegraphed` or `OnIntentRetargeted` fires. It is the same description used in I.5 tooltip copy.

### I.5 Tooltip / Inspect Content

**Trigger:** Mouse hover over enemy zone + hold 400ms (K/M); hold South face button while enemy is focused (gamepad). The 400ms delay prevents accidental tooltip pop on fast mouse passes.

**Tooltip is additive.** The primary read from I.1 (glyph + label + value) must be fully sufficient to play the game. The tooltip is an extended reference for players who want more detail, not a required read. A player who never opens a tooltip must not be at an informational disadvantage.

**Tooltip layout (top-to-bottom):**

| Section | Content | Source |
|---|---|---|
| Header | Archetype name in bold (e.g., "Patch Rider"); family tag on a second line (e.g., "Raider · Sand Flats") | `EnemyDefinitionSO.DisplayName`, `VisualFamily`, biome label |
| HP / Armor | "HP: {current} / {max}" on one line; "Armor: ⛊ {currentArmor} / {maxArmor}" on a second line | `IVehicleView.HullHP`, `CurrentArmor`, `MaxArmor` |
| Slot states | Four rows, one per slot: "{SlotIcon} {SlotName}: {State} ({currentHP}/{maxHP})" | `VehicleState.Slots[]` via `IVehicleView` |
| Enrage status | If `EnrageActive == true`: "ENRAGED" in red `#B03030`. If `EnrageActive == false`: "Enrages in {TurnsUntilEnrage} turn(s)" in amber `#E8B23A`; or "Enrages this turn" if `TurnsUntilEnrage <= 1`. | `EnrageActive`, derived `TurnsUntilEnrage` |
| Current intent (full sentence) | The accessibility description string from I.4 verbatim — this ensures the tooltip and the a11y tree always agree. | `NextIntent` via I.4 format |

**Tooltip z-order.** The tooltip renders above all chassis-zone elements and above the intent zone's background panel, but it must not obscure the intent glyph + target-slot label (C📌-1 hard constraint: intent tell is never behind a tooltip). Proposed position: tooltip anchors to the left edge of the enemy chassis zone, expanding leftward, so the intent zone top-right remains fully clear.

**Tooltip dismissal.** Tooltip closes immediately on mouse-out (no delay) or on any card play action. It does not persist through the player's end-turn action — the enemy's intent zone updates at the top of the next cycle and the tooltip would be stale.

### I.6 Wire-Level Placement Notes

All percentages are of the 1920×1080 reference canvas, 16:9.

```
┌──────────────────────────────────────────────────────────────────────────┐
│ COMBAT HUD (full screen)                                                 │
│                                                                          │
│  [PLAYER SIDE — left 55%]     │  [ENEMY SIDE — right 45%]                │
│                               │                                          │
│                               │  ┌──────────────────────┐ ← top-right   │
│                               │  │   INTENT ZONE        │   ~20% wide   │
│                               │  │  [GLYPH 48×48 min]   │   ~22% tall   │
│                               │  │  [TARGET-SLOT LABEL] │               │
│                               │  │  [PREDICTED VALUE]   │               │
│                               │  └──────────────────────┘               │
│                               │                                          │
│                               │  ┌────────────────────────────────────┐ │
│                               │  │  NAME PLATE (~5% tall)             │ │
│                               │  │  "Patch Rider  ·  Raider · Biome 1"│ │
│                               │  └────────────────────────────────────┘ │
│                               │                                          │
│                               │  ┌──┐  ┌────────────────────────────┐   │
│                               │  │  │  │                            │   │
│                               │  │SL│  │   ENEMY CHASSIS SPRITE     │   │
│                               │  │OT│  │   (160–400px canvas)       │   │
│                               │  │  │  │   [ENRAGE RIM overlay]     │   │
│                               │  │ST│  │   [ENRAGE TINT overlay]    │   │
│                               │  │AT│  │                            │   │
│                               │  │ES│  └────────────────────────────┘   │
│                               │  └──┘  ← slot state panel, left edge    │
│                               │          of chassis, ~6% wide           │
│                               │                                          │
│                               │  ┌────────────────────────────────────┐ │
│                               │  │  HP BAR   [████░░░]  24 / 40       │ │
│                               │  │  ARMOR    ⛊ 6 / 8                  │ │
│                               │  └────────────────────────────────────┘ │
│                               │  ← HP readout, bottom of chassis area   │
│                               │    ~8% screen height                    │
└──────────────────────────────────────────────────────────────────────────┘
```

**Slot state panel detail (left edge of chassis):**

```
┌────┐
│ W  │  ← Weapon slot icon + state dot
│ E  │  ← Engine slot icon + state dot
│ M  │  ← Mobility slot icon + state dot
│ F  │  ← Frame slot icon + state dot
│ ⛊6 │  ← Frame Armor sub-readout (shield glyph + int)
└────┘
```

**Enrage warning position.** The pre-activation "ENRAGE IN N" label sits at the top of the name plate strip, right-aligned, in amber `#E8B23A`. When `EnrageActive` flips to true, the text is replaced by "ENRAGED" in red and the 2px rim activates on the sprite.

### I.7 Interaction Patterns

**Hover / Focus Propagation.** In MVP combat (single enemy), focus is binary: the player either has the enemy focused or they do not. There is no multi-entity focus competition.

Focus triggers are:
- Mouse enters any pixel of the enemy chassis zone or intent zone (K/M hover)
- Player presses the cycle-focus binding (gamepad R1/RB — proposed)

Focus is cleared by:
- Mouse exits all enemy zones (K/M)
- Player presses the dismiss binding or navigates back to player-side UI (gamepad)
- Any card play action (card play locks attention to the card's target; clearing enemy focus prevents split-attention visual noise)

While focused: the ember echo pulse fires on the player's predicted-target slot (H.1.4). The echo pulse is not retriggered on every frame — it fires once on focus acquisition and again if `NextIntent.TargetSlot` changes while focus is held.

**Multi-Enemy Degradation (Future-Proofing).** This spec is written for MVP single-enemy combat. If multi-enemy combat is introduced post-MVP, the following degradation rules must be honored:

1. **Stacking layout.** Multiple enemies stack vertically in the enemy chassis zone. Each enemy retains its own intent zone stacked in a column in the top-right, ordered top-to-bottom by enemy initiative / arrival order.
2. **Intent icon minimum preserved.** Each intent zone in a multi-enemy stack retains the 48×48px minimum glyph (H.1.4). The sprite canvas may scale down (but not below the Small class minimum of 160×80px) to fit the stack.
3. **Focus cycle.** Gamepad cycle-focus binding iterates through enemies in the stack. Hover focus goes to whichever enemy zone the mouse enters.
4. **Echo pulse specificity.** The echo pulse fires only on the predicted-target slot of the *focused* enemy, not all enemies simultaneously.

This GDD does not design multi-enemy combat. The degradation rules exist solely to prevent a single-enemy assumption from being baked into the HUD architecture.

**Retarget Transition Perceived Behavior (H.1.6 UX Interpretation).** When `OnIntentRetargeted(old, new)` fires, the intent *zone itself is stable* — it does not move, scale, or flash. Only the **target-slot label** within the zone animates through the 3-phase transition (H.1.6: fade-out old label → crossfade sweep → pulse-in new label).

From the player's perspective: they see the glyph stay constant (the type of threat is unchanged — it is still a Damage intent, for example) while the destination label updates. This communicates "same threat, new target" more precisely than replacing the whole intent icon. The glyph changing *would* imply the type of intent changed, which is only true when `OnIntentRetargeted` supplies a new intent with a different `IntentType` (e.g., a Damage retarget to NoOpIntent after all slots go Offline).

If the retarget results in `NoOpIntent` (EC1, EC4): both glyph and label update — the glyph transitions from the original icon to the grey em-dash, and the label transitions to "—". This is the one case where the full icon changes during a retarget transition.

### I.8 UI Contract Summary

The following are hard contractual MUSTs that the Combat HUD UX Spec must honor. These are not suggestions.

1. **Intent tell is always visible.** The intent glyph and target-slot label (I.1, Intent Zone) must be present on screen, un-occluded, and at full legibility from the moment `OnIntentTelegraphed` fires until the next `OnIntentTelegraphed` or `OnIntentRetargeted` replaces it. No particle effect, tooltip, status overlay, or z-ordered element may render in front of the glyph + label pair. This is the C📌-1 hard constraint.
2. **Intent zone position never changes.** The intent zone is a fixed reserved area (top-right, per I.1). It does not animate position, does not dodge other elements, and does not scale with archetype size class. Players must be able to build a fixed scan habit to top-right every turn.
3. **Echo pulse fires on the player's chassis slot.** When the player hovers or focuses the enemy entity, the amber `#E8630A` 300ms pulse fires on the player-side slot matching `NextIntent.TargetSlot` (H.1.4). The HUD must maintain a mapping of `SlotId → player chassis slot UI element` to execute this. If `TargetSlot == SlotId.None` (NoOpIntent), no pulse fires.
4. **Enrage state must be reflected immediately.** When `OnEnrageActivated` fires, the HUD must synchronously apply the 2px red rim and `#2A1414` tint (H.1.5) to the enemy sprite and update the damage readout to `PredictedDamage + EnrageBonus` (D.2 HUD contract, where `EnrageBonus` is computed per F-CC additive formula) before the player's next input is accepted.
5. **NoOpIntent renders the neutral "—" state.** When `NextIntent.Type == Utility && NextIntent.Target == None` (EC4 sentinel), the intent zone must render the grey em-dash glyph, the "—" target-slot label, no numeric value, and no echo pulse. No null-check failures, no empty boxes, no missing icon. The "—" state is a first-class render path, not a fallback.
6. **Tooltip never obscures the intent tell.** The inspect tooltip (I.5) must anchor away from the top-right intent zone. Proposed anchor: left-expanding from the left edge of the chassis zone. The intent glyph and label must remain fully visible while the tooltip is open.
7. **All interactions are input-method agnostic.** Every visual behavior triggered by K/M hover (echo pulse, tooltip, opacity change) must have an exact gamepad equivalent triggered by focus. There must be no information accessible only via mouse hover that is unavailable on gamepad.

## Acceptance Criteria

### J.1 Core Rules

| ID | Story Type | Given / When / Then | Test Tag |
|----|------------|---------------------|----------|
| **AC-ES1** | Logic | **Given** an enemy is spawned. **When** its runtime state is inspected. **Then** it holds a `VehicleState` POCO with exactly four slots (`Weapon`, `Engine`, `Mobility`, `Frame`), a `MaxArmor` field, and exposes only `IVehicleView` / `IVehicleMutator` — no enemy-exclusive state fields exist on the object. | [AUTO] |
| **AC-ES2** | Logic | **Given** an `EnemyDefinitionSO` asset. **When** it is imported. **Then** it contains non-null `SilhouetteClass` (Small / Medium / Large / Boss), `VisualFamily` (Raider / Scavenger / Elite / Boss), and all four `BaseSlotHP[s]` values. Neither field drives any runtime damage or targeting calculation. | [AUTO] |
| **AC-ES3** | Logic | **Given** an `IEnemyBrain` implementation. **When** `SelectNextIntent(CombatContext)` is called with any valid `CombatContext`. **Then** the return value is non-null, is a fully-populated `EnemyIntent` struct, and the brain mutates no fields outside its return value (pure function — no side effects on any external state). | [AUTO] |
| **AC-ES4** | Logic | **Given** combat has been set up and the bootstrap brain call has completed. **When** the player's turn begins. **Then** `VehicleState.NextIntent` is non-null. If `NextIntent` is null at this point, `InvalidCombatStateException` is thrown with the message `"IEnemyBrain.SelectNextIntent returned null for archetype {ArchetypeId} at TurnCount {TurnCount}"` and combat halts — no silent default substitution occurs. | [AUTO] |
| **AC-ES5** | Logic | **Given** the enemy is in state `Telegraphed` and the player destroys the `TargetSlot` before the enemy's turn. **When** the enemy turn begins. **Then** `IEnemyBrain.OnInvalidTarget(CombatContext)` is called exactly once — never zero times and never twice — and the state machine proceeds to `Resolving` unconditionally after that single call. | [AUTO] |
| **AC-ES6** | Logic | **Given** `TurnCount >= EffectiveEnrageTurn` is true at the start of `SelectingNextIntent`. **When** the Enrage threshold check runs. **Then** `EnrageActive` is set to `true` permanently on the runtime combat record and `OnEnrageActivated(VehicleState)` fires exactly once. `EnrageActive` cannot be cleared by any status effect, card, or subsequent brain call. **Test vector:** `TurnCount = 8`, `EffectiveEnrageTurn = 8` → Enrage activates. `TurnCount = 7` → Enrage does not activate. | [AUTO] |
| **AC-ES7** | Integration | **Given** a full combat turn sequence. **When** the turn order is evaluated. **Then** player card effects resolve first, then enemy intent resolves through `IVehicleMutator`, then end-of-turn status ticks fire — in that exact order with no interleaving. | [AUTO] |
| **AC-ES8** | Logic | **Given** an enemy is in any non-terminal state. **When** `HullHP` reaches `≤ 0` from any damage source (card, DOT tick, or status). **Then** the state machine enters `EndOfCombat` immediately, `OnCombatEnded(CombatEndedPayload)` fires exactly once, and no further brain calls, state transitions, or mutator calls occur on that enemy. | [AUTO] |
| **AC-ES9** | Logic | **Given** an enemy has one or more status effects applied (Burning, Bleeding, or Stalled). **When** those effects tick or are applied. **Then** they route through the identical `ApplyStatusEffectSO` pipeline used for the player vehicle, apply the same `RemainingDuration = 3` cap symmetrically, and fire in the same before-tick order as the player-side pipeline. No enemy-exclusive status code path exists. | [AUTO] |
| **AC-ES10** | Visual | **Given** two archetypes sharing the same `SilhouetteClass`. **When** their sprites are rendered at 20% canvas scale as pure black silhouettes against white. **Then** a playtester who has not seen the archetypes before names the correct `SilhouetteClass` and `VisualFamily` for each within 2 seconds. Additionally, each archetype's sprite bakes exactly one biome tint — no runtime shader re-tint is applied. | [VISUAL] |
| **AC-ES11** | Logic | **Given** a `BrainRuleset` with multiple candidates and context-conditional weight modifiers. **When** `FinalWeight(c) = BaseWeight(c) × Π{ Multiplier(m) | Condition(m) == true }` is computed for every candidate. **Then** candidates with `FinalWeight ≤ 0` are excluded from the draw pool before normalization and do not contribute to the denominator. At least one candidate must remain; if all are filtered, the brain returns `NoOpIntent` and logs the authoring-error warning. | [AUTO] |
| **AC-ES12** | Logic | **Given** two identical `CombatContext` snapshots with the same explicit `System.Random` run seed. **When** `IEnemyBrain.SelectNextIntent` is called on each. **Then** both calls return an identical `EnemyIntent` (same `Type`, `TargetSlot`, all numeric fields). No call to `UnityEngine.Random`, `Time.time`, or `Time.frameCount` occurs anywhere in the brain or weight-modifier code path. | [AUTO] |

### J.2 State Machine & Lifecycle

| ID | Story Type | Given / When / Then | Test Tag |
|----|------------|---------------------|----------|
| **AC-ES13** | Logic | **Given** an enemy is instantiated from `EnemyDefinitionSO` by `IEnemyPool`. **When** the bootstrap sequence runs. **Then** the state machine transitions `Spawned → SelectingFirstIntent → Telegraphed` before control is handed to the player's first turn. `NextIntent` is non-null at `Telegraphed` entry; `TurnCount = 0` at the brain call in `SelectingFirstIntent`. | [AUTO] |
| **AC-ES14** | Logic | **Given** the enemy is in `Telegraphed` and an enemy turn begins. **When** Stalled is active on the enemy with `RemainingCharges ≥ 1`. **Then** the state machine enters `Stalled_Skipping`, consumes exactly one Stalled charge, bypasses `Resolving` entirely (`IVehicleMutator` is NOT called), then transitions to `SelectingNextIntent`. The previously telegraphed intent does not execute. | [AUTO] |
| **AC-ES15** | Logic | **Given** the enemy is in `Telegraphed` and an enemy turn begins. **When** Stalled is NOT active and `NextIntent.TargetSlot` is `Offline`. **Then** the state machine enters `Retargeting`, calls `OnInvalidTarget` exactly once, and transitions to `Resolving` unconditionally after that call — regardless of whether the replacement intent's target is valid (per transition rule 4). | [AUTO] |
| **AC-ES16** | Logic | **Given** the enemy is in `Resolving`. **When** all effect-chain and status-tick processing completes and `HullHP > 0`. **Then** the state machine transitions to `SelectingNextIntent`, `TurnCount` increments, the Enrage threshold check fires, and `SelectNextIntent` is called. The state machine does NOT return to the player until `NextIntent` is non-null. | [AUTO] |
| **AC-ES17** | Logic | **Given** the enemy's `TurnCount` would satisfy `TurnCount >= EffectiveEnrageTurn` at the top of `SelectingNextIntent`. **When** `TurnCount` increments. **Then** `EnrageActive = true` is set before `SelectNextIntent` is called, so the brain's draw uses `EnrageIntentCandidates` (if non-empty) on the same turn Enrage activates. `TurnCount` increments equally on Stalled and non-Stalled turns — Stalled does not defer the Enrage threshold check. | [AUTO] |
| **AC-ES18** | Logic | **Given** the enemy is in `Stalled_Skipping` and a DOT status tick reduces `HullHP` to `≤ 0` during that state. **When** the HP ≤ 0 check fires. **Then** the state machine immediately enters `EndOfCombat` from `Stalled_Skipping`, `OnCombatEnded` fires with the correct `TurnsToKill` (the `TurnCount` at `EndOfCombat` entry), `Resolving` is NOT entered, and `IVehicleMutator` is NOT called. The same behavior applies when HP ≤ 0 is detected during `Retargeting`. | [AUTO] |
| **AC-ES19** | Logic | **Given** all player slots are `Offline` and the enemy's `RetargetPolicy` is exhausted. **When** `OnInvalidTarget` returns `NoOpIntent`. **Then** `CombatContext` exposes `allPlayerSlotsOffline == true` to the subsequent `SelectNextIntent` call, and a `BrainRuleset` with a weight modifier for this condition can suppress damage intents (multiplier 0.0) so the enemy does not repeatedly telegraph damage it cannot resolve. | [AUTO] |
| **AC-ES20** | Logic | **Given** `EndOfCombat` is entered. **When** the state is active. **Then** it is terminal: no further `SelectNextIntent`, `OnInvalidTarget`, or `IVehicleMutator` calls occur. `OnCombatEnded(CombatEndedPayload)` fires exactly once with all eight payload fields populated. | [AUTO] |
| **AC-ES21** | Logic | **Given** a `NoOpIntent` sentinel is returned by any code path. **When** inspected. **Then** all fields exactly match the EC4 contract: `Type = IntentType.Utility`, `Target = TargetType.None`, `TargetSlot = SlotId.None`, `PredictedDamage = 0`, `PredictedStatusStacks = 0`, `StatusRef = null`, `ArmorGain = 0`, `HealAmount = 0`. No other field combination constitutes a valid NoOp sentinel. | [AUTO] |
| **AC-ES22** | Logic | **Given** `OnEnrageActivated` fires and Stalled is simultaneously active. **When** the Stalled turn is evaluated. **Then** `TurnCount` increments normally, Enrage activates (if threshold met), `EnrageBonus` is NOT added this turn (because `Resolving` was bypassed — no damage event occurs), and `EnrageActive` is `true` for all subsequent turns. The next non-Stalled `Resolving` state applies `EnrageBonus` using the then-current `TurnCount`. | [AUTO] |

### J.3 Formulas & Math Invariants

**AC-ES23** — HP Scaling (D.1.1 & D.1.2) [AUTO]

**Test vector — Patch Rider non-elite, Biome 2:**

| Input | Value |
|-------|-------|
| `BaseHullHP` | 40 |
| `BiomeHPScalar(2)` | 1.4 |
| `EliteFactor` | 1.0 |
| **Expected `HullHP`** | **56** (`40 × 1.4 × 1.0`) |
| `BaseSlotHP[Weapon]` | 10 |
| **Expected `SlotHP[Weapon]`** | **14** (`10 × 1.4 × 1.0`) |

**Given** an `EnemyDefinitionSO` with `BaseHullHP = 40` (four slots at 10 HP each), spawned at `biomeIndex = 2` as a non-elite. **When** the HP formula is applied. **Then** `HullHP = 56`, each `SlotHP[s] = 14`, `ΣSlotHP = HullHP`, and all values are integers (ceiling applied where fractional). `MaxArmor` is not scaled — it is the value authored on the SO.

**Elite test vector — same archetype as elite in Biome 2:**
`HullHP = 40 × 1.4 × 1.75 = 98`. `SlotHP[Weapon] = 10 × 1.4 × 1.75 = 24.5 → 25` (ceiling). `ΣSlotHP` must equal `HullHP` (any rounding residual is assigned per-slot according to V&P rounding rules).

---

**AC-ES24** — Damage Scaling + HUD Promise Invariant (D.2) [AUTO]

**Test vector:**

| Input | Value |
|-------|-------|
| `BaseDamage` | 12 |
| `BiomeDamageScalar(1)` | 1.0 |
| `EliteDamageFactor` | 1.0 |
| `EnrageBonus` (pre-Enrage) | 0 |
| **Expected `PredictedDamage`** | **12** |
| **Expected `ResolvedDamage` (pre-Enrage)** | **12** |
| `EffectiveEnrageBaseBonus` | 2 |
| `TurnCount` at first Enrage `Resolving` (EnrageTurn = 8) | 9 |
| `EnrageBonus` at turn 9 = `2 + max(0, 9 - 8) = 3` | 3 |
| **Expected `ResolvedDamage` (post-Enrage, turn 9)** | **15** |

**Given** a Damage intent telegraphed before Enrage activation with `PredictedDamage = 12`. **When** Enrage has activated and the first post-Enrage `Resolving` state runs at turn 9 (`EffectiveEnrageTurn = 8`, `EffectiveEnrageBaseBonus = 2`). **Then** `ResolvedDamage = PredictedDamage + EnrageBonus = 12 + 3 = 15` (Enrage bonus applied at `Resolving` time, not at brain-call time). `PredictedDamage` on the frozen `EnemyIntent` struct remains `12`; the HUD updates the *displayed* value to 15 via the Enrage-update path (D.2 HUD contract). `ResolvedDamage` must never be less than `PredictedDamage` except via Enrage (resolved ≥ telegraphed invariant). Any case where resolved < telegraphed is a bug. On subsequent turns, `EnrageBonus` grows by +1 per turn (turn 10: 4, turn 11: 5, …) — this escalation is system-wide and not overridable per F-CC.

---

**AC-ES25** — Weighted-Draw Filter + Distribution (D.3) [AUTO]

**Test vector — 3-candidate ruleset, one candidate filtered:**

| Candidate | BaseWeight | Condition active? | Multiplier | FinalWeight |
|-----------|-----------|------------------|------------|-------------|
| Ram | 40 | Yes (Frame Damaged) | 2.0 | **80** |
| Swerve | 20 | Yes (Engine Offline) | 0.0 | **0** (filtered) |
| Fortify | 30 | No | — | **30** |

**Given** a `BrainRuleset` with the weights above. **When** `SelectNextIntent` runs with matching `CombatContext`. **Then** the draw pool is `{Ram: 80, Fortify: 30}` (Swerve excluded), `P(Ram) = 80/110 ≈ 0.727`, `P(Fortify) = 30/110 ≈ 0.273`. Over 10,000 seeded draws with fresh distinct seeds, the empirical distribution is `0.727 ± 0.02` for Ram and `0.273 ± 0.02` for Fortify. Swerve is never selected.

---

**AC-ES26** — Enrage Behavior-Shift Candidate List (D.4) [AUTO]

**Given** `EnrageActive = true` and the archetype's `BrainRuleset` has a non-empty `EnrageIntentCandidates` list. **When** `SelectNextIntent` is called. **Then** the draw uses `EnrageIntentCandidates` exclusively — the base candidate list is not consulted. If `EnrageIntentCandidates` is empty or absent, the base list is used with `EnrageBonus` added to all resulting damage intents' `ResolvedDamage` (per F-CC). **Test vector:** base list `{Ram 40, Swerve 20, Fortify 30}`; Enrage list `{Ram 70, DoubleRam 30}`; at `TurnCount = 9`, `EnrageActive = true` → draw from `{Ram 70, DoubleRam 30}` only, `P(Ram) = 0.70`.

---

**AC-ES27** — DifficultyScore Weights Sum to 1.0 and Patch Rider Test Vector (D.5) [AUTO]

**Given** the `CombatConfig` DifficultyScore weight fields. **When** their sum is computed. **Then** `w_HP + w_DPT + w_IC + w_ES = 1.0` (exact float equality within ε = 0.0001). The `CombatConfig` validator enforces this at asset import time.

**Patch Rider test vector:**

| Component | Computed value | norm result |
|-----------|---------------|-------------|
| `BaseHullHP = 40` | `norm(40, 20, 120)` | 0.200 |
| `AvgDPT ≈ 5.33` | `norm(5.33, 4, 25)` | 0.063 |
| `IntentCount = 3` | `norm(3, 1, 8)` | 0.286 |
| `EnrageSeverity = 0.0` | `norm(0, 0, 12)` | 0.000 |
| `DifficultyScore` | `0.30×0.20 + 0.40×0.063 + 0.15×0.286 + 0.15×0.0` | **0.128 ± 0.001** |

**When** `DifficultyScore` is recomputed for the Patch Rider archetype from its authored fields using the D.5 formula. **Then** the result is within ε = 0.001 of 0.128.

---

**AC-ES28** — Retarget Policy Resolution — Three Modes (D.6) [AUTO]

**Given** a `PriorityList(Weapon, Engine, Frame, Mobility)` policy, player `Weapon = Offline`, player `Engine = Damaged`. **When** `OnInvalidTarget` is called. **Then** the first valid slot in the list is selected (`Engine`, state `Damaged`), the replacement `EnemyIntent.TargetSlot = Engine`, and `Resolving` proceeds normally. If all listed slots are `Offline`: return `NoOpIntent`, log `"RetargetPolicy(PriorityList) found no valid slot for archetype {id}"`. `ContextualRule` fallback: if no condition matches, the policy falls back to `DefaultRetargetPolicy(Frame)`; if Frame is also `Offline`, return `NoOpIntent`.

---

**AC-ES29** — Patch Rider Turn-9 Regression Trace (D.7) [AUTO]

This is the gold-standard determinism regression test. **Given** the Patch Rider archetype (`BaseHullHP=40`, slots 10 HP each, `MaxArmor=8`, `BaseDamage(Ram)=12`, `EnrageTurn=8`, `EnrageBaseBonusOverride=null` (uses `DefaultEnrageBaseBonus = 2`), `RetargetPolicy=FixedSlot(Frame)`, `BiomeDamageScalar(1)=1.0`, non-elite) and the exact seeded `CombatRng` values from the D.7 worked example. **When** the combat is replayed from turn 1 through turn 9 with identical seeds and the player state as specified in D.7 (Frame `Offline` from turn 4 onward, Engine repaired turn 9). **Then** the following exact outcomes are reproduced:

| Turn | Selected intent | ResolvedDamage | State outcome |
|------|----------------|----------------|---------------|
| 1 | Ram / Frame / 12 | 12 (Armor soaks 6 → Frame 12 HP) | Telegraphed |
| 5 | Ram / Frame (→ NoOp) | 0 | `OnInvalidTarget` → `NoOpIntent` |
| 8 | Ram / Frame (→ NoOp) | 0 | Enrage activates; `OnEnrageActivated` fires |
| 9 | DoubleRam / Frame (→ NoOp) | 0 | `OnInvalidTarget` → `NoOpIntent` (Frame still Offline) |

Any deviation is a determinism regression failure.

### J.4 Edge Cases

| ID | Story Type | Given / When / Then | Test Tag |
|----|------------|---------------------|----------|
| **AC-ES30** | Logic | **(EC1) Given** all four player slots are `Offline`. **When** `OnInvalidTarget` is called on any policy type. **Then** the policy exhausts all candidates (none are `Online` or `Damaged`), returns `NoOpIntent` with the exact EC4 field contract, and logs `"RetargetPolicy(...) found no valid slot for archetype {id}"`. `OnIntentRetargeted(old, NoOpIntent)` fires. `Resolving` executes with `NoOpIntent` — `IVehicleMutator` is not called. | [AUTO] |
| **AC-ES31** | Logic | **(EC2) Given** a `BrainRuleset` where every candidate has a condition that evaluates true with `Multiplier = 0.0`. **When** `SelectNextIntent` is called. **Then** the brain returns `NoOpIntent` (not `null`) and emits the warning `"BrainRuleset malformed: all candidates have FinalWeight ≤ 0 for archetype {id}"`. `NextIntent` is non-null (Telegraph Contract satisfied). Combat continues. | [AUTO] |
| **AC-ES32** | Logic | **(EC3) Given** Stalled is active with 1 remaining charge AND `TurnCount` at `SelectingNextIntent` will reach `EffectiveEnrageTurn`. **When** one enemy turn elapses. **Then** the exact ordering is: `Stalled_Skipping` entered → charge consumed → `SelectingNextIntent` entered → `TurnCount` increments → `EnrageActive = true` → `OnEnrageActivated` fires → `SelectNextIntent` called with `EnrageActive = true` in context → `IVehicleMutator` was NOT called during this turn. | [AUTO] |
| **AC-ES33** | Logic | **(EC4) Given** the `NoOpIntent` factory constant or construction path. **When** its fields are inspected. **Then** every field matches exactly: `Type = Utility`, `Target = None`, `TargetSlot = SlotId.None`, `PredictedDamage = 0`, `PredictedStatusStacks = 0`, `StatusRef = null`, `ArmorGain = 0`, `HealAmount = 0`. No other field combination is acceptable for this sentinel. | [AUTO] |
| **AC-ES34** | Logic | **(EC5) Given** the enemy has `HullHP = 1` and a DOT status effect with 1 remaining stack. **When** the DOT tick fires during `Stalled_Skipping` or `Retargeting`. **Then** `HullHP` drops to 0, `EndOfCombat` is entered immediately from whichever state was active, `OnCombatEnded` fires with `TurnsToKill = TurnCount` at that moment, and `Resolving` is never entered. The `EnrageReached` field in the payload correctly reflects whether `EnrageActive` was ever set during the combat. | [AUTO] |
| **AC-ES35** | Logic | **(EC6) Given** all four enemy slots are driven to `Offline` via `IVehicleMutator`. **When** `HullHP > 0` remains. **Then** `EndOfCombat` is NOT entered, `SelectNextIntent` is still called on the enemy's next turn, and `IVehicleMutator` is not prevented from receiving the resulting intent. The enemy remains a valid `IVehicleView` target until `HullHP ≤ 0`. | [AUTO] |
| **AC-ES36** | Logic | **(EC7) Given** an `IEnemyBrain` whose `SelectNextIntent` returns `null`. **When** the Combat Loop receives the return value during `SelectingFirstIntent`. **Then** `InvalidCombatStateException` is thrown with message `"IEnemyBrain.SelectNextIntent returned null for archetype {ArchetypeId} at TurnCount {TurnCount}"`. No `OnIntentTelegraphed` fires, no state machine advance occurs, and no silent `NoOpIntent` substitution is made. | [AUTO] |
| **AC-ES37** | Logic | **(EC8) Given** Stalled expired on a prior turn (`RemainingDuration = 0`) and `TurnCount` at `SelectingNextIntent` reaches `EffectiveEnrageTurn`. **When** the enemy turn resolves. **Then** the sequence is: `Resolving` entered normally (not `Stalled_Skipping`), `IVehicleMutator` called, `SelectingNextIntent` entered, `TurnCount` increments, `EnrageActive = true`, `OnEnrageActivated` fires, `SelectNextIntent` called with `EnrageActive = true`. `Stalled_Skipping` is NOT entered. | [AUTO] |
| **AC-ES38** | Logic | **(EC9) Given** an intent candidate with `IntentType = Damage` and a null `DamageEffectSO` reference reaches `Resolving`. **When** `IVehicleMutator` is invoked. **Then** `IVehicleMutator` rejects the call, logs `"IVehicleMutator: null effect reference on intent candidate '{candidateLabel}' for archetype {ArchetypeId}. Intent resolved as NoOp."`, no vehicle mutation is applied, no exception is thrown, and the state machine continues to `SelectingNextIntent` normally. | [AUTO] |
| **AC-ES39** | Logic | **(EC10) Given** `OnInvalidTarget` returns an `EnemyIntent` whose `TargetSlot` is still `Offline` (authoring defect). **When** `Resolving` passes this intent to `IVehicleMutator`. **Then** `IVehicleMutator` rejects the call and logs `"IVehicleMutator: target slot {SlotId} is Offline for archetype {ArchetypeId}. Mutation rejected."`, no mutation is applied, `OnInvalidTarget` is NOT called a second time (one-call-per-turn invariant holds), and the state machine continues to `SelectingNextIntent`. | [AUTO] |

### J.5 Authoring & Validator

| ID | Story Type | Given / When / Then | Test Tag |
|----|------------|---------------------|----------|
| **AC-ES40** | Logic | **Given** a `BrainRuleset` is imported. **When** the SO import validator runs. **Then** it verifies at least one weight modifier condition for `allPlayerSlotsOffline == true` is present on the ruleset (EC1 authoring rule). If absent, the validator logs an authoring warning (not a hard error — the engine degrades gracefully) and flags the asset for review before submission. | [AUTO] |
| **AC-ES41** | Logic | **Given** a `BrainRuleset` where every candidate has a weight modifier condition tied to the enemy's own slot state (e.g., `enemy.Weapon.state == Online`, multiplier 0.0). **When** the SO import validator runs. **Then** it rejects the ruleset if analysis shows no candidate can remain valid when all enemy slots are `Offline` (EC6 authoring rule). At minimum, one unconditional candidate or a `FixedSlot(Frame)` damage candidate must survive all-enemy-slots-Offline context. | [AUTO] |
| **AC-ES42** | Logic | **Given** a `PriorityList` retarget policy on any `EnemyDefinitionSO`. **When** the SO import validator runs. **Then** it verifies `Frame` is the final entry in the list. If `Frame` is absent from the list or is not the terminal entry, the import is rejected with `"PriorityList retarget policy must include Frame as its terminal entry (EC10 authoring rule)."` | [AUTO] |
| **AC-ES43** | Logic | **Given** an `EnemyDefinitionSO` with `BrainRuleset = null`. **When** the SO import validator runs. **Then** the import is rejected with a clear error message. Additionally, a `BrainRuleset` where `BaseCandidates` contains zero candidates with `BaseWeight > 0` must also be rejected (EC7 authoring rule). These are distinct failure modes and must produce distinct error messages. | [AUTO] |
| **AC-ES44** | Logic | **Given** a `BrainRuleset.BaseCandidates` list. **When** the SO import validator runs. **Then** every candidate with `IntentType != Utility` must carry a non-null, valid asset reference for its payload type (`DamageEffectSO` for `Damage`, `StatusEffectSO` for `Status`, etc.). Any candidate with a null effect reference is rejected with `"Non-Utility intent candidate '{label}' has null effect reference (EC9 authoring rule)."` The same check applies to `EnrageIntentCandidates`. | [AUTO] |
| **AC-ES45** | Logic | **Given** a `BiomePoolSO` entry at `biomeIndex > 1` that references an `EnemyDefinitionSO` whose `RetargetPolicy == FixedSlot(Frame)` (the Biome 1 default). **When** the SO import validator runs on the pool asset. **Then** the import is rejected with `"Biome 2/3 archetypes must override RetargetPolicy with PriorityList or ContextualRule (G.4 authoring rule #3). Found FixedSlot(Frame) on archetype {id}."` | [AUTO] |
| **AC-ES46** | Logic | **Given** an `EnemyDefinitionSO` where `ΣBaseSlotHP[s]` across all four slots does not equal `BaseHullHP`. **When** the SO import validator runs. **Then** the import is rejected with `"BaseSlotHP values do not sum to BaseHullHP for archetype {id}. Expected {BaseHullHP}, got {ΣSlotHP}."` | [AUTO] |

### J.6 UI Contract

| ID | Story Type | Given / When / Then | Test Tag |
|----|------------|---------------------|----------|
| **AC-ES47** | UI | **(I.8 MUST #1) Given** `OnIntentTelegraphed` fires with any non-null `EnemyIntent`. **When** the HUD renders. **Then** the intent glyph (≥ 48×48 px) and target-slot label are visible, un-occluded, and at full legibility in the top-right intent zone. No particle effect, tooltip, status overlay, or UI element renders in front of the glyph or label. This must hold for all six `IntentType` values including `Utility` / `NoOpIntent`. | [MANUAL] |
| **AC-ES48** | UI | **(I.8 MUST #2) Given** the game is in any combat state. **When** the HUD is inspected across all archetypes and all `IntentType` values. **Then** the intent zone is always at the same fixed position (top-right, 24px inset per I.1 placement spec). The zone does not animate its position, does not reflow when status overlays appear, and does not scale with the enemy's `SilhouetteClass`. | [MANUAL] |
| **AC-ES49** | UI | **(I.8 MUST #3) Given** the player hovers the mouse over the enemy chassis or intent zone (K/M) or focuses the enemy via gamepad. **When** `NextIntent.TargetSlot` is a valid `SlotId` (not `SlotId.None`). **Then** a 300ms amber `#E8630A` pulse fires on the matching player-side chassis slot. If `NextIntent.TargetSlot == SlotId.None` (NoOpIntent), no pulse fires on any player slot. | [MANUAL] |
| **AC-ES50** | UI | **(I.8 MUST #4) Given** `OnEnrageActivated` fires. **When** the HUD processes the event. **Then** before the player's next input is accepted: the 2px red `#B03030` rim is applied to the enemy sprite, the `#2A1414` multiply tint is applied at 30% opacity, and the displayed damage readout updates to `PredictedDamage + EnrageBonus` (where `EnrageBonus` is computed per F-CC using the current `TurnCount`). All three changes are synchronous with the event — not deferred to the next frame. | [VISUAL] |
| **AC-ES51** | UI | **(I.8 MUST #5) Given** `NextIntent.Type == IntentType.Utility && NextIntent.Target == TargetType.None` (NoOpIntent sentinel per EC4). **When** the intent zone renders. **Then** it displays the grey em-dash glyph (`—`, ash `#6A6660`), the "—" target-slot label, no numeric value readout, and no echo pulse on the player's chassis. No null-reference exceptions, no empty boxes, and no missing icon are produced. The NoOp state is a first-class render path — not an error state or a fallback. | [MANUAL] |
| **AC-ES52** | UI | **(I.8 MUST #6) Given** the inspect tooltip (I.5) is open. **When** the intent zone is inspected. **Then** the intent glyph and target-slot label in the top-right intent zone remain fully visible and un-occluded. The tooltip is anchored to the left of the chassis zone, expanding leftward, so no part of the tooltip overlaps the intent zone. | [MANUAL] |
| **AC-ES53** | UI | **(I.8 MUST #7) Given** any visual behavior triggered by K/M hover (echo pulse, tooltip display, intent zone opacity change). **When** the same interaction is performed via gamepad focus (R1/RB cycle binding). **Then** the identical visual outcome is produced. No information or visual state is accessible only via mouse hover that is unavailable to a gamepad user. | [MANUAL] |

### J.7 Pillar 3 Read-to-Win

**AC-ES54** — Two-Second Read Target [MANUAL]

**Given** a playtester who has never encountered a specific archetype before. **When** they see the intent zone update for the first time after that archetype telegraphs (`OnIntentTelegraphed` fires). **Then** within 2 seconds of the update, the playtester can correctly state all four of: (1) `IntentType` (e.g., "Damage"), (2) target slot (e.g., "Frame"), (3) predicted numeric value (e.g., "12"), and (4) whether Enrage is active or imminent. This test is run on first, second, and third encounter of each archetype in a playtest session (to confirm the habit is buildable, not just lucky on first encounter). A failure rate above 20% across the playtester cohort on any archetype is a legibility defect.

**Acceptance threshold:** ≥ 80% of playtester cohort completes all four reads in ≤ 2 seconds on first encounter of each archetype. Evidence: timed playtester observation log in `production/qa/evidence/pillar3-readtest-[sprint].md`, signed off by QA Lead.

---

**AC-ES55** — Intent Glyph Never Occluded in Any Known In-Game State [VISUAL]

**Given** a screenshot-based regression suite covering: (a) all six `IntentType` values rendered in the intent zone, (b) Enrage rim and tint active, (c) inspect tooltip open, (d) status-effect particle overlays on the enemy chassis active, (e) death cascade animation mid-frame. **When** each screenshot is inspected. **Then** the intent glyph and target-slot label are fully visible — pixel-verified that no other layer covers the 48×48px glyph area or the label beneath it. Evidence: screenshots deposited in `production/qa/evidence/intent-zone-occlusion-[sprint]/`. Sign-off by Art Director and QA Lead.

---

**AC-ES56** — NoOpIntent First-Class Render Path (No Empty Boxes) [VISUAL]

**Given** a combat state where the enemy has returned `NoOpIntent` (any of the EC1, EC2, or D.6-exhausted-policy paths). **When** a screenshot of the intent zone is taken. **Then** the zone renders the grey em-dash glyph and "—" label with no empty icon slots, no missing asset indicators ("?", pink squares), no null-text labels, and no zero-value numeric display where a numeric is not expected. The visual treatment is indistinguishable in quality from any other deliberate `IntentType`. Evidence: screenshot captured from a test scene with injected `NoOpIntent`, deposited in `production/qa/evidence/noopintent-render-[sprint].md`, signed off by QA Lead.

---

**AC-ES57** — Accessibility String Always Current [AUTO]

**Given** the intent zone's accessibility description (I.4 format: `"{ArchetypeDisplayName}: {IntentVerb} {SlotLabel} for {Value}{Unit}, telegraphed turn {TurnCount}"`). **When** `OnIntentTelegraphed` or `OnIntentRetargeted` fires. **Then** the UI accessibility tree string is updated synchronously with the visual update — same frame. For `NoOpIntent`, the string is `"{ArchetypeDisplayName}: no action, telegraphed turn {TurnCount}"`. The string is never stale relative to `IVehicleView.NextIntent`.

## Open Questions

### K.1 Closed This GDD

**OQ-CC1 — Enemy retargeting policy per-archetype**

*Status: **CLOSED** by Section C.6 + D.6.*

**Resolution.** Section C.6 defines `OnInvalidTarget` as firing exactly once per enemy turn when the telegraphed `TargetSlot` is `Offline`. Section D.6 defines three `RetargetPolicy` modes — `FixedSlot(s)`, `PriorityList(s1, s2, …)`, `ContextualRule(fn)` — as a per-archetype field on `EnemyDefinitionSO`. Biome 1 archetypes default to `FixedSlot(Frame)`; Biome 2/3 archetypes MUST use `PriorityList` or `ContextualRule` (G.4 authoring constraint #3, enforced by AC-ES45 validator). EC1 defines NoOpIntent fallback when all slots are exhausted.

### K.2 New Open Questions From This GDD

**OQ-ES1 — Gamepad focus-cycle binding (R1/RB)**

- *Source:* Section I.4 — ux-designer proposed R1/RB for enemy focus-cycle but did not verify against Card Combat's input map.
- *Trigger to resolve:* First implementation sprint that wires up Enhanced Input actions for combat. Check Card Combat input map; if R1/RB conflicts, propose an alternative (D-pad right? R3 stick click?).
- *Owner:* Gameplay programmer + ux-designer at implementation.

**OQ-ES2 — EnemyDefinitionSO.DisplayName authored field**

- *Source:* Section I.2 — ux-designer proposed adding a `DisplayName: string` field separate from the code-side `ArchetypeId` so writer can author localized, flavorful names ("Patch Rider") without changing asset references.
- *Trigger to resolve:* Writer involvement sprint — before any archetype authoring passes through Writer for polish. Currently `ArchetypeId` is doubling as display name, which will not survive localization.
- *Owner:* Narrative director + writer, confirmed by game-designer.

**OQ-ES3 — Empty Frame Armor render treatment**

- *Source:* Section I.2 data binding table — when `CurrentArmor == 0`, does the Armor readout hide entirely or show a faded shield glyph at 40% opacity?
- *Trigger to resolve:* Combat HUD UX Spec authoring OR first playtest with armor-stripping gameplay — whichever is sooner. Hiding saves screen real estate; faded glyph preserves legibility continuity.
- *Owner:* ux-designer + art-director at Combat HUD UX Spec.

**OQ-ES4 — Inspect tooltip anchor behavior**

- *Source:* Section I.5 — ux-designer proposed "anchor left of chassis, expand leftward" to guarantee the intent zone is never obscured (I.8 MUST #6). Exact anchor coordinates and multi-enemy behavior deferred.
- *Trigger to resolve:* Combat HUD UX Spec authoring. Single-enemy MVP can use simple left-anchor; multi-enemy spec (post-MVP per I.7) needs per-focused-enemy anchor logic.
- *Owner:* ux-designer at Combat HUD UX Spec.

### K.3 Telemetry-Gated Questions (from G.5)

These questions cannot be resolved through design; they require live-build telemetry to calibrate. Listed for tracking — do not block GDD approval.

**OQ-ES5 — DifficultyScore weight calibration (post-EA)**

- *Source:* G.5 telemetry metric — DifficultyScore vs. actual run-outcome correlation.
- *Question:* Do the weights `{w_HP=0.30, w_DPT=0.40, w_IC=0.15, w_ES=0.15}` actually predict player-felt difficulty (clear rate, TurnsToKill, close-call frequency)?
- *Trigger:* After 1,000+ logged combats in EA.
- *Owner:* Analytics engineer + systems-designer.

**OQ-ES6 — Retarget trigger rate per archetype (post-EA)**

- *Source:* G.5 telemetry metric — `OnInvalidTarget` trigger rate per archetype.
- *Question:* Is retarget complexity actually exercised, or is `PriorityList`/`ContextualRule` over-engineering for archetypes where it rarely fires?
- *Trigger:* After 500+ logged combats per Biome 2/3 archetype in EA.
- *Owner:* Analytics engineer + game-designer.

**OQ-ES7 — EnrageIntentCandidates utilization (post-EA)**

- *Source:* G.5 telemetry metric — Enrage reach rate + post-Enrage outcome distribution.
- *Question:* How often does combat reach Enrage? Of those, how often does the player survive Enrage activation? This validates whether the 8-turn default `EnrageTurn` is tuned correctly.
- *Trigger:* After 1,000+ logged combats in EA.
- *Owner:* Analytics engineer + systems-designer.

### K.4 Soft Retrofits Surfaced (Deferred Documentation)

Not OQs — these are back-reference updates needed to other GDDs that are not yet written. Listed here so the retrofit queue is discoverable when those GDDs get authored.

| GDD (undesigned) | Retrofit needed |
|------------------|-----------------|
| Loot & Reward | F.2 provisional default: DifficultyScore-driven reward multiplier hook. Confirm at L&R authoring. |
| Node Encounter | F.2 provisional default: `BiomePoolSO` reference for combat-node spawns. Confirm at NE authoring. |
| Combat HUD UX Spec | F.2 provisional default: intent-zone rendering contract (C📌-1, I.1, I.8 MUSTs). Must honor I.8 hard-MUSTs. |
| Biome (undesigned) | F.2 provisional default: biome-tint baking guidelines (H.1.3 + H.2.4). |
