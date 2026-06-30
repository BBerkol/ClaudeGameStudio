# Polish Capture: 4-Vehicle Prefab Drift Bake (2026-06-30)

**Date:** 2026-06-30
**System:** Vehicle prefab authoring (`CombatPrefabAuthor.BuildVehicleScaffold` +
  `BuildHitZonesForVehicle` + `BuildVehicleHudAnchors`)
**Trigger:** User edited PlayerVehicle, DuneSkimmer, IronShepherd, Dredge in
  Prefab Mode after Slice 2.6 Phase 1c. Bake protocol fires before any
  re-author menu can run.
**Affected paths:**
- `Assets/Editor/CombatPrefabAuthor.cs` — `VehicleScaffoldSpec` extended;
  `BuildVehicleScaffold`, `BuildHitZonesForVehicle`, `BuildVehicleHudAnchors`
  read per-vehicle override tables.
- `Assets/Prefabs/CombatView/PlayerVehicle.prefab` — regenerated from baked source.
- `Assets/Prefabs/CombatView/Enemies/DuneSkimmer.prefab` — regenerated.
- `Assets/Prefabs/CombatView/Enemies/IronShepherd.prefab` — regenerated.
- `Assets/Prefabs/CombatView/Enemies/Dredge.prefab` — regenerated.
- `Assets/Prefabs/CombatView/Combat.prefab` — re-authored to refresh
  `RunSceneHost._combatBeaconArchetypes` (fixes runtime DuneSkimmer crash).

## Why this capture

Designer ran a pass on all 4 vehicles tuning HUD anchors, hit zones, sprite-slot
positions, and decoration silhouettes. A naive re-author would wipe every tweak
(twice-burned on DuneSkimmer already — memory `feedback_pre_author_bake_hook`).
This capture lists every value the bake must preserve, with per-vehicle sections.

## Drift surface — PlayerVehicle (clean / position-only)

**HudAnchors** (current author seed → designer value):
| Anchor | Author seed | Designer value |
|---|---|---|
| `weapon_0` | (1.64, 1.27) | (1.959, 0.844) |
| `weapon_1` | (-2.45, 1.81) | (-1.855, 1.368) |
| `engine_0` | (2.40, 0.75) | (3.16, 0.404) |
| `mobility_0` | (3.61, -0.50) | (-2.154, -0.096) |

**MainBar** localPosition: designer set `anchoredPosition=(0, -1.85)` on the
nested MainBar instance (author currently leaves at prefab default).

**Hit zones**: intact, no drift.
**FrameSprite subtree**: intact.

## Drift surface — DuneSkimmer (heavy structural)

**Sprite slot positions under FrameSprite**:
- `EngineSlot`: (0.67, -0.696)
- `WeaponSlot` (weapon_0): (-1.785, 0.473)

**Wheel positions under MobilitySlot**:
- `WheelRearNear`: (-1.45, 0.1)
- `WheelRearFar`: (-1.2, 0.2)
- `WheelFrontNear`: (2.159, 0.1)
- `WheelFrontFar`: **DELETED** (structural subtraction)

**Decorations subtree** under FrameSprite:
- 4 of 5 children deleted (Door / Bumper / Canister / Visor / Light all gone)
- Only `FrontwheelConnection` remains (a new designer-added child)
- `VehicleVisual._decorations` array references are null

**HitZones** (only those that drifted):
- `HitZone_MachineGun` weapon_0: position tuned (off author seed (1.6374, 0.264))

**HudAnchors**:
| Anchor | Author seed | Designer value |
|---|---|---|
| `weapon_0` | (1.64, 1.27) | (-1.065, 1.026) |
| `engine_0` | (2.40, 0.75) | (1.404, -0.665) |
| `mobility_0` | (3.61, -0.50) | (-0.589, -0.5) |

## Drift surface — IronShepherd (medium)

**Decorations subtree**: gutted — single "Empty" placeholder at
(0.527, -0.304, 0) scale (0.33, 0.33, 0.33). All 5 named sprite children gone.
`VehicleVisual._visor / _light / _bumper / _canister` all null; `_door` references
the remaining SR.

