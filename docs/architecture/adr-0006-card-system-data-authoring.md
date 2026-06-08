# ADR-0006: Card System — Data Authoring, Runtime Projection, and Reward Draw Pipeline

## Status

Accepted

## Date

2026-04-25

## Acceptance Date

2026-04-27

## Last Verified

2026-05-18 (Decision 16 amendment per ADR-0007 — nullable `CardData.SourceSlotId` field added; prior verification 2026-04-27)

## Decision Makers

- User (creative/design lead)
- technical-director (TD-ADR review — completed 2026-04-25, verdict CONCERNS, blocker B1 resolved by Option 1: Loot & Reward writes + persists `RarePityCounter` in `LootStateDTO`; non-blocking notes N1 and N2 folded into the doc comment, algorithm step 6, and Risks table)
- unity-specialist (engine-idiom review — completed 2026-04-25, verdict CONCERNS, all 3 concerns addressed in revisions to Decision §3, Migration steps 4-5, Risks IL2CPP row, Validation Criterion 7)

## Summary

The Card System is split across three Unity Assembly Definitions: an engine-free `WastelandRun.Cards` assembly holds the runtime POCO surface (`ICardData`, `ICardEffect`, `ICardEffectCondition`, `CardDraft`, `ICardRewardGenerator`, `TokenResolver`); the existing `WastelandRun.Gameplay` assembly holds the Unity-side authoring types (`CardDefinitionSO`, eight sealed `CardEffectSO` subclasses, three `EffectConditionSO` subclasses, `ChassisMasteryDefinitionSO`, `AddressablesCardCatalog`); the existing `WastelandRun.Combat` assembly references `WastelandRun.Cards` so card resolution stays engine-free. Card pool assets are loaded once via a single `card-definitions` Addressables label, partitioned at load by `ChassisPool`. Rarity weights, pity counter, primary-family bias, and copy-limit fallback compose into a single deterministic `RewardDrawAlgorithm` that consumes a caller-owned `System.Random` per ADR-0003. Description tokens resolve only at draft-generation and combat-display time through a shared `TokenResolver` service, never pre-baked. Persistence follows ADR-0004 with a `CardSystemDTO` sealed record (`SystemId = "card-system"`, `SchemaVersion = 1`) holding three `List<string>` of CardId for deck/discard/exhausted, plus `RarePityCounter` and `CardCopyCounts`.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.13f1) |
| **Domain** | Core / Scripting (with Asset Loading sub-domain) |
| **Knowledge Risk** | LOW — `ScriptableObject` + Assembly Definitions are stable surface; one MEDIUM-risk surface (Addressables exception semantics, identical to ADR-0005) |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md`, `.claude/docs/technical-preferences.md`, ADR-0002, ADR-0003, ADR-0004, ADR-0005, `design/gdd/card-system.md`, `design/gdd/card-combat-system.md`, `design/gdd/loot-reward.md`, `design/gdd/scrap-economy.md` |
| **Post-Cutoff APIs Used** | `Addressables.LoadAssetsAsync<ScriptableObject>` (Unity 6.2+ exception-throw semantics — see Risks); `[CreateAssetMenu]` polymorphic `[SerializeReference]` for effect lists is NOT used (sealed-subclass per-asset model is used instead — see Decision §3) |
| **Verification Required** | (1) IL2CPP standalone build smoke test loads one `CardDefinitionSO` via Addressables and asserts non-null with non-null `Effects` array. (2) Editor-side determinism test seeds `RewardDrawAlgorithm` with `new System.Random(42)`, draws 100k offers, asserts identical SHA-256 of the draft sequence on three consecutive runs. (3) `noEngineReferences` audit: `WastelandRun.Cards.asmdef` compiles with zero `UnityEngine.*` imports (CI grep). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (engine-free combat assembly + POCO state model — Card System mirrors this assembly discipline); ADR-0003 (caller-owned `System.Random`, no `UnityEngine.Random`, CI-enforced forbidden tokens — `RewardDrawAlgorithm` follows the same contract); ADR-0004 (distributed save-schema registry — `CardSystemDTO` declares `SystemId` + `SchemaVersion` constants); ADR-0005 (assembly split pattern, `IPartData`/SO-projection precedent, IL2CPP `link.xml` discipline, Addressables loader pattern — all reused here) |
| **Enables** | ADR-0010 Node Encounter Handler Orchestration (reward nodes consume `CardDraft[]`); ADR-0011 Enemy Data & Brain Contracts (enemy intents may reference card-like effect descriptors via the same `ICardEffect` surface); ADR-0012 Loot & Reward Generation Pipeline (composes `ICardRewardGenerator`); future Card Combat resolution implementation epic (consumes `ICardData` + `ICardEffect`) |
| **Blocks** | Card content authoring epic (75–100 cards × 2 chassis = 150–200 SOs); Combat resolution implementation (cannot resolve `Effects` list until `ICardEffect` surface is locked); Loot & Reward implementation (cannot generate reward offers until `ICardRewardGenerator` is implementable); Save & Persistence wiring of card state (cannot register `CardSystemDTO` until schema is locked) |
| **Ordering Note** | Must be Accepted before any card SO authoring begins (designer iteration would break under retroactive contract changes), before Combat resolution implementation (signature-level dependency), and before Loot & Reward implementation (interface-level dependency). |

## Context

### Problem Statement

Card System is the largest data-driven surface in the game (75–100 cards × 2 chassis at EA, plus rarity/mastery/family/keyword/effect taxonomies). Five downstream systems have hard contracts on it that cannot be implemented without architectural lock-in:

1. **Combat resolution** must read card data (cost, family, position requirement, keywords) and resolve effects in list order without leaking Unity types into the engine-free combat assembly (ADR-0002).
2. **Loot & Reward** must generate `CardDraft[]` deterministically from `(chassis, mastery, rarePity, drawCount, rng)` per `card-system.md` line 495 — but the current GDD only specifies the *output* shape, not the algorithm decomposition (rarity roll → primary-family bias → copy-limit filter → pity override → empty-pool fallback) or the SO-to-POCO projection responsible for producing `IReadOnlyList<ICardData>` from the Addressables-loaded asset set.
3. **Save & Persistence** must serialize deck/discard/exhausted state across sessions; ADR-0004 mandates per-system DTOs but Card System has not registered a `SystemId`/`SchemaVersion` or chosen between SO references and stable string IDs.
4. **Scrap Economy** must enforce `MerchantPrice ≠ 30` parity with `GlobalPurgeCost` and trigger purge transactions consulting the `DeckSize > 10` precondition (EC7) — this requires a stable card-API surface for "is this card still in the deck" queries.
5. **UI surfaces** (combat hand, reward screen, Merchant, Chopshop, deck inspection) all render description text with `{token}` substitution — token resolution must be specified once and shared, not duplicated per surface.

The GDD locks the *what* (data fields, validation rules, formulas) but not the *where* (which assembly), the *when* (load timing, token resolution timing), or the *how* (concrete effect type modeling). Without an ADR, implementation decisions get made under deadline pressure and downstream systems write against contradictory assumptions.

### Current State

- `WastelandRun.Combat` exists with `noEngineReferences: true` (ADR-0002) holding `CardDefinition` as a POCO with primitive fields only (CardId, BaseDamage, EnergyCost — no effect list, no token templating, no family/rarity).
- `WastelandRun.Vehicle` exists with `noEngineReferences: true` (ADR-0005) providing `SlotType`, `ChassisType`, `IPartData`, `IVehicleView`, `IVehicleMutator`, `IPartCatalog` — Card System depends on the first three.
- `WastelandRun.Gameplay` exists with Unity references holding `PartDefinitionSO`, `ChassisDefinitionSO`, `AddressablesPartCatalog` — Card System will add nine new SO subclasses and a card-catalog loader to this assembly.
- `WastelandRun.CombatView` exists with Unity references holding `CombatController` (IMGUI today) — card hand UI lives here later; this ADR does not specify view-layer details.
- No Card SOs exist yet. No assembly-defined `WastelandRun.Cards` exists. No `link.xml` card preservation entries exist.
- `tr-registry.yaml` has 25 active `TR-card-NNN` requirements (TR-001 through TR-025) covering Data, Performance, and Communication domains.

### Constraints

- **Tech-prefs forbidden patterns** (binding): no `UnityEvent` in combat, no combat state on MonoBehaviours, no `UnityEngine.Random` for seeded systems, no hardcoded gameplay values.
- **ADR-0002 boundary**: combat resolution code must remain in `noEngineReferences` assemblies. Card resolution is part of combat resolution. Ergo: card runtime types must be POCOs in an engine-free assembly.
- **ADR-0003 RNG discipline**: `RewardDrawAlgorithm` and any internal pool selection must accept `System.Random` as a parameter and never construct one. CI-enforced forbidden tokens (`UnityEngine.Random`, `DateTime.Now`, etc.) apply.
- **ADR-0004 save schema**: `CardSystemDTO` declares `public const string SystemId` and `public const int SchemaVersion` as compile-time constants; uniqueness is grep-enforced in CI.
- **ADR-0005 IL2CPP discipline**: `CardDefinitionSO` and `CardEffectSO` subclasses are referenced at runtime only through interfaces (`ICardData`, `ICardEffect`); IL2CPP managed-stripping=High will strip them without `link.xml` preservation.
- **Unity 6.2+ Addressables**: `LoadAssetsAsync<T>` throws on label-miss; loader must wrap in try/catch + `handle.IsValid()` finally guard per the registered `vehicle_part_asset_loading` API decision (ADR-0001/0005).
- **GDD invariants** (per card-system.md): rarity weights sum to 100; `BaseDamage ≥ 1`; `MerchantPrice ≠ 30`; copy limits 3 Common/Uncommon, 1 Rare; Innate cap 3; `Ethereal` + `Retain` mutually exclusive; subsystem-strike (`BypassPlating = true`) cannot target Frame.
- **Performance target**: 60 FPS / 16.6 ms frame budget. Reward draws are infrequent (post-combat only, ~14×/run) so algorithmic cost is irrelevant; combat hand tooltip rendering must not allocate per frame.

### Requirements

- Card runtime types must be testable in EditMode without a scene or play-mode bootstrap (matches ADR-0002 testing posture).
- Same `(chassis, mastery, rarePity, runSeed, drawIndex)` tuple must always produce the same `CardDraft[]` (ADR-0003 determinism, AC verified by simulated-draws acceptance criteria in card-system.md lines 641-645).
- View layer must never write to deck/discard/exhausted state through a back door — read-only view interface required, mirroring ADR-0001's `IVehicleView` separation.
- Adding a new effect type post-EA must require zero changes to existing card SO assets (matches GDD EC3 keyword extensibility constraint, applied to effects).
- Token templating must produce the same resolved string in all five display contexts (combat hand tooltip, reward screen, Merchant, Chopshop, deck inspection) per AC `Description tokens (...) resolve to correct numeric values in the following display contexts: ...`.
- `CardSystemDTO` must roundtrip through Newtonsoft.Json without loss; deserialization must reject unknown CardIds with a typed exception (the run is corrupt if a CardId references a deleted asset).

## Decision

The Card System is implemented as three concentric layers across three assemblies, with a fourth layer (presentation) deferred to a separate UI ADR.

### Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│  WastelandRun.CombatView.asmdef  (+ UnityEngine)                       │
│    Combat hand tooltip, reward screen, Merchant, Chopshop, deck view   │
│    All display surfaces consume ICardData + TokenResolver              │
└────────────────────┬───────────────────────────────────────────────────┘
                     │ one-way reference
                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Gameplay.asmdef  (+ UnityEngine)                         │
│    [CreateAssetMenu] CardDefinitionSO        ──projects via──┐         │
│    [CreateAssetMenu] DamageEffectSO  ──┐                     │         │
│    [CreateAssetMenu] RestorePlatingEffectSO                  │         │
│    [CreateAssetMenu] RestoreArmorEffectSO   ──ToRuntime()──▶ │         │
│    [CreateAssetMenu] ApplyStatusEffectSO    each subclass    │         │
│    [CreateAssetMenu] RepairSubsystemEffectSO returns its     │         │
│    [CreateAssetMenu] DrawCardsEffectSO       record type     │         │
│    [CreateAssetMenu] GainEnergyEffectSO  ──┘                 │         │
│    [CreateAssetMenu] ShiftPositionEffectSO                   │         │
│    [CreateAssetMenu] PositionConditionSO  ──┐                │         │
│    [CreateAssetMenu] SlotStateConditionSO    ToRuntime()──▶  │         │
│    [CreateAssetMenu] StatusConditionSO    ──┘                │         │
│    [CreateAssetMenu] ChassisMasteryDefinitionSO              │         │
│                                                              │         │
│    AddressablesCardCatalog : ICardCatalog                    │         │
│      LoadAssetsAsync<CardDefinitionSO>("card-definitions")   │         │
│      try / catch / handle.IsValid() finally guard            │         │
│      Partitions by ChassisPool at load time                  │         │
│                                                              │         │
│    link.xml: preserve="all" for                              │         │
│      WastelandRun.Gameplay.Cards.CardDefinitionSO            │         │
│      WastelandRun.Gameplay.Cards.{8 effect SO subclasses}    │         │
│      WastelandRun.Gameplay.Cards.{3 condition SO subclasses} │         │
│      WastelandRun.Gameplay.Cards.ChassisMasteryDefinitionSO  │         │
│      WastelandRun.Gameplay.Cards.EffectConditionProjection   │         │
│        (static helper called by *EffectSO.ToRuntime())       │         │
└────────────────────┬─────────────────────────────────────────┼─────────┘
                     │ one-way reference                       │
                     ▼                                         ▼
┌────────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Cards.asmdef   ("noEngineReferences": true)              │
│                                                                        │
│    POCO records:                                                       │
│      ICardData          (interface — concrete: CardData record)        │
│      ICardEffect        (interface — 8 sealed record subclasses)       │
│      ICardEffectCondition (interface — 3 sealed record subclasses)     │
│      CardDraft          (sealed record; pre-resolved for UI)           │
│                                                                        │
│    Services (pure C#):                                                 │
│      ICardCatalog        — runtime lookup by CardId / ChassisPool      │
│      ICardRewardGenerator — Generate(...) → CardDraft[]                │
│      RewardDrawAlgorithm  — composes rarity/pity/family/copy-limit     │
│      TokenResolver        — resolves {token} against ICardData         │
│      CardSystemDTO        — save/load record (ADR-0004)                │
│                                                                        │
│    Enums:                                                              │
│      CardFamily          (Precision, Assault, Control, Repair,         │
│                           Maneuver)                                    │
│      CardRarity          (Common, Uncommon, Rare, Legendary[reserved]) │
│      CardKeyword         ([Flags] Exhaust, Retain, Innate, Ethereal)   │
│      CardTargetType      (Self, EnemySubsystem, AllEnemySubsystems,    │
│                           NoTarget)                                    │
│      PositionRequirement (None, RequiresAhead, RequiresBehind,         │
│                           BonusIfAhead, BonusIfBehind)                 │
└────────────────────┬───────────────────────────────────────────────────┘
                     │ referenced by
                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Combat.asmdef   ("noEngineReferences": true)             │
│    CombatLoop / Vehicle / Slot / Deck / Hand / Discard                 │
│    Resolves card plays by walking ICardData.Effects                    │
│    and dispatching each ICardEffect via a switch / visitor             │
│    Mutates state via IVehicleMutator (ADR-0005)                        │
└────────────────────┬───────────────────────────────────────────────────┘
                     │ EditMode tests
                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│  Tests/EditMode/Cards/   (NUnit)                                       │
│  Tests/EditMode/Combat/  (existing — exercises card resolution)        │
└────────────────────────────────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
// === WastelandRun.Cards assembly (noEngineReferences: true) ============

namespace WastelandRun.Cards
{
    using System;
    using System.Collections.Generic;
    using WastelandRun.Vehicle;   // SlotType, ChassisType, StatusType (ADR-0005 / pending ADR-0007)

    // ---------- enums ---------------------------------------------------

    public enum CardFamily          { Precision, Assault, Control, Repair, Maneuver }
    public enum CardRarity          { Common, Uncommon, Rare, Legendary }   // Legendary reserved post-EA (OQ3)
    [Flags] public enum CardKeyword { None = 0, Exhaust = 1, Retain = 2, Innate = 4, Ethereal = 8 }
    public enum CardTargetType      { Self, EnemySubsystem, AllEnemySubsystems, NoTarget }
    public enum PositionRequirement { None, RequiresAhead, RequiresBehind, BonusIfAhead, BonusIfBehind }
    public enum CardLocation        { Deck, Hand, Discard, Exhausted }

    // ---------- runtime POCO surface ------------------------------------

    public interface ICardData
    {
        string                    CardId           { get; }   // [chassis]_[family]_[seq]
        string                    DisplayName      { get; }
        string                    DescriptionTemplate { get; }   // raw with {tokens}
        string                    FlavorText       { get; }
        string                    CardArtKey       { get; }      // Addressables key
        ChassisType               ChassisPool      { get; }
        CardFamily                Family           { get; }
        CardRarity                Rarity           { get; }
        bool                      IsStarterCard    { get; }
        int                       EnergyCost       { get; }
        int                       MerchantPrice    { get; }      // 0 = unlisted at Merchant
        CardTargetType            TargetType       { get; }
        IReadOnlyList<SlotType>   ValidSubsystemTargets { get; } // empty = all slots
        PositionRequirement       PositionRequirement { get; }
        CardKeyword               Keywords         { get; }
        IReadOnlyList<ICardEffect> Effects         { get; }
        int                       BaseDamage       { get; }      // 0 if no DamageEffect; else == Effects[0..].Amount per AC line 618
        string?                   SourceSlotId     { get; }      // ADR-0007 Decision 16 — non-null for part-derived granted cards (set at install time); null for starter-deck cards and external-source grants (e.g. Dredge Javelin tether cohort). Authoring SO leaves this null; runtime projection stamps it when a part install grants a card.
    }

    public interface ICardEffect { }   // marker — concrete records below

    public sealed record DamageEffect(
        int Amount,
        int PositionBonus,
        bool BypassPlating,
        IReadOnlyList<ICardEffectCondition> Conditions
    ) : ICardEffect;

    public sealed record RestorePlatingEffect(int Stacks, SlotType TargetSlot) : ICardEffect;
    public sealed record RestoreArmorEffect(int Amount) : ICardEffect;          // Self-target only; capped at MaxArmor
    public sealed record ApplyStatusEffect(StatusType Status, int Stacks, int Duration, SlotType? TargetSlot) : ICardEffect;
    public sealed record RepairSubsystemEffect(int HpRestored, bool CanReviveOffline) : ICardEffect;
    public sealed record DrawCardsEffect(int Count, CardFamily? FamilyFilter) : ICardEffect;
    public sealed record GainEnergyEffect(int Amount) : ICardEffect;
    public sealed record ShiftPositionEffect(int Direction) : ICardEffect;     // +1 ahead, -1 behind

    public interface ICardEffectCondition { }

    public sealed record PositionCondition(PositionRequirement Required) : ICardEffectCondition;
    public sealed record SlotStateCondition(SlotType Slot, DamageState RequiredState) : ICardEffectCondition;
    public sealed record StatusCondition(StatusType Status, bool Present) : ICardEffectCondition;

    // ---------- reward draw pipeline ------------------------------------

    public sealed record CardDraft
    {
        public string     CardId         { get; init; }
        public string     DisplayName    { get; init; }
        public string     RulesText      { get; init; }   // tokens already resolved
        public CardFamily Family         { get; init; }
        public CardRarity Rarity         { get; init; }
        public int        EnergyCost     { get; init; }
        public string     CardArtKey     { get; init; }
        public IReadOnlyList<string> KeywordBadges { get; init; }
        public int?       MerchantPrice  { get; init; }   // null at reward, populated at Merchant
        public int        SelectionHash  { get; init; }   // deterministic — for telemetry
    }

    public interface ICardCatalog
    {
        ICardData GetById(string cardId);
        IReadOnlyList<ICardData> GetByChassis(ChassisType chassis);
        IReadOnlyList<ICardData> GetByChassisAndRarity(ChassisType chassis, CardRarity rarity);
    }

    public interface ICardRewardGenerator
    {
        // Deterministic. Caller owns rng (ADR-0003). Card System MUST NOT mutate
        // rarePityCounter — Loot & Reward owns the counter (writes + persists
        // in LootStateDTO per ADR-0004's "writer owns DTO field" principle).
        // Return contract is exactly one of two shapes:
        //   - Length == drawCount: a full offer, all drafts populated
        //   - Length == 0       : pity-empty case (rarePityCounter ≥ 8 AND
        //                         the chassis-filtered Rare pool is empty
        //                         AND no fallback Rare exists). Loot & Reward
        //                         EC-LR6 awards Scrap compensation.
        // Length is NEVER between 1 and drawCount-1 — partial exhaustion is
        // absorbed internally by the tier-degradation fallback (algorithm step 4).
        CardDraft[] Generate(
            ChassisType   chassis,
            int           mastery,
            int           rarePityCounter,
            int           drawCount,
            IReadOnlyList<string> currentDeckCardIds,   // for copy-limit filter
            System.Random rng);
    }

    public sealed class RewardDrawAlgorithm : ICardRewardGenerator
    {
        public RewardDrawAlgorithm(ICardCatalog catalog, IMasteryWeights weights, TokenResolver tokens) { /* ... */ }

        public CardDraft[] Generate(/* ... */) { /* see Decision §4 algorithm */ }
    }

    public interface IMasteryWeights
    {
        // Loaded from ChassisMasteryDefinitionSO; flat data, no engine refs.
        (int Common, int Uncommon, int Rare) GetWeights(ChassisType chassis, int mastery);
        IReadOnlyList<CardFamily> GetPrimaryFamilies(ChassisType chassis);   // used for slot-1 bias when mastery 1-3
    }

    public sealed class TokenResolver
    {
        // Stateless. Resolves {damage}, {damage.N}, {bonus}, {heal}, {plating},
        // {armor}, {draws}, {energy}, {stacks}, {duration}, {cost}.
        // Per card-system.md §"Description Token Binding".
        public string Resolve(ICardData card);
    }

    // ---------- save DTO (ADR-0004) -------------------------------------

    [System.Serializable]
    public sealed record CardSystemDTO
    {
        public const string SystemId      = "card-system";
        public const int    SchemaVersion = 1;

        public List<string>            Deck            { get; init; }   // CardId
        public List<string>            Discard         { get; init; }
        public List<string>            Exhausted       { get; init; }
        public Dictionary<string, int> CardCopyCounts  { get; init; }   // CardId → count in deck (derived but persisted for offer-filter speed)

        // NOTE: RarePityCounter is NOT persisted here. Loot & Reward writes the
        // counter (per card-system.md L494 + ADR-0006 Decision §"Pity counter
        // authority") and therefore owns its persistence in LootStateDTO
        // (defined in the future Loot & Reward Generation Pipeline ADR — see
        // "Enables" in the dependencies table). This ADR locks Card System as
        // a read-only consumer: the counter is passed into RewardDrawAlgorithm.
        // Generate as a parameter, not loaded from CardSystemDTO. ADR-0004's
        // "writer owns the DTO field" principle holds.
    }
}

// === WastelandRun.Gameplay assembly (+ UnityEngine) ====================

namespace WastelandRun.Gameplay.Cards
{
    using UnityEngine;
    using WastelandRun.Cards;
    using WastelandRun.Vehicle;

    // ---------- abstract authoring base ---------------------------------

    public abstract class CardEffectSO : ScriptableObject
    {
        public abstract ICardEffect ToRuntime();
    }

    public abstract class EffectConditionSO : ScriptableObject
    {
        public abstract ICardEffectCondition ToRuntime();
    }

    // ---------- 8 sealed effect SOs (one .cs file each) -----------------

    [CreateAssetMenu(menuName = "Wasteland/Card/Effects/Damage")]
    public sealed class DamageEffectSO : CardEffectSO
    {
        [SerializeField] private int amount;
        [SerializeField] private int positionBonus;
        [SerializeField] private bool bypassPlating;
        [SerializeField] private EffectConditionSO[] conditions;

        public override ICardEffect ToRuntime() =>
            new DamageEffect(amount, positionBonus, bypassPlating,
                             EffectConditionProjection.Project(conditions));

        private void OnValidate() { /* GDD AC §"Data Contract" SO import rules */ }
    }

    // (RestorePlatingEffectSO, RestoreArmorEffectSO, ApplyStatusEffectSO,
    //  RepairSubsystemEffectSO, DrawCardsEffectSO, GainEnergyEffectSO,
    //  ShiftPositionEffectSO follow the same shape — see Migration Plan)

    // ---------- 3 sealed condition SOs ---------------------------------

    [CreateAssetMenu(menuName = "Wasteland/Card/Conditions/Position")]
    public sealed class PositionConditionSO : EffectConditionSO
    {
        [SerializeField] private PositionRequirement required;
        public override ICardEffectCondition ToRuntime() => new PositionCondition(required);
    }

    // (SlotStateConditionSO, StatusConditionSO follow same shape)

    // ---------- top-level card SO --------------------------------------

    [CreateAssetMenu(menuName = "Wasteland/Card/Definition")]
    public sealed class CardDefinitionSO : ScriptableObject, ICardData
    {
        // Fields are [SerializeField] private; ICardData properties forward.
        // OnValidate() runs every AC import rule from card-system.md §"Data Contract".

        public IReadOnlyList<ICardEffect> Effects =>
            _effectsCache ??= ProjectEffects();

        private IReadOnlyList<ICardEffect> _effectsCache;
        private IReadOnlyList<ICardEffect> ProjectEffects()
        {
            var arr = new ICardEffect[_effectSOs.Length];
            for (int i = 0; i < _effectSOs.Length; i++) arr[i] = _effectSOs[i].ToRuntime();
            return arr;
        }

        private void OnValidate()
        {
            _effectsCache = null;   // invalidate cache before any read so editor edits to
                                    // referenced EffectConditionSOs round-trip through ToRuntime
                                    // on the next access (specialist concern #1).
            // ... AC import rule checks ...
        }
    }

    // ---------- catalog loader ------------------------------------------

    public sealed class AddressablesCardCatalog : ICardCatalog
    {
        // Loads "card-definitions" Addressables label once at run start.
        // try / catch / handle.IsValid() finally — same as AddressablesPartCatalog (ADR-0005).
        // Partitions by ChassisPool field at load time into per-chassis lists.
        // link.xml entries preserve all SO subclass types.
        // Sole responsibility is CardDefinitionSO lookup — does NOT load mastery weights.
    }

    // Sibling loader for chassis mastery weights. Mirrors AddressablesCardCatalog
    // shape so RewardDrawAlgorithm can be constructed with two independently-
    // loaded read-only catalogs (resolves specialist concern #3 — keeps
    // AddressablesCardCatalog's responsibility narrow per ADR-0003 DI discipline).
    public sealed class AddressablesMasteryCatalog : IMasteryWeights
    {
        // Loads "chassis-mastery-definitions" Addressables label once at run start.
        // Same try / catch / handle.IsValid() finally pattern.
        // Returns one ChassisMasteryDefinitionSO per ChassisType; SO implements IMasteryWeights.
        // EditorBuildPreprocessor gates that the chassis-mastery-definitions group is
        // flagged Include-In-Build (extends ADR-0005 build preprocessor).
    }

    // Bootstrap composition (lives in run-start orchestration, outside this ADR's scope):
    //   var cardCatalog    = await AddressablesCardCatalog.LoadAsync();
    //   var masteryCatalog = await AddressablesMasteryCatalog.LoadAsync();
    //   var tokens         = new TokenResolver();
    //   var rewardGen      = new RewardDrawAlgorithm(cardCatalog, masteryCatalog, tokens);

    // ---------- mastery weights authoring -------------------------------

    [CreateAssetMenu(menuName = "Wasteland/Card/ChassisMastery")]
    public sealed class ChassisMasteryDefinitionSO : ScriptableObject, IMasteryWeights
    {
        [SerializeField] private ChassisType chassis;
        [SerializeField] private CardFamily[] primaryFamilies;   // for Mastery 1-3 slot-1 bias
        [SerializeField] private MasteryTier[] tiers;            // weights per mastery range

        // OnValidate enforces: weights sum to 100; weights >= 0; tier ranges contiguous.
    }
}
```

