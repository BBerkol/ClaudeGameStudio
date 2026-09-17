# ADR-0018: UGUI Retention for Transient World-Anchored Annotations

## Status

Accepted (2026-09-13)

## Date

2026-09-13

## Last Verified

2026-09-13 — every canvas claim below was read from the shipping prefab YAML on
this date. See the audit table in Context.

## Decision Makers

User (BertanBerkol), Claude (technical-director-equivalent session)

## Supersedes

**ADR-0014 — "UI Toolkit as Primary Stack, UGUI Retained for World-Space Popups
Only" (Accepted 2026-06-13).**

Superseded rather than amended. ADR-0014's central factual premise — that the
retained `Popups` canvas is world-space — is false, and it is asserted in the
document's **title, summary, decision, architecture diagram, performance
rationale and consequences**. An append-only amendment would have left six wrong
statements in force with one correction buried at the end, which is the
duplicated-wrong-rationale failure this project treats as the defect itself.

ADR-0014's body is **not edited**. It carries a Status pointer to this document.

Capture: `production/polish-captures/2026-09-13-adr-0014-supersession.md`.

## Summary

Wasteland Run adopts UI Toolkit (UXML + USS + C# controllers) as the primary
stack for all screens, menus, HUD and panels — unchanged from ADR-0014 and still
correct.

UGUI is retained for one **category**: canvases that render **transient,
per-event or per-interaction annotations positioned by a live world transform**,
with no persistent screen-space layout of their own. Membership is a short,
closed, ADR-owned **registry**, not a pattern — because the members share no
mechanically-checkable field, and dressing an allowlist up as a rule is worse
than declaring it.

## Context

### Why ADR-0014 could not stand

ADR-0014 was written 2026-06-13 and its canvas inventory was never re-checked
against the prefabs. A 2026-09-13 audit read every `Canvas` component in
`Assets/Prefabs` and `Assets/Scenes` — **14 across 7 files** — and diffed them
against the document.

| ADR-0014 claim | Verified reality (2026-09-13) | Verdict |
|---|---|---|
| Title + `:24-25` + `:152`: UGUI retained for "the **world-space** `Popups` canvas" | `Popups` is `m_RenderMode: 1` — **ScreenSpaceCamera**. `CombatHud.ApplyScreenSpaceCamera` (`:520-526`) actively forces it there on every HUD build, and `CombatHud.cs:500-502` says so in a comment | **FALSE** |
| `:113-115`: exception justified because "UGUI's direct WorldSpace Canvas" avoids per-frame `RuntimePanelUtils.ScreenToPanel` cost | `Popups` is not a WorldSpace canvas, and `DamagePopupWidget` already performs world→screen→canvas-local conversion — the very cost cited as disqualifying — and performs acceptably | **SELF-REFUTING** |
| `:196-197`: "No new UGUI `Canvas` may be added outside the `Popups` canvas subtree" | 13 of the 14 shipping canvases are outside it | **UNENFORCEABLE** |
| Current State table lists 5 UGUI surfaces | Omits `HudAnchors` (×4 vehicles), `HitZonesCanvas` (×4), `CardHand`, `BuffStripCanvas`, and `DamagePopupCanvas` | **INCOMPLETE** |
| `:91`: "Zero UI Toolkit in the project today. Zero UXML, zero USS." | 18 `.uxml`, 22 `.uss`, 19 scripts referencing `UIDocument` | **STALE** (true when written; presented as current) |
| `:247` P5 gate: forbid `Canvas` outside the `Popups` subtree | Would fire on 13 canvases on the day it shipped | **CANNOT SHIP AS WRITTEN** |

Verified canvas inventory:

| Canvas | Render mode | overrideSorting | Order | File |
|---|---|---|---|---|
| `Combat_HUD` | ScreenSpaceCamera | 0 | 10 | `CombatHud.prefab` |
| `CardHand` | (nested — mode inert) | 1 | 25 | `CombatHud.prefab` |
| `Popups` | ScreenSpaceCamera | 0 | 60 | `CombatHud.prefab` |
| `Debug` | ScreenSpaceCamera | 0 | 110 | `CombatHud.prefab` |
| `DamagePopupCanvas` | ScreenSpaceOverlay | 0 | 30 | `DamagePopupSpawner.prefab` |
| `BuffStripCanvas` | (nested — mode inert) | 1 | 22 | `MainBar.prefab` |
| `HudAnchors` | WorldSpace | 0 | 20 | ×4 vehicle prefabs |
| `HitZonesCanvas` | WorldSpace | 0 | 15 | ×4 vehicle prefabs |

Plus two built at runtime by `CombatHud.BuildWorldCanvas`: `IntentCanvas`
(WorldSpace, order 5) and `TargetingReadoutCanvas` (WorldSpace, order 30).

### Why the obvious replacement predicates also fail

Two candidate rules were tested and both break in **opposite** directions:

- **"World-space render mode"** excludes `Popups` — the one surface the
  exception exists for — and wrongly catches `CardHand`, whose serialized
  `m_RenderMode: 2` is inert because it is nested.
- **"Parented under a vehicle root"** wrongly catches `HudAnchors`, which hosts
  MainBar and the rings — **the single largest surface P4 exists to migrate**.

The three genuine members are ScreenSpaceCamera / WorldSpace / WorldSpace, one
screen-space sibling and two world-space, one vehicle-parented and one
LaneAxis-parented. They share no checkable field.

## Decision

### 1. The invariant

> UGUI is retained for canvases rendering **transient, per-event or
> per-interaction annotations positioned by a live world transform**, with no
> persistent screen-space layout of their own.

`Popups` — a number that spawns, floats and dies. `HitZonesCanvas` — a raycast
mask shaped by a sprite silhouette. `IntentCanvas` / `TargetingReadoutCanvas` —
follower panels registered to `VehiclePositionAnimator`.

`HudAnchors`, MainBar and the rings **fail** it: persistent HUD state with an
authored layout, which is precisely why P4 wants them in USS.

The invariant is **semantic and deliberately not greppable.** It describes *why*
a surface resists USS, not how it happens to be configured today, so it stays
correct if `IntentCanvas` is reparented or `Popups` changes render mode.

### 2. The registry (not a predicate)

> **SUPERSEDED by Amendment A (2026-09-17)** — the table below is the original
> 4-member registry, retained as the record; the current 6-member registry,
> corrected invariant and >7 cap live in §A.2. Do not cite this table as
> current membership.

| Member | Exit criterion — what would move it to UI Toolkit |
|---|---|
| `Popups` | Damage numbers stop tracking world transforms, or `ScreenToPanel` is measured acceptable at expected density |
| `HitZonesCanvas` | UI Toolkit gains a sprite-alpha hit test equivalent to `Image.alphaHitTestMinimumThreshold` |
| `IntentCanvas` | The intent telegraph stops following the enemy transform |
| `TargetingReadoutCanvas` | Same, for the player-side readout |

**Nothing else is a member.** `Combat_HUD`, `Debug`, `CardHand`,
`BuffStripCanvas`, `HudAnchors` and `DamagePopupCanvas` are P4 scope.

**Growth threshold:** if the registry exceeds **5 members**, the category is
wrong and must be re-derived, not extended. Naming this now rather than
discovering it later is the price of not having a mechanical rule.

`DamagePopupCanvas` is listed as P4 scope pending one open question (below); it
is deliberately **not** granted membership by default. Granting membership to an
unexamined canvas is how registries rot.

### 3. The corrected rationale

The exception is **ergonomic, not performance**. UGUI's `RectTransformUtility`
world-anchoring for short-lived annotations is materially simpler than the UI
Toolkit equivalent, and these surfaces are small and transient. ADR-0014's
performance argument is withdrawn: it was never measured, and it is false for
its own named example.

### 4. P5 rescoped

The gate asserts **no `Canvas` component in any prefab outside the declared
registry**, with the registry as an explicit list in the gate, cross-referenced
to this ADR. It is called a registry check, not a pattern match — the honesty
matters, because a future reader who believes it is a rule will try to extend it
by reasoning rather than by amending this document.

## Amendment A (2026-09-17) — category re-derived; `HudAnchors` seated; P4 closes on P4a scope

Governing verdict: `production/td-verdicts/2026-09-17-structural-debt-cleanup.md`
§4. Trigger: a code read found the registry **factually wrong on the `Popups`
row** — damage numbers render on `DamagePopupCanvas` (ScreenSpaceOverlay 30,
built by `DamagePopupSpawner`), not `Popups` (order 60, which hosts the buff
tooltip). That settles §"Open questions" both ways at once: the fifth registry
seat was already spoken for, so seating `HudAnchors` exceeds the cap and
triggers the re-derivation §2 prescribes. Re-derived here, in place.

### A.1 The corrected invariant

`transient` is DROPPED — it was descriptive of the first four members, never
causal. The mechanism that actually resists USS:

> UGUI is retained for canvases whose element **position or hit-shape is
> derived from a live scene transform or sprite** — scene-graph parenting,
> per-frame world-transform following, or sprite-alpha hit testing. UI Toolkit
> has no scene graph and no sprite-alpha hit test; these surfaces would have to
> re-implement one in C#, which is a reimplementation, not a migration.

`Combat_HUD`, `Debug`, `CardHand` and the (since-deleted) nested CardHand
canvas still fail it — the category retains its teeth.

### A.2 The registry, corrected (supersedes §2's table)

| Member | Exit criterion — what would move it to UI Toolkit |
|---|---|
| `Popups` | Hosts the **buff tooltip**, not damage numbers (audit correction). Exits when the tooltip stops positioning from a hovered widget's world rect (`BuffTooltipWidget.cs:172-174`) — i.e. tooltip anchoring moves into panel space |
| `HitZonesCanvas` | UI Toolkit gains a sprite-alpha hit test equivalent to `Image.alphaHitTestMinimumThreshold` |
| `IntentCanvas` | The intent telegraph stops following the enemy transform |
| `TargetingReadoutCanvas` | Same, for the player-side readout |
| `DamagePopupCanvas` | Damage numbers stop projecting from a live world anchor, or `RuntimePanelUtils.ScreenToPanel` is measured acceptable at the 8-popup pool's peak spawn density |
| `HudAnchors` | See A.3 — the only member with a two-part, ORDERED exit criterion |

MainBar, the per-slot `SlotTargetRing`s, `EnemyNumberBadge`s and the nested
`BuffStripCanvas` count as **children of `HudAnchors`, not separate members** —
stated explicitly so the count cannot rot.

**Cap reset:** six members; the category is re-derived (again, not extended) at
**>7**. Stated explicitly — a cap silently carried forward is worse than none.

### A.3 `HudAnchors` exit criterion

Migrates when **both** hold, in this order:

1. **Authoring parity.** Per-archetype anchor placement can be authored against
   the live vehicle sprite with the same direct-manipulation affordance Prefab
   Mode provides today (`VehicleHudAnchors._entries` +
   `CombatPrefabAuthor.SeedHudAnchor`'s `AnchorPositions` bake). A blind
   numeric table does not satisfy this — it is the exact workflow
   `VehicleHudAnchors.cs:14-22` was built to retire after Dredge drifted under
   it.
2. **Conversion cost bounded by a real measurement.** A **single** follower
   component owns the world→panel conversion per vehicle per frame (fanned out
   by cached local offsets, never per element), measured at **≤0.3 ms/frame for
   2 vehicles × 12 elements at 1080p** on the reference machine, including the
   layout-invalidation cost of the inline position writes.

**Ordering is not symmetric.** Until (1) exists, (2) is irrelevant and must not
be spiked — a passing performance number would otherwise be used to justify
trading away a designer capability. Re-open this row when a UI Toolkit
sprite-relative placement workflow exists, not when someone has spare cycles to
profile.

### A.4 Consequences of the audit correction

This is the **second** canvas-identity error in the ADR-0014/0018 lineage, and
it was found by a code read, not by the YAML audit. The Validation Criteria's
"canvas inventory matches a fresh YAML read" is therefore necessary but **not
sufficient**: YAML tells you a canvas exists; only code tells you what renders
into it.

### A.5 P4 and P5 under this amendment

**P4 closes on P4a scope** (screen-space surfaces — landed 2026-09-14, defect
wave remediated 2026-09-16/17). P4b (HudAnchors migration) is retired as a
phase; its subject is now registry row A.3. **P5** asserts no `Canvas` outside
the six-name registry, as an explicit literal list in `tools/ci/grep-gates.sh`
cross-referenced to A.2 — shipped as its own slice after a fresh per-canvas
CODE audit (per A.4, a YAML sweep cannot certify the list), with the gate
negative-tested against a deliberately added canvas.

## Migration Plan

Carried forward from ADR-0014 unchanged except P5. P1–P3 landed; P3's detail is
preserved in ADR-0014 and not restated here.

| Phase | Scope | Status |
|---|---|---|
| **P1** | USS design tokens, base controls, `PanelSettings`, `WastelandRun.UI` asmdef | LANDED |
| **P2** | Slice 6 node-map + Run Complete authored UI Toolkit native | LANDED |
| **P3** | `CardRewardPicker` + `CombatOutcomeOverlay` migrated | LANDED 2026-06-23 |
| **P4** | ~~Migrate `Combat_HUD`, `CardHand`, `BuffStripCanvas`, `HudAnchors`, MainBar and rings~~ **Amendment A: closes on P4a scope** (`Combat_HUD`/`CardHand`/`BuffStripCanvas` panel — LANDED 2026-09-14, remediated 2026-09-16/17). `HudAnchors` is registry row A.3, not migration scope | **CLOSED 2026-09-17** |
| **P5** | CI registry check per §4 + A.5 | **DONE 2026-09-17** — `canvas_registry_gate` in `tools/ci/grep-gates.sh`: YAML half (globs all prefabs+scenes, resolves every `!u!223` to its owning GameObject via `m_GameObject`, 7-name allow-list) + CODE half (`AddComponent<Canvas>` pinned to exactly 2 sites; `BuildWorldCanvas` name literals must be registry names) + retired-name tripwires (`Debug`, `Combat_HUD`) + anti-vacuity (zero canvases found ⇒ FAIL; allow-list >7 ⇒ FAIL citing the A.2 cap). Negative-tested in 5 steps, all confirmed firing, incl. the nested-PrefabInstance and vacuous-code-half cases. Prerequisite deletions: the `Debug` and `Combat_HUD` canvases (both rendered nothing — see Consequences), verdict `production/td-verdicts/2026-09-17-p5-canvas-deletions.md` |

Rollback shape is unchanged: each phase ships in its own commit and reverts
independently.

## Open questions carried into P4 — BOTH RESOLVED by Amendment A (2026-09-17)

**`HudAnchors`** — resolved: seated in the registry under the re-derived
invariant (row A.3), with an ordered two-part exit criterion in which authoring
parity gates the cost measurement, never the reverse. The deciding argument was
authoring, not performance.

**`DamagePopupCanvas`** — resolved by code read: damage numbers render THERE
(ScreenSpaceOverlay 30, positions projected from live world anchors), not on
`Popups`, which hosts the buff tooltip. Both rows corrected in A.2. Original
question prose: git history + capture
`production/polish-captures/2026-09-17-adr-0018-amendment-a.md`.

## Consequences

**P5 closeout note (2026-09-17):** the category audit found two canvases that
were registry-shaped but rendered nothing in any played frame (`Debug`,
`Combat_HUD` — both deleted rather than allow-listed); the gate is what stops
a third. Deleting `Combat_HUD` also removed the last runtime canvas
constructor outside the two pinned sites (`CombatHud.BuildCanvas`, an
ADR-0011 #3 bimodal path) — the gate's code half exists precisely because a
static YAML check can never see that failure class.

### Positive

- The retained-UGUI rule is now true of the code it describes.
- P5 becomes shippable; under ADR-0014 it could not have passed on day one.
- The registry makes every exception visible and individually justified.

### Negative

- The registry needs review whenever a member is added — a real maintenance
  cost, and the honest price of a semantic invariant.
- Two ADRs now describe UI stack policy. ADR-0014 is retained for P3's landed
  detail and for the record of what was believed.

### Neutral

- No code changes. This ADR corrects the description, not the implementation.

## Validation Criteria

- The canvas inventory table matches a fresh YAML read. **Re-verify before P4
  opens** — this document's predecessor was wrong precisely because nobody did.
- P5's registry check fires on a `Canvas` added outside the registry, and passes
  on the shipping tree.

## ADR-0011 compliance

The registry is an explicit, enumerated exception list with a stated exit
criterion per member and a re-derivation threshold — not a bridge, a compat
layer, or a bimodal path. It does not let two mechanisms solve one problem: the
members are surfaces UI Toolkit cannot currently express, each with a written
condition under which it migrates.

## Related

- **ADR-0014** — superseded by this document
- **ADR-0011** — no bridges at done state
- **ADR-0015** — configuration narrowing; the registry is the data-table
  equivalent for a policy that resisted a predicate
- `production/remediation-plan-2026-09-08.md` §5.5 — the targeting rework that
  surfaced this