**HudAnchors**:
| Anchor | Author seed | Designer value |
|---|---|---|
| `weapon_0` | (1.64, 1.27) | (1.921, 0.825) |
| `weapon_1` | (-2.45, 1.81) | (-1.695, 1.374) |
| `engine_0` | (2.40, 0.75) | (3.242, 0.343) |
| `mobility_0` | (3.61, -0.50) | (-2.151, -0.122) |

**MainBar**: missing entirely from HudAnchors (Phase 1c re-author hadn't been
run for this prefab yet — the SeedMainBarAnchor scale fix will land it).

**HitZones**: intact (positions match author).
**Sprite slot positions**: intact.

## Drift surface — Dredge (medium structural + new HitZone)

**NEW HitZone — designer-added (must bake as additive)**:
- `HitZone_WheelsMiddle`: anchoredPosition=(-3.1048, -0.6912), sizeDelta=(1.4824, 1.4824)
- slotId=`mobility_0` (routes middle-axle taps to the wheels subsystem)
- `_sourceRenderer` → middle-wheel SR
- Originated as a duplicate of `HitZone_WheelsRear`, renamed + repositioned over
  the middle-axle asset (user confirmed in turn message).

**HitZones — moved**:
- `HitZone_Javelin` weapon_2: (1.0, 0.9) → (2.06, 0.4)

**Decorations subtree**: 4 of 5 SRs orphaned. `VehicleVisual._visor / _light /
_bumper / _canister` all null; only `_door` wired.

**HudAnchors** (all 7 repositioned):
| Anchor | Author seed | Designer value |
|---|---|---|
| `weapon_0` | (1.64, 1.27) | (0.23, 1.7) |
| `weapon_1` | (-2.45, 1.81) | (-2.05, 1.37) |
| `weapon_2` | (1.00, 1.90) | (2.47, 0.77) |
| `engine_0` | (2.40, 0.75) | (3.4, 0.18) |
| `mobility_0` | (3.61, -0.50) | (-2.24, -0.1) |
| `slot_exposable_1` | (1.80, 0.50) | (1.41, -0.12) |
| `slot_exposable_2` | (-1.80, 0.50) | (0.23, 0.2) |

**Wheels**: all 6 present, `WheelRearMidFar` at (-1.24, 0.4) (author comment had -1.1).

## Bake architecture (proposed — pending user approval)

The drift surface splits cleanly into two layers:

**Layer A — position overrides (mechanical bake)**
- Per-vehicle `Dictionary<string, Vector2>` for HudAnchor positions
- Per-vehicle `Dictionary<string, Vector2>` for HitZone positions
- Per-vehicle `Dictionary<string, Vector2>` for FrameSprite-child slot positions
  (EngineSlot, WeaponSlot, WeaponSlot 2/3)
- Per-vehicle `Dictionary<string, Vector2>` for Wheel positions
- `Vector2? MainBarLocalPosition` per vehicle

**Layer B — structural overrides (additive/subtractive)**
- `bool HasWheelsMiddleHitZone` + `Vector2 WheelsMiddleHitZonePos` (Dredge only)
- `DecorationMode` enum: `All` (PlayerVehicle), `None` (DuneSkimmer / IronShepherd),
  `DoorOnly` (Dredge) — controls which Decoration children get authored
- `bool SkipFrontFarWheel` (DuneSkimmer only) — gates the FrontFar wheel build

**Spec shape (TD-approved with refinements)**:
```csharp
private struct VehicleScaffoldSpec
{
    // ... existing fields ...

    // Per-vehicle position overrides — null/empty = use shared author seed.
    // Struct lists (not Dictionaries) so authoring sites read top-to-bottom,
    // diffs are line-stable, and the existing MakeSlotStats helper pattern
    // ports cleanly. Builders convert to dict on entry for O(1) lookup.
    public List<PositionOverride> AnchorPositions;
    public List<PositionOverride> HitZonePositions;
    public List<PositionOverride> SpriteSlotPositions;
    public List<PositionOverride> WheelPositions;
    public Vector2? MainBarLocalPosition;

    // Structural overrides — named-and-honest, not generalised. Revisit
    // only when a 5th vehicle adds a 4th structural axis.
    public DecorationMode Decorations;       // default All
    public bool HasWheelsMiddleHitZone;      // Dredge only
    public Vector2 WheelsMiddleHitZonePos;
    public bool SkipFrontFarWheel;           // DuneSkimmer only
}

private struct PositionOverride { public string SlotId; public Vector2 Pos; }
private enum DecorationMode { All, None, DoorOnly }

// Helper, mirrors MakeSlotStats authoring pattern
private static List<PositionOverride> MakePositions(
    params (string slotId, Vector2 pos)[] entries) { ... }
```

