# Polish Capture: Runtime Enemy Visual Swap (Slice 2.6 follow-up — Option A)

**Date:** 2026-06-30
**System:** Combat.prefab vehicle nesting → runtime archetype-driven instantiation
**Trigger:** Slice 2.6 (VehicleHudAnchors) exposed that Combat.prefab nests
`IronShepherd.prefab` as the canonical `EnemyVehicle` placeholder, while the
runtime model loads the real archetype per beacon. Pre-2.6 this worked because
HUD anchor positions lived on the *FrameLayout* (per-archetype data). Post-2.6
anchors live on the `VehicleVisual` (per-prefab data), so the IronShepherd
catalog is consulted for every combat regardless of which enemy actually fights
— Dredge's `weapon_2` / `slot_exposable_1` / `slot_exposable_2` log misses.

**Affected paths:**
- `Assets/Editor/CombatPrefabAuthor.cs` — `AuthorCombat` enemy + player nested-prefab block (lines ~6881–6953) → empty Transform mounts with `VehiclePositionAnimator` on each
- `Assets/Scripts/CombatView/CombatHud.cs` — `_enemyVehicleVisual` / `_playerVehicleVisual` become runtime-assigned (non-serialized), `BuildEnemyBarStack` / `BuildPlayerBarStack` no longer pre-bind from Awake (rebind happens per beacon)
- `Assets/Scripts/CombatView/CombatController.cs` — `HandleCombatReady` gains an `InstantiateVehicleVisualsForBeacon()` step BEFORE the `OnCombatRebuilt` event so the bar stacks see the fresh visuals
- `Assets/Scripts/CombatView/VehicleBarStack.cs` — `_hudAnchorsWarnLogged` path upgrades from `LogWarning` to `LogError` (after swap, a missing anchor is a hard authoring bug, not a placeholder mismatch)
- `Assets/Scripts/CombatView/CombatSceneBlockout.cs` — no source change; serialized refs still point at the empty mounts (which carry the animator components)
- `Assets/Prefabs/CombatView/Combat.prefab` — regenerated: `EnemyVehicle` + `PlayerVehicle` become empty Transform mounts (no nested archetype prefab); the override block targeting nested `VehicleVisual` / `VehicleHudAnchors` is dropped

## Proposed change

**Cut the bridge.** Combat.prefab nesting one specific archetype is itself a
placeholder-as-canonical pattern (ADR-0011 sniff). Replace with:

1. `AuthorCombat` creates empty `EnemyVehicle` and `PlayerVehicle` GameObject
   *mounts* under `LaneAxis` at the same `(±laneSep/2, -0.5, 0)` localPosition
   and `0.8` scale they previously inherited. Each mount carries:
   - `VehiclePositionAnimator` (was on the nested archetype prefab root — moves to the mount so `CombatSceneBlockout._enemyAnimator` keeps a stable ref across beacon swaps).
   - No `VehicleVisual`, no `VehicleHudAnchors` — those live on the spawned child.
2. `CombatController.HandleCombatReady` gains an `InstantiateVehicleVisualsForBeacon(beacon)` step that runs **before** `OnCombatRebuilt`:
   - Look up the beacon's `EnemyArchetypeId` → `RunSceneHost.ResolveBinder(id).gameObject`.
   - Destroy any prior spawned child under the enemy mount.
   - Instantiate the archetype prefab as a child at `localPosition=0, localScale=1`.
   - Strip / disable the redundant `VehiclePositionAnimator` on the spawned child (mount has the canonical one).
   - Hand the spawned `VehicleVisual` to `CombatHud` via a new public setter (`SetVehicleVisualsForCombat(playerVisual, enemyVisual)`).
   - Same for player visual — first combat instantiates `PlayerVehicle.prefab` under the player mount; subsequent combats reuse it (player archetype is stable inside a run; ADR-0012 part-swap path will re-instantiate later).
3. `CombatHud`:
   - `_enemyVehicleVisual` / `_playerVehicleVisual` drop the `[SerializeField]` attribute (runtime-assigned only).
   - `BuildEnemyBarStack` / `BuildPlayerBarStack` no longer bind from Awake — they just cache the bar-stack reference and register followers.
   - `HandleCombatRebuilt` becomes the single binding seam: calls `BindForCombat` (full bind, not just `RebuildForCurrentVehicle`) so the bar stacks pick up the freshly-spawned visual's `VehicleHudAnchors` catalog.
   - `EnsureWorldCameras` (line 521-522) called from `HandleCombatRebuilt` too — the hit-zones canvas now lives on the spawned child.
4. `VehicleBarStack._hudAnchorsWarnLogged` path → `LogError`. With swap landed, missing anchors mean an unauthored prefab (designer bug), not a placeholder mismatch (system bug). Hard-error makes it un-shippable.

## Final-game picture this serves

