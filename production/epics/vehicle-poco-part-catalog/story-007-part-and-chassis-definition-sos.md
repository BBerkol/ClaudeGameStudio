# Story 007: PartDefinitionSO + ChassisDefinitionSO + FrameLayoutSO + Validators

> **Epic**: Vehicle POCO + Part Catalog
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Manifest Version**: 2026-05-25

## Context

**GDD**: `design/gdd/vehicle-and-part-architecture.md` + `design/gdd/vehicle-and-part-mechanics.md`
**Requirement**: `TR-vehicle-002`, `TR-vehicle-006`, `TR-vehicle-007`, `TR-vehicle-022`, `TR-vehicle-023`

**ADR Governing Implementation**: ADR-0005 (amended by **ADR-0007 — Frame-Driven Variable Slot System**)
**ADR Decision Summary**: `PartDefinitionSO`, `ChassisDefinitionSO`, **`FrameLayoutSO`** (new per ADR-0007), and `StatModifierSO` are ScriptableObjects in `WastelandRun.Gameplay`. Use `[field: SerializeField]` for auto-property serialisation (Unity 6.3 fields-only rule). `OnValidate` runs Editor-only with the **R11 expanded ruleset** (ADR-0007): rejects duplicate SlotIds, zero structural slots, Armor `RedirectsTo` pointing at non-existent or non-protectable kind, incompatible chassis allowlist with mounting requirements, GrantedCards count mismatch with rarity, etc. `IL2CPP link.xml` preserves Newtonsoft + reflection-touched types.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ScriptableObject + `[field: SerializeField]` is Unity 6.3-specific (Unity 6.3 made `[SerializeField]` fields-only — must use `[field: SerializeField]` for auto-implemented properties). `OnValidate` is editor-only; runtime `LayoutNotValidatedThisSessionException` enforces re-validation on every game session via a `[RuntimeInitializeOnLoadMethod]` that scans all loaded FrameLayoutSOs and stamps each with `_validatedThisSession = true` after `IFrameLayout.Validate()` succeeds.

**Control Manifest Rules (this layer)**:
- Required: `[field: SerializeField]` for auto-property serialisation on SOs — source: engine-reference Unity 6.3 breaking-changes
- Required: `OnValidate` is Editor-only (`#if UNITY_EDITOR` guard) — source: ADR-0007
- Required: `FrameLayoutSO` implements `IFrameLayout` — source: ADR-0007
- Required: R11 ruleset enforced by `OnValidate` and `IFrameLayout.Validate()` — source: ADR-0007
- Forbidden: Storing derived state on SOs (DamageState, CurrentHp, etc.) — source: ADR-0005
- Forbidden: `OnValidate` logic running at runtime — source: ADR-0007

---

## Acceptance Criteria