**Resolution pattern**: each builder method tries the per-vehicle override
list first (converted to dict on entry); falls back to the shared author seed
if no entry. Designer can still re-tune in Prefab Mode; the bake protocol
re-runs on the next disclosure.

**Additional bake requirement (TD-flagged)**: when `Decorations != All`, the
author must explicitly null `VehicleVisual._visor/_light/_bumper/_canister`
SerializedProperties on the SerializedObject — not just skip child creation.
Otherwise the SR refs persist as dangling references to deleted GameObjects,
which is an ADR-0011 vestige.

## Technical Director Review

**Verdict: CONCERNS — not REJECT. Direction is right; ADR-0011-clean (this
IS the final 1.0 shape, not a bridge).**

Two refinements applied above:
1. **Struct list, not Dictionary** for position overrides. Authoring sites
   are read top-to-bottom by humans diffing against this capture; a
   `List<PositionOverride>` (with a `MakePositions(...)` helper mirroring
   the existing `MakeSlotStats`) gives ordered, greppable, line-stable diffs.
   Dictionaries serialise unpredictably in C# initialisers. Builders convert
   list→dict on entry for O(1) lookup. Same runtime cost, much cleaner author.
2. **Keep ad-hoc Layer B fields, don't generalise.** A per-vehicle override SO
   would split the source of truth across two surfaces and re-introduce the
   bake-drift problem this protocol exists to prevent. `SkipChildren: string[]`
   + `AddHitZones: HitZoneSpec[]` over-engineers a category with N=1 occupant
   each. Three named bools/enums are honest about the drift — they read like
   a changelog.

**Gaps the capture missed (now folded in)**:
- Clearing `VehicleVisual` SR refs (`_visor/_light/_bumper/_canister`) when
  `DecorationMode != All` — see "Additional bake requirement" above.
- Sorting orders not in the drift surface. One-line grep before commit to
  confirm designer didn't tune any `SpriteRenderer.sortingOrder` per-vehicle.