### Amendment — 2026-05-18 (per ADR-0007 Decision 16)

`ICardData.SourceSlotId` is a nullable string that names the slot whose installed part produced this card, when one exists. It is set by the runtime when a part install fans out granted cards into a vehicle's deck (or hand / discard, per the granting effect's contract), and it stays attached to the `ICardData` projection for the life of that card instance. It is read by `IVehicleMutator.HardRemoveCards(string? sourceSlotId, IReadOnlyList<string> cardIds)` (ADR-0007 Decision 16) to atomically sweep the three card zones (deck + hand + discard) when a hard-removal trigger fires — currently (a) the source slot is scrapped per V&P GDD R6 and (b) an external source like Dredge Javelin terminates its grant cohort. For trigger (b) the `sourceSlotId` argument is `null` and the mutator removes by `cardIds` alone; the field on the `ICardData` projection remains whatever was stamped at install time (typically null for external-source grants, since they were never bound to a slot in the first place). Authoring SOs (`CardDefinitionSO`) do NOT serialize `SourceSlotId` — the field is runtime-stamped during the `CardDefinitionSO.ToRuntime()` projection path or wherever a granted-card factory produces an `ICardData`. Starter-deck cards and reward-granted cards both leave `SourceSlotId` null; only part-derived grants populate it.

