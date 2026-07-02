# TD Verdict — SlotTargetRing Widget (Slice 2a)

**Date:** 2026-07-02
**Scope:** Author `SlotTargetRing.cs` MonoBehaviour + `SlotTargetRing.prefab`
in isolation. No anchor swaps, no bind-loop rewrite, no vehicle re-authoring.
Slice 2b (later session) handles the destructive canonical cut.

**Umbrella verdict:** `production/td-verdicts/2026-07-01-slot-target-ring-refactor.md`
**Spec sheet:** `design/hud/slot-target-ring.md`
**Prep capture:** `production/polish-captures/2026-07-02-slot-target-ring-prep.md`

## Requested class shape

New file: `Assets/Scripts/CombatView/SlotTargetRing.cs` (~270 lines)

- `class SlotTargetRing : MonoBehaviour, ICombatHoverTarget, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler`
- Nested `HideRule` enum mirroring `SubsystemBar.HideRule` verbatim (5 members)
- `Bind` / `Refresh` / `SetHideRule` / `SetTargetHover` / `SetInteractable` / `ClearHandlers` — mirrors `SubsystemBar` verbs so Slice 2b call-site swap is minimal
- Serialized: `CombatBarPalette _palette` reference, five `Image` refs, geometry (40 outer / 3 outline / 28 icon), `_offlineDim = 0.4f`, `_hideRule` default
- Inline palette fallbacks for null-tolerant test instantiation
- `OnValidate` guard per `feedback_executealways_asset_guard`

## Technical Director Review

**Verdict: AMEND** — accept the shape with one enforceable non-goal added
to the spec sheet + capture doc + this verdict before code lands.

### Accepted as proposed

1. **`HideRule` enum duplication** — mirroring on `SlotTargetRing` is an
   in-flight state between two atomic commits, not an ADR-0011 bridge.
   Covered by ADR-0011 exception #1 (one-shot migrators) by analogy. Do
   NOT extract to a shared location — that would create a permanent third
   home for an enum that should live on exactly one class at done state.
   `SubsystemBar.HideRule` deletes in Slice 2b; only `SlotTargetRing.HideRule`
   remains.

2. **`ICombatHoverTarget` implementation** — ring correctly consolidates the
   drag-cast target role from `SubsystemBar` with the marker's visual role.
   One hover target per slot, not two. `_iconImage` carries `raycastTarget=false`
   so the marker sprite doesn't intercept pointer events.

3. **Inline fallback palette values** — mirrors the `SubsystemBar` /
   `SubsystemMarker` precedent for EditMode test instantiation without
   requiring the palette SO to be wired. Not a magic-number violation
   because `CombatBarPalette` remains the authored source of truth; inline
   values are the null-guard, and Slice 2b tests can inject a stub palette
   SO if we want stricter coverage.

### Required amendment — enforce 2a non-goal

**Slice 2a ships `SlotTargetRing.cs` and `SlotTargetRing.prefab`, but ZERO
vehicle-authoring code references either.** No `AuthorPlayerVehicle` /
`AuthorEnemyArchetypePrefabs` / `BuildVehicleHudAnchors` / `SeedHudAnchor`
path mounts a ring in Slice 2a. Tests instantiate `SlotTargetRing` directly;
the running game continues to use `SubsystemBar` + `SubsystemMarker`
exclusively.

**Why this matters:** if any authoring path gains a ring mount in 2a, some
slots on some vehicles become rings while the rest remain bars, and 2b
stops being a single canonical cut per ADR-0011 (production state becomes
bimodal on merge). The discipline that keeps 2b atomic is enforced HERE,
in 2a scope.

**Enforcement:**
- Spec sheet `design/hud/slot-target-ring.md` — add a "Slice 2a Non-Goals"
  section listing the forbidden touch surface.
- Prep capture `production/polish-captures/2026-07-02-slot-target-ring-prep.md`
  — mirror the non-goal.
- Slice 2a commit review — grep the diff for `SlotTargetRing` references
  outside `SlotTargetRing.cs`, its prefab, its tests, and the
  `VehicleHudAnchors.ResolveRing` sibling. Any other reference blocks the
  commit.

### Gate-check requirement — EditMode-green tests

Per `feedback_gate_check_requires_green_tests`: Slice 2a close-out requires
EditMode-green attestation on `SlotTargetRingTests`. Compilation-green
alone is insufficient. Minimum coverage:

- `Bind` sets SlotId, DisplayName, Info, tooltip, tooltipKey
- `Refresh` sets fill color per damage band (green > 0.80, yellow > 0.40,
  red ≤ 0.40)
- `Refresh` applies HideRule SetActive matrix (all 5 rules × alive/full/
  damaged/destroyed permutations)
- `SetTargetHover(true)` flips `_targetHoverOutlineImage.enabled` to true;
  false flips it off
- Palette null-fallback path returns the inline fallback colors

Run mode: Unity batchmode with `-runTests` (no `-quit` per
`project_unity_batchmode_no_quit`).

### ADRs at risk — verified clean

- **ADR-0010** (single slot vocabulary): ring uses `string slotId` throughout. Clean.
- **ADR-0011** (no bridges): enforced by the 2a non-goal above.
- **ADR-0014** (UI stack): ring is UGUI matching `CombatHud.prefab`; no new stack introduced. UI Toolkit port is separate future work.

## Verdict

**AMEND** — accept the class shape as proposed, with the non-goal amendment
enforced across spec sheet + capture doc before `SlotTargetRing.cs` is
written. Tests must ship EditMode-green in the same commit. Once those
constraints are met, APPROVE Slice 2a.
