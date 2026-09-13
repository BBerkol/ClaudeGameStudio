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

## Migration Plan

Carried forward from ADR-0014 unchanged except P5. P1–P3 landed; P3's detail is
preserved in ADR-0014 and not restated here.

| Phase | Scope | Status |
|---|---|---|
| **P1** | USS design tokens, base controls, `PanelSettings`, `WastelandRun.UI` asmdef | LANDED |
| **P2** | Slice 6 node-map + Run Complete authored UI Toolkit native | LANDED |
| **P3** | `CardRewardPicker` + `CombatOutcomeOverlay` migrated | LANDED 2026-06-23 |
| **P4** | Migrate `Combat_HUD`, `CardHand`, `BuffStripCanvas`, `HudAnchors`, MainBar and rings to UI Toolkit. Highest-risk migration | NOT STARTED — HIGH risk |
| **P5** | CI registry check per §4 | Same commit as P4 close |

Rollback shape is unchanged: each phase ships in its own commit and reverts
independently.

## Open questions carried into P4

**`HudAnchors` is a WorldSpace canvas** that positions MainBar and the rings on
the vehicle. P4 migrates those surfaces, which means P4 must solve world-anchored
positioning in UI Toolkit — the exact `ScreenToPanel` cost ADR-0014 worried
about, now genuinely in scope rather than hand-waved.

This ADR does **not** pre-judge it. If P4 measures that cost as unacceptable,
the correct outcome is `HudAnchors` joining the registry with a stated exit
criterion — not a silent exception. That decision belongs to P4 with numbers
attached, and it is flagged here so it is made deliberately.

**`DamagePopupCanvas`** (ScreenSpaceOverlay, order 30, in
`DamagePopupSpawner.prefab`) needs the same determination: whether damage numbers
actually render there or on `Popups` was not established by this audit, and the
answer decides its classification.

## Consequences

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