This amendment does NOT alter `CardSystemDTO`: persisted card identity remains `CardId : string`. `SourceSlotId` lives only on the in-memory `ICardData` projection and is reconstructed on load by replaying part-install grants from `VehicleStateDTO` (per ADR-0007 / V&P GDD reload contract). The DTO surface is unchanged and the `SchemaVersion` does NOT increment.

### Reward Draw Algorithm (deterministic)

The `RewardDrawAlgorithm.Generate` algorithm composes the GDD's draw rules into one deterministic pipeline. All randomness flows from the caller-owned `System.Random` per ADR-0003. Pseudocode:

```
Generate(chassis, mastery, rarePityCounter, drawCount, currentDeckCardIds, rng):
    drafts = []
    seenInOffer = HashSet<CardId>()                               // without-replacement constraint

    for slotIndex in [0 .. drawCount):
        // 1. Determine target rarity for this slot
        if rarePityCounter >= 8 AND no Rare drafted yet in this offer:
            rarity = Rare (pity precedence — overrides F2 fallback per AC line 644)
        else if slotIndex == 0 AND mastery in [1..3]:
            rarity = pickWeighted(weights[chassis][mastery], rng)
            // primary-family bias applies to pool selection below, not rarity
        else:
            rarity = pickWeighted(weights[chassis][mastery], rng)

        // 2. Build candidate pool (filter by chassis, rarity, copy-limit, seenInOffer)
        pool = catalog.GetByChassisAndRarity(chassis, rarity)
                      .Where(c => copyLimitOK(c, currentDeckCardIds))
                      .Where(c => !seenInOffer.Contains(c.CardId))

        // 3. Apply primary-family bias (slot 1, mastery 1-3 only)
        if slotIndex == 0 AND mastery in [1..3]:
            biasedPool = pool.Where(c => primaryFamilies.Contains(c.Family))
            if biasedPool.NonEmpty: pool = biasedPool   // else fall through to full pool

        // 4. Tier-degradation fallback (F2 — non-pity case)
        if pool.Empty AND rarity != Rare-via-pity:
            for fallbackRarity in [Uncommon, Common] starting from rarity-1:
                pool = catalog.GetByChassisAndRarity(chassis, fallbackRarity)...
                if pool.NonEmpty: break

        // 5. Pity-empty special case (AC line 644)
        if pool.Empty AND rarity == Rare-via-pity:
            return []   // signals Loot & Reward to award Scrap compensation

        // 6. Final empty case (chassis pool exhausted across all three rarities —
        //    practically impossible at 75-100 cards, but contract must be defined).
        //    Per Generate doc comment: result is exactly drawCount OR 0, never
        //    partial. Treat full exhaustion same as pity-empty: short-circuit
        //    the entire offer.
        if pool.Empty:
            return []   // L&R EC-LR6 awards Scrap compensation

        // 7. Pick from pool
        chosen = pickUniform(pool, rng)
        seenInOffer.Add(chosen.CardId)

        // 8. Project to CardDraft (resolve tokens, build keyword badges,
        //    stamp deterministic SelectionHash from CardId + chassis + mastery + slotIndex)
        drafts.Add(toDraft(chosen, tokenResolver, slotIndex))

    return drafts.ToArray()
```

