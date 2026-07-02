# SlotTargetRing — HUD Widget Spec

**Status:** Draft (governs Slice 2-3 authoring)
**Date:** 2026-07-02
**TD verdict:** `production/td-verdicts/2026-07-01-slot-target-ring-refactor.md`
**Capture:** `production/polish-captures/2026-07-02-slot-target-ring-prep.md`

---

## Overview

`SlotTargetRing` is a single circular widget mounted at each non-structural
slot anchor on a vehicle. It replaces the current `SubsystemBar` +
`SubsystemMarker` pair with one artifact per slot, and pairs with a
horizontal `MainBarWidget` armor pill above the chassis (MainBar-c decision
from TD verdict).

**Player fantasy:** targeting a vehicle should feel like reading a HUD in a
gunsight — circular readouts around a silhouette, with damage state
communicated by outline treatment rather than depleting horizontal bars.

## Designer Editing Contract

This is a **clean prefab system**. Every value below is authored in Prefab
Mode via serialized fields; nothing is procedural-only or hardcoded in C#
outside a single default-value seed.

- **One base prefab:** `SlotTargetRing.prefab` — canonical widget, all
  designer-visible fields exposed on its `SlotTargetRing` MB
- **Two prefab variants** (per TD verdict slice sequence step 4):
  - `SlotTargetRing_PlayerSmooth.prefab` — smooth outline treatment
  - `SlotTargetRing_EnemyJagged.prefab` — jagged outline treatment
- **All colors sourced from `CombatBarPalette`** (single-asset SO); do not
  duplicate hex values on the ring MB — bind by field, not by copy
