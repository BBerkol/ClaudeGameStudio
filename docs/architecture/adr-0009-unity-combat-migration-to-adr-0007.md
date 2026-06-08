# ADR-0009: Unity Combat Migration to ADR-0007

## Status

**Superseded by ADR-0010** (2026-05-31). The migration contract
established here — including any inline Amendment classifying
`LegacyKindBridge` as permanent — is replaced by ADR-0010's six-phase
retirement of the legacy vocabulary entirely. See
`docs/architecture/adr-0010-slot-system-single-vocabulary.md`.

_Prior status: **Accepted** (2026-05-28) — operational migration contract. Authored
because the 2026-05-26 framework→Unity pivot inadvertently extended the
pre-ADR-0007 4/5-slot foundation (legacy `SlotKind { MachineGun,
Flamethrower, Engine, Wheels, Frame }`) instead of migrating to the
binding ADR-0007 frame-driven model. This ADR scopes and sequences the
correction._

## Date

2026-05-28

## Decision Makers

- User (creative/design lead) — locked the migration path 2026-05-28
  after surfacing the contradiction
- unity-specialist — owns the rewrite
- creative-director — consulted only for scope deferrals (player armor
  retirement, FlameBarrier redesign)

## Context

ADR-0007 (Accepted 2026-05-19) is the binding design contract for
vehicle structure: `SlotKind { Weapon, Engine, Mobility, Hull, Armor }`,
`SlotPosition { Any, Front, Back }`, `FrameLayoutSO`-driven slot lists,
`StructuralHp`-based death, armor redirection via `RedirectsTo` and
`ExposureMultiplier`, and the canonical event order F-VP2.

Unity Combat code (`Assets/Scripts/Combat/`) was authored against the
older 4-slot model and was extended on 2026-05-25 to a 5-slot model
(`Vehicle.cs` with MaxArmor pool, `SlotKind { MachineGun, Flamethrower,
Engine, Wheels, Frame }`) as a pivot-session decision. Three enemy
archetypes (DuneSkimmer, IronShepherd, Dredge) plus 48+ EditMode tests
were then built on this legacy foundation. The pivot did not
cross-check ADR-0007; the result is contract drift.

This ADR exists to make the migration auditable and to declare a
forbidden list so no further legacy-enum code is written after the
acceptance date.

## Decision

**Migrate Unity Combat to ADR-0007 in 5 sequenced phases. Defer
non-essential rework. No new code against the legacy enum after
2026-05-28.**

### Phase 1 — Foundation (Vehicle POCO + SlotKind enum)

- Replace `SlotKind` enum with ADR-0007 set: `{ Weapon, Engine,
  Mobility, Hull, Armor }`.
- Introduce `SlotPosition { Any, Front, Back }`.
- Introduce `SlotDefinition` struct (engine-free POCO) per ADR-0007 §2.
- Introduce `FrameLayoutSO` (ScriptableObject) holding ordered
  `SlotDefinition[]` and `DegradedThresholdPct`.
- Rename existing `Position` (lane axis Front/Back) → `LanePosition` to
  free the `SlotPosition` identifier.
- `Vehicle` POCO accepts a `FrameLayoutSO` (or its data equivalent) at
  construction; slot dictionary keys become `SlotId` strings, not enum
  values.
- `StructuralHp` = sum over slots where `IsStructural == true`.
- `IsDead` = `StructuralHp == 0`. **No** `MaxArmor` pool on Vehicle.
- Keep transitional `MaxArmor` shim only in archetypes that still need
  it for player parity (cleared in Phase 3).

### Phase 2 — DamagePipeline + Armor mechanic + Event order

- Rewrite damage application against `SlotId`-keyed slots.
- Implement R_ARM: 1× absorb intact, on breaking hit overflow ×
  `RedirectsTo.ExposureMultiplier` → redirected slot (floor rounding).
