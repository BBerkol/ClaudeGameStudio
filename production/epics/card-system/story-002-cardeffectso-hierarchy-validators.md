# Story 002: CardEffectSO Hierarchy & OnValidate Validators

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6 hours
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-004`, `TR-card-005`, `TR-card-006`, `TR-card-008`, `TR-card-009`, `TR-card-025`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring
**ADR Decision Summary**: Eight sealed `CardEffectSO` subclasses defined in `WastelandRun.Gameplay`; `OnValidate` runs Editor-only (guarded against IL2CPP stripping); SO import validators enforce all data contract rules at asset-save time, not runtime.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `OnValidate` is Editor-only. Guard with `#if UNITY_EDITOR` or `Application.isEditor` to prevent runtime stripping in IL2CPP builds. `[field: SerializeField]` required for auto-properties on SOs (Unity 6.3 breaking change — `[SerializeField]` on properties/methods is invalid).

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: pending
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` and ADR-0006, scoped to this story:*

- [ ] All 8 required EA `CardEffectSO` subclasses exist in `WastelandRun.Gameplay`: `DamageEffectSO`, `RestorePlatingEffectSO`, `RestoreArmorEffectSO`, `ApplyStatusEffectSO`, `RepairSubsystemEffectSO`, `DrawCardsEffectSO`, `GainEnergyEffectSO`, `ShiftPositionEffectSO`
- [ ] `DamageEffectSO` has inspector-exposed fields: `Amount (int)`, `PositionBonus (int)`, `BypassPlating (bool, default false)`
- [ ] `RestoreArmorEffectSO` has field `Amount (int)`; target is implicit Self — no per-slot targeting field
- [ ] `DrawCardsEffectSO` has fields `Count (int)` and `FilterFamily (CardFamily?)` (nullable)
- [ ] `OnValidate` rejects `Ethereal AND Retain` on the same card (EC2) — logs a Unity error at SO save time
- [ ] `OnValidate` rejects `EnergyCost < 0` — logs a Unity error
- [ ] `DamageEffectSO.OnValidate` rejects `PositionBonus < 0` on the effect asset itself — logs a Unity error at the effect SO level
- [ ] `OnValidate` rejects any card containing a `DamageEffectSO` where `BaseDamage != DamageEffectSO.Amount` (TR-card-025) — logs a Unity error identifying which field mismatches
- [ ] `OnValidate` rejects any card containing a `DamageEffectSO` where `BaseDamage < 1` — logs a Unity error
- [ ] `OnValidate` rejects any `RestoreArmorEffectSO` where `Amount < 1` — logs a Unity error
- [ ] `OnValidate` rejects any Control-family card (`Family == Control`) that does not contain at least one `DamageEffectSO` with `Amount >= 1` in its `Effects` list (TR-card-008)
- [ ] `OnValidate` rejects any card where `DamageEffectSO.BypassPlating == true` AND `ValidSubsystemTargets` includes `SlotType.Frame`
- [ ] `OnValidate` rejects any card where `DamageEffectSO.BypassPlating == true` AND `TargetType == AllEnemySubsystems`
- [ ] `OnValidate` rejects any card where `DamageEffectSO.BypassPlating == true` AND `ValidSubsystemTargets.Count == 0` (empty array = all-slots, which includes Frame — BypassPlating requires explicit non-Frame targets)
- [ ] `OnValidate` rejects rarity weights on `ChassisMasteryDefinitionSO` that do not sum to 100, that contain any negative value, or whose tier ranges are non-contiguous — each violation logs a separate Unity error per failing tier
- [ ] `OnValidate` rejects `MerchantPrice == 30` when `MerchantPrice > 0` (degenerate parity with `GlobalPurgeCost`). `MerchantPrice = 0` (unset) must NOT be rejected. A unit test covers: `MerchantPrice=30` → rejected; `MerchantPrice=0` → accepted; `MerchantPrice=29` → accepted
- [ ] `OnValidate` rejects `CardId` not matching pattern `^[a-z]+_[a-z]+_[0-9]{3}$`. Test cases: `"scout_precision_007"` passes; `"Scout_precision_007"` (uppercase) rejected; `"scout_precision_7"` (non-padded) rejected; `"scout-precision-007"` (hyphens) rejected; `"scout_precision_"` (empty sequence) rejected; `""` (empty string) rejected
- [ ] `OnValidate` rejection logic must be guarded with `#if UNITY_EDITOR` (compile-time exclusion — NOT `Application.isEditor`, which is runtime-only and does not strip code from IL2CPP standalone builds); zero `OnValidate` validator code appears in the IL2CPP standalone build
- [ ] All three `EffectConditionSO` subclasses exist in `WastelandRun.Gameplay`: `PositionConditionSO`, `SlotStateConditionSO`, `StatusConditionSO`. Each implements `ToRuntime()` returning the corresponding POCO condition record (`PositionCondition`, `SlotStateCondition`, `StatusCondition` from `WastelandRun.Cards`) and is creatable via `[CreateAssetMenu]` in the Inspector

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

