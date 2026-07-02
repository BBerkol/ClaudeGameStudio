# Combat.prefab Drift Audit — 2026-07-02

## Sentinel Context

`production/session-state/prefab-drift-pending.json` flagged `Combat` at
`2026-07-02T14:37:40+03:00`. Last actual Combat.prefab git commit was
`78696fa` (2026-06-30 07:30) — "Slice 2.6 Phase 1c VehicleBarStack collapse
+ prefab drift bake." Git working tree shows `Combat.prefab` clean since
that commit.

The flag was almost certainly triggered by prompt keyword matching in the
`pre-author-bake-required.sh` hook (edit-verb pattern + "combat" token),
not by actual disk changes. Nevertheless, per project protocol
`feedback_pre_author_capture_protocol`, a full prefab-vs-author diff was
performed before clearing.

## Files audited

- `Assets/Prefabs/CombatView/Combat.prefab` (478 lines)
- `Assets/Editor/CombatPrefabAuthor.cs`, `AuthorCombat()` @ lines 7445-7696

## Diff Result

| Path | Author (source) | Prefab (disk) | Action |
|------|-----------------|---------------|--------|
| `Combat/LaneAxis` `m_LocalPosition.y` | `defaultLaneAxisY = 0.5f` (line 7504) | `-0.1` | **BAKE**: update const to `-0.1f` |
| `Combat/LaneAxis/ChaseRail` SR `m_Color.a` | `0f` (line 7495) | `1` | No-bake: `m_Enabled=0` (line 7575) hides SR regardless of alpha; author comment already anticipates designer alpha tweaks (7573-7574) |
| `Combat/LaneAxis` children order | `[ChaseRail, PlayerVehicle, EnemyVehicle]` (7568/7597/7604) | Same | Match |
| `Combat` root children order | `[SceneVisuals, LaneAxis, HUD]` (7528/7563/7622) | Same | Match |
| `Combat/LaneAxis/ChaseRail` scale | `(9, 0.08, 1)` from `laneSeparation`, `railThickness` | `(9, 0.08, 1)` | Match |
| `Combat/LaneAxis/PlayerVehicle` position | `(-4.5, -0.5, 0)` from `-laneSep*0.5`, `VehicleAuthoredY` | `(-4.5, -0.5, 0)` | Match |
| `Combat/LaneAxis/EnemyVehicle` position | `(+4.5, -0.5, 0)` | `(+4.5, -0.5, 0)` | Match |
| Vehicle mount scales | `0.8` uniform from `VehicleAuthoredScale` | `(0.8, 0.8, 0.8)` both | Match |
| `VehiclePositionAnimator` `_tweenDurationSec` | C# default `0.50f` | `0.5` both | Match |
| `VehiclePositionAnimator` `_overtakeDipUnits` | C# default `0.12f` | `0.12` both | Match |
| `CombatController._maxLogLines` | C# default `14` | `14` | Match |
| `CombatSceneBlockout._laneSeparation` | `9f` via SO (line 7652) | `9` | Match |
| `CombatSceneBlockout._railThickness` | `0.08f` | `0.08` | Match |
| `CombatController._balanceAsset` | SO-wired to `CombatBalance_Default.asset` (line 7668) | guid `a3e99cae9187abb448adf23819d5712c` | Match |
| `Combat/HUD` name override | `hudInstance.name = "HUD"` (7623) | `m_Name: HUD` override | Match |
| `Combat/SceneVisuals` name override | `sceneVisuals.name = "SceneVisuals"` (7529) | `m_Name: SceneVisuals` override | Match |

## Why the drift is small

`CombatPreservePaths` (line 7917) captures & restores `LaneAxis`,
`LaneAxis/ChaseRail`, and vehicle mounts across re-authors. So even if the
author const is stale, `CapturePreservedNodes` reads the disk value before
rebuild and `ApplyPreservedNodes` re-stamps it afterwards. The
`defaultLaneAxisY = 0.5f` const is effectively a first-run seed only; the
current -0.1 value is preserved indefinitely.

However, per project rule `feedback_bake_designer_edits`, the author
source **should** reflect current designer values as the primary source of
truth. The `-0.1f` bake is a small, safe cleanup.

## Bake Proposed

**File**: `Assets/Editor/CombatPrefabAuthor.cs`
**Line 7504**: `const float defaultLaneAxisY = 0.5f;` → `const float defaultLaneAxisY = -0.1f;`

Add a comment explaining the value origin (designer-tuned via
`CombatPreservePaths`, baked here as source of truth on 2026-07-02).

## Sentinel Clearance

After bake approved + applied, run `rm production/session-state/prefab-drift-pending.json`
to clear the flag. Combat is the only vehicle in `pending`, so the whole
file goes.

---

## Follow-up: Dead-code sweep (2026-07-02, later same day)

While auditing the Combat prefab against `CombatPrefabAuthor.cs` for drift,
also identified dead code left behind by Slice 2.6 Phase 1c (commit
`78696fa`, 2026-06-30). Phase 1c retired the LaneAxis-parented bar canvases
(`PlayerBarStackCanvas` + `EnemyBarStackCanvas`) and moved bars onto each
vehicle prefab's `VehicleHudAnchors` container. The standalone
`VehicleBarStack.prefab` asset was also deleted.

### Dead items to delete from `CombatPrefabAuthor.cs`

| Item | Lines | Why dead |
|------|-------|----------|
| `private enum BarStackSide { Player, Enemy }` | 7705 | Only referenced inside the dead method below |
| `AuthorBarStackCanvas(...)` method (~98 lines) | 7719-7797 | Zero callers; `VehicleBarStack.prefab` input no longer exists |
| `CombatPreservePaths["LaneAxis/EnemyBarStackCanvas"]` | 7923 | Path no longer present in Combat.prefab tree |
| `CombatPreservePaths["LaneAxis/PlayerBarStackCanvas"]` | 7924 | Path no longer present in Combat.prefab tree |
| Log message ends `+ bar stacks + nested CombatHud` | 7697 | Stale — no bar stacks under LaneAxis after Phase 1c |

`VehicleBarStack` (the C# MonoBehaviour type) stays — still mounted on
vehicle prefabs via `VehicleHudAnchors`. Only the standalone canvas prefab
+ its authoring helper are dead.

## Technical Director Review

Dead `AuthorBarStackCanvas` path and its `BarStackSide` enum reference
`VehicleBarStack.prefab`, which was deleted in 78696fa when bar canvases
moved onto per-vehicle `VehicleHudAnchors`. Retaining the ~100-line
method plus stale `CombatPreservePaths` entries for
`LaneAxis/{Player,Enemy}BarStackCanvas` violates ADR-0011 (no bridges /
vestigial code at done state) and would mislead future readers into
thinking the bar architecture is bimodal. Grep confirms zero callers
outside the file and the `VehicleBarStack` component type itself stays
live (mounted via VehicleHudAnchors), so the delete is scoped to the
retired canvas-authoring path only.

**Success metric**: post-delete, `Author Combat Prefab` still produces a
Combat.prefab that matches the 78696fa layout (no bar canvases under
LaneAxis, bars sourced from vehicle prefabs). Run it once and confirm no
new LogErrors or missing-child warnings.

**Verdict**: APPROVE. Proceed with items 1-4 as described.

