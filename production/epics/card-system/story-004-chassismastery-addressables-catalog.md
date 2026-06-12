# Story 004: ChassisMasteryDefinitionSO & AddressablesCardCatalog

> **Epic**: Card System
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: pending — `docs/architecture/control-manifest.md` not yet authored

## Context

**GDD**: `design/gdd/card-system.md`
**Requirements**: `TR-card-011`, `TR-card-012`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Card System Data Authoring; ADR-0008: Addressables Runtime Asset Loading
**ADR Decision Summary**: `AddressablesCardCatalog` loads all `CardDefinitionSO` assets via a single `card-definitions` Addressables label partitioned by `ChassisPool`. `ChassisMasteryDefinitionSO` holds per-mastery-tier rarity weights and primary-family bias configuration. ADR-0008 is **Accepted** for this catalog loading use case.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: Addressables API in Unity 6.3 — use `Addressables.LoadAssetsAsync<CardDefinitionSO>("card-definitions", null)`. Wrap in try/catch/finally with `handle.IsValid()` guard calling `Addressables.Release(handle)`. Memory release on scene unload must be verified manually under the Unity Profiler (cannot be asserted in NUnit). IL2CPP standalone smoke test (ADVISORY) verifies that `Effects.Count > 0` and `DamageEffect.Conditions.Count > 0` survive managed-stripping=High.

**Control Manifest Rules (this layer)**:
- Required: pending
- Forbidden: pending
- Guardrail: pending

---

## Acceptance Criteria

*From `design/gdd/card-system.md` and ADR-0006, scoped to this story:*

- [ ] `ChassisMasteryDefinitionSO` contains per-mastery-tier rarity weights (`W_Common`, `W_Uncommon`, `W_Rare`) that must sum to 100 — enforced at SO import (`OnValidate`)
- [ ] `ChassisMasteryDefinitionSO` contains `PrimaryFamily (CardFamily[])` and `PrimaryFamilyBiasEnabled (bool)` per mastery tier. Scout configuration: `PrimaryFamily=[Precision, Maneuver]`, bias enabled at Mastery 1–3, disabled at 4+. Assault configuration: `PrimaryFamily=[Assault]`
- [ ] `AddressablesCardCatalog.LoadAsync()` wraps `Addressables.LoadAssetsAsync<CardDefinitionSO>("card-definitions")` in a try/catch block with a `handle.IsValid()` finally guard that calls `Addressables.Release(handle)` — verified by code review showing the try/catch/finally structure (not automated)
- [ ] An EditMode integration test calls `AddressablesCardCatalog.LoadAsync()` against a minimal test Addressables group containing exactly 3 `CardDefinitionSO` assets tagged `card-definitions` (2 Scout, 1 Assault). Asserts: (a) `GetByChassis(ChassisType.Scout)` returns exactly 2 cards; (b) `GetById("scout_precision_001")` returns non-null; (c) `GetByChassisAndRarity(ChassisType.Scout, CardRarity.Common)` returns only Common cards
- [ ] `AddressablesCardCatalog.GetCardsForChassis(ChassisType.Scout)` returns only cards where `ChassisPool == Scout` — Assault cards are not returned. Verified by the EditMode integration test
- [ ] **ADVISORY — IL2CPP standalone smoke (blocks epic DoD, not this story's completion)**: A standalone IL2CPP build with `ManagedStrippingLevel=High` loads one `CardDefinitionSO` via `AddressablesCardCatalog` and a dev-build assertion verifies: (a) `Effects.Count > 0`; (b) a card with at least one `DamageEffect` carrying conditions has `DamageEffect.Conditions.Count > 0` after loading. Evidence saved to `production/qa/evidence/story-004-il2cpp-smoke-[date].txt`

---

## Implementation Notes

*Derived from ADR-0006 and ADR-0008 Implementation Guidelines:*

- The `card-definitions` Addressables label is the single entry point for all card catalog queries. The catalog loads all cards with this label and partitions them in memory by `ChassisPool` for fast `GetByChassis()` lookup.
- `AddressablesCardCatalog` must not hold a strong reference to the `AsyncOperationHandle` after loading completes without the finally guard. Leaking a handle is the most common Addressables memory bug — the try/catch/finally pattern from ADR-0005 (`AddressablesPartCatalog`) applies here.
- `ChassisMasteryDefinitionSO.OnValidate` runs the weight-sum-to-100 check on every tier independently. If Mastery 1–3 weights sum to 99, that tier is flagged even if Mastery 4–6 is valid.
- For the EditMode integration test, create a minimal Addressables test group at `Assets/Tests/Card-System-Test-Group/` with 3 stub `CardDefinitionSO` assets. The test group must not interfere with production Addressables groups.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **Story 005**: `RewardDrawAlgorithm` uses `AddressablesCardCatalog` as its data source but implements the draw logic independently
- **Story 010**: Authoring the full EA card pool assets (this story sets up the loading infrastructure only)

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new cases.*

**AC-1 (Code Review): try/catch/finally handle guard**
- Setup: Review `AddressablesCardCatalog.LoadAsync()` source
- Verify: `Addressables.LoadAssetsAsync` call is inside a try block; finally block checks `handle.IsValid()` before calling `Addressables.Release(handle)`
- Pass condition: Reviewer can identify the try/catch/finally structure with no bare `handle.Release()` outside the guard

**AC-2 (Integration): Catalog filters by chassis**
- Given: Test Addressables group with 2 Scout cards and 1 Assault card, all tagged `card-definitions`
- When: `GetByChassis(ChassisType.Scout)` is called after `LoadAsync()` completes
- Then: Returns exactly 2 cards; both have `ChassisPool == Scout`
- Edge cases: `GetByChassis(ChassisType.Assault)` returns exactly 1 card

**AC-3 (Integration): GetById returns correct asset**
- Given: Test group containing asset with `CardId="scout_precision_001"`
- When: `GetById("scout_precision_001")` is called
- Then: Returns non-null; `result.CardId == "scout_precision_001"`
- Edge cases: `GetById("nonexistent_id")` returns null without throwing

**AC-4 (Advisory): IL2CPP strip survival**
- Setup: Standalone IL2CPP build with ManagedStrippingLevel=High; one `CardDefinitionSO` with a `DamageEffect` carrying one `PositionCondition`
- Verify: Dev-build assertion passes: `Effects.Count > 0` AND `DamageEffect.Conditions.Count > 0`
- Pass condition: Build log shows no stripping warnings for card types; assertion output saved to evidence file

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/card-system/addressables-catalog_test.cs` — EditMode integration test must exist and pass
- Code review diff showing try/catch/finally handle guard
- `production/qa/evidence/story-004-il2cpp-smoke-[date].txt` (ADVISORY — required for epic DoD, not story completion)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003 must be DONE (`CardDefinitionSO` must be authored and importable)
- Unlocks: Story 005 (RewardDrawAlgorithm uses the catalog as its data source)