- **Geometry serialized fields** on the ring MB, edit-time visible in Scene
  and Prefab Mode (per `feedback_edit_prefab_visibility`):
  - `_outerDiameterPx` (default `40f`)
  - `_outlineThicknessPx` (default `3f`)
  - `_iconDiameterPx` (default `28f`)
  - `_previewArcStartDeg` (default `-90f`, 12 o'clock)
  - `_previewArcDirection` (default `Clockwise`)
- **Anchor placement** unchanged: designer authors slot positions on each
  vehicle prefab's `VehicleHudAnchors` component in Prefab Mode; ring
  instances mount at the resolved `RectTransform` per `slotId`
- **MainBar armor pill** ships as a separate serialized widget on the vehicle
  prefab under the same authoring surface; designer edits width scale
  (`_pixelsPerHp`), offset, and palette bindings inline

Author-menu integration: `AuthorPlayerVehicle` / `AuthorEnemyVehicleBase`
mount the appropriate ring variant at every non-structural entry in
`VehicleHudAnchors._entries`. Re-authoring preserves designer edits via
`EditorUpsert` (idempotent — see `VehicleHudAnchors` editor helpers).

## Visual States

Damage state is communicated by **fill color** and **outline treatment**.
Both are serialized fields on the ring MB, sourced from `CombatBarPalette`.

| Damage Band | Fill Ratio | Fill Color | Outline (Player) | Outline (Enemy) |
|-------------|------------|------------|------------------|-----------------|
| Green | > 0.80 | `HpBandGreen` | Smooth 3px | Jagged 3px |
| Yellow | > 0.40 | `HpBandYellow` | Smooth 3px | Jagged 3px |
| Red | > 0 | `HpBandRed` | Smooth 3px | Jagged 3px |
| Offline | == 0 | `MainBarBg` fill | Smooth dim | Jagged dim |
| Preview (hover) | overlay | `PreviewFill` arc | + `TargetHoverOutline` | + `TargetHoverOutline` |

Threshold constants match `SubsystemBar.ColorForBand` verbatim (see capture
doc for the exact ratio boundaries — upper bounds exclusive).

## Ring Geometry

- **Outer diameter:** 40px (matches current bar+marker footprint at 4px/HP
  scale — no re-anchor pass required)
- **Outline thickness:** 3px
- **Icon slot:** 28px centered (reuses `SubsystemMarker` icon sprite refs)
- **Preview arc:** clockwise from 12 o'clock, sweeps `hoverPreviewHp / maxHp`
  fraction, clamped at `curHp` (no over-draw past empty per capture doc
  semantic contract #1)
- **Fill:** solid interior tinted by damage band; no gradient in v1

## Hover Behavior

Ring implements `ICombatHoverTarget` and routes to `AttackStateController`
identical to current `SubsystemBar`:

- Hovering the ring while an attack card is queued shows a preview arc
  (color: `PreviewFill`)
- `AttackStateController.IsHoveringTarget(this)` drives the
  `TargetHoverOutline` outline highlight
- Right-click / drag-back cancels attack — unchanged from current wiring

## HideRule Mapping

Ring inherits the semantic contract from `SubsystemBar.HideRule` via
`VehicleBarStack.ResolveHideRule`:

| SlotDefinition | Combat | Rest | Ring behavior |
|---------------|--------|------|---------------|
| `AttackStateGated` | HideOnFullUnlessAttackActive | AlwaysVisible | Ring always renders; outline treatment shifts on attack focus |
| `DamagedOnly` | HideOnFullOrDestroyed | AlwaysVisible | Ring visible when damaged or hovered; hidden at full+idle |
| `AlwaysVisible` | AlwaysVisible | AlwaysVisible | Ring always visible |

**Key change from bars:** where `HideOnFullUnlessAttackActive` previously
hid the entire bar off-attack, the ring stays visible and communicates full
state via green fill + smooth outline. This is intentional: rings serve
targeting fantasy even at full HP. Designer contrast tuning (idle-Full
outline dim) deferred to Slice 3+.

## Slot Exclusions

Ring is **not** authored for:
- `SlotDefinition.IsStructural == true` (chassis handled by MainBar-c ring
  on the frame slot; frame itself has no ring)
- `SlotKind.Armor` (armor is buffer, not a slot — see
  `project_armor_not_subsystem`; armor renders as horizontal pill above
  chassis per MainBar-c)

## MainBar-c Decision

Per TD verdict:
- **Armor:** horizontal pill above chassis, palette color `MainBarArmor`,
  width = `curArmor × _pixelsPerHp` (default 4), hidden when `curArmor == 0`
- **Structural HP:** ring on the chassis slot (uses same `SlotTargetRing`
  variant, no special-case widget)
- Retires `MainBarWidget.cs` in Slice 3 once the pill widget authors
  cleanly on all vehicle prefabs

## Tooltip Payload

Unchanged contract from current `SubsystemBar` / `BuffTooltipWidget`:
- Hover shows slot display name + current/max HP + `OfflineConsequenceText`
  (Hull / Weapon / Engine / Mobility / Exposable dispatch)
- Tooltip suppressed when ring is on a slot marked `IsStructural == false`
  AND the ring is hidden (DamagedOnly + full HP)

## Slice 2a Non-Goals (TD-enforced)

Slice 2a authors `SlotTargetRing.cs` + `SlotTargetRing.prefab` in isolation.
The following surfaces are **explicitly not touched** in 2a per the TD
verdict at `production/td-verdicts/2026-07-02-slot-target-ring-widget.md`:

- No mount in `AuthorPlayerVehicle` / `AuthorEnemyArchetypePrefabs` /
  `AuthorCombatPrefab` — vehicle authoring code emits zero `SlotTargetRing`
  references
- No changes to `BuildVehicleHudAnchors` / `SeedHudAnchor` / `SeedMainBarAnchor`
- No changes to `VehicleBarStack.BuildPerSlotBars` — bind loop stays on
  `ResolveBar` / `ResolveMarker`
- No deletion of `SubsystemBar.cs` / `SubsystemMarker.cs` / their prefabs
- `VehicleHudAnchors.ResolveRing(slotId)` may ship as a sibling to the
  existing resolvers (dormant surface), but no runtime code calls it yet

**Why:** if any vehicle prefab mounts a ring in 2a, the running game
becomes bimodal (some slots ring, some bar), and Slice 2b stops being a
single canonical cut per ADR-0011. The atomicity of 2b is enforced by
2a's non-goals.

**Grep gate at commit review:** any reference to `SlotTargetRing` outside
`SlotTargetRing.cs`, `SlotTargetRing.prefab`, `SlotTargetRingTests.cs`,
and `VehicleHudAnchors.ResolveRing` blocks the Slice 2a commit.

## Deferred to Later Slices

Explicitly out of scope for Slice 2 canonical cut:
- **Ring damage pulse** (`Sin(π·t)` envelope on curHp drop — carry forward
  from `MainBarWidget._hpPulseDurationSec`, but defer authoring)
- **Player idle-Full contrast level** (subtle dim on Full+idle so damaged
  rings pop more; designer tuning pass in Slice 3+)
- **Prefab variant art authoring** (jagged vs smooth outline sprites) —
  ships as Slice 2 placeholder + Slice 3 art polish
- **Ring pulse on Offline transition** — surface for designer request
  before authoring

## Dependencies

- `VehicleHudAnchors` (Slice 2.6 — already shipped) — anchor catalog
- `CombatBarPalette` (single palette SO) — color source
- `AttackStateController` + `ICombatHoverTarget` — hover routing
- `VehicleBarStack` — bind loop; rewritten in Slice 2 to drive rings

## Acceptance Criteria

- AC-RING-1: `Author Combat Prefab` mounts a `SlotTargetRing` at every
  non-structural `VehicleHudAnchors` entry on every vehicle prefab.
- AC-RING-2: Combat playtest — bar-visibility semantics unchanged from
  pre-swap (rings hide/show under the same conditions bars did).
- AC-RING-3: Rest playtest — all rings visible on entry per `BindForRest`
  override; hover routing intact.
- AC-RING-4: Armor pill renders above chassis with palette color
  `MainBarArmor`, width scales with `curArmor`, hidden at `curArmor == 0`.
- AC-RING-5: Zero `SubsystemBar` / `SubsystemMarker` / `MainBarWidget`
  MonoBehaviours remain in scene tree after Slice 3 (grep gate — ADR-0011
  no-bridges compliance).
- AC-RING-6: Designer can edit ring colors, geometry, and outline treatment
  in Prefab Mode on `SlotTargetRing.prefab` (or its variants) without
  touching C#. Re-authoring a vehicle preserves those edits.
