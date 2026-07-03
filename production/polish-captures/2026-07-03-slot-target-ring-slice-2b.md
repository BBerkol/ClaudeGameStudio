# SlotTargetRing Refactor — Slice 2b Canonical Cut — 2026-07-03

## Purpose

Slice 2b executes the canonical widget swap per ADR-0011: `SubsystemBar`
and `SubsystemMarker` are deleted in a single atomic cut; every vehicle
prefab is re-authored to mount `SlotTargetRing` under each
`VehicleHudAnchors` entry. Per project rule
`feedback_capture_before_destroy_view_layer`, this capture enumerates
every authored value about to be destroyed so Slice 2b re-authors from
one source of truth rather than reverse-engineering YAML drift.

Prior slice captures:

- `production/polish-captures/2026-07-02-slot-target-ring-prep.md`
  (Slice 1 palette / geometry / HideRule capture)

Prior verdicts:

- `production/td-verdicts/2026-07-01-slot-target-ring-refactor.md`
  (multi-slice refactor)
- `production/td-verdicts/2026-07-02-slot-target-ring-widget.md`
  (Slice 2a non-goals — widget in isolation)
- `production/td-verdicts/2026-07-03-slot-target-ring-slice-2b.md`
  (this slice — APPROVE-WITH-CHANGES, 6 binding decisions Q1-Q6)

Spec sheet: `design/hud/slot-target-ring.md`

## Slice 2b Precondition (blocking)

`Assets/Prefabs/CombatView/SlotTargetRing.prefab` does NOT exist on
disk. Slice 2a shipped `SlotTargetRing.cs` + `AuthorSlotTargetRingPrefab`
menu + `SlotTargetRingTests.cs`, but the author menu was never run.
Slice 2b cannot mount rings under vehicle anchors until this prefab
exists.

**Resolution (step 0 of 2b):** run
`Tools > Wasteland Run > Author SlotTargetRing Prefab` from a warm Unity
Editor session, OR batchmode-invoke via `-executeMethod
WastelandRun.CombatView.Editor.CombatPrefabAuthor.AuthorSlotTargetRingPrefabMenu`.

## Files at Risk (destructive scope for this slice)

Code to be deleted:

- `Assets/Scripts/CombatView/SubsystemBar.cs` (399 lines)
- `Assets/Scripts/CombatView/SubsystemBar.cs.meta`
- `Assets/Scripts/CombatView/SubsystemMarker.cs` (114 lines)
- `Assets/Scripts/CombatView/SubsystemMarker.cs.meta`

Prefabs to be deleted:

- `Assets/Prefabs/CombatView/SubsystemBar.prefab` (566 lines)
- `Assets/Prefabs/CombatView/SubsystemBar.prefab.meta`
- `Assets/Prefabs/CombatView/SubsystemMarker.prefab` (129 lines)
- `Assets/Prefabs/CombatView/SubsystemMarker.prefab.meta`

Font material orphan (per TD verdict "Files at Risk"):

- `Assets/Fonts/RussoOne SDF - SubsystemBar HpText.mat` — loaded by
  `CombatPrefabAuthor.cs:1345`; zero consumers after 2b (rings have no
  HP text per Q1). Delete.
- `.mat.meta` companion.

Code to rewrite (retire bar/marker refs, wire rings):

- `Assets/Scripts/CombatView/SlotTargetRing.cs` — drop `_tooltip` +
  `_tooltipKey` fields + Bind params (per Q1).
- `Assets/Scripts/CombatView/VehicleBarStack.cs` (972 lines) — replace
  bar+marker bind loop with ring bind loop; retire `TryBuildRestWidgets` /
  `UpdateRestBound` bar/marker branches; strip `_runtimeBars` /
  `_runtimeMarkers` fields.
- `Assets/Scripts/CombatView/VehicleHudAnchors.cs` — delete
  `ResolveBar` + `ResolveMarker` methods; `ResolveRing` becomes primary.
- `Assets/Scripts/CombatView/VehiclePartHitZone.cs` — rename
  `pairedTarget` binding argument to point at ring (rename-only per Q3).
