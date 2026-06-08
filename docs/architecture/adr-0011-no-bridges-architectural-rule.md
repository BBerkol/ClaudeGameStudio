# ADR-0011: No-Bridges Architectural Rule (Project-Wide)

## Status

**Accepted** (2026-05-31) — binding meta-rule. Every prior accepted ADR is re-scoped against this rule; every future ADR must reference it and explicitly state how the design avoids bridge patterns.

## Date

2026-05-31

## Last Verified

2026-05-31

## Decision Makers

- User (creative/design lead) — issued the directive 2026-05-31: "from now on i dont want any bridges or any legacy systems when we are done"; clarified same session: "i am talking about the whole game. this does not only apply to our current work, i mean this for all our previous and future work"
- technical-director — owns enforcement and audit cadence
- lead-programmer — owns CI grep gate addition per retirement slice

## Summary

This ADR forbids permanent adapter layers, parallel storage of the same concept, and bimodal code paths anywhere in the game's done state. It establishes the project-wide architectural rule that every system must reach a single-vocabulary, single-storage, single-path final state before any milestone is declared done — and codifies the allowed exceptions (one-shot data migrators, save-data versioning, editor-only authoring tools).

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Engine-agnostic (applies to Unity 6.3 LTS code today; portable to any future engine choice) |
| **Domain** | Core (architectural meta-rule, all subsystems) |
| **Knowledge Risk** | LOW — the rule is a policy on code shape, not an engine API |
| **References Consulted** | None (rule is engine-agnostic) |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | CI grep gates per retirement slice (see Migration Plan) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0010 (combat slot retirement — first concrete application); future per-system retirement ADRs |
| **Blocks** | All milestone "done" declarations until the per-system audit punch-list is cleared |
| **Ordering Note** | Must be Accepted before the project-wide bridge/legacy audit punch-list (`docs/architecture/no-bridges-audit-2026-05-31.md`) can be authored — the audit checks code against this rule. |

## Context

### Problem Statement

The project has accumulated multiple bridge layers and bimodal code paths across systems. The combat slot system alone has 1,081 `LegacySlotKind` references across 72 files, 39 `LegacyKindBridge` field references, 254 bridge entry-point call sites, and 44 legacy-mode test constructor sites. ADR-0009 Amendment (2026-05-30) classified the combat slot bridge as "paid debt — permanent layer." User reversed that classification 2026-05-31 and expanded the reversal to the entire codebase, retroactive and forward.