**Scope**: one commit, not staged. All four vehicles drifted together; partial
bake means partial protection (next re-author wipes whatever wasn't baked).
Tasks #42 + #43 collapse into this single bake.

— `technical-director`, 2026-06-30

## Re-author sequence (after bake lands)

1. Tools → Wasteland Run → Author PlayerVehicle Prefab
2. Tools → Wasteland Run → Author Enemy Archetype Prefabs (rebuilds all 3)
3. Tools → Wasteland Run → Author Combat Prefab  ← **MANDATORY** — refreshes
   `RunSceneHost._combatBeaconArchetypes` (fixes the DuneSkimmer roster crash).
4. Enter Play Mode; verify all 4 vehicles render with anchors/hit zones in
   the tuned positions.
5. Clear sentinel: `rm production/session-state/prefab-drift-pending.json`

## Open questions for user

1. **Decoration silhouettes** — DuneSkimmer + IronShepherd kept zero standard
   decorations. Confirm `DecorationMode.None` for both (vs `DoorOnly`)?
2. **DuneSkimmer FrontwheelConnection child** — this is a designer-added
   sprite under Decorations. Is it intended as a permanent decoration
   (needs a new sprite slot in author) or a temporary placement marker?
3. **Dredge structural drift** — only `_door` is wired; the other 4
   Decoration SRs exist as null refs. Bake as `DecorationMode.DoorOnly`
   (deletes other 4 SRs from prefab) — confirm?
4. **MainBar localPosition** drift seen only on PlayerVehicle so far.
   Should `MainBarLocalPosition` be a per-vehicle field or shared default?

---

## Amendment v2 — 2026-06-30 (second designer pass)

The v1 bake landed but designer ran a **second** Prefab Mode pass after re-author
to dial in sprite assets, scales, and a structural rename. v1 baked positions
and structural toggles; v2 adds the surface v1 missed (sprite refs, per-wheel
scales, shadow tuning, decoration placeholder rename). All values pulled from
the live prefab YAML via Explore agent — see `Assets/Prefabs/Enemies/*.prefab`.

### Spec extensions

`VehicleScaffoldSpec` gained five new fields + two helper types:

```csharp
// New / expanded fields
public bool HideDecorationsContainer;    // IronShepherd = true
public DecorationPlaceholder? Placeholder; // Dredge "Empty"
public string FrameSpriteName;           // DuneSkimmer = "DuneSkimmer Frame"
public string WeaponSlot3SpriteName;     // Dredge = "Javelin"
public List<ScaleOverride> WheelScales;
public List<ScaleOverride> SpriteSlotScales;
public Vector3? ShadowLocalScale;
public float ShadowOffsetX;
public List<PositionOverride> HitZoneSizeDeltas;

private struct ScaleOverride { public string Name; public Vector3 Scale; }
private struct DecorationPlaceholder {
    public string Name; public Vector3 LocalPosition;
    public Vector3 LocalScale; public Vector3 LocalEulerAngles;
    public Color Color;
}
```

Bool polarity flipped on the decorations gate: `HideDecorationsContainer`
(default false) > `HasDecorationsContainer` because C# zero-init means default
`false` would have broken the 3 existing vehicles. Inversion keeps current
behaviour as the no-op default.

### Drift surface — DuneSkimmer (v2)

- **Frame sub-sprite**: `frame` → `DuneSkimmer Frame` (Vehicle_Buggy.psb sub-sprite,
  spriteID `37e9515e8108de34086716363635bf5c`).
- **HitZone_Frame sizeDelta**: `(8.7189, 2.4798)` → `(4.1784, 2.4798)`
  (compacted to fit the smaller chassis silhouette).
- **HitZone positions** (re-anchored on the tighter Frame):
  | Zone | v1 author | v2 designer |
  |---|---|---|
  | `HitZone_MachineGun` | (1.6374, 0.264) | (-1.819, 0.725) |
  | `HitZone_Engine` | (2.3986, -0.2496) | (0.739, -0.65185) |
  | `HitZone_Wheels` | (3.611, -0.8019) | (2.146, -0.880) |
  | `HitZone_WheelsRear` | (-3.1048, -0.6912) | (-1.464, -0.880) |
  | `HitZone_Frame` | (0.0, -0.0101) | (-0.2197, -0.0101) |
- **Wheel scales**: stock Near = 1.0, stock Far = 0.9. Designer baked:
  | Wheel | Scale |
  |---|---|
  | `WheelRearNear` | (0.80, 0.80, 1) |
  | `WheelRearFar` | (0.75, 0.75, 1) |
  | `WheelFrontNear` | (0.80, 0.80, 1) |
- **WheelRearFar position**: (-1.20, 0.20) → (-1.249, 0.20) (small nudge).
- **Shadow scale**: stock (1, 1, 1) → (0.5117952, 1, 1) on **both** Shadow and CarShadow.
- **Shadow X offset**: 0 → 0.1953 on both.
- **FrontwheelConnection color**: white → `(0.32156864, 0.227451, 0.1137255, 1)` (brown-tan).

### Drift surface — IronShepherd (v2)

- **Decorations container**: GO `Decorations` deleted entirely. New spec field
  `HideDecorationsContainer=true`; `_decorations` SerializedProperty wires to null.
- **Wheel scales**: match stock defaults (Near 1.0, Far 0.9) — no spec override.
  (v1 capture suggested per-wheel overrides; verified-against-YAML they were unnecessary.)

### Drift surface — Dredge (v2)

- **WeaponSlot 3 sprite**: light-blue placeholder → `Javelin` PSB sub-sprite.
- **Sprite slot positions** (under FrameSprite):
  | Slot | v1 author | v2 designer |
  |---|---|---|
  | `WeaponSlot` | (1.64, 0.266) | (0.110, 1.190) |
  | `WeaponSlot 3` | (1.0, 0.9) | (1.900, 0.340) |
  | `Exposable_1Slot` | (1.8, -0.5) | (0.300, -0.060) |
  | `Exposable_2Slot` | (-1.8, -0.5) | (-0.770, -0.060) |
- **Sprite slot scales**: Exposable_1Slot + Exposable_2Slot baked at (0.8, 0.8, 1)
  via new `SpriteSlotScales` spec field (was hardcoded in v1; now data-driven).
- **Decorations rename**: standard `Door` SR renamed to `Empty`. v1 baked as
  `DecorationMode.DoorOnly`; v2 switches to `DecorationMode.None` + a
  `Placeholder` carrying the designer's `Empty` GO config:
  - Name `Empty`
  - LocalPosition (0.9, -0.139, 0)
  - LocalScale (0.21, 0.21, 0.21) — uniform
  - Color (0.09019608, 0.07450981, 0.06666667, 1) — near-black
  - Sprite: PlaceholderSquare (designer reassigns in Prefab Mode)
- **VehicleVisual._door wiring**: BuildVehicleScaffold's `_door` SerializedProperty
  now falls through to the placeholder SR when no canonical Door SR is built.
  Keeps flame-eats-door and ADR-0011 invariants intact.
- **HUD anchors** retuned a second time (delta vs v1):
  | Anchor | v1 | v2 |
  |---|---|---|
  | `weapon_0` | (0.23, 1.70) | (0.45, 1.74) |
  | `weapon_1` | (-2.05, 1.37) | (-1.86, 1.38) |
  | `weapon_2` | (2.47, 0.77) | (2.10, 0.77) |
  | `engine_0` | (3.40, 0.18) | (3.25, 0.24) |
  | `mobility_0` | (-2.24, -0.10) | (-2.14, -0.10) |
  | `slot_exposable_1` | (1.41, -0.12) | (0.92, -0.08) |
  | `slot_exposable_2` | (0.23, 0.20) | (-0.17, 0.29) |

### Builder changes (BuildVehicleScaffold + BuildHitZonesForVehicle)

- `BuildVehicleScaffold` now resolves `frameSpriteName` and `weapon3Sprite` per
  spec; gates the entire Decorations container creation on `!HideDecorationsContainer`;
  applies `Placeholder` after the standard 5 SRs; applies `ShadowLocalScale` +
  `ShadowOffsetX` to both Shadow and CarShadow; applies per-wheel overrides
  via `ApplyWheelScale(wheel, dict, name)` helper after wheel spawn.
- `BuildHitZonesForVehicle` resolves `size:` arg through the new `hzSizeOverrides`
  dict; all existing zones keep their v1 shared seed unless the spec overrides.

### Re-author sequence (post-v2 bake)

1. Tools → Wasteland Run → Author Enemy Archetype Prefabs (rebuilds all 3).
2. Tools → Wasteland Run → Author PlayerVehicle Prefab.
3. Tools → Wasteland Run → Author Combat Prefab — **MANDATORY** — refreshes the
   nested archetype refs on `Combat.prefab._enemyArchetypePrefab` + RunSceneHost.
4. Enter Play Mode; verify each vehicle renders with:
   - DuneSkimmer: small chassis, smaller tires, compacted shadow, brown frontwheel cover
   - IronShepherd: no Decorations GO at all in hierarchy; bars + hit zones intact
   - Dredge: Javelin sprite on WeaponSlot 3, "Empty" placeholder under Decorations,
     re-tuned anchors
5. Clear sentinel: `rm production/session-state/prefab-drift-pending.json`

### Coverage check

After v2 the bake captures every value the Explore agent surfaced from the
live YAML — positions, scales, sprites, colors, structural absence. Designer
can re-author with confidence that no Prefab Mode work is lost.

— `claude (continued from compaction)`, 2026-06-30
