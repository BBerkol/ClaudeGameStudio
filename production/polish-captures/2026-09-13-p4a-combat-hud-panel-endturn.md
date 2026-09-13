# Capture — P4a slice 1: Combat_HUD panel + EndTurnButton migration

**Date:** 2026-09-13
**System:** Combat HUD — UGUI → UI Toolkit (ADR-0018 P4, split as P4a)
**Files at risk:** `Assets/Scripts/CombatView/EndTurnButton.cs` (deleted),
`Assets/Prefabs/CombatView/EndTurnButton.prefab` (deleted),
`Assets/Editor/CombatPrefabAuthor.cs` (`AuthorEndTurnButton` deleted),
`Assets/Scripts/CombatView/CombatHud.cs` (rewired)
**New:** `Assets/UI/CombatHudPanel.uxml` + `.uss`,
`Assets/Scripts/UI/CombatHudPanelController.cs`,
`Assets/Editor/AuthorCombatHudPanel.cs`,
`Assets/Prefabs/UI/CombatHudPanel.prefab`

The author lives in its own `AuthorCombatHudPanel.cs` rather than inside the
9,450-line `CombatPrefabAuthor.cs`, matching the existing convention for UI
Toolkit roots (`AuthorDialogueSceneRoot.cs`, `AuthorRunHUDHost.cs`) and keeping
the UGUI author script on its path to shrinking rather than growing.

## Why this slice, and why this shape

P4 as scoped in ADR-0018 is the largest and highest-risk item in the plan. It
was split:

- **P4a — screen-space Combat_HUD.** Cards, EndTurn, EnergyOrb, pile chips +
  popup, turn banner, ambush banner, crosshair. 25 existing UI Toolkit
  controllers of precedent.
- **P4b — `HudAnchors`** (MainBar, rings, badges, BuffStrip). World-anchored,
  and **the project has zero UI Toolkit prior art for world-anchored
  positioning** — verified: nothing under `Assets/Scripts/UI/` references
  `RuntimePanelUtils`, `ScreenToPanel` or `WorldToScreenPoint`. Gated on a
  measurement spike per ADR-0018's open question.

The split keeps the risky half from holding the tractable half hostage, and
keeps P4a away from the ring/badge/tooltip surfaces tuned across four playtests
on 2026-09-12–13.

**Why a shared panel rather than a per-widget one.** P3 migrated two
*self-contained overlays*, each naturally its own `UIDocument`. The Combat_HUD
widgets are children of **one** canvas, so their 1.0 shape is one panel with one
UXML tree. Giving EndTurn its own panel would be throwaway scaffolding that P4a
later collapses — precisely what `feedback_demo_forward_over_infrastructure`
forbids. So slice 1 stands up the panel and migrates one widget into it;
subsequent widgets are additive elements in the same tree and cost far less.

**Why EndTurnButton is the canary.** 59 lines, two visual states, one action,
permanently on screen, and clicking it every turn makes a regression
immediately obvious. It also exercises UI Toolkit **input routing**, which
ADR-0014 listed as unverified (`:40` Verification Required, item b).

## Authored values being destroyed — complete enumeration

### `EndTurnButton.prefab` — root

| Property | Authored value |
|---|---|
| `sizeDelta` | **200 × 80** |
| `anchorMin` / `anchorMax` | (0.5, 0.5) / (0.5, 0.5) |
| `anchoredPosition` | (0, 0) |
| `pivot` | (0.5, 0.5) |
| `Image.color` | **RGBA(0.20, 0.18, 0.15, 1)** — inactive background |
| `Image.raycastTarget` | true |

### `EndTurnButton.prefab` — `Label` child

| Property | Authored value |
|---|---|
| anchors | full-rect: min (0,0), max (1,1), offsets 0 |
| font | `TMP_Settings.defaultFontAsset` |
| `fontSize` | **22** |
| `fontStyle` | **Bold** |
| `alignment` | **Center** |
| `color` | **RGBA(0.55, 0.50, 0.45, 1)** — inactive label |
| `text` | **"END TURN"** |
| `raycastTarget` | false |

### Runtime state colours — `EndTurnButton.cs`

| Constant | Value |
|---|---|
| `ActiveBg` | **RGBA(0.55, 0.45, 0.30, 1)** |
| `InactiveBg` | RGBA(0.20, 0.18, 0.15, 1) |
| `ActiveLabel` | **RGBA(1.00, 0.95, 0.85, 1)** |
| `InactiveLabel` | RGBA(0.55, 0.50, 0.45, 1) |

### Placement inside `Combat_HUD` — `CombatPrefabAuthor`

| Constant | Value |
|---|---|
| `EndTurnXPx` | **765** |
| `EndTurnYPx` | **100** |
| anchor | bottom-centre (mirrors EnergyOrb across the vertical centreline) |

Every value above is reproduced in `CombatHudPanel.uss` so the migration is
visually identity-preserving. Any deliberate change is a separate commit.

### Behaviour being preserved

Two gates collapse to one visible state — `Phase == PlayerTurn` **AND**
`!HandSequencer.IsRunning`. The sequencer gate is a structural cooldown, not a
timer: spam-clicking during the post-EndTurn discard/draw window otherwise makes
the model see a second EndTurn before the first turn's draws have settled. Both
gates are re-applied in the controller, and the click path re-checks them.

## Known risk — panel vs canvas layering

A `UIDocument` panel and a UGUI `Canvas` do not share a sorting space cleanly:
panel depth comes from `PanelSettings.sortOrder`, canvas depth from
`Canvas.sortingOrder`. The combat screen currently has canvases at 10, 15, 20,
22, 25, 30, 60 and 110.

This is the same class of defect that bit twice on 2026-09-12 (`overrideSorting`
inert on nested canvases; `IntentCanvas` at 5 sitting under `HitZonesCanvas` at
15). It is called out here so the playtest checks it deliberately rather than
discovering it later: **the End Turn button must render above the vehicles and
below the outcome overlay.**

## Technical Director Review

No TD agent spawned for this slice. It executes ADR-0018 P4 as written, under
the P4a/P4b split, and the governing constraints are that ADR's:

- P4 is HIGH risk and requires exhaustive capture-before-destroy plus a full
  playtest before merge (ADR-0014 `:247`, carried into ADR-0018).
- `HudAnchors` classification is an open question to be answered with numbers,
  not assumed — hence P4b is gated and not attempted here.
- The registry members (`Popups`, `HitZonesCanvas`, `IntentCanvas`,
  `TargetingReadoutCanvas`) stay UGUI and are untouched by this slice.

The one judgement added here is the shared-panel-over-per-widget-panel decision,
justified above on 1.0-shape grounds; it is recorded rather than silently taken.

## Merge conditions

EditMode ≥1287 with one `[Explicit]` skip, PlayMode ≥17, zero `error CS`, both
result XMLs present, grep-gates clean.

**Playtest:** button reads "END TURN"; dims outside the player's turn and
brightens during it; click ends the turn; spam-clicking during the draw window
does nothing and produces no model warnings; and it renders above the vehicles
and below the outcome overlay.

## Approval

User approved proceeding with P4a on 2026-09-13 ("go as you recommend").
