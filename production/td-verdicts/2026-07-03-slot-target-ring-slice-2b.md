# TD Verdict — SlotTargetRing Slice 2b Canonical Cut — 2026-07-03

## Context

Slice 2b executes the canonical widget swap per ADR-0011. Slice 2a
(2026-07-02) shipped `SlotTargetRing.cs` + author menu + dormant
`ResolveRing` sibling in isolation; 2b retires `SubsystemBar` and
`SubsystemMarker` in a single atomic cut and re-authors every vehicle
prefab.

Prior verdicts:
- `production/td-verdicts/2026-07-01-slot-target-ring-refactor.md` — governs
  the multi-slice refactor
- `production/td-verdicts/2026-07-02-slot-target-ring-widget.md` — Slice 2a
  non-goals (widget in isolation)

Slice 1 capture: `production/polish-captures/2026-07-02-slot-target-ring-prep.md`

Spec sheet: `design/hud/slot-target-ring.md`

## Verdict — APPROVE-WITH-CHANGES

The proposed canonical cut is architecturally sound. Six binding decisions
below tighten scope so 2b stays one clean cut and doesn't leak into
Slice 3's structural / MainBar-c category.

## Binding Decisions

### Q1 — Tooltip payload ownership
**Verdict: (c) — Ring surrenders tooltip ownership; `VehiclePartHitZone` is
sole tooltip source per slot.**

`SubsystemBar` maintains a mid-hover poll (SubsystemBar.cs:370-376) that
fights the hit-zone tooltip for the same slot — bar tooltip + zone tooltip
compete on tooltip-key ownership because bars-and-markers were two widgets
serving one semantic slot. Rings collapse that. `VehiclePartHitZone` is the
primary semantic hover target (W7.27 decision, VehiclePartHitZone.cs:18-24);
rings are HUD readouts, not target zones.

**Action:** delete `_tooltip` + `_tooltipKey` fields from `SlotTargetRing.cs`,
drop them from the `Bind` signature, remove the `_tooltip.Show`/`Hide` calls
from `OnPointerEnter`/`OnPointerExit`. Ring keeps `OnHover` firing (for
hover mirror) and `_interactable` raycast toggle (drag-cast surface).

**Non-goal (enforced):** no 7-arg tooltip overload added to rings.

### Q2 — Fill fraction vs band color
**Verdict: band color alone in 2b. Radial fill is Slice 3.**

Player fantasy is gunsight readout — target state, not depletion percent.
HP-counts-as-numbers is the tooltip's job (hit-zone-owned per Q1). Adding
`Image.Type.Filled` radial in 2b conflates the canonical cut with a
visual-language iteration.

**Action:** ship 2b with solid band color (existing 2a behavior). Slice 3
adds radial fill as serialized fields (`_previewArcStartDeg`,
`_previewArcClockwise`, etc.) if playtesting reveals a legibility gap.

### Q3 — VehiclePartHitZone paired target
**Verdict: (a) — zone→ring proxy is a rename-only change; no hover-ownership
shift in 2b.**

Zones remain primary drag-cast surface (pixel-perfect on part art). Rings
become the mirror that lights up when the zone is targeted. This is
`BindHitZone(pairedTarget: bar)` → `pairedTarget: ring`. No `_combatHitTargets`
list reordering, no per-slot-kind hover strategy split, no first-hit-wins
semantic change.

**Action:** rename the `pairedTarget` argument sites; that's it.

### Q4 — Author-time destructive replacement
**Verdict: (a) — atomic `DestroyImmediate` of stale nested children, gated
on child-name match.**

Options (b)/(c) fail: (c) is bimodal (ADR-0011 forbidden), (b) shifts a
machine-verifiable step to manual designer work (defeats the pre-author-bake
hook). Option (a) is correct IF the destroy is name-scoped: `SeedHudAnchor`
finds and destroys any child named `SubsystemBar` or `SubsystemMarker`
(deterministic prefab-instance names), then upserts the ring child. Anchor
RectTransform itself is never touched — designer positional edits on
`_entries` survive.

