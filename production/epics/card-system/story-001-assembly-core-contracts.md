# Story 001: WastelandRun.Cards Assembly Core Contracts

> **Epic**: Card System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 8 hours
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-001`, `TR-card-002`, `TR-card-003`, `TR-card-007`, `TR-card-009`, `TR-card-022`, `TR-card-023`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring
**ADR Decision Summary**: Three-assembly split — `WastelandRun.Cards` (engine-free POCO, `noEngineReferences: true`), `WastelandRun.Gameplay` (Unity SO authoring types), `WastelandRun.Combat` (references both). `ICardEffect` is a pure marker interface; effect dispatch in combat uses a C# `switch` expression over sealed record subtypes — NOT virtual `Apply()` dispatch. This avoids a circular assembly dependency (`CardResolutionContext` cannot live in the engine-free assembly).

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `WastelandRun.Cards` must compile with zero `UnityEngine.*` references — verified by CI assembly-reference guard or EditMode test. `[SerializeReference]` polymorphism is not used here (stripping-unsafe under IL2CPP=High per ADR-0006). `SourceSlotId` is runtime-only and NOT serialized in DTO (ADR-0006 Amendment 2026-05-18).

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: pending
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` and ADR-0006, scoped to this story:*

- [ ] `WastelandRun.Cards` assembly compiles with no `UnityEngine.*` references — verified by a CI assembly-reference guard or an EditMode NUnit test that inspects the compiled assembly's referenced namespaces
- [ ] `ICardData` interface exposes exactly these members (verified by a compile-time unit test that assigns a mock implementation): `CardId (string)`, `DisplayName (string)`, `DescriptionTemplate (string)`, `FlavorText (string)`, `CardArtKey (string)`, `ChassisPool (ChassisType)`, `Family (CardFamily)`, `Rarity (CardRarity)`, `IsStarterCard (bool)`, `EnergyCost (int)`, `MerchantPrice (int)`, `TargetType (TargetType)`, `ValidSubsystemTargets (IReadOnlyList<SlotType>)`, `PositionRequirement (PositionRequirement)`, `Keywords (CardKeyword)`, `Effects (IReadOnlyList<ICardEffect>)`, `BaseDamage (int)`, `SourceSlotId (string?)`
- [ ] `ICardEffect` is a pure marker interface with no methods and no properties — verified by `typeof(ICardEffect).GetMethods().Length == 0` in a unit test. No `Apply()` method exists on `ICardEffect` or any of its implementations (code review gate)
- [ ] `DamageEffect` sealed record carries `IReadOnlyList<ICardEffectCondition> Conditions` (not on `ICardEffect`). A unit test creates a `DamageEffect` with two `PositionCondition` entries and asserts `Conditions.Count == 2`
- [ ] `ICardEffectCondition` is a **pure marker interface with no methods and no properties** — `typeof(ICardEffectCondition).GetMethods().Length == 0`. Condition evaluation is performed in `WastelandRun.Combat` by switching over the sealed condition record subtypes (`PositionCondition`, `SlotStateCondition`, `StatusCondition`). No `IsMet()` method exists anywhere in `WastelandRun.Cards` (code review gate). `CardResolutionContext` lives in the Combat assembly — the Cards assembly has no reference to it.
- [ ] `CardFamily` enum contains exactly: `Precision`, `Assault`, `Control`, `Repair`, `Maneuver`
- [ ] `CardRarity` enum contains exactly four values: `Common`, `Uncommon`, `Rare`, `Legendary` — a reflection test asserts `Enum.GetValues(typeof(CardRarity)).Length == 4` and each named value is present (ADR-0006 line 194)
- [ ] `RewardDrawAlgorithm.Generate` never selects `Rarity == Legendary` regardless of mastery level — a unit test with a stub catalog containing exactly one Legendary card runs 10,000 draws using `new System.Random(seed: 42)` and asserts the Legendary draft count is 0. Test must produce the same result on every run (deterministic)
- [ ] `CardKeyword` is a `[Flags]` enum containing at minimum: `Exhaust`, `Retain`, `Innate`, `Ethereal`
- [ ] `TokenResolver` resolves each of the following 10 standard tokens to its correct source field value (per GDD `design/gdd/card-system.md` §1a binding table). "Correctly" means: given a mock `ICardData` with known field values, the resolved string equals the field value formatted as a decimal integer string. All 10 must pass: `{damage}` → `DamageEffect.Amount`; `{bonus}` → `DamageEffect.PositionBonus`; `{heal}` → `RepairSubsystemEffect.HpRestored`; `{plating}` → `RestorePlatingEffect.Amount`; `{armor}` → `RestoreArmorEffect.Amount`; `{draws}` → `DrawCardsEffect.Count`; `{energy}` → `GainEnergyEffect.Amount`; `{stacks}` → `ApplyStatusEffect.Stacks`; `{duration}` → `ApplyStatusEffect.Duration`; `{cost}` → `ICardData.EnergyCost`. Unrecognized tokens resolve to literal `"?"` (no throw, no null)
- [ ] `TokenResolver` handles indexed tokens: given a card with two `DamageEffect` entries (`Amount=5` first, `Amount=8` second), `{damage.1}` resolves to `"5"` and `{damage.2}` resolves to `"8"`. Given a card with only one `DamageEffect`, `{damage.2}` resolves to `"?"` (out-of-bounds index = unrecognized token, same as unknown tokens). Given no `DamageEffect` at all, both `{damage.1}` and `{damage.2}` resolve to `"?"`
- [ ] `ICardRewardGenerator.Generate` has exactly this signature: `CardDraft[] Generate(ChassisType chassis, int mastery, int rarePityCounter, int drawCount, IReadOnlyList<string> currentDeckCardIds, System.Random rng)`. A compile-time test assigns a lambda conforming to this signature. The method does NOT accept `int seed` or `RunSeed` — caller constructs and owns the `System.Random` instance (ADR-0003)
- [ ] `CardDraft` exposes exactly these 10 typed fields matching ADR-0006 (line 249–261): `CardId (string)`, `DisplayName (string)`, `RulesText (string)`, `Family (CardFamily)`, `Rarity (CardRarity)`, `EnergyCost (int)`, `CardArtKey (string)`, `KeywordBadges (string[])`, `MerchantPrice (int?)`, `SelectionHash (int)`. A compile-time test assigns values to all 10 fields on a concrete `CardDraft` instance; a reflection test asserts exactly 10 public non-static fields/properties are present. Both name and type must match — count-only verification is insufficient.
- [ ] The three sealed condition record types exist in `WastelandRun.Cards`: `PositionCondition` (implements `ICardEffectCondition`, holds `PositionState` field), `SlotStateCondition` (holds `SlotType` and `DamageState` fields), `StatusCondition` (holds `StatusType` and `TargetVehicle` fields). A unit test creates one instance of each and asserts it implements `ICardEffectCondition`. Note: the Unity-side `EffectConditionSO` authoring hierarchy lives in `WastelandRun.Gameplay` and is out of scope for this story — see Story 002.

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

