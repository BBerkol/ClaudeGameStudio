# ADR-0005: Vehicle POCO Sub-Model + Part Catalog

## Status

Accepted

## Date

2026-04-25

## Acceptance Date

2026-04-27

## Last Verified

2026-04-27

## Decision Makers

- User (creative/design lead)
- technical-director (TD-ADR gate completed 2026-04-25, verdict CONCERNS — all addressed; insights folded into Risks rows 7–11)
- unity-specialist (engine-idiom review completed 2026-04-25, verdict CONCERNS — all addressed; IL2CPP `link.xml`, Addressables Unity 6.2+ exception semantics, build-profile gate, `OnValidate` runtime stripping, and domain-reload handle invalidation captured)

## Summary

The Vehicle is a plain C# POCO with four typed slots (Weapon, Engine, Mobility, Frame), a derived vehicle-level Armor stat, and a status list — authored through `ChassisDefinitionSO` + `PartDefinitionSO` ScriptableObjects and looked up via a stateless `IPartCatalog`. Downstream systems read vehicle state exclusively through `IVehicleView` and mutate it only through `IVehicleMutator`, with stat composition resolved by a fixed `(base + Add) × Multiply` pipeline and Override as a single-winner short-circuit.

## ADR-0001 Amendments

The `vehicle-and-part-system.md` GDD (post-retrofit 2026-04-23) diverges from ADR-0001's originally-registered contract in four places. **The GDD is authoritative**; this ADR aligns the data-layer code with the GDD and formally amends the registered contract. ADR-0001's visual-layer decisions (URP Sprite Lit Shader Graph, MPB overlays, Addressables groups) remain in force unchanged — only the interface surface shifts.

| Locked in ADR-0001 (pre-retrofit) | Revised per V&P GDD + this ADR | Rationale |
|---|---|---|
| **5 visible slots**: Weapon, Armor, Engine, Mobility, Frame | **4 data slots**: Weapon, Engine, Mobility, Frame. Armor is a vehicle-level derived stat (`MaxArmor = Σ part.ArmorContribution`). | GDD R1 (lines 32–35) declares Armor is not a slot. Armor visuals in ADR-0001 persist as a chassis-level MPB overlay; no `ArmorSlot` sprite renderer. |
| **`DamageState { Functional, Degraded, Offline }`** (3 values) | **`DamageState { Empty, Functional, Degraded, Offline }`** (4 values; `Empty` means `InstalledPart == null`) | GDD R3 (line 176) explicitly declares 4 values; `Empty` is required so `IVehicleView.GetDamageState` can return a meaningful value on an uninstalled slot without throwing. |
| **`event Action<SlotType, DamageState>`** (2-arg: slot, new state) | **`event Action<SlotType, DamageState, DamageState>`** (3-arg: slot, from, to) | GDD Section R9 (line 189) declares `(slot, from, to)`. The extra `from` parameter lets visual transitions animate differently based on direction (Functional → Degraded vs. Offline → Degraded repair). |
| **`void ApplyDamage(SlotType slot, int amount)`** (2-arg) | **`void ApplyDamage(SlotType slot, int amount, DamageSource source)`** (3-arg) | GDD Section R9 (line 203) adds `DamageSource` for F-VP2 Armor-bypass on DOT damage. `DamageSource` enum is `{ Card, Status, Environment }` per GDD line 178. |

**Action on Accept of ADR-0005**:
1. Update ADR-0001 `## Status` to `Accepted` with an amendment note: `"Amended 2026-04-25 by ADR-0005 — see ADR-0001 Amendments subsection."`
2. Update `docs/registry/architecture.yaml`:
   - `state_ownership.vehicle_slot_damage_state.interface` → add `(4 values: Empty/Functional/Degraded/Offline)`
   - `interfaces.vehicle_state_access.signal_signature` → `event Action<SlotType, DamageState, DamageState> OnSlotDamageStateChanged  // (slot, from, to)`
   - Add `referenced_by: docs/architecture/adr-0005-vehicle-poco-part-catalog.md` on both entries.
3. Update ADR-0001's "5 visible slots" wording to "4 data slots + vehicle-level Armor overlay" in the Context and Decision sections. Leave ADR-0001's art-production Consequences (40–60 sprite budget, Addressables groups) unchanged — the visual asset count is independent of data-slot count.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.13f1) |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW — pure C# POCOs + standard `ScriptableObject` + Addressables label lookup; no post-cutoff APIs |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/modules/` (scripting, addressables), `.claude/docs/technical-preferences.md`, ADR-0001, ADR-0002 |
| **Post-Cutoff APIs Used** | None (ScriptableObject + Addressables lookup are pre-cutoff APIs; Unity 6.3 behavior matches training-data semantics for these) |
| **Verification Required** | Confirm `Addressables.LoadResourceLocationsAsync` + label `part-definitions` round-trip at editor-time (standard path, but validate on first spike). POCO layer has no runtime engine dependency to verify. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (Visual Vehicle Part System — locks `IVehicleView.GetDamageState`, write-whitelist registry, `OnSlotDamageStateChanged` event); ADR-0002 (Card Combat POCO residency pattern — same `noEngineReferences: true` discipline); ADR-0003 (Deterministic RNG — IPartCatalog lookup must accept caller-owned `System.Random` when used by Loot/Map seeded paths) |
| **Enables** | ADR-0007 (Status Effects — reads slot state + applies via `IVehicleMutator.ApplyStatus`); ADR-0008 (Scrap Economy — sole non-Combat caller of `InstallPart`/`RemovePart`/`Repair`); ADR-0010 (Node Encounter Handlers — route on vehicle state); ADR-0012 (Loot & Reward — uses `IPartCatalog.GetParts` + `PreviewInstall`) |
| **Blocks** | All V&P system stories; Combat integration stories that bind Card effects to slot-level mutation; Loot/reward generation stories |
| **Ordering Note** | This is the priority-1 Foundation-layer ADR per architecture-traceability. ADR-0006 (Card System Data Authoring) is priority-2 and may be written in parallel (no direct dependency). All Core ADRs (0007/0008/0009) must wait on this. |

## Context

### Problem Statement

The `vehicle-and-part-system.md` GDD (25 TRs across 9 sections) defines the single most shared data shape in the game: every combat turn, every status tick, every loot drop, every chopshop transaction, and every save snapshot reads or mutates a Vehicle. ADR-0001 locked the *visual* layer (sprite rendering, MPB overlays, Addressables groups) but explicitly deferred the *data* layer: the GDD's 4-slot structure, the vehicle-level derived Armor stat, the stat composition pipeline, the install/scrap/damage rules, and the authoring contracts for Chassis and Part ScriptableObjects are undecided in architecture.

Writing any Core ADR (Status Effects, Scrap Economy, Node Map) without this one first forces those systems to either (a) invent a provisional vehicle shape and refactor later, or (b) block on an implicit assumption that cannot be audited. The cost of not deciding now is that eight downstream ADRs either stall or build on unstated contracts.

