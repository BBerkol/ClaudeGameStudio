# Vehicle Prefab Drift Capture — 2026-06-30

**Scope:** Bake every designer-tuned value in the four vehicle prefabs back into
`Assets/Editor/CombatPrefabAuthor.cs` so re-authoring no longer wipes the work.
Triggered by the user's in-flight visual rework: enemy decorations stripped,
DuneSkimmer fully re-shaped, Dredge mid-axle hitzone added, per-vehicle
VehicleHudAnchors (Slice 2.6) all designer-positioned.

**Source of truth at capture time:** YAML on disk in
`Assets/Prefabs/CombatView/PlayerVehicle.prefab`,
`Assets/Prefabs/Enemies/DuneSkimmer.prefab`,
`Assets/Prefabs/Enemies/IronShepherd.prefab`,
`Assets/Prefabs/Enemies/Dredge.prefab`.

---

## Player Vehicle (`PlayerVehicle.prefab`)

### VehicleHudAnchors (Slice 2.6 — designer-tuned this session)

| Anchor | anchoredPosition | seed equivalent | drift? |
|---|---|---|---|
| Anchor_weapon_0   | (1.45,  0.36)  | (1.40, 0.47) | yes |
| Anchor_weapon_1   | (-2.30, 0.75)  | (-2.45, 0.77) | yes |
| Anchor_engine_0   | (2.45, -0.13)  | (2.40, -0.10) | yes |
| Anchor_mobility_0 | (3.22, -0.76)  | (-2.58, -0.50) | yes (radical) |

User's note: "for player i just changed positions of markers." These are
the markers being referenced — bars not yet positioned (player markers don't
host live bars in current build).

### Sprite slots — all at seed positions (no drift)
- FrameSlot / FrameSprite: (0,0,0)
- WeaponSlot: (1.640, 0.266) — seed
- WeaponSlot 2: (-2.456, 0.812) — seed
- EngineSlot: (2.400, -0.250) — seed
- WheelFrontNear (3.61, 0.20) scale 1
- WheelFrontFar (3.20, 0.35) scale (0.9, 0.9)
- WheelRearNear (-3.10, 0.30) scale 1
- WheelRearFar (-2.70, 0.40) scale (0.9, 0.9)

### Decorations — KEEP active (player keeps its decorations)
- Door     @ (0.168, -0.139, 0.000)
- Bumper   @ (4.321, -0.457, -0.001)
- Canister @ (-1.153, 0.139, -0.002)
- Visor    @ (0.223, 0.610, -0.003)
- Light    @ (4.231, -0.426, -0.004)

### HitZones — all at seed positions (no drift)

---

## DuneSkimmer (`DuneSkimmer.prefab`)

### Sprite slots — radical drift, vehicle re-shaped to "tiny raider"

| Slot | current pos | current scale | seed pos | seed scale | drift |
|---|---|---|---|---|---|
| FrameSlot / FrameSprite | (0,0,0) | 1 | (0,0,0) | 1 | none |
| WeaponSlot              | (-1.699, 0.386) | 1 | (1.64, 0.266) | 1 | **flipped to LEFT** |
| EngineSlot              | (-0.553, 0.019) | 1 | (2.40, -0.25) | 1 | **moved to CENTER** |
| WheelRearNear           | (-1.40, 0.10) | (0.8, 0.8) | (-3.10, 0.30) | 1.0 | **closer + smaller** |
| WheelRearFar            | (-1.16, 0.20) | (0.7, 0.7) | (-2.70, 0.40) | 0.9 | **closer + smaller** |
| WheelFrontNear          | ( 2.14, 0.10) | (0.8, 0.8) | ( 3.61, 0.20) | 1.0 | **closer + smaller** |
| WheelFrontFar           | **DELETED** | — | (3.20, 0.35) | 0.9 | **gone (3-wheel skimmer)** |

### Decorations — all DELETED (none present)
No Door / Bumper / Canister / Visor / Light GameObjects in prefab.

### VehicleHudAnchors

| Anchor | anchoredPosition |
|---|---|
| Anchor_weapon_0   | (-1.603,  0.612) |
| Anchor_engine_0   | (-0.703,  0.381) |
| Anchor_mobility_0 | (-1.119, -0.519) |

