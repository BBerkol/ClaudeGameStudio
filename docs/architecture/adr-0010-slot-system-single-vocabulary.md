# ADR-0010: Slot System Single Vocabulary

## Status

**Accepted** (2026-05-31) — supersedes ADR-0007 and ADR-0009 (including any
inline Amendment to ADR-0009).

## Date

2026-05-31

## Last Verified

2026-05-31

## Decision Makers

- User (creative/design lead) — locked the four principles + ten decisions
  in session 33194a33-4e0b-4809-bb6c-a85c2feb9508 (2026-05-31).
- unity-specialist — owns the six-phase execution.
- creative-director — consulted on Amendment B (framework `src/` revert).

## Summary

The Unity combat slot system carries two parallel vocabularies (modern
`SlotKind` + legacy 5-shape `LegacySlotKind`) and bimodal state storage that
ADR-0009's Amendment classified as permanent. ADR-0011's no-bridges meta-rule
makes that classification untenable, and V1 Stage A Dredge needs 9 variable-N
slots that the 4-slot compile-time invariant cannot represent. This ADR locks
a single-vocabulary slotId model, retires `LegacySlotKind`/`LegacyKindBridge`/
`IsLegacyMode`, and sequences the cleanup in six phases.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md` |
| **Post-Cutoff APIs Used** | `[field: SerializeField]` on auto-properties (6.3 rule), `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` (6.0+) |
| **Verification Required** | Re-author all 4 vehicle prefabs after Phase 1 view rebuild; CI grep gate runs Green after Phase 5. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0011 (no-bridges meta-rule); ADR-0001 (visual vehicle part system, art-pipeline scope unaffected); ADR-0005 (vehicle POCO part catalog — the variable-N slot model). |
| **Enables** | V1 Stage B Dredge brain sequencing (cannot ship Cut Chain card on a bimodal vehicle); D1 3-enemy sequence runner; future slot-shape additions without code edits. |
| **Blocks** | Tasks #11 (V6 wheel-bar follow), #12 (D1 runner), #13 (V1 Stage B Dredge) — pending Phase 6 completion. |
| **Ordering Note** | Phases must run in order. Phase 1 (view) and Phase 2 (production) must not interleave — view rebuild commits the SlotIconRegistry boundary, Phase 2 commits the slotId-only call graph; mixed phases leak vocabulary across the boundary. |
| **Supersedes** | ADR-0007 (4-slot visual model); ADR-0009 (Unity Combat migration to ADR-0007) and any inline Amendment classifying `LegacyKindBridge` as permanent. |

## Context

### Problem Statement

The Unity-side combat system runs on two parallel slot vocabularies:

- **Modern** — `SlotKind { Weapon, Engine, Mobility, Hull, Armor, Exposable }`,
  used by `FrameLayoutSO`, `Vehicle.Slots`, and the V1 Stage A boss layouts.
- **Legacy** — `LegacySlotKind { MachineGun, Flamethrower, Engine, Wheels,
  Frame, Exposable1, Exposable2, Javelin }`, used by `VehicleDefinitionSO`,
  `VehiclePartHitZone`, `VehiclePartTint`, the view layer's fixed-N arrays,
  and 27 EditMode test files that construct `Vehicle` via the legacy
  `new Vehicle(string)` ctor.

`SlotDefinition.LegacyKindBridge` translates between them at runtime.
`Slot.ArmorContribution` stubs to `0` in layout mode. `Vehicle` carries both
`_maxArmor`/`_currentArmor` (legacy pool) and per-slot armor (modern), bridged
by `EffectiveMaxArmor`/`EffectiveCurrentArmor` reads. ADR-0009 + its inline
Amendment classified this bridge as **permanent** because removal cost was
estimated higher than tolerance for diff churn.

ADR-0011 (Accepted 2026-05-31) makes that classification untenable: the
no-bridges meta-rule forbids vestigial enums, parallel storage, bimodal
paths, and stub returns at the project's done state. The bridge is now
out-of-contract.

Concurrently, V1 Stage A Dredge (locked 2026-05-31) needs 9 slots:
`weapon_0` MG, `weapon_1` Flame, `weapon_2` Javelin, `engine_0`,
`mobility_0`, `hull_0`, `armor_0`, `slot_exposable_1`, `slot_exposable_2`.
The legacy 5-shape vocabulary cannot name three of these (`weapon_2`,
`slot_exposable_1`, `slot_exposable_2`) without re-extending the enum, which
ADR-0011 also forbids.

### Current State (drift counts, 2026-05-31)

| Symbol | Occurrences | Files |
|---|---|---|
| `LegacySlotKind` | 371 | 40 |
| `LegacyKindBridge` | 14 | (subset of above) |
| `VehicleDefinitionSO` consumers | — | 3 (`CombatDataInitializer.cs`, `CombatController.cs`, the SO itself) |
| `VehicleDefinitionSO` `.asset` instances | 1 (`Vehicle_Scout.asset`) | — |

Counts are smaller than the parked plan's original capture (1,081 / 72) —
intermediate cleanup chipped at it. The six-phase structure is unchanged;
phase scopes proportionally shrink.

### Constraints

- **No new bridges** — ADR-0011 binding. One-shot data migrators are the only
  bridge-shaped code permitted, and they delete themselves in the same slice.
- **Variable-N slots** — `FrameLayoutSO` already declares a `List<SlotDefinition>`;
  the data layer supports it. Code must match.
- **Designer prefab workflow** — User requirement (memory `Bake Designer Edits`
  + `Pre-Author Capture Protocol`): everything must stay prefab-mode-tunable.
  This rules out runtime-only slot construction; SOs and prefabs hold the
  authored data.
- **Test integrity** — 27 legacy-ctor test files must rewrite cleanly without
  losing assertion coverage. Audit MaxArmor assertions against the new
  slot-sum derivation.
- **No UnityEvent in combat** — per `.claude/docs/technical-preferences.md`;
  C# events / Actions only.

### Requirements

- Single vocabulary at runtime: `string slotId` is the only identifier.
- Single storage of armor state: per-slot `armor_0` only; no Vehicle-level
  legacy pool.
- Single path per operation: `Vehicle.PlateArmor(int)` distributes via one
  rule; `DamagePipeline` resolves via one redirection chain; `IntentPool`
  targets via one slotId lookup.
- No fixed-N hardcoding: view layer discovers slots via
  `GetComponentsInChildren<VehiclePartHitZone>` and iterates `Vehicle.Slots`.
- CI-enforced post-Phase 6: forbidden grep tokens listed below produce
  build-time failures.

## Decision

### Four principles

1. **One vocabulary per concept.** The slot identifier is `string slotId`,
   period. `SlotKind` survives only as an authoring-time filter on cards
   ("Weapon-targeting card"). `LegacySlotKind` is deleted.
2. **One storage per piece of state.** Armor lives on `Slot.armor_0` only.
   The Vehicle-level pool (`_maxArmor` / `_currentArmor`) is deleted.
3. **One path per operation.** No bimodal `IsLayoutMode` branches; no
   bridge translations; no stubbed-out `ArmorContribution` returns.
4. **No fixed-N hardcoding for variable-N data.** View arrays sized
   `[5]` are replaced with discovery + iteration; SO fields named after
   5 specific slot shapes are replaced with `List<SlotSpec>`.

### Ten locked decisions

These were sign-off-locked in the 2026-05-31 session and are reproduced
verbatim from `production/adr-0010-phase-plan.md`:

1. **Slot identifier:** free `string slotId`.
2. **Plate distribution:** fill-first-non-full Armor slot in slot-list order.
3. **Icon mapping:** flat global `SlotIconRegistry` ScriptableObject keyed
   by `SlotKind` (not slotId — icons remain shape-based for art reuse).
4. **Card targeting:** `SlotKind` filter at authoring; runtime resolves to
   a specific `slotId` at play time.
5. **Intent targeting:** per-vehicle `slotId` binding hand-authored against
   the target layout (no auto-resolve).
6. **MaxArmor naming:** rename `EffectiveMaxArmor` → `MaxArmor` (drop
   `Effective` prefix once the legacy pool is gone in Phase 3).
7. **Structural slot:** enforce exactly one `IsStructural=true` slot per
   `FrameLayoutSO` via validation. Death lives on this slot's
   `StructuralHp`.
8. **`VehiclePartHitZone` tag:** `slotId` string only (no `SlotKind`,
   no `LegacySlotKind`).
9. **`PlateArmor` signature:** kind-broad `PlateArmor(int amount)`
   distributing across Armor slots per decision #2.
10. **One-shot data migrators allowed:** they run once, delete themselves
    in the same slice, and are the only bridge-shaped code permitted under
    ADR-0011.

### Six-phase execution

| Phase | Title | Scope | Exit Criteria |
|---|---|---|---|
| **0** | Lock design via ADR-0010 | This document. Marks ADR-0007 + ADR-0009 superseded. | This ADR on disk + `Accepted`. |
| **1** | View layer rebuild | `SlotIconRegistry` (✓ shipped 2026-05-31), `VehicleVisual` discovery, `VehicleBarStack` dynamic iteration, `VehiclePartTint` enumeration, `MainBarWidget`/`CardWidget`/`IntentWidget` slotId reads, `VehiclePartHitZone` slotId swap, one-shot prefab migrator, re-author 4 vehicle prefabs. **`VehicleDefinitionSO` rebuild BLOCKED on Part SO design (Amendment A revised).** | All 4 vehicles render correctly in Prefab Mode without entering Play. |
| **2** | Production code slotId migration | `CombatLoop`, `DamagePipeline`, `IntentPool`, `WeightModifier`, `CardPlayResult`, `EnemyIntent`, `EnemyTurnResult`, `SlotType` (legacy class file), `ScriptedSequenceBrain`, `AdaptiveSequenceBrain`, `SelfRepairBrain` → slotId. 3 archetype intent definitions rewritten. One-shot `CardDefinitionSO` data migrator. **`VehicleDefinitionSO.BuildVehicle()` rewrite BLOCKED on Part SO design (Amendment A revised).** | Production code compiles with `LegacySlotKind` references contained to test files only. |
| **3** | Vehicle simplification | Delete `_maxArmor`/`_currentArmor`; delete `IsLayoutMode`/`IsLegacyMode` branches; delete `new Vehicle(string)` legacy ctor overload; delete `MaxArmor`/`CurrentArmor` legacy property reads; rename `EffectiveMaxArmor` → `MaxArmor`, `EffectiveCurrentArmor` → `CurrentArmor`; delete `Slot.ArmorContribution`. | `Vehicle.cs` and `Slot.cs` carry no bimodal state. |
| **4** | Test migration | 27 test files: `new Vehicle(string)` → `new Vehicle(string, FrameLayoutSO)`; audit MaxArmor assertions against slot-sum derivation; delete tests that explicitly tested the bridge (`CombatLoopSlotIdSurfaceTests.cs:171` `legacyEnemy` paths; `SmallFrameLayoutTests.cs:298` `legacy` path; `DamagePipeline_R_ARM_Tests.cs:321` `Legacy`). | EditMode suite Green with zero legacy ctor calls. |
| **5** | Demolition | Delete `LegacySlotKind.cs`; delete `SlotDefinition.LegacyKindBridge` field; delete `legacyKindBridge:` arguments in 4 `FrameLayout` SOs (Tiny/Small/Hauler/Dredge); delete `armorContribution` field if anything survives; **`SlotSpec.ArmorContribution` deletion depends on `VehicleDefinitionSO` Part-SO rebuild having landed (Amendment A revised)**; CI grep gate goes Red on any forbidden token. | CI grep gate Green; symbols below absent from the tree. |
| **6** | Doc + comment scrub | ADR-0007 + ADR-0009 final-superseded amendments. Walk the 10 bridge-defining files; scrub "transitional" / "bridge" / "slice 2.7" comments. Update `production/session-state/active.md` to drop bridge-invalidated-section warnings. | No comment mentions a bridge as a current concept. |

### CI grep gate (Phase 5 acceptance)

Forbidden tokens; any occurrence outside `tests/` or comments fails CI:

- `LegacySlotKind`
- `LegacyKindBridge`
- `IsLegacyMode`
- `_maxArmor` / `_currentArmor`
- `EffectiveMaxArmor` / `EffectiveCurrentArmor`
- `_machineGun` / `_flamethrower` / `_wheels` (VehicleDefinitionSO legacy field names)
- `ArmorContribution` (post-Phase 5)

### 2026-05-31 amendments (self-contained inline)

**Amendment A — VehicleDefinitionSO rebuild BLOCKED on Part SO design (revised 2026-05-31).**
`Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs` carries 5 hardcoded
`SlotSpec` fields named by the legacy 5-shape vocabulary (`_machineGun`,
`_flamethrower`, `_engine`, `_wheels`, `_frame`). `BuildVehicle()` maps those
legacy field names onto modern slotIds. `SlotSpec.ArmorContribution` is
serialized but IGNORED at build time (a stub return, violates the
no-bridges rule).

_Initial direction (retired 2026-05-31)_: Phase 1 rebuilds the SO around
`List<SlotSpec>` where each spec carries its own slotId.

_Revised direction_: Per user directive 2026-05-31, slots do NOT conceptually
carry Hp / ArmorContribution. The player chassis derives those values from
the parts installed into each slot. No `PartDefinitionSO` (or equivalent part
data shape) exists in code yet — `Assets/Resources/VehicleParts/` holds art
only. The correct rebuild shape (`List<{ SlotId, PartRef }>` over
`InstallPart(slotId, partDef)`) is therefore blocked on a separate Part SO
data system ADR. Until that lands:

- `VehicleDefinitionSO` stays in its legacy-named-fields shape as a
  pre-existing bridge (recognized debt, classified pending real rebuild).
- `Vehicle_Scout.asset` may be absent — `CombatController.BuildScout()`
  hardcoded fallback (lines 284–294) covers it cleanly with the same HP
  values the SO holds.
- Enemies remain on static `EnemyArchetypes.BuildVehicle()`. "Configure from
  prefab" is a forward direction, not in this slice.
- `SlotIconRegistry` SO (independent of the part-data question) shipped
  2026-05-31 as Phase 1 step 1.
- Phase 5 still deletes `SlotSpec.ArmorContribution` if the field is still
  present; if the SO has been re-shaped against the Part SO design by then,
  the field is gone in the rebuild.

The bridge classification recorded in ADR-0011 covers this case until the
Part SO ADR is authored and ADR-0010 Phase 1 is reopened for VehicleDefinitionSO.

**Amendment B — Framework `src/` reverted, not migrated.**
Commit `4a6e5f9` (engine-free POCO foundation under `src/WastelandRun.*`)
was reverted as `3e96291` on 2026-05-31. Its 4-slot compile-time `SlotType`
enum (`{ Weapon, Engine, Mobility, Frame }`, "no runtime add/remove") was
structurally incompatible with V1 Stage A's 9-slot Dredge. ADR-0010 stays
Unity-side-only; the variable-N slot model lives entirely in
`Assets/Scripts/`. Future ADRs proposing a framework-canonical migration
must first re-design around variable-N.

## Alternatives Considered

### Alternative 1: Keep the bridge, accept ADR-0009 Amendment as permanent

- **Description**: Classify `LegacyKindBridge` as a permanent translation
  layer. Add `weapon_2`/`slot_exposable_1`/`slot_exposable_2` to
  `LegacySlotKind` to support Dredge. Continue carrying two vocabularies.
- **Pros**: Zero immediate refactor cost.
- **Cons**: Violates ADR-0011 directly. Re-extending `LegacySlotKind`
  produces a vestigial enum that grows. View arrays sized `[5]` would need
  to become `[8]` then `[N]` — same problem twice.
- **Estimated Effort**: Trivial short-term; debt compounds every new slot
  shape.
- **Rejection Reason**: User directive 2026-05-31 ("zero bridges at done")
  + ADR-0011 acceptance forbid this path.

### Alternative 2: Framework-canonical migration (move combat to `src/WastelandRun.*`)

- **Description**: Promote the engine-free POCO foundation from commit
  `4a6e5f9` as the canonical combat layer; delete `Assets/Scripts/Combat/`
  entirely; Unity becomes a thin view over framework types.
- **Pros**: Clean separation; testable without Unity; smaller Unity-side
  surface.
- **Cons**: Framework code's 4-slot compile-time invariant cannot represent
  V1 Stage A Dredge without re-introducing LegacySlotKind-shaped vestiges.
  Cross-repo work would block on framework redesign before slot retirement
  could start.
- **Estimated Effort**: 2× the in-place path due to cross-repo coordination.
- **Rejection Reason**: Surfaced as B → B3 in session 2026-05-31; user
  reverted framework `src/` as commit `3e96291`. Captured as Amendment B
  above so future sessions don't re-attempt this.

### Alternative 3: Phase-collapsed single-PR demolition

- **Description**: One large PR that deletes `LegacySlotKind`, rewrites all
  consumers, re-authors prefabs, and updates tests atomically.
- **Pros**: Eliminates phase-boundary risk; no transient bridge state.
- **Cons**: 371 occurrences across 40 files + 27 test rewrites + 4 prefab
  re-authors + 1 SO rebuild in a single reviewable diff is intractable.
  Prefab Mode verification cannot complete in one sitting.
- **Estimated Effort**: Similar total work, but unreviewable.
- **Rejection Reason**: Phase boundaries are reviewer ergonomics; the cost
  of bridge state across 6 phases is bounded (the bridge already exists).

## Consequences

### Positive

- Single vocabulary at the boundary — designers tune one set of names; code
  reads one set of identifiers.
- Variable-N slot support — V1 Stage A Dredge and any future N-shape vehicle
  ship without code edits.
- CI grep gate prevents regression — vestiges cannot quietly re-appear.
- ADR-0011 gets its first concrete enforcement case, validating the meta-rule.
- Reduced cognitive load: no `IsLayoutMode` branches to reason about.

### Negative

- 4 vehicle prefabs re-author (memory `Pre-Author Capture Protocol` applies —
  4 explicit checkpoints).
- 27 test file rewrites + assertion audit against new slot-sum derivation.
- Two one-shot migrators are temporary code — they exist for one slice each,
  but they exist.
- Phase 6 doc scrub touches comments across 10 files, producing noisy diffs.

### Neutral

- `SlotKind` (modern enum) survives as an authoring-time filter; its
  presence is not a bridge, it's a category.
- Card SOs change shape — designers update once, then ship.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Phase 1 view rebuild leaks slotId into prefabs that Phase 2 hasn't rewritten yet, producing runtime null lookups | MED | HIGH | Phase boundary discipline: view layer commits first, full Prefab Mode verification, then Phase 2 starts. No interleaving. |
| 27 test rewrites drop assertion coverage of the legacy MaxArmor pool by accident | MED | MED | Phase 4 includes explicit "audit MaxArmor assertions against slot-sum derivation" step; sample-check 5 random tests post-rewrite. |
| `Vehicle_Scout.asset` SO migrator corrupts the only existing instance | LOW | MED | Migrator is idempotent + reads-then-writes; commit `.asset` before running; verify via Inspector. |
| Drift between drift-check (371/40) and Phase 1 start | LOW | LOW | Re-run grep at Phase 1 kickoff; if count grew, pause and surface. |
| User authors new content against legacy vocabulary mid-retirement | LOW | MED | Memory `No Bridges At Done — PROJECT-WIDE` already reminds future sessions; ADR-0010 acceptance triggers immediate forbidden-token CI warning (advisory until Phase 5). |

## Performance Implications

Not a performance-driven decision. Removing `IsLayoutMode` branches yields
~0 measurable frame-time impact (branch predictor handles it). The dynamic
`GetComponentsInChildren<VehiclePartHitZone>` call in `VehicleVisual` runs
once at vehicle spawn, not per-frame.

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | ~0.1ms (combat loop) | ~0.1ms | 16.6ms |
| Memory | — | — | 2GB |
| Load Time | — | — | — |

## Migration Plan

The six phases in §Decision are the migration plan. Each phase exits when
its criterion in the phase table holds. The operational backstop is
`production/adr-0010-phase-plan.md` (carries 2026-05-31 Amendments A & B
inline) — that file holds the file-level work list. Phase boundaries are
reviewable; phases do not interleave.

**Rollback plan**: Each phase is committed independently. Rolling back any
phase reverts to the prior phase's vocabulary state. Phase 5 (demolition)
is the point of no return — after `LegacySlotKind.cs` is deleted, rollback
requires restoring from commit history. Recommended: tag the repo at the
Phase 4 → Phase 5 boundary.

## Validation Criteria

- [ ] Phase 1 complete: 4 vehicle prefabs render in Prefab Mode without
      entering Play; `Vehicle_Scout.asset` migrator ran once and was deleted.
- [ ] Phase 2 complete: production code compiles with zero `LegacySlotKind`
      references outside `tests/`.
- [ ] Phase 3 complete: `Vehicle.cs` and `Slot.cs` carry no bimodal storage;
      `MaxArmor` / `CurrentArmor` are direct (not `Effective*`).
- [ ] Phase 4 complete: EditMode test suite Green; zero `new Vehicle(string)`
      legacy ctor calls.
- [ ] Phase 5 complete: CI grep gate Green on the forbidden token list above.
- [ ] Phase 6 complete: no comment in the tree describes the bridge as a
      current concept; ADR-0007 + ADR-0009 marked Superseded.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|---|---|---|---|
| `design/gdd/combat-card-system.md` | Combat / slot vocabulary | Cards target slots by shape filter, resolve to instance at play time | Decision #4: `SlotKind` filter at authoring, slotId resolved at runtime |
| `design/gdd/vehicle-parts-system.md` | Vehicle assembly | Slot shapes are variable per chassis layout | Decisions #1, #7: free slotId + `FrameLayoutSO`-driven slot list |
| `design/gdd/biome-1-enemy-roster.md` (Dredge entry) | V1 Stage A Dredge | 9 slots including `weapon_2` Javelin and 2 Exposable slots | Decisions #1, #8: free slotId + slotId-only HitZone tagging |
| Foundational consequence of ADR-0011 | All systems | No bridges at done state | Six-phase retirement is the first concrete application of ADR-0011 |

## Related

- **Supersedes**: ADR-0007 (4-slot visual model); ADR-0009 + Amendment
  (Unity Combat migration to ADR-0007 classified `LegacyKindBridge` permanent).
- **Depends on**: ADR-0011 (no-bridges meta-rule); ADR-0005 (vehicle POCO
  part catalog).
- **Adjacent (untouched)**: ADR-0002 (card combat POCO state model);
  ADR-0003 (deterministic RNG); ADR-0008 (Addressables — dormant until art
  assets are created).
- **Operational backstop**: `production/adr-0010-phase-plan.md` (file-level
  work list with 2026-05-31 Amendments A & B inline).
- **Audit context**: `docs/architecture/no-bridges-audit-2026-05-31.md`
  (project-wide audit identifying combat slot + VehicleDefinitionSO as
  BLOCKING).
- **Code touchpoints (Phase 1+)**: `Assets/Scripts/Combat/LegacySlotKind.cs`,
  `SlotDefinition.cs`, `Slot.cs`, `Vehicle.cs`,
  `Assets/Scripts/CombatView/Data/VehicleDefinitionSO.cs`,
  `Assets/Scripts/CombatView/Visual/VehicleVisual.cs`,
  `Assets/Scripts/CombatView/Widgets/{MainBarWidget,CardWidget,IntentWidget}.cs`.
