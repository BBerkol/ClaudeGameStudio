# Polish Capture — SlotTargetRing Slice 3 (EnemyNumberBadge)

**Date:** 2026-07-03
**Slice:** SlotTargetRing HUD refactor Slice 3
**System:** `EnemyNumberBadge` widget + enemy per-slot HUD asymmetry
**Author:** Claude (Opus 4.7) with user oversight
**Pre-state reference:** Unity commit `a0b4d6d` (Slice 2b canonical), framework commit `eeda588` (Slice 2b docs)

## What's being edited

**Additive slice.** No destructive edit to authored content. Slice 3:

1. Adds new script `Assets/Scripts/CombatView/EnemyNumberBadge.cs`
2. Adds new prefab `Assets/Prefabs/CombatView/EnemyNumberBadge.prefab`
3. Adds new author menu `Tools > Wasteland Run > Author EnemyNumberBadge Prefab`
4. Adds `VehicleScaffoldSpec.UseEnemyBadges : bool` (companion to existing `SkipPerSlotRings`)
5. Extends `VehicleHudAnchors` with `ResolveBadge(slotId)` mirror of `ResolveRing`
6. Extends `VehicleBarStack` with badge binding path parallel to ring path
7. Adds `CombatController.CurrentDragCastCard` accessor + CombatHud push-on-drag seam
8. Re-authors 3 enemy prefabs (Dune / Iron / Dredge) to nest a badge under each non-armor anchor RT

## Pre-Slice-3 enemy state (from `a0b4d6d`)

### Dredge (`Assets/Prefabs/Enemies/Dredge.prefab`)

- **FrameLayout slots (9):** `weapon_0` (18), `weapon_1` (20), `weapon_2` (14), `engine_0` (22), `mobility_0` (22), `hull_0` (80), `armor_0` (0), `slot_exposable_1` (40), `slot_exposable_2` (40)
- **VehicleHudAnchors entries (7):** `weapon_0`, `weapon_1`, `weapon_2`, `engine_0`, `mobility_0`, `slot_exposable_1`, `slot_exposable_2`. `hull_0` + `armor_0` are absent by design (structural HP + armor pool are MainBar-only reads).
- **MainBar:** present, name label "The Dredge", palette-driven visuals.
- **VehicleBarStack._slotIconRegistry:** stamped in Slice 2b (unused for badges; preserved for future re-add).
- **Per-slot ring mounts:** none (stripped in Slice 2b via `SkipPerSlotRings`).

### IronShepherd (`Assets/Prefabs/Enemies/IronShepherd.prefab`)

- **VehicleHudAnchors:** ~5 anchor entries (weapon(s), engine, mobility, exposables per FrameLayout).
- MainBar + registry stamp: identical to Dredge shape.

### DuneSkimmer (`Assets/Prefabs/Enemies/DuneSkimmer.prefab`)

- **VehicleHudAnchors:** ~3-4 anchor entries.
- MainBar + registry stamp: identical to Dredge shape.

## What Slice 3 preserves

- **All authored VehicleHudAnchors entries** (SlotId + Anchor RT references). Idempotent `EditorUpsert` re-applies.
- **All authored MainBar state** — MainBar is untouched on both player and enemy. No swap. No BuffStripCanvas lift.
- **All authored anchor RT local positions** — SeedHudAnchor preserves existing RT position if the anchor exists.
- **VehicleBarStack._slotIconRegistry stamp** from Slice 2b.
- **Player prefab (PlayerVehicle.prefab)** — untouched by Slice 3. Player rings continue to render per Slice 2b.

## What Slice 3 changes destructively

**Only the 3 enemy prefabs**, and only additively:

- Nests one `EnemyNumberBadge.prefab` instance under each non-armor `VehicleHudAnchors.Entry.Anchor` RT.
- If a designer had manually placed a stub widget under an anchor RT prior to Slice 3, that stub would be replaced by the badge. **No such stubs exist** as of `a0b4d6d` (Slice 2b stripped rings and shipped anchors as empty containers).

## Rollback path

`git revert <slice-3-commit>` fully rolls back:

- New files deleted (EnemyNumberBadge.cs, .prefab, .asset).
- 3 enemy prefabs revert to `a0b4d6d` state (empty anchors, no badges).
- `CombatController.CurrentDragCastCard` accessor + CombatHud push seam remove cleanly.
- `VehicleScaffoldSpec.UseEnemyBadges` flag removes.

No manual cleanup required.

## Technical Director Review

TD verdict from this session (transcript summary):

**Q1 (Additive vs Replace):** *Superseded by user clarification 2026-07-03.* Original TD verdict was for a MainBar swap on enemies; user clarified Slice 3 is a per-slot widget swap, not vehicle-headline swap. MainBar remains on both sides. TD's replace-MainBar verdict is **not applied**. Corresponding BuffStripCanvas lift is **not required** (MainBar stays, BuffStripCanvas stays nested).

**Q2 (Anchor topology):** Confirmed — parallel nullable serialized fields on `VehicleHudAnchors`. Existing per-slot `Entry[]` catalog carries anchor RTs; badges nest under those RTs via a mirror of the ring-seed path. New `ResolveBadge(slotId)` accessor added alongside `ResolveRing(slotId)`. No new abstraction; ADR-0011-clean (SkipPerSlotRings pattern parallel).

