# Polish Capture: VehicleHudAnchors (Slice 2.6)

**Date:** 2026-06-30 (planned start — DEFERRED until PlayerVehicleStage extract closes)
**System:** Per-vehicle-prefab HUD anchor authoring (retire `SlotDefinition.HudAnchor`)
**Affected paths:**
- `Assets/Scripts/Combat/SlotDefinition.cs` — `HudAnchor` field removed
- `Assets/Scripts/Combat/AnchorPoint.cs` — deleted (no remaining consumers)
- `Assets/Scripts/Combat/Archetypes/SmallFrameLayout.cs` — all `hudAnchor:` ctor args dropped
- `Assets/Scripts/Combat/Archetypes/TinyFrameLayout.cs` — same
- `Assets/Scripts/Combat/Archetypes/HaulerFrameLayout.cs` — same
- `Assets/Scripts/Combat/Archetypes/DredgeFrameLayout.cs` — same
- `Assets/Scripts/CombatView/Data/FrameLayoutSO.cs` — `OnValidate` HudAnchor branch removed
- `Assets/Scripts/CombatView/VehicleHudAnchors.cs` — NEW (MonoBehaviour, ~80 LOC)
- `Assets/Scripts/CombatView/VehicleBarStack.cs` — `SpawnAt:637-653` UV projection rewritten to `anchors.Resolve(slotId).anchoredPosition`
- `Assets/Prefabs/CombatView/PlayerVehicle.prefab` — `VehicleHudAnchors` component + 6 RectTransform children authored
- `Assets/Prefabs/CombatView/EnemyVehicle.prefab` — `VehicleHudAnchors` + 4-6 anchors (per archetype)
- `Assets/Prefabs/CombatView/EnemyVehicle_Hauler.prefab` (or wherever the Hauler variant lives) — 6 anchors
- `Assets/Prefabs/CombatView/EnemyVehicle_Dredge.prefab` — 9 anchors (Dredge has slot_exposable_1/2)
- `Assets/Editor/CombatPrefabAuthor.cs` — author code paths that previously referenced `FrameLayout.HudAnchor` removed

## Proposed change

Replace engine-free UV-space HudAnchors (chassis-UV → world AABB → canvas-local
projection chain in `VehicleBarStack.SpawnAt`) with per-prefab `VehicleHudAnchors`
MonoBehaviour holding `List<{string SlotId, RectTransform Anchor}>`. The view
layer reads `anchoredPosition` directly — no projection. Designer hand-places
bars/markers in Prefab Mode per vehicle. Slot topology (HP, SlotKind, AABB)
stays in FrameLayout where it belongs; visual placement moves to the view layer.

## Final-game picture this serves

The 1.0 enemy roster is per-vehicle-prefab (memory `project_vehicle_variant_chain`
— player + each enemy are independent prefabs, no variant link). Designer asked
in the 2026-06-29 session to hand-place bars/markers per vehicle. UV authoring
in `*FrameLayout.cs` is a numeric proxy that has produced visible drift on
Dredge (memory `project_dredge_uvs_deferred`) and will produce drift on every
new enemy that doesn't share Small chassis proportions. Slice 2.6 closes the
authoring loop: the prefab IS where placement lives, not a remote numeric file.

## Authored values being destroyed

### SmallFrameLayout (PlayerVehicle, 6 slots)

| Slot | Current UV | Replacement plan |
|---|---|---|
| weapon_0 | (0.45, 0.75) | `Anchor_weapon_0` RectTransform child, designer-positioned |
| weapon_1 | (0.55, 0.75) | `Anchor_weapon_1` RectTransform child |
| engine_0 | (0.50, 0.55) | `Anchor_engine_0` RectTransform child |
| mobility_0 | (0.50, 0.35) | `Anchor_mobility_0` RectTransform child |
| hull_0 | (0.50, 0.15) | `Anchor_hull_0` RectTransform child |
| armor_0 | (0.50, 0.25) | `Anchor_armor_0` RectTransform child |

### TinyFrameLayout (4 slots, no armor)

| Slot | Current UV | Replacement plan |
|---|---|---|
| weapon_0 | (0.50, 0.70) | `Anchor_weapon_0` |
| engine_0 | (0.50, 0.50) | `Anchor_engine_0` |
| mobility_0 | (0.50, 0.30) | `Anchor_mobility_0` |
| hull_0 | (0.50, 0.10) | `Anchor_hull_0` |

### HaulerFrameLayout (6 slots)

| Slot | Current UV | Replacement plan |
|---|---|---|
| weapon_0 | (0.45, 0.75) | `Anchor_weapon_0` |
| weapon_1 | (0.55, 0.75) | `Anchor_weapon_1` |
| engine_0 | (0.50, 0.55) | `Anchor_engine_0` |
| mobility_0 | (0.50, 0.35) | `Anchor_mobility_0` |
| hull_0 | (0.50, 0.15) | `Anchor_hull_0` |
| armor_0 | (0.50, 0.25) | `Anchor_armor_0` |

