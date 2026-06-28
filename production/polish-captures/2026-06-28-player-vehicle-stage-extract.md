---
date: 2026-06-28
system: PlayerVehicleStage prefab extract (player-vehicle composite)
type: refactor / authoring + view-layer
related_slices: Rest Area Pass 1, Slice 9b RestScopeToggler
status: pending-approval
---

# PlayerVehicleStage Extract — Capture Before Destroy

## Why this refactor

Rest Area Pass 1 first playtest surfaced that `RestRoot.prefab` carries no
`VehicleBarStack` canvas (deliberate per `CombatPrefabAuthor.cs:7741-7744` —
"designer adds via Prefab Mode after eye-checking the cut") and `RunScene` has
no `EventSystem` (UGUI pointer events on `VehiclePartHitZone` can't fire).

Smallest correct fix was to extend `AuthorRestRootPrefab` with a direct
`AuthorBarStackCanvas` call + add `EnsureEventSystem` to `RunSceneHost`. User
retracted that path in favor of factoring out the shared substrate that
Combat + Rest + future beacon roots (Haven/Merchant/Event/Chopshop/Elite) all
compose from. Per `feedback_demo_forward_over_infrastructure` — build the 1.0
shape, not a stopgap. Per `feedback_overall_picture_thinking` — hold the
final-game picture in mind for system refactors.

## What changes

- New prefab `Assets/Prefabs/CombatView/PlayerVehicleStage.prefab` — a
  positionally-neutral composite:

  ```
  PlayerVehicleStage           (plain Transform root, zero local pos)
  ├─ PlayerVehicle             (prefab instance, zero local pos)
  └─ PlayerBarStackCanvas      (RectTransform child, WorldSpace canvas,
                                scale 0.01, scale-anchor centered, sortingOrder 4,
                                GraphicRaycaster, nested VehicleBarStack with _side=Player)
  ```

- New `Editor` menu `Tools → Wasteland Run → Author PlayerVehicleStage Prefab`
  + helper `BuildPlayerVehicleStageRoot` (refactor of current
  `AuthorBarStackCanvas` reused inside it).

- `Editor/CombatPrefabAuthor.cs` lines 6892-6893 (AuthorCombat) — replace
  the two `AuthorBarStackCanvas` calls with stage-instance + per-side
  positioning. Enemy side keeps direct `AuthorBarStackCanvas` (enemy archetype
  prefab variation; outside this refactor's scope — re-evaluate at Elite).

- `Editor/CombatPrefabAuthor.cs` lines 7741-7800 (AuthorRestRootPrefab) —
  replace direct `PlayerVehicle` prefab instance with `PlayerVehicleStage`
  instance, parented at zero local pos under RestRoot. Delete the
  "designer adds VBS in Prefab Mode" comment.

- New `Scripts/CombatView/PlayerVehicleStage.cs` — tiny MonoBehaviour on the
  stage root. `Awake` / `OnEnable` registers itself with `RunSceneHost` so the
  host assigns `Canvas.worldCamera` on the bar canvas (pull model per TD
  Q2 verdict — no race with additive Combat scene load).

- `Scripts/CombatView/RunSceneHost.cs` — add `EnsureEventSystem()` (mirrors
  `CombatHud.EnsureEventSystem` exactly: `ENABLE_INPUT_SYSTEM` ifdef branch,
  `InputSystemUIInputModule` on Awake/Initialize); add
  `RegisterStage(PlayerVehicleStage)` / `UnregisterStage` so each stage
  registers itself and the host wires `Canvas.worldCamera = Camera.main`.

- `Scripts/CombatView/CombatHud.cs` `EnsureWorldCameras()` — leave in place
  as defense-in-depth (idempotent — `FindAnyObjectByType<EventSystem>()`
  guard already there). Per TD: Combat scene may load standalone in EditMode
  tests, don't delete.

## Pre-authored values being destroyed / re-baked

### Combat.prefab — `EnemyBarStackCanvas` (lines 3-80)

| Property | Value |
|---|---|
| `m_LocalPosition` | `(0, 0, 0)` |
| `m_LocalScale` | `(0.01, 0.01, 0.01)` |
| `m_AnchorMin` / `m_AnchorMax` / `m_Pivot` | `(0.5, 0.5)` |
| `m_AnchoredPosition` | `(4.5, 0)` ← designer-tuned enemy lane offset |
| `m_SizeDelta` | `(700, 500)` |
| `m_Father` | LaneAxis (`4964221122556935786`) |
| `m_RenderMode` | `2` (WorldSpace) |
| `m_Camera` | `{fileID: 0}` (null — wired at runtime) |
| `m_SortingOrder` | `4` |
| `m_ReceivesEvents` | `1` |
| `m_PlaneDistance` | `100` |
| GraphicRaycaster `m_IgnoreReversedGraphics` | `1` |
| GraphicRaycaster `m_BlockingObjects` | `0` |
| GraphicRaycaster `m_BlockingMask` | all-bits (`4294967295`) |
| Nested VBS `_side` | `Enemy` (default in source prefab) |

### Combat.prefab — `PlayerBarStackCanvas` (lines 81-158)

| Property | Value |
|---|---|
| `m_LocalPosition` | `(0, 0, 0)` |
| `m_LocalScale` | `(0.01, 0.01, 0.01)` |
| `m_AnchorMin` / `m_AnchorMax` / `m_Pivot` | `(0.5, 0.5)` |
| `m_AnchoredPosition` | `(-4.3, 0)` ← designer-tuned player lane offset (asymmetric vs enemy +4.5) |
| `m_SizeDelta` | `(700, 500)` |
| `m_Father` | LaneAxis (`4964221122556935786`) |
| `m_RenderMode` | `2` (WorldSpace) |
| `m_Camera` | `{fileID: 0}` (null — wired at runtime) |
| `m_SortingOrder` | `4` |
| `m_ReceivesEvents` | `1` |
| `m_PlaneDistance` | `100` |
| GraphicRaycaster: same as enemy |
| Nested VBS `_side` override | `Player` (value `0` per author script line 7120) |

### Combat.prefab — LaneAxis transform (line 342-361)

| Property | Value |
|---|---|
| `m_LocalPosition` | `(0, -0.1, 0)` |
| `m_LocalScale` | `(1, 1, 1)` |
| `m_Children` | `[ChaseRail, PlayerVehicle, EnemyVehicle, EnemyBarStackCanvas, PlayerBarStackCanvas]` |

### Combat.prefab — vehicle prefab instance positions under LaneAxis

| Vehicle | `m_LocalPosition.x` override |
|---|---|
| EnemyVehicle | `4.5` (line 610) |
| PlayerVehicle | `-4.5` (line 706) |

The bar canvas anchoredPosition asymmetry (+4.5 enemy vs -4.3 player) is
designer-baked; the stage refactor must preserve this — the stage prefab
itself is positionally neutral (zero), and the AuthorCombat refactor assigns
each side's `m_AnchoredPosition` on the stage's nested bar canvas as an
instance override, matching the current values exactly.

### CombatHud serialize references that survive the refactor

| Field | Bound to |
|---|---|
| `_enemyBarStack` | nested VehicleBarStack inside EnemyBarStackCanvas (fileID 3677240562982658606) |
| `_playerBarStack` | nested VehicleBarStack inside PlayerBarStackCanvas (fileID 5397409874625285315) |
| `_enemyBuffStrip` | nested BuffStripWidget in enemy MainBar (fileID 1928316779453795226) |
| `_playerBuffStrip` | nested BuffStripWidget in player MainBar (fileID 7146540278344590711) |
| `_enemyVehicleVisual` | EnemyVehicle prefab instance (fileID 5777455456861578236) |
| `_playerVehicleVisual` | PlayerVehicle prefab instance (fileID 4529230212807304569) |

These are wired during `AuthorCombat`. Re-author after the stage refactor
must re-bind to the new fileIDs inside the stage instance — the existing
`AuthorCombat` SerializedObject writes already cover this pattern.

### RestRoot.prefab — current state (clean — only structural cut, no designer tunes)

| Property | Value |
|---|---|
| RestRoot transform `m_LocalPosition` | `(0, 0, 0)` |
| RestRoot children | `[PlayerVehicle, RestPicker]` |
| PlayerVehicle prefab instance `m_LocalPosition` | `(0, 0, 0)` |
| RestPicker UIDocument `m_PanelSettings` | `41f96296f400e0245a37aa4e9f1de5f8` (Assets/UI/PanelSettings.asset) |
| RestPicker UIDocument `sourceAsset` | `00f3a1a1dc4746749b620436326df55d` (RestPicker.uxml) |
| RestPickerController `_document` | wired (fileID 8881048680306532287) |
| RestPickerController `_weldingCursor` | **unassigned** (on-disk capture predates the welding cursor extension to AuthorRestRootPrefab — re-author will wire) |
| RestPickerController `_weldingCursorHotspot` | `(0, 0)` (default — re-author will set to `(4, 4)`) |
| RestPickerController `_weldingSparks` | **unassigned** (re-author will create + wire) |
| WeldingSparks GameObject | **not present** (re-author will add) |

After the stage refactor, RestRoot children become `[PlayerVehicleStage, RestPicker, WeldingSparks]`
— PlayerVehicle gone, replaced by stage instance.

## Technical Director Review

### Q1 — Stage transform: positionally neutral (Option A)

> Principle: parents own placement; the stage prefab makes no claim about
> scene position. The stage prefab represents a *composite* (vehicle +
> bars-as-sibling), not a *placement*. Combat's lane-offset is a Combat-scene
> concern; Rest's centered-at-origin is a RestRoot concern. Future
> Haven/Merchant/Chopshop will each have their own placement story.
> Encoding any one of those into the prefab transform forces every other
> consumer to override, which collides head-on with
> `feedback_recttransform_override_pinning`.
>
> Concern (must address): the dual-write (`localPosition` + `anchoredPosition`)
> pattern at AuthorCombat 7071-7078 exists because LaneAxis is a plain
> Transform. Recommend the stage root be a plain `Transform`, with the bar
> canvas RectTransform nested one level inside. That sidesteps the pin trap
> and matches how Combat already structures it.

### Q2 — Bar canvas mode: keep WorldSpace, hoist camera assignment to RunSceneHost (pull model)

> Principle: scene-global concerns live on the persistent host; rendering
> mode stays consistent across all beacon roots.
>
> Switching to ScreenSpaceCamera breaks HudAnchor's world-space projection
> — those anchors are tied to vehicle sprite world positions and the
> FrameLayoutSO migration (`project_dredge_uvs_deferred`) is mid-flight.
> Hard reject.
>
> Null-fallback (option 3) is myth in Unity 6.3 — WorldSpace canvases with
> null `worldCamera` do NOT fall back to Camera.main; they render via the
> first enabled camera that finds them in its culling mask. Do not rely.
>
> Concern (must address): the "Combat additive over RunScene racing the
> host" scenario is real. Mitigation: RunSceneHost wires `worldCamera` on
> a `Canvas` lookup driven by a registration call from the stage's
> Awake/OnEnable — pull model, not push. Stage registers itself; host
> assigns the camera. No race.
>
> Chopshop/Elite/Forge all keep working because they all live under
> RunSceneHost and share the camera-wire path.

Full TD verdict: in chat history 2026-06-28 (agentId a11a05dc085993454).

## Risks

- **Re-authoring Combat blows away CombatHud serialize wiring** — AuthorCombat
  already re-wires every CombatHud field via SerializedObject on each run;
  the refactor must carry the same re-wire to the new stage-nested fileIDs.
  Test: run AuthorCombat, open Combat.prefab, verify all 6 CombatHud refs
  bind to non-null targets.

- **VehicleBarStack `_side` field** — Player side currently set via
  SerializedObject write (line 7115-7122). Stage prefab bakes `_side=Player`
  as an instance override on the nested VBS. New AuthorPlayerVehicleStage
  menu writes it once at stage-prefab build time. Enemy side stays in
  AuthorCombat (direct AuthorBarStackCanvas call).

- **WorldSpace canvas null worldCamera during the gap between Awake and
  host-wire** — first frame render may pick wrong camera. Mitigation: stage
  registers itself in Awake (before first render), host wires synchronously.
  EditMode tests will need a stand-in.

- **Re-author user step** — after merging, user must run:
  1. `Tools → Wasteland Run → Author PlayerVehicleStage Prefab` (new menu)
  2. `Tools → Wasteland Run → Author Combat Prefab`
  3. `Tools → Wasteland Run → Scenes → Author Rest Root Prefab`
  Combat regressions catch immediately; Rest gets working bars + hit zones.

- **Enemy-side bar canvas split** — Combat keeps `EnemyBarStackCanvas` as a
  direct AuthorBarStackCanvas authored entity (enemy archetype variation
  is outside stage scope). Refactor leaves enemy authoring alone. Re-visit
  when Elite beacons re-introduce an enemy in a non-LaneAxis context.

## Final-game picture this serves

Every non-Combat beacon (Rest, Haven, Merchant, Event, Chopshop, Forge,
Upgrade pass 2/3, Cargo, Refuel) will instance `PlayerVehicleStage.prefab`
under its prefab root and present player bars + hit zones consistently.
Elite Combat is the only beacon that re-introduces an enemy + LaneAxis. The
stage prefab is the single source of truth for player presentation across
all 9+ beacon types; designer changes to bar appearance / vehicle composition
propagate everywhere by editing one prefab.

## Approval gate

Approve this capture + the refactor plan before any code change lands.
Awaiting user sign-off.
