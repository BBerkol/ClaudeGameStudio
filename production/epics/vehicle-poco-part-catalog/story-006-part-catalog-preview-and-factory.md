# Story 006: IPartCatalog + PreviewInstall + VehicleFactory

> **Epic**: Vehicle POCO + Part Catalog
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-25

## Context

**GDD**: `design/gdd/vehicle-and-part-architecture.md` + `design/gdd/vehicle-and-part-mechanics.md`
**Requirement**: `TR-vehicle-020`, `TR-vehicle-021`

**ADR Governing Implementation**: ADR-0005 (amended by ADR-0007 R6 Cluster B — Vehicle assembly stays engine-free; SO-reading factory lives in `WastelandRun.Gameplay`)
**ADR Decision Summary**: `IPartCatalog` is a stateless lookup: `IReadOnlyList<PartDefinition> GetParts(SlotKind kind, Rarity rarity)`. `PreviewInstall(IVehicleView vehicle, string slotId, PartDefinition def)` returns a `VehicleStatePreview` (engine-free value type in `WastelandRun.Vehicle`) WITHOUT mutating the vehicle. `VehicleFactory.Build(ChassisDefinition chassis, ulong rngSeed)` reads `IFrameLayout` from `chassis.Layout`, calls `layout.Validate()`, constructs `SlotInstance`s from `SlotDefinition`s, and returns a fresh `Vehicle`.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `VehicleFactory` lives in `WastelandRun.Gameplay` (the SO-aware assembly), but reads only the engine-free `IFrameLayout` interface — keeping `WastelandRun.Vehicle` itself engine-free. `IPartCatalog` implementation lives in `WastelandRun.Gameplay` and pulls from `PartDefinitionSO` assets (story 007).

**Control Manifest Rules (this layer)**:
- Required: `PreviewInstall` non-mutating; verified via before/after state diff = zero — source: ADR-0005
- Required: `GetParts` returns empty list (never null) — source: ADR-0005
- Required: `VehicleFactory` calls `IFrameLayout.Validate()` before constructing — source: ADR-0007
- Forbidden: `PreviewInstall` returning a `Vehicle` reference (must return a value-type preview snapshot) — source: ADR-0005

---

## Acceptance Criteria

- [ ] **AC-1** *(TR-020)*: `IPartCatalog.GetParts(SlotKind kind, Rarity rarity)` returns `IReadOnlyList<PartDefinition>`. Returns empty list (NOT null) when no assets match.
- [ ] **AC-2**: `IPartCatalog.GetParts(SlotKind kind)` overload returns all rarities. `GetPartById(PartId id)` returns the specific definition or throws `PartNotFoundException`.
- [ ] **AC-3** *(TR-021)*: `PreviewInstall(IVehicleView vehicle, string slotId, PartDefinition def)` returns a `VehicleStatePreview` with: predicted `SlotInstance` state for the affected slot, predicted GrantedCards delta (`IReadOnlyList<CardData> Added`), predicted `StatModifier` delta, predicted `StructuralHp`, and a `bool IsValidInstall` flag + `string? RejectionReason` when invalid.
- [ ] **AC-4**: `PreviewInstall` is non-mutating: a before/after state diff on the input `IVehicleView` shows zero changes after the call.
- [ ] **AC-5**: `VehicleStatePreview` is a `readonly record struct` (or sealed record) in `WastelandRun.Vehicle`. No engine references.
- [ ] **AC-6**: `VehicleFactory.Build(ChassisDefinition chassis, ulong rngSeed)` constructs a `Vehicle` by: (1) reading `chassis.Layout` (an `IFrameLayout`); (2) calling `Layout.Validate()`; (3) instantiating `SlotInstance` per `SlotDefinition` with `CurrentHp = MaxHp`, `InstalledPart = null`; (4) installing chassis starter parts via `InstallPart` for each slot in `chassis.StarterParts`; (5) seeding the deck with `chassis.StarterDeck` cards (`SourceSlotId = null`); (6) returning the new `Vehicle`.
- [ ] **AC-7**: If `Layout.Validate()` throws `FrameLayoutInvalidException`, `VehicleFactory.Build` lets it propagate (no swallow). Caller (game bootstrap) handles the user-facing failure.
- [ ] **AC-8**: `VehicleFactory` is in `WastelandRun.Gameplay` (NOT `WastelandRun.Vehicle`) — verified by namespace + asmdef location. Engine-free `WastelandRun.Vehicle` has no factory class.
- [ ] **AC-9**: Two `VehicleFactory.Build` calls with the same chassis + same `rngSeed` produce vehicles with identical `Slots` order, identical starter cards order, identical RNG state. Determinism is total.
- [ ] **AC-10**: `IPartCatalog` implementation is stateless — does not cache filtered results across calls (or if it does, cache invalidation is documented). No global mutable state.