### HitZones — UNCHANGED from seed (this is the bug — user wants these retuned)

| Zone | current pos | current size | should target |
|---|---|---|---|
| HitZone_MachineGun | (1.6374, 0.264) | (1.6833, 1.0096) | WeaponSlot @ (-1.70, 0.39) |
| HitZone_Engine     | (2.3986, -0.2496) | (1.1963, 1.1963) | EngineSlot @ (-0.55, 0.02) |
| HitZone_Wheels     | (3.6110, -0.8019) | (1.3396, 1.3396) | FrontWheel @ (2.14, 0.10) |
| HitZone_WheelsRear | (-3.1048, -0.6912) | (1.4824, 1.4824) | RearWheels @ (-1.4..-1.16) |
| HitZone_Frame      | (0.000, -0.0101) | (8.7189, 2.4798) | Frame (likely smaller now) |

**This is the slice's primary action item: retune Skimmer hitzones to match new visual.**

---

## IronShepherd (`IronShepherd.prefab`)

### Sprite slots — at seed positions
- All sprite slots match seed values.
- Wheels at seed positions/scales.

### Decorations — all DELETED (none present)
No Door / Bumper / Canister / Visor / Light GameObjects in prefab.

### VehicleHudAnchors

| Anchor | anchoredPosition |
|---|---|
| Anchor_weapon_0   | ( 1.40,  0.47) |
| Anchor_weapon_1   | (-2.45,  0.77) |
| Anchor_engine_0   | ( 2.40, -0.10) |
| Anchor_mobility_0 | (-2.58, -0.50) |

### HitZones — at seed positions (no drift; visual didn't change)

---

## Dredge (`Dredge.prefab`)

### Sprite slots — partial drift

| Slot | current pos | current scale | seed | drift |
|---|---|---|---|---|
| FrameSlot / FrameSprite | (0,0,0) | 1 | (0,0,0) | none |
| WeaponSlot              | ( 0.06,  1.20) | 1 | ( 1.64, 0.266) | **top-mounted MG** |
| WeaponSlot 2            | (-2.456, 0.812) | 1 | (-2.456, 0.812) | none |
| WeaponSlot 3 (Javelin)  | ( 1.95,  0.40) | 1 | (Javelin source has no SR slot pos in seed yet) | designer placement |
| EngineSlot              | ( 2.40, -0.25) | 1 | seed | none |
| WheelFrontNear          | ( 3.61, 0.20) | 1 | seed | none |
| WheelFrontFar           | ( 3.20, 0.35) | (0.9, 0.9) | seed | none |
| WheelRearNear           | (-3.10, 0.30) | 1 | seed | none |
| WheelRearFar            | (-2.70, 0.40) | (0.9, 0.9) | seed | none |
| WheelRearMidNear        | (-1.50, 0.30) | 1 | seed | none |
| WheelRearMidFar         | (-1.20, 0.40) | (0.9, 0.9) | seed (-1.10, 0.40) | **+0.1 x** |
| Exposable_1Slot         | ( 0.51, 0.03, 0.001) | (0.8, 0.8) | designer-placed | tuned |
| Exposable_2Slot         | (-0.57, 0.01, 0.001) | (0.8, 0.8) | designer-placed | tuned |

### Decorations — all DELETED (none present)

### VehicleHudAnchors

| Anchor | anchoredPosition |
|---|---|
| Anchor_weapon_0          | (-0.17,  1.42) |
| Anchor_weapon_1          | (-2.30,  0.95) |
| Anchor_weapon_2          | ( 1.57,  0.51) |
| Anchor_engine_0          | ( 2.61,  0.04) |
| Anchor_mobility_0        | (-1.97, -0.50) |
| Anchor_slot_exposable_1  | ( 0.43, -0.04) |
| Anchor_slot_exposable_2  | (-0.61, -0.01) |

### HitZones — partial drift; user added one new zone

