# Story 005: IFuelContainer + IFuelMutator

> **Epic**: Vehicle POCO + Part Catalog
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-25

## Context

**GDD**: `design/gdd/vehicle-and-part-architecture.md` + `design/gdd/vehicle-and-part-mechanics.md`
**Requirement**: `TR-vehicle-018`, `TR-vehicle-019`

**ADR Governing Implementation**: ADR-0005 — Vehicle POCO Sub-Model + Part Catalog
**ADR Decision Summary**: `FuelContainer` is a plain C# class in `WastelandRun.Vehicle` exposing `CurrentFuel`, `FuelCap`, `FuelMultiplier`. `IFuelContainer` is the read-only interface; `IFuelMutator` owns `SpendFuel(amount)` and `TryGrantFuel(amount)`. Overflow above cap is silently dropped (returns actual amount granted); underflow clamps to 0 (permissive contract per epic decision lock).

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No engine API touched. Pure data + integer math.

**Control Manifest Rules (this layer)**:
- Required: Fuel mutation only via `IFuelMutator` — source: ADR-0005
- Required: TryGrantFuel returns int (actual amount granted, not requested) — source: ADR-0005 / decision lock
- Forbidden: Resource conversion (Scrap↔Fuel) in this assembly — source: ADR-0005 (lives in Scrap Economy)

---

## Acceptance Criteria

- [ ] **AC-1** *(TR-018)*: `IFuelContainer` interface exposes read-only: `int CurrentFuel`, `int FuelCap`, `float FuelMultiplier`.
- [ ] **AC-2** *(TR-018)*: `IFuelMutator` interface declares: `void SpendFuel(int amount)`, `int TryGrantFuel(int amount)`. Extends `IFuelContainer`.
- [ ] **AC-3** *(TR-019)*: `TryGrantFuel(amount)` returns the actual amount granted (`min(amount, FuelCap - CurrentFuel)`); excess is silently dropped. Returns 0 when already full or amount ≤ 0.
- [ ] **AC-4**: `SpendFuel(amount)` clamps `CurrentFuel` to 0 (does not throw on overdraft). Fires `OnFuelChanged(oldFuel, newFuel)` when the value actually changes.
- [ ] **AC-5**: `FuelMultiplier` is mutable only via internal `Vehicle` recompute (when parts with fuel-efficiency modifiers install/remove). Default value is `1.0f`. Map travel cost formula `D-NM1` multiplies the node cost by `FuelMultiplier` at the call site (not this assembly).
- [ ] **AC-6**: `FuelContainer` constructor: `FuelContainer(int initialFuel, int cap, float multiplier)`. Throws `ArgumentOutOfRangeException` if `cap <= 0`, `initialFuel < 0`, `initialFuel > cap`, or `multiplier <= 0`.
- [ ] **AC-7**: `OnFuelChanged` is a plain C# `event Action<int, int>` (old, new). No `UnityEvent`.
- [ ] **AC-8**: `FuelContainer` is engine-free — no `UnityEngine.*` usings.

---

## Implementation Notes

- Keep this class tiny and immutable in shape (only `_currentFuel` mutates). `FuelCap` and `FuelMultiplier` mutate only through internal Vehicle recompute hooks.
- Expose internal setter for `FuelMultiplier` and `FuelCap` as `internal` so only `Vehicle` (same assembly) can write.
- `SpendFuel` permissive contract: overdraft scenarios (event triggers, end-of-turn drain) shouldn't crash the run.

---

## Out of Scope

- Travel cost formula D-NM1 — Node Map epic.
- Scrap↔Fuel conversion events — Scrap Economy assembly (per memory: `IScrapEconomy.TryConvert*` is caller-agnostic).
- Fuel UI display — view layer.

---

## QA Test Cases

**Logic story — automated test specs (live under `tests/unit/vehicle/fuel_container_test.cs`):**

- **AC-3**: TryGrantFuel overflow
  - Given: FuelContainer(50, 100, 1.0)
  - When: TryGrantFuel(80)
  - Then: returns 50; CurrentFuel = 100
  - Edge cases: already full → returns 0; negative amount → returns 0 (or throws? — pick one and document); zero → returns 0

- **AC-4**: SpendFuel clamp
  - Given: FuelContainer(10, 100, 1.0)
  - When: SpendFuel(25)
  - Then: CurrentFuel = 0 (no throw); OnFuelChanged fires once (10, 0)
  - Edge cases: SpendFuel(0) → no event fire (no change); SpendFuel on already 0 → no event

- **AC-6**: constructor guards
  - Given: invalid constructor params
  - When: instantiated
  - Then: throws ArgumentOutOfRangeException with parameter name
  - Edge cases: cap=0, cap=-1, initialFuel=-1, initialFuel > cap, multiplier=0, multiplier=-1

- **AC-7**: event type
  - Given: reflection
  - When: OnFuelChanged type checked
  - Then: `Action<int, int>`; not UnityEvent

- **AC-8**: engine-free
  - Given: grep over `FuelContainer.cs`
  - When: pattern `using UnityEngine` or `using Unity\.`
  - Then: zero matches

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/vehicle/fuel_container_test.cs` — must exist and pass all AC cases
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (events / engine-free assembly setup)
- Unlocks: Map travel cost (Node Map epic), event-system Scrap↔Fuel exchange (Node Encounter epic)