### Current State

- ADR-0001 established `IVehicleView.GetDamageState(SlotType)` and a write-whitelist (`vehicle_state_access` entry in `docs/registry/architecture.yaml`) but did not define the full POCO, the IPartCatalog, the stat composition order, or the install/scrap rules.
- ADR-0002 established the POCO residency pattern (`noEngineReferences: true`) in the `WastelandRun.Combat` assembly but scoped it to combat orchestration — Vehicle currently lives inside that assembly as an ad-hoc type (`Vehicle.cs` in `Assets/Scripts/Combat`) with the state fields Combat needs (`MaxArmor`, `CurrentArmor`, `ActiveStatuses`, slot `Hp`, `PlatingStacks`) but without the GDD-authoritative shape (no `CompatibleChassis` validation, no stat composition, no `InstallPart` method, no derived MaxArmor recomputation).
- No `IVehicleView`/`IVehicleMutator` interfaces exist in code yet — ADR-0001 declared them as contracts to be filled by this ADR.
- No `PartDefinitionSO` / `ChassisDefinitionSO` types exist. Chassis variants (Scout/Assault) are placeholder MonoBehaviours.
- No `IPartCatalog` exists. Loot currently hardcodes drops; Chopshop has no backing data source.
- The V&P GDD (lines 1–1109) is the authoritative design; the implementation must conform.

### Constraints

- **ADR-0001 interface contract (locked)**: POCO must expose `IVehicleView.GetDamageState(SlotType) → DamageState`; mutation must route through `IVehicleMutator` with callers restricted to Combat (`ApplyDamage`, `AddPlating`, `AddArmor`, `ApplyStatus`, `Repair`), Economy (`InstallPart`, `RemovePart`), and Loot (`InstallPart`); `OnSlotDamageStateChanged` event must fire on state transitions.
- **ADR-0002 POCO residency**: Vehicle sub-model must compile in a `noEngineReferences: true` assembly — no `UnityEngine.*` types in fields, constructors, or method signatures. This forbids `ScriptableObject` *as a direct field*, but SO references may be stored as interface handles (`IPartDefinition`) or loaded into POCO projections.
- **ADR-0003 RNG discipline**: any IPartCatalog path used by Loot or Map must accept caller-owned `System.Random`; IPartCatalog must not own an internal RNG.
- **Tech-prefs forbidden patterns**: no `UnityEvent`, no `UnityEngine.Random`, no hardcoded gameplay values, no combat state on MonoBehaviours.
- **Unity 6.3 breaking changes**: `[SerializeField]` is fields-only (use `[field: SerializeField]` on auto-properties in SOs); `FindObjectsByType<T>(FindObjectsSortMode.None)` replaces `FindObjectsOfType` (not used by POCO anyway).
- **Runtime budget**: 60 FPS / 16.6 ms. Stat composition runs O(Σ installed parts × modifiers) per read; must be under 0.1 ms for typical vehicles (4 parts × 4 modifiers each = 16 ops). Recalculation triggered only on install/scrap/state transition.

### Requirements

- **R1** Vehicle POCO lives in a `noEngineReferences: true` assembly (candidate: `WastelandRun.Vehicle` or existing `WastelandRun.Combat`).
- **R2** Exactly 4 slots (Weapon, Engine, Mobility, Frame). Slot count is a compile-time invariant; no runtime slot add/remove.
- **R3** `MaxArmor` is a derived vehicle-level stat, not a slot: `MaxArmor = Σ part.ArmorContribution` across installed parts. `CurrentArmor` is always clamped `0 ≤ CurrentArmor ≤ MaxArmor`.
- **R4** `DamageState` is derived from `Hp`, never stored: `Empty` (no part) / `Functional` (Hp > DegradedThreshold% of MaxHp) / `Degraded` (0 < Hp ≤ DegradedThreshold%) / `Offline` (Hp == 0).
- **R5** Stat composition order is `(base + Σ Add) × Π Multiply`, with `Override` as a single-winner short-circuit that replaces the result. Multiple Overrides for the same stat = SO-import error (AC-VP29).
- **R6** `InstallPart` auto-scraps the existing part (if any), validates `CompatibleChassis` and `SlotType`, resets `Hp = MaxHp` and `PlatingStacks = 0`, recalculates `MaxArmor` and clamps `CurrentArmor`, adds granted cards to the deck pile, and fires `OnPartInstalled` + (if applicable) `OnMaxArmorChanged`.
- **R7** `RemovePart` removes granted cards from all deck zones (deck/hand/discard), sets `Hp = 0` and `PlatingStacks = 0`, recalculates `MaxArmor`, and fires `OnPartRemoved`. Scrap is permanent for the run.
- **R8** Damage resolves plating before Hp: `plating_consumed = min(amount, PlatingStacks)`; remainder subtracts from `Hp` (non-Frame) or vehicle `CurrentArmor` then `Hp` (Frame). DOT ticks bypass Armor.
- **R9** `IPartCatalog.GetParts(SlotType, PartRarity, ChassisType)` returns an `IReadOnlyList<PartDefinitionSO>` — stateless, no caller-owned RNG; seeded selection happens in Loot/Map (ADR-0003 compliant).
- **R10** `IPartCatalog.PreviewInstall(IVehicleView, SlotType, PartDefinitionSO) → VehicleStatePreview` is a pure read helper for compare-panel UI; projects stat changes without mutating.
- **R11** SO import validators (editor-time): `CompatibleChassis.Length ≥ 1`; `GrantedCards.Length` matches rarity table (1/2/3/3); `ArmorContribution ≥ 0`; `MaxPlating ∈ [0..5]` (and 0 for Frame); `SlotMaxHp.Count == 4`; `StarterParts.Count == 4` with all compatible; `StarterDeck.Length == 10`; `PrimaryFamilies.Length ∈ [1..2]`; `Σ StarterParts.GrantedCards.Count ≤ 4`.
- **R12** Stat modifier composition must run in < 0.1 ms for typical 4-part vehicle with up to 4 modifiers per part.
- **R13** POCO must be trivially serializable by ADR-0004 save orchestrator — no engine types, no cycles, no delegates held as state. SO references in DTOs project to `string PartId` keys, not object references.
- **R14** MVP includes only Scout + Assault chassis. Heavy Truck is authored post-EA by adding a third `ChassisDefinitionSO` + 5 parts + 10 cards, with **zero code changes** required.

## Decision

The vehicle data model lives in a new engine-free assembly `WastelandRun.Vehicle` that the existing `WastelandRun.Combat` assembly references. Three POCO types (`Vehicle`, `SlotState`, `StatModifier`), two ScriptableObject authoring types (`ChassisDefinitionSO`, `PartDefinitionSO`), two stable interfaces (`IVehicleView`, `IVehicleMutator`), and one catalog interface (`IPartCatalog`) define the full surface. Stat composition is a single static function; install/scrap/damage semantics live on a single mutator implementation.

### Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Combat.asmdef                                         │
│    (references WastelandRun.Vehicle; noEngineReferences: true)      │
│                                                                     │
│  CombatLoop ─── IVehicleView ───▶ reads slot/armor state            │
│             ─── IVehicleMutator ─▶ ApplyDamage / AddPlating /       │
│                                   AddArmor / ApplyStatus / Repair   │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ references
                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Vehicle.asmdef   ("noEngineReferences": true)         │
│                                                                     │
│  ┌───────────┐     ┌───────────┐     ┌────────────────┐             │
│  │  Vehicle  │────▶│ SlotState │◀───▶│ StatModifier   │             │
│  │  (POCO,   │     │ (Hp,Plate,│     │ (Add/Mult/Ovr) │             │
│  │  Armor,   │     │  Status,  │     └────────────────┘             │
│  │  Statuses)│     │  PartId)  │                                    │
│  └───────────┘     └───────────┘                                    │
│         ▲                  ▲                                        │
│         │                  │                                        │
│  ┌──────┴───────┐   ┌──────┴──────────┐   ┌──────────────────┐      │
│  │ IVehicleView │   │ IVehicleMutator │   │ IPartCatalog     │      │
│  │ (read)       │   │ (whitelisted)   │   │ (stateless query)│      │
│  └──────────────┘   └─────────────────┘   └────────┬─────────┘      │
│                                                    │                │
│  StatComposer.Compose(base, modifiers) ──▶ float   │                │
└────────────────────────────────────────────────────┼────────────────┘
                               ▲                    │
                               │ references         │ Addressables label
                               │ (MonoBehaviour     │ "part-definitions"
                               │  authoring layer)  ▼
┌─────────────────────────────────────────────────────────────────────┐
│  WastelandRun.Gameplay.asmdef  (+ UnityEngine)                      │
│                                                                     │
│  ChassisDefinitionSO : ScriptableObject  ── authoring asset         │
│  PartDefinitionSO    : ScriptableObject  ── authoring asset         │
│  AddressablesPartCatalog : IPartCatalog  ── runtime lookup impl     │
│  VehicleFactory.FromChassis(ChassisDefinitionSO) → Vehicle          │
└─────────────────────────────────────────────────────────────────────┘
```

One-way dependency: `Combat → Vehicle ← Gameplay`. The POCO has zero knowledge of `ScriptableObject`; the authoring layer projects SO data into POCO-native `PartData` / `ChassisData` records at load time.

### Key Interfaces

```csharp
// === WastelandRun.Vehicle assembly (noEngineReferences: true) ===

namespace WastelandRun.Vehicle
{
    public enum SlotType      { Weapon, Engine, Mobility, Frame }
    public enum DamageState   { Empty, Functional, Degraded, Offline }   // V&P GDD R3
    public enum DamageSource  { Card, Status, Environment }              // V&P GDD R9 / F-VP2 DOT bypass
    public enum StatOperation { Add, Multiply, Override }
    public enum PartRarity    { Common, Uncommon, Rare, Legendary }
    public enum ChassisType   { Scout, Assault, HeavyTruck }

    // StatType aggregates stat identifiers referenced by downstream systems.
    // V&P GDD (R8 / line 166) defers "which stats exist" to consumers: Card Combat owns
    // Damage/Energy/Hand-size stats; Scrap Economy owns Price stats. This ADR is the
    // canonical location for the enum so a single type name is shared across assemblies.
    // Extending the enum requires an ADR amendment (ADR-0006 / ADR-0008 will register
    // the authoritative stats for their systems and extend this enum through amendment).
    public enum StatType
    {
        // Card Combat (ADR-0002 / card-combat-system.md)
        MaxEnergyBase,        // base energy per turn
        MaxHandSize,          // cap on held cards
        CardBaseDamageOut,    // multiplier on outgoing card damage
        CardBaseRepair,       // multiplier on repair effects
        DrawPerTurn,          // card draws per turn

        // Scrap Economy (scrap-economy.md — pending ADR-0008)
        ShopPriceMultiplier,  // buy price modifier at merchants / chopshop
        ScrapYieldMultiplier, // sell price modifier when scrapping

        // Vehicle-derived (informational; not a direct stat modifier target)
        ArmorMax              // exposed read-only through IVehicleView.MaxArmor — not directly modifier-targeted
    }

    // --- POCO state ---
    public sealed class Vehicle
    {
        public ChassisType Chassis { get; }
        public int MaxArmor { get; private set; }        // derived, cached
        public int CurrentArmor { get; private set; }
        public IReadOnlyDictionary<SlotType, SlotState> Slots { get; }
        public IReadOnlyList<StatusInstance> ActiveStatuses { get; }
        internal Vehicle(ChassisType chassis, IDictionary<SlotType, SlotState> slots);
    }

