# Story 002: Vehicle POCO + IVehicleView + IVehicleMutator

> **Epic**: Vehicle POCO + Part Catalog
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-05-25

## Context

**GDD**: `design/gdd/vehicle-and-part-architecture.md` + `design/gdd/vehicle-and-part-mechanics.md`
**Requirement**: `TR-vehicle-001`, `TR-vehicle-003`, `TR-vehicle-013`, `TR-vehicle-014`

**ADR Governing Implementation**: ADR-0005 — Vehicle POCO Sub-Model + Part Catalog (amended by **ADR-0007 — Frame-Driven Variable Slot System**)
**ADR Decision Summary**: `Vehicle` is a sealed POCO holding `IFrameLayout Layout` + `IReadOnlyList<SlotInstance> Slots` + `FuelContainer` + `RngState`. Slot access is O(1) via cached `Dictionary<string, SlotInstance>` (`_bySlotId`) and `Dictionary<SlotKind, IReadOnlyList<SlotInstance>>` (`_byKind`). Mutation is whitelisted via `IVehicleMutator`; observation is via the wider `IVehicleView`. **No vehicle-level Armor** — Armor is per-slot via `SlotKind.Armor` instances with a `RedirectsTo` SlotId. `StructuralHp` is the sum of `IsStructural`-flagged slot HPs.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: Vehicle has no `[SerializeField]`, no `MonoBehaviour`, no `ScriptableObject`. Save/load is handled by ADR-0004's DTO; this class never references Newtonsoft or Unity serialisers.

**Control Manifest Rules (this layer)**:
- Required: All vehicle mutation goes through `IVehicleMutator` — source: ADR-0005
- Required: `IVehicleView` is read-only (no methods returning mutable refs) — source: ADR-0005
- Forbidden: Vehicle-level `MaxArmor`/`CurrentArmor` fields (Armor is per-slot via `SlotKind.Armor`) — source: ADR-0007
- Forbidden: `IsDead` derived from any single slot's HP (use `StructuralHp == 0`) — source: ADR-0007

---

## Acceptance Criteria

