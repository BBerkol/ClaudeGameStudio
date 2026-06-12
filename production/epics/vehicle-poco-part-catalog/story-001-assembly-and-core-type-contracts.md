# Story 001: Assembly + Core Type Contracts

> **Epic**: Vehicle POCO + Part Catalog
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-05-25

## Context

**GDD**: `design/gdd/vehicle-and-part-architecture.md` + `design/gdd/vehicle-and-part-mechanics.md`
**Requirement**: `TR-vehicle-004`, `TR-vehicle-025`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0005 — Vehicle POCO Sub-Model + Part Catalog (amended by **ADR-0007 — Frame-Driven Variable Slot System**, which supersedes the original `SlotType` enum and introduces frame-layout-driven slot instances)

**ADR Decision Summary**:
- ADR-0005: Vehicle as engine-free POCO; core types in `WastelandRun.Vehicle.asmdef` with `noEngineReferences: true`.
- ADR-0007 (amends ADR-0005): Replace the fixed `SlotType { Weapon, Engine, Mobility, Frame }` enum with the **`SlotKind` enum** (`Weapon, Engine, Mobility, Hull, Armor`) and represent slots as **`SlotInstance` records** declared by a per-chassis `IFrameLayout`. Damage flows through a stack-allocated `DamageContext` and respects per-slot `ExposureMultiplier`, `IsStructural`, and `MountDirection`.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: This assembly must compile with zero engine references. Verify via `asmdef` JSON inspection and a CI assembly-reference guard. Any indirect `UnityEngine.*` leak (via `JsonUtility`, `Mathf`, `Random`, `Debug`, etc.) breaks the foundation contract. `DamageContext` is a `readonly ref struct` (stack-allocated, no heap GC).

**Control Manifest Rules (this layer)**:
- Required: `WastelandRun.Vehicle.asmdef` compiles with `noEngineReferences: true` — source: ADR-0005
- Required: Engine-free vehicle contract types live in `WastelandRun.Vehicle`; SO authoring assets live in `WastelandRun.Gameplay` — source: ADR-0007 R6 Cluster B
- Required: Use `SlotKind { Weapon, Engine, Mobility, Hull, Armor }` for slot categorisation — source: ADR-0007
- Forbidden: `SlotType` enum (replaced by `SlotKind`) — source: control-manifest line 92 / ADR-0007
- Forbidden: `SlotKind.Frame` value (Frame was renamed to `Hull`) — source: control-manifest line 93 / ADR-0007
- Forbidden: Any `using UnityEngine;` or `using Unity.*;` in `WastelandRun.Vehicle` — source: ADR-0005
- Forbidden: `UnityEngine.Random` for seeded systems (use `System.Random`) — source: ADR-0003

---

## Acceptance Criteria

*From ADR-0005 §3 (assembly contract) and ADR-0007 §2 (type surface), scoped to this story:*

- [ ] **AC-1**: `WastelandRun.Vehicle.asmdef` exists with `"noEngineReferences": true` and `"references": []` (or only other engine-free internal asmdefs). Compilation succeeds.
- [ ] **AC-2**: `SlotKind` enum is declared with exactly five values in this order: `Weapon, Engine, Mobility, Hull, Armor`. No `Frame` value exists. The legacy `SlotType` enum is **not** present anywhere in `src/WastelandRun.Vehicle/`.
- [ ] **AC-3**: `SlotPosition` enum is declared (`Front`, `Side`, `Rear`, `Top`, `Internal`) for `AnchorPoint` and front/back damage routing.
- [ ] **AC-4**: `SlotDefinition` is declared as a `readonly record struct` (or `sealed record`) with fields: `SlotId` (string, stable identifier), `Kind` (`SlotKind`), `Anchor` (`AnchorPoint`), `MaxHp` (int, > 0), `IsStructural` (bool), `ExposureMultiplier` (float, default 1.0), `MountDirection` (`MountDirection`), `PositionRequirement` (`PositionRequirement?` nullable — pre-install filter).
- [ ] **AC-5**: `SlotInstance` is declared as a runtime record with fields: `Definition` (`SlotDefinition`), `CurrentHp` (int), `InstalledPart` (`PartId?` nullable), `RedirectsTo` (`string?` nullable — for `Armor` slots, names the Hull/Weapon/Engine/Mobility SlotId they protect). Equality is by `Definition.SlotId`.
- [ ] **AC-6**: `AnchorPoint` is declared as a `readonly record struct` carrying `SlotPosition Position` and `Vector2-equivalent` engine-free local offset (use `System.Numerics.Vector2` — engine-free).
- [ ] **AC-7**: `IFrameLayout` interface is declared with: `IReadOnlyList<SlotDefinition> SlotDefinitions { get; }`, `string FrameId { get; }`, and `void Validate()` (throws `FrameLayoutInvalidException` on duplicate SlotIds, zero structural slots, Armor `RedirectsTo` pointing at non-existent or non-protectable kind).
- [ ] **AC-8**: `DamageContext` is declared as a `readonly ref struct` with fields: `int IncomingDamage`, `DamageSource Source` (enum: `Card, Collision, DoT, Environment`), `bool BypassesArmor` (DoT sets true), `SlotPosition AttackerPosition` (for front/back routing), `int ReentrancyDepth` (int, starts 0, bounded by const `MAX_REENTRANCY` = 8).
- [ ] **AC-9**: `MountDirection` enum is declared (`Forward`, `Rear`, `Omnidirectional`).
- [ ] **AC-10**: `PositionRequirement` is declared as a `readonly record struct` (`SlotPosition Required, bool Strict`) — used by Part SOs to filter installable slots.
- [ ] **AC-11**: `LayoutNotValidatedThisSessionException`, `FrameLayoutInvalidException`, `SlotIdConflictException`, `InvalidPartInstallException` are declared in this assembly (all inherit `System.Exception`, no engine types).
- [ ] **AC-12** *(TR-004)*: `DamageState` enum is declared with exactly four values: `Empty, Functional, Degraded, Offline`. A pure helper `DamageState DeriveFor(SlotInstance slot, float degradedThreshold)` returns `Empty` when `InstalledPart == null`, `Offline` when `CurrentHp == 0`, `Degraded` when `CurrentHp <= MaxHp * degradedThreshold`, else `Functional`. State is **derived** — never stored on `SlotInstance`.
- [ ] **AC-13** *(TR-025)*: `StatModifier` value type is declared as a `readonly record struct` with fields: `TargetStat Target` (enum: `Speed, Damage, Armor, FuelEfficiency, RepairCost, …`), `StatOperation Op` (enum: `Add, Multiply, Override`), `float Value`, `string SourceTag` (for debugging). The `StatModifierSO` ScriptableObject wrapper is **not** declared here (it lives in `WastelandRun.Gameplay`, story 007).
- [ ] **AC-14**: All types above are covered by a CI/test guard that scans `src/WastelandRun.Vehicle/**/*.cs` for any `using UnityEngine` or `using Unity.` directive — guard reports zero matches.