| Zone | current pos | current size | drift |
|---|---|---|---|
| HitZone_MachineGun     | (1.6374, 0.264)   | (1.6833, 1.0096) | seed (visual moved, not retuned yet) |
| HitZone_Flamethrower   | (-2.4521, 0.813)  | (1.9722, 1.0572) | seed |
| HitZone_Javelin        | ( 1.87, 0.30)     | (1.97, 1.00)     | **retuned by designer** |
| HitZone_Engine         | (2.3986, -0.2496) | (1.1963, 1.1963) | seed |
| HitZone_Wheels         | (3.6110, -0.8019) | (1.3396, 1.3396) | seed |
| HitZone_WheelsRear     | (-3.1048, -0.6912)| (1.4824, 1.4824) | seed |
| **HitZone_WheelsRear 2** (NEW) | (-1.59, -0.6912) | (1.4824, 1.4824) | **USER ADDED — mid-axle zone** |
| HitZone_Frame          | (0, -0.0101)      | (8.7189, 2.4798) | seed |
| HitZone_Exposable_1    | ( 1.80, -0.50)    | (1.20, 0.90)     | seed |
| HitZone_Exposable_2    | (-1.80, -0.50)    | (1.20, 0.90)     | seed |

**Dredge hitzone retune deferred per user request** (Dredge visual not in yet —
"once dredge is in we can do its hitzones"). The single non-deferred action for
Dredge this slice: bake the NEW `HitZone_WheelsRear 2` and the Javelin retune
into the author so re-authoring doesn't drop them.

---

## What this capture authorizes baking into `CombatPrefabAuthor.cs`

### 1. VehicleScaffoldSpec gains per-vehicle override fields
- `IncludeDecorations` (bool) — Player = true, all enemies = false.
- `WheelLayout` (struct/array) — per-vehicle wheel positions + scales; supports
  3-wheel skimmer (no WheelFrontFar), 4-wheel default, 6-wheel Dredge.
- `SpritePositions` (struct) — overrides for WeaponSlot[0..2], EngineSlot,
  Exposable_1Slot, Exposable_2Slot per archetype.
- `HudAnchorPositions` (dict-like) — per-vehicle Anchor_* positions.
- `HitZoneOverrides` (dict-like) — per-vehicle HitZone_* positions/sizes,
  including the new `HitZone_WheelsRear_Mid` slot for Dredge (mobility_0).

### 2. Author changes
- `BuildHitZonesForVehicle` reads positions from spec, falls back to the
  PlayerVehicle baseline for any unset override.
- `BuildVehicleHudAnchors` reads anchor positions from spec.
- Wheel spawn block reads layout from spec (supports omitting WheelFrontFar).
- Decorations block skipped when `IncludeDecorations == false`.

### 3. Per-archetype Author* functions
- `AuthorPlayerVehicle`: Anchor positions updated (1.45,0.36 / -2.3,0.75 /
  2.45,-0.13 / 3.22,-0.76); IncludeDecorations=true.
- `AuthorDuneSkimmer`: full sprite + wheel + hitzone + anchor block;
  IncludeDecorations=false; 3-wheel layout.
- `AuthorIronShepherd`: Anchor positions updated (1.4,0.47 / -2.45,0.77 /
  2.4,-0.1 / -2.58,-0.5); IncludeDecorations=false; sprites/wheels seed.
- `AuthorDredge`: Anchor positions updated; IncludeDecorations=false;
  WeaponSlot moved to (0.06, 1.2); WeaponSlot 3 placement (1.95, 0.4);
  Exposable_1Slot (0.51, 0.03, z=0.001) scale 0.8; Exposable_2Slot
  (-0.57, 0.01, z=0.001) scale 0.8; HitZone_Javelin retuned; new
  HitZone_WheelsRear_Mid at (-1.59, -0.6912) size (1.4824, 1.4824).

### 4. Skimmer hitzone retune (the slice's named ask)
Recompute Skimmer hitzone positions/sizes from the new sprite anchors:
- HitZone_MachineGun → anchored at WeaponSlot (-1.70, 0.39); size to match
  weapon sprite bounds (TBD — needs visual measurement against sprite).
- HitZone_Engine → anchored at EngineSlot (-0.55, 0.02); size to match
  engine sprite bounds.