- [ ] **AC-1** *(TR-006)*: `PartDefinitionSO` declares: `[field: SerializeField] PartId Id`, `SlotKind Kind`, `IReadOnlyList<ChassisFamily> CompatibleChassis`, `Rarity Rarity`, `IReadOnlyList<CardData> GrantedCards`, `IReadOnlyList<StatModifier> StatModifiers`, `int MaxPlating`, `int ArmorContribution`, `bool CanReviveOffline`, `PositionRequirement? PositionRequirement`, `MountDirection MountDirection`, `int BaseMaxHp`.
- [ ] **AC-2** *(TR-022)*: `ChassisDefinitionSO` declares: `[field: SerializeField] ChassisFamily Family`, `FrameLayoutSO Layout` (assignable in editor), `float DegradedThreshold` (default 0.5), `IReadOnlyList<PartId> StarterParts`, `IReadOnlyList<CardData> StarterDeck`, `IReadOnlyList<ChassisFamily> PrimaryFamilies`, `float CriticalThreshold` (default 0.3).
- [ ] **AC-3** *(NEW, ADR-0007)*: `FrameLayoutSO` is a ScriptableObject implementing `IFrameLayout`. Declares: `[field: SerializeField] string FrameId`, `IReadOnlyList<SlotDefinition> SlotDefinitions`. Provides `Validate()` per the R11 ruleset (AC-7).
- [ ] **AC-4** *(TR-025)*: `StatModifierSO` declares: `[field: SerializeField] TargetStat TargetStat`, `StatOperation Operation`, `float Value`. Provides `StatModifier ToValue()` to emit the engine-free `StatModifier` value type from story 001.
- [ ] **AC-5** *(TR-007)*: `OnValidate` on `PartDefinitionSO` enforces GrantedCards count by Rarity: Common=1, Uncommon=2, Rare=3, Legendary=3. Editor-only `Debug.LogError` with asset name on violation.
- [ ] **AC-6** *(TR-002, TR-023)*: `ChassisDefinitionSO` no longer enforces fixed 4 slots — the slot count is owned by `FrameLayoutSO`. The standard families' starter layouts ship with at least one slot per `SlotKind { Weapon, Engine, Mobility, Hull }`; Armor slots are optional and frame-specific (e.g., Scout: 1 Hull (16 HP) + 1 Weapon + 1 Engine + 1 Mobility, total non-Armor HP 50; Assault: 1 Hull (24 HP) + 1 Weapon + 1 Engine + 1 Mobility + 1 Armor (8 HP, RedirectsTo "hull_main"), total Hull HP 24, total all-slot HP 58). Verified by FrameLayoutSO assets in `assets/Definitions/Vehicles/Frames/`.
- [ ] **AC-7** *(R11 ruleset on `IFrameLayout.Validate`)*: `Validate()` throws `FrameLayoutInvalidException` on any of:
  1. Duplicate `SlotId` across `SlotDefinitions`.
  2. Zero slots flagged `IsStructural = true`.
  3. An Armor `SlotDefinition` whose `RedirectsTo` does not match any non-Armor SlotId.
  4. An Armor `SlotDefinition` whose `RedirectsTo` points to another Armor slot.
  5. A `SlotDefinition` with `MaxHp <= 0`.
  6. A `SlotDefinition` with `ExposureMultiplier <= 0`.
  7. A `PositionRequirement` on a `SlotDefinition` (slots do not have requirements; only Parts do — explicit reject).
- [ ] **AC-8**: `OnValidate` on `FrameLayoutSO` runs the same R11 ruleset and logs Editor errors on each failure (does not throw inside OnValidate — Editor would gag).
- [ ] **AC-9**: Runtime `[RuntimeInitializeOnLoadMethod]` calls `Validate()` on every loaded `FrameLayoutSO`. Failure throws `FrameLayoutInvalidException` with the offending FrameId in the message, stopping startup.
- [ ] **AC-10**: `LayoutNotValidatedThisSessionException` (from story 001) is thrown by `ApplyDamage` when invoked with a `Vehicle.Layout._validatedThisSession == false`. The `[RuntimeInitializeOnLoadMethod]` sets the flag after Validate passes.
- [ ] **AC-11**: IL2CPP `link.xml` includes preservation entries for: `PartDefinitionSO`, `ChassisDefinitionSO`, `FrameLayoutSO`, `StatModifierSO`, `Newtonsoft.Json`, and all DTO types from ADR-0004 save layer. Verified by `link.xml` file content + IL2CPP smoke test.
- [ ] **AC-12**: SO authoring assets live at `assets/Definitions/Vehicles/Parts/`, `assets/Definitions/Vehicles/Chassis/`, `assets/Definitions/Vehicles/Frames/`. Naming: `Part_[Family]_[Slot]_[Name].asset`, `Chassis_[Family].asset`, `Frame_[Family].asset`.

---

## Implementation Notes

- `[field: SerializeField]` example:
  ```csharp
  [field: SerializeField] public PartId Id { get; private set; }
  ```
