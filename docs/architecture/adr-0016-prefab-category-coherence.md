# ADR-0016: Composition by Conceptual Category and Edit Cadence

## Status

Accepted (2026-06-17 — document authored 2026-09-09)

## Date

2026-06-17

> **Authoring note — read this first.** The principle below was decided in a
> technical-director session on **2026-06-17** and has governed composition
> decisions since. The document was never written. The 2026-09-08 clean-slate
> audit found the gap, and also found:
>
> - **Accepted ADR-0017 citing this number five times**, including in its
>   `Depends On` row (lines 41, 49, 269, 334, 460), for two months against a
>   document that did not exist;
> - a TD audit (`production/td-verdicts/2026-07-29-whole-game-health-opt-audit.md:262`)
>   proposing to recycle number 0016 for an unrelated "Derived-State Caching
>   Pattern";
> - roughly ten production documents already citing ADR-0016 as governing.
>
> The `Date` field is the decision date so that ADR-0017's dependency is
> chronologically valid; the document authoring date is stated above and in
> `Last Verified`. This ADR is **retroactive**: it codifies practice already in
> the tree rather than proposing new work. Its applications shipped before it
> did, which is precisely the failure mode it exists to prevent. That is
> recorded here rather than tidied away.

## Last Verified

2026-09-09 — `Run.prefab` and `Combat.prefab` confirmed separately scoped
(`Assets/Prefabs/Run/`, `Assets/Prefabs/CombatView/`) with **zero** cross-prefab
containment in either direction; `VehicleHudAnchors` confirmed sibling-component
via `visual.GetComponent<VehicleHudAnchors>()` at `VehicleBarStack.cs:216/251/351`;
`VehicleBodyworkAnchors` confirmed zero-hit (unbuilt, owed by ADR-0017 Phase 2.5).

## Decision Makers

User (BertanBerkol) — caught the originating categorical mismatch on a cold
eyeball pass. Claude (technical-director sessions 2026-06-17 and 2026-09-08).

## Summary

Composition units — prefabs, scenes, and the components inside them — are
organized by **conceptual category** (what does this unit claim to be?), with
**edit cadence** as a tiebreaker when category is ambiguous. They are never
organized by dependency convenience (where can this connect most cheaply?).
Dependency-led composition is locally rational at every step and globally
produces composition rot: Slice 6 closed with the run-loop driver inside the
combat composite, and the next slice piled map view, run-complete view and the
overlay host on top of it. This ADR states the principle once so that each
future "where does this new thing live?" question has an answer that does not
have to be re-derived.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core (composition standard; expressed in Unity prefab/scene terms) |
| **Knowledge Risk** | LOW — relies only on prefab nesting, scene-root siblings and `GetComponent`, all stable since Unity 2018 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, ADR-0011, ADR-0014, ADR-0017, `production/td-verdicts/2026-06-17-scene-split-verdict.md`, `feedback_composition_smell_test`, `feedback_td_briefing_discipline` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None at runtime. One containment invariant is CI-gated (see Decision); the categorical judgment is review-time by design. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0011 (no bridges at done — this ADR supplies the *composition* half of the same discipline; 0011 governs code shape, 0016 governs where that shape lives) |
| **Enables** | ADR-0017 (cites this ADR for `VehicleBodyworkAnchors` sibling discipline and Bodywork-as-distinct-category); Slice 6.1 prefab decomposition; every future beacon-type slice that must choose scene vs PrefabRoot |
| **Blocks** | Prospectively: any slice adding a component, child GameObject or `SerializeField` to an existing composite prefab, and any slice introducing a new beacon presentation surface |
| **Ordering Note** | Authored after its consumer ADR-0017 was Accepted. Number claimed to prevent collision with the pending "Derived-State Caching Pattern" proposal — see *Related*. |

## Context

### Problem Statement

Unity applies no structural pressure to where a component lives. Any
`MonoBehaviour` can be dropped onto any GameObject in any prefab and it will
work. The only thing preventing a composite from accreting unrelated
responsibilities is a rule someone remembers to apply — and by the time the
accretion is visible it is expensive to unwind, because prefab reparenting
silently retargets scene `m_Modifications` (`feedback_nested_prefab_topology_orphans_overrides`).
Composition mistakes are not cheap to reverse.

Without a codified rule, each "where does this go?" question is answered by the
cheapest available connection, and every individual answer looks reasonable.

### Current State