- HitZone_Wheels → anchored at WheelFrontNear (2.14, 0.10); size scaled
  to the 0.8 wheel.
- HitZone_WheelsRear → anchored at WheelRearNear (-1.4, 0.1) — closer center.
- HitZone_Frame → matches new frame extent (likely smaller).

Final Skimmer hitzone values to be measured against actual sprite outlines
during the bake.

---

## What is NOT being touched this slice

- **Dredge hitzone retune** — deferred until Dredge visual ships (user req).
- **Player bar positions** — user is still positioning; revisit after.
- **HitZone shape fidelity** — current hitzones are rectangles; user noted
  they want them to follow the sprite pixel outline ("we need to … form the
  hitzone highlight aswell"). This is a separate task — for this bake we
  reposition/resize the rectangles only; pixel-shape highlights stay as-is.
  The hover highlight currently uses `EnsureTargetHoverOutline` which derives
  from the zone's mask sprite — already silhouette-aware. The "still see
  rectangles" complaint may resolve once positions match (sprite bounds will
  match visual bounds, so highlight will look correct). Re-check after retune.

## Surfaced after capture — bar editability gap (user 2026-06-30)

**User feedback:** "i am unhappy with the editibility of the bars. their
positioning and scale i need to tweak but i cannot."

**Current state:** `VehicleHudAnchors` exposes per-slot RectTransform anchors
that the designer CAN move in Prefab Mode. The bar widgets, however, are
runtime-built by `VehicleBarStack.TryBuildCombatWidgets` from the
`_combatBarPrefab` — they live under each anchor at the prefab's authored
position/scale, with no per-vehicle override surface. Net result: designer can
move the MARKER but not the bar that lands under it.

**Three options to address (post-bake — not in this slice):**

A. **Static bar instances in vehicle prefab.** Move the bar prefab Instantiate
   out of `VehicleBarStack` and make each bar a hand-placed prefab instance
   under each anchor (one per slot). Designer scales/moves freely; runtime
   binds existing instances instead of spawning. Most flexible, biggest delta.

B. **Per-anchor offset + scale on `VehicleHudAnchors`.** Add `Vector2 BarOffset`
   and `Vector2 BarScale` per anchor entry. `VehicleBarStack` reads them when
   positioning the spawned bar. Smallest change; reaches ~80% of desired
   editability without touching prefab topology.

C. **Per-archetype bar prefab.** Add a `BarPrefab` field to each archetype's
   spec. Designer authors a different bar prefab per vehicle. Helps if bar
   ART differs per archetype but doesn't help with per-anchor tweaks.

Recommendation: **Option B first** (cheapest, unblocks user). Option A if B
proves insufficient after a tuning pass. Decision deferred to user.

---

## Technical Director Review (consulted 2026-06-30 — AMENDED)

TD overruled the "Option B first" recommendation above. Verdict summary:

**Decision 2 — Bars: OPTION A.** Option B is a bridge (ADR-0011 violation) —
adds a second source of placement truth (anchor world position + offset
numbers) that compose at runtime, designer still can't see bar in Prefab Mode.
Per user's `feedback_edit_prefab_visibility.md` memory: bars must render in
Prefab Mode without entering Play. Option A:
- Bars become authored nested-prefab children under each `Anchor_*` in
  `VehicleHudAnchors`. `SubsystemBar.prefab` + `SubsystemMarker.prefab` are
  instantiated **at author time** by `CombatPrefabAuthor`, not at runtime.
- `VehicleBarStack` becomes a pure binder — walks anchor list, reads the
  `SubsystemBar` child under each, registers with `Vehicle` model.
- Delete `SpawnAt`, `_barTemplate`, `_markerTemplate`, the
  `WarnAnchorMissingOnce`/`WarnHudAnchorsMissingOnce` flags, and the
  runtime `Instantiate(_barTemplate)` path entirely.
- Designer freely scales/moves/restyles bars in Prefab Mode; idempotent
  upsert on re-author preserves designer tweaks (same pattern as
  `VehicleHudAnchors.EditorUpsert`).

