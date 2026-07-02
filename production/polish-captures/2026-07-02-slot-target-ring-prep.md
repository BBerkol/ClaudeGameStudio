# SlotTargetRing Refactor — Slice 1 Prep Capture — 2026-07-02

## Purpose

Bake all load-bearing values from `SubsystemBar`, `SubsystemMarker`,
`MainBarWidget`, and `CombatBarPalette` before Slice 2-3 destructive edits
retire those three widgets in favour of a single circular `SlotTargetRing`.
Per project rule `feedback_capture_before_destroy_view_layer`, every authored
value and semantic contract that must survive the swap is enumerated here so
Slice 2 can author from a single source of truth rather than reverse-engineering
disk state.

## Slice 2a Non-Goals (TD-enforced)

Slice 2a authors `SlotTargetRing.cs` + `SlotTargetRing.prefab` only. Per
the TD verdict at `production/td-verdicts/2026-07-02-slot-target-ring-widget.md`,
the following surfaces are **explicitly not touched** in 2a:

- No mount in `AuthorPlayerVehicle` / `AuthorEnemyArchetypePrefabs` /
  `AuthorCombatPrefab`
- No changes to `BuildVehicleHudAnchors` / `SeedHudAnchor` / `SeedMainBarAnchor`
- No changes to `VehicleBarStack.BuildPerSlotBars`
- No deletion of `SubsystemBar` / `SubsystemMarker` code or prefabs
- `VehicleHudAnchors.ResolveRing(slotId)` may ship as a dormant sibling

This discipline keeps Slice 2b atomic per ADR-0011. Files listed under
"Files at Risk" below apply to Slice 2b-3, not 2a.

## Files at Risk (Slice 2-3 destructive scope)

- `Assets/Scripts/CombatView/SubsystemBar.cs` — **DELETE** after ring ships
- `Assets/Scripts/CombatView/SubsystemMarker.cs` — **DELETE** after ring ships
- `Assets/Scripts/CombatView/MainBarWidget.cs` — **DELETE** after MainBar-c pill lands
- `Assets/Scripts/CombatView/VehicleBarStack.cs` — bind loop rewrite (drives rings, not bars)
- `Assets/Scripts/CombatView/VehicleHudAnchors.cs` — anchor catalog reused as-is
- `Assets/Scripts/CombatView/RestPickerController.cs` — hover routing update
- All vehicle prefabs (Scout / Assault / Truck / Dune / IronOx / Dredge) — re-author to mount rings + armor pill instead of bar+marker+MainBar
- All test surface referencing `SubsystemBar` / `SubsystemMarker` / `MainBarWidget` — migrate to canonical ring APIs (no bridge shims per ADR-0011)

## Palette Hex Values

From `CombatBarPalette.cs` (single source of truth — rings reuse these directly):

| Field | RGBA | Purpose |
|-------|------|---------|
| `SubBarBackground` | `0.22, 0.22, 0.24, 0.95` | Ring interior dim fill (Offline state) |
| `SubBarHpGreen` | `0.40, 0.85, 0.40, 1.00` | Damage band > 0.80 |
| `SubBarHpYellow` | `0.95, 0.85, 0.30, 1.00` | Damage band > 0.40 |
| `SubBarHpRed` | `0.90, 0.30, 0.25, 1.00` | Damage band ≤ 0.40 |
| `SubBarPreview` | `1.00, 0.55, 0.10, 1.00` | Hover-projected damage arc |
| `SubBarMarkerRing` | `1.00, 1.00, 1.00, 0.85` | Ring outline baseline (Slice 2a addition — was inline on SubsystemMarker) |
| `SubBarHoverOutline` | `1.00, 0.85, 0.10, 1.00` | Target-hover halo colour |
| `MainBarArmor` | `0.80, 0.80, 0.82, 1.00` | Armor pill fill (MainBar-c) |
| `MainBarHp` | `0.40, 0.85, 0.40, 1.00` | Chassis ring baseline (matches band-green) |
| `MainBarBackground` | `0.05, 0.05, 0.06, 0.85` | Armor pill / chassis backdrop |
| `MainBarHoverOutline` | `1.00, 0.85, 0.10, 1.00` | Main-bar target-hover outline highlight |

## Damage-State Thresholds

From `SubsystemBar.ColorForBand` (line reference: ratio computed as `curHp / maxHp`):

- `ratio > 0.80` → `HpBandGreen`
- `ratio > 0.40` → `HpBandYellow`
- `ratio ≤ 0.40` → `HpBandRed`

Upper bounds are exclusive. Offline (`curHp == 0` + `IsOnline == false`) is a
distinct state — rings render as `MainBarBg` fill with `HpBandRed` outline dim
(spec sheet defines exact treatment).