    public sealed class SlotState
    {
        public SlotType Slot { get; }
        public string PartId { get; private set; }       // null == Empty
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }           // from chassis + part
        public int PlatingStacks { get; private set; }
        public int MaxPlating { get; private set; }
        public DamageState DamageState { get; }          // derived from Hp / DegradedThreshold
    }

    public readonly record struct StatModifier(
        StatType TargetStat, StatOperation Operation, float Value);

    // --- Contracts ---
    public interface IVehicleView
    {
        ChassisType Chassis { get; }
        int MaxArmor { get; }
        int CurrentArmor { get; }
        bool IsDead { get; }                             // Frame.DamageState == Offline (V&P GDD: no separate Hull pool)

        // Slot-level reads
        DamageState GetDamageState(SlotType slot);       // V&P GDD R3 — derived from Hp
        string GetPartId(SlotType slot);                 // null when DamageState == Empty
        int GetSlotHp(SlotType slot);
        int GetSlotMaxHp(SlotType slot);
        int GetPlating(SlotType slot);

        // Status reads (vehicle-level + per-slot). ActiveStatuses is vehicle-scoped
        // per V&P GDD line 35; per-slot statuses (e.g., Ignite on Weapon) live on
        // SlotState and are surfaced via GetSlotStatuses. Final StatusInstance shape
        // is owned by Status Effects (ADR-0007); V&P only stores the list.
        IReadOnlyList<StatusInstance> GetStatuses();                       // vehicle-level
        IReadOnlyList<StatusInstance> GetSlotStatuses(SlotType slot);      // slot-scoped

        // Stat composition (V&P GDD R8 / line 187 — canonical method name)
        float GetStatModifier(StatType stat);            // returns composed (base + Add) * Multiply, or Override if present; base is consumer-defined

        // Events (fire order specified in Implementation Guidelines — see section below)
        event Action<SlotType, DamageState, DamageState> OnSlotDamageStateChanged;  // (slot, from, to) — V&P GDD line 189
        event Action<SlotType, string /*partId*/> OnPartInstalled;
        event Action<SlotType, string /*partId*/> OnPartRemoved;
        event Action<int> OnMaxArmorChanged;
        event Action<int> OnCurrentArmorChanged;
        event Action<SlotType, StatusType> OnStatusApplied;                // V&P GDD line 192
        event Action<SlotType, StatusType> OnStatusExpired;                // V&P GDD line 193
    }

    public interface IVehicleMutator
    {
        // Whitelisted callers per docs/registry/architecture.yaml :
        // - Combat         : ApplyDamage, AddPlating, AddArmor, ApplyStatus, Repair, TickStatuses (end-of-turn)
        // - Status Effects : ApplyDamage (DOT), ApplyStatus, RemoveStatus, TickStatuses
        // - Economy        : InstallPart, RemovePart, Repair
        // - Loot           : InstallPart
        // - Enemy AI       : ApplyStatus (via its own IVehicleMutator reference to the target vehicle)
        //
        // Single-threaded by construction: combat runs on a single thread per ADR-0002;
        // all mutation calls are synchronous on the calling thread. No locks required.

        // Damage pipeline
        void ApplyDamage(SlotType slot, int amount, DamageSource source);  // F-VP2: plating → armor (Frame, skipped if source==Status) → hp
        void Repair(SlotType slot, int hpRestored, bool canReviveOffline);
        void AddPlating(SlotType slot, int stacks);      // non-Frame only
        void AddArmor(int amount);                       // clamps at MaxArmor

        // Status pipeline (interface extension expected by ADR-0007; StatusInstance shape TBD there)
        void ApplyStatus(StatusType type, int duration, int stacks, SlotType? targetSlot);  // V&P GDD R9 line 206
        void RemoveStatus(StatusType type, SlotType? targetSlot);                            // needed by Status Effects for removal on Offline transition
        void TickStatuses();                                                                  // end-of-turn bookkeeping; DOT damage internally routes through ApplyDamage with DamageSource.Status

        // Part lifecycle
        void InstallPart(SlotType slot, IPartData part); // R5: auto-scraps existing, validates, resets, recalculates MaxArmor, fires events
        void RemovePart(SlotType slot);                  // R6: permanent this run, removes granted cards from all zones
    }

    // --- Data projection (POCO-native, not SO-typed) ---
    public interface IPartData
    {
        string PartId { get; }
        SlotType SlotType { get; }
        IReadOnlyList<ChassisType> CompatibleChassis { get; }
        PartRarity Rarity { get; }
        IReadOnlyList<string> GrantedCardIds { get; }
        IReadOnlyList<StatModifier> StatModifiers { get; }
        int MaxPlating { get; }
        int ArmorContribution { get; }
    }

    public interface IChassisData
    {
        ChassisType Chassis { get; }
        IReadOnlyDictionary<SlotType, int> SlotMaxHp { get; }
        float DegradedThresholdPct { get; }              // 0..1
        IReadOnlyDictionary<SlotType, IPartData> StarterParts { get; }
        IReadOnlyList<string> StarterDeckCardIds { get; }
        int MaxEnergyBase { get; }
        int MaxHandSizeBase { get; }
    }

    // --- Catalog ---
    public interface IPartCatalog
    {
        IReadOnlyList<IPartData> GetParts(
            SlotType slot, PartRarity rarity, ChassisType chassis);
        IPartData GetById(string partId);
        VehicleStatePreview PreviewInstall(
            IVehicleView vehicle, SlotType slot, IPartData candidate);
    }

    // --- Composition function (static, pure) ---
    public static class StatComposer
    {
        public static float Compose(float baseValue, IEnumerable<StatModifier> mods);
        //   add  = Σ mod.Value  where Op == Add
        //   mult = Π mod.Value  where Op == Multiply
        //   ovr  = first mod.Value where Op == Override (throws if >1 Override)
        //   return ovr ?? (baseValue + add) * mult;
    }

    // --- Factory ---
    public static class VehicleFactory
    {
        public static Vehicle FromChassis(
            IChassisData chassis, IPartCatalog catalog);
        // Creates Vehicle with chassis.StarterParts pre-installed,
        // Hp = MaxHp for each, PlatingStacks = 0,
        // MaxArmor = Σ ArmorContribution, CurrentArmor = MaxArmor.
    }

    // --- Save DTO (ADR-0004 distributed schema registry) ---
    //
    // Vehicle is its own persisted system. Serializes to/from the save orchestrator
    // via this DTO. PartIds are serialized as strings, NOT SO references — ADR-0004
    // R6 boundary. CurrentArmor is NOT serialized (V&P GDD line 34: resets per combat);
    // ActiveStatuses *are* serialized (Status Effects persist across turn boundaries but
    // not across runs — Combat is scoped to a single node encounter).
    public sealed record VehicleStateDTO(
        string Chassis,                                  // ChassisType.ToString() for forward-compat
        IReadOnlyDictionary<string /*SlotType*/, SlotStateDTO> Slots,
        int MaxArmor                                     // cached; recomputable from Slots on load
    )
    {
        public const string SystemId      = "vehicle-state";   // ADR-0004: unique across project, CI-enforced
        public const int    SchemaVersion = 1;                 // ADR-0004: bump on breaking shape change
    }

    public sealed record SlotStateDTO(
        string  Slot,           // SlotType.ToString()
        string  PartId,         // null when Empty
        int     Hp,
        int     MaxHp,
        int     PlatingStacks,
        int     MaxPlating
    );
}

// === WastelandRun.Gameplay assembly (+ UnityEngine) ===

namespace WastelandRun.Gameplay.Vehicle
{
    [CreateAssetMenu(...)]
    public sealed class PartDefinitionSO : ScriptableObject, IPartData
    {
        [field: SerializeField] public string PartId { get; private set; }
        [field: SerializeField] public SlotType SlotType { get; private set; }
        [field: SerializeField] public ChassisType[] CompatibleChassisArray { get; private set; }
        [field: SerializeField] public PartRarity Rarity { get; private set; }
        [field: SerializeField] public string[] GrantedCardIdsArray { get; private set; }
        [field: SerializeField] public StatModifier[] StatModifiersArray { get; private set; }
        [field: SerializeField] public int MaxPlating { get; private set; }
        [field: SerializeField] public int ArmorContribution { get; private set; }
        // IPartData interface reads map array fields to IReadOnlyList<T>
        // OnValidate() runs SO import validators R11 (throws on Editor-side misauthoring)
    }

    [CreateAssetMenu(...)]
    public sealed class ChassisDefinitionSO : ScriptableObject, IChassisData { /* similar */ }