- `OnValidate` runs in the Editor only; wrap in `#if UNITY_EDITOR` if you call any Editor-only API. Logging `Debug.LogError(message, this)` highlights the asset in the inspector.
- `Validate()` (on `IFrameLayout`) is runtime-safe; it throws. Different contract from `OnValidate` (Editor-only, logs).
- The `_validatedThisSession` flag on `FrameLayoutSO` is non-serialised (`[NonSerialized]`), so it always starts `false` per game session — guarantees runtime validation runs at least once.
- For test setup convenience, expose `internal void MarkValidatedForTests()` so unit tests can bypass `[RuntimeInitializeOnLoadMethod]`.

---

## Out of Scope

- Card content (CardData) — Card Combat assembly.
- Stat composition pipeline (`(base + Add) × Multiply` with Override) — consuming systems (Card Combat for Damage, Map for Speed, etc.) implement composition; this story only emits the modifiers.
- Editor inspector polish — Tools epic, separate story.
- Frame visual representation (sprites, anchor display) — view layer.

---

## QA Test Cases

**Integration story — automated test specs (live under `tests/integration/vehicle/definitions_test.cs` + Editor tests under `tests/editor/vehicle/onvalidate_test.cs`):**

- **AC-1 / AC-2 / AC-3 / AC-4**: SO field shape via reflection
  - Given: typeof(PartDefinitionSO/ChassisDefinitionSO/FrameLayoutSO/StatModifierSO)
  - When: properties enumerated
  - Then: each declared field is present with the expected type
  - Edge cases: every field has `[field: SerializeField]` attribute (verified via reflection)

- **AC-5**: GrantedCards count by rarity
  - Given: a PartDefinitionSO with Rarity=Rare and 2 GrantedCards (should be 3)
  - When: OnValidate runs
  - Then: Debug.LogError invoked with asset name + the count mismatch
  - Edge cases: Common with 0 cards → error; Legendary with 4 cards → error

- **AC-6**: per-chassis frame layouts
  - Given: shipped Frame_Scout.asset and Frame_Assault.asset
  - When: loaded
  - Then: Scout has 4 slots (no Armor), Hull MaxHp = 16; Assault has 5 slots (one Armor RedirectsTo "hull_main"), Hull MaxHp = 24

- **AC-7**: R11 ruleset
  - Given: malformed layouts (duplicate slot, zero structural, invalid Armor redirect, etc.)
  - When: `Validate()` called
  - Then: FrameLayoutInvalidException with rule-specific message
  - Edge cases: each of the 7 R11 rules tested in isolation; combinations tested too

- **AC-9**: runtime init validates layouts
  - Given: a project with one invalid FrameLayoutSO loaded
  - When: runtime initialisation fires (simulate via direct call)
  - Then: throws on startup with FrameId

- **AC-10**: LayoutNotValidatedThisSessionException
  - Given: a Vehicle built bypassing the runtime init (e.g., test that doesn't call MarkValidatedForTests)
  - When: ApplyDamage invoked
  - Then: throws LayoutNotValidatedThisSessionException
  - Edge cases: after MarkValidatedForTests, ApplyDamage proceeds normally

- **AC-11**: link.xml preservation
  - Given: `link.xml` at the project root
  - When: scanned for preservation entries
  - Then: contains PartDefinitionSO, ChassisDefinitionSO, FrameLayoutSO, StatModifierSO, Newtonsoft.Json, all save-DTO types
  - Edge cases: IL2CPP build succeeds (smoke test in CI)

- **AC-12**: asset path / naming convention
  - Given: Glob over `assets/Definitions/Vehicles/`
  - When: file names checked
  - Then: each matches the documented pattern

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/vehicle/definitions_test.cs` — runtime tests for SO shape, Validate path
- `tests/editor/vehicle/onvalidate_test.cs` — Editor-only OnValidate logging behavior (uses LogAssert)
- `production/qa/evidence/story-007-il2cpp-smoke.md` — sign-off doc capturing the IL2CPP build smoke test result

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Stories 001, 002, 004, 006 (Vehicle types + factory consume these SOs)
- Unlocks: Vehicle authoring workflow (Scout / Hauler / Dredge frames), enemy roster authoring (Biome 1 enemies need PartDefinitionSO + FrameLayoutSO for their bodies)
