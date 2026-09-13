# Capture — P4a slice 2: EnergyOrbWidget migration

**Date:** 2026-09-13
**System:** Combat HUD — UGUI → UI Toolkit (ADR-0018 P4a)
**Files at risk:** `Assets/Scripts/CombatView/EnergyOrbWidget.cs` (deleted),
`Assets/Prefabs/CombatView/EnergyOrb.prefab` (deleted),
`Assets/Editor/CombatPrefabAuthor.cs` (`AuthorEnergyOrb` + menu deleted),
`Assets/Scripts/CombatView/CombatHud.cs` (rewired),
`Assets/Prefabs/CombatView/CombatHud.prefab` (nested instance excised)
**Extended:** `Assets/UI/CombatHudPanel.uxml` + `.uss`,
`Assets/Scripts/UI/CombatHudPanelController.cs`

Second widget into the tree stood up by slice 1. No new panel, no new prefab,
no new author file — which is the whole point of having paid for slice 1.

## Authored values being destroyed — complete enumeration

### `EnergyOrb.prefab`

| Property | Authored value |
|---|---|
| `sizeDelta` | **110 × 110** (circular) |
| background | circular sprite — `SharedSprites.Circle()` placeholder, or designer art |
| `fontSize` | **38** |
| `fontStyle` | **Bold** |
| alignment | **Center** |

### Runtime state colours — `EnergyOrbWidget.cs`

| Constant | Value |
|---|---|
| `FullBgColor` | **RGBA(0.55, 0.30, 0.05, 0.95)** — dark amber |
| `EmptyBgColor` | **RGBA(0.30, 0.10, 0.10, 0.95)** — muted red |
| `FullTextColor` | **RGBA(1.00, 0.92, 0.55, 1)** |
| `EmptyTextColor` | **RGBA(0.85, 0.55, 0.55, 1)** |

### Placement — `CombatHud`

| Constant | Value |
|---|---|
| `EnergyOrbAnchoredPos` | **(−720, 85)** |
| anchor / pivot | bottom-centre, pivot (0.5, 0.5) |

Designer-baked per W7.22: the orb and pile chips were deliberately broken out of
the EndTurn/hand-width symmetry to claim their own real estate at the screen
edges. These are literal offsets, not formulas — do not "restore" a relationship
to the hand width.

### Behaviour being preserved

- Text is `"{cur}/{max}"`.
- Empty (`cur <= 0`) swaps BOTH background and text colour — the muted red means
  "no plays possible until next turn", and it is the only signal for that state.
- **Hidden entirely during `Setup` and `Ended`**, visible otherwise. Energy only
  matters on the player's turn.

`EnergyOrbWidget.Awake` also re-assigned a placeholder circle sprite if the
prefab's was null. That disappears with the widget and needs no replacement: a
USS `border-radius` is not a sprite reference and cannot be accidentally cleared
by a prefab edit. The repair existed because the value could go missing; in USS
it cannot.

### Dying with it

`CombatHud._energyOrbDiameterPx` — written from the prefab's `sizeDelta` on Awake
(`:443`) and **never read anywhere**. Same shape as `_endTurnWidthPx/HeightPx` in
slice 1.

## Nested-instance excision — expected, second occurrence

`_energyOrb: {fileID: 4615983946678217487}` carries **no `guid:`**, which is the
tell for a local/nested reference. So `EnergyOrb.prefab` is instantiated inside
`CombatHud.prefab` and deleting the source would leave a broken instance.

Removing it means excising three things, per the slice-1 lesson now recorded in
`feedback_nested_prefab_topology_orphans_overrides`:
1. the `PrefabInstance` block (source guid `b93b9f6916d0731409c1e9f308534359`),
2. every `stripped` block naming that instance,
3. the orphaned `m_Children` entry in the parent transform.

Done highest-line-first against a scratchpad backup, with every seam verified
before any test run.

## Technical Director Review

No TD agent spawned. This executes the pattern approved and proven in slice 1
(`9b4c848`), under ADR-0018 P4a, with no new architectural decisions:

- one shared `CombatHudPanel` tree rather than a panel per widget,
- document sortingOrder −10 so the outcome overlay still covers the HUD,
- `WastelandRun.UI` names no `CombatView` type — this widget needs only
  `CombatLoop` (`Phase`, `CurrentEnergy`, `MaxEnergy`), all of which live in
  `WastelandRun.Combat`, so no injected predicate is required this time.

The one judgement recorded: the `Awake` sprite-repair is dropped rather than
ported, because its failure mode does not exist in USS.

## Merge conditions

EditMode ≥1287 with one `[Explicit]` skip, PlayMode ≥17, zero `error CS`, both
result XMLs present, grep-gates clean.

**Playtest:** orb sits bottom-left of the hand, round, reads `cur/max`; turns
muted red at 0 energy and amber otherwise; **disappears during Setup and after
combat ends**; and still renders above the vehicles and below the outcome
overlay.

## Approval

User approved continuous execution of P4a on 2026-09-13 ("keep going until we
are done").