- `Assets/Editor/CombatPrefabAuthor.cs` — rewrite `SeedHudAnchor` with
  name-scoped `DestroyImmediate` sweep (per Q4); delete
  `AuthorSubsystemBarMenu` + `AuthorSubsystemMarkerMenu`; delete font
  material load path at line 1345.
- Doc-comment sweeps in `BarWidget.cs`, `MainBarWidget.cs`,
  `ICombatHoverTarget.cs`, `AttackStateController.cs`,
  `VehicleVisual.cs` (no functional refs — comments only).

Test surface:

- `Assets/Tests/EditMode/CombatView/SlotTargetRingTests.cs` — update to
  new `Bind` signature (drop tooltip params).
- No `SubsystemBarTests` / `SubsystemMarkerTests` on disk (already gone
  from prior slices).

Vehicle prefabs (7 total — re-authored via
`AuthorPlayerVehicle` / `AuthorEnemyArchetypePrefabs` +
`AuthorCombatPrefab`, then binder rebake):

- `Assets/Prefabs/CombatView/PlayerVehicle_Scout.prefab`
- `Assets/Prefabs/CombatView/PlayerVehicle_Assault.prefab`
- `Assets/Prefabs/CombatView/PlayerVehicle_Truck.prefab`
- `Assets/Prefabs/CombatView/Enemy_Dune.prefab`
- `Assets/Prefabs/CombatView/Enemy_IronOx.prefab`
- `Assets/Prefabs/CombatView/Enemy_Dredge.prefab`
- `Assets/Prefabs/CombatView/Combat.prefab` (RunSceneHost binder)

## Authored Values About to be Destroyed

### SubsystemBar.prefab (root component)

| Field | Value | Source |
|-------|-------|--------|
| Root `RectTransform.sizeDelta` | `(240, 20)` | prefab line 466 |
| `_pixelsPerHp` | `8` | prefab line 489 (overrides source default `4f`) |
| `_palette` | `CombatBarPalette.asset` (guid `c612067c785d2ee43a5aa28c2e64e6d9`) | line 483 |
| `_barBgColor` (fallback) | `(0.22, 0.22, 0.24, 0.95)` | line 484 |
| `_hpBandGreen` (fallback) | `(0.40, 0.85, 0.40, 1)` | line 485 |
| `_hpBandYellow` (fallback) | `(0.95, 0.85, 0.30, 1)` | line 486 |
| `_hpBandRed` (fallback) | `(0.90, 0.30, 0.25, 1)` | line 487 |
| `_previewColor` (fallback) | `(1.00, 0.55, 0.10, 1)` | line 488 |
| Child bindings | `_barBg`, `_remainingFill`, `_previewFill`, `_hpText`, `_nameText` | lines 480-491 |

Nested children (all destroyed with the prefab):

| Child | sizeDelta | Note |
|-------|-----------|------|
| `BarBg` | `(240, 20)` | background rect (line 42) |
| `PreviewFill` | `(0, 20)` | runtime-driven width (line 117) |
| `HpText` | `(0, 20)` | stretched TMP (line 192) |
| `NameText` | `(0, 10)` | stretched TMP (line 329) |
| `RemainingFill` | `(240, 20)` | HP fill rect (line 527) |

`_pixelsPerHp: 8` is a **prefab-level override** of the source default
`4f`. Rings ship band-color-only (no fill fraction in 2b per Q2), so
this value has no ring successor — captured for historical fidelity only.

### SubsystemMarker.prefab (root component)

| Field | Value | Source |
|-------|-------|--------|
| Root `RectTransform.sizeDelta` | `(10, 10)` | prefab line 38 |
| `_circleDiameterPx` | `10` | prefab line 54 |
| `_circleColor` | `(1, 1, 1, 0.85)` | prefab line 53 |

Nested child:

| Child | sizeDelta | Color |
|-------|-----------|-------|
| `Circle` | `(10, 10)` | `(1, 1, 1, 0.85)` (line 113) |