- Implement F-VP2 canonical event order:
  `OnSlotHpChanged → OnSlotDamageStateChanged → OnArmorExposed →
   StructuralHp recompute → IsDead → OnVehicleDied →
   OnCriticalStateChanged`.
- Slot Offline = soft disable per Decision 16 (granted cards stay,
  become unplayable via source-slot gate).

### Phase 3 — Archetype rewrites against FrameLayoutSO

- **DuneSkimmer** → `tiny_frame` (4 slots, per GDD).
- **IronShepherd** → `hauler_frame` (5 slots, per GDD).
- **Dredge** → `dredge_frame` (10 slots, incl. `armor_chest` +
  `armor_back` redirecting to `hull_0` with ExposureMultiplier 3.0).
  Spike Flail moves to **Mobility** slot per
  `biome-1-enemy-roster.md` line 292; Flamethrower-as-spike-ball
  workaround is removed.
- Honor user directives: Dredge 80 HP / 40 armor (40 all on the armor
  pair via `armorContribution`, not distributed across subsystems).
- Player chassis becomes `small_frame` (`weapon_front` + `weapon_back`
  + `engine_0` + `mobility_0` + `hull_0=16`).

### Phase 4 — EditMode test migration

- ~26 test classes; ~600 LOC enum-coupled fixtures rewritten.
- Test logic (~5000 LOC: brain idioms, intent strip-gating,
  WeightedPoolBrain, PhaseSwapBrain) survives unchanged in shape.
- Helper assertions move from `GetSlot(SlotKind.X)` to
  `GetSlot("slot_id")`.

### Phase 5 — Card SOs + view layer

- Update `PositionRequirement` enum per ADR-0007 §4: rename
  `RequiresAhead/RequiresBehind` → `EnemyAhead/EnemyBehind`; add
  `BonusIfAhead/BonusIfBehind`.
- View layer (HUD slot strip, status badges) reads `HudAnchor` from
  `SlotDefinition` instead of fixed 5-position layout.
- Granted card lifecycle: source-slot gate added to
  `Card.IsPlayable()`.

### Scope Deferrals (out of this ADR; tracked separately)

- **Player armor pool retirement** — `MaxArmor` shim remains on player
  until armor-as-slot redesign is scoped.
- **Per-weapon icon art** — one generic Weapon icon stub for EA.
- **FlameBarrier redesign** — keep current part-trait-driven badge;
  do not rework as Armor-slot effect this pass.
- **Card SO full overhaul** — only minimal field updates this pass;
  full ADR-0006 reauthoring deferred.
- **`AdaptiveSequenceBrain` / `ScriptedSequenceBrain`** — delete if grep
  confirms no live caller; otherwise keep dormant.

## Consequences

### Positive

- Code matches binding ADR; future enemy authoring lands on the right
  foundation.
- Variable slot counts unlock medium_frame (5) and heavy_frame (7)
  player chassis post-EA.
- Test surface stabilizes against a contract that is not expected to
  shift again pre-EA.

### Negative

- 3 sessions of archetype + test work is partially invalidated. Brain
  logic survives; enum-coupled fixtures do not.
- The pivot-session active.md "5-slot model" and "Frame + Wheels +
  Engine + ≥1 weapon" rules are invalidated and must be cleared from
  session state before another session reads them.

### Forbidden from 2026-05-28

- **No new code** referencing the legacy `SlotKind { MachineGun,
  Flamethrower, Engine, Wheels, Frame }` enum after this ADR is
  accepted. CI grep gate added at end of Phase 1.
- **No new vehicle** constructed via `MaxArmor` pool — armor must be
  expressed as a slot with `armorContribution` on a Frame-positioned
  slot (transitional) or a true Armor slot (target).

## ADR Dependencies

- **Binds:** ADR-0007 (Frame-Driven Variable Slot System) — this ADR is
  the operational migration to that contract.
- **Touches:** ADR-0001 (visual part system) — view layer slot strip
  must read `HudAnchor`; no contract change.