The failure this ADR generalizes from, in order:

1. Slice 6 closed with `RunSceneHost` — the run-loop driver — inside
   `Combat.prefab`, the combat composite. **TD reviewed it and approved.** The
   reasoning was dependency-shaped and locally sound: the host needed to reach
   combat, and combat was right there.
2. The next session's Workstream D extended the same logic, adding `MapView`,
   `RunCompleteView` and `RunSceneOverlayHost` to the same prefab. Each addition
   was justified by the previous one.
3. The user opened the prefab cold for an eyeball pass and immediately asked why
   map view was inside the combat prefab.

Root cause is path-dependence — *"X already lives at Y, so the new thing related
to X should also live at Y"* — compounded by a mental-model split. The
programmer model thinks in dependencies: where can this connect most cheaply?
The designer model thinks in categories: combat is combat, map is map. The
categorical model is the cleaner abstraction, and it is the one that caught the
problem.

The narrow fix from that session — run-layer and phase-layer prefabs are
siblings — answers *that* question and no other. It does not answer "does the
next beacon get a scene or a PrefabRoot?", "should reward inventory live in
`CombatHud`?", or "should the pause menu sit at scene root?" A principle answers
all of them; a rule answers one.

## Decision

### The principle (load-bearing)

> **Composition units are organized by conceptual category, with edit cadence as
> a tiebreaker — never by dependency convenience.**

- **Conceptual category — *what does this unit claim to be?* (primary, binding.)**
  A prefab or scene's name is a promise about its contents. `Combat.prefab`
  claims to be combat. Anything inside it that a reader would not predict from
  the name is a violation, however convenient the wiring.

- **Edit cadence — *who edits this, how often, and in isolation or alongside its
  siblings?* (tiebreaker only.)** Cadence applies in exactly two situations:
  (a) category is genuinely ambiguous, or (b) a single category has grown too
  large to edit in isolation and needs internal subdivision. **Cadence never
  overrides a clear categorical answer.**

The precedence matters and is not decoration. Two co-equal axes that can
disagree, with no stated ordering, is a decision procedure with a hole in it:
whoever wants to accrete simply picks the axis that says yes, which is the
path-of-least-resistance defeat this ADR exists to prevent wearing a new excuse.
The tree already contains a case that would break under co-equal axes —
`Run.prefab` holds `MapView`, `RunCompleteView` and `GameOverView`, one category
but materially different cadences (the map view is iterated constantly,
`GameOverView` is near-static). Category correctly keeps them together; cadence
as a peer axis would have argued to split them.

Dependency convenience is explicitly **not** an input. Where the categorically
correct home makes wiring harder, pay the wiring cost.

### The smell test (apply at the moment of decision)

Before adding any component, child GameObject or `SerializeField` to an existing
composite — or extending an author tool to bake new structure into one — ask:

> **"Would a designer who opened this unit to edit its primary purpose expect to
> find this new thing here?"**

If no: **stop.** Do not extend it. Propose a separate prefab, scene or sibling
component, name the categorical mismatch explicitly, and bring it to TD and the
user before executing.

**This question is a required field in any composition-touching TD brief.** It
is not enough for the rule to exist — the 2026-06-17 failure was not an unknown
rule, it was an *unasked question*: TD approved `RunSceneHost` inside
`Combat.prefab` because the brief only asked whether the wiring worked. Making
the question part of the brief format means it is asked by the process rather
than remembered by the reviewer. See `feedback_td_briefing_discipline`.

### Application 1 — Run-layer and phase-layer prefabs are scene-root siblings

Run-loop driver and run-overlay UI live in `Run.prefab`. Phase composites —
`Combat.prefab`, and any future `Haven.prefab` / `Merchant.prefab` — are
top-level siblings in the scene. **A phase prefab is never nested inside the run
prefab, and vice versa.** This breaks the cross-prefab reference cycle outright
rather than relocating it, and is consistent with ADR-0014: `Run.prefab` is a
scene-topology peer, not a UI-stack choice, so UI Toolkit primacy is unaffected.

**Cross-seam resolution.** Sibling units cannot use inspector references across
the seam. Resolve via `FindAnyObjectByType`, explicit injection, or a POCO
mediator (the `RunOverlayEvents` precedent). **Resolve once at bind time and
cache the result — never per frame.** `FindAnyObjectByType` is a scene-wide
scan; a per-frame call would turn this composition rule into a performance
defect, and this clause exists so that no one can cite the ADR while writing one.