**Determinism contract** (verified by AC card-system.md lines 641-645): given identical inputs, this algorithm produces identical output. The `seenInOffer` check makes draws within a single offer non-independent; the algorithm preserves the GDD's "without replacement" semantic without breaking determinism.

**Pity counter authority**: `RewardDrawAlgorithm` reads `rarePityCounter` but never writes it. Loot & Reward (per its GDD) increments/resets the counter based on whether the drafts contain a Rare. This authority split is registered in `interfaces.card_reward_generation` (Phase 6).

## Alternatives Considered

### Alternative 1: Pure SO with engine reference in combat assembly

- **Description**: Drop `WastelandRun.Cards`. Combat reads `CardDefinitionSO` directly. `WastelandRun.Combat` adds a `UnityEngine` reference.
- **Pros**: Smaller asset count (no projection layer). One source of truth. Faster initial implementation.
- **Cons**: Breaks ADR-0002's engine-free combat assembly boundary. Combat tests would require play-mode bootstrap or Unity test fixture infrastructure (currently 136 tests run headless). All combat code becomes harder to reason about because engine and game logic mix. Future port to a non-Unity runtime (replay capture, headless balance simulator) is foreclosed.
- **Rejection Reason**: Violates ADR-0002 explicitly. Sacrifices the most valuable architectural property of the combat layer (testability, determinism, replay-friendliness) for a small asset-count saving.

