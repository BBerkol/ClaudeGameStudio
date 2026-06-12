# Story 003: Damage Pipeline F-VP2 + Reentrant ApplyDamage

> **Epic**: Vehicle POCO + Part Catalog
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-25

## Context

**GDD**: `design/gdd/vehicle-and-part-architecture.md` + `design/gdd/vehicle-and-part-mechanics.md` (F-VP2)
**Requirement**: `TR-vehicle-005`, `TR-vehicle-015`, `TR-vehicle-016`, `TR-vehicle-017`

**ADR Governing Implementation**: **ADR-0007 — Frame-Driven Variable Slot System** (amends ADR-0005's damage model)
**ADR Decision Summary**: Damage flows through F-VP2 in this order: **Plating → Armor (per-slot redirect) → Hp**. DOT (`DamageContext.BypassesArmor == true`) skips Armor entirely. `ApplyDamage` is reentrant via a stack-allocated `DamageContext.ReentrancyDepth` counter (bounded by `MAX_REENTRANCY = 8`). `OnCriticalStateChanged` fires at most once per top-level entry — nested damage from on-hit triggers must not refire it. `OnSlotDamageStateChanged` fires once per slot when a state transition occurs (Functional → Degraded, Degraded → Offline, Offline → Functional via repair). **Repair** emits `OnSlotHpChanged` (Decision 12) and may emit `OnSlotDamageStateChanged` if the slot crosses the threshold back up.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: `DamageContext` is `readonly ref struct` (stack-only). Reentrant calls increment depth on each recursion entry. Implementation must never box, store, or capture the context.

**Control Manifest Rules (this layer)**:
- Required: Damage routing follows F-VP2 (Plating → Armor → Hp; DOT bypasses Armor) — source: ADR-0007 §4
- Required: Reentrant `ApplyDamage` uses `DamageContext.ReentrancyDepth`, bounded by `MAX_REENTRANCY` — source: ADR-0007
- Required: `OnCriticalStateChanged` fires at-most-once per top-level entry — source: ADR-0007 Decision 15
- Required: `RepairSlot` emits `OnSlotHpChanged` — source: ADR-0005 Decision 12
- Forbidden: Vehicle-level Armor pool (CurrentArmor field) — Armor is per-slot — source: ADR-0007
- Forbidden: `UnityEngine.Random` in damage calculation — use `Vehicle.NextRngStep()` — source: ADR-0003

---

## Acceptance Criteria

- [ ] **AC-1** *(TR-015)*: `ApplyDamage(in DamageContext ctx, string targetSlotId)` consumes `PlatingStacks` first (one stack absorbs one damage instance, then is removed; remaining damage continues). When `PlatingStacks == 0`, walks `GetSlotsByKind(SlotKind.Armor)`; for each Armor slot whose `RedirectsTo == targetSlotId` and whose `CurrentHp > 0`, redirects damage to that Armor slot until Armor is depleted; only then applies remaining damage to the target slot's `CurrentHp`.
- [ ] **AC-2** *(TR-016)*: When `ctx.BypassesArmor == true` (DoT), Armor redirect is skipped entirely — Plating still absorbs, but damage flows Plating → Hp directly. Corrode-type effects apply their bonus damage **before** Plating absorption.
- [ ] **AC-3** *(TR-005)*: When a Hull slot's `CurrentHp` reaches 0 due to ApplyDamage, and `Vehicle.IsDead` becomes true (i.e., `StructuralHp == 0`), no further damage is dispatched in the same call. The vehicle is considered destroyed; subsequent `ApplyDamage` calls early-return with no events.
- [ ] **AC-4** *(TR-005)*: Non-structural slots (Weapon, Engine, Mobility) that hit 0 HP transition to `DamageState.Offline` but do **not** kill the vehicle. They can be revived by `RepairSlot` when the part's `CanReviveOffline` flag is true.
- [ ] **AC-5** *(TR-017)*: On every `CurrentHp` change, fire `OnSlotHpChanged(slotId, oldHp, newHp)` exactly once per slot per change. On a `DamageState` transition (derived via `DeriveFor`), fire `OnSlotDamageStateChanged(slotId, oldState, newState)` exactly once — even if multiple sub-thresholds were crossed in a single damage instance.
- [ ] **AC-6**: `OnCriticalStateChanged(bool isCritical)` fires at most once per top-level `ApplyDamage` call. Reentrant calls (triggered by on-hit effects, chain damage) may modify state but must not re-fire the event. Implementation: check `ctx.ReentrancyDepth == 0` before firing.
- [ ] **AC-7**: `ApplyDamage` increments `ReentrancyDepth` when recursing for chain effects; throws `MaxReentrancyExceededException` when depth would exceed `MAX_REENTRANCY = 8`.
- [ ] **AC-8** *(Decision 12)*: `RepairSlot(string slotId, int amount)` clamps to `MaxHp`, emits `OnSlotHpChanged(slotId, oldHp, newHp)`. If `newHp > 0` and prior `DamageState == Offline`, emits `OnSlotDamageStateChanged(slotId, Offline, Functional)`. Repair on a part whose `CanReviveOffline == false` and currently Offline throws `InvalidRepairException`.
- [ ] **AC-9**: Damage rounding rule: per-step damage is `int`; fractional Armor multipliers (e.g., ExposureMultiplier) compute as `(int)Math.Round(damage * mult, MidpointRounding.AwayFromZero)`. Floor at 1 per landed instance (no zero-damage no-ops once the call reaches Hp).
- [ ] **AC-10**: `ApplyDamage` is deterministic given the same `Vehicle` state and `DamageContext` — no `UnityEngine.Random`, no `DateTime.Now`, no global state. Uses `Vehicle.NextRngStep()` if random tie-breaks are needed.

---

## Implementation Notes

*Derived from ADR-0007 §4 Damage Pipeline + Decisions 12, 15:*

- Pseudocode skeleton:
  ```
  ApplyDamage(in ctx, targetSlotId):
    if IsDead: return
    if ctx.ReentrancyDepth >= MAX_REENTRANCY: throw
    bool firedCritical = (ctx.ReentrancyDepth == 0) ? PrepareCriticalFlag() : false

    remaining = ctx.IncomingDamage
    remaining = ConsumePlating(remaining)  // both DOT and non-DOT
    if not ctx.BypassesArmor:
      remaining = ConsumeArmorRedirectingTo(targetSlotId, remaining)
    ApplyToSlotHp(targetSlotId, remaining)  // emits OnSlotHpChanged + possibly OnSlotDamageStateChanged

    if firedCritical && CriticalStateNowDiffers(): FireOnCriticalStateChanged()
  ```
- Use `Span<SlotInstance>` (engine-free) when iterating armor slots if performance matters; otherwise a simple `foreach` is fine for ≤6 slots.
- `OnSlotDamageStateChanged` event order: HP first (`OnSlotHpChanged`), THEN state (`OnSlotDamageStateChanged`). Subscribers reading HP in the state handler must see the new HP.
- The "critical" semantic for `OnCriticalStateChanged` is: `IsCritical = (StructuralHp / MaxStructuralHp) <= ChassisDefinition.CriticalThreshold`. Read threshold from `Vehicle.Layout` (story 007 supplies via `FrameLayoutSO`).

---

## Out of Scope

- Card-effect damage sources (Card Combat assembly) — that layer just calls `ApplyDamage` with a built `DamageContext`.
- VFX/audio reaction to damage events — subscribed by view layer, not this story.
- Repair cost economy — Scrap Economy assembly handles cost; this story just exposes `RepairSlot`.

---

## QA Test Cases

**Logic story — automated test specs (live under `tests/unit/vehicle/damage_pipeline_test.cs`):**

- **AC-1**: F-VP2 ordering with Plating + Armor + Hp
  - Given: vehicle with PlatingStacks=2, Armor slot HP=10 (RedirectsTo "hull_main"), hull_main HP=20
  - When: `ApplyDamage(15 damage, "hull_main")` (non-DOT)
  - Then: PlatingStacks=0 (absorbed 2), Armor HP=0 (absorbed 10), hull_main HP=17 (took 3)
  - Edge cases: damage exactly equal to plating (5 vs 5) → plating zeroed, no armor consumption; damage smaller than plating (3 vs 5 stacks) → 3 stacks consumed, 2 remain

- **AC-2**: DOT bypasses Armor
  - Given: same vehicle, DOT damage with `BypassesArmor=true`
  - When: `ApplyDamage(15 dot damage)`
  - Then: Plating still absorbs (2 stacks → 13 remaining), Armor untouched (still 10), hull_main HP=7 (took 13)
  - Edge cases: Corrode bonus damage applied BEFORE plating; verify corrode + plating produces correct order

- **AC-3 / AC-4**: structural vs non-structural death rules
  - Given: vehicle with all 3 Hull slots at 1 HP, Weapon at 5 HP
  - When: `ApplyDamage(3 damage, "hull_a")` then "hull_b" then "hull_c"
  - Then: after third call, IsDead=true; further calls return immediately
  - Edge cases: weapon dropped to 0 → DamageState.Offline, IsDead still false; engine dropped to 0 → same

- **AC-5**: event fire counts
  - Given: subscribe to OnSlotHpChanged and OnSlotDamageStateChanged
  - When: ApplyDamage takes a slot from 20 HP (Functional) through Degraded (≤10) to Offline (0) in one call (e.g., 25 damage)
  - Then: OnSlotHpChanged fires once with (20, 0); OnSlotDamageStateChanged fires once with (Functional, Offline) — NOT twice
  - Edge cases: zero net damage (fully absorbed by plating) emits no events

- **AC-6 / AC-7**: reentrancy & critical event
  - Given: a chain-damage effect that triggers ApplyDamage from within an OnSlotHpChanged handler
  - When: top-level ApplyDamage call resolves
  - Then: OnCriticalStateChanged fires at most once (or zero times if critical state didn't change end-to-end); reentry depth 9 throws MaxReentrancyExceededException
  - Edge cases: critical→non-critical→critical within one top-level call → final state critical → fires once with `true`

- **AC-8**: repair semantics
  - Given: slot at 0 HP, Offline, part CanReviveOffline=true
  - When: RepairSlot(slotId, 5)
  - Then: HP becomes 5; OnSlotHpChanged fires; OnSlotDamageStateChanged fires (Offline→Functional)
  - Edge cases: repair over MaxHp clamps; repair on CanReviveOffline=false while Offline throws InvalidRepairException

- **AC-9**: rounding
  - Given: ExposureMultiplier=1.5 on a hit of 3 damage
  - When: ApplyDamage computes
  - Then: damage = Round(4.5, AwayFromZero) = 5; floor at 1 (never zero once it reaches Hp)
  - Edge cases: 0.4 × 1 = 0.4 → rounds to 0 → floor to 1 (lands as 1 damage)

- **AC-10**: determinism
  - Given: two vehicles in identical state, same DamageContext sequence
  - When: ApplyDamage run on each
  - Then: identical final state, identical event firing order
  - Edge cases: parallel runs with same seed → identical

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/vehicle/damage_pipeline_test.cs` — must exist and pass all AC cases
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Stories 001, 002
- Unlocks: Story 004 (install/remove uses repair semantics), card combat work in later epics
