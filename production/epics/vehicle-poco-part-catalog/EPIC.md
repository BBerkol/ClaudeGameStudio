# Epic: Vehicle POCO + Part Catalog

> **Layer**: Foundation
> **GDD**: design/gdd/vehicle-and-part-architecture.md + design/gdd/vehicle-and-part-mechanics.md
> **Architecture Module**: `WastelandRun.Gameplay` — VehicleState POCO, SlotState, IVehicleView, IVehicleMutator, IPartCatalog, PartDefinitionSO, ChassisDefinitionSO, stat composition pipeline
> **Status**: Ready
> **Stories**: 7 stories created (2026-05-25)

## Stories

| # | Story | Type | Status | ADR | TRs Covered |
|---|-------|------|--------|-----|-------------|
| 001 | Assembly + Core Type Contracts | Logic | Ready | ADR-0005 + ADR-0007 | TR-004, TR-025 |
| 002 | Vehicle POCO + IVehicleView + IVehicleMutator | Logic | Ready | ADR-0005 + ADR-0007 | TR-001, TR-003, TR-013, TR-014 |
| 003 | Damage Pipeline F-VP2 + Reentrant ApplyDamage | Logic | Ready | ADR-0005 + ADR-0007 | TR-005, TR-015, TR-016, TR-017 |
| 004 | Part Install/Remove + Event Fire Order + Granted-Card Soft Disable | Logic | Ready | ADR-0005 + ADR-0007 | TR-008, TR-009, TR-010, TR-011, TR-012, TR-024 |
| 005 | IFuelContainer + IFuelMutator | Logic | Ready | ADR-0005 | TR-018, TR-019 |
| 006 | IPartCatalog + PreviewInstall + VehicleFactory | Logic | Ready | ADR-0005 + ADR-0007 | TR-020, TR-021 |
| 007 | PartDefinitionSO + ChassisDefinitionSO + FrameLayoutSO + Validators | Integration | Ready | ADR-0005 + ADR-0007 | TR-002, TR-006, TR-007, TR-022, TR-023 |

**Coverage**: All 25 TR-vehicle requirements (`TR-vehicle-001` through `TR-vehicle-025`).
**Decisions locked at story-creation**:
- OnSlotHpChanged event added to IVehicleView in Story 003
- SpendFuel overdraft clamps CurrentFuel to 0 (permissive contract)
- FuelMultiplier semantic tied to Node Map travel cost formula D-NM1
- **ADR-0007 amendments incorporated 2026-05-26 (re-author)**:
  - `SlotType` enum replaced by `SlotKind { Weapon, Engine, Mobility, Hull, Armor }`
  - Slots are now `IReadOnlyList<SlotInstance>` driven by per-chassis `IFrameLayout`
  - No vehicle-level Armor — Armor is per-slot via `SlotKind.Armor` with `RedirectsTo`
  - `IsDead = StructuralHp == 0` (sum of `IsStructural` slot HPs)
  - `DamageContext` is `readonly ref struct` (stack-only); `ApplyDamage` reentrant with `MAX_REENTRANCY = 8`
  - `OnCriticalStateChanged` at-most-once per top-level entry (Decision 15)
  - Decision 16: granted cards soft-disable on Offline, restored on Repair; hard removal only on Scrap or ExternalSourceEnded
  - New `FrameLayoutSO` ScriptableObject implements `IFrameLayout`; R11 ruleset enforced by `Validate()` + `OnValidate`

## Overview