Marker's circle color was already promoted to
`CombatBarPalette.SubBarMarkerRing` in Slice 2a
(`(1.00, 1.00, 1.00, 0.85)`) — captured here for the Ring outline
baseline; no value lost.

### CombatPrefabAuthor.SeedHudAnchor authored constants

Values stamped onto every vehicle anchor at author time (retired with
the widget swap — replaced by `SlotTargetRing.prefab` mount):

- `SubsystemBarLocalSize = new Vector2(240f, 20f)` — bar sizeDelta stamped
  every author run
- `MarkerLocalAnchoredPos = new Vector2(-55f, -22f)` — global marker
  offset relative to Anchor_{slotId}
- Font material load path: `Assets/Fonts/RussoOne SDF - SubsystemBar
  HpText.mat` at line 1345

**All three retired.** Ring geometry is authored inside
`AuthorSlotTargetRingPrefab` (Slice 2a code) with:

- outer 40px, outline 3px, icon 28px (matches spec sheet)
- outline sprite / icon sprite / preview arc sprite / hover outline
  sprite refs already wired in the 2a author menu

## Load-Bearing Contracts That Survive

These behaviors must carry forward — the widget shape changes, the
semantic contract does not:

1. **HideRule mapping.** `HideOnFullUnlessAttackActive` (combat) /
   `AlwaysVisible` (rest) for `AttackStateGated`; `HideOnFullOrDestroyed`
   (combat) / `AlwaysVisible` (rest) for `DamagedOnly`; `AlwaysVisible`
   both modes for `AlwaysVisible`. Ring already implements all 5
   `HideRule` cases inherited from `SubsystemBar.HideRule` — verified in
   `SlotTargetRing.cs:38-45`.
2. **Slot exclusions.** `IsStructural == true` skipped
   (MainBar owns chassis); `SlotKind.Armor` skipped (armor is buffer per
   `project_armor_not_subsystem`). `VehicleBarStack.BuildPerSlotBars`
   filter block survives verbatim into the ring bind loop.
3. **VehiclePartHitZone as primary drag-cast surface.** Zones own
   pixel-perfect alpha hit-testing; rings mirror target-hover state.
   Per Q3: `BindHitZone(pairedTarget: bar)` → `pairedTarget: ring`,
   rename-only.
4. **Palette single source of truth.** `CombatBarPalette` (guid
   `c612067c785d2ee43a5aa28c2e64e6d9`) is reused verbatim by rings; all
   SubBar\* fields consumed unchanged. `SubBarMarkerRing` (Slice 2a
   addition) drives ring outline baseline.
5. **Hover / click surface via `ICombatHoverTarget`.** Rings implement
   this interface (`SlotTargetRing.cs:32-35`) — same contract
   `SubsystemBar` served for `_combatHitTargets` registration.
6. **Anchor positions.** `VehicleHudAnchors._entries` RectTransform
   positions are load-bearing per `project_hud_anchors_slice_26` — Slice
   2b authors rings AT these anchors, not new positions. `SeedHudAnchor`
   never touches anchor RectTransforms; designer positional edits
   survive.

## Load-Bearing Contracts That Retire

Per TD verdict Q1:

- **Tooltip ownership.** Ring surrenders. `VehiclePartHitZone` is sole
  tooltip source per slot (already primary — W7.27 decision). Drop
  `_tooltip` + `_tooltipKey` fields from `SlotTargetRing.cs`; drop them
  from `Bind` signature; remove `_tooltip.Show/Hide` calls from
  `OnPointerEnter/OnPointerExit`. Ring keeps `OnHover` firing (hover
  mirror) and `_interactable` raycast toggle (drag-cast surface).

Per TD verdict Q2:

- **Fill fraction / radial preview arc.** DEFERRED to Slice 3. 2b ships
  band-color-only. `_previewArcImage` + `_targetHoverOutlineImage`
  remain authored-but-inactive on the prefab per
  `feedback_data_flag_lagging_dependency` (dormant serialized capacity,
  not a bridge).

Per TD verdict Q5:

