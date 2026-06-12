# ADR-0010 Phase Plan — Combat Slot System Retirement

**Status:** UNBLOCKED 2026-05-31 — ADR-0011 landed, audit complete, framework `src/` reverted (commit `3e96291`). Ready to start Phase 0.

**Why parked (historical):** User directive 2026-05-31 expanded scope from "combat slot system only" to project-wide ("the whole game"). The retirement plan below is correct as-is and unchanged — but the meta-rule (ADR-0011) needed to land first so the slot retirement becomes the first concrete *application* of the rule, not an isolated cleanup.

**Originating session:** 33194a33-4e0b-4809-bb6c-a85c2feb9508 (2026-05-31).

## 2026-05-31 amendments (post-audit)

Two findings from `docs/architecture/no-bridges-audit-2026-05-31.md` fold into this plan:

**Amendment A — VehicleDefinitionSO rebuild BLOCKED on Part SO design (revised 2026-05-31 mid-execution).**
`Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs` lines 39–43 carry five hardcoded `SlotSpec` fields named by the legacy 5-shape vocabulary (`_machineGun`, `_flamethrower`, `_engine`, `_wheels`, `_frame`). `BuildVehicle()` lines 57–61 map those legacy field names onto modern `slotId` strings. `SlotSpec.ArmorContribution` is serialized but IGNORED at build time.

_Initial direction (retired 2026-05-31 same-day)_: rebuild around `List<SlotSpec>` where each spec carries its own `slotId`. Phase 1 step 2 shipped briefly under this shape (`SlotSpec { SlotId, MaxHp, ArmorContribution }`) and was reverted same session after the design correction below.

_Revised direction_: Per user directive 2026-05-31, **slots do not conceptually carry Hp / ArmorContribution** — the player chassis derives those values from the parts installed into each slot. No `PartDefinitionSO` (or equivalent part data shape) exists in code yet; `Assets/Resources/VehicleParts/` is art-only. The correct SO shape (`List<{ SlotId, PartRef }>` driving `InstallPart(slotId, partDef)`) is therefore blocked on a separate Part SO data system ADR.

Status until Part SO ADR lands:
- `VehicleDefinitionSO` stays in its legacy-named-fields shape (recognized bridge, ADR-0011 classification pending real rebuild).
- `Vehicle_Scout.asset` may be absent — `CombatController.BuildScout()` static fallback (lines 284–294) covers it cleanly.
- Enemies remain on static `EnemyArchetypes.BuildVehicle()`; "configure from prefab" is future work.
- `SlotIconRegistry` (Phase 1 step 1, independent of part-data question) shipped 2026-05-31.
- Phase 5 `SlotSpec.ArmorContribution` deletion depends on the Part SO rebuild having landed first.

**Amendment B — Framework `src/` reverted, not migrated.**
Commit `4a6e5f9` (engine-free POCO foundation) was reverted as `3e96291` on 2026-05-31. Its 4-slot compile-time `SlotType` enum was structurally incompatible with V1 Stage A's 9-slot Dredge. ADR-0010 stays Unity-side-only as originally planned — no cross-repo consolidation. Do not re-attempt a framework-canonical migration; the variable-N slot model lives entirely in `Assets/Scripts/`.

**See also:**
- Memory: `project_no_bridges_at_done.md` (project-wide directive)
- Memory: `feedback_pre_author_capture_protocol.md` (re-author discipline for Phase 1)
- Consumer survey (counted 2026-05-31): 1,081 `LegacySlotKind` occurrences across 72 files; 39 `LegacyKindBridge` field references across 10 files; 254 bridge entry-point calls across 37 files; 44 legacy-mode `new Vehicle(name)` test constructor sites across 27 test files.

---

## Design (locked, ready to write into ADR-0010 when resumed)

**Four principles:** one vocabulary per concept, one storage per piece of state, one path per operation, no fixed-N hardcoding for variable-N data.

**Ten sign-off decisions, ALL ACCEPTED by user implicitly via "make a clean system" directive:**

1. Slot identifier: free `string slotId`.
2. Plate distribution: fill-first-non-full Armor slot in slot-list order.
3. Icon mapping: flat global `SlotIconRegistry` SO.
4. Card targeting: SlotKind filter at authoring, runtime resolves to specific slotId at play time.
5. Intent targeting: per-vehicle slotId binding hand-authored against target layout.
6. MaxArmor naming: rename `EffectiveMaxArmor` → `MaxArmor` (drop `Effective` prefix once legacy pool is gone).
7. Structural slot: enforce exactly one `IsStructural=true` slot per FrameLayout via validation.
8. `VehiclePartHitZone` tag: `slotId` string only (no SlotKind).
9. `PlateArmor` signature: kind-broad `PlateArmor(int amount)` distributing across Armor slots per decision #2.
10. One-shot data migrators allowed (run once, deleted in same slice). The only "bridge-shaped" code allowed, and it is temporary by definition.

---

## Phase 0 — Lock design via ADR-0010

Write `docs/architecture/adr-0010-slot-system-single-vocabulary.md`. Marks ADR-0009 (and its Amendment) superseded. Encodes the four principles and ten decisions above as binding contract. Adds CI grep gate planning targets.

## Phase 1 — View layer rebuild