Implements the shared Vehicle data model that every other system reads or mutates. The `Vehicle` plain C# POCO holds an `IFrameLayout` (per-chassis slot blueprint), an `IReadOnlyList<SlotInstance>` (the live runtime slots), a `FuelContainer`, and a `RngState`. Slots use the `SlotKind { Weapon, Engine, Mobility, Hull, Armor }` taxonomy (ADR-0007) — Hull slots are `IsStructural=true`, Armor slots redirect damage from non-Armor siblings via `RedirectsTo`. There is no vehicle-level Armor; `StructuralHp` is the sum of structural slot HPs and drives `IsDead`. Each `SlotInstance` carries `InstalledPart` (nullable), `CurrentHp`, `MaxHp` (declared in `SlotDefinition`), derived `DamageState` (Empty/Functional/Degraded/Offline), `PlatingStacks`, `CombatsSurvived`, and `RedirectsTo` (Armor only). The `IVehicleView` read-only interface is the exclusive external read surface; `IVehicleMutator` is a whitelisted-caller write interface (Card Combat for damage/repair, Scrap Economy for install/remove, Status Effects for apply). Damage flows through F-VP2 (Plating → Armor → Hp; DOT bypasses Armor) via a stack-allocated `DamageContext` (`readonly ref struct`) with bounded reentrancy. Granted cards soft-disable on slot Offline (Decision 16); hard removal only on scrap or external-source-end. Stat composition follows a fixed `(base + Add) × Multiply` pipeline with Override as a single-winner short-circuit, composed at the consuming call site. `IPartCatalog` provides a stateless `GetParts(slot, rarity)` lookup and `PreviewInstall()` returning a `VehicleStatePreview` without mutation. `IFuelContainer`/`IFuelMutator` manage the Fuel resource with overflow semantics. `ChassisDefinitionSO`, `PartDefinitionSO`, `FrameLayoutSO`, and `StatModifierSO` are the authoring SOs; `OnValidate` logic runs Editor-only (R11 ruleset). This epic delivers the single most load-bearing interface in the game — every combat, economy, map, and save system reads through it.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0005: Vehicle POCO Sub-Model + Part Catalog | VehicleState as plain C# POCO; IVehicleView/IVehicleMutator surface; IPartCatalog stateless lookup; `(base + Add) × Multiply` stat pipeline; Override short-circuit; IL2CPP `link.xml` for Newtonsoft interactions | HIGH |
| ADR-0007: Frame-Driven Variable Slot System | `SlotKind { Weapon, Engine, Mobility, Hull, Armor }`; per-chassis `FrameLayoutSO`; `SlotInstance` runtime records; per-slot Armor via `RedirectsTo`; stack-allocated `DamageContext`; reentrant `ApplyDamage` (MAX_REENTRANCY=8); Decision 12 (Repair fires OnSlotHpChanged); Decision 15 (OnCriticalStateChanged at-most-once); Decision 16 (granted-card soft-disable); R11 layout validator ruleset; `[field: SerializeField]` for SO auto-properties; runtime `LayoutNotValidatedThisSessionException` guard | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-vehicle-001 | Vehicle POCO is plain C# with no MonoBehaviour; contains Chassis enum, 4-slot Dict, MaxArmor, CurrentArmor, ActiveStatuses | ADR-0005 ✅ |
| TR-vehicle-002 | Slot taxonomy: SlotKind { Weapon, Engine, Mobility, Hull, Armor } per ADR-0007. Per-chassis FrameLayoutSO declares slot set — variable per chassis. Hull is structural (replaces Frame); Armor is per-slot via RedirectsTo (replaces vehicle-level CurrentArmor). | ADR-0005 + ADR-0007 ✅ |
| TR-vehicle-003 | SlotState contains InstalledPart (nullable), Hp, MaxHp, DamageState (derived), PlatingStacks, CombatsSurvived | ADR-0005 ✅ |
| TR-vehicle-004 | DamageState enum: Empty, Functional, Degraded (50% MaxHp threshold), Offline; derived from Hp not stored | ADR-0005 ✅ |
| TR-vehicle-005 | StructuralHp=0 → vehicle death (sum of all IsStructural slot HPs, typically Hull); non-structural slots Offline are salvageable via Repair with `canReviveOffline` flag | ADR-0005 + ADR-0007 ✅ |
| TR-vehicle-006 | `PartDefinitionSO` owns SlotType, CompatibleChassis allowlist, Rarity, GrantedCards[], StatModifiers[], MaxPlating, ArmorContribution | ADR-0005 ✅ |
| TR-vehicle-007 | GrantedCards count enforced by rarity: Common 1, Uncommon 2, Rare 3, Legendary 3 | ADR-0005 ✅ |
| TR-vehicle-008 | InstallPart resets HP to MaxHp, recalculates MaxArmor, adds GrantedCards to deck, fires OnPartInstalled | ADR-0005 ✅ |
| TR-vehicle-009 | RemovePart nulls InstalledPart, removes GrantedCards from all deck zones, recalculates MaxArmor, fires OnPartRemoved | ADR-0005 ✅ |
| TR-vehicle-010 | Armor is per-slot via SlotKind.Armor with RedirectsTo (replaces vehicle-level MaxArmor). Layout-declared Armor slots have their own MaxHp; no vehicle-level aggregate. | ADR-0005 + ADR-0007 ✅ |
| TR-vehicle-011 | Armor SlotInstance CurrentHp resets to MaxHp at combat start (ResetCombatEphemeral), persists across turns, NOT serialized between combats | ADR-0005 + ADR-0007 ✅ |
| TR-vehicle-012 | CombatsSurvived counter increments +1 per win with part installed; resets on install/scrap; consumed by Scrap Economy | ADR-0005 ✅ |
| TR-vehicle-013 | IVehicleView provides read-only Layout, Slots, GetSlot, GetSlotsByKind, StructuralHp, IsDead, Fuel, GetSlotDamageState, GetStatModifiers (no MaxArmor/CurrentArmor — per-slot now) | ADR-0005 + ADR-0007 ✅ |
| TR-vehicle-014 | IVehicleMutator exclusive callers: Card Combat (damage/repair), Scrap Economy (install/remove), Status Effects (apply) | ADR-0005 ✅ |
| TR-vehicle-015 | F-VP2 ordering: Plating → Armor (per-slot, redirected) → Hp. Plating consumed first for ALL damage; Armor walked via GetSlotsByKind(Armor) + RedirectsTo before applying to target slot Hp. | ADR-0007 ✅ |
| TR-vehicle-016 | DOT damage (DamageContext.BypassesArmor=true) bypasses Armor layer entirely; Corrode bonus added before Plating absorption | ADR-0007 ✅ |
| TR-vehicle-017 | State-crossing damage event fires one OnSlotDamageStateChanged for terminal state per slot; OnCriticalStateChanged at-most-once per top-level ApplyDamage entry (Decision 15) | ADR-0007 ✅ |
| TR-vehicle-018 | IFuelContainer exposes CurrentFuel, FuelCap, FuelMultiplier; IFuelMutator owns SpendFuel and TryGrantFuel | ADR-0005 ✅ |
| TR-vehicle-019 | TryGrantFuel silently drops overflow above FuelCap; returns actual-granted; no resource conversion | ADR-0005 ✅ |
| TR-vehicle-020 | IPartCatalog.GetParts() returns empty list (never null) when no assets match filter | ADR-0005 ✅ |
| TR-vehicle-021 | PreviewInstall() returns VehicleStatePreview without mutating; includes predicted MaxArmor and GrantedCards delta | ADR-0005 ✅ |
| TR-vehicle-022 | ChassisDefinitionSO owns Family, FrameLayoutSO (variable slots), DegradedThreshold%, CriticalThreshold%, StarterParts, StarterDeck, PrimaryFamilies | ADR-0005 + ADR-0007 ✅ |
| TR-vehicle-023 | Scout: 4 slots (Hull=16, no Armor), total HP 50 (glass-cannon); Assault: 5 slots (Hull=24 + Armor=8 RedirectsTo hull_main), total HP 58 (tank) | ADR-0007 ✅ |
| TR-vehicle-024 | Granted cards carry CardInstanceId + SourceSlotId; removal targets exact CardInstanceId matches across deck+hand+discard atomically. Decision 16: soft-disable on Offline (no removal), hard removal only on Scrap or ExternalSourceEnded. | ADR-0005 + ADR-0007 ✅ |
| TR-vehicle-025 | StatModifierSO contains TargetStat enum, Operation (Add/Multiply/Override), Value; consuming systems define vocabulary | ADR-0005 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/vehicle-and-part-architecture.md` and `design/gdd/vehicle-and-part-mechanics.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- `VehicleState` POCO compiles with no `UnityEngine.*` references (verified by CI assembly reference guard)
- `IVehicleView` / `IVehicleMutator` caller whitelist enforced: only Card Combat, Scrap Economy, Status Effects may call mutator methods
- `OnValidate` guarded with `[Editor only]` path; no runtime stripping in IL2CPP builds
- `PreviewInstall()` verified non-mutating via unit test (before/after state diff = zero)
- Stat composition pipeline unit-tested: Add, Multiply, Override operations, multi-modifier stacking, floor behavior
- `CombatsSurvived` increments and resets verified under install/scrap/win scenarios

## Next Step

Run `/create-stories vehicle-poco-part-catalog` to break this epic into implementable stories.