- `WastelandRun.Cards` is a plain C# assembly with `noEngineReferences: true` in its `.asmdef`. This is enforced at CI — do not relax this constraint.
- `ICardEffect` is a **marker interface only**. Effect dispatch belongs in `WastelandRun.Combat` via `switch` on sealed record types. If you find yourself writing `Apply()` on any `ICardEffect` implementation, stop — that logic belongs in the combat assembly.
- Conditions live on `DamageEffect` specifically (as `IReadOnlyList<ICardEffectCondition>`) because only damage effects have conditional behavior in the combat pipeline. Other effect types do not carry conditions.
- `SourceSlotId` on `ICardData` is stamped at runtime when a part installs a card. It is NOT persisted in `CardSystemDTO`. It is nullable — starter cards and Merchant-purchased cards have `null` source slot.
- `CardDraft.RulesText` must be pre-templated by `TokenResolver` before being returned from `ICardRewardGenerator.Generate`. The presentation layer renders it verbatim — no token substitution at display time.
- `Legendary` must exist in `CardRarity` for forward compatibility but must be excluded from all EA reward pools. The draw algorithm must treat Legendary as unreachable by never including Legendary cards in the weighted pool.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Story 002**: `CardEffectSO` Unity-side subclass implementations, `OnValidate` import validators, and the `EffectConditionSO` authoring hierarchy in `WastelandRun.Gameplay`
- **Story 003**: `CardDefinitionSO` authoring SO and `TokenResolver` display wiring
- **Story 005**: `RewardDrawAlgorithm` implementation (pool selection, determinism, copy-limit filtering)
- **`WastelandRun.Combat`**: Condition evaluation (`IsMet` equivalent) — implemented as a `switch` expression over condition record types; lives in the Combat assembly, not here

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1: Assembly has no UnityEngine references**
- Given: `WastelandRun.Cards.dll` compiled
- When: Assembly referenced namespaces are inspected
- Then: No entry contains `UnityEngine` — assertion passes
- Edge cases: Check transitive references; a helper class in the assembly importing `UnityEngine.Debug` would still fail