- ✓ **Step 1 SHIPPED 2026-05-31:** `SlotIconRegistry` SO created (`Assets/Scripts/CombatView/Data/SlotIconRegistry.cs`). Asset instance and consumer rewiring deferred to step 7.
- Replace `VehicleVisual`'s 5 hit-zone SerializeField fields with `GetComponentsInChildren<VehiclePartHitZone>` discovery.
- Replace `VehicleBarStack._subsystemPairs[]` fixed-N array with dynamic `Vehicle.Slots` iteration.
- Replace `VehiclePartTint` hardcoded SlotKind polls with dynamic enumeration.
- `MainBarWidget` / `CardWidget` / `IntentWidget` → slotId/SlotKind reads only.
- `VehiclePartHitZone` swap from LegacySlotKind to slotId.
- ⛔ **VehicleDefinitionSO rebuild BLOCKED (Amendment A revised):** Player slots don't conceptually carry Hp / ArmorContribution — those values come from installed parts. No `PartDefinitionSO` exists in code yet. SO stays in legacy-named-fields shape until a Part SO data system ADR is authored. Phase 1 step 2 shipped briefly under the wrong `List<SlotSpec { SlotId, MaxHp }>` shape on 2026-05-31 and was reverted same session.
- **One-shot data migrator (prefabs)** runs over the 4 vehicle prefabs translating SerializeField LegacySlotKind values to slotIds. Deleted after the slice.
- ~~One-shot data migrator (VehicleDefinitionSO `.asset` files)~~ — out of scope until Part SO design lands.
- Re-author all 4 vehicle prefabs (pre-author capture protocol per memory — 4 explicit checkpoints).

## Phase 2 — Production code slotId migration

- CombatLoop, DamagePipeline, IntentPool, WeightModifier, CardPlayResult, EnemyIntent, EnemyTurnResult, SlotType, ScriptedSequenceBrain, AdaptiveSequenceBrain, SelfRepairBrain — internal `LegacySlotKind` calls converted to slotId.
- 3 archetype intent definitions (DuneSkimmer, IronShepherd, Dredge) rewritten with slotId targeting.
- ⛔ **VehicleDefinitionSO.BuildVehicle() rewrite BLOCKED (Amendment A revised):** depends on Part SO design landing first. SO stays in its legacy field shape and `CombatController.BuildScout()` fallback covers gameplay when the asset is absent.
- One-shot `CardDefinitionSO` data migrator runs over `.asset` files.

## Phase 3 — Vehicle simplification

- Delete `_maxArmor` / `_currentArmor` fields on Vehicle.
- Delete `IsLayoutMode` / `IsLegacyMode` branches.
- Delete legacy `new Vehicle(name)` ctor overload.
- Delete `MaxArmor`/`CurrentArmor` legacy property reads.
- Rename `EffectiveMaxArmor` → `MaxArmor`, `EffectiveCurrentArmor` → `CurrentArmor`.
- Delete `Slot.ArmorContribution`.
- Delete `SlotSpec.ArmorContribution` field on `VehicleDefinitionSO` ONLY if the Part SO rebuild has landed (Amendment A revised). If the SO is still in its legacy shape at Phase 5 time, this bullet defers — the legacy SO is itself an unresolved bridge tracked separately.

## Phase 4 — Test migration

- 27 test files: rewrite legacy `new Vehicle(name)` → `new Vehicle(name, layout)` with appropriate layout per test.
- Audit MaxArmor assertions against new slot-sum derivation.
- Delete tests that explicitly tested the bridge (e.g., `CombatLoopSlotIdSurfaceTests.cs:171` `legacyEnemy` paths, `SmallFrameLayoutTests.cs:298` `legacy` path, `DamagePipeline_R_ARM_Tests.cs:321` `Legacy`).

## Phase 5 — Demolition

- Delete `LegacySlotKind.cs` enum file.
- Delete `SlotDefinition.LegacyKindBridge` field.
- Delete `legacyKindBridge:` arguments in 4 FrameLayouts (Tiny/Small/Hauler/Dredge).
- Delete `armorContribution` field if it survives anywhere.
- CI grep gate: zero `LegacySlotKind` / `LegacyKindBridge` / `IsLegacyMode` / `_maxArmor` references in the codebase.

## Phase 6 — Doc + comment scrub

- ADR-0009 final amendment marking it superseded by ADR-0010.
- Walk 10 bridge-defining files; scrub "transitional" / "bridge" / "slice 2.7" comments.
- Update `production/session-state/active.md` to remove invalidated-section warnings about bridge.

---

## When resuming

1. ✓ ADR-0011 has landed (the meta-rule that justifies this slice's existence).
2. ✓ Project-wide audit punch-list under `docs/architecture/no-bridges-audit-2026-05-31.md` is complete and ADR-0010 is the first item to action.
3. ✓ Framework `src/` revert landed as `3e96291` — no cross-repo work needed, ADR-0010 stays Unity-side-only per Amendment B.
4. Re-run the consumer survey to confirm counts haven't drifted (expect ~1,081 / 72; if bigger, drift creep happened and we surface to user). Also count `VehicleDefinitionSO` `.asset` consumers (`SlotSpec` field references) for migrator sizing.
5. Start at Phase 0 — write ADR-0010 from the design captured above, including the two 2026-05-31 amendments.
6. After Phase 6 completes, the next system on the audit punch-list begins its own retirement slice using ADR-0010's six-phase pattern as the template.