---

## Implementation Notes

- `VehicleStatePreview` should be small (just deltas, not a deep copy of the entire vehicle).
- For `PreviewInstall`, the cheapest approach: clone only the affected `SlotInstance`, recompute `StructuralHp` from the would-be slot states, return the result. Do NOT clone the entire `Vehicle`.
- `VehicleFactory.Build` is the SINGLE entry point for constructing a Vehicle in play. Tests may construct Vehicle directly; production code uses the factory.
- `IPartCatalog` implementation that reads from `PartDefinitionSO` assets goes in story 007.

---

## Out of Scope

- Storefront UI that calls `PreviewInstall` to show what an install would do — Scrap Economy / UI layer.
- ScriptableObject asset loading patterns — story 007.
- Save/load of vehicle state — ADR-0004 DTO layer (separate epic).

---

## QA Test Cases

**Logic story — automated test specs (live under `tests/unit/vehicle/catalog_factory_test.cs`):**

- **AC-1 / AC-2**: catalog never returns null
  - Given: catalog with zero entries
  - When: `GetParts(SlotKind.Weapon, Rarity.Common)`
  - Then: returns empty IReadOnlyList; `Count == 0`
  - Edge cases: `GetPartById` with unknown id → PartNotFoundException

- **AC-3 / AC-4**: PreviewInstall is non-mutating
  - Given: a vehicle V with snapshot state S_before
  - When: `PreviewInstall(V, "weapon_front", def)`
  - Then: V's state hash == S_before's hash; preview returns predicted slot HP = def.MaxHp, predicted Added cards count == def.GrantedCards.Length
  - Edge cases: invalid install (incompatible chassis) → `IsValidInstall = false`, `RejectionReason` populated; no state mutation

- **AC-6 / AC-7**: factory build path
  - Given: a valid `ChassisDefinition` with 4 slots + 4 starter parts + 10 starter cards
  - When: `VehicleFactory.Build(chassis, seed=42)`
  - Then: vehicle has all 4 slots, all starter parts installed, deck has 10 starter cards with `SourceSlotId == null`
  - Edge cases: invalid layout → FrameLayoutInvalidException propagates; missing starter part for a declared slot → InvalidPartInstallException

- **AC-8**: factory lives in Gameplay assembly
  - Given: assembly check
  - When: typeof(VehicleFactory).Assembly
  - Then: assembly name == "WastelandRun.Gameplay"; not "WastelandRun.Vehicle"
  - Edge cases: ensure no factory class lives in Vehicle assembly

- **AC-9**: determinism
  - Given: two factory builds, same chassis, same seed
  - When: both run
  - Then: slot order identical; starter card instance order identical; both vehicles produce same RNG sequence over 100 NextRngStep calls

- **AC-10**: catalog statelessness
  - Given: catalog returns list A for query Q
  - When: query Q is re-issued after irrelevant mutations elsewhere
  - Then: returns list A' equivalent to A (same elements, may be different instance)
  - Edge cases: no test order dependencies

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/vehicle/catalog_factory_test.cs` — must exist and pass all AC cases
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Stories 001, 002, 004 (factory uses InstallPart)
- Unlocks: Story 007 (SO-backed catalog implementation), game bootstrap, Scrap Economy preview UI