**Action:** at top of rewritten `SeedHudAnchor`, enumerate anchor children;
`DestroyImmediate` any whose name matches the retired prefab names; then
upsert the ring child (idempotent).

### Q5 — Ring at structural slot?
**Verdict: DEFER MainBar-c to Slice 3. MainBar is NOT a bridge in 2b.**

ADR-0011's "no bridges at done state" applies to the *done state* of a
shipped feature. MainBar in 2b's shipped state is not a transitional adapter
over `SubsystemBar` — it's a *distinct widget* representing the *structural*
slot with different semantics (armor pill co-location, enemy name label,
structural-hit-zone routing). Slice 2b closes the per-slot-widget category
(Engine/Weapon/Mobility/Exposable). Slice 3 closes the structural-widget
category (MainBar-c = ring-on-chassis + armor pill split).

Categorical scope narrowing via distinct data tables is ADR-0011-clean —
same logic as ADR-0015: each application is a canonical vertical slice, not
a bridge over incomplete generalization.

**Slice 2b done state:** `SubsystemBar` + `SubsystemMarker` deleted;
`MainBarWidget` persists unchanged serving the structural slot. Grep gate:
zero `SubsystemBar` / `SubsystemMarker` refs.

### Q6 — Order of operations
**Verdict: reorder so the compiler becomes the safety net.**

Proposed sequence risked a mid-refactor window where prefabs contain stale
children AND code still compiles. Correct sequence:

1. Rewrite `SlotTargetRing.Bind` — drop tooltip params (per Q1). Update 2a
   tests.
2. Rewrite `VehicleHudAnchors` — delete `ResolveBar` + `ResolveMarker`
   methods; `ResolveRing` becomes primary in doc comment.
3. Rewrite `VehicleBarStack.BuildPerSlotBars` + `Update()` per-slot branch
   + `TryBuildRestWidgets` + `UpdateRestBound` + `RebuildForCurrentVehicle`.
   **Compilation now breaks on every `SubsystemBar` / `SubsystemMarker` type
   ref outside the retired files themselves.**
4. Retarget `VehiclePartHitZone.BindHitZone(pairedTarget: ring)` and update
   doc comments in `BarWidget.cs` / `MainBarWidget.cs` / `ICombatHoverTarget.cs`
   / `AttackStateController.cs` / `VehicleVisual.cs`.
5. Delete `SubsystemBar.cs`, `SubsystemMarker.cs`, their `.meta` files.
6. Rewrite `CombatPrefabAuthor.SeedHudAnchor` with name-scoped
   `DestroyImmediate` sweep (per Q4). Update the `SlotTargetRing.prefab`
   ref inside.
7. Delete `AuthorSubsystemBarMenu` + `AuthorSubsystemMarkerMenu` methods +
   font material load path referencing `RussoOne SDF - SubsystemBar
   HpText.mat` (SubsystemBar-specific).
8. **Compile green gate.** Only proceed if EditMode compiles clean.
9. Re-author all 7 vehicle prefabs via Author Combat Prefab menu.
10. Delete `SubsystemBar.prefab` + `SubsystemMarker.prefab` + `.meta` files
    from `Assets/Prefabs/CombatView/`.
11. Delete font material `Assets/Fonts/RussoOne SDF - SubsystemBar
    HpText.mat` (SubsystemBar-only consumer; ring has no HP text per Q1).
12. Full EditMode test run. All 13 ring tests + full suite green.
13. PlayMode smoke on Player + Dredge + one enemy — hover, drag-cast,
    damage-state visual.

Steps 5-7 must land in the same commit-worth-of-work as step 3; splitting
creates the bridge window ADR-0011 forbids.

## Files at Risk (missed / added)

- **`Assets/Fonts/RussoOne SDF - SubsystemBar HpText.mat`** — font material
  loaded by `CombatPrefabAuthor.cs:1345` for SubsystemBar's HP text. Ring
  has no HP text per Q1 → material has zero consumers after 2b. Delete.