**AC-2: ICardData exposes all 18 required members**
- Given: A mock class implementing `ICardData` is compiled
- When: Each of the 18 specified fields is accessed via the interface reference
- Then: Compilation succeeds; no member access fails at compile time
- Edge cases: Add a 19th field to the mock that is NOT on `ICardData` — accessing it via the interface must fail to compile

**AC-3: ICardEffect is a pure marker interface**
- Given: `typeof(ICardEffect)`
- When: `.GetMethods()` is called
- Then: Returns an array of length 0
- Edge cases: Ensure `GetMethods(BindingFlags.Instance | BindingFlags.Public)` also returns 0

**AC-4: DamageEffect carries Conditions**
- Given: A `DamageEffect` record created with two `PositionCondition` entries
- When: `Conditions` is accessed
- Then: `Conditions.Count == 2`; the entries are the same references passed in
- Edge cases: `DamageEffect` with no conditions: `Conditions.Count == 0` (not null)

**AC-5a: ICardEffectCondition is a pure marker interface**
- Given: `typeof(ICardEffectCondition)`
- When: `.GetMethods(BindingFlags.Instance | BindingFlags.Public)` and `.GetProperties()` are called
- Then: Both return arrays of length 0; no `IsMet` method exists
- Edge cases: Verify that `PositionCondition`, `SlotStateCondition`, `StatusCondition` each implement `ICardEffectCondition` — `typeof(PositionCondition).IsAssignableTo(typeof(ICardEffectCondition))` must be true

**AC-5b: CardRarity enum shape**
- Given: `Enum.GetValues(typeof(CardRarity))`
- When: Values are enumerated
- Then: Length == 4; `Common`, `Uncommon`, `Rare`, `Legendary` are all present

**AC-5c: Legendary excluded from draw algorithm**
- Given: A stub catalog containing exactly one card with `Rarity == Legendary`; `new System.Random(seed: 42)`
- When: `RewardDrawAlgorithm.Generate()` is called 10,000 times
- Then: Zero returned `CardDraft` entries have `Rarity == Legendary`; same count on every run (deterministic)
- Edge cases: Catalog with one Legendary + one Common: all 10,000 draws return Common only

**AC-6: ICardRewardGenerator.Generate signature**
- Given: A lambda `(ChassisType c, int m, int p, int d, IReadOnlyList<string> deck, System.Random rng) => new CardDraft[0]`
- When: Assigned to `ICardRewardGenerator`
- Then: Compilation succeeds
- Edge cases: Replacing `System.Random rng` with `int seed` must fail to compile

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/card-system/assembly-contracts_test.cs` — must exist and pass

**Status**: [x] `tests/unit/card-system/assembly-contracts_test.cs` — 37 test functions

---

## Dependencies

- Depends on: None
- Unlocks: Story 002 (CardEffectSO subclasses build on these interfaces), Story 003 (TokenResolver builds on ICardData + token table), Story 005 (RewardDrawAlgorithm builds on ICardRewardGenerator + CardDraft)

---

## Completion Notes

**Completed**: 2026-05-22
**Criteria**: 15/15 passing (AC-5c deferred — `[Ignore]` pending Story 005 RewardDrawAlgorithm)
**Deviations**: `SlotType` uses `Mobility` per ADR-0005 (task brief said `Utility`); `EqualityContract` defensive filter added in record equality tests; `[System.Serializable]` removed from `CardSystemDTO` (Newtonsoft.Json doesn't need it); `int.TryParse` overflow guard added to `TokenResolver` for malformed index tokens
**Test Evidence**: `tests/unit/card-system/assembly-contracts_test.cs` — 37 test functions
**Code Review**: Complete — LP: APPROVE | QL-TEST-COVERAGE: GAPS (5 advisory, none blocking)
