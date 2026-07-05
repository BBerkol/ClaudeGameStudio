# Slice 2.5A — Data-Model Foundation (Atomic Commit)

**Epic:** Parts Progression Axis (Phase 2.5)
**Slice:** 2.5A — Data-Model Foundation
**Type:** Atomic commit (all 6 pieces or none)
**Estimated time:** 1 week
**Depends on:** ADR-0017 Proposed → Accepted, ADR-0012 amendment landed (both in this slice's commit)
**Blocks:** All of Phase 2.5B–2.5I

## Purpose

Land the six data-model changes that unblock every downstream Phase 2.5 slice. One atomic commit; ADR-0017 flips Proposed → Accepted and ADR-0012 amendment ships in the same commit. Non-atomic split re-opens the `InstallPart(armorContribution=0)` default-param semantic trap between commits.

## The six pieces (atomic commit contents)

### Piece 1 — `SlotKind.Bodywork` enum extension

**File:** `Assets/Scripts/Combat/SlotKind.cs`

Add `Bodywork` as the 7th enum value after `Exposable`. Update `SlotKindExtensionsV2.CategoryLabel` switch with `case SlotKind.Bodywork: return "Bodywork";` — the default arm throws `ArgumentOutOfRangeException`, so unmigrated call sites fail loudly at first use (not silently).

**Xmldoc for the new value:** cosmetic-only in combat per ADR-0017; contributes to sum-of-parts armor via `ArmorContribution`; installed/swapped only on RunMap between beacons or at Chopshop.

### Piece 2 — Exhaustive-switch audit fixes

Every site listed in ADR-0017's audit checklist must get an explicit `Bodywork` arm:

| Site | File | Bodywork treatment |
|---|---|---|
| CategoryLabel | `SlotKind.cs` | Return `"Bodywork"` |
| SaveSchemaRegistry / DTO round-trip | `Assets/Scripts/Save/Dtos/SlotSnapshotDto.cs`, `VehicleStateDto.cs` | Serialize normally; state never transitions in combat, snapshot is authored-time invariant |
| Damage routing | `Assets/Scripts/Combat/DamagePipeline.cs` (or wherever `Apply` lives) | Explicit early-return before damage applied; Bodywork is not a valid damage target |
| Sum-of-parts recompute | `Assets/Scripts/Combat/Vehicle.cs` `RecomputeArmorPool` | Bodywork slots included in ArmorContribution sum (they contribute at build; do not state-transition; stay non-Offline throughout combat) |
| HUD bars | `Assets/Scripts/CombatView/VehicleBarStack.cs` + HUD subscribers | Skip Bodywork (no bar rendered) — must be an explicit filter, not a silent default |
| Prefab authoring | `Assets/Editor/CombatPrefabAuthor.cs` + chassis authors | No-op for now (no Bodywork parts to author until Piece 4/6); Piece is authoring surface addition for 2.5B |

Compile-time gate: after Piece 1, every `switch (SlotKind)` site that doesn't cover Bodywork throws at first-use of the new enum value. Migration is forced by the compiler + runtime throws, not by grep.

### Piece 3 — `FrameLayoutSO` field on `VehicleDefinitionSO` + `SmallFrameLayout` deletion

**Files:**
- `Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs` — add `[SerializeField] private FrameLayoutSO _frameLayout;` field; `BuildVehicle()` line 42 passes `_frameLayout` instead of `SmallFrameLayout.Instance`.
- `Assets/Scripts/Combat/Archetypes/SmallFrameLayout.cs` — **DELETE** the file. Singleton is retired. No "kept for tests" compat overload.

**Migration for tests:** any test that used `SmallFrameLayout.Instance` migrates to a `FrameLayoutSO` asset reference (or `ScriptableObject.CreateInstance<FrameLayoutSO>()` in-memory fake with the fields populated). Test-side migration count depends on grep count of `SmallFrameLayout.Instance` — expected to be low since most tests build Vehicles via TestVehicleFactory or `VehicleDefinitionSO.BuildVehicle`.

**Existing Scout `VehicleDefinitionSO` asset** (e.g. `Vehicle_Scout.asset`): the `_frameLayout` field must be populated with the Scout FrameLayoutSO asset. Needs a one-time asset edit or a `CombatDataInitializer` bake pass.

**Note on enemy archetypes** (Dredge, IronShepherd, DuneSkimmer): they use code-defined `IFrameLayout` singletons (`DredgeFrameLayout`, etc.), NOT `SmallFrameLayout`. Those stay code-defined per ADR-0015 authoring narrowing — out of scope for this slice.

### Piece 4 — `MountDirection : SlotPosition` field on `PartDefinitionSO`

**File:** `Assets/Scripts/Combat/PartDefinitionSO.cs`

Add `[SerializeField] private SlotPosition _mountDirection = SlotPosition.Any;` field with `public SlotPosition MountDirection => _mountDirection;` accessor. Reuse existing `SlotPosition` enum (`Any`/`Front`/`Back`) from `Assets/Scripts/Combat/SlotPosition.cs`. **Do NOT introduce a `WeaponPosition` enum** — closes ADR-0007 xmldoc pre-declared drift.

**Slot-install validation:** when installing a `PartDefinitionSO` into a slot, the slot's `SlotDefinition.Position` must be compatible with the part's `MountDirection`. Compatibility rule:
- `MountDirection == Any` → installs into any slot regardless of `Position`
- `MountDirection == Front` → installs only into slots with `Position == Front`
- `MountDirection == Back` → installs only into slots with `Position == Back`

Validation lives in `Vehicle.InstallPart(SlotId, PartDefinitionSO)` — throw `InvalidOperationException` on mismatch (exception-based validation per ADR-0002). Slice 2.5D adds the card-play predicate downstream.

### Piece 5 — `StatModifier` struct + `StatKind` enum (initial vocabulary)

**Files (new):**
- `Assets/Scripts/Combat/StatKind.cs`
- `Assets/Scripts/Combat/StatModifier.cs`

**StatKind initial vocabulary (Phase 2.5A M1 set):**
```csharp
public enum StatKind
{
    DodgeRate,       // percent; +5% = 5f
    CritRate,        // percent
    ArmorRatingBonus // flat armor rating add
}
```

Expands via playtest per parts-axis memory (`project_parts_axis_in_1_0.md`). Additional candidates for M2 (**do not add now**): weapon-damage flat/percent modifiers, engine-speed modifiers, mobility-based dodge, hull-based DR.

**StatModifier struct:**
```csharp
[Serializable]
public struct StatModifier
{
    public StatKind Kind;
    public float Value;
}
```

`PartDefinitionSO` gets `[SerializeField] private List<StatModifier> _statModifiers;` with `public IReadOnlyList<StatModifier> StatModifiers => _statModifiers;` accessor.

**No StatModifier aggregation in this slice.** Vehicle-level stat totals surface in Slice 2.5E (Inventory UI) — this piece establishes the authoring shape only.

**Guard: `ArmorContribution` stays a top-level field on `PartDefinitionSO`, NOT folded into `StatModifiers`.** Avoids the bimodal aggregator (sum-of-parts recompute vs. stat aggregation) that would violate ADR-0011.

### Piece 6 — ADR-0012 amendment: `InstallPart(armorContribution=0)` default REMOVED

**File:** `Assets/Scripts/Combat/Vehicle.cs`

Change signature from:
```csharp
public void InstallPart(string slotId, int maxHp, int armorContribution = 0);
```
to:
```csharp
public void InstallPart(string slotId, int maxHp, int armorContribution);
```

Migration:
- Enemy archetypes (`Dredge` 8 calls, `IronShepherd` 5, `DuneSkimmer` 4 — ~14 semantic calls): mechanical `, 0` append at 2-arg sites.
- Tests (~170 calls across ~40 files): mechanical `, 0` append. `TestVehicleFactory` is the natural funnel — updating the factory covers many test suites indirectly.
- `RunSceneHost` (5 calls): mechanical `, 0` append.
- `VehicleDefinitionSO.BuildVehicle` uses the SO overload — unaffected.
- `SlotInstance.cs` (1 call): review — is this a self-call or an escalation? Check and update.

Compile-error gate: no 2-arg call site survives the commit. If any test file is skipped, its 2-arg calls throw at first `dotnet build` or Unity domain reload.

## Order of operations within the atomic commit

1. **Piece 1** — enum extension. Sanity: `dotnet build` still succeeds (`CategoryLabel` switch update landed same edit).
2. **Piece 2** — exhaustive-switch audit fixes (all 6 sites). Sanity: `dotnet build` still green.
3. **Piece 6** — `InstallPart` signature change + all ~200 call-site migrations. Sanity: `dotnet build` green, EditMode test suite green.
4. **Piece 3** — `FrameLayoutSO` field on `VehicleDefinitionSO` + `SmallFrameLayout.cs` deletion + Scout asset field population + test migration to `FrameLayoutSO` fakes. Sanity: `dotnet build` green, all tests green.
5. **Piece 4** — `MountDirection` field on `PartDefinitionSO` + slot-install validation. Sanity: `dotnet build` green, install tests pass with mismatched-direction throws.
6. **Piece 5** — `StatKind` enum + `StatModifier` struct + `_statModifiers` list on `PartDefinitionSO`. Sanity: `dotnet build` green (no behavioral tests yet — Slice 2.5E adds them).
7. **ADR flips** — ADR-0017 Status `Proposed` → `Accepted`, `Last Verified` bumped. ADR-0012 amendment already inline from prior write, no further change.

**One commit** at the end — do not push intermediate commits. If any piece fails, back up, redo the affected piece, and commit as one whole.

## Test evidence expected

**Automated (BLOCKING):**
- EditMode green (existing test suite passes with all `InstallPart` migrations).
- New EditMode test — `MountDirection_MismatchedInstall_Throws`: install a `MountDirection = Front` PartDefinitionSO into a `Position = Back` slot → expect `InvalidOperationException`.
- New EditMode test — `Bodywork_TakesDamage_Skipped`: apply damage to a Bodywork slot via `DamagePipeline.Apply` → expect 0 HP change, 0 events raised.
- New EditMode test — `Bodywork_ContributesToArmorPool`: build a vehicle with a Bodywork part authored `ArmorContribution = 3` → expect `armor_0.MaxHp` sum to include the 3.
- New EditMode test — `SmallFrameLayout_Removed_TestMigration`: sanity smoke that the deleted `SmallFrameLayout.Instance` symbol is gone (compile fails if reintroduced).

**Manual (advisory):**
- Unity Editor domain reload: no console errors.
- Scout `VehicleDefinitionSO` asset opens in Inspector with `_frameLayout` field populated.

## Success criteria

- [ ] All 6 pieces landed in one commit.
- [ ] EditMode test suite green.
- [ ] `dotnet build` clean, no warnings for `SlotKind` incomplete switches.
- [ ] `SmallFrameLayout.Instance` symbol does not appear anywhere in the codebase (CI grep gate — add if not present).
- [ ] Every `switch (SlotKind)` site has an explicit `case SlotKind.Bodywork` arm (visual audit + throw safety net).
- [ ] `PartDefinitionSO.MountDirection` field grep confirms no new enum type introduced (reuses `SlotPosition`).
- [ ] ADR-0017 Status flipped to `Accepted`, `Last Verified` bumped to commit date.
- [ ] ADR-0012 Amendment section present and consistent with committed code.

## Out of scope (defer to later 2.5 slices)

- Any Bodywork PartDefinitionSO authored assets → 2.5B (chassis refactor + first Bodywork parts on Scout).
- `VehicleBodyworkAnchors` component + Scout prefab authoring → 2.5B.
- Card `PlayableAt : LanePosition?` field → 2.5D.
- Card-play predicate check against `Vehicle.Pos` → 2.5D.
- `RunInventory` POCO + DTO → 2.5C.
- `Vehicle.SwapPart` damage-ratio-preserve → 2.5C.
- `IPartRewardSource` seam + `PartOfferSeedMix` → 2.5C.
- Any UI (Inventory / Deck viewer / RunMap viewer) → 2.5E / 2.5F.
- Chassis 2 + 3 authoring → 2.5G.
- `MasteryStateDto` + `PartDiscoveryStateDto` → 2.5H.
- Codex → 2.5I.

## Risk callouts

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| ~200 test call-site migrations introduce a semantic error at a corner-case test | LOW | MEDIUM (test failure, not gameplay bug) | TestVehicleFactory funnel + compile-error gate + full EditMode test-suite run before commit |
| A `switch (SlotKind)` site missed the audit and swallows Bodywork silently | LOW | HIGH (save corruption, invisible parts) | `CategoryLabel` default-arm throw + audit-checklist grep before commit |
| Scout `VehicleDefinitionSO` asset `_frameLayout` field left null | MEDIUM | HIGH (null-ref at first Vehicle build) | Editor bake pass in `CombatDataInitializer` populates the field on first import + assertion in `BuildVehicle` |
| `SmallFrameLayout.cs` deletion leaves an orphan meta file | LOW | LOW (Unity import warning) | Delete both `.cs` and `.cs.meta` in the commit |
| Atomic-commit-too-large review latency | MEDIUM | LOW (review time cost, not blocker) | TD pre-approved via ADR-0017 Round 4 + this slice brief pre-approved via user "do in order" directive |

## Post-slice handoff

Slice 2.5B — Chassis pattern refactor, `VehicleBodyworkAnchors` component, first 5 Bodywork parts on Scout (Door / Bumper / Canister / Visor / Light). Capture-before-destroy required for `VehicleVisual` composition change per ADR-0017 alternative 3 rejection.