- [ ] **AC-1**: `Vehicle` is a `sealed class` in `WastelandRun.Vehicle` with constructor `Vehicle(IFrameLayout layout, IReadOnlyList<SlotInstance> slots, FuelContainer fuel, ulong rngSeed)`. Constructor calls `layout.Validate()` and rethrows `FrameLayoutInvalidException` if invalid.
- [ ] **AC-2**: `Vehicle.Slots` returns `IReadOnlyList<SlotInstance>` in `Layout.SlotDefinitions` order (stable across the vehicle's lifetime).
- [ ] **AC-3**: `Vehicle.GetSlot(string slotId)` returns the matching `SlotInstance` in O(1) via `_bySlotId`; throws `KeyNotFoundException` (with the missing SlotId in the message) if absent.
- [ ] **AC-4**: `Vehicle.GetSlotsByKind(SlotKind kind)` returns `IReadOnlyList<SlotInstance>` in O(1) via `_byKind`; returns empty list (NOT null) if no slots of that kind exist.
- [ ] **AC-5**: `Vehicle.StructuralHp` returns the sum of `CurrentHp` across every slot where `Definition.IsStructural == true`. Returns 0 only when all structural slots are at 0 HP.
- [ ] **AC-6**: `Vehicle.IsDead` returns `StructuralHp == 0`. Does NOT depend on any single slot, SlotKind, or part state.
- [ ] **AC-7**: `IVehicleView` interface exposes read-only access: `IFrameLayout Layout`, `IReadOnlyList<SlotInstance> Slots`, `SlotInstance GetSlot(string)`, `IReadOnlyList<SlotInstance> GetSlotsByKind(SlotKind)`, `int StructuralHp`, `bool IsDead`, `IFuelContainer Fuel`, `DamageState GetSlotDamageState(string slotId)`, `StatModifier[] GetStatModifiers(TargetStat target)`. NO methods returning mutable refs.
- [ ] **AC-8**: `IVehicleMutator` declares mutation primitives: `void ApplyDamage(in DamageContext ctx, string targetSlotId)`, `void RepairSlot(string slotId, int amount)`, `void InstallPart(string slotId, PartId partId, PartDefinition def)`, `void RemovePart(string slotId, RemovalReason reason)`. Signatures only — implementations live in stories 003/004.
- [ ] **AC-9**: `IVehicleMutator` extends `IVehicleView` (mutators always have read access).
- [ ] **AC-10**: `Vehicle` implements both `IVehicleView` and `IVehicleMutator` directly.
- [ ] **AC-11** *(TR-013/014)*: Internal mutation events are plain C# `event Action<...>` (NO `UnityEvent`): `OnSlotHpChanged(string slotId, int oldHp, int newHp)`, `OnPartInstalled(string slotId, PartId part)`, `OnPartRemoved(string slotId, PartId part, RemovalReason reason)`, `OnCriticalStateChanged(bool isCritical)`, `OnSlotDamageStateChanged(string slotId, DamageState oldState, DamageState newState)`.
- [ ] **AC-12**: `Vehicle.RngState` is `System.Random` seeded from constructor `rngSeed`. Exposed via `IVehicleView.NextRngStep()` returning the next deterministic step (per ADR-0003 discipline).
- [ ] **AC-13**: `GetStatModifiers(TargetStat target)` walks every installed `PartDefinition.StatModifiers` and returns matching entries in install order — pipeline ordering (`(base + Add) × Multiply`, Override wins) is **not** applied here; the call site composes.

---

## Implementation Notes

- Cache `_bySlotId` and `_byKind` in the constructor — do not rebuild on read.
- `_byKind` values are `IReadOnlyList<SlotInstance>` snapshots cached at construction. Slot identity is stable for a `Vehicle`'s lifetime; only their internal state mutates in place.
- `StructuralHp` recomputes on read (sum over ≤ ~6 slots — cheaper than cache invalidation).
- `IVehicleMutator` consumers (Card Combat, Scrap Economy, Status Effects) MUST receive the interface, not the concrete `Vehicle` — enforces the whitelisted-caller pattern (ADR-0005).
- DO NOT add `MaxArmor`/`CurrentArmor` properties to `Vehicle`. Armor protection is computed by walking `GetSlotsByKind(SlotKind.Armor)` and following `RedirectsTo` (story 003 implements that walk).
- `ApplyDamage` signature uses `in DamageContext` (pass by readonly reference) to keep the ref struct stack-only.

---

## Out of Scope

- Story 003: Damage routing implementation, Armor redirect walking, OnCriticalStateChanged firing rules, ApplyDamage body.
- Story 004: Install/Remove invariants, event fire order, soft-disable on Offline.
- Story 005: FuelContainer concrete class.
- Story 006: Factory that builds a Vehicle from chassis SO.

---

## QA Test Cases

**Logic story — automated test specs (live under `tests/unit/vehicle/vehicle_poco_test.cs`):**

- **AC-1**: constructor validates layout
  - Given: a frame layout with duplicate SlotIds
  - When: `new Vehicle(badLayout, ...)`
  - Then: throws `FrameLayoutInvalidException`
  - Edge cases: zero slots → throws; null layout → ArgumentNullException

- **AC-3 / AC-4**: O(1) slot lookup
  - Given: a vehicle with 6 slots
  - When: `GetSlot("weapon_front")` and `GetSlotsByKind(SlotKind.Hull)` are called 10000 times each
  - Then: time grows linearly with iteration count, not with slot count
  - Edge cases: missing SlotId throws with SlotId in message; unused SlotKind returns empty list (not null)

- **AC-5 / AC-6**: StructuralHp + IsDead
  - Given: vehicle with two structural slots (Hull A: 30/40, Hull B: 20/40) and one non-structural (Weapon: 10/10)
  - When: `StructuralHp` is read
  - Then: returns 50; `IsDead == false`
  - Edge cases: drop both Hulls to 0 → `IsDead == true` even with Weapon at 10/10; non-structural slots never contribute

- **AC-7 / AC-8 / AC-9 / AC-10**: interface surface
  - Given: reflection on `IVehicleView` and `IVehicleMutator`
  - When: members are enumerated
  - Then: view has no mutating verbs; mutator extends view; both implemented by `Vehicle`
  - Edge cases: any `ref`/`out` mutable return on view interface fails

- **AC-11**: events are plain Action, never UnityEvent
  - Given: reflection on Vehicle event fields
  - When: event type is checked
  - Then: all are `System.Action<...>`; none are `UnityEvent`; raising with zero subscribers does not throw
  - Edge cases: subscribing with a null handler raises `ArgumentNullException` at subscribe time

- **AC-12**: RNG determinism
  - Given: two Vehicles built with the same seed
  - When: `NextRngStep()` called 100 times on each
  - Then: identical sequence
  - Edge cases: parallel construction with same seed still yields same sequence

- **AC-13**: GetStatModifiers ordering
  - Given: vehicle with parts installed in order [A, B, C] each contributing one StatModifier for `Damage`
  - When: `GetStatModifiers(TargetStat.Damage)` is called
  - Then: returns modifiers in install order [A, B, C]
  - Edge cases: parts with no modifier for the target are skipped (not null-returned)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/vehicle/vehicle_poco_test.cs` — must exist and pass all AC cases
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (core types must exist)
- Unlocks: Stories 003, 004, 005, 006