- **MainBar-c category deferral.** `MainBarWidget` persists unchanged
  serving the structural slot. Slice 2b closes the per-slot-widget
  category (Engine/Weapon/Mobility/Exposable). Slice 3 closes the
  structural category (MainBar-c = ring-on-chassis + armor pill split).
  Categorical scope narrowing per ADR-0015 — not a bridge.

## Non-Goals (enforced for Slice 2b)

Copied verbatim from TD verdict §Non-Goals:

1. No MainBar-c changes — structural slot untouched.
2. No radial fill / preview arc wiring — Slice 3.
3. No `_targetHoverOutline` wiring — Slice 3 with drag-cast integration.
4. No `_combatHitTargets` list reordering — Q3 is rename-only.
5. No new tooltip overloads — Q1 removes tooltip ownership from rings.
6. No armor pill / MainBar visual work — Slice 3.
7. No player-smooth / enemy-jagged outline sprite work — Slice 3
   variants.
8. No `CombatBarPalette` changes.
9. No animation / tween — instant color flip on Refresh (same as 2a).

## Post-Swap Verification (from TD verdict §Validation Criteria)

- Grep for `SubsystemBar` / `SubsystemMarker` across `Wasteland
  Run/Assets/**` returns zero matches (except archived captures).
- All 7 vehicle prefabs render rings in Prefab Mode at authored anchor
  positions (no drift).
- 13 ring tests + full EditMode suite green.
- PlayMode: hovering a wheel / engine / weapon lights the corresponding
  ring's paired-target state; drag-cast lands on the intended slot;
  tooltip shows once per hover (no double).
- No visual regression on structural hit zone / MainBar (untouched).

## Technical Director Review

Full verdict:
`production/td-verdicts/2026-07-03-slot-target-ring-slice-2b.md`

**Verdict: APPROVE-WITH-CHANGES.** Six binding decisions govern
Slice 2b:

- **Q1 — Tooltip payload ownership:** Ring surrenders tooltip ownership;
  `VehiclePartHitZone` is sole tooltip source per slot. Drop `_tooltip`
  + `_tooltipKey` from `SlotTargetRing.cs`.
- **Q2 — Fill fraction vs band color:** Band color alone in 2b. Radial
  fill deferred to Slice 3.
- **Q3 — VehiclePartHitZone paired target:** Zone→ring proxy is
  rename-only. No `_combatHitTargets` reorder.
- **Q4 — Author-time destructive replacement:** Atomic
  `DestroyImmediate` of stale nested children in `SeedHudAnchor`,
  name-scoped to `SubsystemBar` / `SubsystemMarker` (deterministic
  prefab-instance names). Anchor RectTransform itself never touched —
  designer positional edits survive.
- **Q5 — Ring at structural slot?** DEFER MainBar-c to Slice 3. MainBar
  is a distinct widget category, not a bridge over `SubsystemBar`.
  Categorical scope narrowing per ADR-0015.
- **Q6 — Order of operations:** 13-step sequence with the compiler as
  the safety net. Compilation breaks on every stale bar/marker type ref
  after step 3, forcing complete conversion before green.

Prior verdicts:

- `production/td-verdicts/2026-07-01-slot-target-ring-refactor.md`
  (multi-slice)
- `production/td-verdicts/2026-07-02-slot-target-ring-widget.md`
  (Slice 2a non-goals)

Files at risk beyond the obvious retirements: font material `RussoOne
SDF - SubsystemBar HpText.mat` (SubsystemBar-only consumer, delete) and
`BuffTooltipWidget.Show(pos, key, name, DamageState, info)` 5-arg
overload (SubsystemBar's `.cs:381` was the sole caller — delete or
document why it stays). Dormant `_previewArcImage` +
`_targetHoverOutlineImage` on `SlotTargetRing` stay authored-but-inactive
per `feedback_data_flag_lagging_dependency` (Slice 3 lights them up).

**Proceed with 13-step Q6 sequence after running the Slice 2a author
menu to seed `SlotTargetRing.prefab` on disk.** No Edit/Write until the
user approves this capture.