ADR-0012 says player part install/uninstall implies *visual* recomposition.
Today the visual is a single nested instance in Combat.prefab — to recompose, we
would have to author a swap path anyway. Building the swap NOW on the enemy side
(forced by Slice 2.6) gives us the same seam for free on the player side. After
this slice, every visual on the battlefield is a runtime instantiation under a
stable mount — when ADR-0012 part install ships, it instantiates a re-built
visual against the same mount and the bar stack rebinds without ceremony.

It also kills the "Combat.prefab is the IronShepherd combat" bridge that Slice
2.6 inadvertently exposed.

## Authored values being destroyed

### Combat.prefab — nested EnemyVehicle (IronShepherd instance)

| What | Current value | Replacement plan |
|---|---|---|
| Nested prefab GUID | `348efc5257369ab4787a95fe4a33ad3b` (IronShepherd.prefab) | Empty `GameObject "EnemyVehicle"` (no prefab nesting) |
| `localPosition` | `(+4.5, -0.5, 0)` (laneSep/2) | Same — written by AuthorCombat onto the empty mount |
| `localScale` | `(0.8, 0.8, 0.8)` | Same |
| `VehiclePositionAnimator` | On nested prefab root | Added to mount root in AuthorCombat |
| `VehicleVisual` + `VehicleHudAnchors` | Inherited from nested prefab | Live on runtime-spawned child (per-archetype) |

### Combat.prefab — nested PlayerVehicle

| What | Current value | Replacement plan |
|---|---|---|
| Nested prefab GUID | `0616e48eXXXXXXX...` (PlayerVehicle.prefab) | Empty `GameObject "PlayerVehicle"` |
| `localPosition` | `(-4.5, -0.5, 0)` | Same — empty mount |
| `localScale` | `(0.8, 0.8, 0.8)` | Same |
| `VehiclePositionAnimator` | On nested prefab root | Added to mount root |

### CombatHud serialized refs (in Combat.prefab override block)

| Field | Current binding | Replacement plan |
|---|---|---|
| `_enemyVehicleVisual` | fileID of nested IronShepherd's `VehicleVisual` | Runtime-assigned (drop `[SerializeField]`); set per beacon by `CombatController.InstantiateVehicleVisualsForBeacon` |
| `_playerVehicleVisual` | fileID of nested PlayerVehicle's `VehicleVisual` | Runtime-assigned (drop `[SerializeField]`); set once on first combat |

### CombatSceneBlockout serialized refs

| Field | Current binding | Replacement plan |
|---|---|---|
| `_enemyVehicle` (Transform) | nested IronShepherd root | Empty mount transform (no change in field shape) |
| `_playerVehicle` (Transform) | nested PlayerVehicle root | Empty mount transform |
| `_enemyAnimator` (VehiclePositionAnimator) | component on nested IronShepherd | Component on empty mount root (re-wired by AuthorCombat) |
| `_playerAnimator` | component on nested PlayerVehicle | Component on empty mount root |

**No designer values lost.** All inspector-tuned numbers (lane separation,
authored Y, scale, anchor placements) survive — they live either on
`CombatSceneBlockout` SerializeFields (untouched) or on the per-archetype source
prefabs (DuneSkimmer / IronShepherd / Dredge `.prefab` — also untouched).

## Risk surface

- **Bar stack bind ordering.** Pre-2.6 `BuildEnemyBarStack` could bind in Awake because the visual was known. Post-swap, Awake runs before any combat starts; the first `HandleCombatRebuilt` is the earliest bind point. Need to verify CombatHud `EnsureWorldCameras` migration doesn't strand the per-frame hit-zone raycaster on a stale `worldCamera=null` for first combat.
- **Camera resolution timing.** `Camera.main` may be null inside `HandleCombatReady` if the combat overlay activates the camera root just-in-time. Mitigation: call `EnsureWorldCameras` at the end of `HandleCombatRebuilt`, after the Camera should be live.
- **VehiclePositionAnimator authoring.** Moving the animator from per-archetype prefab roots to the persistent mount means the existing animator components on `PlayerVehicle.prefab` / `DuneSkimmer.prefab` / `IronShepherd.prefab` / `Dredge.prefab` become dead weight when those prefabs are instantiated as children. Either strip on instantiation (clean) or leave disabled (lazy). Cleaner: strip.
- **`CombatSceneBlockout.Awake` defensive walk** (lines 169-179) finds `_playerAnimator`/`_enemyAnimator` by walking LaneAxis for named children — this keeps working since the mounts are named `PlayerVehicle`/`EnemyVehicle` and carry the animator.
- **EditMode tests** that build `CombatLoop` without a scene continue to work — the swap path is gated by `RunSceneHost` presence; headless tests bypass it.

## Out of scope