Bridge layers and bimodal paths produce concrete harm: B1 (Dredge armor bar displayed 0 because `MainBarWidget` read the legacy pool while the actual 60hp lived on `armor_0.Hp`) is a direct symptom of parallel storage of the same concept. Every new enemy slot shape (Dredge's Exposable1/Exposable2/Javelin) required bridge cells AND view-layer SerializeField updates because the view layer was keyed by fixed-N legacy vocabulary. Each new contributor must learn both vocabularies, the bridge map, and which side of `IsLayoutMode` they are on.

Without a binding rule, future features will continue to ship behind new bridges (the path of least resistance during any migration), and the done state will permanently carry adapter debt.

### Current State

- ADR-0007 (Accepted 2026-05-19) mandated the modern slot vocabulary (`{Weapon, Engine, Mobility, Hull, Armor}` + `SlotPosition` + `FrameLayoutSO`) but did not forbid bridging back to the legacy vocabulary.
- ADR-0009 (Accepted 2026-05-28) scoped a phased migration from the legacy slot vocabulary to ADR-0007's vocabulary via a `LegacyKindBridge` adapter field, with a planned Slice 2.7 deletion.
- ADR-0009 Amendment (2026-05-30) reclassified the bridge as permanent after a consumer survey showed 1,051 occurrences. Slice 2.7 was deferred indefinitely.
- Multiple other systems carry parallel storage or bimodal paths (full extent unknown until audit completes).

### Constraints

- **Code-base scope** — applies to both the framework repo (`Madmax Roguelike/`) and the Unity project (`Wasteland Run/`).
- **Engine constraint** — Unity 6.3 LTS deprecates the legacy Input class in favor of the new Input System. Any code using both surfaces is bridged by this rule's definition.
- **Migration cost** — retirement work consumes development time that does not produce player-visible features. The rule accepts this trade against the long-term cost of permanent debt.
- **Test surface impact** — combat slot retirement alone touches 27 test files. Other systems' retirements may have similar impact.

### Requirements

- **Single source of truth per concept** — armor (or any gameplay state) must live in exactly one storage location. No parallel pools, no duplicate caches.
- **Single vocabulary per axis** — slot identity, slot kind, position, etc. each described by exactly one type. No alias enums, no bridge maps from old-name to new-name.
- **Single code path per operation** — `ApplyDamage`, `Repair`, `Plate`, etc. each have one implementation. No `if (IsLegacyMode) … else …` branches.
- **No fixed-N hardcoding for variable-N data** — view widgets enumerate collections dynamically rather than serializing N-position fields keyed by a finite enum.
- **CI-enforceable** — each retirement slice adds a grep gate that fails the build if the retired marker reappears.

## Decision

**Adopt the no-bridges architectural rule project-wide, retroactive and forward. Every system's done-state code must satisfy the four single-X requirements above. Bridge patterns are permitted only as scaffolding during in-flight retirement slices and must be removed before the slice closes.**

### Forbidden patterns at done state

1. **Adapter layers** — fields, methods, or types whose sole purpose is to translate one vocabulary into another (e.g., `SlotDefinition.LegacyKindBridge`, which maps `slotId` to `LegacySlotKind` enum values for old callers).
2. **Parallel storage of the same concept** — two fields/properties/collections storing the same gameplay state in different shapes (e.g., `Vehicle._maxArmor` pool AND `SlotKind.Armor` slot HP both representing armor).
3. **Bimodal code paths** — `if (IsLegacyMode) … else …` branches choosing between an old and a new path for the same operation. Single mode only.
4. **Vestigial enum members or types** — enum values or classes that exist only because old call sites still reference them (e.g., `LegacySlotKind` enum after `SlotKind` superseded its purpose).
5. **Backwards-compat constructor or method overloads** — extra signatures preserved only so old call sites compile (e.g., `new Vehicle(string name)` legacy ctor alongside `new Vehicle(string name, IFrameLayout layout)`).
6. **Stub returns** — properties or methods returning `0`, `null`, or `default` because the call site is asking the wrong question (e.g., `Slot.ArmorContribution` returning 0 when the slot is in layout mode).
7. **"Transitional" / "for now" / "bridge" comments** — markers indicating scaffolding that should have been removed. Their presence is evidence the rule has been violated.
8. **Duplicate enums for the same categorical axis** — two enums describing the same concept with different vocabularies (e.g., `LegacySlotKind` and `SlotKind` both describing slot identity).

### Allowed (explicitly NOT bridges)

1. **One-shot data migrators** — code that reads an old-format `.asset` or save file at slice time and writes the new format, then is deleted in the same slice. Temporary by definition. Permitted because the runtime path remains single after the migrator runs.
2. **Save-data schema version fields** — version numbers on persisted DTOs (per ADR-0004). Versioning is not bridging; there is no parallel old-format runtime path, only forward-migration at load time.
3. **Editor-only authoring tools** — scripts under `Assets/Editor/` (e.g., `CombatPrefabAuthor`) that translate designer Inspector input into runtime data in the canonical format. Authoring is not bridging.
4. **Polymorphism and strategy patterns** chosen for legitimate design reasons — multiple `IBrain` implementations, multiple `IFrameLayout` archetypes, multiple `ICardEffect` types. These are clean architecture, not legacy.
5. **CI grep gates** — guard code that prevents disallowed patterns from re-entering the codebase. Tooling, not gameplay code.

### Architecture

This ADR creates no system architecture; it constrains the architectures other ADRs may propose. There is no diagram.

### Key Interfaces

This ADR creates no interfaces; it constrains the interfaces other ADRs may create. Specifically:

- **For new ADRs** — the Decision section must include a paragraph titled "ADR-0011 compliance" stating how the proposed design satisfies the four single-X requirements and explicitly listing any one-shot migration assets the slice will produce and delete.
- **For existing ADRs** — those proposed before 2026-05-31 are not retroactively rewritten, but their implementation code is audited (per the punch-list) and retirement slices are scoped per-system. Retirement slices use ADR-0010's six-phase template (Lock design → View layer → Production code → Simplification → Test migration → Demolition → Doc scrub).

### Implementation Guidelines

**When designing new work:**
- Choose single-storage, single-vocabulary designs from the start. Pay the migration cost up front rather than introducing a bridge for short-term migration safety.
- If a proposed design has an "add an adapter so old callers keep working" component, flag it as violating this ADR and pick a different design.
- Every new ADR's Decision section must include an explicit "ADR-0011 compliance" paragraph.

**When extending existing code:**
- Prefer the modern code path. Do not add new callers to a legacy or bridged surface.
- If a system has an active bridge, scope a retirement slice and surface it to the user. Do not let new features cement the bridge in place.

**When auditing for done state:**
- Every accepted ADR is reviewed for bridge patterns before the milestone is declared done. The project-wide punch-list (`docs/architecture/no-bridges-audit-2026-05-31.md`) tracks the surface.
- CI grep gates are added per retirement slice. Each slice must define its own forbidden-token list and add a grep job to the build.

**Per-system retirement slice template** (six phases from ADR-0010):
1. Lock the clean design via a new ADR that supersedes the bridge-tolerant ones.
2. Rebuild the view layer or boundary callers first (variable-N enumeration where applicable).
3. Migrate internal production code to the single vocabulary.
4. Simplify the core type by deleting parallel fields, bimodal branches, compat overloads.
5. Migrate the test surface to the single vocabulary.
6. Delete the bridge enum/field/stub and add the CI grep gate.
7. Doc scrub: mark superseded ADRs, remove transitional comments, update session state.

## Alternatives Considered

### Alternative 1: Keep bridges as paid debt (status quo of ADR-0009 Amendment)

- **Description**: Bridge layers remain permanent. New systems may introduce new bridges as ergonomic shortcuts.
- **Pros**: No upfront migration cost. Features ship faster in the short term.
- **Cons**: B1-class bugs (parallel storage drift) recur. Every new enemy/card/system multiplies the view-layer hardcoding cost. Reader cost permanent. Three docs (ADR-0007 + ADR-0009 + ADR-0009 Amendment) explain one system.
- **Estimated Effort**: Zero short-term; unbounded long-term as new bridges accumulate.
- **Rejection Reason**: User explicitly reversed this 2026-05-31. The "paid debt" classification proved false in practice (B1 was direct evidence).

### Alternative 2: Per-system rule, not project-wide

- **Description**: Apply no-bridges to the combat slot system only. Other systems retain their bridges if any.
- **Pros**: Smaller audit scope. Combat-demo can declare done faster.
- **Cons**: User explicitly said "the whole game." Other systems' bridges (save format, addressables fallback, input duplication, etc.) would carry into post-demo work. New systems would lack the rule and could ship with new bridges.
- **Estimated Effort**: Slightly less audit work than the chosen approach.
- **Rejection Reason**: Contradicts user directive. Half-applying the rule produces a codebase where some systems are clean and others are not, which is harder to reason about than uniform-bridged or uniform-clean.

### Alternative 3: Informal convention, no binding ADR

- **Description**: Treat no-bridges as a coding-standard preference rather than a binding architectural rule. No CI gates, no per-ADR compliance paragraph.
- **Pros**: No doc overhead. Lighter weight.
- **Cons**: Conventions drift. ADR-0009 was a binding doc and still got "paid debt" amended onto it. Without a binding meta-rule and CI gates, new bridges enter the codebase silently and the audit punch-list gets stale.
- **Estimated Effort**: Zero authoring; unbounded drift cost.
- **Rejection Reason**: User asked for the rule to apply project-wide and durably. Informal conventions cannot enforce that.

## Consequences

### Positive

- **B1-class bugs eliminated by construction.** Parallel storage drift cannot occur if there is only one storage.
- **New systems cost less to add.** Adding a slot kind, a card effect, or an enemy archetype no longer requires updating fixed-N view-layer hardcoding.
- **Tests match runtime.** Single-mode code means test fixtures and production code share the same shape; a test passing is meaningful evidence the runtime path works.
- **Reader cost down.** New contributors (and future-self) learn one vocabulary per concept, not two plus a bridge map.
- **Doc volume reduced long-term.** One ADR per system, not ADR + Bridge ADR + Amendment.
- **Audit cadence formalised.** Per-milestone audit catches drift before it ships.

### Negative

- **Upfront migration cost.** Combat slot retirement alone is six phases across ~1,081 occurrences / 72 files / 27 test files. Future per-system retirements pay analogous costs.
- **Dev velocity hit during retirement slices.** No player-visible progress during a retirement slice.
- **Per-ADR compliance paragraph adds boilerplate.** Every new ADR carries an ADR-0011 compliance statement.

### Neutral

- **CI grep gates added per slice.** Build job grows; protects against regression.
- **Existing ADRs not rewritten.** They keep their original Accepted text; implementation code is audited and retired per the punch-list. Superseded amendments tag them at retirement time.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| New bridge introduced silently in future work | MEDIUM | HIGH | CI grep gates per known bridge marker; per-ADR compliance paragraph in template; producer surfaces violations during sprint review |
| Audit punch-list incomplete (a system's bridge missed) | MEDIUM | MEDIUM | Audit covers both repos + all accepted ADRs; follow-up audits at each milestone; user can surface suspected bridges for re-audit |
| Retirement slice ships partial (some bridge code remains, build green by accident) | LOW | HIGH | Each slice's grep gate fails build on token presence; gate must be the last step before slice closes |
| One-shot migrator left behind (becomes a permanent bridge by neglect) | MEDIUM | MEDIUM | Migrator code lives in the same commit as its deletion; PR description must show before-and-after grep counts |
| Rule re-litigated under time pressure ("just ship this with a bridge") | LOW | HIGH | This ADR cannot be amended to permit bridges without explicit user re-decision; memory `project_no_bridges_at_done.md` carries the original directive verbatim |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a | n/a | n/a |
| Memory | n/a | n/a | n/a |
| Load Time | n/a | n/a | n/a |
| Network | n/a | n/a | n/a |

No direct performance implications. Single-vocabulary code paths may eliminate trivial bridge-lookup overhead but the saving is not measurable at our scale. The rule is enforced for correctness and maintainability, not perf.

## Migration Plan

1. **Audit punch-list** (`docs/architecture/no-bridges-audit-2026-05-31.md`) — walk both repos and every accepted ADR; grep for forbidden patterns; output per-system sections with file:line citations and severity (BLOCKING / HIGH / MEDIUM / LOW).
2. **Per-system retirement ADRs** — each system on the punch-list gets its own retirement ADR following ADR-0010's six-phase template. Ordering: by gameplay-impact severity, not code volume.
3. **CI grep gates** — each retirement slice adds a grep job that fails the build if the retired marker reappears.
4. **ADR retirement amendments** — bridge-tolerant ADRs (currently ADR-0009 + Amendment) get a final amendment marking them superseded by their retirement ADR.

**First system in flight**: combat slot system (ADR-0010, plan parked at `production/adr-0010-phase-plan.md`).

**Rollback plan**: This is a meta-rule, not a system. There is no rollback in the conventional sense. If the rule proves unworkable, it can be amended — but the amendment must be authored by the user, not the implementer, and must explicitly identify which specific bridge pattern is being re-permitted and why. The default position is enforcement.

## Validation Criteria

- [ ] CI grep gate per retired bridge marker — build fails on regression.
- [ ] Audit punch-list closed (every entry resolved by a retirement slice or explicitly waived by user with rationale logged).
- [ ] Every accepted ADR after 2026-05-31 contains an "ADR-0011 compliance" paragraph.
- [ ] Zero `if (IsLegacyMode)` / `if (IsLayoutMode)` branches in the codebase at milestone-done.
- [ ] Zero `LegacyKindBridge`-style adapter fields at milestone-done.
- [ ] No ADR carries "transitional" / "bridge" / "for now" comments in its decision section at milestone-done.

## GDD Requirements Addressed

Foundational — no direct GDD requirement. This ADR is a code-shape policy.

**Enables:**
- All future GDD requirements that touch systems currently behind bridges (new enemy slot shapes, new card effects keyed by SlotKind, new save formats, new input bindings).
- ADR-0010 (combat slot retirement) and analogous per-system retirements.

## Related

- **Supersedes** the architectural posture of ADR-0009 Amendment (2026-05-30) which classified the combat slot bridge as permanent. The amendment itself stays on disk as historical record; its conclusion is reversed by this rule.
- **Enables** ADR-0010 (combat slot retirement — first application).
- **Memory references**:
  - `project_no_bridges_at_done.md` (the directive verbatim)
  - `project_adr_0010_parked.md` (combat slot retirement plan parked at `production/adr-0010-phase-plan.md`)
- **Tooling reference**: per-slice CI grep gates land in `tools/ci/` (path TBD when first gate is added).