**Q3 (Author flag naming):** Confirmed — single `UseEnemyBadges : bool` flag on `VehicleScaffoldSpec` per TD's naming recommendation. Coupled decision, one axis. Enemy scaffolds set true.

**Traps addressed:**
- `ICombatHoverTarget` — implemented on `EnemyNumberBadge`; circular backdrop image is the hover raycast surface (same seam as `MainBarWidget` and `SlotTargetRing`).
- `ADR-0014` — world-space widget on vehicle. Same precedent as MainBar. Not a new exception.
- **Editor visibility (feedback_edit_prefab_visibility)** — copy IntentWidget's `[SerializeField]` placeholder sprite pattern; OnValidate with `!gameObject.scene.IsValid()` guard (feedback_executealways_asset_guard).
- **GUID churn (project_vehicle_author_guid_churn)** — enemy re-author will refresh binder GUIDs on `Run.prefab._combatBeaconArchetypes`. Slice 3 commit includes the rebaked Run.prefab.
- **Capture-before-destroy** — this document.

**New in Slice 3 (post-clarification):**
- **Hover damage preview.** Badge polls `CombatController.CurrentDragCastCard` each frame while `SetTargetHover(true)`; computes `DamagePipeline.PreviewDamage(vehicle, baseDamage, slotId)`; renders `max(0, currentHp - projectedDamage)` in the number text. On hover exit or drag release, badge renders live `slot.CurrentHp`. Poll pattern matches `IntentWidget.Update` polling `Loop.CurrentEnemyIntent`.
- **Broken-state visuals.** When `slot.CurrentHp <= 0`: number TMP is disabled (hidden), backdrop `Image.sprite` swaps to `_backdropBrokenSprite`. Widget stays visible. On repair (HP > 0), reverse: number re-enables, backdrop restores `_backdropIntactSprite`. Reads at widget level in addition to the existing `VehiclePartTint` part-body red tint.
- **`CombatController.CurrentDragCastCard` accessor.** New public getter, set internally by `CombatHud` on `StartTargeting` / `EndTargeting`. Zero-cost when no drag is active.

## Test surface

- `Assets/Tests/EditMode/CombatView/EnemyNumberBadgeTests.cs` — new file.
- Coverage:
  1. `Bind_RendersCurrentHp` — Bind(vehicle+slotId+controller) then Update → number text = `slot.CurrentHp.ToString()`.
  2. `HoverWithAttackCard_RendersProjectedHp` — SetTargetHover(true) while `controller.CurrentDragCastCard` = attack card with baseDamage → number text = `max(0, currentHp - projectedDamage)`.
  3. `HoverExit_RestoresCurrentHp` — SetTargetHover(false) → number text = `slot.CurrentHp.ToString()`.
  4. `SlotHpZero_HidesNumberSwapsBackdrop` — slot.CurrentHp = 0 → number.enabled = false, backdrop.sprite = broken variant.
  5. `RepairFromZero_RestoresNumberAndBackdrop` — HP goes from 0 → positive → number re-enabled, backdrop.sprite = intact variant.
  6. `HoverWithAttackCard_TintsNumberYellow_HoverExitRestoresWhite` — **Delta A** — SetTargetHover(true) with attack card in `CurrentDragCastCard` → number `.color` = `(0.95, 0.85, 0.30, 1)` (matches `CombatBarPalette.FallbackYellow`). SetTargetHover(false) → number `.color` restores to `Color.white`.

## Delta A — post-PlayMode-smoke amendment (2026-07-03)

**User observation from Slice 3 PlayMode smoke:** *"hovered number projects damaged but remains white, should become yellow."*

**Scope expansion:** while the badge is drag-cast-hovered with an attack card (`_targetHover && controller.CurrentDragCastCard.PrimaryDamage > 0`), the TMP `.color` tints to `CombatBarPalette.FallbackYellow` `(0.95, 0.85, 0.30, 1)` — same yellow used by the ring's damage band, the intent-chip attack tint, and the drag-cast halo. On hover exit or drag release, the number restores to `Color.white`.

**Implementation:**
- `EnemyNumberBadge.cs` — added `NumberLiveColor`/`NumberPreviewColor` constants + `_lastRenderedPreviewing` memoization field. `Update()` writes the color only when the previewing state transitions, so idle frames are still zero-cost.
- `Bind()` resets `_lastRenderedPreviewing = false` alongside the number memo.
- **No new interface, no new palette dependency** — the color literal matches `CombatBarPalette.FallbackYellow` by value (documented in the source comment) rather than pulling the SO reference through the widget. Kept the widget self-contained.

**Delta A test evidence:** `HoverWithAttackCard_TintsNumberYellow_HoverExitRestoresWhite` — green.

## Acceptance criteria

- All 6 EditMode tests pass.
- Full EditMode suite stays at 693/707 (13 pre-existing RunSceneHost/SaveBootstrap failures — not ring-related; 1 pre-existing skip).
- PlayMode smoke on each enemy:
  - Badge renders live HP on each non-armor slot.
  - Drag attack card over badge → number renders projected post-damage HP.
  - Drag ends off badge → number restores.
  - Play card enough to break a slot → badge stays visible, number hides, backdrop swaps to broken sprite.
  - Play Repair on that slot → badge shows number again, backdrop restores.
- Player HUD unchanged — SlotTargetRing rings still render with icons + always-visible per Slice 2b.
- MainBar unchanged on both sides.