**This invariant is CI-gated** — see *Enforcement* below.

### Application 2 — Scene versus PrefabRoot is a category decision

The same principle applies one level up, at scene topology. A beacon type is
presented either as an additively-loaded scene or as a `PrefabRoot` inside
`RunScene`, and that choice is made on category, not on convenience.

The worked example is `production/td-verdicts/2026-06-17-scene-split-verdict.md`,
whose Option B hybrid this ADR ratifies: a beacon earns its own **scene** when it
claims a distinct presentation surface with its own lighting, camera framing and
edit cadence (Combat, Elite, Boss). It ships as a **PrefabRoot** when it is a
modal surface over the run map that shares the run's presentation context
(Rest, Event, Merchant, Chopshop). Both patterns are live in the tree today and
their coexistence is an axis-aligned hybrid, not a bimodal path under ADR-0011 —
the same reasoning ADR-0014 uses for world-space UGUI popups alongside UI Toolkit.

The practical consequence: a new beacon type does not inherit whatever the last
beacon did. It answers the categorical question first.

> **Known open violation.** `Assets/Scenes/CombatScene.unity` (18,016 bytes)
> exists alongside `Assets/Scenes/Beacons/Combat.unity` (7,225 bytes). Two scene
> files claiming to be the combat scene is a category collision under this very
> application, present in the tree on the day this ADR was authored. It is
> deliberately **not** resolved here: per
> `production/remediation-plan-2026-09-08.md` §4.1 it is a separate orphan with
> its own follow-up, requiring a scene deletion plus a build-settings edit and
> its own acceptance test. Recorded rather than omitted, because an ADR that
> hides its own first violation is the failure mode this document is about.

### Application 3 — Sibling components, not nested ones

When a new responsibility attaches to an existing component's GameObject, it
ships as a **sibling component** rather than as fields bolted onto the existing
one, whenever it represents a distinct category.

`VehicleHudAnchors` is the shipped precedent — a separate `MonoBehaviour` on the
same GameObject as `VehicleVisual`, resolved by
`visual.GetComponent<VehicleHudAnchors>()`. `VehicleBodyworkAnchors` (owed by
ADR-0017 Phase 2.5, currently unbuilt) must mirror it: `VehicleVisual`'s category
is core chassis composition — Weapon, Engine, Mobility, Hull — and Bodywork is a
distinct category, so it does not become fields on `VehicleVisual`. Sprite
hierarchies may nest independently for rendering order; the **component** stays
sibling.

### Enforcement

Split deliberately between what is mechanizable and what is not.

**Gated — the containment invariant.** `tools/ci/grep-gates.sh` asserts that
`Assets/Prefabs/Run/Run.prefab` does not contain the `Combat.prefab` GUID
(`4ee0cdd0c4ea8e442863de089215c801`) and that `Combat.prefab` does not contain
`Run.prefab`'s (`87507ab03d415264396cd3d2019e994c`). Verified baseline is **zero
in both directions**. This gate makes no categorical judgment — it asserts one
specific containment that Application 1 forbids, against a known-good zero, so
its false-positive surface is nil.

**Not gated — the categorical judgment.** "Would a designer expect this here?"
is a semantic question about a unit's claimed purpose. Grep can detect a token,
not a category error. A gate attempting it would false-positive on legitimate
composition and false-negative on exactly the path-dependent accretion it was
meant to catch — and a gate that cannot fire correctly is documented false
assurance, which the 2026-09-08 audit identified as worse than no gate at all.
This half is enforced at review time by the required TD-brief field above.

### Scope boundary

This ADR governs **composition** — where a thing lives. It does not govern:

- **Code shape** — ADR-0011.
- **UI stack choice** — ADR-0014.
- **Scope narrowing** — ADR-0015.
- **Authoring authority** — *which tool owns a prefab's bytes* is explicitly out
  of scope. Two writers for one artifact is ADR-0011 forbidden pattern #2
  (parallel storage) expressed in authoring form, and needs no new ADR; the
  resolution for the Combat surfaces specifically is ADR-0014 P4, which retires
  those surfaces from the author script entirely. Operational authoring rules
  belong in `docs/architecture/control-manifest.md`, which is versioned and
  designed to change as reality changes. An ADR is not.

A categorically correct home for a stub is still an ADR-0011 violation; a
categorically wrong home for correct code is still an ADR-0016 violation. The
checks are independent.