    public sealed class AddressablesPartCatalog : IPartCatalog
    {
        // Loads all PartDefinitionSO at scene init via Addressables label "part-definitions"
        // Caches in Dictionary<string, IPartData> by PartId
        // GetParts filters by (slot, rarity, chassis) — O(n) scan, n ≤ 100 expected
    }
}
```

### Implementation Guidelines

**Where code lives**
- POCO + interfaces + `StatComposer` + `VehicleFactory` → `Assets/Scripts/Vehicle/` in a new `WastelandRun.Vehicle.asmdef` with `noEngineReferences: true`.
- `PartDefinitionSO`, `ChassisDefinitionSO`, `AddressablesPartCatalog` → `Assets/Scripts/Gameplay/Vehicle/` in `WastelandRun.Gameplay.asmdef` (already exists; references `WastelandRun.Vehicle`).
- Existing ad-hoc `Vehicle.cs` in `Assets/Scripts/Combat` is migrated to the new assembly; `WastelandRun.Combat.asmdef` adds a reference to `WastelandRun.Vehicle`.

**Interface exposure**
- The concrete `Vehicle` class implements both `IVehicleView` and `IVehicleMutator`. Downstream code always depends on the interface, never on the concrete type. Casting `IVehicleView` to `IVehicleMutator` is forbidden by code review (use an explicit dependency injection of `IVehicleMutator` where mutation is allowed).

**Stat composition caching**
- `Vehicle.MaxArmor` is cached and invalidated on `InstallPart`/`RemovePart`/slot `Offline` transition only. Other stats are composed on-read via `GetComposedStat(stat, base)` — not cached, since reads are infrequent (combat-turn boundaries only).

**Damage pipeline (F-VP2)**
- `ApplyDamage(slot, amount, source)` logic:
  1. `plating_consumed = min(amount, slot.PlatingStacks)`; decrement plating.
  2. `after_plating = amount - plating_consumed`.
  3. If `slot == Frame`: subtract from `CurrentArmor` first (skipped if `source == DamageSource.Status`); remainder → `Hp`.
  4. Else: `after_plating` → `Hp` directly.
  5. Clamp `Hp ≥ 0`; transition check → fire `OnSlotDamageStateChanged` if state crossed a threshold.
  6. If crossed to `Offline`: remove granted cards from all zones; if Frame → mark IsDead.

**Install/scrap (R6/R7)**
- Always validate `CompatibleChassis.Contains(this.Chassis)` and `part.SlotType == slot` before any mutation; throw `PartIncompatibleException` on mismatch (exception-based validation matches ADR-0002 pattern).
- Granted cards are tracked by `(PartId, CardId, InstanceGuidFromPartLoad)` so scrap removes the exact instances added by that part (EC-VP11) — deck identity lives in the card model (ADR-0006 scope).

**Events — fire order**
- All events use `event Action<...>` per tech-prefs (forbidden: `UnityEvent`).
- Event dispatch is synchronous on the mutating thread. Combat runs single-threaded by ADR-0002 — no locks, no cross-thread dispatch.
- **Fire order on `InstallPart` (when an existing part was scrapped)**:
  1. `OnPartRemoved(slot, oldPartId)` — granted cards already cleared from deck zones
  2. `OnSlotDamageStateChanged(slot, oldState, Empty)` if the old part wasn't already at Hp 0
  3. `OnPartInstalled(slot, newPartId)` — new granted cards already added to deck pile
  4. `OnSlotDamageStateChanged(slot, Empty, Functional)` — Hp reset to MaxHp
  5. `OnMaxArmorChanged(newMaxArmor)` if `Σ ArmorContribution` changed
  6. `OnCurrentArmorChanged(newCurrentArmor)` if clamp adjusted CurrentArmor
- **Fire order on damage that crosses a state threshold**:
  1. `OnSlotDamageStateChanged(slot, oldState, newState)` — single fire even if multiple thresholds crossed in one hit (e.g., Functional → Offline skips Degraded; the event reports the final state, not intermediate)
  2. If `newState == Offline` and the part contributed ArmorContribution: `OnMaxArmorChanged(newMaxArmor)` then `OnCurrentArmorChanged(newClamped)` if applicable
  3. If `slot == Frame` and `newState == Offline`: vehicle dies (`IsDead == true`); no further events fire — combat resolution is owned by Combat (ADR-0002) and reads `IsDead`
- HUD subscribers may rely on this ordering. New mutators must preserve it.

**SO import validators (R11) — gate location**
- Implemented as `OnValidate()` (editor-side, fires on every inspector edit) + a custom `EditorBuildPreprocessor` (build-side, fails the build on invalid SO). Runtime code does not re-validate.
- **Important**: `OnValidate` is stripped from runtime builds and does not fire for SOs loaded via Addressables in standalone. The `EditorBuildPreprocessor` is the sole authoritative gate. Pipelines that import SOs without going through Unity's editor (e.g., a future asset-bake CLI tool) must run the validator suite explicitly — flagged in Risks below.

**Exception locations**
- `MultipleOverrideException` — *editor-time* only, thrown by the SO import validator when two `StatModifier` entries with `Operation == Override` target the same `StatType`. Never thrown at runtime; runtime trusts the import gate.
- `PartIncompatibleException` — *runtime*, thrown by `IVehicleMutator.InstallPart` when `part.CompatibleChassis` does not contain the vehicle's chassis OR `part.SlotType != slot`. Caller-recoverable (Loot / Economy decide whether to discard the drop or surface a UI error).
- `MultipleOverrideException` is also re-thrown at runtime if `IPartCatalog.GetById` returns a part that fails composition — this is a defense-in-depth check; under normal operation the editor gate catches it first.

**`IReadOnlyList<T>` interface contract on SOs**
- Every SO interface property typed `IReadOnlyList<T>` (e.g., `IPartData.GrantedCardIds`) is backed by a private `[field: SerializeField] private T[] _xxxArray` field. Unity's serializer handles `T[]` natively; the interface getter returns the array (which implements `IReadOnlyList<T>` since .NET Standard 2.1 / C# 8). **Do not** use `[field: SerializeField] public List<T> XxxList` — `List<T>` allocates extra heap and obscures the engine-free interface contract.

**Threading and dispatch**
- `Vehicle` instances are owned by a single `CombatLoop` (ADR-0002) which is single-threaded; no field is accessed by any other thread at runtime.
- Background save (`Task.Run` per ADR-0004) reads via the `VehicleStateDTO` projection; the DTO is built synchronously on the combat thread before the background write runs, so no Vehicle field is read off the combat thread.

**MVP scope (R14)**
- Only Scout and Assault `ChassisDefinitionSO` ship. HeavyTruck is an enum value reserved in code but has no SO; `IPartCatalog.GetParts(..., HeavyTruck)` returns empty list (no assertion failure).

## Alternatives Considered

### Alternative A: Typed POCO + SO-backed stateless catalog (CHOSEN)

- **Description**: Vehicle is a plain C# class with typed `Dictionary<SlotType, SlotState>`; stat modifiers are `readonly record struct`; `IPartCatalog` is a stateless interface implemented against Addressables-loaded SO assets. `IVehicleView`/`IVehicleMutator` split enforced by interface-only downstream dependencies.
- **Pros**: Matches V&P GDD conventions exactly (no GDD rewrite needed); reuses ADR-0002 POCO-residency pattern; SO authoring leverages Unity's inspector + Addressables without polluting POCO; stat composition is a single pure function; trivially serializable by ADR-0004.
- **Cons**: Two parallel data types (SO for authoring, POCO projection for runtime) — requires a mapper; developer must remember to read through the interface, not the concrete class.
- **Estimated Effort**: 3–5 days (POCO + interfaces ~1d; SO types + OnValidate ~1d; AddressablesPartCatalog + catalog tests ~1d; stat composer + tests ~0.5d; VehicleFactory + existing `Vehicle.cs` migration ~1d; code-review of write-whitelist ~0.5d).
- **Rejection Reason**: N/A — chosen.

### Alternative B: Component-based ECS-lite (IDamageable / IModifiable per slot)

- **Description**: Each slot is an object that implements `IDamageable`, `IModifiable`, `IStatusTarget` interfaces; the Vehicle is a thin container; damage/status/mutation flow through interface dispatch. Parts are attached as `IStatModifierProvider` components.
- **Pros**: Flexibility for future slot-specific behaviors (e.g., unique slot types post-EA); easier to unit-test individual concerns in isolation.
- **Cons**: Five to seven additional interfaces for the same surface; interface-dispatch overhead on hot paths (damage resolution); doesn't match V&P GDD mental model (forces GDD rewrite); over-designs for MVP (4 slot types, no planned slot variants).
- **Estimated Effort**: 6–8 days.
- **Rejection Reason**: Over-design for a 4-slot, 3-chassis MVP. The GDD's natural shape is a typed dictionary, not a component graph. Adds interface churn without a payoff — EA doesn't ship any slot-specific behavior the uniform slot model can't handle.

### Alternative C: Single mutable `Vehicle` class, no View/Mutator split

- **Description**: One `Vehicle` class with all public methods; downstream systems hold a direct reference and call whatever they need.
- **Pros**: Simpler; zero interface indirection; one fewer type to maintain.
- **Cons**: Violates ADR-0001 interface contract (`IVehicleView.GetDamageState` is locked); no enforcement of the write-whitelist (any caller could call `InstallPart`); undermines the visual-layer read-only guarantee; breaks the existing `OnSlotDamageStateChanged` event subscription surface that ADR-0001 already assumes.
- **Estimated Effort**: 2 days (smaller).
- **Rejection Reason**: Contradicts ADR-0001 directly. Non-starter.

### Alternative D: Hybrid — POCO-with-component-slots

- **Description**: Vehicle is a POCO but slots are a `SlotState[]` with slot-type-specific subclasses (`WeaponSlotState : SlotState`, `FrameSlotState : SlotState`, etc.) to carry slot-specific logic.
- **Pros**: Slot-specific behavior (e.g., Frame's armor interaction) can live on the subclass; opens future per-slot behavior expansion.
- **Cons**: Frame-specific logic is only *one* special case (vehicle-level Armor interaction); subclassing adds five types for one divergence; doesn't match the GDD's uniform slot model; creates save/load churn (DTO-per-subclass or discriminated union).
- **Estimated Effort**: 4–5 days.
- **Rejection Reason**: One special case (Frame↔Armor) does not justify five slot subclasses. The damage pipeline branches on `slot == Frame` in a single `if` — far simpler than a type hierarchy.

## Consequences

### Positive

- Eight downstream ADRs (Status Effects, Scrap Economy, Node Map, Node Encounter, Enemy Brain, Loot & Reward + retrofits to Combat and Save) now have a stable vehicle contract to build on.
- `IVehicleView` / `IVehicleMutator` split makes code review mechanically enforceable: any `IVehicleMutator.*` call-site outside the whitelisted callers is a blocking comment.
- POCO residency keeps combat/vehicle/status logic testable in NUnit EditMode without play-mode bootstrap, preserving ADR-0002's <5-second test-suite target.
- MVP scope (2 chassis, ~20 parts) requires no code changes to add Heavy Truck post-EA — pure SO authoring.
- ADR-0004 save orchestrator can serialize `Vehicle` via the POCO projection without special-casing engine types.

### Negative

- Two parallel data types (SO for authoring, POCO `IPartData`/`IChassisData` for runtime) require a one-time projection map. Developers adding a new field must update both the SO and the interface.
- The write-whitelist (`IVehicleMutator`) is enforced by convention + code review, not by language. A careless call from an unvetted caller compiles clean — the safety depends on the review gate.
- Introducing a new assembly (`WastelandRun.Vehicle`) splits the existing `WastelandRun.Combat` — one extra `.asmdef`, one extra assembly reference, minor compile-time impact.

### Neutral

- Moves the existing ad-hoc `Vehicle.cs` from `WastelandRun.Combat` to `WastelandRun.Vehicle`. One-time migration; no runtime behavior change for callers that already go through properties matching the new interface.
- `StatType` enum is centralized here. Other systems that refer to stats (Card Combat, Enemy) must import it from the Vehicle assembly rather than defining parallel enums — acceptable tradeoff for a single source of truth.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Developer calls `IVehicleMutator` from an unvetted caller | MEDIUM | HIGH (silently breaks invariants) | Code review gate on every `.InstallPart`/`.RemovePart`/`.ApplyDamage` call-site; architecture registry lists the whitelisted callers; CI grep job flags new usages in non-whitelisted paths (follow-up). |
| SO import validators pass in editor but break at runtime after asset edits | LOW | MEDIUM | `OnValidate()` runs on every inspector edit; `EditorBuildPreprocessor` re-runs full validator suite on every build; smoke-check skill reads catalog at runtime to confirm parity. |
| Heavy Truck post-EA authoring requires hidden code changes | LOW | MEDIUM | MVP build includes a "HeavyTruck stub" test case that loads a HeavyTruck ChassisDefinitionSO (stub, 1 part per slot) and confirms pure-data extensibility before EA ships. |
| Stat composition pipeline (Add → Multiply → Override) produces surprising results for designers | MEDIUM | LOW | `StatComposer` has golden-value unit tests covering the GDD example (base 3, +1 Add, ×1.25 = 5.0) plus edge cases (Override wins, zero-mod no-op, negative Add). Published in tuning docs. |
| Granted-card scrap loses track of instances on mid-combat part replacement | MEDIUM | HIGH | Track granted cards by `(PartId, CardId, InstanceId)` tuple per EC-VP11; unit-test replacement + scrap + re-install sequence; ADR-0006 (Card System Data) refines the card instance identity contract. |
| Derived `MaxArmor` cache drifts from sum-of-parts after a sequence of install/scrap ops | LOW | HIGH | `RecalculateMaxArmor` runs on every install, scrap, and slot `→ Offline` transition (not just install); unit test applies 50 random install/damage/scrap sequences and asserts `MaxArmor == Σ ArmorContribution` at each step. |
| **IL2CPP strips `PartDefinitionSO` / `ChassisDefinitionSO` types** because runtime references go through `IPartData`/`IChassisData` interfaces only — the concrete SO type appears unreferenced, the linker drops it, Addressables fails to instantiate at runtime in standalone builds | MEDIUM | HIGH (catalog returns empty in standalone; never reproduces in editor) | Add explicit `link.xml` entries preserving `WastelandRun.Gameplay.Vehicle.PartDefinitionSO` and `WastelandRun.Gameplay.Vehicle.ChassisDefinitionSO` (full type, including private fields used by Unity's serializer). Migration step 5 includes the `link.xml` edit. CI build step runs an IL2CPP standalone build smoke test that loads one part SO via Addressables and asserts non-null. |
| **`Addressables.LoadAssetsAsync` throws on label miss in Unity 6.2+** (changed from null-return in Unity 2022) — `AddressablesPartCatalog` constructor would crash the scene if the `part-definitions` label has no entries (e.g., empty group, content-update mismatch) | MEDIUM | HIGH (scene fails to init; no recovery path) | Wrap catalog init in try/catch with `handle.IsValid()` final check (per registered API decision `vehicle_part_asset_loading`). On failure: in dev builds, log + boot a hand-authored fallback catalog so playtests proceed; in shipped builds, surface a load-error scene per ADR-0004 recovery contract. Validation criteria includes a "missing label" smoke test. |
| **Addressables group `part-definitions` ships without `Include in Build` enabled** in the build profile — content catalog at runtime is empty even though editor sees all SOs | LOW | HIGH (silent failure; only catches in standalone) | Build profile validation: `EditorBuildPreprocessor` reads the Addressables settings, verifies the `part-definitions` group has `Include In Build == true`, and fails the build with a clear error otherwise. Documented in tech-prefs Addressables checklist. |
| **`OnValidate` does not fire at runtime** — SOs loaded via Addressables in standalone bypass editor validation entirely; a corrupt-but-shipped SO produces undefined behavior | LOW | MEDIUM | `EditorBuildPreprocessor` runs the full R11 validator suite at build-time on every SO in the `part-definitions` group (already gated). Future asset-bake pipelines (CLI tools that import SOs without going through Unity's editor) must explicitly invoke the validator suite — flagged in implementation guidelines. Runtime trusts the build gate; no runtime re-validation. |
| **Domain reload invalidates cached Addressables handles** during editor play-mode iteration — `AddressablesPartCatalog` instance from a previous play session holds stale `AsyncOperationHandle` references, throwing on next access | LOW | LOW (editor-only; never hits shipped build) | Catalog is owned by combat scene initialization, not a static singleton. Domain reload tears down the scene; new combat scene constructs a fresh catalog. Documented in tech-prefs as a known editor-iteration gotcha. |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU: stat composition per read | N/A (no composer) | <0.02 ms (4 parts × 4 mods) | 0.1 ms |
| CPU: InstallPart (including MaxArmor recalc + card-pile update) | N/A | <0.5 ms | 1 ms |
| CPU: ApplyDamage (hot path, called once per turn per slot) | ~0.01 ms (current Vehicle) | <0.02 ms | 0.1 ms |
| Memory: Vehicle instance | ~0.5 KB current | ~0.8 KB (added IPartData refs + composed-stat cache headers) | 2 KB |
| Memory: IPartCatalog Addressables load (100 PartDefinitionSO) | N/A | ~200 KB loaded once at scene init | 1 MB |
| Load time: Catalog build at scene init | N/A | <100 ms (100 SOs via Addressables label) | 500 ms |

No network implications (single-player).

## Migration Plan

1. **Create `WastelandRun.Vehicle.asmdef`** with `noEngineReferences: true`. Add enum types (`SlotType`, `DamageState`, `StatOperation`, `PartRarity`, `ChassisType`, `StatType`) + interfaces (`IPartData`, `IChassisData`, `IVehicleView`, `IVehicleMutator`, `IPartCatalog`) + `StatModifier` struct + `StatComposer`. No behavior yet — contracts only. *Verify*: compile passes with zero `UnityEngine.*` imports (CI grep).
2. **Add `WastelandRun.Combat.asmdef → WastelandRun.Vehicle` reference**. *Verify*: existing 136 Combat EditMode tests still pass.
3. **Migrate ad-hoc `Vehicle.cs` to the new assembly** as the concrete class implementing `IVehicleView + IVehicleMutator`. Preserve existing field semantics; rename methods where needed to match the new interface. *Verify*: existing 136 Combat tests pass unchanged (rename-only refactor).
4. **Add `VehicleFactory.FromChassis` stub** that reads from a hand-authored `IChassisData` fixture (no SO yet). *Verify*: new unit test constructs a Scout Vehicle and asserts slot composition matches the fixture.
5. **Author `PartDefinitionSO` + `ChassisDefinitionSO`** in `WastelandRun.Gameplay.asmdef` with `[CreateAssetMenu]`. Implement `IPartData` / `IChassisData` via array-to-list projection. Add `OnValidate()` running R11 validators. **Add `link.xml` entries** preserving `WastelandRun.Gameplay.Vehicle.PartDefinitionSO` and `WastelandRun.Gameplay.Vehicle.ChassisDefinitionSO` (full type + serialized fields) to prevent IL2CPP stripping when only the interfaces are referenced at runtime. *Verify*: an editor-side smoke test authors one invalid SO (e.g., `GrantedCards.Length == 0` on Common) and confirms the validator rejects it; a standalone IL2CPP build smoke test loads one SO via Addressables and asserts non-null instance.
6. **Implement `AddressablesPartCatalog`** loading label `part-definitions`. Cache results by `PartId`. Wrap initialization in try/catch with `handle.IsValid()` final check per the registered `vehicle_part_asset_loading` API decision. Verify build profile has the `part-definitions` group set to `Include In Build` (gated by `EditorBuildPreprocessor`). *Verify*: runtime catalog round-trip test loads all authored parts and asserts `GetParts(Weapon, Common, Scout)` returns non-empty; "missing label" smoke test confirms graceful failure path on empty group.
7. **Author Scout + Assault `ChassisDefinitionSO` + starter parts** (4 parts × 2 chassis + 4 starter cards per chassis). Author ~12 additional part SOs covering Common/Uncommon/Rare/Legendary at each slot. *Verify*: manual Unity editor smoke test creates a Scout vehicle and plays one combat encounter.
8. **Wire `IPartCatalog` into combat/loot flow** via constructor injection (Combat already uses this pattern per ADR-0002). *Verify*: a full loot-reward → install-part → combat-turn sequence in EditMode confirms the end-to-end path.
9. **Update `docs/registry/architecture.yaml`** with new stances registered in step 10 of this ADR's authoring flow. Update `docs/architecture/architecture-traceability.md` to mark V&P TRs covered.

**Rollback plan**: Revert commits in reverse step order. The existing ad-hoc `Vehicle.cs` can be restored from `WastelandRun.Combat` with one `git revert`. The new assembly is purely additive; removing the `Combat → Vehicle` asmdef reference reverts combat to pre-ADR behavior. No save data change yet (ADR-0004 save shape is unaffected by the assembly split).

## Validation Criteria

- [ ] New `WastelandRun.Vehicle.asmdef` compiles with `noEngineReferences: true`; CI grep finds zero `UnityEngine.` tokens in that assembly's sources.
- [ ] Existing 136 Combat EditMode tests pass unchanged after the migration refactor.
- [ ] Unit test: `StatComposer.Compose(3, [+1 Add, ×1.25 Multiply]) == 5.0` (GDD golden value AC-VP28).
- [ ] Unit test: two `Override` modifiers on the same stat throws `MultipleOverrideException` at SO import (AC-VP29).
- [ ] Unit test: 50 random install/damage/scrap sequences maintain `Vehicle.MaxArmor == Σ installed.ArmorContribution` invariant at each step.
- [ ] Unit test: `InstallPart` with incompatible chassis throws `PartIncompatibleException` without mutating state.
- [ ] Unit test: `InstallPart` on occupied slot auto-scraps, removes old cards from all deck zones, adds new cards, resets `Hp = MaxHp`, fires exactly one `OnPartRemoved` + one `OnPartInstalled`.
- [ ] Unit test: `ApplyDamage(Frame, 10, DamageSource.Status)` subtracts from Hp directly without touching `CurrentArmor` (DOT bypass, AC-VP40).
- [ ] Unit test: `ApplyDamage(Frame, 10, DamageSource.CardPlay)` consumes plating, then `CurrentArmor`, then Hp in order (F-VP2).
- [ ] Unit test: `RemovePart` is permanent — subsequent `InstallPart` on the same slot does not restore the scrapped part.
- [ ] Integration test: `VehicleFactory.FromChassis(ScoutSO, catalog)` produces a Vehicle with 4 installed starter parts, 10-card deck, `MaxArmor = Σ starter.ArmorContribution`, `CurrentArmor = MaxArmor`.
- [ ] Addressables label `part-definitions` resolves all authored parts at scene init in <100 ms.
- [ ] Editor smoke test: invalid `PartDefinitionSO` (empty `CompatibleChassis`) fails build via `EditorBuildPreprocessor`.
- [ ] `docs/registry/architecture.yaml` updated with new stances (state ownership, interfaces, forbidden patterns, API decisions) per step 6 of the authoring flow.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/vehicle-and-part-system.md` | V&P | TR-vehicle-001: Vehicle POCO shape (4 slots + derived Armor + statuses) | Defines `Vehicle` class with typed `Slots` dictionary, derived `MaxArmor`, `ActiveStatuses` list. |
| `design/gdd/vehicle-and-part-system.md` | V&P | TR-vehicle-006: PartDefinitionSO authoring contract | `PartDefinitionSO : ScriptableObject, IPartData` with all required fields + R11 OnValidate. |
| `design/gdd/vehicle-and-part-system.md` | V&P | TR-vehicle-016: Stat composition order (Add + Multiply + Override) | `StatComposer.Compose` implements `(base + Σ Add) × Π Multiply` with Override short-circuit. |
| `design/gdd/vehicle-and-part-system.md` | V&P | TR-vehicle-017: IVehicleView / IVehicleMutator contract | Two interfaces explicitly split reads from writes; whitelist enforced by code review. |
| `design/gdd/vehicle-and-part-system.md` | V&P | TR-vehicle-019: IPartCatalog lookup by (slot, rarity, chassis) | `IPartCatalog.GetParts` signature + Addressables-backed implementation. |
| `design/gdd/vehicle-and-part-system.md` | V&P | TR-vehicle-021: ChassisDefinitionSO with MVP 2+1 chassis scaling | `ChassisDefinitionSO` with `ChassisType` enum reserving HeavyTruck; no code changes required to add. |
| `design/gdd/vehicle-and-part-system.md` | V&P | TR-vehicle-022: Install/scrap invariants (R5, R6, R6b) | `InstallPart` auto-scraps + validates + resets + fires events; `RemovePart` removes cards + fires events. |
| `design/gdd/vehicle-and-part-system.md` | V&P | TR-vehicle-003: Damage state derivation + F-VP2 damage pipeline | `DamageState` is derived from `Hp`; `ApplyDamage` implements plating→armor→Hp order with Frame fork. |
| `design/gdd/card-combat-system.md` | Combat | Vehicle interface access for card effect resolution | Combat consumes `IVehicleView` / `IVehicleMutator`; no change to ADR-0002 shape. |
| `design/gdd/enemy-system.md` | Enemy | `SilhouetteClass` enum sharing across chassis/enemy | `ChassisDefinitionSO.SilhouetteClass` references shared enum (imported from Enemy system — ADR-0011 will finalize the enum location). |
| `design/gdd/loot-reward.md` | Loot | Part selection by slot/rarity/chassis | `IPartCatalog.GetParts` signature matches loot reward pipeline's needs; caller passes seeded RNG (ADR-0003) for the actual selection. |
| `design/gdd/scrap-economy.md` | Economy | Part install/remove/repair mutation path | `IVehicleMutator.InstallPart`/`RemovePart`/`Repair` are the authorized mutation APIs; ADR-0008 will wrap these in transaction semantics. |

## Related

- **ADR-0001** (Visual Vehicle Part System) — locks visual interface contract and write-whitelist registry; this ADR fills the data-layer half of that contract. *Flagged for GDD Sync update*: ADR-0001 says "5 visible slots"; this ADR + V&P GDD establish 4 data slots + vehicle-level Armor. Recommend amending ADR-0001's wording after ADR-0005 is Accepted.
- **ADR-0002** (Card Combat POCO) — same `noEngineReferences: true` residency pattern; Combat assembly references this ADR's new Vehicle assembly one-way.
- **ADR-0003** (Deterministic RNG) — `IPartCatalog.GetParts` is stateless; seeded selection stays in Loot/Map callers per the RNG discipline.
- **ADR-0004** (Save & Persistence) — Vehicle POCO is trivially serializable through the save orchestrator; `PartId` strings (not SO references) project cleanly into DTOs.
- **ADR-0006** (Card System Data Authoring — pending) — will refine card instance identity tracking referenced in R7 (`(PartId, CardId, InstanceId)`).
- **`design/gdd/vehicle-and-part-system.md`** — authoritative design (25 TRs); this ADR covers 8 TRs directly and enables the remaining 17 to be closed by ADR-0007 / ADR-0008 / ADR-0012.