## HideRule Mapping

From `VehicleBarStack.ResolveHideRule` + `BuildPerSlotBars`:

| Source (SlotDefinition) | Combat mode | Rest mode | Ring behavior |
|-------------------------|-------------|-----------|---------------|
| `AttackStateGated` | `HideOnFullUnlessAttackActive` | `AlwaysVisible` | Ring always visible; outline treatment shifts on attack focus |
| `DamagedOnly` | `HideOnFullOrDestroyed` | `AlwaysVisible` | Ring visible when damaged OR when hovered; hidden at full+idle |
| `AlwaysVisible` | `AlwaysVisible` | `AlwaysVisible` | Ring always visible |

**Slot exclusions** (skipped by `BuildPerSlotBars` — ring is NOT authored for these):
- `IsStructural == true` (hull chassis in MainBar-c gets ring; frame does not)
- `SlotKind.Armor` (armor is buffer, not a slot — see `project_armor_not_subsystem`; MainBar-c ships armor as horizontal pill above chassis, not a ring)

## Widget Geometry Constants

From `SubsystemBar` / `SubsystemMarker` / `MainBarWidget`:

- `SubsystemBar._pixelsPerHp = 4f`
- `SubsystemMarker._circleDiameterPx = 10f`
- `MainBarWidget._pixelsPerHp = 4f`
- `MainBarWidget._targetHoverOutsetPx = 4f`
- `MainBarWidget._hpPulseDurationSec = 0.4f` (damage pulse envelope)

**Ring geometry (new, defined in spec sheet)** — outer 40px / outline 3px /
icon 28px / preview arc CW from 12 o'clock. Values chosen to visually match
current bar+marker footprint on all authored vehicle anchors so no re-anchor
pass is required.

## Semantic Contracts to Survive

These behaviors must carry forward — the widget changes shape, not meaning:

1. **Hover preview clamp** — projected damage arc clamps at `curHp` (never
   over-draws past empty); matches current `SubsystemBar._previewFill` behavior.
2. **Tooltip suppression** — no tooltip when slot is `IsStructural` and
   MainBar owns the chassis display (MainBar-c ships chassis as its own ring).
3. **OfflineConsequenceText dispatch** per `SlotKind`
   (Hull/Weapon/Engine/Mobility/Armor/Exposable) — reused verbatim from
   `VehicleBarStack.OfflineConsequenceText`.
4. **Damage pulse envelope** — `Sin(π·t)` over 0.4s on `curHp` decrement;
   defer to Slice 3 or later per spec sheet (not required for Slice 2 ship).
5. **Armor overlay behavior** (MainBar-c) — pill width = `curArmor × 4`,
   hidden when `curArmor == 0`, palette color `MainBarArmor`.
6. **`HideOnFullUnlessAttackActive` → attack-active override** — bar becomes
   visible when `AttackStateController.IsHoveringTarget(this)`; ring inherits
   this via `ICombatHoverTarget` implementation on the ring MB.

## Values NOT Captured Here

Per-vehicle anchor positions live on each vehicle prefab's `VehicleHudAnchors`
component (designer-authored in Prefab Mode; see
`project_hud_anchors_slice_26`). Those RectTransform positions survive the
widget swap because Slice 2 authors rings AT the same anchors — the entry map
(`SlotId → Anchor`) is the load-bearing artifact and stays intact.

## Post-Swap Verification

After Slice 3 lands, `Author Combat Prefab` should produce a Combat.prefab where:
- Every non-structural slot renders a `SlotTargetRing` at its `VehicleHudAnchors` entry
- MainBar renders as an armor pill above chassis + a ring on the chassis slot
- No `SubsystemBar` / `SubsystemMarker` / `MainBarWidget` MonoBehaviours remain
  in the scene tree (grep gate)
- Combat playtest: bar-visibility semantics unchanged from pre-swap (rings hide/show
  under the same conditions bars did)
- Rest playtest: all rings visible on entry (per `BindForRest` override)

## Technical Director Review

Ring refactor Slice 1 = non-destructive prep only (capture + spec sheet). No
code touched, no prefabs re-authored, no tests migrated. Full TD verdict for
the multi-slice refactor lives at
`production/td-verdicts/2026-07-01-slot-target-ring-refactor.md` and remains
authoritative for Slices 2-8.

Forward-pressure precondition (TD verdict line 14: "AFTER forward-pressure
ships") is satisfied by Node Map V3 fuel-as-clock GDD landing 2026-07-02
(commits `dedfbf7` + `b3b4d4a`). Path D locked; ring refactor is unblocked.

**Verdict**: APPROVE Slice 1. Proceed with capture + spec sheet writes, then
pause for user review before authoring the ring widget in Slice 2.