### ADR-0011 compliance

Per ADR-0011:108, which requires every ADR accepted after 2026-05-31 to state
its compliance explicitly:

- **Single source of truth** — this ADR is the sole authority for composition
  reasoning. The same rationale currently exists in `feedback_composition_smell_test`,
  `adr-0017:332-338` and `2026-06-17-scene-split-verdict.md:67-69`; those become
  pointers to this ADR rather than parallel statements, so four copies cannot
  drift.
- **Single path** — one decision procedure, with category primary and cadence
  explicitly subordinate. No alternate route exists for units that find the
  categorical answer inconvenient.
- **Single vocabulary** — "conceptual category" and "edit cadence" are the only
  terms; "categorical fit," "category coherence" and "composition smell test"
  used in earlier documents all denote this same principle.
- **No transitional constructs** — this ADR introduces no adapter, no bimodal
  path, no vestigial enum and no stub. It produces no one-shot migration assets,
  because Applications 1 and 3 are already true in the tree and Application 2
  ratifies a shipped topology.

The one known deviation is stated in the open-violation note under Application 2
rather than being silently carried.

## Alternatives Considered

### Alternative 1 — Case-by-case TD judgment, no codified principle

- **Pros**: Zero documentation cost; maximum flexibility; each decision gets
  full context rather than being forced through a general rule.
- **Cons**: This is the actual status quo ante — what the project did from
  Slice 6 until 2026-06-17 — and it has a documented failure with a named
  victim. TD *did* review `RunSceneHost` inside `Combat.prefab` and *did*
  approve it. Case-by-case judgment did not fail through inattention; it failed
  because there was no principle to judge against, so the locally-rational
  dependency answer won.
- **Rejection Reason**: Demonstrated insufficient. A rule that must be
  re-derived at each decision is not enforcement, and the re-derivation reliably
  produces the dependency answer.

### Alternative 2 — Ship only the narrow rule ("run-layer and phase-layer are siblings")

- **Pros**: Smaller; answers the question actually in front of us; no risk of
  over-generalizing from one incident.
- **Cons**: Answers exactly one question. The scene-vs-PrefabRoot decision, the
  pause menu, reward inventory and boss cutscenes all re-open the argument from
  nothing.
- **Rejection Reason**: The originating session was explicit that the narrow
  rule is insufficient and that the principle must be stated first with
  applications enumerated beneath it.

### Alternative 3 — Full CI grep-gate enforcement

- **Pros**: Automatic; matches the ADR-0011 enforcement pattern; independent of
  anyone remembering.
- **Cons**: The categorical judgment is not mechanizable, and a gate that cannot
  fire correctly is documented false assurance.
- **Rejection Reason**: Rejected only in part. The containment invariant *is*
  mechanizable and is now gated; the semantic judgment is review-time by design.
  See *Enforcement*.

### Alternative 4 — Include authoring authority ("the tool is the authoring surface")

- **Pros**: 0016 was originally scoped as an authoring-surface ADR; the rule is
  real and currently unwritten.
- **Cons**: Fails this ADR's own smell test — authoring authority is not
  composition, and including it here because 0016 was *originally* scoped that
  way is the exact path-dependence the ADR names. Its natural carve-out list was
  also wrong on inspection: `ChopshopRoot.prefab` is tool-authored
  (`CombatPrefabAuthor.cs:8273`, `:9127`), not a surgical-YAML exemption. And
  the rule's scope shrinks by design as ADR-0014 P4 migrates surfaces out of the
  author script, so accepting it would owe an amendment on arrival.
- **Rejection Reason**: Out of scope, factually unstable, and expiring. Covered
  by ADR-0011 #2; operational form belongs in the control manifest.

## Consequences

### Positive

- Every future "where does this live?" question has a stated answer requiring no
  round-trip to first principles.
- Composition units stay readable to the person most likely to open them cold —
  the designer — which is the review pass that caught the original problem.
- Categorically scoped units are edited in isolation, so they collide less in
  version control and their overrides are less likely to be orphaned by
  reparenting.
- Gives ADR-0017 the dependency it already declared, and claims number 0016
  before the pending caching proposal collides with it.
- The containment invariant is now machine-checked rather than trusted.

### Negative

- **Wiring costs more.** No inspector references across a sibling seam; the
  bind-time-and-cache rule is mandatory rather than advisory, and forgetting it
  converts this ADR into a per-frame scene scan.