### Alternative 2: Discriminated-union effect descriptor (no SO subclass hierarchy)

- **Description**: Single `CardEffectDescriptor` POCO with `EffectType` enum and `Dictionary<string, object>` (or shaped union) parameters. No `CardEffectSO` subclass per type — instead, one `CardEffectSO` with a fat field set, or `[SerializeReference]` polymorphism on a base class.
- **Pros**: Asset count drops by 8. Authoring fits in one Inspector field. Adding a new effect type post-EA is a one-enum-value change.
- **Cons**: Loses Inspector polymorphism (designers can't see "this card has a Damage effect, this one has a DrawCards effect" — they see a generic descriptor with mostly-empty fields). Validation per-effect-type becomes a giant `switch` instead of per-class `OnValidate`. `[SerializeReference]` is not stripping-safe under IL2CPP managed-stripping=High without per-type `link.xml` entries (the IL2CPP cost is identical to the sealed-subclass approach but with worse Editor UX). Loses compile-time type safety: a `DrawCardsEffect` becoming a `DamageEffect` at runtime is a silent failure.
- **Rejection Reason**: Locked at Phase 3 design question by user. Loses Editor UX and compile-time type safety to save asset-count overhead that doesn't materially affect performance or memory. The sealed-subclass design also matches the GDD's effect taxonomy (each effect type already has its own field set in card-system.md §"EA Effect Types") — the descriptor approach forces the designer to mentally translate between GDD spec and authoring model.

### Alternative 3: Pre-baked tokens on CardDefinitionSO

- **Description**: Editor pipeline runs after every SO save and bakes `Description` into a serialized `ResolvedRulesText` field. Runtime never resolves tokens.
- **Pros**: Zero runtime token-resolution cost. UI surfaces consume `ResolvedRulesText` directly. No `TokenResolver` service.
- **Cons**: Brittle under designer iteration — every effect-SO field change requires re-baking every dependent CardDefinitionSO. Stale resolved text is silent (no compile error if effect SO Amount changes but card SO didn't re-bake). The "single source of truth" property is broken: the designer thinks the SO field is authoritative, but actually the baked string is. Cross-cuts ADR-0002 boundary if the baker runs in `WastelandRun.Cards` (would require Unity reference for AssetDatabase) — ergo baker would have to live in `WastelandRun.Gameplay` and the runtime has to trust the bake didn't drift.
- **Rejection Reason**: Locked at Phase 3 design question by user. Trades a real property (single source of truth) for an irrelevant performance gain (token resolution is ~1µs per card; not on the hot path).

## Consequences

### Positive

- **Engine-free combat preserved**: Combat resolution can walk `ICardEffect` records without any Unity reference, keeping the 136-test EditMode suite headless and extensible to future card types.
- **Determinism inherited**: `RewardDrawAlgorithm` follows ADR-0003's caller-owned RNG pattern, so reward draws roundtrip in saves and reproduce identically in tests/replays/balance simulators.
- **Save schema is locked**: `CardSystemDTO` declares `SystemId` + `SchemaVersion` constants per ADR-0004; CI grep enforces uniqueness against `vehicle-state` and others.
- **Token resolution is single-sourced**: All five UI display contexts call `TokenResolver.Resolve(card)`; balance changes propagate immediately, no rebaking.
- **GDD effect taxonomy is preserved**: 8 sealed `CardEffectSO` subclasses match the 8 effect types the GDD already names; designer mental model is 1:1 with implementation.
- **IL2CPP discipline**: `link.xml` entries for `CardDefinitionSO` and 11 SO subclasses (8 effects + 3 conditions + 1 chassis-mastery + the card SO) prevent stripping; the same Standalone IL2CPP smoke test from ADR-0005 extends to load one card SO and assert non-null `Effects` array.
- **Loader pattern reused**: `AddressablesCardCatalog` is a near-clone of `AddressablesPartCatalog` from ADR-0005 — same try/catch + `handle.IsValid()` finally guard, same `EditorBuildPreprocessor` "Include In Build" gate.

### Negative

- **Two assemblies for cards** (Cards + Gameplay) instead of one — slight build-time overhead, one extra `.asmdef` to maintain.
- **Projection layer maintenance**: every new field added to `CardDefinitionSO` requires the corresponding update to `ICardData` and the projection method. Mitigated by the small surface (the data contract is locked in card-system.md and rarely changes).
- **Effect projection allocates**: Each card's `Effects` is projected once per `CardDefinitionSO` instance and cached. First access cost is `O(N) * (allocate record)`; cached access is free. Acceptable because cards load at run-start, not per frame.
- **`CardCopyCounts` is derived but persisted**: Risks drift from `Deck` list if a save mutation forgets to update both. Mitigated by `CardSystemSaveAdapter` being the sole writer; an invariants test asserts `CardCopyCounts == Deck.GroupBy(id).Count()` on every save.
- **Designer authoring overhead**: Adding a new card requires (a) creating a `CardDefinitionSO`, (b) creating one `CardEffectSO` per effect, (c) wiring effects via Inspector references, (d) adding to the `card-definitions` Addressables label. Mitigated by an editor menu helper "Create Card with N Effects" that scaffolds the assets in one click.

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| **IL2CPP strips `CardDefinitionSO`, effect/condition SO subclasses, or `EffectConditionProjection` static helper** because runtime references go through `ICardData`/`ICardEffect`/`ICardEffectCondition` interfaces only | MEDIUM | HIGH (cards return empty Effects, or DamageEffects load without their conditions, in standalone — never reproduces in editor) | Add `link.xml` entries with `preserve="all"` for `WastelandRun.Gameplay.Cards.CardDefinitionSO`, all 8 `*EffectSO` subclasses, all 3 `*ConditionSO` subclasses, `ChassisMasteryDefinitionSO`, and the `EffectConditionProjection` static helper class. Migration step 4 includes the `link.xml` edit. The ADR-0005 standalone IL2CPP smoke test extends to load one card SO via Addressables and assert (a) `Effects.Count > 0` and (b) a card with a conditional `DamageEffect` retains its conditions after projection. |
| **`Addressables.LoadAssetsAsync<CardDefinitionSO>("card-definitions")` throws when the label is empty in a build profile that excluded the group** | MEDIUM | HIGH (no cards available; reward screen breaks) | Wrap in try/catch + `handle.IsValid()` finally guard per the registered `vehicle_part_asset_loading` API decision (ADR-0001/0005). Editor `EditorBuildPreprocessor` reads Addressables settings and fails the build if the `card-definitions` group is not flagged Include-In-Build. |
| **Card pool partition by `ChassisPool` at load is wrong when designer mid-iteration assigns the wrong enum** | MEDIUM | MEDIUM (cards never appear in offers; silently absent) | `OnValidate()` on `CardDefinitionSO` rejects `ChassisPool == default` (forces explicit choice). Editor smoke test enumerates all loaded cards and asserts each chassis pool has ≥ 75 cards (the EA pool floor). |
| **Pity counter desync between Card System and Loot & Reward** if both try to mutate it | LOW | HIGH (rare cadence breaks, telemetry diverges) | ADR locks the authority split: Card System reads counter, Loot & Reward writes counter. Forbidden pattern registered in Phase 6: `card_system_writes_pity_counter`. Code review and a dev-build assertion confirm `RewardDrawAlgorithm` never reaches a counter-mutation method. |
| **`CardCopyCounts` drift from `Deck` list contents** under partial save corruption | LOW | MEDIUM (offer filter shows 4th copy of a 3-cap card) | `CardSystemSaveAdapter` is the sole writer of `CardSystemDTO`. On load, an invariants check rebuilds `CardCopyCounts` from `Deck` and logs a warning + auto-corrects if mismatch. Unit test covers corrupted-input path. |
| **`SelectionHash` collisions cause telemetry false-merge** | LOW | LOW (analytics only; no gameplay impact) | Hash is `HashCode.Combine(CardId, chassis, mastery, slotIndex, runSeed)` — collision space is negligible for run-scope analytics. If telemetry pipeline finds collisions in the 0.001%+ range, escalate to GUID-per-draft. |
| **Token resolver order-dependency**: a card with two `DamageEffect`s referencing `{damage.1}` and `{damage.2}` produces wrong text if `Effects` order changes during projection | LOW | MEDIUM (display drift after refactor) | Projection order matches `_effectSOs` array order, which matches Inspector order. AC test (card-system.md line 673) asserts indexed token resolution holds. Unit test covers two-DamageEffect card across all five display surfaces. |
| **`[Flags]` `CardKeyword` enum: bitwise misuse** by designer setting `Ethereal | Retain` | LOW | MEDIUM (mutually-exclusive contract violated, GDD EC2) | `OnValidate()` on `CardDefinitionSO` rejects `(Keywords & Ethereal) != 0 && (Keywords & Retain) != 0` per AC. SO import error logged with descriptive message. |
| **Combat resolution adds a switch statement on `ICardEffect` subtype** that breaks open-closed when new effects are added post-EA | MEDIUM | LOW (compile-time switch warning, not silent) | Use C# pattern matching with discriminated `switch` expression on the sealed record hierarchy — adding a new `ICardEffect` subtype forces a compile-time error in combat resolution until the new branch is added. EC3 keyword extensibility property holds for effects too. |
| **`StatusType` shape is locked by pending ADR-0007 (Status Effects Subsystem)** — `ApplyStatusEffect`, `StatusCondition`, and the `OnStatusApplied` event chain all assume `StatusType` is a stable enum imported from `WastelandRun.Vehicle`. If ADR-0007 redesigns status as a non-enum identity (e.g., string ID, `StatusDefinitionSO` reference, or hash-based registry), several record fields churn. | MEDIUM | LOW (amendment is mechanical — fixed-arity record signature change, no behavioral redesign) | This ADR does not lock `StatusType`'s shape — it imports whatever ADR-0007 finalizes. **Amendment clause**: if ADR-0007 changes `StatusType` from an enum, ADR-0006 amends `ApplyStatusEffect.Status` and `StatusCondition.Status` field types to match. The sealed-record discriminated hierarchy makes the change compile-time-safe across combat resolution. No data migration needed pre-Acceptance. |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| card-system.md | TR-card-001 — `CardDefinitionSO` is single authority for card metadata | `CardDefinitionSO` declared as `[CreateAssetMenu]` ScriptableObject; `ICardData` projects it to runtime. All combat reads flow through `ICardData`, no other source. |
| card-system.md | TR-card-002 — `CardId` format `[chassis]_[family]_[seq]` stable for save/serialization | `CardId` is the only card identity persisted in `CardSystemDTO` — Deck/Discard/Exhausted are `List<string>`. `OnValidate` rejects malformed CardIds. |
| card-system.md | TR-card-003 — Description tokens resolve at display time | `TokenResolver.Resolve(ICardData)` runs at draft-generation (CardDraft.RulesText) and at combat hand tooltip; never pre-baked. |
| card-system.md | TR-card-004 — `CardEffectSO` hierarchy + condition gating + sequential resolution | 8 sealed `CardEffectSO` subclasses + 3 sealed `EffectConditionSO` subclasses; combat walks `ICardData.Effects` in list order. |
| card-system.md | TR-card-005 — `DamageEffectSO.BypassPlating` enables subsystem-strike | `DamageEffect` record has `BypassPlating` field; combat F-CC1 step 2 skip is owned by Card Combat resolution, not by this ADR. |
| card-system.md | TR-card-006 — `RestoreArmorEffectSO` uses `IVehicleMutator.AddArmor`, capped at MaxArmor | `RestoreArmorEffect` resolves through `IVehicleMutator.AddArmor(int)` (registered per ADR-0005); cap is V&P-internal. |
| card-system.md | TR-card-007 — Card families taxonomy | `CardFamily` enum with 5 values; consistent across chassis. |
| card-system.md | TR-card-008 — Control family must contain ≥1 DamageEffect with Amount ≥ 1 | `CardDefinitionSO.OnValidate` enforces per AC line 617. |
| card-system.md | TR-card-009 — Card keywords + Ethereal/Retain mutual exclusion | `CardKeyword` `[Flags]` enum; `OnValidate` rejects the conflicting pair. |
| card-system.md | TR-card-010 — Innate cap of 3 per deck | Filter applied in `RewardDrawAlgorithm` step 2 by checking `currentDeckCardIds` for Innate-keyword count. |
| card-system.md | TR-card-011 — Starter deck is 10 cards per chassis | `ChassisDefinitionSO.StarterDeck` (existing per ADR-0005's chassis SO) holds the 10 CardId references. |
| card-system.md | TR-card-012 — Rarity weights stored on `ChassisMasteryDefinitionSO`, sum to 100 | `ChassisMasteryDefinitionSO` declared in `WastelandRun.Gameplay.Cards`; implements `IMasteryWeights`. `OnValidate` enforces sum-to-100. |
| card-system.md | TR-card-013 — Pool exhaustion fallback to next-lower tier | `RewardDrawAlgorithm` step 4 (tier-degradation fallback). |
| card-system.md | TR-card-014 — Pity counter: 8 dry combats → 9th guaranteed Rare or Scrap | `RewardDrawAlgorithm` step 1 (pity precedence) + step 5 (empty Rare pool returns `[]` for Scrap compensation). |
| card-system.md | TR-card-015 — Primary-family bias slot 1 at Mastery 1-3 | `RewardDrawAlgorithm` step 3. |
| card-system.md | TR-card-016 — Copy limits + no duplicates in single offer | `RewardDrawAlgorithm` step 2 filter (`copyLimitOK`) + `seenInOffer` HashSet. |
| card-system.md | TR-card-017 — Card states (Deck/Hand/Discard/Exhausted) + reshuffle | `CardLocation` enum + Combat assembly's existing Deck/Hand/Discard model. |
| card-system.md | TR-card-018 — Purge costs flat `GlobalPurgeCost = 30`, deck floor 10 | Card System exposes `DeckSize > 10` precondition; Scrap Economy owns the cost constant. No per-card purge cost field. |
| card-system.md | TR-card-019 — Free purge valve from `runSeed + nodeIndex` | Owned by Scrap Economy / Node Map ADRs (future); Card System is read-only consumer of the resolved boolean. |
| card-system.md | TR-card-020 — `DamageOutput = BaseDamage + (PositionBonus × PositionConditionMet)` | `DamageEffect` record carries `Amount` + `PositionBonus`; combat resolution applies the formula. |
| card-system.md | TR-card-021 — `ICardRewardGenerator.Generate()` provides `CardDraft[]` with resolved tokens | Interface declared in `WastelandRun.Cards`; `RewardDrawAlgorithm` is the single implementation. |
| card-system.md | TR-card-022 — Card effect resolution reads via `IVehicleView`, mutates via `IVehicleMutator` | Combat resolution receives `(IVehicleView, IVehicleMutator)` and dispatches per `ICardEffect` subtype; this ADR locks the runtime types. |
| card-system.md | TR-card-023 — `EffectConditionSO` hierarchy gates effects | 3 sealed `EffectConditionSO` subclasses → `ICardEffectCondition` records on `DamageEffect.Conditions`. |
| card-system.md | TR-card-024 — `DrawCardsEffectSO` with empty deck silently produces nothing | `DrawCardsEffect` resolution in Combat handles empty deck per EC5. Card System surface (the record) is unchanged. |
| card-system.md | TR-card-025 — SO import validates `BaseDamage == DamageEffectSO.Amount`, `BaseDamage ≥ 1` | `CardDefinitionSO.OnValidate` enforces per AC lines 618-619. |
| card-combat-system.md | Card resolution must walk `Effects` in order with conditional gating | `ICardEffect` is sealed-record hierarchy enabling exhaustive C# `switch` expression in combat resolution. |
| loot-reward.md | Reward node consumes `CardDraft[]` from `ICardRewardGenerator` (retrofit 2026-04-23) | Interface signature locked here; `CardDraft` schema matches loot-reward.md L&R interface contract. |
| scrap-economy.md | `MerchantPrice` field on cards must not equal `GlobalPurgeCost (30)` | `CardDefinitionSO.OnValidate` rejects `MerchantPrice == 30` per AC line 616. |
| save-persistence.md | Per-system DTO with `SystemId` + `SchemaVersion` compile-time constants | `CardSystemDTO` declares both as `public const`; CI grep verifies uniqueness. |

## Performance Implications

- **CPU**: Reward draw is post-combat (~14×/run). Algorithmic cost is `O(drawCount × poolSize)` filtering — dominated by copy-limit and seen-in-offer checks. Negligible at 100 cards × 2 slots = 200 ops per draw.
- **Token resolution**: `TokenResolver.Resolve` runs (a) once per `CardDraft` at generation (~14×/run), (b) once per combat-hand tooltip hover (~1-3×/turn). Each call is an `O(|Description|)` regex pass + small `string.Format`. ~1µs per resolve. Not on the per-frame hot path.
- **Memory**: `CardDefinitionSO` count is 75-100 cards × 2 chassis = 150-200 SO instances. Each carries an `ICardEffect[]` projection cache. Estimated 50-200 KB total at run-start, persistent for the run.
- **Load time**: Single `Addressables.LoadAssetsAsync<CardDefinitionSO>("card-definitions")` call at run-start. ~50-200 ms cold load on a typical PC; acceptable as a one-shot run-start cost behind a loading screen.
- **Network**: N/A — single-player only.

## Migration Plan

1. **Create `WastelandRun.Cards.asmdef`** with `noEngineReferences: true` at `Assets/Scripts/Cards/`. Add enums (`CardFamily`, `CardRarity`, `CardKeyword`, `CardTargetType`, `PositionRequirement`, `CardLocation`) + interfaces (`ICardData`, `ICardEffect`, `ICardEffectCondition`, `ICardCatalog`, `ICardRewardGenerator`, `IMasteryWeights`) + records (`CardDraft`, 8 effect records, 3 condition records, `CardSystemDTO`). No behavior yet — contracts only. *Verify*: compile passes with zero `UnityEngine.*` imports (CI grep extends existing ADR-0005 audit).
2. **Add `WastelandRun.Combat.asmdef → WastelandRun.Cards` reference** + `WastelandRun.Gameplay.asmdef → WastelandRun.Cards` reference. *Verify*: existing 136 Combat EditMode tests still pass; Gameplay assembly compiles.
3. **Implement `TokenResolver` and `RewardDrawAlgorithm` in `WastelandRun.Cards`**. *Verify*: NUnit EditMode tests cover (a) all 11 token types resolve correctly per the binding table, (b) determinism — `new System.Random(42)` produces identical 100k-draw sequence on three runs (SHA-256 match), (c) primary-family bias holds at ≥98% for slot 1 / Mastery 1-3 in 10k draws, (d) pity counter at 8 → 9th draw is Rare or `[]`, (e) copy-limit filter holds at all rarity tiers.
4. **Author `CardDefinitionSO`, 8 `CardEffectSO` subclasses, 3 `EffectConditionSO` subclasses, `ChassisMasteryDefinitionSO`** in `Assets/Scripts/Gameplay/Cards/` under `WastelandRun.Gameplay.Cards`. Each implements `ToRuntime()` projection. `[CreateAssetMenu]` paths under `Wasteland/Card/...`. **Add `link.xml` entries** with `preserve="all"` for: `WastelandRun.Gameplay.Cards.CardDefinitionSO`, all 8 `*EffectSO` subclasses, all 3 `*ConditionSO` subclasses, `WastelandRun.Gameplay.Cards.ChassisMasteryDefinitionSO`, and `WastelandRun.Gameplay.Cards.EffectConditionProjection` (static helper called by every `*EffectSO.ToRuntime()` that holds gating conditions — without this entry IL2CPP managed-stripping=High strips the helper and condition projection silently returns empty). Also add `_effectsCache = null;` as the first statement of `CardDefinitionSO.OnValidate()` to invalidate the projection cache before any read — prevents stale cache during editor authoring of referenced condition SOs (specialist concern #1). *Verify*: Editor smoke test authors one invalid SO of each type (e.g., Control card with no DamageEffect, Rare card with weights summing to 99) and confirms `OnValidate` rejects each. Standalone IL2CPP smoke test extends to load one `CardDefinitionSO` via Addressables and assert `Effects.Count > 0` AND that a `DamageEffect` with at least one condition retains its conditions after projection (catches `EffectConditionProjection` stripping).
5. **Implement `AddressablesCardCatalog` AND `AddressablesMasteryCatalog` in `WastelandRun.Gameplay.Cards`** following the `AddressablesPartCatalog` pattern from ADR-0005 — try/catch around `LoadAssetsAsync<CardDefinitionSO>("card-definitions")` (resp. `LoadAssetsAsync<ChassisMasteryDefinitionSO>("chassis-mastery-definitions")`), `handle.IsValid()` finally guard, partition results by `ChassisPool` (resp. `ChassisType`) into per-chassis read-only lookup. The two catalogs are siblings — `AddressablesCardCatalog` does not depend on or know about `AddressablesMasteryCatalog`. Bootstrap orchestration loads both and constructs `new RewardDrawAlgorithm(cardCatalog, masteryCatalog, tokens)` — keeps the catalog's responsibility narrow per ADR-0003 DI discipline. Verify the build profile has BOTH `card-definitions` and `chassis-mastery-definitions` Addressables groups set to Include In Build (gated by extending `EditorBuildPreprocessor` from ADR-0005 to check both groups).
6. **Author starter-deck card SOs** (10 per chassis × 2 chassis = 20 cards) and validate each against `OnValidate`. Wire `ChassisDefinitionSO.StarterDeck` to reference these CardIds. *Verify*: starter deck integrity AC (line 627-628) passes for both chassis.
7. **Migrate existing `CardDefinition` POCO in `WastelandRun.Combat`** to use the new `ICardData` surface. Combat resolution code adds a `switch` expression over `ICardEffect` subtypes; each branch dispatches to the existing `IVehicleMutator` methods (or no-ops for surface-level effects like `DrawCardsEffect`). *Verify*: 136 EditMode tests still pass after migration; new EditMode tests cover effect-resolution branches.
8. **Implement `CardSystemSaveAdapter`** in `WastelandRun.Cards` following ADR-0004's adapter pattern. Sole writer of `CardSystemDTO`; on load, invariants check rebuilds `CardCopyCounts` from `Deck` list and logs+auto-corrects if mismatch. *Verify*: ADR-0004 save-roundtrip integration test extends to cover Card System.
9. **Update `tr-registry.yaml`**: mark TR-card-001 through TR-card-025 as covered by ADR-0006 (Phase 8 of `/architecture-review` will do this; this step records the link).
10. **Author full EA card pool** (75-100 cards per chassis × 2 chassis = 150-200 SOs). Out of scope for this ADR — happens in the card-content authoring epic after this ADR is Accepted.

## Validation Criteria

This ADR is correct iff all of the following hold after Migration steps 1-8:

1. **Engine-free**: `WastelandRun.Cards.asmdef` compiles with zero `UnityEngine.*` imports (CI grep).
2. **Determinism**: `RewardDrawAlgorithm.Generate(chassis, mastery, pity, drawCount, deck, new System.Random(42))` produces SHA-256-identical `CardDraft[]` output on three consecutive runs across 100,000 simulated draws (per AC card-system.md line 641).
3. **Rarity weights**: 100,000-draw simulation at Mastery 1-3 holds Rare in [0.7%, 1.3%] band (AC line 641).
4. **Primary-family bias**: 10,000-draw simulation at Scout / Mastery 1-3 has Slot 1 in primary-family pool ≥98% of offers where pool is non-empty (AC line 645).
5. **Pity counter**: simulated 8 dry combats → 9th draw is Rare (or `[]` for Scrap compensation if pool empty); counter resets correctly (AC lines 642-643).
6. **Copy limits**: 4th copy of a Common/Uncommon and 2nd copy of a Rare cannot enter deck via reward or Merchant (AC line 647).
7. **IL2CPP standalone**: build with managed-stripping=High loads one `CardDefinitionSO` via `Addressables.LoadAssetsAsync` and observes (a) `Effects.Count > 0` and (b) a card with at least one conditional `DamageEffect` retains `Conditions.Count > 0` after projection (catches `EffectConditionProjection` static-helper stripping). Also loads one `ChassisMasteryDefinitionSO` via `AddressablesMasteryCatalog` and observes `GetWeights(chassis, mastery)` returns non-default values (extending the ADR-0005 smoke test).
8. **SO import rules**: `OnValidate` rejects every invalid case from card-system.md AC §"Data Contract" (12 rules). Editor smoke test covers each.
9. **Token resolution**: indexed `{damage.1}` / `{damage.2}` resolve correctly in all five display contexts (AC line 673).
10. **Save roundtrip**: `CardSystemDTO` serialize → deserialize → re-serialize produces byte-identical JSON; corrupted `CardCopyCounts` is auto-corrected on load.
11. **Combat tests pass**: existing 136 EditMode Combat tests pass after Migration step 7 with no test modifications.
12. **No banned tokens**: `WastelandRun.Cards` and `WastelandRun.Gameplay.Cards` contain zero occurrences of `UnityEngine.Random`, `DateTime.Now`, `Random.Range`, `Time.realtimeSinceStartup` (CI grep extends ADR-0003 audit to the new namespaces).
13. **`SourceSlotId` projection invariants** (per Amendment 2026-05-18, ADR-0007 Decision 16): (a) `CardDefinitionSO`-authored cards loaded via `AddressablesCardCatalog` have `SourceSlotId == null` (no part-install context). (b) Cards produced by a part-install grant factory carry `SourceSlotId == <slotId>` matching the installing slot. (c) External-source granted cards (e.g. Dredge Javelin tether cohort) have `SourceSlotId == null`. (d) `CardSystemDTO` does NOT serialize `SourceSlotId`; save → load → save roundtrip remains byte-identical for any deck whose cards were granted from the same install path (covered by an EditMode test that grants two Engine cards, saves, reloads, and asserts the reconstructed projection re-stamps the same `SourceSlotId`).

## Related Decisions

- **ADR-0001** (Visual Vehicle Part System) — supplies `SlotType` enum and rendering pipeline that card effects ultimately drive
- **ADR-0002** (Card Combat POCO state model) — establishes `noEngineReferences` assembly discipline, deterministic seeding, exception-based validation; this ADR extends the same pattern to card data authoring
- **ADR-0003** (Loot RNG determinism) — `RewardDrawAlgorithm` follows the caller-owned `System.Random` contract; CI-enforced forbidden tokens cover the new namespaces
- **ADR-0004** (Save & Persistence) — `CardSystemDTO` follows the distributed schema registry pattern (`SystemId` + `SchemaVersion` constants)
- **ADR-0005** (Vehicle POCO + Part Catalog) — supplies `ChassisType`, `IPartCatalog` reference pattern (mirrored as `ICardCatalog`), assembly split idiom, IL2CPP `link.xml` discipline, Addressables loader pattern with try/catch + `handle.IsValid()` guard
- **`design/gdd/card-system.md`** — primary GDD; 25 TR-card requirements addressed
- **`design/gdd/card-combat-system.md`** — consumer of `ICardData` + `ICardEffect`; resolution logic owned there
- **`design/gdd/loot-reward.md`** — consumer of `ICardRewardGenerator`; pity counter authority owned there
- **`design/gdd/scrap-economy.md`** — owns `GlobalPurgeCost = 30` and `MerchantPrice` parity rule
- **`design/gdd/save-persistence.md`** — distributed schema registry consumer

## Revision History

| Date | Change | Source |
|------|--------|--------|
| 2026-04-25 | Authored | Card System architecture round |
| 2026-04-27 | Accepted | Verdict round (TD-ADR + unity-specialist CONCERNS resolved) |
| 2026-05-18 | Amendment — nullable `ICardData.SourceSlotId` field added; granted-card lifecycle (soft disable + hard removal) cross-referenced to ADR-0007 Decision 16. No `SchemaVersion` increment (runtime-only field). Validation criterion #13 added. | ADR-0007 Phase 2 R6 handoff |