---

## Implementation Notes

*Derived from ADR-0005 §3 + ADR-0007 §2 + §6 R6 Cluster B:*

- Existing `src/WastelandRun.Vehicle/Enums.cs` is **stale** (declares old `SlotType`). Delete the old `SlotType` declaration outright — do not retain it for "compat" (control manifest forbids the symbol).
- Use `readonly record struct` for all small value types — saves allocations and gives free structural equality.
- `DamageContext` is `ref struct` so it cannot escape to the heap or be stored in a field; this enforces the stack-only reentrancy invariant from ADR-0007 §4.
- `IFrameLayout.Validate()` MUST be called by `VehicleFactory` before any vehicle is constructed; bypass triggers `LayoutNotValidatedThisSessionException` on first damage call (story 003 implements the check).
- Place all types under `namespace WastelandRun.Vehicle` (no sub-namespaces yet — keep flat until justified).
- Document each public type with XML doc comments referencing the originating ADR section (e.g., `/// <summary>… per ADR-0007 §2.3</summary>`).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: The `Vehicle` POCO class itself, slot lookup APIs, `IVehicleView`/`IVehicleMutator`.
- Story 003: `ApplyDamage` pipeline using `DamageContext`.
- Story 006: `IFrameLayout` concrete implementation reading from `FrameLayoutSO`.
- Story 007: `FrameLayoutSO` + `PartDefinitionSO` + `ChassisDefinitionSO` (these live in `WastelandRun.Gameplay`, not this assembly).

---

## QA Test Cases

**Logic story — automated test specs (live under `tests/unit/vehicle/types_test.cs`):**

- **AC-1**: asmdef contract
  - Given: `src/WastelandRun.Vehicle/WastelandRun.Vehicle.asmdef`
  - When: parsed as JSON
  - Then: `noEngineReferences == true` AND `references` contains zero `Unity.*` or `UnityEngine.*` entries
  - Edge cases: trailing whitespace, BOM, missing field → fail loudly

- **AC-2 / AC-3 / AC-9**: enum shape
  - Given: reflection on `SlotKind`, `SlotPosition`, `MountDirection`
  - When: `Enum.GetNames()` is called
  - Then: returns exactly the declared values in declared order; `SlotKind` contains no `Frame`; no `SlotType` type exists in the assembly
  - Edge cases: someone adds a value mid-list → equality test on the full ordered array catches it

- **AC-4 / AC-5 / AC-6 / AC-10**: record struct equality
  - Given: two `SlotDefinition` instances with identical fields
  - When: `==` is invoked
  - Then: returns true; HashCode equal; `SlotInstance` equality keyed on `Definition.SlotId` only (CurrentHp difference does NOT break equality)
  - Edge cases: nullable `PositionRequirement` null vs default — distinct

- **AC-7**: IFrameLayout.Validate
  - Given: a stub layout with duplicate SlotId `"hull_main"`
  - When: `Validate()` is called
  - Then: throws `FrameLayoutInvalidException` with message naming the duplicate SlotId
  - Edge cases: zero structural slots, Armor RedirectsTo non-existent SlotId, Armor RedirectsTo another Armor slot → each must throw

- **AC-8**: DamageContext is ref struct
  - Given: source code compilation
  - When: someone attempts `DamageContext field;` in a class
  - Then: compile error CS8345 (ref struct cannot be a field)
  - Edge cases: boxing attempt via `object` cast → compile error

- **AC-11**: exception types
  - Given: typeof checks
  - When: each named exception is instantiated
  - Then: inherits `System.Exception`, message round-trips through `.Message`

- **AC-12**: engine-free guard
  - Given: glob `src/WastelandRun.Vehicle/**/*.cs`
  - When: grep for `^using UnityEngine` or `^using Unity\.`
  - Then: zero matches across all files
  - Edge cases: commented-out using lines are tolerated; aliased usings are not

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/vehicle/types_test.cs` — must exist and pass all AC-1 through AC-12 cases above
- CI guard script `tools/ci/check_vehicle_engine_free.sh` (or PowerShell equivalent) — referenced from CI workflow

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None (Foundation layer entry point)
- Unlocks: Stories 002, 003, 004, 005, 006, 007