- **The smell test is judgment-dependent** and has already failed once at TD
  review. The required-brief-field rule is the mitigation, but it is a process
  control, not a mechanism.
- **Category can be argued too finely**, producing unit sprawl. Edit cadence as
  tiebreaker is the intended brake.

### Neutral

- No runtime cost, no API surface, no test surface beyond the one grep gate.
  This ADR changes review and authoring behaviour.

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Smell test skipped under delivery pressure | MED | MED | Required field in composition-touching TD briefs; user's cold eyeball pass is the backstop that caught it before |
| Category argued too finely → unit sprawl | LOW | MED | Edit cadence as tiebreaker; units always edited together stay together |
| Cross-seam `FindAnyObjectByType` called per frame | MED | MED | Bind-time-and-cache stated as a rule in Application 1, not left implicit |
| Retroactive status normalizes writing ADRs late | MED | MED | The authoring note names the ordering failure explicitly; audit §0.3 tracks ADR doc drift as its own remediation item |

## Performance Implications

None inherent. The single performance-relevant clause is the cross-seam
resolution rule in Application 1: `FindAnyObjectByType` is a scene-wide scan and
must be called once at bind time and cached. No per-frame cost is introduced by
any part of this ADR.

## Migration Plan

None required. Applications 1 and 3 are already true in the tree and were
verified 2026-09-09; Application 2 ratifies the shipped Option B hybrid
topology. This ADR documents existing state.

Two items are tracked outside it: the `CombatScene.unity` category collision
(remediation plan §4.1) and demoting the duplicated rationale in `adr-0017:332`
to a pointer.

## Validation Criteria

1. `Run.prefab` and `Combat.prefab` exist as separately scoped units with zero
   containment in either direction. **Verified 2026-09-09 — 0 and 0.**
2. The containment grep gate is present in `tools/ci/grep-gates.sh` and passes.
3. `VehicleHudAnchors` is a distinct `MonoBehaviour` resolved via `GetComponent`,
   not fields on `VehicleVisual`. **Verified 2026-09-09.**
4. When `VehicleBodyworkAnchors` ships (ADR-0017 Phase 2.5) it is a sibling
   component. **Pending — currently zero-hit.**
5. Prospective: no TD brief adding a component to an existing composite is
   approved without answering the categorical question.
6. Prospective: the next new beacon type's scene-vs-PrefabRoot decision cites
   this ADR rather than precedent-matching the previous beacon.
7. ADR-0014 P4 lands without requiring an amendment to this ADR.

## GDD Requirements Addressed

None directly. This is a composition standard with no player-facing surface. It
serves GDD requirements indirectly by keeping the authoring surfaces that produce
player-facing content legible to their authors.

## Related

- **ADR-0011** — no bridges at done. Governs code shape; this ADR governs where
  that shape lives. Authoring authority is 0011 #2, not 0016.
- **ADR-0014** — UI Toolkit primary stack. P4 is the proper resolution of the
  authoring-surface question this ADR declines to take.
- **ADR-0015** — configuration narrowing. Independent axis: 0015 governs how a
  system's scope narrows, 0016 governs where the system lives.
- **ADR-0017** — multi-chassis standard. Consumer; cites this ADR for
  `VehicleBodyworkAnchors` sibling discipline and Bodywork-as-distinct-category.
  Note: 0017 was Accepted 2026-07-05 citing this ADR while the document did not
  yet exist — see the authoring note.
- **Number collision closed.** `production/td-verdicts/2026-07-29-whole-game-health-opt-audit.md:262`
  proposes ADR-0016 for a "Derived-State Caching Pattern." Number 0016 is
  claimed for composition. Derived-state caching routes to
  `.claude/docs/technical-preferences.md` or a new ADR-0018.
- `production/td-verdicts/2026-06-17-scene-split-verdict.md` — Option B hybrid;
  the worked example for Application 2, cited at its own request (line 69).
- `feedback_composition_smell_test` — the smell test in memory form.
- `feedback_td_briefing_discipline` — the required-brief-field rule.
- `feedback_nested_prefab_topology_orphans_overrides` — why composition mistakes
  are expensive to reverse.
- `production/polish-captures/2026-09-09-adr-0016-prefab-category-coherence.md` —
  capture + full TD verdict for this ADR.
- `production/remediation-plan-2026-09-08.md` §0.2 — the audit item this closes.