- All 8 `CardEffectSO` subclasses live in `WastelandRun.Gameplay` (Unity side), not `WastelandRun.Cards`. They implement `ICardEffect` (the marker) from Story 001.
- `OnValidate` is called by Unity at SO save time in the Editor. It must use `Debug.LogError` (not `throw`) so Unity surfaces validation errors as inspector messages without breaking the save operation.
- The `BypassPlating` triple-check (Frame in ValidSubsystemTargets, AllEnemySubsystems TargetType, empty ValidSubsystemTargets) are three independent validators — each can fire independently. A card with `BypassPlating=true` and `TargetType=AllEnemySubsystems` AND `ValidSubsystemTargets` including Frame would produce three separate errors.
- `CardId` regex `^[a-z]+_[a-z]+_[0-9]{3}$`: the `[a-z]+` segments enforce lowercase letters only, no digits, no underscores within a segment. The `[0-9]{3}` segment enforces exactly 3 zero-padded digits. Use `System.Text.RegularExpressions.Regex.IsMatch()` in the validator.
- `OnValidate` on `ChassisMasteryDefinitionSO` checks each mastery tier's weight set independently. A tier with weights `[85, 14, 2]` (sums to 101) must be rejected even if another tier is valid.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Story 001**: The `ICardEffect` marker interface, `ICardData` contract, and the three sealed condition record types (`PositionCondition`, `SlotStateCondition`, `StatusCondition`) in `WastelandRun.Cards`
- **Story 003**: `CardDefinitionSO` authoring SO inspector layout; `TokenResolver` display wiring
- **Story 004**: `ChassisMasteryDefinitionSO` data authoring (this story validates the SO; Story 004 authors its content)

> **Scope note (from Story 001 AC13)**: The `EffectConditionSO` **authoring hierarchy** (`PositionConditionSO`, `SlotStateConditionSO`, `StatusConditionSO` as Unity ScriptableObjects in `WastelandRun.Gameplay`) is part of this story's scope. These are the Editor-side authoring types that serialize condition data into `CardDefinitionSO` assets. Verify all three SO subclasses exist and can be created via the Inspector before closing Story 002.

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1: Ethereal+Retain rejected, Innate+Ethereal permitted**
- Given: Four `CardDefinitionSO` instances with keyword combos: (a) `Ethereal|Retain`, (b) `Innate|Ethereal`, (c) `Innate|Exhaust`, (d) `Exhaust|Retain`
- When: `OnValidate()` is called on each
- Then: Only (a) logs a Unity error; (b), (c), (d) log no error for this rule
- Edge cases: Confirm (b) Innate+Ethereal does not trigger the Ethereal+Retain check

**AC-2: BypassPlating validation — three independent rules**
- Given: Three cards each with `BypassPlating=true`: (a) `ValidSubsystemTargets=[Frame]`, (b) `TargetType=AllEnemySubsystems`, (c) `ValidSubsystemTargets=[]`
- When: `OnValidate()` is called
- Then: Each logs exactly one error for its respective rule; a card satisfying none of the three conditions logs no BypassPlating error
- Edge cases: Card with `BypassPlating=true`, `TargetType=EnemySubsystem`, `ValidSubsystemTargets=[Weapon,Engine]` — accepted