**Decision 1 — Spec arrays as the canonical end shape.** Approved with
amendments:
- Use typed structs (`WheelEntry`, `SpriteOverride`, `AnchorOverride`,
  `HitZoneOverride`) as flat arrays on `VehicleScaffoldSpec`.
- `null` array = "use historical default for this category." Empty array
  `Array.Empty<>()` = "explicitly authored zero entries." (Canonical
  default-vs-empty signal — no missing-key hidden branching.)
- **Drop `WheelCount` and `HasExposables`** — vestigial fork-and-patch
  flags. `Wheels.Length` is the truth; Skimmer's 3-wheel stops being a
  special case.
- Once bars live under anchors, the anchor's localPos IS the bar's
  position — no separate `BarPositions` field. ~30% spec surface reduction
  vs. the original "Option B" plan.

**Execution order: Phase 1 first, then Phase 2.**
- **Phase 1 (Option A topology shift, one commit):** Move bar/marker
  Instantiate from `VehicleBarStack` runtime to
  `CombatPrefabAuthor.BuildVehicleHudAnchors`. `VehicleBarStack` becomes
  binder. Delete `SpawnAt` + template SerializedFields + warn-once
  flags. Re-author all four vehicles with current anchor positions; bars
  now visible in Prefab Mode.
- **Phase 2 (drift bake, second commit):** Extend `VehicleScaffoldSpec`
  with the four arrays. Bake per-vehicle drift values from the tables
  above into the four `Author*` methods. Re-author. Diff against capture.

**Doing Phase 2 first wastes work** — would bake spec around runtime-spawn
placement contract that Phase 1 then invalidates.

**ADR audit (all clean under TD plan):**
- ADR-0011: Option A unifies topology (no parallel storage). Option B
  would have been a bridge.
- ADR-0015: Spec arrays = canonical scope narrowing.
- ADR-0016: Bars-as-authored-children passes "would a designer expect
  this here?" categorical-fit test.
- Test impact: none. `BindForCombat` contract unchanged.

**Validation criteria (TD's exit gates):**
1. Opening any vehicle prefab in Prefab Mode shows all bars rendered at
   authored positions — no Play required.
2. `grep "Instantiate(_barTemplate"` and `grep "Instantiate(_markerTemplate"`
   return zero hits in `VehicleBarStack.cs`.
3. After Phase 2 re-author, prefab YAML diff against capture tables is
   byte-identical for every drifted value.
4. Each `Author*` method is <80 lines, no `if (archetype == ...)` branches.
5. Designer can scale a `Bar_*` GameObject in Prefab Mode, save, and the
   change survives re-author (idempotent upsert).

**Protocol note from TD:** The pre-amendment "Option B first" recommendation
above was wrong to ship — runtime-vs-authored boundary calls warrant TD
consultation BEFORE the capture's recommendations section, not after the
user re-surfaces the concern. Adjusted process for next polish-capture
touching a similar boundary.

---

## Technical Director Review

**TD agent not consulted before this capture.** This drift is not a system
refactor — it's a one-shot bake of designer values that already exist on disk.
The architectural change (per-vehicle spec overrides) is mechanical: each
override field has a single non-conditional code path, no parallel-storage or
bridge concerns. The CI grep gates for ADR-0011 forbidden patterns are not
triggered (no `Legacy*`, no `Compat*`, no "If old path" branches).

If during the bake we hit a structural decision (e.g. how to express the
"3-wheel skimmer" cleanly without `if (WheelCount == 3)` branches, or whether
the per-archetype override list should be a Dictionary, an array, or just
`?:` defaults inline), surface it before coding and consult TD then.

---

## Acceptance criteria

1. After re-authoring all four prefabs, every value in the tables above is
   reproduced byte-for-byte in the prefab YAMLs (verified by diff).
2. DuneSkimmer hitzones overlay the new sprite positions (visual check in
   Play Mode against all three archetypes — Skimmer / IronShepherd / Dredge).
3. Dredge gains the new HitZone_WheelsRear_Mid zone in the
   `_hitZoneWheelsRearMid` (or equivalent) serialized field on VehicleVisual
   AND routes pointer events to `mobility_0`.
4. No enemy prefab regrows Decorations on re-author.
5. Existing tests still pass (no model-side changes — author-only).