### DredgeFrameLayout (9 slots — armor + 2 exposable)

| Slot | Current UV | Replacement plan |
|---|---|---|
| weapon_0 | (0.45, 0.78) | `Anchor_weapon_0` |
| weapon_1 | (0.55, 0.78) | `Anchor_weapon_1` |
| weapon_2 (Javelin) | (0.38, 0.70) | `Anchor_weapon_2` |
| engine_0 | (0.50, 0.58) | `Anchor_engine_0` |
| mobility_0 | (0.50, 0.40) | `Anchor_mobility_0` |
| hull_0 | (0.50, 0.15) | `Anchor_hull_0` |
| armor_0 | (0.50, 0.28) | `Anchor_armor_0` |
| slot_exposable_1 | (0.45, 0.32) | `Anchor_slot_exposable_1` |
| slot_exposable_2 | (0.55, 0.32) | `Anchor_slot_exposable_2` |

### Projection chain (`VehicleBarStack.SpawnAt:637-653`)

Current: `chassis-art UV → world AABB → canvas-local`. Math is correct but
indirect; every chassis rescale re-projects. Replacement: `anchors.Resolve(
slotId).anchoredPosition`. No projection, no AABB math, no chassis-rescale
dependency.

### FrameLayoutSO `OnValidate` HudAnchor rule

Current: `R_FL` enforces `HudAnchor.IsInUnitRect` (X,Y in [0,1]) + finite.
Replacement: rule deleted with the field. `VehicleHudAnchors.OnValidate`
enforces unique-SlotId entries on the component instead.

## Migration checklist (one-commit cut)

1. **Pre-cut screenshots** — player + Dredge + Hauler + Tiny, full combat
   layout with bars/markers visible. File under `production/polish-captures/
   2026-06-30-vehicle-hud-anchors-pre/`.
2. **Author `VehicleHudAnchors` component** — `List<Entry { string SlotId;
   RectTransform Anchor; }>`, `OnValidate` enforces unique SlotIds, `Resolve(
   string slotId): RectTransform` API. ~80 LOC.
3. **Author RectTransform children on each vehicle prefab** — name pattern
   `Anchor_<slotId>`. Initial positions: project current UVs onto each prefab's
   chassis AABB to seed (pre-cut tool, throwaway editor menu item). Designer
   then nudges in Prefab Mode.
4. **Rewrite `VehicleBarStack.SpawnAt` projection block** — replace UV→world→
   canvas chain with `_hudAnchors.Resolve(slotId).anchoredPosition`. Bind
   `_hudAnchors` in `Bind()` via `chassisRoot.GetComponent<VehicleHudAnchors>()`.
5. **Delete `SlotDefinition.HudAnchor` field + `AnchorPoint` struct** — strip
   all `hudAnchor:` ctor args from the 4 FrameLayout files. Delete
   `FrameLayoutSO.OnValidate` HudAnchor branch.
6. **CombatPrefabAuthor.cs sweep** — remove any author code that read FrameLayout
   HudAnchor (e.g. seeding bar positions from UV).
7. **Post-cut screenshots** — same 4 frames, side-by-side compare.
8. **Verify Dredge UV drift gone** — closes memory `project_dredge_uvs_deferred`.

## Technical Director Review

**Verdict:** APPROVE (Shape D, all-at-once)
**Spawned at:** 2026-06-29 (transcript captured in memory
`project_hud_anchors_slice_26.md`)

**TD reasoning summary:**

- ADR-0011 forbids Shape B/C (parallel Transform + UV storage) and Shape A
  (FrameLayoutSO inspector authoring) is still numeric. Shape D moves visual
  placement to the view layer where it always belonged.
- FrameLayout keeps slot topology (HP, SlotKind, AABB); the view owns
  placement. One axis per layer — no bimodal storage.
- Single canonical authoring contract (no `Find("Marker_" + slotId)` strings):
  `VehicleHudAnchors` component with serialized List and `OnValidate`
  enforcing unique-SlotId entries.
- Defer until stage-extract closes — landing on top of `project_stage_extract_
  in_flight` compounds capture surface. Stage-extract uses one polish-capture;
  slice 2.6 wants its own.
- ~1 focused session; 4 vehicle prefabs (player + 3 enemies).

**Memory pointer:** see `project_hud_anchors_slice_26.md` for the full
TD-verdict block + Shape A/B/C/D comparison.

## User approval

- Reviewed: 2026-06-29
- Approved by: bertanberkol
- Notes: User asked for hand-placement (2026-06-29 session) "if it won't
  threaten the structure of 1.0". TD confirmed Shape D is structurally clean
  + the right time is post stage-extract. Approval is for the slice plan +
  deferral — capture file becomes live when work begins.