**AC-3: CardId format validation**
- Given: Six `CardDefinitionSO` instances with CardIds as specified in AC
- When: `OnValidate()` is called on each
- Then: `"scout_precision_007"` passes; the 5 malformed IDs each log a descriptive error
- Edge cases: `"scout_precision_000"` (zero-padded) must pass; `"scout_precision_999"` must pass

**AC-4: MerchantPrice parity rejection**
- Given: Cards with `MerchantPrice` values of 30, 0, 29, 31
- When: `OnValidate()` is called
- Then: `MerchantPrice=30` rejected; `MerchantPrice=0`, 29, 31 accepted
- Edge cases: `MerchantPrice=30` with `EnergyCost=0` must still be rejected

**AC-5: BaseDamage consistency check**
- Given: A card with `DamageEffectSO.Amount=5` and `BaseDamage=4`
- When: `OnValidate()` is called
- Then: Error logged identifying the mismatch; `BaseDamage=5` with `Amount=5` passes
- Edge cases: A card with no `DamageEffectSO` — `BaseDamage` field irrelevant, no validation error for this rule

**AC-6: Control-family must-include-damage rule**
- Given: Four `CardDefinitionSO` instances: (a) Control-family, `Effects=[DamageEffectSO{Amount=2}]`; (b) Control-family, `Effects=[DamageEffectSO{Amount=0}]`; (c) Control-family, `Effects=[RestoreArmorEffectSO{Amount=3}]` (no damage); (d) Repair-family, `Effects=[RestoreArmorEffectSO{Amount=3}]`
- When: `OnValidate()` is called on each
- Then: (a) accepted; (b) rejected (Amount=0 fails >= 1 check); (c) rejected (no DamageEffectSO); (d) accepted (family gate does not apply)
- Edge cases: Control-family card with two `DamageEffectSO` entries where only one has `Amount >= 1` — accepted (at least one qualifies)

**AC-7 (EffectConditionSO): ToRuntime() projection correctness**
- Given: One instance each of `PositionConditionSO` (Required=RequiresAhead), `SlotStateConditionSO` (Slot=Weapon, RequiredState=Degraded), `StatusConditionSO` (Status=Burning, Present=true)
- When: `ToRuntime()` is called on each
- Then: Returns `PositionCondition(RequiresAhead)`, `SlotStateCondition(Weapon, Degraded)`, `StatusCondition(Burning, true)` respectively — types are the POCO records from `WastelandRun.Cards`
- Edge cases: Null SO reference does not throw — `ToRuntime()` returns null or throws `InvalidOperationException` (document which in the implementation)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/card-system/cardeffectso-validators_test.cs` — must exist and pass

**Status**: [x] `tests/unit/card-system/cardeffectso-validators_test.cs` — 44 test functions

---

## Dependencies

- Depends on: Story 001 must be DONE (`ICardEffect` marker and `ICardData` contract must exist)
- Unlocks: Story 003 (CardDefinitionSO authoring requires valid effect types), Story 004 (ChassisMasteryDefinitionSO weight validation)

---

## Completion Notes
**Completed**: 2026-05-22
**Criteria**: 19/19 passing
**Deviations**:
- ADVISORY (QL-TEST-COVERAGE): AC-11 multi-DamageEffectSO edge case and AC-16 MerchantPrice=30+EnergyCost=0 cross-field test were absent at `/code-review` time; both added during `/story-done` — no scope violation, tests cover already-implemented behaviour.
- LP-CODE-REVIEW blocking issues resolved before close: (1) doc comments added to all 11 SO subclasses; (2) null-entry propagation fixed in `ProjectEffects()` and `EffectConditionProjection.Project()` with `ValidateNullEffects()` added to `OnValidate()`.
**Test Evidence**: Logic — `tests/unit/card-system/cardeffectso-validators_test.cs` (44 test functions)
**Code Review**: Complete (LP-CODE-REVIEW — CHANGES REQUIRED resolved, re-reviewed APPROVED)
