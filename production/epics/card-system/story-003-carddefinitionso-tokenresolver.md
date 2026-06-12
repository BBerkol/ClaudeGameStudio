# Story 003: CardDefinitionSO & TokenResolver

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4 hours
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-001`, `TR-card-002`, `TR-card-003`, `TR-card-020`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring
**ADR Decision Summary**: `CardDefinitionSO` is the authoritative Unity-side card definition in `WastelandRun.Gameplay`. `TokenResolver` lives in `WastelandRun.Cards` (engine-free) and resolves `{param}` tokens at display time from effect SO fields — never pre-baked. Display-context wiring (combat tooltip, reward screen, etc.) is Integration/UI scope handled separately.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `[field: SerializeField]` required for auto-properties on SOs in Unity 6.3 (cannot use `[SerializeField]` on property accessors directly). `CardDefinitionSO` lives in `WastelandRun.Gameplay`; `TokenResolver` lives in `WastelandRun.Cards` (engine-free).

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: pending
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` and ADR-0006, scoped to this story:*

- [ ] **AC-1a (Logic — automatable)**: A reflection test confirms that `CardDefinitionSO` declares all required serialized fields as `[SerializeField]`-decorated private members with correct types: `_cardId` (string), `_displayName` (string), `_descriptionTemplate` (string), `_family` (CardFamily), `_rarity` (CardRarity), `_chassisPool` (ChassisType), `_isStarterCard` (bool), `_energyCost` (int), `_targetType` (CardTargetType), `_keywords` (CardKeyword), `_effectSOs` (CardEffectSO[])
- [ ] **AC-1b (DEFERRED — smoke-check)**: A `CardDefinitionSO` asset can be created in the Unity Inspector via Assets > Create menu and saved without Inspector errors. Verified manually at story close. Evidence: `production/qa/evidence/story-003-inspector-smokecheck.md`
- [ ] `TokenResolver.Resolve(ICardData)` correctly substitutes all 10 standard tokens using engine-free mock objects — no Unity context required: `{damage}` → `DamageEffect.Amount`; `{bonus}` → `DamageEffect.PositionBonus`; `{heal}` → `RepairSubsystemEffect.HpRestored`; `{plating}` → `RestorePlatingEffect.Amount`; `{armor}` → `RestoreArmorEffect.Amount`; `{draws}` → `DrawCardsEffect.Count`; `{energy}` → `GainEnergyEffect.Amount`; `{stacks}` → `ApplyStatusEffect.Stacks`; `{duration}` → `ApplyStatusEffect.Duration`; `{cost}` → `ICardData.EnergyCost`
- [ ] `TokenResolver` handles indexed tokens: `{damage.1}` resolves to the first `DamageEffect.Amount` in the effects list; `{damage.2}` resolves to the second. Neither renders as `"?"` on a card with two `DamageEffect` entries
- [ ] An unrecognized token (e.g., `{dmg}`, `{xyz}`) resolves to the literal string `"?"` and emits a compile-time validation warning at SO import — no runtime crash, no null reference
- [ ] `TokenResolver.Resolve()` called on a card whose `Effects` list contains a `DamageEffect` with `Amount = 0` returns `"0"` for `{damage}` — does not throw, does not return null, does not return `"?"`. (BaseDamage=0 is rejected at SO import by Story 002; this test covers the resolver's defensive posture only)
- [ ] F1 formula: `DamageOutput = BaseDamage + (PositionBonus × PositionConditionMet)`. Boundary tests: `BaseDamage=1, PositionBonus=0, PositionConditionMet=0` → `1`; `BaseDamage=1, PositionBonus=0, PositionConditionMet=1` → `1`; `BaseDamage=12, PositionBonus=8, PositionConditionMet=1` → `20`; `BaseDamage=12, PositionBonus=8, PositionConditionMet=0` → `12`
---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Display context wiring** (combat tooltip, reward screen, Merchant, Chopshop, deck inspection): the wiring of `TokenResolver` output into each display surface is a UI/Integration story. This story implements `TokenResolver` in the engine-free assembly only. When `DamageEffectSO.Amount` changes and the SO is re-saved, display contexts reflecting the updated value is a requirement on the display wiring story, not on this story.
- **Story 002**: `OnValidate` validators on `CardDefinitionSO`
- **Story 004**: `AddressablesCardCatalog` loading of `CardDefinitionSO` assets
- **Story 005**: `CardDraft.RulesText` token pre-baking — `ICardRewardGenerator.Generate()` must call `TokenResolver.Resolve()` before populating `RulesText`; `CardDraft.RulesText` is pre-templated and the presentation layer renders it verbatim (moved from original AC-7 per QA readiness review)

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1a: CardDefinitionSO serialized field declaration (reflection test)**
- Given: `CardDefinitionSO` type loaded via `typeof(CardDefinitionSO)`
- When: `GetFields(NonPublic | Instance)` is called and filtered for `[SerializeField]` attribute
- Then: All 11 required fields exist with their correct declared types; no required field is missing or typed incorrectly
- Edge cases: `[field: SerializeField]` on auto-properties is a Unity 6.3 error — confirm decorator is on the private backing field, not a property accessor

**AC-1b: Inspector smoke-check (DEFERRED — manual)**
- Setup: Open Unity Editor, right-click in Project panel, Assets > Create > Wasteland > Card > Definition
- Verify: All 11 fields are visible and editable in the Inspector; no missing field warnings; saving the asset does not produce Console errors
- Pass condition: Asset saves cleanly; all fields are serialized and round-trip correctly on domain reload

**AC-2: All 10 standard tokens resolve**
- Given: A mock `ICardData` implementation with all effect types populated (one of each)
- When: `TokenResolver.Resolve(card)` is called
- Then: Each `{token}` in the description template is replaced with the correct numeric string; no `"?"` appears
- Edge cases: Card with only a `DamageEffect` — `{heal}` token resolves to `"?"` (no matching effect)

**AC-3: Indexed token resolution**
- Given: A mock `ICardData` with two `DamageEffect` entries: `Amount=5` (first), `Amount=8` (second); description = `"Deal {damage.1} then {damage.2}"`
- When: `TokenResolver.Resolve(card)` is called
- Then: Result is `"Deal 5 then 8"`
- Edge cases: `{damage.3}` on a two-effect card resolves to `"?"` (no third entry)

**AC-4: Unknown token renders as "?"**
- Given: A card with description `"Deal {dmg} damage"`
- When: `TokenResolver.Resolve(card)` is called
- Then: Returns `"Deal ? damage"`; no exception thrown
- Edge cases: Multiple unknown tokens in one description: each independently renders as `"?"`

**AC-5: DamageEffect.Amount=0 defensive case**
- Given: A mock `ICardData` with a `DamageEffect` having `Amount=0`; description = `"Deal {damage} damage"`
- When: `TokenResolver.Resolve(card)` is called
- Then: Returns `"Deal 0 damage"` — no throw, no null, no `"?"`
- Edge cases: This is the resolver's defensive posture; this case cannot occur from correctly validated SO assets

**AC-6: F1 formula boundary values**
- Given: Four `DamageOutput` computation calls with specified BaseDamage/PositionBonus/PositionConditionMet inputs
- When: F1 formula is evaluated
- Then: `(1, 0, 0)→1`, `(1, 0, 1)→1`, `(12, 8, 1)→20`, `(12, 8, 0)→12`
- Edge cases: `PositionBonus=0` with `PositionConditionMet=1` must produce `BaseDamage` only (no negative or addition error)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/card-system/token-resolver_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 must be DONE (`ICardData` interface must exist), Story 002 must be DONE (effect SO types must exist)
- Unlocks: Story 004 (AddressablesCardCatalog loads CardDefinitionSO assets authored here)

## Completion Notes
**Completed**: 2026-05-22
**Criteria**: 6/7 passing (AC-1b DEFERRED — manual inspector smoke-check, by design)
**Deviations**: ADVISORY — AC-2 spec text named `{plating}` field as `RestorePlatingEffect.Amount`; actual field is `Stacks`. Implementation and tests use `e.Stacks` (correct per record definition). AC text was inaccurate; code is correct.
**Test Evidence**: Logic — `tests/unit/card-system/token-resolver_test.cs` (18 tests, all automatable ACs covered)
**Code Review**: Complete — LP-CODE-REVIEW: APPROVED; QL-TEST-COVERAGE: ADEQUATE
**LP Suggestions (non-blocking)**: Add one-line doc comments to `RepairSubsystemEffect`, `GainEnergyEffect`, `RestoreArmorEffect`, `RestorePlatingEffect` records; add doc comment to `CardDefinitionSO.ValidSubsystemTargets` clarifying empty = all slots.