- **`BuffTooltipWidget.Show(pos, key, name, DamageState, info)` 5-arg
  overload** — only consumer is `SubsystemBar.cs:381`. After 2b, orphan.
  Delete the overload OR document why it stays. (`VehiclePartHitZone` uses
  the 7-arg overload; `VehicleBarStack.HandleWidgetHover:929` uses 4-arg
  `(pos, key, header, body)`; `BuffIconWidget:136` uses 2-arg
  `(pos, badge)`. The 5-arg overload is unique to SubsystemBar.)
- **`_previewArcImage` + `_targetHoverOutlineImage` on `SlotTargetRing`** —
  authored serialized fields not bound to any Refresh path yet. Stay
  authored-but-inactive on the prefab (Slice 3 lights them up). This is
  dormant serialized capacity per `feedback_data_flag_lagging_dependency`,
  NOT a bridge. Document explicitly.

## Non-Goals (enforced for Slice 2b)

1. No MainBar-c changes — structural slot untouched.
2. No radial fill / preview arc wiring — Slice 3.
3. No `_targetHoverOutline` wiring — Slice 3 with drag-cast integration.
4. No `_combatHitTargets` list reordering — Q3 is rename-only.
5. No new tooltip overloads — Q1 removes tooltip ownership from rings.
6. No armor pill / MainBar visual work — Slice 3.
7. No player-smooth / enemy-jagged outline sprite work — Slice 3 variants.
8. No `CombatBarPalette` changes.
9. No animation / tween — instant color flip on Refresh (same as 2a).

## Slice 2c / 3 Scope Shape (post-2b)

**Slice 2c — DELETE.** No 2c. Refactor is 2a (widget) → 2b (canonical cut
for per-slot category) → 3 (structural + variants + fill fraction).

**Slice 3 scope:**
- MainBar-c: ring on chassis slot (uses same `SlotTargetRing` prefab
  variant); armor pill splits out as its own widget.
- Player-smooth vs enemy-jagged outline sprite variants (prefab variants
  of `SlotTargetRing.prefab`, override `_outlineImage` sprite).
- Radial fill fraction wiring (Q2 deferral).
- `_targetHoverOutlineImage` wired into CombatHud's drag-cast service.
- Slice 3 grep gate: zero `MainBarWidget` refs.

## Validation Criteria

2b is right if:

- Grep for `SubsystemBar` / `SubsystemMarker` across `Wasteland Run/Assets/**`
  returns zero matches (except archived captures).
- All 7 vehicle prefabs render rings in Prefab Mode at authored anchor
  positions (no drift).
- 13 ring tests + full EditMode suite green.
- PlayMode: hovering a wheel/engine/weapon lights the corresponding ring's
  paired-target state; drag-cast lands on the intended slot; tooltip shows
  once per hover (no double).
- No visual regression on structural hit zone / MainBar (untouched).

## Slice 2b Precondition (blocking)

`Assets/Prefabs/CombatView/SlotTargetRing.prefab` does not exist on disk
(only `.cs` + tests + author menu shipped in 2a). The author menu was NOT
run in the Slice 2a session. Slice 2b requires this prefab to exist before
`SeedHudAnchor` can nest it under each vehicle anchor.

**Resolution:** first step of 2b is running `Tools > Wasteland Run > Author
SlotTargetRing Prefab` from a warm Unity Editor session. Batchmode can
invoke the menu via `-executeMethod
WastelandRun.CombatView.Editor.CombatPrefabAuthor.AuthorSlotTargetRingPrefabMenu`
without opening the Editor GUI.

## Verdict Signature

**APPROVE-WITH-CHANGES.** Proceed with the 13-step sequence in Q6 after
running the Slice 2a author menu to seed the ring prefab on disk. Do NOT
begin any Edit/Write until the user approves the capture doc containing
this verdict.