- Player visual recomposition on part install/uninstall (ADR-0012 work — separate slice, but this slice provides the mount seam).
- VehiclePartHitZone canvas re-parenting (still per-archetype prefab; just gets re-instantiated with the swap).
- Per-archetype tuning of mount Y/scale (single shared offset stays canonical).
- Anchor authoring on the three archetype prefabs (already shipped in Slice 2.6).

## Technical Director Review

**TD-ARCHITECTURE: CONCERNS** (verdict captured 2026-06-30, pre-implementation):

> **Does this self-resolve when visual assets land?** No. Visual-assets work is
> asset-shaped (PSB swap, sprite refinement) — not architecture-shaped. The
> mismatch is structural: the visual layer was never per-archetype. Anchor
> miss-logging will continue to fire for every Dredge encounter regardless of
> art quality.
>
> **Is Option A (runtime visual swap) the right 1.0 shape?** Yes. Combat.prefab
> nesting one specific archetype-prefab as the canonical enemy visual is itself
> a bridge-shaped pattern (ADR-0011 sniff #2 "parallel storage" — Combat.prefab
> stores enemy visual state separately from the per-archetype prefab roster).
> Slice 2.6 didn't introduce the bridge; it exposed one that was already there.
> Cut it now and the rest of the visual layer (bar stack, hit zones, anchors)
> falls into a single-source-of-truth shape per archetype.
>
> **Do-now small** scope:
> 1. Strip enemy-visual instantiation from `CombatPrefabAuthor` — author empty mounts.
> 2. Add `CombatController.InstantiateEnemyVisual()` invoked from `HandleCombatReady` before `OnCombatRebuilt` fires.
> 3. Mirror mount for player vehicle (future-proofs loot/customization per ADR-0012; same seam, no extra work).
> 4. Keep placeholder squares until PSBs land — this slice doesn't gate on art.
> 5. Upgrade canvas-center fallback `LogWarning` to `LogError` after swap — missing anchors become a hard authoring bug.
>
> **Second-order watch:** Player customization will need the same swap-on-rebuild
> path when parts install/uninstall (ADR-0012). Defer-and-batch is bridge-shaped
> temptation; resist. Build the player-side mount + swap now even though player
> archetype doesn't change between beacons in a run, so when the ADR-0012 part-
> install slice lands it just calls the same `Instantiate→Bind→Rebuild` path.
>
> **What is not OK:** Adding a per-archetype `if (Dredge) augment IronShepherd
> catalog` branch. That is the bridge in concentrated form — it makes Combat.prefab
> "the IronShepherd combat, plus patches per other archetype" — and the patches
> would multiply as enemies are added. Reject any version of Option A that
> doesn't fully strip the nested archetype from Combat.prefab.

**Implementation-detail finding from author research (post-TD-verdict):** TD's
phrasing "small scope" understated the bind-ordering ripple. CombatHud's
`BuildEnemyBarStack` calls `BindForCombat(_enemyVehicleVisual)` from `Awake`,
which captures `_combatVisual` once. To swap visuals per beacon, the bind itself
must move from `Awake` → `HandleCombatRebuilt`, and the visual must be assigned
before that event fires. This is included in the proposed change above but
constitutes additional surface area in `CombatHud` beyond what the TD verdict
read suggested. Surface explicitly so the user can decide whether the scope
matches what they meant by "ok go ahead."

## Acceptance criteria

- Author Combat Prefab produces a `Combat.prefab` with empty `PlayerVehicle` and `EnemyVehicle` Transform mounts (no nested archetype prefab references).
- Entering combat against DuneSkimmer / IronShepherd / Dredge in turn:
  - Spawns the correct visual under each mount.
  - VehicleBarStack reports zero `_anchorMissingWarned` entries for any of the three archetypes.
  - Per-slot bars + markers appear at their hand-placed anchor positions.
  - Hit-zone drag-cast continues to land hits on slots.
- VehicleBarStack now `LogError`s instead of `LogWarning`s when an anchor is missing for a SlotId (verified by temporarily removing an anchor from a prefab in a manual test).
- Player-side mount carries its own runtime-spawned `PlayerVehicle.prefab`; no nested player vehicle in `Combat.prefab`.
- CombatSceneBlockout's animator binding survives across 3+ beacon swaps (no NRE in `_enemyAnimator.Bind`).

## Next steps after capture approval

1. Implement AuthorCombat empty-mount rewrite (`CombatPrefabAuthor.cs`).
2. Implement runtime swap (`CombatController.cs` + `CombatHud.cs`).
3. Upgrade VehicleBarStack warn → error.
4. Re-author Combat.prefab (Tools > Wasteland Run > Author Combat Prefab) — *captured per pre-author capture protocol; this file IS the capture*.
5. Manual Play Mode verification against all three archetypes.
6. Flip Slice 2.6 capture to fully shipped; close `project_dredge_uvs_deferred` memory pointer.