- **Touches:** ADR-0002 (POCO combat state) — preserved; only
  enum/keying changes.
- **Touches:** ADR-0006 (card system data) — minimal: `PositionRequirement`
  rename only; full rework deferred.

## Engine Compatibility

Unity 6.3 LTS. No engine-version-specific concerns. `FrameLayoutSO`
authored via `[CreateAssetMenu]`; `SlotDefinition` is `[Serializable]`
to round-trip through SO inspector.

## GDD Requirements Addressed

- `design/gdd/biome-1-enemy-roster.md` — three archetypes will match the
  GDD slot counts and Spike Flail mobility-mount after Phase 3.
- `design/gdd/vehicle-frames.md` (V&P GDD) — `small_frame`,
  `hauler_frame`, `tiny_frame`, `dredge_frame` enter Unity as concrete
  `FrameLayoutSO` assets.

## Acceptance Conditions

- Phase 1: legacy `SlotKind` enum deleted; CI grep gate active.
- Phase 2: F-VP2 event order test passes; R_ARM redirection test passes.
- Phase 3: three archetypes load against `FrameLayoutSO` assets; old
  archetype constants (`MachineGunHp` etc.) replaced with frame-defined
  HP.
- Phase 4: EditMode test suite green (excluding any tests intentionally
  retired and noted in Phase 4 commit message).
- Phase 5: card layer + view layer compile against new enum; player
  vehicle renders 5 slots with correct `HudAnchor`.

## Amendment 2026-05-30 — Path B bridge made permanent

> **SUPERSEDED 2026-05-31 by ADR-0010 + ADR-0011.** The "bridge is permanent"
> classification below was reversed on 2026-05-31 when ADR-0011 (no-bridges
> meta-rule) was Accepted. ADR-0010 retires the bridge in six phases. Read
> the section below as historical context only; do not author new code against
> its conclusion.

**Decision**: Slice 2.7 (`LegacySlotKind` enum retirement + mass test
migration + view-layer fixed-5 structural rebuild) is **deferred
indefinitely**. The Path B per-`SlotDefinition` `LegacyKindBridge`
mechanism is reclassified from *transitional shim* to *accepted
permanent layer*.

**Rationale**:
- Consumer survey 2026-05-30: 1,051 `LegacySlotKind` occurrences
  across 72 files (32 production, 28 tests, 14 view, 2 editor). Largest
  hotspots: `VehicleBarStack.cs` (38, fixed-5 array), `CombatPrefabAuthor.cs`
  (30), `CombatLoop.cs` (30), `Dredge.cs` (26), `Vehicle.cs` (23). Slice
  is ~8× the size of any previously-shipped migration slice and includes
  structural view-layer rebuilds that are not test-runner-catchable.
- The bridge is paid debt, not bleeding debt. No runtime cost beyond one
  `int` field per `SlotDefinition`. Tests are green. All three biome 1
  enemies and the player vehicle work end-to-end through the bridge.
- The `Enum.GetValues(typeof(LegacySlotKind))` iteration hazard that
  surfaced during slice 2.5c-3 has been neutralized at the source:
  `Vehicle`'s legacy constructor enumerates the enum to seed `_slots`,
  so future enum additions auto-extend without test breakage.
- Opportunity cost: project focus is the playable combat demo (1
  player + 3 enemies, sequenced fights with reward picks). Migration
  work yields zero player-facing capability.

**Implications for this ADR**:
- Acceptance Conditions Phase 1 ("legacy `SlotKind` enum deleted; CI
  grep gate active") is **retired**. The CI grep gate against new
  legacy-enum code stays active — bridge values may grow when new
  layout-mode slots need card-targeting reachability (precedent:
  `Exposable1`/`Exposable2` added 2026-05-29 for Dredge boss slots).
- All other Phases remain as shipped.
- This ADR is no longer "in flight." Stays Accepted; bridge is
  henceforth the canonical layer for legacy-API ↔ layout-API
  interop.
